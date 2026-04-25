# Phase 3 Handoff — Layer 3 workflow scenarios — 0 new bridge bugs surfaced

**Phase:** 3
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~3h
**Commits made:** TBD — pending Aaron's sign-off (Phase 3 double-commit cadence: work + hash-record).
**Live install synced:** Yes — interim Phase 3 sync (bridge + `config.py` + `tools_patching.py`) per PLAN.md § Phase 3 Step 2. Path: `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`.

## What was done

### Preflight (live install sync at v2.7.1 → v2.8.0)

- Built **interim publish** of Phase 1's bridge: `dotnet publish -c Release -r win-x64 --self-contained false -o ../../build-output/mutagen-bridge/` from `tools/mutagen-bridge/`. Phase 1 had landed a `dotnet build` at `tools/mutagen-bridge/bin/Release/net8.0/` (SHA `f998c4e0…c8bb04`); `build-output/` still held v2.7.1's publish (SHA `a0f1d983…1253e68`). The Phase 3 publish refreshes `build-output/` with Phase 1's bridge — new SHA `fb723cd339d3ad18076a065b28a060ceb7f932dbb0c386357b9832e1348926fa`. Different from Phase 1's build SHA as expected (publish embeds runtime config + different timestamps).
- Synced the new bridge to live: `cp -rf build-output/mutagen-bridge/. <live>/tools/mutagen-bridge/`.
- Found **layout discrepancy from PLAN.md sync command**: live Python files live flat under `<live>/` (e.g. `<live>/tools_patching.py`), not nested in `<live>/mo2_mcp/`. Synced at the corrected paths: `cp Claude_MO2/mo2_mcp/config.py <live>/config.py` and `cp Claude_MO2/mo2_mcp/tools_patching.py <live>/tools_patching.py`. Removed `<live>/__pycache__/`.
- Aaron restarted MO2's Claude server; `mo2_ping` confirmed `version: "2.8.0"`.
- Effects-list smoke against Skyrim.esm:07E881 (REQ_Creature_AtronachFlame_Flames; output `v2.8-preflight.esp`): replace-semantics (2 source effects → 1 new), BaseEffect FormLink, Magnitude/Area/Duration all confirmed via `mo2_record_detail` readback. Patch deleted; F5'd.

### Layer 3 scenarios — 5/5 executed, all PASS

Each scenario wrote `v2.8-scenario-N.esp` to `<modlist>/mods/Claude Output/`, ran `mo2_record_detail` readback against the patch ESP, deleted the ESP, and the user F5'd. **Total assertions: 46/46 PASS. Bridge bugs surfaced: 0.**

## Verification performed

### Per-scenario assertion table

#### Scenario 3.1 — Reqtify-race + ability spells (consumer-driven; the workflow that surfaced Phase 1's gap)

**Records picked:** RACE `halfkhajiit.esp:03322B` (HalfKhajiitRace, won by Authoria - High Poly Head Patcher.esp); SPEL `Requiem.esp:AE3B1D` (REQ_Trait_Physique_Khajiit, won by Requiem - WAR Races Redone Patch.esp); SPEL `Requiem.esp:AE3B24` (REQ_Trait_Heritage_Khajiit, won by Requiem - Races Redone.esp). Per Aaron's constraint (Ohmes-style custom-race plugin); Half-Khajiit / Ohmes-Raht via halfkhajiit.esp is the same shape as the consumer's workflow.

**Assertions (13/13 PASS):** RACE response `keywords_added=2 / spells_added=1 / fields_set=6`; RACE readback Keywords (2 new + 8 source preserved = 10), ActorEffect (1 new + 6 source = 7), Starting `[H:200, M:150, S:200]` (Tier B aliases), Regen `[H:0.5, M:1, S:2]` (Tier B aliases); SPEL-Physique `fields_set=1`, Effects.Count=1 (replace from 5), BaseEffect `Requiem.esp:0008C7`, Magnitude=75, nested Conditions `[GetLevel ≥ 25]`; SPEL-Heritage `fields_set=1`, Effects.Count=2 (replace from 6), both entries' BaseEffect/Magnitude correct; all 3 records in single output ESP.

**Phase 1 capabilities exercised:** SPEL.Effects array write (single-entry + multi-entry); replace-semantics (cleared 5 source effects on Physique, 6 on Heritage); per-effect nested Conditions (the carry-over absorbed in Phase 1); IFormLinkNullable BaseEffect bonus-catch.

#### Scenario 3.2 — Creature mod kw + script attachment

