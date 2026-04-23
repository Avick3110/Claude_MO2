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
from .esp_index import LoadOrderIndex
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
_build_status: dict[str, Any] = {'state': 'idle'}

# onPluginStateChanged hook is registered exactly once per server process.
# Guarded because register_record_tools re-runs on every server start/stop
# cycle. v2.6.0 Phase 4 retired the onRefreshed + onModMoved hooks — the
# per-query ensure_fresh() freshness check covers those paths.
_plugin_state_hook_registered = False

# Module-level organizer reference stashed during register_record_tools so
# auto-build paths (invoked from query handlers without a captured closure
# over `organizer`) can route through MO2's VFS resolver. v2.6.0 Phase 2.
_organizer = None

# v2.6.0 Phase 3: plugin directory + bridge scan_fn closure stashed at
# register time so any ensure_fresh() / auto-build path can invoke the
# bridge without re-resolving its path each call. Same pattern as
# `_organizer`.
_plugin_dir: Path | None = None
_index_scan_fn: Any = None

# Populated by the onPluginStateChanged hook and by ensure_fresh() when it
# detects drift; surfaced via mo2_record_index_status so Claude can see
# whether the last interaction picked up any state changes.
_last_auto_refresh: dict[str, Any] | None = None

# Signalled by the onRefreshed hook when MO2's directory refresh
# completes. Used by _refresh_and_wait() on the write path so
# mo2_create_patch can return only after MO2's pluginList / VFS has
# picked up the newly-written plugin — needed because organizer.refresh()
# is async and the next read-back would otherwise race with MO2's own
# rebuild. v2.6.0 Phase 4d.
#
# Starts set() so a stray wait() from boot time doesn't block the first
# caller; write paths re-clear it right before calling organizer.refresh.
_refresh_event = threading.Event()
_refresh_event.set()

# How long _refresh_and_wait blocks for onRefreshed after calling
# organizer.refresh(save_changes=True). 30s covers Aaron's modlist
# baseline (~10-15s refresh on 3300+ plugins) with 2× headroom. On
# timeout, the caller proceeds with a warning — the file is already
# on disk; pluginList visibility catches up asynchronously.
_REFRESH_WAIT_TIMEOUT_S = 30.0


def _get_index() -> LoadOrderIndex | None:
    """Return the current index without waiting. Used by status/diagnostic
    handlers that should report live state even mid-build, and by tools_patching
    which calls this directly for its pre-patch index-availability check."""
    return _index


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
    plugin_list = organizer.pluginList()

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
            "EditorID, and type, and detects conflicts. Blocking — returns "
            "when the build completes. Typical timing: ~6-15s on cache-hit "
            "reload, ~30-80s on force_rebuild for a large modlist. Note: "
            "Claude Code's default MCP tool timeout is 60s. Force_rebuild "
            "on large modlists can exceed that; if the client reports a "
            "timeout, the server-side build still completes — check "
            "mo2_record_index_status. For routine force_rebuild use, set "
            "MCP_TIMEOUT=120000 before launching Claude Code."
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
        handler=lambda args: _handle_build_index(args),
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

    # Hook onPluginStateChanged (checkbox toggle → in-place enabled bit
    # flip) and onRefreshed (signal-only, for write-path _refresh_and_wait).
    # onModMoved stays retired — ensure_fresh covers mod-priority drift
    # via its per-query mtime/loadorder walk.
    _register_hooks(plugin_list)


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


