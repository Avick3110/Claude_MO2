"""MCP tools for record-level queries, field interpretation, and conflict detection.

Field interpretation for `mo2_record_detail` is routed through mutagen-bridge.exe
(Mutagen-backed) as of v2.0.0. The v1.x esp_schema/esp_fields schema walker was
retired because it had known limitations (VMAD fragments, localized strings,
union deciders) that Mutagen handles correctly.

Index queries (query_records, conflict_chain, plugin_conflicts, conflict_summary)
stay in Python — they're dict lookups on the cached index, no field interpretation
needed.
"""

from __future__ import annotations

import json
import os
import re
import subprocess
import threading
import time
import traceback
from pathlib import Path
from typing import Any

import mobase
from PyQt6.QtCore import qInfo, qWarning

from .config import PLUGIN_NAME
from .esp_index import LoadOrderIndex, make_formid_key
from .tools_modlist import scan_missing_masters

# Bridge `scan` command batch size (Phase 3). 100 plugins per subprocess
# call amortises the ~1.3s .NET CLR + Mutagen JIT startup cost across
# more work — at 30/batch the initial Phase 3 force_rebuild on Aaron's
# 3350-plugin modlist took 154s of which ~143s was subprocess overhead
# (112 invocations × ~1.27s). 100/batch cuts invocations to ~34 and
# brings the total estimate to ~68s for force_rebuild. Phase 4's
# freshness-check incremental rebuilds will scan 0-5 plugins per
# event, well under one batch.
#
# Future iteration (Phase 4+): batches are currently random-by-load-order,
# so a batch that happens to contain Skyrim.esm + Update.esm + DLCs
# produces a much larger response than one of small ESPs. Variable-sized
# batches targeting a fixed *record count* (rather than fixed *plugin
# count*) would balance better. Not urgent — current shape works.
_SCAN_BATCH_SIZE = 100


# ── Shared State ────────────────────────────────────────────────────────

_index: LoadOrderIndex | None = None
_build_lock = threading.Lock()
_build_status: dict[str, Any] = {'state': 'idle'}

# Cleared while a build is in progress, set when idle. Record-query handlers
# wait on this so they never serve stale data between an MO2 refresh and the
# subsequent re-index completing.
_build_complete = threading.Event()
_build_complete.set()

# onRefreshed hook is registered exactly once per server process. Guarded
# because _register_record_tools re-runs on every server start/stop cycle.
_refresh_hook_registered = False

# Module-level organizer reference stashed during register_record_tools so
# the debounced-rebuild timer callbacks (which are invoked from threads
# without captured closures over `organizer`) can still route through MO2's
# VFS resolver. v2.6.0 Phase 2.
_organizer = None

# v2.6.0 Phase 3: plugin directory + bridge scan_fn closure stashed at
# register time so debounced-rebuild paths (which run on timer threads
# with no closure over the registration scope) can rebuild the index
# via the bridge without re-resolving the bridge binary path each time.
# Same pattern as `_organizer`.
_plugin_dir: Path | None = None
_index_scan_fn: Any = None

# Populated by the onRefreshed callback; surfaced via mo2_record_index_status
# so Claude can see whether the latest rebuild was auto-triggered.
_last_auto_refresh: dict[str, Any] | None = None

# How long a record query blocks waiting for an in-progress rebuild before
# returning an error. Single-threaded HTTPServer means one slow handler stalls
# every request, so don't exceed typical MCP client timeouts.
_BUILD_WAIT_TIMEOUT_S = 30.0

# How long trigger_refresh_and_wait_for_index blocks for the post-write
# refresh + rebuild to complete. One MO2 refresh cycle on a large modlist
# (~3300 plugins) is ~17-25s, plus a ~0.5s debounce and a ~10-15s rebuild.
# 60s gives headroom for modlists up to roughly 5000 plugins. Kept separate
# from _BUILD_WAIT_TIMEOUT_S so read-query blocking stays snappy.
_WRITE_REFRESH_TIMEOUT_S = 60.0

# MO2 holds plugin/mod state mutations in memory; plugins.txt and loadorder.txt
# are not flushed synchronously. Event callbacks (onPluginStateChanged,
# onModMoved) fire before disk flush, so rebuilding immediately would re-read
# the pre-change state. A short debounce gives MO2 time to flush, AND
# coalesces bursts (multi-select drag -> N onModMoved fires) into one rebuild.
_STATE_FLUSH_DELAY_S = 0.5

# Debounce timer: fresh events cancel-and-replace this, so the rebuild fires
# _STATE_FLUSH_DELAY_S after the LAST event in a burst, not after the first.
_rebuild_timer: threading.Timer | None = None
_rebuild_timer_lock = threading.Lock()

# Flag set when a state-change event fires during an active rebuild. The
# active rebuild can't include those new changes, so the build thread chains
# a fresh rebuild in its finally block. Queries stay blocked across the
# chain -- no stale data released between builds.
_rebuild_pending_during_build = False


def _get_index() -> LoadOrderIndex | None:
    """Return the current index without waiting. Used by status/diagnostic
    handlers that should report live state even mid-build."""
    return _index


def _get_index_fresh(timeout_s: float = _BUILD_WAIT_TIMEOUT_S) -> tuple[LoadOrderIndex | None, bool]:
    """Return (index, timed_out). If a build is in progress, blocks up to
    timeout_s for it to complete so callers never see a stale index after an
    auto-triggered rebuild. `timed_out` is True only if the wait expired."""
    completed = _build_complete.wait(timeout=timeout_s)
    return _index, not completed


def _find_bridge_for_read(plugin_dir: Path) -> Path | None:
    """Find mutagen-bridge.exe. Same search order as tools_patching._find_bridge,
    duplicated here to keep tools_records free of tools_patching imports.
    Spooky-named paths remain as a one-release shim for v2.5.x installs."""
    candidates = [
        plugin_dir / "tools" / "mutagen-bridge.exe",
        plugin_dir / "tools" / "mutagen-bridge" / "mutagen-bridge.exe",
        plugin_dir / "tools" / "spooky-bridge.exe",
        plugin_dir / "tools" / "spooky-bridge" / "spooky-bridge.exe",
    ]
    for path in candidates:
        if path.is_file():
            return path
    return None


# ── Bridge-scan plumbing for the record index (Phase 3) ────────────────

def _run_bridge_scan(bridge: Path, plugin_paths: list[str], timeout: int = 120) -> dict:
    """Invoke the bridge's `scan` command with one batch of plugin paths.

    Returns the parsed JSON response dict (`{'success': bool, 'plugins':
    [...], 'error': str | None}`). On subprocess failure, synthesises an
    error response so callers don't have to special-case exceptions.

    UTF-8 forced on stdin/stdout — verified clean end-to-end through the
    bridge in PHASE_3_HARNESS_OUTPUT (Unicode-path round-trip section).
    """
    request = {'command': 'scan', 'plugins': plugin_paths}
    try:
        proc = subprocess.run(
            [str(bridge)],
            input=json.dumps(request),
            capture_output=True,
            text=True,
            encoding='utf-8',
            timeout=timeout,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
        )
    except subprocess.TimeoutExpired:
        return {
            'success': False,
            'error': f'mutagen-bridge scan timed out after {timeout}s',
            'plugins': [],
        }
    except FileNotFoundError:
        return {
            'success': False,
            'error': f'Bridge exe not found: {bridge}',
            'plugins': [],
        }
    except Exception as exc:
        return {
            'success': False,
            'error': f'Failed to run bridge scan: {type(exc).__name__}: {exc}',
            'plugins': [],
        }

    out = (proc.stdout or '').strip()
    if not out:
        return {
            'success': False,
            'error': f'Bridge returned no output (exit {proc.returncode})',
            'plugins': [],
            'stderr': (proc.stderr or '').strip()[:500],
        }
    try:
        return json.loads(out)
    except json.JSONDecodeError as exc:
        return {
            'success': False,
            'error': f'Bridge returned invalid JSON: {exc}',
            'plugins': [],
            'raw_output': out[:500],
        }


