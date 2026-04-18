"""
test_esp_index.py - Test the load order indexer and conflict detector.

Usage:
    python test_esp_index.py                    # scan first 20 plugins
    python test_esp_index.py --count 50         # scan first 50 plugins
    python test_esp_index.py --full             # scan ALL plugins (slow)
    python test_esp_index.py --cache-test       # test cache round-trip
"""

from __future__ import annotations

import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from esp_index import (
    LoadOrderIndex,
    PluginResolver,
    read_load_order,
    read_active_plugins,
)


MODLIST_ROOT = Path(os.environ.get('MO2_MODLIST_ROOT', r'.'))
PROFILE_DIR = Path(os.environ.get('MO2_PROFILE_DIR', str(MODLIST_ROOT / 'profiles' / 'Default')))


def print_header(title: str) -> None:
    print(f'\n{"=" * 60}')
    print(f'  {title}')
    print(f'{"=" * 60}')


def test_resolver() -> None:
    print_header('Plugin Resolver')
    resolver = PluginResolver(MODLIST_ROOT)

    test_names = [
        'Skyrim.esm', 'Update.esm', 'Dawnguard.esm',
        'SkyUI_SE.esp', 'nonexistent_plugin.esp',
    ]
    for name in test_names:
        path = resolver.resolve(name)
        if path:
            size_mb = path.stat().st_size / 1024 / 1024
            print(f'  {name:40s} -> {size_mb:.1f} MB')
        else:
            print(f'  {name:40s} -> NOT FOUND')


def test_load_order() -> None:
    print_header('Load Order')
    lo = read_load_order(PROFILE_DIR)
    active = read_active_plugins(PROFILE_DIR)
    print(f'  Plugins in loadorder.txt: {len(lo)}')
    print(f'  Active in plugins.txt:    {len(active)}')
    print(f'  First 10:')
    for i, name in enumerate(lo[:10]):
        marker = '*' if name.lower() in {a.lower() for a in active} else ' '
        print(f'    [{i:4d}] {marker} {name}')


def test_build(count: int) -> LoadOrderIndex:
    lo = read_load_order(PROFILE_DIR)
    subset = lo[:count]
    print_header(f'Index Build ({len(subset)} plugins)')

    idx = LoadOrderIndex(MODLIST_ROOT, PROFILE_DIR)

    def progress(name, i, total):
        if i % 50 == 0 or i == total - 1:
            print(f'  [{i+1:4d}/{total}] {name}')

    result = idx.build(load_order=subset, progress_cb=progress)

    print()
    for k, v in result.items():
        if k == 'errors':
            print(f'  errors ({len(v)}):')
            for e in v[:10]:
                print(f'    - {e}')
        else:
            print(f'  {k}: {v}')

    return idx


def test_edid_lookup(idx: LoadOrderIndex) -> None:
    print_header('EditorID Lookup')

    test_edids = [
        'IronSword', 'DragonbornFrostResist50',
        'ArmorIronCuirass', 'HideBoots',
        'DA14DremoraGreatswordFire03', 'DremoraBoots',
        'EncVampire00BretonF', 'nonexistent_edid_xyz',
    ]
    for edid in test_edids:
        key = idx.lookup_edid(edid)
        if key:
            origin, local_id = key
            chain = idx.get_conflict_chain(origin, local_id)
            winner = chain[-1] if chain else None
            plugins = [r.plugin for r in chain]
            print(f'  {edid}:')
            print(f'    FormID: {origin}:{local_id:06X}')
            print(f'    Chain ({len(chain)}): {" -> ".join(plugins)}')
            if winner:
                print(f'    Winner: {winner.plugin}')
        else:
            print(f'  {edid}: not found')


def test_conflict_chain(idx: LoadOrderIndex) -> None:
    print_header('Conflict Chains (first 10 multi-plugin conflicts)')

    count = 0
    for key, refs in idx.get_all_conflicts():
        if count >= 10:
            break
        origin, local_id = key
        # Find editor ID
        edid = None
        for eid, ekey in idx._edids.items():
            if ekey == key:
                edid = eid
                break

        print(f'\n  {origin}:{local_id:06X} ({refs[0].record_type})'
              f'{f" [{edid}]" if edid else ""}')
        for r in refs:
            marker = '<-- winner' if r == refs[-1] else ''
            print(f'    [{r.load_order:4d}] {r.plugin:40s} {marker}')
        count += 1

    if count == 0:
        print('  No conflicts found')


