"""
esp_index.py — Load order indexer + conflict detector, v2.6.0 (Phase 3).

Mutagen-authoritative. Every record this index knows about came from
the bridge's `scan` command, which uses Mutagen's `CreateFromBinaryOverlay`
+ `EnumerateMajorRecords` to enumerate plugin contents. Mutagen's `FormKey`
already encodes ESL-compacted slot IDs and the master-resolved origin, so
the index FormIDs match xEdit by construction — no master-table arithmetic
on the Python side.

What this module DOES NOT do (anymore — see PHASE_3_HARNESS_OUTPUT.md):
  * Parse plugin file bytes. Deleted with `esp_reader.py`.
  * Resolve plugin paths via an alphabetical mods/ walk. MO2's
    `organizer.resolvePath` is the only resolver — it returns the
    same path MO2's VFS exposes to xEdit and the game.
  * Parse plugins.txt / loadorder.txt / Skyrim.ccc. MO2's
    `organizer.pluginList()` is the source of truth for which
    plugins are active and which are implicit-load (base ESMs +
    Creation Club masters); it correctly excludes uninstalled CC
    entries that our hand-rolled `read_implicit_plugins` was
    silently misclassifying.
  * Compute origin FormKeys from raw bytes + master tables. Mutagen
    does this natively at scan time.

What stays in Python:
  * The merged-index dict-of-lists data structure that backs
    `lookup_formid` / `get_conflict_chain` / `get_plugin_conflicts`
    / `get_conflict_summary` / `query_records`.
  * The on-disk pickle cache, bumped to format version 2 with
    invalidate-on-mismatch (delete + rebuild silently).
  * Per-plugin enable-state tracking with in-place flips via
    `set_plugin_enabled` (called from MO2's onPluginStateChanged
    fast-path).

The bridge call itself lives in `tools_records.py` — `LoadOrderIndex.build`
takes a `scan_fn` callback so this module stays free of subprocess /
bridge-path-detection concerns.
"""

from __future__ import annotations

import logging
import pickle
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Iterator

# mobase is only needed at build() time for PluginState.ACTIVE comparison
# against the live MO2 organizer. Lazy-imported inside build() so tests
# (test_esp_index.py) and any organizer-less consumers can import this
# module without an MO2 environment present. Consumers running inside
# MO2's Python process pay the import cost once on first build().
log = logging.getLogger(__name__)


# ── Cache format version ────────────────────────────────────────────────

# Bumped from implicit v1 (pre-v2.6) to v2 with the Mutagen-bridge rewrite.
# The cache shape changed: `_BridgePluginCache.records` is now
# (record_type, canonical_formid_str, edid) tuples instead of
# (record_type, raw_int_formid, edid, file_offset). On version mismatch,
# the cache file is deleted and a fresh build is auto-triggered — the
# old cache is unparseable under the new schema.
_CACHE_FORMAT_VERSION = 2


# ── Load-order file helper ──────────────────────────────────────────────
#
# Reading MO2's profile/loadorder.txt is the canonical way to enumerate
# every plugin in the profile's load order, including INACTIVE entries.
# The pluginList Python API exposes loadOrder() but returns -1 for both
# MISSING and INACTIVE plugins — making it impossible to distinguish
# "plugin file is gone" from "plugin file exists but the user unticked
# its checkbox". loadorder.txt is MO2's own canonical artifact for this
# question (MO2 writes it, MO2 reads it back), so consuming it isn't
# reimplementing MO2's domain logic — it's consuming MO2's output. The
# v2.6 deletion pass retired the parallel implementations of MO2's
# *plugins.txt parsing* (read_active_plugins) and *Skyrim.ccc parsing*
# (read_implicit_plugins) — both of which are MO2's *inputs* — but
# loadorder.txt sits on the other side of MO2's API.

