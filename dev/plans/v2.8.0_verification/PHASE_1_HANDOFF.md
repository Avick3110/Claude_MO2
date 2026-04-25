# Phase 1 Handoff — Effects-list write capability + version bump to 2.8.0

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~3h
**Commits made:** `[v2.8 P1] Effects-list write capability + version bump to 2.8.0` (work) + `[v2.8 P1] Handoff: record commit hash <work-hash>` (hash record). See work-commit body for the bridge SHA and audit summary.
**Live install synced:** No (Phase 1 does not touch the live install per scope-lock; live remains v2.7.1 until Phase 5).

## What was done

### Probe-first discipline (per plan)

- **Extended `tools/race-probe/Program.cs`** with an Effects API contract block (~250 lines). For each of `{Spell, Ingestible, ObjectEffect, Scroll, Ingredient}`: probed the `Effects` property type, the `Effect` runtime shape, the `EffectData` sub-LoquiObject shape, the `Conditions` collection, and round-tripped a constructed Effect through `WriteToBinary` + `CreateFromBinary` with read-back assertions on `BaseEffect.FormKey`, `Data.{Magnitude,Area,Duration}`, and `Conditions.Count`.
- **Constructibility section** explicitly tested `Activator.CreateInstance` on `Effect`, `Condition`, `ConditionFloat`, `ConditionGlobal`, and `EffectData` (per Aaron's required check). Result: `typeof(Condition)` throws (`Cannot create an abstract class`); the other four succeed. This justified the bridge's `typeof(Condition)` special case in Branch A.
- **Wrote `dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md`** capturing the runtime contract, the Branch B decision (REQUIRED — `Effect.Data` is a sub-LoquiObject, null-initialized), the bonus-catch prediction (`Effect.BaseEffect` is `IFormLinkNullable<>`, which the existing FormLink branches in `ConvertJsonElementToListItem` don't cover), and the bridge SHA.

### Bridge implementation (`tools/mutagen-bridge/PatchEngine.cs`)

- **`BuildCondition(ConditionEntry)`** factory extracted from `ApplyAddConditions`. `ApplyAddConditions` now calls it in a loop. `BuildConditionFromJson(JsonElement)` wraps the same factory for Branch A's `typeof(Condition)` special case. Single-source-of-truth — one Condition construction codepath used by both `add_conditions` and nested `Effects[i].Conditions`.
- **Branch A** in `ConvertJsonElementToListItem`: when JSON Object element + non-FormLink class element type, special-case `typeof(Condition)` → `BuildConditionFromJson`; otherwise `Activator.CreateInstance(elementType)` + recursive `SetPropertyByPath` per JSON member.
- **Branch B** in `SetPropertyByPath`: when JSON Object value + non-dict, non-FormLink class target property, get-or-Activator-create the sub-instance and recursively `SetPropertyByPath` per JSON member. In-place merge — preserves siblings. Inserted between the dict-merge branch and the `ConvertJsonValue` fallback.
- **`IsFormLinkType` + `IsNullableFormLinkType` helpers** added near `IsClosedDictionary`. The first guards Branch B against incorrectly Activator-creating empty FormLinks; the second picks the right concrete (`FormLinkNullable<T>` for nullable target types) for the bonus-catch fix.
- **Bonus-catch fix** in `ConvertJsonValue`: JSON String → single-field FormLink branch covering all five Mutagen FormLink shapes (`IFormLinkGetter<T>` / `IFormLink<T>` / `IFormLinkNullable<T>` / `FormLink<T>` / `FormLinkNullable<T>`). Required by Phase 1's contract — without it, `Effects[i].BaseEffect` writes fail because `Effect.BaseEffect` is `IFormLinkNullable<IMagicEffectGetter>`. Predicted by audit, deterministically reproduced in smoke (6 of 8 Layer 1.E cells failed pre-fix), folded in per the Phase 1 plan's bonus-catch precedent. Side benefit (not advertised in schema): every record type's single-field FormLink properties are now writable via `set_fields`.

### Coverage smoke (`tools/coverage-smoke/Program.cs`)

- **8 new tests** for Layer 1.E (tests 23–30 mapping to MATRIX.md cells 1.E.01–1.E.08): SPEL replace, SPEL with nested Conditions, ALCH/ENCH/SCRL/INGR replace, SPEL empty-array clear, SPEL bad-FormLink rollback. Each uses Mutagen-direct readback (not bridge) for independent verification.
- **`VerifyEffectsReplace` helper** for compact per-cell assertion. Operates on `IReadOnlyList<IEffectGetter>` (the read-only shape `CreateFromBinaryOverlay` returns).
- **Source-record picks** (one per record type) chosen via `FirstOrDefault` predicate against `source.<Collection>.Effects.Count >= 1`; fresh MGEF FormKeys picked via `FreshMgefFor` helper to make the replace-semantics assertion meaningful.
- **Imports added**: `System.Reflection`, `Noggog`.

### Schema + docs

- **`mo2_mcp/tools_patching.py`** `set_fields` description appended with one clause documenting the Effects-array form on SPEL/ALCH/ENCH/SCRL/INGR with replace semantics. Per scope-lock, sub-LoquiObject merge and single-field FormLink writes are NOT advertised — they are side benefits, not user-facing surfaces.
- **`mo2_mcp/CHANGELOG.md`**: `## v2.8.0 — TBD` heading inserted above v2.7.1; two-bullet `### Added — bridge` entry covering the Effects-list write headline and the FormLink-via-`set_fields` bonus-catch.
- **`KNOWN_ISSUES.md`**: New v2.8.0 patching write surface section above v2.7.1's. Per-effect-spell-conditions removed from carry-overs (now supported); the "Spell conditions apply at effect level" design-trade-off section reframed to point at the new Effects-list path. v2.7.1's "v2.8 candidates" carry-over list collapsed to a single-line reference to the v2.8.0 forward list.

### Version bump (first commit of v2.8.0 era)

- `mo2_mcp/config.py`: `PLUGIN_VERSION = (2, 8, 0)`.
- `installer/claude-mo2-installer.iss` line 21: `#define AppVersion "2.8.0"`.
- `README.md` lines 7 + 59: `claude-mo2-setup-v2.7.1.exe` → `claude-mo2-setup-v2.8.0.exe`.

## Verification performed

### Probe (race-probe)

`dotnet run -c Release --project tools/race-probe` — exits 0 cleanly (`=== probe complete ===`). All five record types round-trip a constructed Effect through binary write+read; `BaseEffect.FormKey`, `Data.{Magnitude=50, Area=10, Duration=30}`, and `Conditions.Count=1` all preserved on read-back. Constructibility: 4 of 5 OK (Condition expected fail).

### Bridge build

`dotnet build -c Release tools/mutagen-bridge` — 0 warnings, 0 errors.

**Bridge SHA after Phase 1 build:**
`f998c4e022450633c3a4f3f4e1ee737e6f0f0d8a992c76a3be8efa6d86c8bb04` (mutagen-bridge.exe at `tools/mutagen-bridge/bin/Release/net8.0/`)

### Coverage smoke

`dotnet run -c Release --project tools/coverage-smoke` — **30 PASS / 0 FAIL**.

| Layer | Cells | Pass | Fail |
|---|---:|---:|---:|
| Pre-existing v2.7.1 (tests 1–22) | 22 | 22 | 0 |
| **Layer 1.E** (tests 23–30) | 8 | 8 | 0 |
| **Total** | **30** | **30** | **0** |

Per-cell evidence captured in EFFECTS_AUDIT.md § Smoke verification.

### Refactor regression check

After the BuildCondition extraction (largest regression-surface change), ran the 22 pre-existing tests in isolation — all PASS. Specifically test 3 (`remove_conditions on ARMO` — the alignment-sentinel for the conditions code path) passed clean. Then ran the full smoke after Branch A + Branch B + bonus-catch landed — still 22/22 plus 8/8 new = 30/30.

### Bonus-catch reproduction + fix evidence

Pre-fix smoke: 6 of 8 Layer 1.E cells failed with identical message `"Cannot convert JSON String to IFormLinkNullable<IMagicEffectGetter>"` — confirms the audit's prediction. Test 30 (bad-FormLink rollback) passed pre-fix for the wrong reason (threw on type mismatch before reaching `FormIdHelper.Parse`). Post-fix: same 6 cells now PASS; test 30 still PASS but with a different captured error message — `"Element [0] of JSON Array could not be converted to Effect: Additional non-parsable characters are at the end of the string."` — confirming the bridge now reaches `FormIdHelper.Parse` and surfaces the genuine parse error.

## Bugs surfaced (Phase 2/3 only)

N/A — Phase 1 is implementation, not verification matrix.

## Deviations from plan

**One bonus-catch absorbed in scope, per the Phase 1 plan's "Bonus-catch precedent" rule:**

- **JSON String → single-field FormLink in `ConvertJsonValue`.** Pre-existed v2.7.1 (the bridge's `ConvertJsonElementToListItem` had the FormLink branch for list elements but `ConvertJsonValue` did not; v2.7.1 never exercised single-field FormLink `set_fields`). Phase 1's per-Effect property recursion exposes the gap deterministically. Audit predicted, smoke confirmed, fix landed (~15 lines: one branch + one helper). Documented in CHANGELOG bullet 2 of v2.8.0's `### Added — bridge` and in EFFECTS_AUDIT.md § Open questions item 3. Side benefit (every record's single-field FormLink properties writable via `set_fields`) is NOT advertised in the schema description.

**No other deviations.** Branch A + Branch B + BuildCondition refactor all worked first-try in the shape the audit predicted. Five record types in scope, no exclusions.

## Known issues / open questions

- **Sub-LoquiObject merge on every record type is now a side effect of Branch B.** `set_fields: {Configuration: {Health: 200}}` on NPC, `set_fields: {BasicStats: {Damage: 50}}` on WEAP, etc. now work as in-place merge into the sub-LoquiObject. Documented in EFFECTS_AUDIT.md § Branch B side effect; NOT advertised in `tools_patching.py` schema. If a future consumer reports surprising behavior here, the schema can be promoted; for now the Effects-array form is the sole user-facing surface for the new mechanism.
- **Single-field FormLink writes via `set_fields` are now possible on every record type** as a side effect of the bonus-catch fix. Same posture: not advertised in schema; if it becomes a documented surface in v2.9, edit the description there.
- **`IFormLinkNullable<T>` is the same generic shape used by many single-field FormLink properties across Mutagen 0.53.1.** Phase 1's fix covers all five generic shapes; no further FormLink-shape gaps anticipated. If Phase 2's broader matrix surfaces a new shape, that's a Phase 4 candidate, not a Phase 1 regression.

## Preconditions for next session (Phase 2)

- ✅ `origin/main` at the work + hash-record commit pair Phase 1 just landed (see commit messages for hashes).
- ✅ MATRIX.md scope (Layer 1.E now has bridge support) — Phase 2 extends `coverage-smoke/Program.cs` to cover the rest of Layers 1, 2, and 4.
- ✅ Bridge builds clean from `tools/mutagen-bridge/`.
- ✅ Effects-list smoke regression tests already laid down (tests 23–30); Phase 2 inherits them as part of the broader matrix run.
- ✅ EFFECTS_AUDIT.md captures the Branch B decision, the bonus-catch fix, and the bridge SHA — Phase 2 reads these for context.
- ⏸️ Live install still at v2.7.1; Phase 2 doesn't touch it (Phase 3 reads via `mo2_create_patch` against the live bridge after a sync; Phase 5 ships).
- ⏸️ PerkAdapter/QuestAdapter `attach_scripts` carry-over remains a candidate for Phase 4 if Phase 2's race-probe extension confirms the failure mode.

## Files of interest for Phase 2

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.8.0_verification/PLAN.md` § Phase 2 | Authoritative steps for Phase 2 (extend coverage-smoke to MATRIX scope; run; capture bug list). |
| `Claude_MO2/dev/plans/v2.8.0_verification/MATRIX.md` | Per-cell test specification for Layers 1, 2, 4 (Layer 3 is Phase 3). |
| `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` | Effects API contract + Branch B + bonus-catch + bridge SHA. Phase 2 reads § Smoke verification to know which Layer 1.E cells already pass. |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | 30 tests today (22 v2.7.1 + 8 Layer 1.E from Phase 1); Phase 2 extends to ~100+ tests covering full MATRIX. |
| `Claude_MO2/tools/race-probe/Program.cs` | Phase 2 extends with the PerkAdapter/QuestAdapter readback probe (per PLAN.md § Phase 2 step 3). |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` | Read-only for Phase 2 unless verification surfaces a bug requiring an in-scope Phase 1 fix retroactively. Branch A/B + bonus-catch landed in lines around the existing `ConvertJsonValue` / `ConvertJsonElementToListItem` / `SetPropertyByPath` / `BuildCondition` / `IsFormLinkType` / `IsNullableFormLinkType` symbols. |