def _make_index_scan_fn(bridge: Path, batch_size: int = _SCAN_BATCH_SIZE):
    """Build the scan_fn closure passed into LoadOrderIndex.build().

    Batches `plugin_paths` into chunks of `batch_size`, invokes the bridge
    once per chunk, and concatenates the per-plugin results. Each
    ScannedPlugin dict carries its own `error` field, so per-plugin
    failures don't void the whole batch.
    """
    def scan(plugin_paths: list[str]) -> list[dict]:
        results: list[dict] = []
        if not plugin_paths:
            return results
        for i in range(0, len(plugin_paths), batch_size):
            batch = plugin_paths[i:i + batch_size]
            response = _run_bridge_scan(bridge, batch)
            batch_plugins = response.get('plugins') or []
            if not batch_plugins and response.get('error'):
                # Synthesise per-plugin error entries so the caller can
                # surface them in its `errors` list. Without this, a
                # subprocess-level failure would silently drop a whole
                # batch with no per-plugin trace.
                err = response['error']
                batch_plugins = [
                    {'plugin_path': p, 'error': f'batch failure: {err}'}
                    for p in batch
                ]
            results.extend(batch_plugins)
        return results

    return scan


# ── Tool Registration ──────────────────────────────────────────────────

def register_record_tools(registry, organizer) -> None:
    """Register all record-level query tools with the MCP tool registry."""

    global _organizer, _plugin_dir, _index_scan_fn
    _organizer = organizer

    plugin_dir = Path(__file__).resolve().parent
    _plugin_dir = plugin_dir
    base_path = organizer.basePath()
    profile_path = organizer.profile().absolutePath()
    plugin_list = organizer.pluginList()
    mod_list = organizer.modList()

    # Resolve the bridge once at register time. If it's missing now, we
    # surface that on the first build attempt rather than at every event
    # callback. The closure caches the (bridge, batch_size) binding.
    bridge = _find_bridge_for_read(plugin_dir)
    _index_scan_fn = _make_index_scan_fn(bridge) if bridge else None

    # ── mo2_record_index_status ─────────────────────────────────────

    registry.register(
        name="mo2_record_index_status",
        description=(
            "Check whether the record index is built. Returns stats "
            "(plugin count, record count, conflict count) and build state. "
            "Also reports any enabled plugins with missing masters — a "
            "blocker condition that breaks the game at load time. "
            "If the index isn't built, call mo2_build_record_index first."
        ),
        input_schema={
            "type": "object",
            "properties": {},
        },
        handler=lambda args: _handle_index_status(plugin_list),
    )

    # ── mo2_build_record_index ──────────────────────────────────────

    registry.register(
        name="mo2_build_record_index",
        description=(
            "Build or rebuild the record index by scanning all plugins in "
            "the active load order. This indexes every record's FormID, "
            "EditorID, and type, and detects conflicts. Takes ~10-15 seconds "
            "for the full load order. Runs in the background; poll "
            "mo2_record_index_status to check progress."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "force_rebuild": {
                    "type": "boolean",
                    "description": "Ignore cache and re-scan everything (default false)",
                    "default": False,
                },
            },
        },
        handler=lambda args: _handle_build_index(
            args, base_path, profile_path, plugin_list,
            organizer=organizer,
        ),
    )

    # ── mo2_query_records ───────────────────────────────────────────

    registry.register(
        name="mo2_query_records",
        description=(
            "Search records in the index. Supports filtering by plugin name, "
            "record type (e.g. ARMO, WEAP, NPC_), editor ID substring, or "
            "specific FormID. Returns FormID, EditorID, type, winning plugin, "
            "and override count. Paginated. By default filters to enabled "
            "plugins only (what the game actually loads); pass "
            "include_disabled=true for diagnostic queries that include "
            "records from disabled plugins."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "plugin_name": {
                    "type": "string",
                    "description": "Filter to records from this plugin",
                },
                "record_type": {
                    "type": "string",
                    "description": "Filter by record type (ARMO, WEAP, NPC_, etc.)",
                },
                "editor_id_filter": {
                    "type": "string",
                    "description": "Substring match on Editor ID",
                },
                "formid": {
                    "type": "string",
                    "description": "Exact FormID lookup: 'PluginName:LocalID' (e.g. 'Skyrim.esm:012E49')",
                },
                "limit": {
                    "type": "integer",
                    "description": "Max results (default 50)",
                    "default": 50,
                },
                "offset": {
                    "type": "integer",
                    "description": "Skip this many results (default 0)",
                    "default": 0,
                },
                "include_disabled": {
                    "type": "boolean",
                    "description": (
                        "Include records from plugins whose checkbox is off "
                        "in MO2's right pane. Default false (enabled-only)."
                    ),
                    "default": False,
                },
            },
        },
        handler=lambda args: _handle_query_records(args),
    )

    # ── mo2_record_detail ───────────────────────────────────────────

    registry.register(
        name="mo2_record_detail",
        description=(
            "Get full interpreted field data for a specific record. "
            "Provide a FormID ('Skyrim.esm:012E49') or Editor ID. "
            "By default returns the winning record among enabled plugins; "
            "specify plugin_name to get a specific plugin's version, or "
            "plugin_names (plural) to fetch the record from multiple "
            "plugins in one call (useful for diffing a conflict chain). "
            "Pass include_disabled=true to resolve against disabled "
            "plugins too (needed if the record only exists in or is "
            "being fetched from a plugin whose checkbox is off in MO2). "
            "Returns all fields with named values, enum labels, flag "
            "names. Set resolve_links=true to annotate FormID strings "
            "with their EditorID from the load-order index."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "formid": {
                    "type": "string",
                    "description": "FormID as 'PluginName:LocalID' (e.g. 'Skyrim.esm:012E49')",
                },
                "editor_id": {
                    "type": "string",
                    "description": "Editor ID of the record",
                },
                "plugin_name": {
                    "type": "string",
                    "description": "Return this plugin's version (default: winning record among enabled plugins). Mutually exclusive with plugin_names.",
                },
                "plugin_names": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": (
                        "Fetch the record from each listed plugin in one batched "
                        "call. Output shape becomes {'records': [...]} instead of "
                        "a single record. Mutually exclusive with plugin_name."
                    ),
                },
                "resolve_links": {
                    "type": "boolean",
                    "description": (
                        "When true, post-process the output: FormID strings that "
                        "match a known record get annotated as 'Plugin:FormID "
                        "(EditorID)'. Unknown FormIDs are left as-is."
                    ),
                    "default": False,
                },
                "include_disabled": {
                    "type": "boolean",
                    "description": (
                        "Resolve against disabled plugins too. Default false "
                        "(enabled-only). Required when reading a specific "
                        "disabled plugin's version of a record, or when the "
                        "record only exists in disabled plugins."
                    ),
                    "default": False,
                },
            },
        },
        handler=lambda args: _handle_record_detail(args, plugin_dir),
    )

    # ── mo2_conflict_chain ──────────────────────────────────────────

    registry.register(
        name="mo2_conflict_chain",
        description=(
            "Show every plugin that modifies a record, in load order. "
            "The last one in the chain is the winner. Provide FormID "
            "('Skyrim.esm:012E49') or Editor ID. By default only "
            "enabled plugins appear -- this matches what the game "
            "actually sees at runtime. Pass include_disabled=true to "
            "see the full history including plugins whose checkbox is "
            "off (useful for forensic analysis)."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "formid": {
                    "type": "string",
                    "description": "FormID as 'PluginName:LocalID'",
                },
                "editor_id": {
                    "type": "string",
                    "description": "Editor ID of the record",
                },
                "include_disabled": {
                    "type": "boolean",
                    "description": (
                        "Include refs from plugins whose checkbox is off "
                        "in MO2's right pane. Default false (enabled-only)."
                    ),
                    "default": False,
                },
            },
        },
        handler=lambda args: _handle_conflict_chain(args),
    )

    # ── mo2_plugin_conflicts ────────────────────────────────────────

    registry.register(
        name="mo2_plugin_conflicts",
        description=(
            "Show all records that a plugin overrides from its masters, "
            "grouped by record type. Shows counts and sample records. "
            "By default filters conflict chains to enabled plugins only "
            "and returns empty if the target plugin itself is disabled. "
            "Pass include_disabled=true to see the plugin's overrides "
            "regardless of enable state."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "plugin_name": {
                    "type": "string",
                    "description": "Plugin filename (e.g. 'Dawnguard.esm')",
                },
                "include_disabled": {
                    "type": "boolean",
                    "description": (
                        "Include this plugin's overrides even if it or "
                        "other plugins in the chain are disabled. Default "
                        "false (enabled-only)."
                    ),
                    "default": False,
                },
            },
            "required": ["plugin_name"],
        },
        handler=lambda args: _handle_plugin_conflicts(args),
    )

    # ── mo2_conflict_summary ────────────────────────────────────────

    registry.register(
        name="mo2_conflict_summary",
        description=(
            "High-level overview of all record conflicts across the load "
            "order. Shows totals grouped by record type, and the top "
            "overriding plugins. By default counts reflect enabled "
            "plugins only -- matches runtime. Pass include_disabled=true "
            "to include conflicts that involve disabled plugins."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "record_type": {
                    "type": "string",
                    "description": "Optional: filter to one record type (e.g. 'ARMO')",
                },
                "include_disabled": {
                    "type": "boolean",
                    "description": (
                        "Include conflicts involving disabled plugins. "
                        "Default false (enabled-only)."
                    ),
                    "default": False,
                },
            },
        },
        handler=lambda args: _handle_conflict_summary(args),
    )

    # Hook MO2 state-change events so the index stays live. Three hooks cover
    # the observed gap (Refresh / plugin toggle / mod toggle / priority drag /
    # install). Record queries block on _build_complete while a rebuild runs,
    # so Claude never serves data from the pre-change load order.
    _register_event_hooks(base_path, profile_path, plugin_list, mod_list)


