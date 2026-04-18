"""MCP tools for record-level queries, field interpretation, and conflict detection.

Field interpretation for `mo2_record_detail` is routed through spooky-bridge.exe
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
from pathlib import Path
from typing import Any

from PyQt6.QtCore import qInfo, qWarning

from .config import PLUGIN_NAME
from .esp_index import (
    LoadOrderIndex,
    PluginResolver,
    read_load_order,
    resolve_formid,
)


# ── Shared State ────────────────────────────────────────────────────────

_index: LoadOrderIndex | None = None
_build_lock = threading.Lock()
_build_status: dict[str, Any] = {'state': 'idle'}


def _get_index() -> LoadOrderIndex | None:
    return _index


def _find_bridge_for_read(plugin_dir: Path) -> Path | None:
    """Find spooky-bridge.exe. Same search order as tools_patching._find_bridge,
    duplicated here to keep tools_records free of tools_patching imports."""
    candidates = [
        plugin_dir / "tools" / "spooky-bridge.exe",
        plugin_dir / "tools" / "spooky-bridge" / "spooky-bridge.exe",
    ]
    for path in candidates:
        if path.is_file():
            return path
    return None


# ── Tool Registration ──────────────────────────────────────────────────

def register_record_tools(registry, organizer) -> None:
    """Register all record-level query tools with the MCP tool registry."""

    plugin_dir = Path(__file__).resolve().parent
    base_path = organizer.basePath()
    profile_path = organizer.profile().absolutePath()

    # ── mo2_record_index_status ─────────────────────────────────────

    registry.register(
        name="mo2_record_index_status",
        description=(
            "Check whether the record index is built. Returns stats "
            "(plugin count, record count, conflict count) and build state. "
            "If not built, call mo2_build_record_index first."
        ),
        input_schema={
            "type": "object",
            "properties": {},
        },
        handler=lambda args: _handle_index_status(),
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
            args, base_path, profile_path,
        ),
    )

    # ── mo2_query_records ───────────────────────────────────────────

    registry.register(
        name="mo2_query_records",
        description=(
            "Search records in the index. Supports filtering by plugin name, "
            "record type (e.g. ARMO, WEAP, NPC_), editor ID substring, or "
            "specific FormID. Returns FormID, EditorID, type, winning plugin, "
            "and override count. Paginated."
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
            "By default returns the winning record; specify plugin_name "
            "to get a specific plugin's version, or plugin_names (plural) "
            "to fetch the record from multiple plugins in one call (useful "
            "for diffing a conflict chain). Returns all fields with named "
            "values, enum labels, flag names. Set resolve_links=true to "
            "annotate FormID strings with their EditorID from the load-order "
            "index."
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
                    "description": "Return this plugin's version (default: winning record). Mutually exclusive with plugin_names.",
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
            },
        },
        handler=lambda args: _handle_record_detail(args, plugin_dir),
    )

    # ── mo2_conflict_chain ──────────────────────────────────────────

    registry.register(
        name="mo2_conflict_chain",
        description=(
            "Show every plugin that modifies a record, in load order. "
            "The last one in the chain is the winner. "
            "Provide FormID ('Skyrim.esm:012E49') or Editor ID."
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
            },
        },
        handler=lambda args: _handle_conflict_chain(args),
    )

    # ── mo2_plugin_conflicts ────────────────────────────────────────

    registry.register(
        name="mo2_plugin_conflicts",
        description=(
            "Show all records that a plugin overrides from its masters, "
            "grouped by record type. Shows counts and sample records."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "plugin_name": {
                    "type": "string",
                    "description": "Plugin filename (e.g. 'Dawnguard.esm')",
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
            "overriding plugins."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "record_type": {
                    "type": "string",
                    "description": "Optional: filter to one record type (e.g. 'ARMO')",
                },
            },
        },
        handler=lambda args: _handle_conflict_summary(args),
    )


# ── Handlers ────────────────────────────────────────────────────────────

def _handle_index_status() -> str:
    idx = _get_index()
    if idx is None or not idx.is_built:
        result = {
            'built': False,
            'build_status': _build_status,
            'message': 'Index not built. Call mo2_build_record_index to scan the load order.',
        }
    else:
        result = idx.stats
        result['build_status'] = _build_status
    return json.dumps(result, indent=2)


def _handle_build_index(
    args: dict, base_path: str, profile_path: str,
) -> str:
    global _index, _build_status

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
        global _index, _build_status
        try:
            idx = LoadOrderIndex(base_path, profile_path)

            def progress(name, i, total):
                _build_status['current_plugin'] = name
                _build_status['progress'] = i + 1
                _build_status['total'] = total
                if i % 200 == 0:
                    safe_name = name.encode('ascii', 'replace').decode('ascii')
                    qInfo(f'{PLUGIN_NAME}: indexing [{i+1}/{total}] {safe_name}')

            if force:
                result = idx.rebuild(progress_cb=progress)
            else:
                result = idx.build(progress_cb=progress)

            idx.save_cache()
            _index = idx

            _build_status = {
                'state': 'done',
                'finished_at': time.time(),
                **{k: v for k, v in result.items() if k != 'errors'},
            }
            if result.get('error_count'):
                _build_status['error_count'] = result['error_count']

            qInfo(f'{PLUGIN_NAME}: index build complete - '
                  f'{result["unique_records"]:,} records, '
                  f'{result["conflicts"]:,} conflicts in '
                  f'{result["build_time_s"]:.1f}s')

        except Exception as e:
            _build_status = {'state': 'error', 'error': str(e)}
            qWarning(f'{PLUGIN_NAME}: index build failed: {e}')

    thread = threading.Thread(target=do_build, daemon=True, name='record-index-build')
    thread.start()

    return json.dumps({
        'status': 'building',
        'message': 'Index build started in background. Poll mo2_record_index_status for progress.',
    }, indent=2)


def _handle_query_records(args: dict) -> str:
    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    # Direct FormID lookup
    formid_str = args.get('formid')
    if formid_str:
        origin, local_id = _parse_formid_str(formid_str)
        if origin is None:
            return json.dumps({'error': f'Invalid FormID format: {formid_str}. Use "PluginName:LocalID".'})

        refs = idx.lookup_formid(origin, local_id)
        if not refs:
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
            refs = idx.get_conflict_chain(key[0], key[1])
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
            edid = idx._key_to_edid.get((plugin.lower(), local_id & 0x00FFFFFF))
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
        )
    except subprocess.TimeoutExpired:
        return {'success': False, 'error': f'spooky-bridge timed out after {timeout}s.'}
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


def _resolve_plugin_ref(idx: LoadOrderIndex, origin: str, local_id: int, plugin_name: str):
    """Return the RecordRef for a specific plugin's version, or None if the
    plugin doesn't override/define this record."""
    chain = idx.get_conflict_chain(origin, local_id)
    if not chain:
        return None
    pn_lower = plugin_name.lower()
    for ref in chain:
        if ref.plugin.lower() == pn_lower:
            return ref
    return None