**Records picked:** NPC_ `mihailgiantfamily.esp:000807` (mihailgiantesses_NPC1) and NPC_ `mihailgiantfamily.esp:00081E` (mihailgiantesses_NPC2). Source winner: `Authoria - Reqtificator Lite Output.esp`. Both lacked pre-existing VMAD — clean targets.

**Assertions (9/9 PASS):** Per record `keywords_added=2 / scripts_attached=1`; VMAD has `TestPhase3CreatureScript` with 3 typed properties — Object FormLink (`Skyrim.esm:013CA9` MGEF, resolves correctly), Int=42, Float=3.14; new keywords additive to source; both records in single output ESP.

#### Scenario 3.3 — Leveled spell list merge

**Record picked:** LVSP `Skyrim.esm:105101` (LSpellWEBattlemageFireLeftHand). Chain: Skyrim.esm → unofficial skyrim special edition patch.esp → Requiem.esp. `base_plugin: Requiem.esp`, `override_plugins: ["unofficial skyrim special edition patch.esp"]`.

**Assertions (7/7 PASS):** Bridge accepts merge_leveled_list (`success=true`); output ESP contains LVSP override; `entries_merged=0` (correct dedup outcome — see finding #4 below); readback Entries.Count=3 matching base, Levels=`[1,1,1]` (Requiem's rebalance, not Skyrim.esm's `[1,5,20]` or USSEP's reordered version), References match base's 3 SPELs by FormID; `ChanceNone` preserved (empty dict); masters list clean (just Skyrim.esm).

#### Scenario 3.4 — NPC bundle (7 operators on one record)

**Record picked:** Hadvar (`Skyrim.esm:02BF9F`, override_count=6, winner `Requiem for the Indifferent.esp`). Vanilla named NPC, no existing VMAD.

**Assertions (9/9 PASS):** Response has all 7 expected mods keys (`fields_set=1, keywords_added=2, spells_added=1, perks_added=1, factions_added=1, inventory_added=1, scripts_attached=1`); `Configuration.HealthOffset=300` (Tier B Health alias); Factions: new CreatureFaction Rank=1 + 6 source preserved (7 total); ActorEffect: new SPEL + 6 source = 7; Perks: new Sneak perk + ~70 source preserved; Items: Lockpick count=5 + 4 source = 5; VMAD `TestPhase3HadvarScript` with 3 typed properties (Int=99, Float=2.5); 2 keywords added; all other source fields preserved (Configuration, Race, Class, HeadParts, FaceMorph, etc.).

**Coverage breadth:** Every NPC operator dispatch arm in the bridge exercised in a single call. No cross-operator interference, no rollback, no Tier D firing.

#### Scenario 3.5 — MGEF condition + PERK reflection set_fields (final)

**Records picked:** MGEF `Skyrim.esm:10E4FA` (PerkElementalProtectionResistFire, override_count=1, source Conditions=[]); PERK `Skyrim.esm:0BE126` (REQ_Sneak_Mastery_000_Stealth1, won by Requiem - Stealth Redone.esp).

**Pre-confirmation finding:** MATRIX § Scenario 3.5 names `Configuration.PerkType` as the PERK reflection target. Mutagen 0.53.1's PERK schema does not have a `Configuration` sub-object. Writable scalars are top-level: `Level` / `NumRanks` / `Trait` / `Playable` / `Hidden` plus `NextPerk` (single-field FormLink). Adjusted plan accordingly — see finding #3 below.

**Assertions (8/8 PASS):** MGEF `conditions_added=1`; readback Conditions.Count=1, `ComparisonValue=50, CompareOperator=GreaterThanOrEqualTo, RunOnType=Subject`; PERK `fields_set=3`; readback `Level=25` (was 0), `NumRanks=3` (was 5), `NextPerk=Skyrim.esm:058214` (was 0C07C6); PERK source fields preserved (Effects, Name, Description, Trait, Playable, Hidden, Conditions); both records in single output ESP.

**Notable validation:** PERK.NextPerk write confirms Phase 1's IFormLinkNullable bonus-catch is genuinely generic — exercised on a different record type (PERK) and different FormLink target (`IFormLinkNullable<IPerkGetter>`) than Phase 1's original SPEL.Effects.BaseEffect (`IFormLinkNullable<IMagicEffectGetter>`).

### Cross-scenario rollup

| # | Scenario | Records | Assertions | Result |
|---|---|---:|---:|---|
| 3.1 | Reqtify-race + ability spells | 1 RACE + 2 SPEL | 13 | PASS |
| 3.2 | Creature kw + script attachment | 2 NPC_ | 9 | PASS |
| 3.3 | Leveled spell list merge | 1 LVSP | 7 | PASS |
| 3.4 | NPC bundle (7 operators) | 1 NPC_ | 9 | PASS |
| 3.5 | MGEF condition + PERK reflection set_fields | 1 MGEF + 1 PERK | 8 | PASS |
| **Total** | | **8 records** | **46** | **5/5 PASS** |

Bridge artifact under test: `fb723cd339d3ad18076a065b28a060ceb7f932dbb0c386357b9832e1348926fa` (live + `build-output/` matched).

### Cleanup confirmation

`E:/Skyrim Modding/Authoria - Requiem Reforged/mods/Claude Output/` confirmed clean of all `v2.8-*.esp` after each scenario; user F5'd between scenarios. No orphaned plugins remain.

## Bugs surfaced

**No new bridge bugs.** The Phase 4 priority-zero entry `perk_quest_adapter_subclass` (carried from Phase 2) remains the only outstanding bridge bug. Phase 3 did not re-exercise it directly:
- Scenario 3.4's `attach_scripts` on Hadvar uses NPC_ which dispatches to base `VirtualMachineAdapter` (not the subclass adapters that trigger the bug).
- Scenario 3.5's PERK touch was `set_fields` only (Level/NumRanks/NextPerk), not `attach_scripts`.

So Phase 3's bug list extension is empty. Phase 4 still has exactly 1 fix session queued (`perk_quest_adapter_subclass`), with the OTFT/SPEL VMAD disambiguation probe folded in as the session's first action per PLAN.md § Phase 4.

## Findings (documentation / matrix accuracy — not bridge bugs)

These are MATRIX.md / KNOWN_ISSUES.md / schema-description corrections to fold into Phase 5's pre-ship pass.

1. **Matrix FormID typo (cell 1.A.01).** `Skyrim.esm:08F95E` is labelled as `VendorItemFood` in MATRIX.md; actual record at that FormID is `AMBWindloopMountainsHills01LP` (an audio category record). Bridge accepts opaque FormLinks for `add_keywords` without record-type validation — this is consistent v2.7.1 behavior and the test still passes mechanically (Phase 2's 1.A.01 also passed despite this). Phase 5 should swap to a real KYWD FormID or relabel.
2. **MATRIX.md merge response field name (Scenario 3.3 / cell 4.lvl).** Matrix predicts `mods.merged_count`. Bridge response actually uses `details[].entries_merged` (top-level on the per-record detail, not nested under `mods`). This was the original Phase 1-era shape; matrix wasn't aligned. Phase 5 / matrix maintenance.
3. **MATRIX.md PERK property reference (Scenario 3.5).** Matrix names `Configuration.PerkType`. Mutagen's PERK schema has no `Configuration` sub-object. Writable PERK scalars are at top level (`Level`, `NumRanks`, `Trait`, `Playable`, `Hidden`) plus `NextPerk` as single-field FormLink. Phase 5 / matrix accuracy correction.
4. **LVSP modlist data shape (Scenario 3.3).** Authoria modlist's LVSP records with override_count=3 (`Skyrim.esm:105101`, `Skyrim.esm:1050FE`) all carry the same SPEL References across vanilla / USSEP / Requiem (Requiem just rebalances levels). Merge correctly dedups to `entries_merged: 0` for this topology — no entry-set diversity exists in the modlist for this LVSP shape. Matrix's "merged_count > 0" expectation can't be met against this modlist. Phase 5 candidate to relax matrix expectation or document the LVSP-data constraint.
5. **add_conditions GetActorValue parameterless default (v2.9 polish candidate).** When `function: "GetActorValue"` is supplied without an `actor_value` parameter, the bridge defaults to ActorValue index 0 (renders as `Aggression` in readback — a 4-state enum where `>= 50` is semantically meaningless). Same v2.7.1 behavior Phase 2 documented for matrix 1.r.33. Schema enhancement candidate — expose `actor_value` parameter on `add_conditions`. Out of scope for v2.8.0; v2.9 candidate.

## Deviations from plan

1. **Sync paths corrected at the live layout.** PLAN.md § Phase 3 Step 4 specified `<live>/mo2_mcp/tools_patching.py` etc. Actual live layout is flat: `<live>/tools_patching.py`. Updated sync commands accordingly. No risk introduced — the corrected paths target what the live install actually reads.
2. **Bridge SHA discrepancy resolved by interim publish.** PLAN.md § Phase 3 Step 2 said "sync from `build-output/mutagen-bridge/` first if preflight fails" — the bridge at `build-output/` was actually still v2.7.1's publish (`a0f1d983…1253e68`) since Phase 1 only `dotnet build`'d, not `dotnet publish`'d. Phase 3 added a publish step before the sync, producing `fb723cd3…48926fa`. This is the new interim "tested = running live" anchor; Phase 5's ship publish will produce another SHA.
3. **PERK property pre-confirmation produced a different reflection target than matrix specified.** MATRIX § Scenario 3.5 named `Configuration.PerkType`; actual writable PERK scalars are top-level (no Configuration sub-object). Substituted `Level=25, NumRanks=3, NextPerk=Skyrim.esm:058214` — three reflection writes, including the IFormLinkNullable bonus-catch path. Captured as finding #3.
4. **No bonus-catch taken.** No latent bridge issue surfaced during workflow scenarios; PatchEngine.cs untouched as scoped.

## Known issues / open questions

1. **`perk_quest_adapter_subclass`** (carried from Phase 2 — only Phase 4 fix session queued). PLAN.md § Phase 4 specifies the OTFT/SPEL VMAD disambiguation probe as the session's first action.
2. **Phase 5 matrix-accuracy work** (findings #1–4 above) — handled in Phase 5's pre-ship cleanup.
3. **v2.9 polish candidates** (finding #5: GetActorValue parameterless default; plus v2.7.1 carryovers in `KNOWN_ISSUES.md`) — out of scope for v2.8.0.

## Preconditions for next session (Phase 4 — `perk_quest_adapter_subclass`)

- ✅ `origin/main` ready to advance to Phase 3's commits (TBD — pending sign-off).
- ✅ Bridge builds clean (Phase 3 publish landed at `fb723cd3…48926fa`).
- ✅ Live install at v2.8.0 (`mo2_ping` returns `version: "2.8.0"`).
- ✅ `coverage-smoke` runs to completion (deterministic from Phase 2; not re-run in Phase 3 since Phase 3 didn't touch PatchEngine.cs).
- ✅ Race-probe Batch 7 PerkAdapter/QuestAdapter probe deterministically reproduces from Phase 2 (will become Phase 4's pre-fix evidence; same probe becomes the post-fix regression test once the subclass-aware Activator-create change lands).
- ⏸️ Phase 4 has 1 bug to fix (`perk_quest_adapter_subclass`). PLAN.md § Phase 4 documents the proposed fix angle (Activator.CreateInstance(vmadProp.PropertyType) at PatchEngine.cs:1739) and the OTFT/SPEL VMAD disambiguation probe as the session's first action.
- ⏸️ Phase 5 inherits matrix-accuracy findings #1–4 above plus v2.9 polish candidate #5.

## Files of interest for next session (Phase 4)

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.8.0_verification/PLAN.md` § Phase 4 | Authoritative steps for the per-bug fix session, including the OTFT/SPEL VMAD disambiguation probe specification. |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_2_HANDOFF.md` § Bug 1 | The `perk_quest_adapter_subclass` repro + failure mode + proposed fix angle (Activator-based subclass construction). |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_3_HANDOFF.md` (this file) | Findings list — Phase 4's `perk_quest_adapter_subclass` session is the right place to fold the OTFT/SPEL docs cleanup per Phase 2's amended placement. |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs:1727+` | `ApplyAttachScripts`. Lines 1739 (`vmad = new VirtualMachineAdapter()`) is the fix target. |
| `Claude_MO2/tools/race-probe/Program.cs` Batch 7 | Existing PerkAdapter/QuestAdapter probe — reuse as Phase 4 pre-fix repro + post-fix regression. Extended VMAD disambiguation probe goes here too. |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | Phase 4 adds regression tests for PERK + QUST attach_scripts after the fix lands. |

## Acceptance — Phase 3

- ✅ All 5 Layer 3 scenarios executed; all 46 sub-assertions PASS.
- ✅ Per-scenario assertion tables documented above.
- ✅ Test patches deleted; `<modlist>/mods/Claude Output/` clean post-phase.
- ✅ Bug list captured (0 new bridge bugs surfaced; carry-forward 1 from Phase 2).
- ✅ Findings list captured (5 documentation/matrix-accuracy items for Phase 5 / v2.9).
- ✅ Pre-flight evidence captured (interim publish SHA, ping confirmation, Effects-list smoke + readback).
- ✅ Record-pick rationale documented per scenario.
- ✅ Cleanup confirmation.
- ⏸️ Two commits pending Aaron's sign-off (per v2.7.1 double-commit cadence): work commit `[v2.8 P3] Layer 3 workflow scenarios — 0 new bugs surfaced` + handoff hash-record commit `[v2.8 P3] Handoff: record commit hash <work-hash>`.