# ── Handlers ────────────────────────────────────────────────────────────

def _handle_index_status(plugin_list) -> str:
    idx = _get_index()
    # Scan is cheap (plugin-list walk, no disk I/O) and always fresh — user
    # may have toggled plugins since the last index build, so cache would go
    # stale. Run every call.
    missing = scan_missing_masters(plugin_list)

    if idx is None or not idx.is_built:
        result = {
            'built': False,
            'build_status': _build_status,
            'missing_masters': missing,
            'missing_masters_count': len(missing),
            'message': 'Index not built. Call mo2_build_record_index to scan the load order.',
        }
    else:
        result = dict(idx.stats)
        result['build_status'] = _build_status
        result['missing_masters'] = missing
        result['missing_masters_count'] = len(missing)
    if _last_auto_refresh is not None:
        result['last_auto_refresh'] = _last_auto_refresh
    return json.dumps(result, indent=2)


def _handle_build_index(
    args: dict, base_path: str, profile_path: str, plugin_list,
    organizer=None,
) -> str:
    global _index, _build_status

    # Fall back to the module-level organizer so event-driven rebuild
    # paths (which lack a captured organizer closure) still use MO2's
    # VFS resolver.
    if organizer is None:
        organizer = _organizer

    force = args.get('force_rebuild', False)
    if isinstance(force, str):
        force = force.lower() in ('true', '1', 'yes')

    if _build_status.get('state') == 'building':
        return json.dumps({
            'status': 'already_building',
            'message': 'Index build is already in progress. Poll mo2_record_index_status.',
            **_build_status,
        }, indent=2)

    _build_status = {
        'state': 'building',
        'started_at': time.time(),
        'current_plugin': '',
        'progress': 0,
    }

    def do_build():
        global _index, _build_status, _rebuild_pending_during_build
        _build_complete.clear()
        try:
            # v2.6.0 Phase 3: index is bridge-fed. LoadOrderIndex consults
            # organizer.pluginList() / .resolvePath() directly for load
            # order and per-plugin paths; record content comes from the
            # bridge's `scan` command, batched ~30 plugins per call.
            #
            # Phase 2's `_resolve_via_mo2` injection is gone — the new
            # LoadOrderIndex constructor takes the organizer and uses
            # resolvePath internally. (The PluginResolver class it was
            # working around is also gone.)
            scan_fn = _index_scan_fn
            if scan_fn is None:
                raise RuntimeError(
                    'mutagen-bridge.exe not found under plugins/mo2_mcp/tools/. '
                    'Re-run the installer or sync the bridge before building '
                    'the record index.'
                )

            idx = LoadOrderIndex(organizer)

            def progress(name, i, total):
                _build_status['current_plugin'] = name
                _build_status['progress'] = i + 1
                _build_status['total'] = total
                if i % 200 == 0:
                    safe_name = name.encode('ascii', 'replace').decode('ascii')
                    qInfo(f'{PLUGIN_NAME}: indexing [{i+1}/{total}] {safe_name}')

            if force:
                result = idx.rebuild(scan_fn=scan_fn, progress_cb=progress)
            else:
                result = idx.build(scan_fn=scan_fn, progress_cb=progress)

            idx.save_cache()
            _index = idx

            missing = scan_missing_masters(plugin_list)

            _build_status = {
                'state': 'done',
                'finished_at': time.time(),
                'missing_masters': missing,
                'missing_masters_count': len(missing),
                **result,  # includes 'errors' (capped at 20) and 'error_count' if any
            }

            qInfo(f'{PLUGIN_NAME}: index build complete - '
                  f'{result["unique_records"]:,} records, '
                  f'{result["conflicts"]:,} conflicts in '
                  f'{result["build_time_s"]:.1f}s '
                  f'({len(missing)} plugin(s) with missing masters)')

        except Exception as e:
            _build_status = {'state': 'error', 'error': str(e)}
            qWarning(f'{PLUGIN_NAME}: index build failed: {e}')
        finally:
            # If events came in during this build, chain a fresh rebuild
            # instead of releasing queries with possibly-incomplete data.
            if _rebuild_pending_during_build:
                _rebuild_pending_during_build = False
                qInfo(f'{PLUGIN_NAME}: state changes occurred during build '
                      f'-- chaining another rebuild, queries remain blocked')
                _schedule_debounced_rebuild(
                    'post-build pending', base_path, profile_path, plugin_list,
                )
            else:
                _build_complete.set()

    thread = threading.Thread(target=do_build, daemon=True, name='record-index-build')
    thread.start()

    return json.dumps({
        'status': 'building',
        'message': 'Index build started in background. Poll mo2_record_index_status for progress.',
    }, indent=2)