def _handle_record_detail(args: dict, plugin_dir: Path) -> str:
    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    plugin_names = args.get('plugin_names')
    plugin_name = args.get('plugin_name')
    resolve_links = bool(args.get('resolve_links', False))
    if isinstance(args.get('resolve_links'), str):
        resolve_links = args['resolve_links'].lower() in ('true', '1', 'yes')

    if plugin_names and plugin_name:
        return json.dumps({'error': "Provide either plugin_name or plugin_names, not both."})

    bridge = _find_bridge_for_read(plugin_dir)
    if bridge is None:
        return json.dumps({
            'error': (
                'spooky-bridge.exe not found. Expected at '
                '{plugin_dir}/tools/spooky-bridge.exe or '
                '{plugin_dir}/tools/spooky-bridge/spooky-bridge.exe.'
            ),
        })

    # Single-plugin flow (existing behavior) ─────────────────────────────
    if not plugin_names:
        ref, origin, local_id, edid = _resolve_target(idx, args)
        if ref is None:
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
    lookup_args = dict(args)
    lookup_args.pop('plugin_names', None)
    lookup_args.pop('plugin_name', None)
    ref, origin, local_id, edid = _resolve_target(idx, lookup_args)
    if ref is None:
        return json.dumps({'error': 'Record not found. Provide formid or editor_id.'})

    batch_items = []
    refs_by_plugin: dict[str, Any] = {}
    errors = []
    for pname in plugin_names:
        p_ref = _resolve_plugin_ref(idx, origin, local_id, pname)
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
    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    ref, origin, local_id, edid = _resolve_target(idx, args)
    if ref is None:
        return json.dumps({'error': 'Record not found. Provide formid or editor_id.'})

    chain = idx.get_conflict_chain(origin, local_id)

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
        result['chain'].append(entry)

    return json.dumps(result, indent=2)


def _handle_plugin_conflicts(args: dict) -> str:
    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    plugin_name = args.get('plugin_name', '')
    pinfo = idx.get_plugin_info(plugin_name)
    if pinfo is None:
        return json.dumps({'error': f'Plugin not found: {plugin_name}'})

    conflicts = idx.get_plugin_conflicts(plugin_name)
    total = sum(len(v) for v in conflicts.values())

    result = {
        'plugin': pinfo.name,
        'load_order': pinfo.load_order,
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
    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({'error': 'Index not built. Call mo2_build_record_index first.'})

    summary = idx.get_conflict_summary()
    return json.dumps(summary, indent=2)


# ── Helpers ─────────────────────────────────────────────────────────────

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
    """Reverse-lookup EditorID for a resolved FormID key."""
    return idx._key_to_edid.get((origin.lower(), local_id))


def _resolve_target(
    idx: LoadOrderIndex, args: dict,
) -> tuple[Any, str, int, str | None]:
    """Resolve formid or editor_id args to a RecordRef.

    Returns (ref, origin, local_id, edid) or (None, '', 0, None).
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

    chain = idx.get_conflict_chain(origin, local_id)
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