def _read_loadorder_txt(profile_dir: Path) -> list[str]:
    """Read MO2 profile loadorder.txt; return plugin filenames in load
    order, skipping comments and blanks. Empty list if the file is
    missing (organizer-less callers, fresh profile)."""
    lo_file = profile_dir / 'loadorder.txt'
    if not lo_file.exists():
        return []
    names: list[str] = []
    with open(lo_file, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                names.append(line)
    return names


# ── Data Classes ────────────────────────────────────────────────────────

@dataclass(slots=True)
class RecordRef:
    """One plugin's reference to a record.

    Stripped down from v2.5.x: `raw_formid` and `file_offset` are gone —
    no consumer used them, and Mutagen-canonical FormIDs are stored on the
    enclosing cache entry as origin-resolved strings.
    """
    plugin: str         # plugin filename (preserved case)
    load_order: int     # position in load order (0-based)
    record_type: str    # 4-char Bethesda type code (ARMO, WEAP, etc.)


@dataclass
class PluginInfo:
    """Metadata for one plugin in the load order.

    `enabled` reflects the plugin's `pluginList().state(name) == ACTIVE` at
    the time of the last build() call. Mutated in place by
    `set_plugin_enabled()` in response to MO2's onPluginStateChanged event,
    so queries reflect the current state without requiring a full rebuild.
    """
    name: str
    path: str
    load_order: int
    masters: list[str]
    is_master: bool
    is_light: bool
    is_localized: bool
    record_count: int
    enabled: bool


@dataclass
class _BridgePluginCache:
    """Per-plugin scan data, populated from a bridge ScannedPlugin response.

    Pickled to `<plugin_dir>/.record_index.pkl`. mtime is the source plugin
    file's mtime at scan time — used as the cache-freshness key.
    """
    name: str
    path: str
    mtime: float
    masters: list[str]
    is_master: bool
    is_light: bool
    is_localized: bool
    # Each record: (record_type, canonical_formid_key, edid_or_none).
    # canonical_formid_key is `f"{plugin.lower()}:{local_id:06X}"` — the
    # output of make_formid_key, normalised once at receive time.
    records: list[tuple[str, str, str | None]]


# ── Canonical FormID key ────────────────────────────────────────────────
#
# Single source of truth for the cache's internal dict-key shape. Every
# call site that puts something INTO _records / _edids / _by_type / _plugins
# routes through here (or through _canonicalise_bridge_formid, which calls
# this). Every call site that LOOKS UP from those dicts likewise routes
# through here. If receive-side and lookup-side ever diverge, every lookup
# misses loudly — that's the easy-to-detect failure mode the format-contract
# test (mo2_mcp/test_esp_index.py) locks in.

def make_formid_key(plugin: str, local_id: int) -> str:
    """Build the canonical internal FormID key.

    Plugin name is lowercased (MO2 plugin names are case-insensitive in
    practice). Local ID is rendered as 6-digit uppercase hex with no `0x`
    prefix — matches `FormIdHelper.Format(FormKey)` on the bridge side.

    Examples:
      make_formid_key('Skyrim.esm', 0x012E49)       -> 'skyrim.esm:012E49'
      make_formid_key('NyghtfallMM.esp', 0x000884)  -> 'nyghtfallmm.esp:000884'
    """
    return f"{plugin.lower()}:{local_id:06X}"


def _canonicalise_bridge_formid(formid_str: str) -> tuple[str, int] | None:
    """Parse a bridge-emitted FormID string ('NyghtfallMM.esp:000884') and
    return (canonical_key, local_id). Returns None on malformed input.

    canonical_key uses make_formid_key for the dict-key insertion;
    local_id is returned separately because the caller also needs the
    integer for RecordRef construction and for resolving the origin
    plugin via the (origin_lower, local_id) tuple shape that some
    helpers (get_conflict_chain) accept on the public side.
    """
    if not formid_str or ':' not in formid_str:
        return None
    plugin, local_hex = formid_str.rsplit(':', 1)
    if not plugin or not local_hex:
        return None
    try:
        local_id = int(local_hex, 16)
    except ValueError:
        return None
    return make_formid_key(plugin, local_id), local_id


# ── Main Index ──────────────────────────────────────────────────────────

class LoadOrderIndex:
    """Full record index across the load order with conflict detection.

    Phase 3 rewrite: built from bridge `scan` output, not raw byte parsing.
    Public API surface preserved — callers continue to pass `(origin_plugin,
    local_id)` tuples; internally those become canonical FormID keys via
    make_formid_key.

    Usage::

        idx = LoadOrderIndex(organizer)
        idx.build(scan_fn=scan_fn_from_tools_records)
        idx.save_cache()

        chain = idx.get_conflict_chain('Skyrim.esm', 0x012E49)
    """

    def __init__(
        self,
        organizer,
        cache_path: str | Path | None = None,
    ):
        """
        Args:
            organizer: The MO2 IOrganizer instance. Required — the index
                queries `pluginList()` for load order + enable state and
                `resolvePath()` for plugin paths. No filesystem-walk
                fallback (the v2.5.x PluginResolver class is deleted).
            cache_path: Override for the on-disk pickle. Defaults to
                `<basePath>/plugins/mo2_mcp/.record_index.pkl`.
        """
        self._organizer = organizer
        self._plugin_list = organizer.pluginList()

        base_path = Path(organizer.basePath())
        self._cache_path = Path(cache_path) if cache_path else (
            base_path / 'plugins' / 'mo2_mcp' / '.record_index.pkl'
        )

        # Per-plugin cached scan data, keyed on plugin name (lowercased).
        self._plugin_cache: dict[str, _BridgePluginCache] = {}

        # Merged index — every key is a canonical FormID string from
        # make_formid_key.
        self._records: dict[str, list[RecordRef]] = {}
        # EditorID → canonical FormID key (so EDID lookups land on a single
        # record-grouping key).
        self._edids: dict[str, str] = {}
        # Canonical FormID key → EditorID (reverse lookup for query handlers).
        self._key_to_edid: dict[str, str] = {}
        # RecordType → set of canonical FormID keys (for type-filtered
        # queries).
        self._by_type: dict[str, set[str]] = {}
        # Plugin name (lowercase) → PluginInfo.
        self._plugins: dict[str, PluginInfo] = {}
        # Plugin filenames in load-order order (preserved case).
        self._load_order: list[str] = []

        self._built = False
        self._build_time: float = 0.0

    # ── Public properties ────────────────────────────────────────────

    @property
    def is_built(self) -> bool:
        return self._built

    @property
    def build_time(self) -> float:
        return self._build_time

    @property
    def stats(self) -> dict:
        if not self._built:
            return {'built': False}
        unique_records = len(self._records)
        conflicts = sum(1 for refs in self._records.values() if len(refs) > 1)
        enabled_count = sum(1 for p in self._plugins.values() if p.enabled)
        return {
            'built': True,
            'cache_format_version': _CACHE_FORMAT_VERSION,
            'plugins': len(self._plugins),
            'plugins_enabled': enabled_count,
            'plugins_disabled': len(self._plugins) - enabled_count,
            'unique_records': unique_records,
            'edids': len(self._edids),
            'conflicts': conflicts,
            'record_types': len(self._by_type),
            'build_time_s': round(self._build_time, 2),
            'cache_memory_estimate_mb': round(self._estimate_memory_mb(), 1),
        }

    # ── Build ────────────────────────────────────────────────────────

    def build(
        self,
        scan_fn: Callable[[list[str]], list[dict]],
        progress_cb: Callable[[str, int, int], None] | None = None,
    ) -> dict:
        """Scan all plugins via the bridge and build the index.

        Args:
            scan_fn: Callback that takes a list of plugin paths and returns
                a list of bridge-shaped ScannedPlugin dicts. Owned by
                `tools_records.py` (which knows how to find + invoke the
                bridge subprocess); injected here to keep this module free
                of subprocess concerns.
            progress_cb: Optional `(plugin_name, index, total)` callback
                fired once per plugin in the load order. Used by the
                build-status reporter.

        Returns:
            A stats dict including `scanned`, `cached_hits`, and any
            `errors` list (capped at 20 entries).
        """
        t0 = time.perf_counter()

        import mobase  # lazy — see module docstring

        # Load order from MO2's profile/loadorder.txt — the canonical
        # source for "every plugin including INACTIVE in proper order".
        # The earlier pluginList().loadOrder() approach broke for
        # INACTIVE plugins (MO2 returns -1 there, indistinguishable
        # from MISSING) so disabled plugins were silently absent from
        # the index, yielding plugins_disabled=0 even on modlists with
        # disabled entries. See PHASE_3_HANDOFF for the bug write-up.
        profile_dir = Path(self._organizer.profile().absolutePath())
        load_order_raw = _read_loadorder_txt(profile_dir)

        # Filter to plugins MO2 still knows about — drops orphans that
        # remain in loadorder.txt after their backing files were
        # removed (MO2 cleans these on its next refresh, but they can
        # linger between runs).
        known_lower = {n.lower() for n in self._plugin_list.pluginNames()}
        load_order = [n for n in load_order_raw if n.lower() in known_lower]

        # Active set (lowercased) — every plugin that pluginList classifies
        # as ACTIVE. Implicit-load plugins (base ESMs, installed CC masters)
        # report ACTIVE automatically; uninstalled CC entries report MISSING
        # and are excluded from `known_lower` above. INACTIVE plugins are
        # in the load order (above) but absent from this set, so they get
        # `enabled=False` on their PluginInfo and the default-filter
        # queries skip them — same semantics as v2.5.x but driven by
        # MO2's API instead of plugins.txt + Skyrim.ccc parsing.
        active_lower = {
            n.lower() for n in self._plugin_list.pluginNames()
            if self._plugin_list.state(n) == mobase.PluginState.ACTIVE
        }

        self._load_order = load_order

        # Try loading cached per-plugin data (may delete the pickle if its
        # format version doesn't match _CACHE_FORMAT_VERSION).
        self._load_cache()

        # Clear merged index — rebuilt below from cached + freshly-scanned data.
        self._records.clear()
        self._edids.clear()
        self._key_to_edid.clear()
        self._by_type.clear()
        self._plugins.clear()

        total = len(load_order)
        scanned = 0
        cached_hits = 0
        errors: list[str] = []

        # Phase 1 — figure out which plugins need a fresh scan and which
        # can hit the cache. Resolve every plugin's disk path via MO2.
        to_scan: list[tuple[int, str, str, float]] = []  # (lo_idx, name, path, mtime)
        cached_for_merge: list[tuple[int, str, _BridgePluginCache]] = []
        unresolved: list[tuple[int, str, str]] = []  # (lo_idx, name, reason)

        for lo_idx, plugin_name in enumerate(load_order):
            try:
                resolved = self._organizer.resolvePath(plugin_name)
            except Exception as exc:
                resolved = None
                unresolved.append((
                    lo_idx, plugin_name,
                    f'resolvePath failed: {type(exc).__name__}: {exc}',
                ))
                continue

            if not resolved:
                unresolved.append((lo_idx, plugin_name, 'resolvePath returned empty'))
                continue

            path = Path(resolved)
            if not path.is_file():
                unresolved.append((lo_idx, plugin_name, f'file not found: {path}'))
                continue

            try:
                current_mtime = path.stat().st_mtime
            except OSError as exc:
                unresolved.append((lo_idx, plugin_name, f'stat failed: {exc}'))
                continue

            cache_key = plugin_name.lower()
            cached = self._plugin_cache.get(cache_key)
            if cached and cached.mtime == current_mtime and cached.path == str(path):
                cached_for_merge.append((lo_idx, plugin_name, cached))
                cached_hits += 1
            else:
                to_scan.append((lo_idx, plugin_name, str(path), current_mtime))

        # Phase 2 — invoke scan_fn for the cache misses. The closure decides
        # batch sizes; we just hand it the path list.
        if to_scan:
            scan_paths = [t[2] for t in to_scan]
            try:
                scan_results = scan_fn(scan_paths)
            except Exception as exc:
                # Catastrophic scan failure — surface as an empty list +
                # one global error. Per-plugin errors come back inside
                # the response on a working scan.
                scan_results = []
                errors.append(f'scan_fn raised: {type(exc).__name__}: {exc}')

            # Index by path so we can attach scan results back to their
            # (lo_idx, name, path, mtime) entries even if scan_fn reordered
            # them.
            scan_by_path: dict[str, dict] = {}
            for entry in scan_results:
                p = entry.get('plugin_path')
                if p:
                    scan_by_path[p] = entry

            for lo_idx, name, path_str, mtime in to_scan:
                if progress_cb:
                    progress_cb(name, lo_idx, total)

                entry = scan_by_path.get(path_str)
                if entry is None:
                    errors.append(f'No scan result for {name} at {path_str}')
                    continue
                if entry.get('error'):
                    errors.append(f'Scan error for {name}: {entry["error"]}')
                    continue

                pdata = self._cache_from_scan_entry(name, path_str, mtime, entry)
                if pdata is None:
                    errors.append(f'Malformed scan entry for {name}')
                    continue

                self._plugin_cache[name.lower()] = pdata
                cached_for_merge.append((lo_idx, name, pdata))
                scanned += 1

        # Phase 3 — merge every plugin (cached + freshly scanned) into the
        # main index in load-order order. NO progress firing here — Phase 2
        # already reported per-plugin progress for the freshly-scanned
        # entries; cache hits don't need their own progress trail (fast).
        # The earlier dedup expression here always evaluated True, causing
        # every progress entry to fire twice — confirmed in Phase 3's
        # initial test run via the MO2 log (1/3350 → 3201/3350 reported
        # twice in succession before "build complete").
        for lo_idx, name, pdata in sorted(cached_for_merge, key=lambda t: t[0]):
            enabled = name.lower() in active_lower
            self._merge_plugin(pdata, lo_idx, enabled)

        # Surface unresolved plugins as errors (was their absence in v2.5.x).
        for _lo_idx, name, reason in unresolved:
            errors.append(f'Skipped {name}: {reason}')

        self._built = True
        self._build_time = time.perf_counter() - t0

        result = self.stats
        result['scanned'] = scanned
        result['cached_hits'] = cached_hits
        if errors:
            result['errors'] = errors[:20]
            result['error_count'] = len(errors)

        return result

    def rebuild(self, **kwargs) -> dict:
        """Force a full rebuild, ignoring any on-disk cache.

        Clears the in-memory cache AND deletes the pickle file before
        calling build(), so build()'s `_load_cache()` can't resurrect
        the stale data.
        """
        self._plugin_cache.clear()
        try:
            self._cache_path.unlink(missing_ok=True)
        except OSError:
            pass
        return self.build(**kwargs)

    def set_plugin_enabled(self, plugin_name: str, enabled: bool) -> bool:
        """Flip the `enabled` flag on an existing PluginInfo in place.

        Called by the onPluginStateChanged event hook so a checkbox toggle
        in MO2's right pane updates query filtering without requiring a
        full rebuild (~10-15s on a large modlist).

        Returns True if the plugin was known to the index, False otherwise
        (caller should trigger a rebuild to pick up the new plugin).
        """
        pinfo = self._plugins.get(plugin_name.lower())
        if pinfo is None:
            return False
        pinfo.enabled = enabled
        return True

    # ── Public lookup API ────────────────────────────────────────────
    #
    # Every method here takes the `(plugin, local_id)` shape that v2.5.x
    # callers used and converts to the canonical string key internally.
    # Public signatures unchanged from v2.5.x.

    def lookup_edid(self, edid: str) -> tuple[str, int] | None:
        """Return (origin_plugin, local_id) for an Editor ID, or None.

        Not filtered by enable state — raw index lookup. Callers that care
        about enable state should follow up with a chain/winner method.
        """
        key = self._edids.get(edid)
        if key is None:
            return None
        return self._split_canonical_key(key)

    def get_edid(self, origin_plugin: str, local_id: int) -> str | None:
        """Return the EditorID associated with a resolved FormID, or None.

        Public accessor for `_key_to_edid` that uses make_formid_key as
        the canonical normaliser — same path as every other lookup, so
        receive-side and lookup-side normalisation can never diverge.
        """
        return self._key_to_edid.get(make_formid_key(origin_plugin, local_id))

    def lookup_formid(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> list[RecordRef] | None:
        """Return refs for a resolved FormID, or None if unknown / all-disabled."""
        refs = self._records.get(make_formid_key(origin_plugin, local_id))
        if refs is None:
            return None
        filtered = self._filter_refs(refs, include_disabled)
        return filtered or None

    def get_conflict_chain(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> list[RecordRef]:
        """Return all plugins that modify a record, sorted by load order."""
        refs = self._filter_refs(
            self._records.get(make_formid_key(origin_plugin, local_id), []),
            include_disabled,
        )
        return sorted(refs, key=lambda r: r.load_order)

    def get_conflict_chain_by_edid(
        self, edid: str, include_disabled: bool = False,
    ) -> list[RecordRef]:
        """Return the conflict chain for a record by its Editor ID."""
        key = self._edids.get(edid)
        if key is None:
            return []
        refs = self._filter_refs(self._records.get(key, []), include_disabled)
        return sorted(refs, key=lambda r: r.load_order)

    def get_winning_record(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> RecordRef | None:
        chain = self.get_conflict_chain(origin_plugin, local_id, include_disabled)
        return chain[-1] if chain else None

    def get_winning_record_by_edid(
        self, edid: str, include_disabled: bool = False,
    ) -> RecordRef | None:
        chain = self.get_conflict_chain_by_edid(edid, include_disabled)
        return chain[-1] if chain else None

    def get_plugin_info(self, plugin_name: str) -> PluginInfo | None:
        return self._plugins.get(plugin_name.lower())

    def get_plugin_conflicts(
        self, plugin_name: str, include_disabled: bool = False,
    ) -> dict[str, list[tuple[str, int, list[RecordRef]]]]:
        """Return all records a plugin overrides from its masters.

        Returns a dict grouped by record type:
            {record_type: [(origin, local_id, full_chain), ...]}
        """
        name_lower = plugin_name.lower()
        pinfo = self._plugins.get(name_lower)
        if pinfo is None:
            return {}
        if not include_disabled and not pinfo.enabled:
            return {}

        result: dict[str, list[tuple[str, int, list[RecordRef]]]] = {}

        for key, refs in self._records.items():
            if len(refs) < 2:
                continue
            if not any(r.plugin.lower() == name_lower for r in refs):
                continue

            origin_lower, local_id = self._split_canonical_key(key)
            # Skip records the target plugin originates (those aren't
            # "overrides from masters", they're the plugin's own records).
            if origin_lower == name_lower:
                continue

            filtered = self._filter_refs(refs, include_disabled)
            if len(filtered) < 2:
                continue
            if not any(r.plugin.lower() == name_lower for r in filtered):
                continue

            rec_type = filtered[0].record_type
            sorted_refs = sorted(filtered, key=lambda r: r.load_order)
            result.setdefault(rec_type, []).append(
                (origin_lower, local_id, sorted_refs),
            )

        return result

    def get_all_conflicts(
        self, record_type: str | None = None, include_disabled: bool = False,
    ) -> Iterator[tuple[tuple[str, int], list[RecordRef]]]:
        """Yield ((origin, local_id), sorted_refs) for every conflict."""
        for key, refs in self._records.items():
            filtered = self._filter_refs(refs, include_disabled)
            if len(filtered) < 2:
                continue
            if record_type and not any(r.record_type == record_type for r in filtered):
                continue
            yield self._split_canonical_key(key), sorted(filtered, key=lambda r: r.load_order)

    def get_conflict_summary(self, include_disabled: bool = False) -> dict:
        """High-level overview of conflicts across the load order."""
        type_counts: dict[str, int] = {}
        total = 0
        plugin_overrides: dict[str, int] = {}

        for key, refs in self._records.items():
            filtered = self._filter_refs(refs, include_disabled)
            if len(filtered) < 2:
                continue
            total += 1
            rtype = filtered[0].record_type
            type_counts[rtype] = type_counts.get(rtype, 0) + 1

            origin_lower, _ = self._split_canonical_key(key)
            for r in filtered:
                if r.plugin.lower() != origin_lower:
                    plugin_overrides[r.plugin] = plugin_overrides.get(r.plugin, 0) + 1

        top_types = sorted(type_counts.items(), key=lambda x: -x[1])[:20]
        top_plugins = sorted(plugin_overrides.items(), key=lambda x: -x[1])[:20]

        return {
            'total_conflicts': total,
            'by_type': dict(top_types),
            'top_overriding_plugins': dict(top_plugins),
        }

    def query_records(
        self,
        plugin_name: str | None = None,
        record_type: str | None = None,
        edid_filter: str | None = None,
        limit: int = 50,
        offset: int = 0,
        include_disabled: bool = False,
    ) -> list[dict]:
        """Search records with optional filters. Returns dicts."""
        results: list[dict] = []
        count = 0

        for key, refs in self._records.items():
            # Type filter — stable across enable state.
            if record_type and not any(r.record_type == record_type for r in refs):
                continue

            # Plugin filter — does this record have ANY ref to the named plugin?
            if plugin_name:
                pn_lower = plugin_name.lower()
                if not any(r.plugin.lower() == pn_lower for r in refs):
                    continue

            # EditorID substring filter.
            edid = self._key_to_edid.get(key)
            if edid_filter:
                if edid is None or edid_filter.lower() not in edid.lower():
                    continue

            # Enable-state filter.
            live_refs = self._filter_refs(refs, include_disabled)
            if not live_refs:
                continue

            # If the caller restricted by plugin_name, the LIVE chain has
            # to actually contain that plugin (it might be the disabled one).
            if plugin_name:
                pn_lower = plugin_name.lower()
                if not any(r.plugin.lower() == pn_lower for r in live_refs):
                    continue

            # Pagination.
            if count < offset:
                count += 1
                continue
            if len(results) >= limit:
                break

            origin_lower, local_id = self._split_canonical_key(key)
            winner = sorted(live_refs, key=lambda r: r.load_order)[-1]
            results.append({
                'origin': origin_lower,
                'local_id': f'{local_id:06X}',
                'formid': f'{origin_lower}:{local_id:06X}',
                'record_type': winner.record_type,
                'editor_id': edid,
                'winning_plugin': winner.plugin,
                'override_count': len(live_refs),
            })
            count += 1

        return results

    # ── Internal helpers ─────────────────────────────────────────────

    def _is_ref_live(self, ref: RecordRef) -> bool:
        pinfo = self._plugins.get(ref.plugin.lower())
        return pinfo is not None and pinfo.enabled

    def _filter_refs(
        self, refs: list[RecordRef], include_disabled: bool,
    ) -> list[RecordRef]:
        if include_disabled:
            return refs
        return [r for r in refs if self._is_ref_live(r)]

    @staticmethod
    def _split_canonical_key(key: str) -> tuple[str, int]:
        """Inverse of make_formid_key. Returns (origin_lower, local_id).

        Internal-only — called from chain/conflict iterators where the key
        has to be split back into its components for the public-API tuple
        return shape. The hex parse is one strconv per record per query;
        the sort happens once over the full _records dict so this is
        comfortably under the noise floor of the dict iteration itself.
        """
        # Canonical keys are always plugin.lower():XXXXXX with a single
        # rsplit-able colon (plugin names don't contain colons).
        plugin_lower, local_hex = key.rsplit(':', 1)
        return plugin_lower, int(local_hex, 16)

    def _cache_from_scan_entry(
        self, name: str, path_str: str, mtime: float, entry: dict,
    ) -> _BridgePluginCache | None:
        """Convert one bridge ScannedPlugin dict into a _BridgePluginCache.

        Each record's bridge-emitted FormID string is canonicalised through
        make_formid_key on the way in, so by the time the data lands in
        the cache the key shape is locked.
        """
        records: list[tuple[str, str, str | None]] = []
        for r in entry.get('records') or []:
            rtype = r.get('type')
            formid = r.get('formid')
            if not rtype or not formid:
                continue
            canon = _canonicalise_bridge_formid(formid)
            if canon is None:
                continue
            records.append((rtype, canon[0], r.get('edid')))

        return _BridgePluginCache(
            name=name,
            path=path_str,
            mtime=mtime,
            masters=list(entry.get('masters') or []),
            is_master=bool(entry.get('is_master')),
            is_light=bool(entry.get('is_light')),
            is_localized=bool(entry.get('is_localized')),
            records=records,
        )

    def _merge_plugin(self, pdata: _BridgePluginCache, lo_idx: int, enabled: bool) -> None:
        """Merge a plugin's scanned records into the merged index."""
        name = pdata.name
        name_lower = name.lower()

        self._plugins[name_lower] = PluginInfo(
            name=name,
            path=pdata.path,
            load_order=lo_idx,
            masters=pdata.masters,
            is_master=pdata.is_master,
            is_light=pdata.is_light,
            is_localized=pdata.is_localized,
            record_count=len(pdata.records),
            enabled=enabled,
        )

        for rec_type, canonical_key, edid in pdata.records:
            ref = RecordRef(
                plugin=name,
                load_order=lo_idx,
                record_type=rec_type,
            )

            self._records.setdefault(canonical_key, []).append(ref)

            if edid:
                self._edids[edid] = canonical_key
                self._key_to_edid[canonical_key] = edid

            self._by_type.setdefault(rec_type, set()).add(canonical_key)

    def _estimate_memory_mb(self) -> float:
        """Rough memory estimate for the merged index — surfaced via stats
        so future sessions can verify the v2.6 string-key shape stays
        within budget if the modlist grows.

        Heuristic: ~120 bytes per RecordRef (slots dataclass + str+int+int)
        + ~110 bytes per dict entry (key + bucket + small list overhead)
        + ~80 bytes per EDID entry. Skip Python interpreter overhead — the
        number is for "did this just balloon 10x?" tripwiring, not exact
        accounting.
        """
        ref_count = sum(len(refs) for refs in self._records.values())
        return (
            (ref_count * 120)
            + (len(self._records) * 110)
            + (len(self._edids) * 80)
        ) / (1024 * 1024)

    # ── Cache I/O ────────────────────────────────────────────────────

    def _load_cache(self) -> None:
        """Load the on-disk pickle if it matches _CACHE_FORMAT_VERSION.

        On version mismatch (or any other parse failure), log a warning,
        delete the stale pickle, and leave the in-memory cache empty —
        the next build will re-scan every plugin and write a fresh pickle.
        """
        if not self._cache_path.exists():
            return
        try:
            with open(self._cache_path, 'rb') as f:
                data = pickle.load(f)
        except Exception as exc:
            log.warning('Failed to load index cache: %s — deleting stale pickle', exc)
            self._safe_unlink_cache()
            return

        if not isinstance(data, dict):
            log.warning(
                'Index cache has unexpected top-level type %s '
                '(expected dict with cache_format_version) — deleting stale pickle',
                type(data).__name__,
            )
            self._safe_unlink_cache()
            return

        version = data.get('cache_format_version')
        if version != _CACHE_FORMAT_VERSION:
            log.warning(
                'Index cache format version %r != %r (current) — '
                'deleting stale pickle and rebuilding from scratch',
                version, _CACHE_FORMAT_VERSION,
            )
            self._safe_unlink_cache()
            return

        plugins = data.get('plugins')
        if isinstance(plugins, dict):
            self._plugin_cache = plugins
        else:
            log.warning('Index cache "plugins" entry is not a dict — ignoring cache')

    def _safe_unlink_cache(self) -> None:
        try:
            self._cache_path.unlink(missing_ok=True)
        except OSError:
            pass
        self._plugin_cache = {}

    def save_cache(self) -> None:
        """Persist per-plugin scan data to disk, tagged with the format version."""
        try:
            self._cache_path.parent.mkdir(parents=True, exist_ok=True)
            payload = {
                'cache_format_version': _CACHE_FORMAT_VERSION,
                'plugins': self._plugin_cache,
            }
            with open(self._cache_path, 'wb') as f:
                pickle.dump(payload, f, protocol=pickle.HIGHEST_PROTOCOL)
        except Exception as exc:
            log.warning('Failed to save index cache: %s', exc)
