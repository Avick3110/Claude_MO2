# Phase 0 Handoff — Audit + scope lock + version bump to v2.7.1

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1h
**Commits made:** `49d9a28` (`[v2.7.1 P0] Audit + scope lock + version bump to 2.7.1`), pushed to `origin/main`.
**Live install synced:** No (Phase 0 is non-runtime — audit, probe, version bumps, doc placeholders. Phase 5 syncs.)

## What was done

- **`dev/plans/v2.7.1_race_patching/AUDIT.md`** (NEW) — operator-by-record-type matrix. One section per operator, with three blocks each: Currently dispatched / Mutagen-supported but not dispatched (P3 wire-up rows) / Out-of-scope rationale. The "Wire up in P3" set totals **16 (operator, record-type) pairs across 9 record types**:
  - `add_keywords` + `remove_keywords` × 6 records (Race, Furniture, Activator, Location, Spell, MagicEffect) = 12 pairs
  - `add_spells` + `remove_spells` × 1 record (Race) = 2 pairs
  - `add_items` × 2 records (LeveledNpc, LeveledSpell) = 2 pairs
  - **Total: 16 pairs.**
  - All "out of scope" rows are schema-confirmed absences (Container/Door/Light/Perk/Quest/Cell/Worldspace/Region don't have Keywords; AMMO has no ObjectEffect; etc.) — none are guesses or assumptions.
- **`dev/plans/v2.7.1_race_patching/PLAN.md`** — already present in working tree, force-added in this commit.
- **`tools/race-probe/Program.cs`** — extended with 7 new verification blocks (one per audit-identified record type) plus a single-mod round-trip section. Each block: constructs the record, dumps the property via reflection, mutates it, and the round-trip block writes all records into one ESP and reads back to confirm.
- **`tools/race-probe/Program.cs`** also gained a `using SkActivator = Mutagen.Bethesda.Skyrim.Activator;` alias to disambiguate `Activator` from `System.Activator` (in scope via implicit usings).
- **Version bumps (4 files):**
  - `mo2_mcp/config.py:9` — `PLUGIN_VERSION = (2, 7, 0)` → `(2, 7, 1)`
  - `installer/claude-mo2-installer.iss:21` — `#define AppVersion "2.7.0"` → `"2.7.1"`
  - `README.md:7` — installer download URL `claude-mo2-setup-v2.7.0.exe` → `v2.7.1.exe`
  - `README.md:59` — Manual Install section installer reference, same swap
- **`mo2_mcp/CHANGELOG.md`** — new top entry `## v2.7.1 — TBD` placeholder, points at PLAN.md for expected sections; Phase 4 finalizes.
- **`KNOWN_ISSUES.md`** — new "v2.7.1 in flight (placeholder — finalized in Phase 4)" section right after the file header. References `PLAN.md` and `AUDIT.md` for current state of the planned write-surface expansion. Does not pre-claim anything; Phase 4 finalizes wording.
- **`dev/plans/v2.7.1_race_patching/PHASE_0_HANDOFF.md`** — this file.

**No production code touched.** PatchEngine.cs and tools_patching.py are unchanged from `e77afcd`. The probe (in `tools/race-probe/`) is a non-shipped diagnostic, not production.

## Verification performed

**1. Probe runs green.** `cd tools/race-probe && dotnet run -c Release` completes with `=== probe complete ===` (no `*** FAIL` lines, exit code 0). Round-trip ESP at `%TEMP%\AuditProbe.esp` is 906 bytes.

Probe output for the new audit verification (abbreviated — full output in build cache):

```
=== P0 audit: Furniture.Keywords (ExtendedList<IFormLinkGetter<IKeywordGetter>>) ===
  Keywords: declared type Noggog.ExtendedList<...IFormLinkGetter<...IKeywordGetter>>, runtime <null>, has setter True
  in-memory Keywords.Count = 2

=== P0 audit: Activator.Keywords ===
  Keywords: declared type Noggog.ExtendedList<...IFormLinkGetter<...IKeywordGetter>>, runtime <null>, has setter True
  in-memory Keywords.Count = 1

=== P0 audit: Location.Keywords ===
  Keywords: declared type Noggog.ExtendedList<...IFormLinkGetter<...IKeywordGetter>>, runtime <null>, has setter True
  in-memory Keywords.Count = 1

=== P0 audit: Spell.Keywords ===
  Keywords: declared type Noggog.ExtendedList<...IFormLinkGetter<...IKeywordGetter>>, runtime <null>, has setter True
  in-memory Keywords.Count = 1

=== P0 audit: MagicEffect.Keywords ===
  Keywords: declared type Noggog.ExtendedList<...IFormLinkGetter<...IKeywordGetter>>, runtime <null>, has setter True
  in-memory Keywords.Count = 1

=== P0 audit: LeveledNpc.Entries (LeveledNpcEntry, Reference IFormLinkGetter<INpcSpawnGetter>) ===
  Entries: declared type Noggog.ExtendedList<...LeveledNpcEntry>, runtime <null>, has setter True
  in-memory Entries.Count = 1, Entry[0].Reference.FormKey = 00B001:Skyrim.esm

=== P0 audit: LeveledSpell.Entries (LeveledSpellEntry, Reference IFormLinkGetter<ISpellRecordGetter>) ===
  Entries: declared type Noggog.ExtendedList<...LeveledSpellEntry>, runtime <null>, has setter True
  in-memory Entries.Count = 1, Entry[0].Reference.FormKey = 00C001:Skyrim.esm

=== P0 audit: round-trip all audit records through WriteToBinary + CreateFromBinary ===
  wrote: ...\AuditProbe.esp (906 bytes)
  Furniture readback:    Keywords.Count = 2
  Activator readback:    Keywords.Count = 1
  Location readback:     Keywords.Count = 1
  Spell readback:        Keywords.Count = 1
  MagicEffect readback:  Keywords.Count = 1
  LeveledNpc readback:   Entries.Count  = 1, Entry[0].Reference.FormKey = 00B001:Skyrim.esm
  LeveledSpell readback: Entries.Count  = 1, Entry[0].Reference.FormKey = 00C001:Skyrim.esm

=== probe complete ===
```

All 7 audit-identified record types: property exists with the expected name + type, mutation works in-memory, round-trip preserves the data. Zero reclassifications driven by probe failures.

**2. Existing race-probe sections still pass.** The pre-existing Race verification (Starting/Regen indexer writes, round-trip of Starting/Regen/UnarmedDamage/UnarmedReach, in-memory Keywords/ActorEffect mutation) all still pass — the extension is purely additive.

**3. Version bumps correct in all four files.** Spot-checked: `config.py` line 9, `.iss` line 21, `README.md` lines 7 and 59. No other v2.7.0 references exist in shippable files (CHANGELOG entries for v2.7.0 are historical and stay).

**4. Mutagen schema cross-reference matches probe output.** The Loqui XML schemas at `https://github.com/Mutagen-Modding/Mutagen/tree/dev/Mutagen.Bethesda.Skyrim/Records/Major%20Records` were spot-checked against the runtime types reported by the probe. Every type signature in AUDIT.md was confirmed by both schema and probe output.

## Deviations from plan

- **Used a single round-trip mod for all 7 audit records** rather than 7 separate WriteToBinary/CreateFromBinary cycles (one per type). The single-mod approach is cleaner, faster, and exercises the same code paths. The probe is a regression check; one round-trip vs seven changes nothing about coverage.
- **`SkActivator` alias added** for `Mutagen.Bethesda.Skyrim.Activator` to disambiguate from `System.Activator`. PLAN.md didn't anticipate this — it's a C# language quirk under `<ImplicitUsings>enable</ImplicitUsings>`.
- **One nuance worth flagging for Phase 1's `OperatorModsKeys` mapping:** in PatchEngine.cs at line 403, `set_flags` and `clear_flags` BOTH write to `mods["flags_changed"]`. The Tier D coverage check therefore needs `OperatorModsKeys["SetFlags"] = "flags_changed"` and `OperatorModsKeys["ClearFlags"] = "flags_changed"` mapping to the SAME key — the unmatched check should treat "key present" as satisfying any operator that maps to it. PLAN.md already calls this out in step 1's example dict; Phase 1 just needs to honor the shared-key semantics.

No deviations from PLAN.md's stated AUDIT.md scope. Every operator the plan named got a section.

## Known issues / open questions

- **None blocking.** All 16 P3 wire-up pairs are probe-green; no AUDIT.md rows need reclassification.
- **`add_conditions` on QUST** stays out of scope per AUDIT.md (Quest has `DialogConditions` + `EventConditions`, not `Conditions` — would need a new operator parameter). Tier D will surface attempts as clean unmatched-operator errors. v2.8 candidate.
- **`attach_scripts` adapter-subclass issue** (PerkAdapter / QuestAdapter) flagged in AUDIT.md. The bridge's reflection cast `vmadProp.GetValue(record) as VirtualMachineAdapter` succeeds for the base type, so Tier D will NOT catch this — failures here will look like "scripts attached, mods reports N" but the adapter shape is wrong. v2.8 candidate; needs a per-type adapter factory, separate concern from the v2.7.1 wire-up theme.
- **Tier D coverage check semantics — handler-matched-but-zero-applied is success.** PLAN.md is explicit on this (e.g. `mods["spells_added"] = 0` is valid; only missing-key is the error). Worth re-confirming when implementing Phase 1: the handler must always write its mods key when the operator field is non-empty, even if the count is 0 (e.g. all items already present and dedup'd). PatchEngine.cs's existing handlers conditionally write their mods key only when `added > 0` — see `if (added > 0) mods["perks_added"] = added;` at line 440. **Phase 1 must change every such conditional to unconditional write** so the Tier D check works correctly. Otherwise a request like `add_perks` against an NPC where every perk is already present would be misclassified as unmatched and rolled back. Flagging here so Phase 1's first move is to scrub these conditions out.

## Preconditions for Phase 1

| Precondition (per PLAN.md) | Status |
|---|---|
| AUDIT.md exists with operator/mods-key mapping | ✓ Met (see top section of AUDIT.md) |
| PatchEngine.cs untouched (P0 doesn't modify production code) | ✓ Met — `git diff e77afcd -- tools/mutagen-bridge/PatchEngine.cs` is empty |
| Version bumps landed (so any P1 commit reflects v2.7.1, not a v2.7.0 rebuild) | ✓ Met (4 files) |
| Probe builds + runs green (so Phase 1's smoke can extend it cleanly) | ✓ Met |
| Probe extended for every "wire up in P3" record type | ✓ Met (7 new blocks, all green) |

## Files of interest for next phase

- **`dev/plans/v2.7.1_race_patching/AUDIT.md`** — Phase 1 reads the operator/mods-key mapping table at the top to populate `OperatorModsKeys`. Also: the per-operator sections enumerate which (operator, record-type) pairs are valid post-P3 — important context for the Tier D semantics ("handler matched but zero applied = success" vs "no handler matched = error").
- **`tools/mutagen-bridge/PatchEngine.cs`** — Phase 1 wraps `ApplyModifications` with operator-coverage tracking. Critical lines:
  - Class `PatchEngine` (top of file) — for `OperatorModsKeys` static dict.
  - `ProcessOverride` at line 153 — calls `ApplyModifications` at line 191; this is where the try/catch for `UnsupportedOperatorException` lives.
  - `ApplyModifications` at line 393 — Phase 1 wraps entry/exit. Note the per-handler `if (added > 0) mods["X"] = added;` pattern at lines like 440, 450, 467, 478 — needs to change to unconditional write per the Known Issues note above.
  - `TryRemoveOverride` at line 1331 — already exists; Phase 1's catch block calls it unchanged.
- **`tools/race-probe/`** — Phase 1's inline smoke test extends the probe (or writes a sibling under `tools/coverage-smoke/`) with a deliberately-failing operator/record combo (e.g. `add_perks` on Container) and confirms the bridge returns the structured error.
- **`PLAN.md` § Phase 1 (lines 341–421)** — exact step-by-step recipe, including the canonical `OperatorModsKeys` dict shape.
