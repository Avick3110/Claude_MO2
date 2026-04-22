"""
esp_index.py - Load order indexer and conflict detector.

Scans every plugin in the active load order, indexes records by FormID
and EditorID, and identifies conflicts (records modified by multiple
plugins).  Results are cached to disk so repeat loads are fast.

Performance strategy:
- Only reads record headers (type, FormID, flags) + EDID subrecord
- Skips full subrecord data during scan
- Per-plugin scan results are cached and keyed by file mtime
- Full index is rebuilt from cached per-plugin data on startup
"""

from __future__ import annotations

import logging
import os
import pickle
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterator

try:
    from .esp_reader import ESPReader
except ImportError:
    from esp_reader import ESPReader

log = logging.getLogger(__name__)


# ── Data Classes ────────────────────────────────────────────────────────

@dataclass(slots=True)
class RecordRef:
    """One plugin's version of a record."""
    plugin: str         # plugin filename
    load_order: int     # position in load order (0-based)
    record_type: str    # 4-char type (ARMO, WEAP, etc.)
    raw_formid: int     # FormID as stored in the file
    file_offset: int    # byte offset of record header in plugin file


@dataclass
class PluginInfo:
    """Metadata for one plugin in the load order.

    `enabled` reflects the plugin's plugins.txt state (checkbox in MO2's
    right pane) at the time of the last build() call. It is mutated in
    place by LoadOrderIndex.set_plugin_enabled() in response to MO2's
    onPluginStateChanged event, so queries reflect the current state
    without requiring a full rebuild.
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
class _PluginCache:
    """Per-plugin scan data for caching."""
    name: str
    path: str
    mtime: float
    masters: list[str]
    is_master: bool
    is_light: bool
    is_localized: bool
    # Each record: (record_type, raw_formid, edid_or_none, file_offset)
    records: list[tuple[str, int, str | None, int]]


# ── FormID Resolution ───────────────────────────────────────────────────

def resolve_formid(
    raw: int, masters: list[str], plugin_name: str,
) -> tuple[str, int]:
    """Resolve a file-local FormID to (origin_plugin, local_id).

    The top byte of a FormID is an index into [*masters, self].
    Returns the originating plugin name and the 24-bit local ID.
    """
    index = (raw >> 24) & 0xFF
    local = raw & 0x00FFFFFF
    if index < len(masters):
        return (masters[index], local)
    return (plugin_name, local)


# ── Plugin File Resolver ────────────────────────────────────────────────

class PluginResolver:
    """Finds plugin files on disk given their filenames.

    Searches Stock Game/Data/ and mods/*/ (depth 1 inside each mod folder).
    """

    def __init__(self, modlist_root: str | Path):
        self._root = Path(modlist_root)
        self._map: dict[str, Path] = {}  # lowercase name -> path
        self._built = False

    def _build(self) -> None:
        if self._built:
            return
        self._built = True

        exts = {'.esp', '.esm', '.esl'}

        # Stock Game/Data
        stock = self._root / 'Stock Game' / 'Data'
        if stock.is_dir():
            for f in stock.iterdir():
                if f.suffix.lower() in exts and f.is_file():
                    self._map[f.name.lower()] = f

        # mods/*/ (plugin files at the root of each mod folder only)
        mods_dir = self._root / 'mods'
        if mods_dir.is_dir():
            for mod_folder in mods_dir.iterdir():
                if not mod_folder.is_dir():
                    continue
                for f in mod_folder.iterdir():
                    if f.suffix.lower() in exts and f.is_file():
                        # Later mods override earlier ones
                        self._map[f.name.lower()] = f

    def resolve(self, plugin_name: str) -> Path | None:
        """Return the on-disk path for a plugin filename, or None."""
        self._build()
        return self._map.get(plugin_name.lower())

    def resolve_all(self, names: list[str]) -> list[tuple[str, Path | None]]:
        """Resolve a list of plugin names. Returns (name, path_or_none) pairs."""
        self._build()
        return [(n, self._map.get(n.lower())) for n in names]


# ── Load Order Reader ───────────────────────────────────────────────────

