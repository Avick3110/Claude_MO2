"""
test_esp_index.py — Format-contract test for v2.6.0 Phase 3.

Locks in the invariant that the receive-side normaliser
(``_canonicalise_bridge_formid``, called when a bridge ScannedRecord lands
in the cache) and the lookup-side builder (``make_formid_key``, called
when a public-API caller asks `lookup_formid(plugin, local_id)`) produce
**byte-identical** strings.

If those two ever diverge — different hex casing, different separator,
different plugin-name normalisation — every cache lookup on a freshly-
scanned plugin would silently return None. The test catches drift before
the rebuild lands instead of surfacing as "every record I just scanned
is invisible to my queries".

This file is the only Python test that ships with v2.6.0 Phase 3. The
v2.5.x tests (``test_esp_reader.py``, the old ``test_esp_index.py``) are
archived to ``dev/archive/v2.6_retired/``; they tested the deleted
ESPReader and PluginResolver code paths. Future phases that want broader
test coverage should write fresh fixtures against the new shape — see
PHASE_3_HANDOFF.md.

Standalone runnable: ``python test_esp_index.py``. No MO2 / Mutagen
dependencies — pure-Python contract assertions.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from esp_index import make_formid_key, _canonicalise_bridge_formid


# Sample bridge ScannedRecord FormIDs that exercise the casing /
# extension / range cases the cache will see in production. Cribbed
# from PHASE_3_HARNESS_OUTPUT.md (NyghtfallMM ESL records that
# motivated the migration), plus a handful of plain ESM/ESP examples.
_BRIDGE_FORMID_SAMPLES = [
    # ESL with compacted slot ID — the v2.6.0 bug-target shape
    ('NyghtfallMM.esp', 0x000884, 'NyghtfallMM.esp:000884'),
    ('NyghtfallMM.esp', 0x000885, 'NyghtfallMM.esp:000885'),
    ('NyghtfallMM.esp', 0x000889, 'NyghtfallMM.esp:000889'),
    # NyghtfallMM's overrides of vanilla records — origin resolves to Skyrim.esm
    ('Skyrim.esm', 0x013686, 'Skyrim.esm:013686'),
    ('Skyrim.esm', 0x017035, 'Skyrim.esm:017035'),
    ('Skyrim.esm', 0x05221E, 'Skyrim.esm:05221E'),  # MUSReveal — the bug-target
    # Plain ESM
    ('Update.esm', 0x012345, 'Update.esm:012345'),
    ('Dawnguard.esm', 0x000ABC, 'Dawnguard.esm:000ABC'),
    # Plain ESP at high local IDs
    ('Requiem.esp', 0xABCDEF, 'Requiem.esp:ABCDEF'),
    ('Requiem.esp', 0xFFFFFF, 'Requiem.esp:FFFFFF'),
    # Names with characters allowed in plugin filenames
    ("Cleaner's Bag.esp", 0x000001, "Cleaner's Bag.esp:000001"),
    ('Mod-With-Hyphen.esp', 0x000002, 'Mod-With-Hyphen.esp:000002'),
    ('Mod_With_Underscore.esp', 0x000003, 'Mod_With_Underscore.esp:000003'),
    # Mixed-case plugin name from a non-canonical source — both
    # normalisers must agree on the lowercase form
    ('NYGHTFALLMM.esp', 0x000884, 'NYGHTFALLMM.esp:000884'),
    ('nyghtfallmm.ESP', 0x000884, 'nyghtfallmm.ESP:000884'),
]


def test_make_formid_key_basic_shape() -> None:
    """Smoke check: lowercase plugin + 6-digit uppercase hex, joined by ':'."""
    assert make_formid_key('Skyrim.esm', 0x012E49) == 'skyrim.esm:012E49'
    assert make_formid_key('SKYRIM.ESM', 0x012E49) == 'skyrim.esm:012E49'
    assert make_formid_key('NyghtfallMM.esp', 0x000884) == 'nyghtfallmm.esp:000884'
    # Hex padding for small IDs.
    assert make_formid_key('Skyrim.esm', 0x1) == 'skyrim.esm:000001'
    # Hex casing — uppercase, no 0x prefix.
    assert make_formid_key('Skyrim.esm', 0xABCDEF) == 'skyrim.esm:ABCDEF'
    print('  test_make_formid_key_basic_shape passed')


def test_canonicalise_bridge_formid_inverse() -> None:
    """The bridge-receive normaliser must produce the same string the
    lookup-side builder produces from the same (plugin, local_id) inputs.

    This is the load-bearing assertion: if these two ever diverge for
    any input, every lookup on freshly-scanned records misses silently.
    """
    failures = []
    for plugin, local_id, bridge_str in _BRIDGE_FORMID_SAMPLES:
        canonical_via_make = make_formid_key(plugin, local_id)
        result = _canonicalise_bridge_formid(bridge_str)
        if result is None:
            failures.append(
                f'_canonicalise_bridge_formid({bridge_str!r}) returned None'
            )
            continue
        canonical_via_bridge, returned_local = result
        if canonical_via_bridge != canonical_via_make:
            failures.append(
                f'mismatch for {plugin!r}+{local_id:#x}:\n'
                f'  make_formid_key            -> {canonical_via_make!r}\n'
                f'  _canonicalise_bridge_formid -> {canonical_via_bridge!r}'
            )
        if returned_local != local_id:
            failures.append(
                f'local_id mismatch for {bridge_str!r}: '
                f'sent {local_id:#x}, got {returned_local:#x}'
            )

    if failures:
        msg = '\n'.join(failures)
        raise AssertionError(
            f'Format-contract violations ({len(failures)}):\n{msg}'
        )
    print(f'  test_canonicalise_bridge_formid_inverse passed '
          f'({len(_BRIDGE_FORMID_SAMPLES)} samples)')


def test_canonicalise_bridge_formid_malformed() -> None:
    """Malformed bridge inputs return None rather than raising."""
    bad_inputs = [
        '',
        'NoColonHere',
        ':missingPlugin',
        'plugin.esp:',
        'plugin.esp:NotHex',
        'plugin.esp:GHIJKL',  # invalid hex chars
    ]
    for bad in bad_inputs:
        assert _canonicalise_bridge_formid(bad) is None, (
            f'_canonicalise_bridge_formid({bad!r}) should have returned None, '
            f'got {_canonicalise_bridge_formid(bad)!r}'
        )
    print(f'  test_canonicalise_bridge_formid_malformed passed '
          f'({len(bad_inputs)} bad inputs)')


def test_round_trip_through_make_formid_key() -> None:
    """For every sample, parse the bridge string and rebuild via
    make_formid_key — the resulting string must equal the canonical form."""
    for _plugin, _local_id, bridge_str in _BRIDGE_FORMID_SAMPLES:
        canonical_a = _canonicalise_bridge_formid(bridge_str)[0]
        # Pretend we're a public-API caller who got (origin_lower, local_id)
        # from somewhere and wants to rebuild the canonical key.
        plugin_lower, local_hex = canonical_a.rsplit(':', 1)
        local_id = int(local_hex, 16)
        canonical_b = make_formid_key(plugin_lower, local_id)
        assert canonical_a == canonical_b, (
            f'round-trip mismatch: {bridge_str!r} -> {canonical_a!r} '
            f'-> rebuild via ({plugin_lower!r}, {local_id:#x}) -> {canonical_b!r}'
        )
    print('  test_round_trip_through_make_formid_key passed')


def main() -> int:
    print('test_esp_index.py — v2.6.0 Phase 3 format-contract tests')
    print('=' * 60)
    tests = [
        test_make_formid_key_basic_shape,
        test_canonicalise_bridge_formid_inverse,
        test_canonicalise_bridge_formid_malformed,
        test_round_trip_through_make_formid_key,
    ]
    failed = 0
    for t in tests:
        try:
            t()
        except AssertionError as exc:
            print(f'FAIL  {t.__name__}: {exc}')
            failed += 1
        except Exception as exc:
            print(f'ERROR {t.__name__}: {type(exc).__name__}: {exc}')
            failed += 1
    print('=' * 60)
    print(f'{"FAIL" if failed else "PASS"}: {len(tests) - failed}/{len(tests)} tests passed')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