def _build_index_sync(force: bool = False) -> dict:
    """Synchronously build (or rebuild) the record index.

    Used by both the explicit `mo2_build_record_index` handler and the
    auto-build-on-first-query path inside `_ensure_index_ready`. Updates
    `_index` and `_build_status` as side effects; returns the stats dict.

    Raises on catastrophic failure (missing bridge, MO2 API error) — the
    caller wraps in try/except and converts to a JSON error envelope.

    Not safe to call while another `_build_index_sync` is running. The
    explicit handler's `_build_status` guard and the single-threaded
    HTTPServer covers the contention window.
    """
    global _index, _build_status
    if _organizer is None:
        raise RuntimeError('_organizer not set — register_record_tools must run first')
    if _index_scan_fn is None:
        raise RuntimeError(
            'mutagen-bridge.exe not found under plugins/mo2_mcp/tools/. '
            'Re-run the installer or sync the bridge before building the '
            'record index.'
        )

    _build_status = {
        'state': 'building',
        'started_at': time.time(),
        'current_plugin': '',
        'progress': 0,
    }

    try:
        idx = LoadOrderIndex(_organizer)

        def progress(name, i, total):
            _build_status['current_plugin'] = name
            _build_status['progress'] = i + 1
            _build_status['total'] = total
            if i % 200 == 0:
                safe_name = name.encode('ascii', 'replace').decode('ascii')
                qInfo(f'{PLUGIN_NAME}: indexing [{i+1}/{total}] {safe_name}')

        if force:
            result = idx.rebuild(scan_fn=_index_scan_fn, progress_cb=progress)
        else:
            result = idx.build(scan_fn=_index_scan_fn, progress_cb=progress)

        idx.save_cache()
        _index = idx

        plugin_list = _organizer.pluginList()
        missing = scan_missing_masters(plugin_list)

        _build_status = {
            'state': 'done',
            'finished_at': time.time(),
            'missing_masters': missing,
            'missing_masters_count': len(missing),
            **result,
        }

        qInfo(f'{PLUGIN_NAME}: index build complete - '
              f'{result["unique_records"]:,} records, '
              f'{result["conflicts"]:,} conflicts in '
              f'{result["build_time_s"]:.1f}s '
              f'({len(missing)} plugin(s) with missing masters)')

        return result

    except Exception as exc:
        _build_status = {'state': 'error', 'error': str(exc)}
        qWarning(f'{PLUGIN_NAME}: index build failed: {exc}')
        raise


def _ensure_index_ready() -> tuple[LoadOrderIndex | None, str | None]:
    """Prepare the index for a read query. Returns (index, error_message).

    First query after server start (or after force_rebuild): auto-builds
    synchronously — the query handler blocks for the duration. Subsequent
    queries: calls `ensure_fresh()` which stats changed plugins + updates
    the enabled set in place; ~50-100ms in the no-drift case.

    Returns `(index, None)` on success, or `(None, error)` if the auto-
    build failed. `(index, error)` is used for ensure_fresh failures
    where the old index is still serviceable — the caller decides
    whether to return an error or proceed with stale data.
    """
    global _last_auto_refresh
    idx = _index
    if idx is None or not idx.is_built:
        qInfo(
            f'{PLUGIN_NAME}: auto-building record index on first query '
            f'(~30-80s on a large modlist)'
        )
        try:
            _build_index_sync(force=False)
        except Exception as exc:
            return None, f'Auto-build failed: {exc}'
        _last_auto_refresh = {
            'at': time.time(),
            'source': 'auto_build_on_first_query',
            'triggered_build': True,
        }
        return _index, None

    try:
        rescanned = idx.ensure_fresh(_index_scan_fn)
    except Exception as exc:
        return idx, f'ensure_fresh failed: {exc}'

    if rescanned:
        safe_names = [n.encode('ascii', 'replace').decode('ascii') for n in rescanned[:5]]
        qInfo(
            f'{PLUGIN_NAME}: ensure_fresh rescanned {len(rescanned)} '
            f'plugin(s): {safe_names}'
        )
        _last_auto_refresh = {
            'at': time.time(),
            'source': 'ensure_fresh',
            'rescanned_plugins': safe_names,
            'rescanned_count': len(rescanned),
        }

    return idx, None


def _handle_build_index(args: dict) -> str:
    """Handler for the explicit mo2_build_record_index tool call.

    v2.6.0 Phase 4b: synchronous / blocking. Runs the build inline in the
    tool-call handler and returns the final stats dict when done. Replaces
    the v2.5.x–v2.6.P4a async pattern (start thread → poll status) because:

      * Post-P4a, explicit builds are rare. The lazy build in
        `_ensure_index_ready` covers first-query warm-up; `ensure_fresh`
        covers mid-session drift. Force_rebuild is the remaining reason
        to call this tool directly, and it's not worth a polling
        protocol.
      * The polling protocol was bug-bait — the Phase 3 session tripped
        on a Monitor grep that missed MCP-wrapped JSON with escaped
        quotes, silently spinning the wait loop without ever matching.
        Every caller reimplementing the poll had the same hazard.

    Timeout caveat: Claude Code's default MCP tool timeout is 60s (see
    https://code.claude.com/docs/en/mcp). Force_rebuild on a large
    (~3000-plugin) modlist runs ~76s — the server-side build completes
    regardless, but the client sees a 60s timeout. Recovery: call
    mo2_record_index_status and read `state == 'done'`. Preempt: set
    MCP_TIMEOUT=120000 before launching Claude Code. The tool description
    documents both paths.
    """
    force = args.get('force_rebuild', False)
    if isinstance(force, str):
        force = force.lower() in ('true', '1', 'yes')

    try:
        _build_index_sync(force=force)
    except Exception as exc:
        return json.dumps({
            'status': 'error',
            'error': str(exc),
            **_build_status,
        }, indent=2)

    return json.dumps(_build_status, indent=2)


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
    idx, err = _ensure_index_ready()
    if err:
        return json.dumps({'error': err})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index unavailable (auto-build did not succeed).'})

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
    idx, err = _ensure_index_ready()
    if err:
        return json.dumps({'error': err})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index unavailable (auto-build did not succeed).'})

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
    idx, err = _ensure_index_ready()
    if err:
        return json.dumps({'error': err})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index unavailable (auto-build did not succeed).'})

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
    idx, err = _ensure_index_ready()
    if err:
        return json.dumps({'error': err})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index unavailable (auto-build did not succeed).'})

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
    idx, err = _ensure_index_ready()
    if err:
        return json.dumps({'error': err})
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index unavailable (auto-build did not succeed).'})

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