def _coerce_bool(value: Any, default: bool = False) -> bool:
    """Coerce a JSON / query-string arg to bool. Accepts True/False, 'true',
    '1', 'yes' (case-insensitive) as True; everything else as False."""
    if isinstance(value, bool):
        return value
    if value is None:
        return default
    if isinstance(value, str):
        return value.strip().lower() in ('true', '1', 'yes')
    return bool(value)


def _handle_query_records(args: dict) -> str:
    idx, timed_out = _get_index_fresh()
    if timed_out:
        return json.dumps({'error': f'Index rebuild still in progress after {_BUILD_WAIT_TIMEOUT_S:.0f}s. Poll mo2_record_index_status and retry.'})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    include_disabled = _coerce_bool(args.get('include_disabled'))

    # Direct FormID lookup
    formid_str = args.get('formid')
    if formid_str:
        origin, local_id = _parse_formid_str(formid_str)
        if origin is None:
            return json.dumps({'error': f'Invalid FormID format: {formid_str}. Use "PluginName:LocalID".'})

        refs = idx.lookup_formid(origin, local_id, include_disabled=include_disabled)
        if not refs:
            # Distinguish "no such record" from "record exists but all disabled"
            all_refs = idx.lookup_formid(origin, local_id, include_disabled=True)
            if all_refs:
                return json.dumps({
                    'error': (
                        f'FormID {formid_str} exists only in disabled plugins. '
                        f'Pass include_disabled=true to see it.'
                    ),
                    'disabled_refs': len(all_refs),
                })
            return json.dumps({'error': f'FormID not found: {formid_str}'})

        # Find editor ID
        edid = _find_edid(idx, origin.lower(), local_id)
        winner = sorted(refs, key=lambda r: r.load_order)[-1]

        result = {
            'formid': formid_str,
            'record_type': winner.record_type,
            'editor_id': edid,
            'winning_plugin': winner.plugin,
            'override_count': len(refs),
            'chain': [{'plugin': r.plugin, 'load_order': r.load_order} for r in sorted(refs, key=lambda r: r.load_order)],
        }
        return json.dumps(result, indent=2)

    # Editor ID exact lookup
    editor_id = args.get('editor_id_filter', '')
    if editor_id and not any(c in editor_id for c in '*?[]'):
        # Try exact match first
        key = idx.lookup_edid(editor_id)
        if key and not args.get('plugin_name') and not args.get('record_type'):
            refs = idx.get_conflict_chain(key[0], key[1], include_disabled=include_disabled)
            winner = refs[-1] if refs else None
            result = {
                'formid': f'{key[0]}:{key[1]:06X}',
                'record_type': winner.record_type if winner else '?',
                'editor_id': editor_id,
                'winning_plugin': winner.plugin if winner else '?',
                'override_count': len(refs),
            }
            return json.dumps({'total': 1, 'records': [result]}, indent=2)

    # General query
    records = idx.query_records(
        plugin_name=args.get('plugin_name'),
        record_type=args.get('record_type'),
        edid_filter=args.get('editor_id_filter'),
        limit=int(args.get('limit', 50)),
        offset=int(args.get('offset', 0)),
        include_disabled=include_disabled,
    )

    return json.dumps({'total': len(records), 'records': records}, indent=2)


_FORMID_RE = re.compile(r"^([A-Za-z0-9_\-'.! ]+?\.(?:esp|esm|esl)):([0-9A-Fa-f]{1,8})$")


def _enrich_formids(value: Any, idx: LoadOrderIndex) -> Any:
    """Walk a JSON-decoded structure; for any string matching 'Plugin.esp:XXXXXX',
    append the EditorID in parentheses if the index knows it. Leaves unknown
    FormIDs untouched. Used by resolve_links=true on mo2_record_detail."""
    if isinstance(value, dict):
        return {k: _enrich_formids(v, idx) for k, v in value.items()}
    if isinstance(value, list):
        return [_enrich_formids(v, idx) for v in value]
    if isinstance(value, str) and ":" in value:
        m = _FORMID_RE.match(value)
        if m:
            plugin = m.group(1)
            local_hex = m.group(2)
            try:
                local_id = int(local_hex, 16)
            except ValueError:
                return value
            edid = idx.get_edid(plugin, local_id & 0x00FFFFFF)
            if edid:
                return f"{value} ({edid})"
    return value


def _run_bridge_read(bridge: Path, request: dict, timeout: int = 15) -> dict:
    """Invoke the bridge with a JSON request, return the decoded response.
    Synthesizes an error dict on any failure."""
    try:
        proc = subprocess.run(
            [str(bridge)],
            input=json.dumps(request),
            capture_output=True,
            text=True,
            timeout=timeout,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
        )
    except subprocess.TimeoutExpired:
        return {'success': False, 'error': f'mutagen-bridge timed out after {timeout}s.'}
    except FileNotFoundError:
        return {'success': False, 'error': f'Bridge exe not found: {bridge}'}
    except Exception as e:
        return {'success': False, 'error': f'Failed to run bridge: {e}'}

    stdout = proc.stdout.strip()
    if not stdout:
        return {
            'success': False,
            'error': f'Bridge returned no output. Exit code: {proc.returncode}',
            'stderr': proc.stderr.strip()[:500] if proc.stderr else None,
        }
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        return {'success': False, 'error': 'Bridge returned invalid JSON.', 'raw_output': stdout[:500]}


def _resolve_plugin_ref(
    idx: LoadOrderIndex, origin: str, local_id: int, plugin_name: str,
    include_disabled: bool = False,
):
    """Return the RecordRef for a specific plugin's version, or None if the
    plugin doesn't override/define this record (or is filtered out)."""
    chain = idx.get_conflict_chain(origin, local_id, include_disabled=include_disabled)
    if not chain:
        return None
    pn_lower = plugin_name.lower()
    for ref in chain:
        if ref.plugin.lower() == pn_lower:
            return ref
    return None