def test_plugin_conflicts(idx: LoadOrderIndex) -> None:
    print_header('Plugin Conflict Report')

    # Find a plugin that has overrides
    for pname, pinfo in sorted(
        idx._plugins.items(), key=lambda x: x[1].load_order,
    ):
        conflicts = idx.get_plugin_conflicts(pinfo.name)
        if conflicts:
            total = sum(len(v) for v in conflicts.values())
            print(f'  Plugin: {pinfo.name} (load order {pinfo.load_order})')
            print(f'  Total overrides: {total}')
            print()
            for rtype, entries in sorted(
                conflicts.items(), key=lambda x: -len(x[1]),
            )[:10]:
                print(f'    {rtype}: {len(entries)} overrides')
                for origin, local_id, chain in entries[:3]:
                    plugins = [r.plugin for r in chain]
                    # Find edid
                    edid = None
                    for eid, ekey in idx._edids.items():
                        if ekey == (origin, local_id):
                            edid = eid
                            break
                    print(f'      {origin}:{local_id:06X}'
                          f'{f" [{edid}]" if edid else ""}'
                          f'  chain: {" -> ".join(plugins)}')
                if len(entries) > 3:
                    print(f'      ... and {len(entries) - 3} more')
            break
    else:
        print('  No plugin with overrides found')


def test_conflict_summary(idx: LoadOrderIndex) -> None:
    print_header('Conflict Summary')
    summary = idx.get_conflict_summary()
    print(f'  Total conflicted records: {summary["total_conflicts"]:,}')

    print(f'\n  By record type:')
    for rtype, count in list(summary['by_type'].items())[:15]:
        print(f'    {rtype:<8} {count:>8,}')

    print(f'\n  Top overriding plugins:')
    for pname, count in list(summary['top_overriding_plugins'].items())[:10]:
        print(f'    {pname:50s} {count:>6,}')


def test_query(idx: LoadOrderIndex) -> None:
    print_header('Record Query')

    print('  Query: record_type=ARMO, limit=5')
    for rec in idx.query_records(record_type='ARMO', limit=5):
        print(f'    {rec["formid"]:30s} {rec.get("editor_id",""):30s} '
              f'winner={rec["winning_plugin"]}')

    print()
    print('  Query: edid_filter="Iron", limit=5')
    for rec in idx.query_records(edid_filter='Iron', limit=5):
        print(f'    {rec["formid"]:30s} {rec.get("editor_id",""):30s} '
              f'type={rec["record_type"]}')


def test_cache(idx: LoadOrderIndex) -> None:
    print_header('Cache Round-Trip')

    idx.save_cache()
    cache_path = idx._cache_path
    cache_size = cache_path.stat().st_size / 1024 / 1024
    print(f'  Cache file: {cache_path}')
    print(f'  Cache size: {cache_size:.1f} MB')

    # Rebuild using cache
    print(f'\n  Rebuilding from cache...')
    lo = idx._load_order
    idx2 = LoadOrderIndex(MODLIST_ROOT, PROFILE_DIR)
    t0 = time.perf_counter()
    result = idx2.build(load_order=lo)
    elapsed = time.perf_counter() - t0
    print(f'  Cache rebuild: {elapsed:.2f}s')
    print(f'  Scanned (cache misses): {result.get("scanned", "?")}')
    print(f'  Cache hits: {result.get("cached_hits", "?")}')
    print(f'  Stats match: {idx.stats == idx2.stats}')


def main() -> None:
    args = sys.argv[1:]
    count = 20
    full = False
    cache_test = False

    while args:
        if args[0] == '--count' and len(args) > 1:
            count = int(args[1])
            args = args[2:]
        elif args[0] == '--full':
            full = True
            args = args[1:]
        elif args[0] == '--cache-test':
            cache_test = True
            args = args[1:]
        else:
            print(f'Unknown arg: {args[0]}')
            sys.exit(1)

    if full:
        lo = read_load_order(PROFILE_DIR)
        count = len(lo)

    test_resolver()
    test_load_order()
    idx = test_build(count)
    test_edid_lookup(idx)
    test_conflict_chain(idx)
    test_plugin_conflicts(idx)
    test_conflict_summary(idx)
    test_query(idx)

    if cache_test:
        test_cache(idx)

    print(f'\n{"=" * 60}')
    print(f'  Done')
    print(f'{"=" * 60}')


if __name__ == '__main__':
    main()