def _on_plugin_state_changed(state_dict: dict) -> None:
    """Handler for MO2's onPluginStateChanged event.

    v2.6.0 Phase 4 simplification: pure in-place enabled-bit flip for known
    plugins. No rebuild fallbacks, no in-flight build coordination, no
    post-build chaining. The per-query `ensure_fresh()` path covers the
    previously-fallback cases:

      * Unknown plugin in the event (newly-installed mod, freshly-written
        patch being ticked for the first time): the next read query stats
        its file mtime, sees it's not in `_plugin_cache`, and rescans it
        via the bridge inside `ensure_fresh`. No event-driven rebuild.
      * Event fires before any index exists: the next read query auto-
        builds. No-op here.
      * Event fires during an explicit `mo2_build_record_index` call:
        the build reads pluginList state live, so any flips done mid-build
        will be visible once the build lands. No rebuild-chain needed.

    Swallows exceptions with traceback logging — a raise here would
    propagate into MO2's Qt event loop and could destabilise other
    plugins' callbacks.
    """
    global _last_auto_refresh
    safe_source = _ascii_safe(f"onPluginStateChanged({len(state_dict)})")

    try:
        fired_at = time.time()

        if _index is None or not _index.is_built:
            qInfo(
                f'{PLUGIN_NAME}: {safe_source} fired before first index build '
                f'-- no-op (next read query will auto-build)'
            )
            _last_auto_refresh = {
                'at': fired_at,
                'source': safe_source,
                'in_place_flips': 0,
                'skip_reason': 'no_prior_index',
            }
            return

        flips = 0
        unknown: list[str] = []
        for plugin_name, new_state in state_dict.items():
            pinfo = _index.get_plugin_info(plugin_name)
            if pinfo is None:
                unknown.append(plugin_name)
                continue
            try:
                is_active = bool(int(new_state) & int(mobase.PluginState.ACTIVE))
            except Exception:
                is_active = (new_state == mobase.PluginState.ACTIVE)
            _index.set_plugin_enabled(plugin_name, is_active)
            flips += 1

        if unknown:
            safe_unknown = [_ascii_safe(n) for n in unknown[:5]]
            qInfo(
                f'{PLUGIN_NAME}: {safe_source} references {len(unknown)} '
                f'unknown plugin(s) {safe_unknown} -- ensure_fresh() will '
                f'pick them up on the next read query'
            )

        qInfo(
            f'{PLUGIN_NAME}: {safe_source} -- in-place flipped {flips} '
            f'plugin(s) ({len(unknown)} unknown, deferred to ensure_fresh)'
        )
        _last_auto_refresh = {
            'at': fired_at,
            'source': safe_source,
            'in_place_flips': flips,
            'deferred_unknown_plugins': len(unknown),
        }

    except Exception as exc:
        qWarning(
            f'{PLUGIN_NAME}: error in {safe_source} callback: {exc}\n'
            f'{traceback.format_exc()}'
        )