def _handle_record_detail(args: dict, plugin_dir: Path) -> str:
    idx, timed_out = _get_index_fresh()
    if timed_out:
        return json.dumps({'error': f'Index rebuild still in progress after {_BUILD_WAIT_TIMEOUT_S:.0f}s. Poll mo2_record_index_status and retry.'})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    plugin_names = args.get('plugin_names')
    plugin_name = args.get('plugin_name')
    resolve_links = _coerce_bool(args.get('resolve_links'))
    include_disabled = _coerce_bool(args.get('include_disabled'))

    if plugin_names and plugin_name:
        return json.dumps({'error': "Provide either plugin_name or plugin_names, not both."})

    bridge = _find_bridge_for_read(plugin_dir)
    if bridge is None:
        return json.dumps({
            'error': (
                'mutagen-bridge.exe not found. Expected at '
                '{plugin_dir}/tools/mutagen-bridge.exe or '
                '{plugin_dir}/tools/mutagen-bridge/mutagen-bridge.exe '
                '(legacy spooky-bridge paths also accepted for v2.5.x '
                'installs).'
            ),
        })

    # Single-plugin flow (existing behavior) ─────────────────────────────
    if not plugin_names:
        ref, origin, local_id, edid = _resolve_target(idx, args, include_disabled=include_disabled)
        if ref is None:
            # Distinguish "no such record" from "record exists but all disabled"
            if not include_disabled:
                probe, _o, _l, _e = _resolve_target(idx, args, include_disabled=True)
                if probe is not None:
                    return json.dumps({
                        'error': (
                            'Record exists only in disabled plugins. Pass '
                            'include_disabled=true to read it.'
                        ),
                        'winning_disabled_plugin': probe.plugin,
                    })
            return json.dumps({'error': 'Record not found. Provide formid or editor_id.'})

        pinfo = idx.get_plugin_info(ref.plugin)
        if pinfo is None:
            return json.dumps({'error': f'Plugin info not found for {ref.plugin}'})

        plugin_path = Path(pinfo.path)
        if not plugin_path.exists():
            return json.dumps({'error': f'Plugin file not found: {pinfo.path}'})

        response = _run_bridge_read(bridge, {
            'command': 'read_record',
            'plugin_path': str(plugin_path).replace('\\', '/'),
            'formid': f'{origin}:{local_id:06X}',
        })

        if not response.get('success'):
            return json.dumps({
                'error': response.get('error', 'Bridge read failed.'),
                'detail': response.get('error_detail') or response.get('stderr'),
            })

        fields = response.get('fields', {})
        if resolve_links:
            fields = _enrich_formids(fields, idx)

        result = {
            'formid': response.get('formid', f'{origin}:{local_id:06X}'),
            'record_type': response.get('record_type', ref.record_type),
            'editor_id': response.get('editor_id', edid),
            'plugin': ref.plugin,
            'load_order': ref.load_order,
            'fields': fields,
        }
        return json.dumps(result, indent=2, default=str)

    # Batch (plugin_names) flow ──────────────────────────────────────────
    # Resolve target once to get origin + local_id (we don't care about winner here).
    # Batch callers are often diffing a conflict chain that may include disabled
    # plugins as comparison points -- use include_disabled=True for the anchor
    # lookup so the origin/local_id resolve even if only disabled refs exist.
    lookup_args = dict(args)
    lookup_args.pop('plugin_names', None)
    lookup_args.pop('plugin_name', None)
    ref, origin, local_id, edid = _resolve_target(idx, lookup_args, include_disabled=True)
    if ref is None:
        return json.dumps({'error': 'Record not found. Provide formid or editor_id.'})

    batch_items = []
    refs_by_plugin: dict[str, Any] = {}
    errors = []
    for pname in plugin_names:
        p_ref = _resolve_plugin_ref(idx, origin, local_id, pname, include_disabled=True)
        if p_ref is None:
            errors.append({'plugin': pname, 'error': f"{pname} does not override/define this record."})
            continue
        pinfo = idx.get_plugin_info(p_ref.plugin)
        if pinfo is None or not os.path.exists(pinfo.path):
            errors.append({'plugin': pname, 'error': f"Plugin file not found: {pname}"})
            continue
        refs_by_plugin[p_ref.plugin.lower()] = p_ref
        batch_items.append({
            'plugin_path': pinfo.path.replace('\\', '/'),
            'formid': f'{origin}:{local_id:06X}',
        })

    if not batch_items:
        return json.dumps({
            'error': 'No plugins from plugin_names resolved successfully.',
            'per_plugin_errors': errors,
        }, indent=2)

    response = _run_bridge_read(bridge, {
        'command': 'read_records',
        'records': batch_items,
    }, timeout=max(15, 5 * len(batch_items)))

    if not response.get('success'):
        return json.dumps({
            'error': response.get('error', 'Bridge batch read failed.'),
            'detail': response.get('error_detail') or response.get('stderr'),
            'per_plugin_errors': errors,
        })

    # Attach load_order to each record in the response (bridge doesn't know it).
    out_records = []
    for rec in response.get('records', []):
        plugin_file = rec.get('plugin') or ''
        p_ref = refs_by_plugin.get(plugin_file.lower())
        fields = rec.get('fields', {})
        if resolve_links and rec.get('success'):
            fields = _enrich_formids(fields, idx)
        out_records.append({
            'formid': rec.get('formid', f'{origin}:{local_id:06X}'),
            'record_type': rec.get('record_type'),
            'editor_id': rec.get('editor_id'),
            'plugin': plugin_file,
            'load_order': p_ref.load_order if p_ref else None,
            'success': rec.get('success', False),
            'error': rec.get('error'),
            'fields': fields if rec.get('success') else None,
        })

    result = {
        'formid': f'{origin}:{local_id:06X}',
        'editor_id': edid,
        'record_type': ref.record_type,
        'records': out_records,
    }
    if errors:
        result['per_plugin_errors'] = errors
    return json.dumps(result, indent=2, default=str)


def _handle_conflict_chain(args: dict) -> str:
    idx, timed_out = _get_index_fresh()
    if timed_out:
        return json.dumps({'error': f'Index rebuild still in progress after {_BUILD_WAIT_TIMEOUT_S:.0f}s. Poll mo2_record_index_status and retry.'})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    include_disabled = _coerce_bool(args.get('include_disabled'))

    ref, origin, local_id, edid = _resolve_target(idx, args, include_disabled=include_disabled)
    if ref is None:
        if not include_disabled:
            probe, _o, _l, _e = _resolve_target(idx, args, include_disabled=True)
            if probe is not None:
                return json.dumps({
                    'error': (
                        'Record exists only in disabled plugins. Pass '
                        'include_disabled=true to see the chain.'
                    ),
                })
        return json.dumps({'error': 'Record not found. Provide formid or editor_id.'})

    chain = idx.get_conflict_chain(origin, local_id, include_disabled=include_disabled)

    result = {
        'formid': f'{origin}:{local_id:06X}',
        'record_type': chain[0].record_type if chain else '?',
        'editor_id': edid,
        'chain_length': len(chain),
        'winner': chain[-1].plugin if chain else None,
        'chain': [],
    }
    for r in chain:
        entry = {
            'plugin': r.plugin,
            'load_order': r.load_order,
            'is_winner': r == chain[-1],
        }
        if include_disabled:
            pinfo = idx.get_plugin_info(r.plugin)
            entry['enabled'] = pinfo.enabled if pinfo is not None else None
        result['chain'].append(entry)

    return json.dumps(result, indent=2)