def read_load_order(profile_dir: str | Path) -> list[str]:
    """Read loadorder.txt from an MO2 profile directory.

    Returns plugin filenames in load order, skipping comments and blanks.
    """
    lo_file = Path(profile_dir) / 'loadorder.txt'
    if not lo_file.exists():
        return []
    names: list[str] = []
    with open(lo_file, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                names.append(line)
    return names


def read_active_plugins(profile_dir: str | Path) -> set[str]:
    """Read plugins.txt to get the set of active (enabled) plugin names."""
    plugins_file = Path(profile_dir) / 'plugins.txt'
    if not plugins_file.exists():
        return set()
    active: set[str] = set()
    with open(plugins_file, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line.startswith('*'):
                active.add(line[1:].strip())
    return active


# Base-game masters Skyrim always loads regardless of plugins.txt.
IMPLICIT_MASTERS = frozenset([
    'skyrim.esm',
    'update.esm',
    'dawnguard.esm',
    'hearthfires.esm',
    'dragonborn.esm',
])


def read_ccc_plugins(game_root: str | Path) -> set[str]:
    """Read Skyrim.ccc for Creation Club plugins Skyrim loads implicitly.

    Skyrim.ccc sits at the game root (alongside SkyrimSE.exe), not inside
    Data. Returns an empty set if the file is missing or unreadable.
    """
    ccc_path = Path(game_root) / 'Skyrim.ccc'
    if not ccc_path.exists():
        return set()
    try:
        names: set[str] = set()
        with open(ccc_path, encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#'):
                    names.add(line)
        return names
    except Exception:
        return set()


def read_implicit_plugins(game_root: str | Path | None) -> set[str]:
    """Plugins the game auto-loads regardless of plugins.txt state.

    Combines the hardcoded base masters (Skyrim / Update / DLC ESMs) with
    Creation Club content listed in ``<game_root>/Skyrim.ccc``. These
    plugins typically aren't starred in plugins.txt because Skyrim loads
    them automatically; classifying them as "disabled" hides vanilla and
    CC records from default conflict analysis.
    """
    result = set(IMPLICIT_MASTERS)
    if game_root:
        result |= read_ccc_plugins(game_root)
    return result


# ── Main Index ──────────────────────────────────────────────────────────

class LoadOrderIndex:
    """Full record index across the load order with conflict detection.

    Usage::

        idx = LoadOrderIndex(modlist_root, profile_dir)
        idx.build()          # scan all plugins
        idx.save_cache()     # persist to disk

        chain = idx.get_conflict_chain_by_formid("Skyrim.esm", 0x012E49)
        winner = idx.get_winning_record("Skyrim.esm", 0x012E49)
    """

    def __init__(
        self,
        modlist_root: str | Path,
        profile_dir: str | Path | None = None,
        cache_path: str | Path | None = None,
    ):
        self._root = Path(modlist_root)
        self._profile = Path(profile_dir) if profile_dir else None
        self._cache_path = Path(cache_path) if cache_path else (
            self._root / 'plugins' / 'mo2_mcp' / '.record_index.pkl'
        )
        self._resolver = PluginResolver(modlist_root)

        # Per-plugin cached scan data
        self._plugin_cache: dict[str, _PluginCache] = {}

        # Merged index (built from cached per-plugin data)
        # Key: (origin_plugin_lower, local_id) → list of RecordRef
        self._records: dict[tuple[str, int], list[RecordRef]] = {}
        # EditorID → (origin_plugin_lower, local_id)
        self._edids: dict[str, tuple[str, int]] = {}
        # Reverse: (origin_plugin_lower, local_id) → EditorID
        self._key_to_edid: dict[tuple[str, int], str] = {}
        # RecordType → set of (origin_plugin_lower, local_id)
        self._by_type: dict[str, set[tuple[str, int]]] = {}
        # Plugin info
        self._plugins: dict[str, PluginInfo] = {}
        # Load order list
        self._load_order: list[str] = []

        self._built = False
        self._build_time: float = 0.0

    # ── Build ────────────────────────────────────────────────────────

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
            'plugins': len(self._plugins),
            'plugins_enabled': enabled_count,
            'plugins_disabled': len(self._plugins) - enabled_count,
            'unique_records': unique_records,
            'edids': len(self._edids),
            'conflicts': conflicts,
            'record_types': len(self._by_type),
            'build_time_s': round(self._build_time, 2),
        }

    def build(
        self,
        load_order: list[str] | None = None,
        active_plugins: set[str] | None = None,
        progress_cb=None,
    ) -> dict:
        """Scan all plugins and build the index.

        Args:
            load_order: Plugin names in load order.  If None, reads
                        from the profile's loadorder.txt.
            active_plugins: Set of plugin filenames currently enabled
                        (checkbox on). If None, reads from the
                        profile's plugins.txt. Plugins not in this set
                        are indexed but flagged ``enabled=False`` and
                        filtered out of query results by default.
            progress_cb: Optional callback(plugin_name, index, total).

        Returns:
            Stats dict.
        """
        t0 = time.perf_counter()

        if load_order is None:
            if self._profile:
                load_order = read_load_order(self._profile)
            else:
                raise ValueError('No load_order provided and no profile_dir set')

        if active_plugins is None:
            active_plugins = read_active_plugins(self._profile) if self._profile else set()

        game_root = self._root / 'Stock Game'
        implicit = read_implicit_plugins(game_root if game_root.is_dir() else None)

        active_lower = {n.lower() for n in active_plugins} | {n.lower() for n in implicit}

        self._load_order = load_order

        # Try loading cached per-plugin data
        self._load_cache()

        # Clear merged index
        self._records.clear()
        self._edids.clear()
        self._key_to_edid.clear()
        self._by_type.clear()
        self._plugins.clear()

        total = len(load_order)
        scanned = 0
        cached_hits = 0
        errors: list[str] = []

        for lo_idx, plugin_name in enumerate(load_order):
            if progress_cb:
                progress_cb(plugin_name, lo_idx, total)

            path = self._resolver.resolve(plugin_name)
            if path is None:
                errors.append(f'Not found: {plugin_name}')
                continue

            # Check cache
            cache_key = plugin_name.lower()
            cached = self._plugin_cache.get(cache_key)
            try:
                current_mtime = path.stat().st_mtime
            except OSError:
                errors.append(f'Cannot stat: {plugin_name}')
                continue

            if cached and cached.mtime == current_mtime and cached.path == str(path):
                pdata = cached
                cached_hits += 1
            else:
                # Scan plugin
                try:
                    pdata = self._scan_plugin(plugin_name, path, current_mtime)
                    self._plugin_cache[cache_key] = pdata
                    scanned += 1
                except Exception as e:
                    errors.append(f'Error scanning {plugin_name}: {e}')
                    continue

            # Merge into index
            enabled = plugin_name.lower() in active_lower
            self._merge_plugin(pdata, lo_idx, enabled)

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
        """Force a full rebuild, ignoring cache.

        Clears the in-memory cache AND deletes the on-disk pickle, so
        ``build()`` can't resurrect the stale cache via ``_load_cache()``.
        """
        self._plugin_cache.clear()
        try:
            self._cache_path.unlink(missing_ok=True)
        except OSError:
            pass
        return self.build(**kwargs)

    def set_plugin_enabled(self, plugin_name: str, enabled: bool) -> bool:
        """Flip the ``enabled`` flag on an existing ``PluginInfo`` in place.

        Called by the ``onPluginStateChanged`` event hook so a checkbox
        toggle in MO2's right pane updates query filtering without
        requiring a full index rebuild (~10-15s on a large modlist).

        Returns:
            True if the plugin was known (its flag was set, possibly to
            the same value). False if the plugin is not in the index —
            caller should trigger a rebuild to pick up the new plugin.
        """
        pinfo = self._plugins.get(plugin_name.lower())
        if pinfo is None:
            return False
        pinfo.enabled = enabled
        return True

    # ── Scanning ─────────────────────────────────────────────────────

    def _is_ref_live(self, ref: RecordRef) -> bool:
        """True if the ref's plugin is currently enabled (checkbox on in
        MO2's right pane). Used to filter chains/winners when
        ``include_disabled`` is False."""
        pinfo = self._plugins.get(ref.plugin.lower())
        return pinfo is not None and pinfo.enabled

    def _filter_refs(
        self, refs: list[RecordRef], include_disabled: bool,
    ) -> list[RecordRef]:
        """Drop refs whose plugin is disabled unless ``include_disabled``."""
        if include_disabled:
            return refs
        return [r for r in refs if self._is_ref_live(r)]

    def _scan_plugin(
        self, name: str, path: Path, mtime: float,
    ) -> _PluginCache:
        """Scan a single plugin file and return cached data."""
        records: list[tuple[str, int, str | None]] = []

        with ESPReader(path) as reader:
            tes4 = reader.tes4
            for rec in reader.iter_all_records():
                try:
                    edid = reader.read_edid(rec)
                except Exception:
                    edid = None
                records.append((rec.type, rec.formid, edid, rec.file_offset))

        return _PluginCache(
            name=name,
            path=str(path),
            mtime=mtime,
            masters=tes4.masters,
            is_master=tes4.is_master_flagged,
            is_light=tes4.is_light_flagged,
            is_localized=tes4.is_localized,
            records=records,
        )

    def _merge_plugin(self, pdata: _PluginCache, lo_idx: int, enabled: bool) -> None:
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

        for rec_tuple in pdata.records:
            rec_type, raw_formid, edid = rec_tuple[0], rec_tuple[1], rec_tuple[2]
            file_offset = rec_tuple[3] if len(rec_tuple) > 3 else 0

            origin, local_id = resolve_formid(
                raw_formid, pdata.masters, name,
            )
            key = (origin.lower(), local_id)

            ref = RecordRef(
                plugin=name,
                load_order=lo_idx,
                record_type=rec_type,
                raw_formid=raw_formid,
                file_offset=file_offset,
            )

            if key not in self._records:
                self._records[key] = [ref]
            else:
                self._records[key].append(ref)

            if edid:
                self._edids[edid] = key
                self._key_to_edid[key] = edid

            if rec_type not in self._by_type:
                self._by_type[rec_type] = set()
            self._by_type[rec_type].add(key)

    # ── Cache I/O ────────────────────────────────────────────────────

    def _load_cache(self) -> None:
        if not self._cache_path.exists():
            return
        try:
            with open(self._cache_path, 'rb') as f:
                data = pickle.load(f)
            if isinstance(data, dict):
                self._plugin_cache = data
        except Exception as e:
            log.warning('Failed to load index cache: %s', e)
            self._plugin_cache = {}

    def save_cache(self) -> None:
        """Persist per-plugin scan data to disk."""
        try:
            self._cache_path.parent.mkdir(parents=True, exist_ok=True)
            with open(self._cache_path, 'wb') as f:
                pickle.dump(self._plugin_cache, f, protocol=pickle.HIGHEST_PROTOCOL)
        except Exception as e:
            log.warning('Failed to save index cache: %s', e)

    # ── Query: Conflict Chain ────────────────────────────────────────

    def get_conflict_chain(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> list[RecordRef]:
        """Return all plugins that modify a record, sorted by load order.

        Args:
            origin_plugin: The plugin where the record was first defined.
            local_id: The 24-bit local FormID.
            include_disabled: If True, include refs from plugins whose
                checkbox is off in MO2's right pane. Default False.
        """
        key = (origin_plugin.lower(), local_id)
        refs = self._filter_refs(self._records.get(key, []), include_disabled)
        return sorted(refs, key=lambda r: r.load_order)

    def get_conflict_chain_by_edid(
        self, edid: str, include_disabled: bool = False,
    ) -> list[RecordRef]:
        """Return the conflict chain for a record by its Editor ID."""
        key = self._edids.get(edid)
        if key is None:
            return []
        return self.get_conflict_chain(key[0], key[1], include_disabled)

    def get_winning_record(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> RecordRef | None:
        """Return the winning (last in load order) version of a record.

        With ``include_disabled=False`` (default), returns None if every
        plugin touching this record is currently disabled — prevents
        callers from reporting a "winner" that isn't actually loading
        in-game.
        """
        chain = self.get_conflict_chain(origin_plugin, local_id, include_disabled)
        return chain[-1] if chain else None

    def get_winning_record_by_edid(
        self, edid: str, include_disabled: bool = False,
    ) -> RecordRef | None:
        """Return the winning version of a record by its Editor ID."""
        chain = self.get_conflict_chain_by_edid(edid, include_disabled)
        return chain[-1] if chain else None

    # ── Query: Lookups ───────────────────────────────────────────────

    def lookup_edid(self, edid: str) -> tuple[str, int] | None:
        """Return (origin_plugin, local_id) for an Editor ID, or None.

        Not filtered by enable state — this is a raw index lookup.
        Callers that care about enable state should use the filtered
        chain/winner methods afterwards.
        """
        return self._edids.get(edid)

    def lookup_formid(
        self, origin_plugin: str, local_id: int,
        include_disabled: bool = False,
    ) -> list[RecordRef] | None:
        """Return refs for a resolved FormID, or None if unknown/all-disabled."""
        key = (origin_plugin.lower(), local_id)
        refs = self._records.get(key)
        if refs is None:
            return None
        filtered = self._filter_refs(refs, include_disabled)
        return filtered or None

    # ── Query: By Plugin ─────────────────────────────────────────────

    def get_plugin_info(self, plugin_name: str) -> PluginInfo | None:
        return self._plugins.get(plugin_name.lower())

    def get_plugin_conflicts(
        self, plugin_name: str, include_disabled: bool = False,
    ) -> dict[str, list[tuple[str, int, list[RecordRef]]]]:
        """Return all records a plugin overrides from its masters.

        Returns a dict grouped by record type:
            {record_type: [(origin, local_id, full_chain), ...]}

        With ``include_disabled=False`` (default): if the target plugin
        itself is disabled, returns {}. For each included record, the
        chain is filtered to enabled plugins only; if the plugin is no
        longer in the filtered chain or the chain collapses below 2,
        the record drops out.
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
            # Check if this plugin is in the chain
            plugin_in_chain = any(
                r.plugin.lower() == name_lower for r in refs
            )
            if not plugin_in_chain:
                continue
            # Check if the record originates from a master (not from self)
            origin = key[0]
            if origin == name_lower:
                continue  # This plugin defined it — not an override

            filtered = self._filter_refs(refs, include_disabled)
            if len(filtered) < 2:
                continue
            if not any(r.plugin.lower() == name_lower for r in filtered):
                continue

            rec_type = filtered[0].record_type
            sorted_refs = sorted(filtered, key=lambda r: r.load_order)
            if rec_type not in result:
                result[rec_type] = []
            result[rec_type].append((key[0], key[1], sorted_refs))

        return result

    # ── Query: Conflicts ─────────────────────────────────────────────

    def get_all_conflicts(
        self, record_type: str | None = None, include_disabled: bool = False,
    ) -> Iterator[tuple[tuple[str, int], list[RecordRef]]]:
        """Yield all records modified by more than one plugin.

        Each yield is ((origin, local_id), sorted_refs).
        Optionally filter by record type. With ``include_disabled=False``
        (default), the chain is pre-filtered to enabled plugins and
        records whose filtered chain drops below 2 are skipped.
        """
        for key, refs in self._records.items():
            filtered = self._filter_refs(refs, include_disabled)
            if len(filtered) < 2:
                continue
            if record_type:
                if not any(r.record_type == record_type for r in filtered):
                    continue
            yield key, sorted(filtered, key=lambda r: r.load_order)

    def get_conflict_summary(self, include_disabled: bool = False) -> dict:
        """High-level overview of conflicts across the load order.

        With ``include_disabled=False`` (default), counts reflect only
        conflicts among enabled plugins — matches what the game
        actually sees at runtime.
        """
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

            # Count overrides per plugin (exclude originator)
            origin = key[0]
            for r in filtered:
                if r.plugin.lower() != origin:
                    pname = r.plugin
                    plugin_overrides[pname] = plugin_overrides.get(pname, 0) + 1

        top_types = sorted(type_counts.items(), key=lambda x: -x[1])[:20]
        top_plugins = sorted(plugin_overrides.items(), key=lambda x: -x[1])[:20]

        return {
            'total_conflicts': total,
            'by_type': dict(top_types),
            'top_overriding_plugins': dict(top_plugins),
        }

    # ── Query: Records ───────────────────────────────────────────────

    def query_records(
        self,
        plugin_name: str | None = None,
        record_type: str | None = None,
        edid_filter: str | None = None,
        limit: int = 50,
        offset: int = 0,
        include_disabled: bool = False,
    ) -> list[dict]:
        """Search records with optional filters. Returns dicts.

        With ``include_disabled=False`` (default), records are filtered
        to their enabled-plugin refs before the winner/count fields are
        computed. Records with no enabled refs drop out entirely, so a
        ``plugin_name`` filter pointed at a disabled plugin returns an
        empty list unless ``include_disabled=True``.
        """
        results: list[dict] = []
        count = 0

        for key, refs in self._records.items():
            # Filter by record type (on raw refs -- type is stable)
            if record_type and not any(
                r.record_type == record_type for r in refs
            ):
                continue

            # Filter by plugin (on raw refs -- check if plugin touches this record at all)
            if plugin_name:
                pn_lower = plugin_name.lower()
                if not any(r.plugin.lower() == pn_lower for r in refs):
                    continue

            # Filter by EditorID substring
            edid = self._key_to_edid.get(key)

            if edid_filter:
                if edid is None or edid_filter.lower() not in edid.lower():
                    continue

            # Apply enable-state filter
            live_refs = self._filter_refs(refs, include_disabled)
            if not live_refs:
                continue

            # If a plugin_name filter was provided but it's disabled, drop
            # this record -- the caller asked for records from this plugin
            # and the plugin isn't currently live.
            if plugin_name:
                pn_lower = plugin_name.lower()
                if not any(r.plugin.lower() == pn_lower for r in live_refs):
                    continue

            # Pagination
            if count < offset:
                count += 1
                continue
            if len(results) >= limit:
                break

            winner = sorted(live_refs, key=lambda r: r.load_order)[-1]
            results.append({
                'origin': key[0],
                'local_id': f'{key[1]:06X}',
                'formid': f'{key[0]}:{key[1]:06X}',
                'record_type': winner.record_type,
                'editor_id': edid,
                'winning_plugin': winner.plugin,
                'override_count': len(live_refs),
            })
            count += 1

        return results