def _register_hooks(plugin_list) -> None:
    """Register MO2's event hooks exactly once per server process.
    `register_record_tools` re-runs on every server start/stop cycle,
    so this must be idempotent.

    Two hooks, each with a narrow purpose:

      * onPluginStateChanged → `_on_plugin_state_changed`. Flips
        PluginInfo.enabled bits in place for known plugins. Unknown
        plugins are deferred to the next query's ensure_fresh.
      * onRefreshed → sets `_refresh_event`. Pure signal for write
        paths (_refresh_and_wait) so mo2_create_patch can wait for
        MO2's directory refresh to land before returning, enabling
        pre-enable read-back. Added in Phase 4d; decoupled from any
        index-rebuild machinery, which ensure_fresh covers.

    Retired from the v2.5.x equivalent: onModMoved — ensure_fresh's
    per-query mtime/loadorder walk on reads covers mod-priority shifts
    without needing the coordination machinery Phase 3/4a dropped.
    """
    global _plugin_state_hook_registered
    if _plugin_state_hook_registered:
        return

    results: dict[str, Any] = {}
    try:
        results['onPluginStateChanged'] = plugin_list.onPluginStateChanged(
            lambda state_dict: _on_plugin_state_changed(state_dict)
        )
    except Exception as exc:
        qWarning(f'{PLUGIN_NAME}: failed to register onPluginStateChanged: {exc}')
        # Without the plugin-state hook the fast path for checkbox toggles
        # is lost but queries still work via ensure_fresh. Continue.

    try:
        results['onRefreshed'] = plugin_list.onRefreshed(
            lambda: _refresh_event.set()
        )
    except Exception as exc:
        qWarning(f'{PLUGIN_NAME}: failed to register onRefreshed: {exc}')
        # Without the refresh hook, _refresh_and_wait will always time out
        # and the write path degrades to fire-and-forget semantics.
        # Non-fatal.

    _plugin_state_hook_registered = True
    qInfo(f'{PLUGIN_NAME}: registered MO2 hooks {results}')


# ── Write-path refresh helper (Phase 4d) ────────────────────────────────

def _refresh_and_wait(
    organizer,
    timeout_s: float = _REFRESH_WAIT_TIMEOUT_S,
) -> tuple[bool, float]:
    """Fire MO2's directory refresh and block until onRefreshed signals.

    Write callers (primarily mo2_create_patch) use this so the next
    read-back query sees the newly-written plugin in MO2's pluginList —
    `organizer.refresh(save_changes=True)` is async and the next-query
    ensure_fresh races with MO2's internal refresh otherwise.

    Returns (completed, elapsed_ms). `completed` is True if onRefreshed
    fired within the timeout; False on timeout or if organizer.refresh()
    itself raised. `elapsed_ms` is wall-clock time from the clear+refresh
    call site to the signal (or timeout).

    Race-free ordering is load-bearing — `_refresh_event.clear()` MUST
    run BEFORE `organizer.refresh()` is called. If MO2's refresh were
    to complete synchronously from a warm cache and fire onRefreshed
    before we cleared the event, a subsequent wait() would return
    instantly with the stale signal. Same concern applies if another
    refresh fired between the previous wait()'s timeout and our next
    clear. DO NOT REFACTOR THIS SEQUENCE without preserving the
    clear-then-call-then-wait order.

    Best-effort: on timeout the caller should proceed, not error. The
    user's file is already on disk; MO2 will finish the refresh on its
    own. The caller surfaces `refresh_status: 'timeout'` in the tool
    response so Claude can tell the user to press F5 manually.

    Acceptable known risk (see Phase 4d handoff): a concurrent refresh
    (user F5, other plugin's trigger) can fire onRefreshed before ours
    completes, unblocking the wait early. Probability low; consequence
    bounded by the downstream read-back which would still work if the
    race-winning refresh also covered our write, or hit the same
    "pluginList not yet reflected" state we'd hit on timeout otherwise.
    """
    _refresh_event.clear()  # Load-bearing — see docstring.
    t0 = time.monotonic()
    try:
        organizer.refresh(save_changes=True)
    except Exception as exc:
        elapsed_ms = (time.monotonic() - t0) * 1000
        qWarning(
            f'{PLUGIN_NAME}: organizer.refresh() raised {type(exc).__name__}: '
            f'{exc} (after {elapsed_ms:.0f}ms)'
        )
        return False, round(elapsed_ms, 1)

    completed = _refresh_event.wait(timeout=timeout_s)
    elapsed_ms = (time.monotonic() - t0) * 1000

    if completed:
        qInfo(
            f'{PLUGIN_NAME}: MO2 directory refresh completed in '
            f'{elapsed_ms:.0f}ms'
        )
    else:
        qWarning(
            f'{PLUGIN_NAME}: MO2 directory refresh did not signal within '
            f'{timeout_s:.0f}s; write is on disk but pluginList / VFS may '
            f'not yet reflect it'
        )

    return completed, round(elapsed_ms, 1)


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