def _handle_plugin_conflicts(args: dict) -> str:
    idx, timed_out = _get_index_fresh()
    if timed_out:
        return json.dumps({'error': f'Index rebuild still in progress after {_BUILD_WAIT_TIMEOUT_S:.0f}s. Poll mo2_record_index_status and retry.'})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    include_disabled = _coerce_bool(args.get('include_disabled'))
    plugin_name = args.get('plugin_name', '')
    pinfo = idx.get_plugin_info(plugin_name)
    if pinfo is None:
        return json.dumps({'error': f'Plugin not found: {plugin_name}'})

    if not include_disabled and not pinfo.enabled:
        return json.dumps({
            'error': (
                f"Plugin '{pinfo.name}' is disabled. Pass include_disabled=true "
                f"to see its conflicts anyway (they won't apply in-game until "
                f"the user enables it)."
            ),
            'plugin': pinfo.name,
            'enabled': False,
        })

    conflicts = idx.get_plugin_conflicts(plugin_name, include_disabled=include_disabled)
    total = sum(len(v) for v in conflicts.values())

    result = {
        'plugin': pinfo.name,
        'load_order': pinfo.load_order,
        'enabled': pinfo.enabled,
        'masters': pinfo.masters,
        'record_count': pinfo.record_count,
        'total_overrides': total,
        'by_type': {},
    }

    for rtype, entries in sorted(conflicts.items(), key=lambda x: -len(x[1])):
        samples = []
        for origin, local_id, chain in entries[:5]:
            edid = _find_edid(idx, origin, local_id)
            plugins = [r.plugin for r in chain]
            samples.append({
                'formid': f'{origin}:{local_id:06X}',
                'editor_id': edid,
                'chain': plugins,
            })
        result['by_type'][rtype] = {
            'count': len(entries),
            'samples': samples,
        }

    return json.dumps(result, indent=2)


def _handle_conflict_summary(args: dict) -> str:
    idx, timed_out = _get_index_fresh()
    if timed_out:
        return json.dumps({'error': f'Index rebuild still in progress after {_BUILD_WAIT_TIMEOUT_S:.0f}s. Poll mo2_record_index_status and retry.'})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    include_disabled = _coerce_bool(args.get('include_disabled'))
    summary = idx.get_conflict_summary(include_disabled=include_disabled)
    summary['include_disabled'] = include_disabled
    return json.dumps(summary, indent=2)


# ── MO2 event hooks ─────────────────────────────────────────────────────

def _ascii_safe(s: str) -> str:
    """MO2's Qt logger on Windows uses ASCII codec -- non-ASCII chars in
    qInfo/qWarning calls raise UnicodeEncodeError and suppress the log entry
    (and everything after it in the handler). Sanitize every log-bound string
    that could contain user-controlled input (plugin names, mod names)."""
    return s.encode('ascii', 'replace').decode('ascii')


def _on_mo2_event(source: str, base_path: str, profile_path: str, plugin_list) -> None:
    """Handler for MO2 events that require a full index rebuild.

    Fed by onRefreshed and onModMoved -- the former covers mod install /
    uninstall / plugin file appearance, the latter covers mod-priority
    drags that can change which provider wins for a given plugin
    filename. Both require re-scanning plugin files on disk.

    onPluginStateChanged goes through `_on_plugin_state_changed` instead
    -- a pure enable-state toggle doesn't change record data, so it can
    update PluginInfo.enabled bits in place rather than paying for a
    full rebuild.

    Query handlers block on `_build_complete` while the rebuild runs, so
    Claude never sees stale data between a state change and the new
    index landing.

    Swallows exceptions with traceback logging -- a raise here would
    propagate into MO2's Qt event loop and could destabilize other
    plugins' callbacks.
    """
    global _last_auto_refresh
    safe_source = _ascii_safe(source)
    try:
        fired_at = time.time()
        try:
            plugin_count = len(plugin_list.pluginNames())
        except Exception:
            plugin_count = -1

        if _index is None or not _index.is_built:
            qInfo(f'{PLUGIN_NAME}: {safe_source} fired before first index build '
                  f'(plugin_count={plugin_count}) -- skipping auto-rebuild')
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'plugin_count': plugin_count,
                'triggered_rebuild': False,
                'skip_reason': 'no_prior_index',
            }
            return

        if _build_status.get('state') == 'building':
            # Flag a post-build rebuild. The in-flight build started reading
            # disk BEFORE this event, so its result won't reflect this change.
            # Build thread's finally chains the next rebuild without releasing
            # _build_complete -- queries stay blocked until the chained build
            # completes.
            global _rebuild_pending_during_build
            _rebuild_pending_during_build = True
            qInfo(f'{PLUGIN_NAME}: {safe_source} fired during active rebuild '
                  f'(plugin_count={plugin_count}) -- flagged post-build rebuild')
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'plugin_count': plugin_count,
                'triggered_rebuild': False,
                'skip_reason': 'build_in_progress_pending_rebuild',
            }
            return

        qInfo(f'{PLUGIN_NAME}: {safe_source} fired (plugin_count={plugin_count}) '
              f'-- scheduling rebuild in {_STATE_FLUSH_DELAY_S}s '
              f'(debounced: further events cancel+replace this timer)')
        _last_auto_refresh = {
            'at': fired_at,
            'source': safe_source,
            'plugin_count': plugin_count,
            'triggered_rebuild': True,
            'delay_s': _STATE_FLUSH_DELAY_S,
        }
        _schedule_debounced_rebuild(safe_source, base_path, profile_path, plugin_list)

    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: error in {safe_source} callback: {e}\n'
                 f'{traceback.format_exc()}')


