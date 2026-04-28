# Phase 1 Handoff — APerkEffect inventory probe + audit

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-28
**Session length:** ~3h
**Commits made:** `<work-hash>` (work) + this hash-record commit
**Live install synced:** No (Phase 1 is probe + audit doc only; live remains at v2.9.2 per CLAUDE.md exemption — Phase 1 doesn't touch the live install)

## Locks

All seven design questions from PHASE_0_HANDOFF.md § Conductor asks are now FINAL:

- **Q1 = A** — explicit `type:` field discriminator (Mutagen leaf class names).
- **Q2 = A** — ship all 12 concrete leaves (Aaron 2026-04-28).
- **Q3 = A** — replace semantics on Effects array.
- **Q4 = A** — v2.9.0 dispatcher composition untouched (Phase 1.5 PEPM round-trip confirmed structurally).
- **Q5 = A** — full Mutagen leaf class names (`PerkEntryPointModifyValue`, `PerkAbilityEffect`, `PerkQuestEffect`, etc.).
- **Q6 = A** — defer all three QUST sub-records.
- **Q7 = A** — wrapper-object DSL (with field-name + location corrections per audit).

**Q1 / Q5 / Q7 transcription corrections** auto-accepted by conductor (audit-as-source-of-truth) on 2026-04-28 after Halt 2 surfaced the architectural surprises. Mechanism design intact; only Mutagen-schema-actual naming + field locations changed. **Q2 escalated to Aaron and locked = A** (ship full 12).

## Conductor decisions inherited

Carry forward from Phase 0 + Phase 1 sign-offs:

1. **Live install exempt for Phase 1** — probe + audit doc only.
2. **Frequency probe scope = vanilla Skyrim.esm + 4 DLC ESMs** (conductor sign-off pre-Halt 1; lieu of Authoria load-order subset; informal Authoria signal exists per PLAN § E).
3. **Phase 1.5 PEPM round-trip supplemental** — conductor sign-off post-Halt 2 to extend race-probe with `PerkEntryPointModifyValue` round-trip after the original PEPE anchor halt-triggered on the missing class name.
4. **Q1 / Q5 / Q7 corrections classified as audit-as-source-of-truth transcription** — conductor auto-accepted (mechanism intact; surface naming changes per Mutagen 0.53.1 actual schema). Not Aaron-relayed.
5. **Q2 escalated to Aaron** — locked = A (ship full 12 concrete leaves; uniform factory; 4 zero-vanilla-instance leaves use synthetic round-trip cells per Phase 2 fixture pattern).

## What was done

- **`tools/race-probe/Program.cs`** — extended with v2.9.3 P1 inventory section (~753 lines added, lines 5132+) + Phase 1.5 PEPM round-trip supplemental (~280 lines added, lines 5897+). Section structure mirrors v2.9.0 P1's ConditionData inventory shape:
  - Inventory totals (concrete leaf count + abstract intermediate scan).
  - Per-subclass property dump with `[base]` / `[subclass-specific]` annotation + ShapeTag (`[FormLink]` / `[Enum]` / `[List]` / `[Sub-Loqui]` / `[Primitive]` / `[Other]`).
  - Activator constructibility table (abstract base expected fail; concretes expected pass).
  - PerkEntryPointEffect anchor (halted on missing class — Phase 1.5 supplemental targeting `PerkEntryPointModifyValue` filled the round-trip evidence gap).
  - PerkAbility / PerkQuestEffect anchor sections (PerkAbility informational-skip; PerkQuestEffect round-trip PASS).
  - Frequency probe (vanilla Skyrim.esm + 4 DLC ESMs).
  - Phase 1.5 PEPM section: EntryPoint enum dump (91 members) + Modification enum dump (3 members) + synthetic round-trip with full assertion chain.
  - `v293p1Failures` + `v293p15Failures` counters wired into `totalFailures` summation.
- **Two ARCH SURPRISES surfaced + halt-triggered correctly:** third-level polymorphism via `Mutagen.Bethesda.Skyrim.APerkEntryPointEffect` abstract intermediate; `PerkEntryPointEffect` doesn't exist as a class name (PLAN.md naming was constructed from read-side render flattening). Q1/Q5/Q7 transcription corrections resolved per § Locks.
- **Probe build clean (0 warnings, 0 errors)** across both build cycles. Two minor compile fixes folded inline (variable `args` shadow rename to `genericArgs`; named-tuple type annotation on `Dictionary` lookups in frequency probe; non-null `!` assertions on already-guarded property references in Phase 1.5 round-trip).
- **Probe runs captured to TWO scratch files:**
  - `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` (160 KB, 2420 lines) — original P1 run; 2 v293p1Failures (the architectural-rename ARCH SURPRISES).
  - `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt` (170 KB, 2540 lines) — P1.5 supplemental re-run; 0 v293p15Failures; PEPM round-trip ALL PASS.
- **`<plan>/APERK_EFFECTS_AUDIT.md`** — NEW (356 lines). Mirrors v2.8.0 EFFECTS_AUDIT + v2.9.0 CONDITIONS_AUDIT structure. Contents: probe references, inventory totals (12 concrete leaves + 1 abstract intermediate), architectural surprises (Q1/Q5/Q7 transcription corrections documented as audit-as-source-of-truth resolution), constructibility table, per-subclass property surface (12 leaves with full property tables), PerkEntryPointModifyValue anchor (Phase 1.5 round-trip evidence), PerkAbilityEffect anchor (informational-skip note), PerkQuestEffect anchor (round-trip PASS evidence), real-world frequency table (vanilla + 4 DLC, 523 PERK records, 847 effect instances), § Phase 2 implications (8 schema-divergence items Phase 2 must handle).
- **`<plan>/MATRIX.md`** — full rename pass + Layer 1.P expansion. Pre-rename: 36 `PerkEntryPointEffect`, 18 `PerkAbility`, 16 `PerkConditions` stale identifiers (70 total). Post-rename: 0 `PerkAbility\b` matches; 7 remaining `PerkEntryPointEffect` matches are all valid (substring matches in `APerkEntryPointEffect` abstract intermediate name + leaf class names like `PerkEntryPointModifyValue`); 1 remaining `PerkConditions` is the audit-as-source-of-truth correction note about the field-name rename itself. Layer 1.P expanded from 5 baseline rows to 12 rows (frequency-ordered: PEPM 3 sub-shapes + SelectSpell + ModifyActorValue + PerkAbilityEffect + PerkQuestEffect + AddActivateChoice + SetText + SelectText + 4 zero-vanilla synthetic-only). Layer 2.03 cell description shows explicit 2-level Conditions nesting in DSL. Phase fill-in checklist marked Phase 1 hand-back as COMPLETE.
- **`<plan>/PHASE_1_HANDOFF.md`** — NEW (this file).

No production code touched. No version bump. No bridge build.

## Verification performed

Phase 1 verification = race-probe runs against vanilla Skyrim.esm + 4 DLC ESMs, plus synthetic in-memory round-trips for two anchors.

| Check | Status | Evidence |
|---|---|---|
| `git log -1 --oneline origin/main` matches Phase 0 hash | ✅ `7a33c01 [v2.9.3 P0] Handoff: record commit hash 25cef3a` | session-start verification |
| `git status` clean before Phase 1 work | ✅ working tree clean | session-start verification |
| race-probe builds: 0 warnings, 0 errors | ✅ both cycles | `dotnet build -c Release` |
| race-probe runs to completion | ✅ 2420 lines (P1) + 2540 lines (P1.5) | scratch files captured |
| APerkEffect base IsAbstract = True | ✅ | scratch P1 line 2171 |
| 12 concrete `APerkEffect` leaves enumerated | ✅ alphabetical list | scratch P1 lines 2173–2184 |
| 1 abstract intermediate `APerkEntryPointEffect` documented | ✅ ARCH SURPRISE log | scratch P1 lines 2185–2186 |
| Activator constructibility table populated | ✅ 1 abstract base FAIL + 12 concrete OK | scratch P1 lines 2338–2353 |
| Per-subclass property surface dumped | ✅ 12 sections | scratch P1 lines 2188–2336 |
| Outer `Conditions` element type = concrete `PerkCondition` | ✅ confirmed | per-subclass dump every leaf shows `[List] Noggog.ExtendedList<Mutagen.Bethesda.Skyrim.PerkCondition>` |
| EntryPoint enum: 91 members dumped | ✅ full member list with indices | scratch P1.5 lines 2422–2515 |
| Modification enum: 3 members dumped (Set/Add/Multiply) | ✅ | scratch P1.5 lines 2517–2518 |
| PerkEntryPointModifyValue synthetic round-trip | ✅ ALL PASS (11 inner assertions) | scratch P1.5 lines 2520–2536 |
| PerkQuestEffect synthetic round-trip | ✅ PASS (Quest + Stage Byte round-trip) | scratch P1 lines 2362–2367 |
| Real-world frequency table populated | ✅ 8 leaves with vanilla data + 4 zero-instance | scratch P1 lines 2369–2416 |
| `v293p1Failures = 2` (both expected ARCH SURPRISES) | ✅ | scratch P1 line 2418 |
| `v293p15Failures = 0` | ✅ | scratch P1.5 line 2538 |
| Probe FAILED summary line names both counter buckets | ✅ | scratch P1.5 line 2540 |

## Bugs surfaced

N/A. Phase 1 is read-only on the bridge; no bugs surface from probe-only work. Architectural surprises are not bugs — they are PLAN-vs-actual schema discrepancies, captured as audit-as-source-of-truth transcription corrections per § Locks.

## Deviations from plan

Three deviations, all conductor-signed-off:

1. **Frequency probe scope = vanilla Skyrim.esm + 4 DLC ESMs in lieu of Authoria load-order subset.** PLAN.md § Phase 1 step 6 named "vanilla Skyrim.esm + a representative subset of the Authoria load order." Vanilla-only was too narrow; full Authoria sampling needed modlist plugin-path enumeration overkill for an informational probe. Conductor sign-off pre-Halt 1: DLC ESMs give canonical Bethesda pattern across all 5 primary masters with zero modlist-navigation complexity. Werewolf/Vampire perks (PerkAbilityEffect-bearing) live in Dawnguard.esm; Standing Stones / Black Book perks in Dragonborn.esm. Authoria's PERK frequency was already known informally per PLAN.md § E ("9 representative PERK records … 100% PerkEntryPointEffect"); rescanning ~1900 records just to confirm a known signal was low-value. **No Q-lock impact** (Q2 = A regardless).

2. **Original PEPE anchor didn't run; Phase 1.5 PEPM supplemental re-targeted.** PLAN.md § Phase 1 step 4 directed a `PerkEntryPointEffect` anchor with EntryPoint enum dump + round-trip. Mutagen 0.53.1 doesn't have a class with that exact name (decomposed into 10 per-EntryPointType concrete leaves under abstract intermediate `APerkEntryPointEffect`); my probe correctly halt-triggered. Conductor sign-off post-Halt 2: extend race-probe with a re-targeted round-trip on `PerkEntryPointModifyValue` (60.3% dominant leaf, AugmentedShock60 family) to capture the EntryPoint + Modification + synthetic-round-trip evidence that the original PEPE anchor couldn't produce. The Phase 1.5 supplemental is a separate code section + scratch file (originals preserved); 0 failures.

3. **Q1 / Q5 / Q7 audit-as-source-of-truth transcription corrections.** PHASE_0_HANDOFF.md § Conductor asks listed Q1 / Q5 / Q7 as design questions awaiting Aaron's lock. Phase 1's audit surfaced that the design directions hold (explicit `type:` discriminator + Mutagen leaf names + wrapper-object DSL) but the specific naming + field locations in PLAN.md were wrong vs the actual Mutagen 0.53.1 schema. Conductor classified these as audit-as-source-of-truth transcription (not design re-litigation) and auto-accepted the corrections without Aaron relay. PLAN/MATRIX/CHANGELOG draft text needs the global rename pass (MATRIX done in this commit; PLAN textual examples will be amended by Phase 2 plan-amend per v2.7.1 / v2.8.0 / v2.9.0 precedent).

## Known issues / open questions

Phase 2 needs to handle 8 schema-divergence items captured in APERK_EFFECTS_AUDIT.md § Phase 2 implications (informational, not Phase-1-blocking):

1. `Nullable<Single>` Value slot on `PerkEntryPointModifyValue` / `PerkEntryPointModifyValues` — bridge `ConvertJsonValue` must handle null + nullable primitive targets. P1.5 evidence: assigning `(float?)1.5f` boxed as `Nullable<Single>` round-tripped cleanly via reflection setter.
2. `Byte` Stage slot on `PerkQuestEffect` — caller-supplied JSON int auto-converts via `Convert.ChangeType`. P1 evidence: `Stage = 100` round-tripped clean.
3. Plain `String` (`PerkEntryPointSelectText.Text`) vs `TranslatedString` Sub-Loqui (`PerkEntryPointSetText.Text`) — different write contracts per cousin class. Phase 2 picks plain-string-to-TranslatedString convenience path or sub-LoquiObject Branch B merge.
4. Two-level `Conditions` nesting in DSL — outer `APerkEffect.Conditions: ExtendedList<PerkCondition>` + inner `PerkCondition.Conditions: ExtendedList<Condition>`. Phase 2 schema description must call out explicitly.
5. `PerkEntryPointAddActivateChoice.Spell` is `IFormLinkNullable<ISpellGetter>` (vs `PerkEntryPointSelectSpell.Spell` non-nullable). Phase 2 schema docs differ on nullability.
6. `PerkQuestEffect.Unknown: Noggog.MemorySlice<System.Byte>` is opaque blob — NOT a write-target. Phase 2 schema description must not advertise it. Bridge `SetPropertyByPath` falling through `ConvertJsonValue` would throw on MemorySlice; explicit reject-with-clean-error is the safer Phase 2 choice (cell `1.D.<NN>` candidate).
7. `Modification` enum is per-leaf, not shared. PEPM/PEPMs's enum is `{Set, Add, Multiply}` (P1.5 confirmed); PEPMA's enum needs Phase 2 dump.
8. `Conditions` list element type is concrete `PerkCondition` (Q7 lock confirmed). Wrapper-object DSL holds. PerkCondition itself is Activator-constructible per P1.5.

No bugs. No questions blocking Phase 2.

## Conductor asks

NONE. Q2 locked = A by Aaron 2026-04-28. Q1/Q5/Q7 transcription auto-accepted by conductor. **Inventory shape acceptable; Phase 2 cleared to begin.**

## Preconditions for next phase

| Precondition | State |
|---|---|
| `tools/race-probe/Program.cs` has v2.9.3 P1 inventory + P1.5 PEPM round-trip section + builds clean | ✅ |
| `<plan>/APERK_EFFECTS_AUDIT.md` exists with full v2.8.0/v2.9.0-style structure | ✅ |
| `<plan>/MATRIX.md` rename pass complete (12-row Layer 1.P + 2-level Conditions nesting + Phase 1 hand-back checklist marked COMPLETE) | ✅ |
| Two scratch capture files preserved | ✅ `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` + `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt` |
| All 7 design questions (Q1–Q7) finalized | ✅ |
| Bridge build state unchanged from v2.9.2 P5 ship | ✅ (Phase 1 didn't touch the bridge) |
| Live install at v2.9.2 baseline | ✅ (Phase 1 didn't sync) |
| Phase 0's PLAN.md + CONDUCTOR_KICKOFF.md + PHASE_0_HANDOFF.md tracked in git | ✅ |
| Phase 1 work committed (work + hash-record double-commit) | pending Halt 3 sign-off → Task 5 |

## Files of interest for next phase

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/PLAN.md` § Phase 2 | Authoritative steps + § Conductor decisions for Phase 2 |
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/PHASE_1_HANDOFF.md` (this file) | Phase 1 deliverables + Q1–Q7 final locks + 8 schema-divergence items Phase 2 must handle |
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/APERK_EFFECTS_AUDIT.md` | Per-leaf property surface (Phase 2 transcribes — does NOT speculate); architectural surprises section + Phase 2 implications |
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/MATRIX.md` § Phase fill-in checklist (Phase 2 hand-back) | Exact rows Phase 2 lands post-implementation |
| `Claude_MO2/tools/race-probe/Program.cs` lines 5132–5896 (v2.9.3 P1 inventory) + 5897+ (P1.5 PEPM) | Inventory + round-trip evidence source; Phase 2 reads to understand the per-leaf shape contract before bridge implementation |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` `ConvertJsonElementToListItem` (line 1441) + `BuildConditionFromJson` (~line 2331) | Phase 2 extension target — Branch A `typeof(APerkEffect)` special case + new `BuildPerkEffectFromJson` factory |
| `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` | Reference: Branch A factory pattern + Activator constructibility shape |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` | Reference: inventory probe pattern + per-shape categorization + architectural-surprise documentation style |
| `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` | Authoritative inventory + per-subclass dump + frequency table source |
| `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt` | Authoritative PerkEntryPointModifyValue round-trip evidence + EntryPoint enum 91-member dump |