def _on_plugin_state_changed(
    state_dict: dict, base_path: str, profile_path: str, plugin_list,
) -> None:
    """Fast path for the onPluginStateChanged event.

    When the user ticks or unticks a plugin in MO2's right pane, no
    record data has changed -- only the enabled bit. Flip
    PluginInfo.enabled in place for each toggled plugin; queries using
    the default include_disabled=False filter will immediately see the
    new state without waiting for a ~10-15s rebuild.

    Falls back to a full rebuild when:
      * No prior index exists (nothing to flip against).
      * Any plugin in the event dict is unknown to the index -- e.g. a
        freshly-written plugin is being enabled for the first time and
        needs its records scanned in.
      * Another rebuild is already running -- piggyback on the existing
        build-pending mechanism so the new state lands in the chained
        rebuild.
    """
    global _last_auto_refresh, _rebuild_pending_during_build
    safe_source = _ascii_safe(f"onPluginStateChanged({len(state_dict)})")

    try:
        fired_at = time.time()

        if _index is None or not _index.is_built:
            qInfo(f'{PLUGIN_NAME}: {safe_source} fired before first index build '
                  f'-- no-op (mo2_build_record_index will pick up current state)')
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'triggered_rebuild': False,
                'skip_reason': 'no_prior_index',
            }
            return

        if _build_status.get('state') == 'building':
            _rebuild_pending_during_build = True
            qInfo(f'{PLUGIN_NAME}: {safe_source} fired during active rebuild '
                  f'-- flagged post-build rebuild so in-place flips land with fresh data')
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'triggered_rebuild': False,
                'skip_reason': 'build_in_progress_pending_rebuild',
            }
            return

        # Walk the state dict, flip known plugins in place, collect unknowns.
        unknown: list[str] = []
        flips: list[tuple[str, bool]] = []
        for plugin_name, new_state in state_dict.items():
            pinfo = _index.get_plugin_info(plugin_name)
            if pinfo is None:
                unknown.append(plugin_name)
                continue
            try:
                is_active = bool(int(new_state) & int(mobase.PluginState.ACTIVE))
            except Exception:
                is_active = (new_state == mobase.PluginState.ACTIVE)
            flips.append((plugin_name, is_active))

        if unknown:
            # Unknown plugin(s) in the event -- index needs to pick them up.
            # Typical case: a write tool just created a new plugin and the
            # user ticked its checkbox for the first time, triggering this
            # event before any onRefreshed has added the plugin to the index.
            safe_unknown = [_ascii_safe(n) for n in unknown[:5]]
            qInfo(f'{PLUGIN_NAME}: {safe_source} references {len(unknown)} '
                  f'unknown plugin(s) {safe_unknown} -- falling back to full rebuild')
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'triggered_rebuild': True,
                'unknown_plugins': safe_unknown,
                'delay_s': _STATE_FLUSH_DELAY_S,
            }
            _schedule_debounced_rebuild(
                safe_source, base_path, profile_path, plugin_list,
            )
            return

        # All known -- in-place flip, no rebuild.
        for plugin_name, is_active in flips:
            _index.set_plugin_enabled(plugin_name, is_active)

        qInfo(f'{PLUGIN_NAME}: {safe_source} -- in-place flipped '
              f'{len(flips)} plugin(s), no rebuild needed')
        _last_auto_refresh = {
            'at': fired_at,
            'source': safe_source,
            'triggered_rebuild': False,
            'in_place_flips': len(flips),
        }

    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: error in {safe_source} callback: {e}\n'
                 f'{traceback.format_exc()}')


def _schedule_debounced_rebuild(
    source: str, base_path: str, profile_path: str, plugin_list,
) -> None:
    """Cancel-and-replace any pending rebuild timer. Clears `_build_complete`
    immediately so record queries block from this moment on -- not just once
    the build starts -- closing the stale-read window through both the
    debounce delay AND the subsequent build."""
    global _rebuild_timer
    with _rebuild_timer_lock:
        if _rebuild_timer is not None:
            _rebuild_timer.cancel()
        _build_complete.clear()
        _rebuild_timer = threading.Timer(
            _STATE_FLUSH_DELAY_S,
            _fire_debounced_rebuild,
            args=(source, base_path, profile_path, plugin_list),
        )
        _rebuild_timer.daemon = True
        _rebuild_timer.name = 'record-index-debounce'
        _rebuild_timer.start()


def _fire_debounced_rebuild(
    source: str, base_path: str, profile_path: str, plugin_list,
) -> None:
    """Runs on the timer thread after the debounce delay. Triggers the real
    rebuild, or re-releases `_build_complete` if something prevents the build
    from starting (otherwise queries would block until the 30s timeout)."""
    try:
        if _build_status.get('state') == 'building':
            qInfo(f'{PLUGIN_NAME}: debounced rebuild ({source}) fired but a build '
                  f'is already running -- the in-flight build will pick up final state')
            return
        qInfo(f'{PLUGIN_NAME}: debounced rebuild firing for {source} '
              f'(flushed {_STATE_FLUSH_DELAY_S}s ago)')
        _handle_build_index({}, base_path, profile_path, plugin_list)
    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: error in debounced rebuild ({source}): {e}\n'
                 f'{traceback.format_exc()}')
        # If we cleared _build_complete but the build never started, unblock
        # queries so they don't hang for the full 30s timeout.
        _build_complete.set()


def _register_event_hooks(
    base_path: str, profile_path: str, plugin_list, mod_list,
) -> None:
    """Register all three MO2 event hooks exactly once. `register_record_tools`
    re-runs on every server start/stop cycle, so this must be idempotent.

    Discovered gap: onRefreshed alone does NOT fire for plugin-checkbox toggle
    (right pane) or mod priority drag (left pane). Without the two extra hooks
    below, Claude would serve stale answers after either of those actions.
    """
    global _refresh_hook_registered
    if _refresh_hook_registered:
        return
    results = {}
    try:
        results['onRefreshed'] = plugin_list.onRefreshed(
            lambda: _on_mo2_event(
                "onRefreshed", base_path, profile_path, plugin_list,
            )
        )
    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: failed to register onRefreshed: {e}')
    try:
        # Callback receives dict[plugin_filename -> new PluginState]. Single
        # toggle sends one entry; batch/select-all sends many. Routed to the
        # fast-path handler -- pure checkbox toggles flip PluginInfo.enabled
        # in place rather than triggering a full rebuild.
        results['onPluginStateChanged'] = plugin_list.onPluginStateChanged(
            lambda state_dict: _on_plugin_state_changed(
                state_dict, base_path, profile_path, plugin_list,
            )
        )
    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: failed to register onPluginStateChanged: {e}')
    try:
        # Callback fires once per moved mod. Multi-select drag produces a burst
        # of callbacks; the state=='building' guard coalesces them into a
        # single rebuild that reads the final post-drag state.
        results['onModMoved'] = mod_list.onModMoved(
            lambda name, old_pri, new_pri: _on_mo2_event(
                f"onModMoved({name}:{old_pri}->{new_pri})",
                base_path, profile_path, plugin_list,
            )
        )
    except Exception as e:
        qWarning(f'{PLUGIN_NAME}: failed to register onModMoved: {e}')

    _refresh_hook_registered = True
    qInfo(f'{PLUGIN_NAME}: registered MO2 event hooks {results}')


# ── Write-side refresh + wait ───────────────────────────────────────────

def trigger_refresh_and_wait_for_index(
    organizer,
    timeout: float = _WRITE_REFRESH_TIMEOUT_S,
) -> dict:
    """Trigger a single MO2 refresh after a write op, then block until the
    auto-rebuild of the record index completes so the caller can
    immediately read back what it just wrote.

    Use case: Claude writes a file (ESP via mo2_create_patch, asset via
    mo2_write_file, extracted BSA contents, compiled .pex) and wants to
    verify or build on top of it in the same session. Without this helper
    the caller would have to tell the user "press F5 in MO2 and rebuild
    the index" before the next read-back query. With it, the tool returns
    only after MO2 has discovered the file on disk, added it to the load
    order, and our event hooks have kicked off + finished a background
    index rebuild.

    What this helper does NOT do: enable the new plugin's MO2 checkbox
    or the parent mod folder's checkbox. Every v2.5.6 beta attempt to
    drive those programmatically via the mobase API failed -- setState
    silently no-ops for freshly-written plugins, organizer.refresh() can
    revert in-memory setState during its plugin-list re-scan, and the
    retry machinery we tried stacked 6+ refresh cycles per patch for no
    reliable gain. Instead: the write response includes a next_step
    field telling the user to tick the checkbox manually when they're
    ready to load the content in-game. Read-back tools
    (mo2_record_detail, mo2_query_records) return empty for the new
    plugin's records until the user enables it (verified against live
    v2.5.6 on 2026-04-20); once the user ticks the checkbox,
    onPluginStateChanged fires an auto-rebuild and the records become
    visible without a manual mo2_build_record_index call.

    Why blocking: Claude's calling pattern is "write, then immediately
    read back." Returning before the rebuild completes means the next
    query sees the pre-write state. organizer.refresh() is async (fires
    onRefreshed on the Qt thread when MO2 finishes), and the debounced
    rebuild starts _STATE_FLUSH_DELAY_S after that -- so the natural
    wait path is _build_complete, which the rebuild thread sets when
    done and _schedule_debounced_rebuild/cancel-replace keep cleared
    across the whole event burst.

    Args:
      organizer: the MO2 IOrganizer instance.
      timeout: max seconds to wait for the refresh + rebuild. Defaults
        to _WRITE_REFRESH_TIMEOUT_S.

    Returns:
      dict with:
        refreshed: bool -- True if the rebuild completed within timeout.
        elapsed_s: float -- wall-clock seconds spent in this call.
        error: str | None -- populated if refresh() failed to start or
          the rebuild did not complete in time.
    """
    start = time.monotonic()

    # Take the block upfront. If we don't, a fast onRefreshed → debounce →
    # rebuild → set() chain could complete before our wait() call, and we'd
    # return immediately while MO2 is still mid-refresh.
    _build_complete.clear()

    try:
        organizer.refresh(save_changes=True)
    except Exception as e:
        _build_complete.set()
        return {
            'refreshed': False,
            'elapsed_s': round(time.monotonic() - start, 2),
            'error': f'organizer.refresh() failed: {e}',
        }

    if not _build_complete.wait(timeout=timeout):
        return {
            'refreshed': False,
            'elapsed_s': round(time.monotonic() - start, 2),
            'error': (
                f'Refresh+rebuild did not complete within {timeout}s'
            ),
        }

    return {
        'refreshed': True,
        'elapsed_s': round(time.monotonic() - start, 2),
        'error': None,
    }


# ── Helpers ─────────────────────────────────────────────────────────────

def build_bridge_load_order_context(
    organizer,
    idx: LoadOrderIndex,
    game_release: str = 'SkyrimSE',
) -> dict:
    """Build the ``load_order`` context dict that mutagen-bridge's
    PatchEngine expects (v2.6.0 / Phase 2).

    Mutagen's write-path needs per-master MasterStyle (ESL / Light / Full)
    to encode FormLinks correctly — pointing at an ESL-flagged master
    without telling Mutagen the master IS ESL is the v2.5.x bug that
    produced the unresolved-FormLink patches the migration fixes.

    Returns a JSON-ready dict with:

      * ``game_release``  — hardcoded "SkyrimSE" for now; Phase 6-ish can
        make this multi-game if the plugin ever targets FO4.
      * ``listings``       — every plugin in the profile's load order,
        with its on-disk path, in order. The bridge reads each path's
        ModHeader (via KeyedMasterStyle.FromPath) to derive MasterStyle.
      * ``data_folder``    — game data folder (Qt API). Forward-compat
        for Phase 3 env-aware reads; not consumed by Phase 2.
      * ``ccc_path``       — <game_root>/Skyrim.ccc path when present.
        Also forward-compat; Phase 2 doesn't need CC filenames because
        loadorder.txt already enumerates CC plugins that are loaded.

    Plugins listed in loadorder.txt but whose disk path the index doesn't
    know (or whose file is missing) are silently skipped — orphans in
    loadorder.txt shouldn't block patches whose records don't touch
    them. Plugins the index does know get pinfo.enabled from the index's
    classification (which already accounts for implicit-load masters).
    """
    listings: list[dict] = []
    for plugin_name in idx._load_order:
        pinfo = idx.get_plugin_info(plugin_name)
        if pinfo is None:
            continue
        disk_path = pinfo.path
        if not disk_path or not os.path.exists(disk_path):
            continue
        listings.append({
            'mod_key': plugin_name,
            'path': disk_path.replace('\\', '/'),
            'enabled': pinfo.enabled,
        })

    ctx: dict = {
        'game_release': game_release,
        'listings': listings,
    }

    # Optional data-folder / ccc-path forward-compat fields. Derive via
    # MO2's managedGame() so we pick up the right directory for the
    # current profile's Stock Game / Data layout. Failures here are
    # non-fatal — the bridge's Phase 2 resolver ignores these anyway.
    try:
        data_dir = organizer.managedGame().dataDirectory().absolutePath()
    except Exception:
        data_dir = None
    if data_dir:
        ctx['data_folder'] = str(data_dir).replace('\\', '/')
        game_root = os.path.dirname(str(data_dir))
        ccc = os.path.join(game_root, 'Skyrim.ccc')
        if os.path.isfile(ccc):
            ctx['ccc_path'] = ccc.replace('\\', '/')

    return ctx


def _parse_formid_str(s: str) -> tuple[str | None, int]:
    """Parse 'PluginName:LocalID' → (plugin, local_id)."""
    if ':' not in s:
        return None, 0
    parts = s.rsplit(':', 1)
    try:
        local_id = int(parts[1], 16)
    except ValueError:
        return None, 0
    return parts[0], local_id


def _find_edid(idx: LoadOrderIndex, origin: str, local_id: int) -> str | None:
    """Reverse-lookup EditorID for a resolved FormID key. Public-API
    accessor on LoadOrderIndex; takes the same (plugin, local_id) shape
    the rest of this file uses."""
    return idx.get_edid(origin, local_id)


def _resolve_target(
    idx: LoadOrderIndex, args: dict, include_disabled: bool = False,
) -> tuple[Any, str, int, str | None]:
    """Resolve formid or editor_id args to a RecordRef.

    Returns (ref, origin, local_id, edid) or (None, '', 0, None).

    ``include_disabled`` gates which refs are considered during chain
    traversal and plugin_name matching. With the default False, a
    caller asking for a specific disabled plugin's version (via
    plugin_name=) gets (None, ...) -- callers should re-try with
    include_disabled=True to surface a useful error.
    """
    formid_str = args.get('formid')
    editor_id = args.get('editor_id')
    plugin_name = args.get('plugin_name')

    origin = ''
    local_id = 0
    edid = None

    if formid_str:
        origin, local_id = _parse_formid_str(formid_str)
        if origin is None:
            return None, '', 0, None
        edid = _find_edid(idx, origin, local_id)
    elif editor_id:
        key = idx.lookup_edid(editor_id)
        if key is None:
            return None, '', 0, None
        origin, local_id = key
        edid = editor_id
    else:
        return None, '', 0, None

    chain = idx.get_conflict_chain(origin, local_id, include_disabled=include_disabled)
    if not chain:
        return None, origin, local_id, edid

    if plugin_name:
        pn_lower = plugin_name.lower()
        for r in chain:
            if r.plugin.lower() == pn_lower:
                return r, origin, local_id, edid
        return None, origin, local_id, edid

    # Default: winning record (last in chain)
    return chain[-1], origin, local_id, edid
