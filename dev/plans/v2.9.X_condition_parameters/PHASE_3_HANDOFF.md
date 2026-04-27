# Phase 3 Handoff — Layer 3 workflow scenarios — 1 pre-existing bridge bug surfaced (INFO override gap)

**Phase:** 3
**Status:** Partial — sync + canary + Scenario 3.2 landed clean (17/17 sub-assertions PASS, dispatcher verified live across 4 concrete IFormLinkOrIndex<T> generic-T types). **Scenario 3.1 BLOCKED** at the override-creation step by a pre-existing bridge gap (not v2.9-dispatcher-related). Bug captured for Phase 4 fix; Phase 5 re-runs Scenario 3.1 against the post-fix bridge per the v2.7.1 P3 → P4 → P5 and v2.8.0 P3 → P4 → P5 cadence.
**Date:** 2026-04-27
**Session length:** ~2.5h
**Commits made:** work commit + hash-record commit (this batch)
**Live install synced:** Yes — v2.8.0 → v2.9.0 at session start. Path: `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`. Bridge SHA: `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e` (matches P2D byte-for-byte).

## What was done

- **Live install sync v2.8.0 → v2.9.0** — single-shot Bash batch, MCP server pre-stopped per Aaron's Option-A operational ordering: 4 bridge artifacts (`mutagen-bridge.{exe,dll,deps.json,runtimeconfig.json}`) copied from `<repo>/tools/mutagen-bridge/bin/Release/net8.0/` → `<live>/tools/mutagen-bridge/`; 2 v2.9-touched Python files (`config.py` v2.8.0→v2.9.0 version bump + `tools_patching.py` v2.9 schema with `parameters` field) copied to FLAT `<live>/` paths per v2.8.0 P3's documented layout; `<live>/__pycache__/` wiped (16 .pyc files); Aaron full-restarted MO2.
- **Pre-flight canary** — `v2.9-preflight-canary.esp` against MGEF `Skyrim.esm:0173DC` (REQ_Effect_ConjurationGM_BanishDaedra_Damage; the v2.9 P2D in-process Test 371 canary's same record). One `add_conditions` entry: `function: "GetIsID"`, `parameters: {Object: "Skyrim.esm:02BF9F"}` (Hadvar). Patch + readback PASS — `Object` slot resolved to `Skyrim.esm:02BF9F (Hadvar)`, NOT FormID 0. Source's existing `GetHasKeyword(ActorTypeDaedra)` condition preserved (additive). Patch deleted + Aaron F5'd before scenarios.
- **Scenario 3.2 — Perk HasPerk/HasSpell prerequisite gate** — `v2.9-scenario-2.esp` against PERK `Skyrim.esm:0CB413` (REQ_Smithing_DaedricSmithing, winner Requiem - Reading teaches smithing perks.esp). Two `add_conditions` entries: `HasPerk + parameters: {Perk: "Skyrim.esm:05218E"}` (REQ_Smithing_AdvancedBlacksmithing) AND `HasSpell + parameters: {Spell: "Skyrim.esm:08CB03"}` (REQ_Restoration1_Healing_ConcSelf_RightHand). Carrier had 3 pre-existing top-level Conditions across 3 different dispatcher shapes (FLI/GetItemCount + legacy Global/GetGlobalValue + Enum/GetActorValue=Smithing) — proves dispatcher coexists with pre-existing multi-shape diversity. Patch + readback PASS — Perk + Spell slots both resolved (NOT FormID 0); 3 source conditions preserved with original slot values + OR/AND flag bitmap; Effects[].PerkConditions preserved (PerkConditionTabCount=2). Output ESP `masters: ["Skyrim.esm", "Requiem.esp"]` — Phase-3-unique correctness check (3.2.L) verified: bridge wrote FormKey by-value, did NOT add the override-winner `Requiem - Reading teaches smithing perks.esp` as an extraneous master. Patch deleted + Aaron F5'd post-scenario.
- **Scenario 3.1 — Dialog GetIsID topic gating — BLOCKED.** Carrier `Skyrim.esm:000E3D` (MQ101 Helgen-escape Stormcloak/Imperial branching dialog INFO with 5 existing DialogConditions including 2 native GetIsID + 2 GetInFaction + 1 GetStage — Aaron-approved swap from MATRIX placeholder). Patch attempt failed at the override-creation step BEFORE any v2.9 dispatcher code ran: `success=false, failed_count=1`, error `"Could not create override for DialogResponsesBinaryOverlay"`. Clean rollback (no orphan ESP). Halt-and-report per kickoff trigger "Any bridge-side error that suggests a code bug (Phase 4 territory; do not attempt fix in Phase 3)." Bug captured for Phase 4 (see § Bugs surfaced); Phase 5 re-runs Scenario 3.1 post-fix.

## Verification performed

### Live install state post-sync

| Check | Expected | Actual | Status |
|---|---|---|---|
| `mo2_ping` post-restart | `version: "2.9.0"` | `2.9.0` | ✓ |
| Live bridge SHA | P2D `2e3a1094…f8293975e` byte-for-byte | exact match | ✓ |
| Live `PLUGIN_VERSION` | `(2, 9, 0)` | `(2, 9, 0)` | ✓ |
| `<live>/__pycache__/` | wiped | gone | ✓ |
| Record index post-restart | rebuilds lazily | cold-built in ~14.8s on first query | ✓ |

### Pre-flight canary trace

```
Patch:    add_conditions on MGEF Skyrim.esm:0173DC (REQ_Effect_ConjurationGM_BanishDaedra_Damage)
          One entry: GetIsID + parameters: {Object: "Skyrim.esm:02BF9F"} (Hadvar)
Response: success=true, conditions_added=1, masters=["Skyrim.esm"]
Readback: condition[1].Data.Object.Link = "Skyrim.esm:02BF9F (Hadvar)" — slot resolved (NOT FormID 0)
          condition[1].Data slot name = "Object" → dispatched to GetIsIDConditionData
          condition[0] (source GetHasKeyword/Keyword=ActorTypeDaedra) preserved
PASS — live dispatcher wired end-to-end through stdin pipe → RouteParameterSlot → IFormLinkOrIndex<IReferenceableObjectGetter> branch → Mutagen FormKey ctor.
```

### Scenario 3.2 — assertion table (12/12 PASS)

| # | Assertion | Status |
|---|---|---|
| 3.2.A | Patch `success: true` | ✓ |
| 3.2.B | `details[0].modifications.conditions_added: 2` | ✓ |
| 3.2.C | Readback `Conditions.Count: 5` (3 source + 2 new) | ✓ |
| 3.2.D | Cond #4 `Data.Perk.Link` = `Skyrim.esm:05218E (REQ_Smithing_AdvancedBlacksmithing)` (NOT FormID 0) | ✓ |
| 3.2.E | Cond #5 `Data.Spell.Link` = `Skyrim.esm:08CB03 (REQ_Restoration1_Healing_ConcSelf_RightHand)` (NOT FormID 0) | ✓ |
| 3.2.F | Cond #4 discriminating slot = `Perk` (HasPerkConditionData) | ✓ |
| 3.2.G | Cond #5 discriminating slot = `Spell` (HasSpellConditionData — different concrete generic-T from #4) | ✓ |
| 3.2.H | Cond #4 + #5 ConditionFloat literal=1, CompareOperator=EqualTo | ✓ |
| 3.2.I | Source conds 1-3 preserved with original slot values + flag bitmap | ✓ |
| 3.2.J | Effects[].PerkConditions preserved (PerkConditionTabCount=2 + nested Conditions) | ✓ |
| 3.2.K | Other source fields (Name/Description/Trait/Level/NumRanks/Playable/Hidden/EditorID) preserved | ✓ |
| 3.2.L | Output ESP `masters: ["Skyrim.esm", "Requiem.esp"]` (NOT `Requiem - Reading teaches smithing perks.esp` despite source winner) | ✓ |

### Cross-scenario rollup

| Cell | Carrier | Op | Functions | Slots resolved | Sub-assertions |
|---|---|---|---|---|---|
| Pre-flight canary | MGEF `Skyrim.esm:0173DC` | `add_conditions` | GetIsID | `Object: Hadvar` | 5/5 PASS |
| Scenario 3.1 | INFO `Skyrim.esm:000E3D` (MQ101 dialog) | `add_conditions` | GetIsID (planned) | n/a — bridge-side blocker | **BLOCKED** before assertions evaluated |
| Scenario 3.2 | PERK `Skyrim.esm:0CB413` (Daedric Smithing) | `add_conditions` | HasPerk + HasSpell | `Perk: AdvancedBlacksmithing`, `Spell: HealingConcSelf` | 12/12 PASS |
| **Total** | 3 records, 3 record types | — | 3 functions, 3 different concrete generic-Ts (4 incl. source-preserved IItemOrListGetter via GetItemCount) | 3 slot resolutions verified | **17/17 sub-assertions PASS, 0 dispatcher bugs, 1 pre-existing bridge bug surfaced** |

Bridge artifact under test: `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e` (live + repo build matched at sync time; no rebuild in Phase 3).

### Cleanup confirmation

`<modlist>/mods/Claude Output/` confirmed clean of all `v2.9-*.esp` after each scenario; Aaron F5'd between canary→3.2 and post-3.2. No orphans remain.

## Bugs surfaced

### `info_override_missing_in_copyasoverride`

**NOT a v2.9 dispatcher bug.** Pre-existing gap in `PatchEngine.cs`'s `CopyAsOverride` switch (lines 2508–2571) — `IDialogResponsesGetter` (the Mutagen interface for INFO records) is missing from the dispatch. When the override target is an INFO, the switch falls through to `_ => null` (line 2570); caller checks for null at line 178 and throws at line 180 with the leaky internal type name `DialogResponsesBinaryOverlay`. v2.7.1 + v2.8.0 had this gap too; no prior phase touched INFO via the live bridge, so v2.9 P3 is the first surface.

| Field | Value |
|---|---|
| Slug | `info_override_missing_in_copyasoverride` |
| Record type | INFO (`IDialogResponsesGetter` per Mutagen 0.53.1) |
| Operator | any `op: "override"` (failure is at override-creation, before operator dispatch) |
| Repro | `mo2_create_patch` with `op: "override"` against any INFO FormID. Trivially reproduces in coverage-smoke or race-probe — no live modlist needed. |
| Failure mode | `success=false, failed_count=1`, error `"Could not create override for DialogResponsesBinaryOverlay"`. Clean rollback (no orphan ESP). Per-record error handling correct; only the underlying override-creation logic is missing the INFO branch. |
| Severity | Medium. INFO is a high-traffic record type for dialog patchers in real consumers (Authoria-style modlists with mod-driven dialog overrides). |
| Phase 4 fix angle | (1) Add `IDialogResponsesGetter r => patchMod.DialogResponses.GetOrAddAsOverride(r),` to `CopyAsOverride` (PatchEngine.cs:2549–2551 area, alongside `IDialogTopicGetter`). (2) Matching branch in `TryRemoveOverride` (per its doc comment "when CopyAsOverride learns a new record type, this switch must too"). (3) Race-probe regression: in-process Mutagen-direct write of an INFO override + `add_conditions`. (4) Coverage-smoke regression cell `1.P.GetIsID.INFO` mirroring Scenario 3.1's exact shape. (5) **Bonus-catch (~5 LOC, absorbed-not-optional per Aaron's promotion):** improve PatchEngine.cs:180 error message — replace `sourceRecord.GetType().Name` with a `RecordTypeCode(sourceRecord)`-style helper so callers see `"INFO"` instead of `"DialogResponsesBinaryOverlay"`. Single-line drop-in, no operator surface, no MCP shape change — fits the project's pre-auth bonus-catch precedent. Pays off when Phase 5's coverage-smoke or a future consumer hits the same code path against another record type missing from the switch. |
| Bridge dispatcher correctness | **Unaffected** — canary + Scenario 3.2 prove the v2.9 dispatcher works fine when override creation succeeds. INFO bug is at the layer BEFORE dispatcher runs. |

## Deviations from plan

1. **Scenario 3.1 carrier picked at scenario-build time per kickoff.** MATRIX § Layer 3 left the live FormID for Phase 3 to pick. Selected `Skyrim.esm:000E3D` (MQ101 Stormcloak/Imperial dialog) for rich existing DialogConditions across 5 entries spanning 3 dispatcher shapes (FLI Quest/Object + legacy Global + FLI Faction). NPC `Object` slot picked as Lydia `Skyrim.esm:000A2C8E` (HousecarlWhiterun) — different from canary's Hadvar for variety; conductor's recall of Lydia at `0001A6E8` was a typo (verified by query — that FormID doesn't exist; Lydia is `000A2C8E`). The pick was sound; the bug is record-type-level (any INFO), not FormID-specific.
2. **Scenario 3.1 BLOCKED before execution.** Bridge override-creation gap surfaced at the `mo2_create_patch` call. Halt-and-report per kickoff mandatory trigger; Phase 5 re-runs against post-Phase-4-fix bridge.
3. **Scenario 3.2 Effects[].PerkConditions preserve assertion (3.2.J) added at scenario-build time.** Conductor's "if you find a vanilla PERK with both top-level Conditions AND Effects[].PerkConditions, that'd be a richer test" criterion was met by `Skyrim.esm:0CB413` (DaedricSmithing has PerkConditionTabCount=2 with nested per-effect Conditions). Added assertion 3.2.J to verify the bonus richness — passes (top-level `add_conditions` doesn't bleed into per-Effect PerkConditions).

## Known issues / open questions

- **`info_override_missing_in_copyasoverride`** (see § Bugs surfaced) — Phase 4 fix item. Phase 5 re-runs Scenario 3.1 post-fix per the standard P3 → P4 → P5 cadence.
- **Scenario 3.1's INFO record selection (`Skyrim.esm:000E3D` MQ101 dialog) is preserved in this handoff for Phase 4's regression cell + Phase 5's re-run** — same FormID, same NPC `Object` slot value (Lydia `Skyrim.esm:000A2C8E`), same expected assertion table from the [original Scenario 3.1 proposal](../../../dev/plans/v2.9.X_condition_parameters/) earlier in this session's transcript.
- **No new architectural surprises beyond the INFO override gap.** Dispatcher correctness verified live across 4 concrete IFormLinkOrIndex<T> generic-T types (`IReferenceableObjectGetter`, `IPerkGetter`, `ISpellGetter`, `IItemOrListGetter`).

## Conductor asks

```
CONDUCTOR ASK
Phase: 3
Topic: Phase 4 spawn-or-skip recommendation
Context:
- Phase 3 surfaced 1 pre-existing bridge bug (info_override_missing_in_copyasoverride) blocking Scenario 3.1 at the override-creation layer (NOT a v2.9 dispatcher bug)
- Bug requires bridge code fix + race-probe + coverage-smoke regression + Phase 5 Scenario 3.1 re-run
- Bonus-catch DX fix (line-180 error wording, ~5 LOC) absorbed-not-optional per Aaron's promotion during HALT 2 of this session
- Pattern matches v2.7.1 P3 → P4 → P5 and v2.8.0 P3 → P4 → P5 (third cycle of the same cadence)
Question: Spawn Phase 4 (single session, 2 items) or skip to Phase 5?
Suggested options:
  A. SPAWN — single session covering Item 1 (INFO override support: ~30 LOC + 2 cells + 1 race-probe) + Item 2 (line-180 error-message DX bonus-catch: ~5 LOC). Mirrors v2.8.0 P4's single-session-with-items model. RECOMMENDED.
  B. SKIP — punt the INFO bug to a v2.9.x point release. Ship v2.9.0 with the gap. Not recommended: INFO is a high-traffic record type and the fix is small.
  C. SPLIT into sub-sessions per item — overkill; both items co-touch the same file (PatchEngine.cs) + share a regression cell.
Default if no response: A (spawn).
```

## Preconditions for Phase 4

| Precondition | State |
|---|---|
| Bridge SHA `2e3a1094…f8293975e` is the Phase-3-tested baseline | ✓ — captured above; race-probe + coverage-smoke ran clean against this SHA at Phase 2 close |
| `info_override_missing_in_copyasoverride` repro is trivial | ✓ — any `mo2_create_patch` with `op: "override"` against any INFO FormID; race-probe in-process Mutagen-direct write of an INFO override is the cleanest pre-fix evidence + post-fix regression |
| Fix angle is straightforward | ✓ — switch-case extension pattern (CopyAsOverride + TryRemoveOverride paired); no operator surface, no MCP shape change, no schema change |
| Phase 4 scope locked at items 1 + 2 above | ✓ — bonus-catch DX promoted to absorbed-not-optional; no other P3 findings to absorb |
| Live install state for Phase 4 | unchanged — Phase 4 is repo-side only (probe + fix + regression cells); doesn't sync to live until Phase 5 |
| Coverage-smoke baseline | 382 cells (376 PASS + 6 SKIP + 0 FAIL) at Phase 2 close. Phase 4 adds ~2 new INFO cells; expected total post-fix ~384 cells. |

## Preconditions for Phase 5

| Precondition | State |
|---|---|
| Live install at v2.9.0 | ✓ — synced in Phase 3; live bridge SHA matches P2D `2e3a1094…f8293975e`. Phase 5's `dotnet publish` will produce a new ship SHA different from this build SHA. |
| Layer 3 Scenario 3.2 verified live | ✓ — 12/12 sub-assertions PASS in this session |
| Layer 3 Scenario 3.1 needs re-run after Phase 4 lands the INFO fix | ⏸️ Phase 5 owns. Same record selection (`Skyrim.esm:000E3D` MQ101 dialog INFO + Lydia `Skyrim.esm:000A2C8E` Object slot) per § Known issues. |
| Final ship SHA | ⏸️ Phase 5 produces via `dotnet publish` (not `dotnet build`). Different SHA from Phase 2/3/4 build SHAs — that's the canonical v2.9.0 ship SHA, must be byte-identical across smoke matrix + installer bundle + live install per kickoff §4 + PLAN.md § Phase 5. |
| Coverage-smoke ship-SHA re-run | ⏸️ Phase 5 owns — runs against the published ship SHA, not the build SHA |
| Race-probe ship-SHA re-run | ⏸️ Phase 5 owns |

## Files of interest for Phase 4

| Path | Why |
|---|---|
| `tools/mutagen-bridge/PatchEngine.cs:2508–2571` | `CopyAsOverride` switch — add `IDialogResponsesGetter` branch alongside `IDialogTopicGetter` at line 2551 |
| `tools/mutagen-bridge/PatchEngine.cs:2581+` | `TryRemoveOverride` switch — matching branch per its doc comment |
| `tools/mutagen-bridge/PatchEngine.cs:177–180` | Error throw site — the bonus-catch DX fix replaces `.GetType().Name` with a clean type-code helper |
| `tools/race-probe/Program.cs` | Add INFO override + add_conditions probe (mirrors P2C/P2D probe patterns) |
| `tools/coverage-smoke/Program.cs` | Add `1.P.GetIsID.INFO` cell mirroring Scenario 3.1's shape (carrier `Skyrim.esm:000E3D`, GetIsID `parameters: {Object: "Skyrim.esm:02BF9F"}` — Hadvar reused since canary already verified that NPC FormKey resolves cleanly) |
| `mo2_mcp/CHANGELOG.md` | Append P4 entry under existing `## v2.9.0 — TBD` section |
| `KNOWN_ISSUES.md` | If Phase 4 fully fixes INFO, no entry needed (was never documented as a known limitation). If fix is partial, add entry under § Patching write surface |
| `dev/plans/v2.9.X_condition_parameters/PHASE_3_HANDOFF.md` (this file) | § Bugs surfaced has the full bug entry + 5-item fix angle |

## Files of interest for Phase 5

| Path | Why |
|---|---|
| `dev/plans/v2.8.0_verification/PHASE_5_HANDOFF.md` | Canonical 12-step ship sequence with halt cadence (Phase 4 hash-record verify → coverage-smoke ship-SHA re-run → Layer 3 re-run if Phase 4 ran → `dotnet publish` → installer build via direct ISCC → live sync → live sanity → CHANGELOG ship date → tag + push tag + GitHub release with hard halt → memory update → handoff + double-commit) |
| `dev/plans/v2.9.X_condition_parameters/PHASE_3_HANDOFF.md` (this file) § Preconditions for Phase 5 | What carries forward from Phase 3 (live state, Scenario 3.2 PASS, Scenario 3.1 carrier reservation) |
| `dev/plans/v2.9.X_condition_parameters/PHASE_4_HANDOFF.md` (when Phase 4 lands) | Phase 4's post-fix bridge SHA + coverage-smoke total + race-probe results — Phase 5 re-runs against the post-fix SHA |
| `mo2_mcp/CHANGELOG.md` | Phase 5 inserts ship date in the existing `## v2.9.0 — TBD` header |
| `mo2_mcp/config.py` + `installer/claude-mo2-installer.iss` + `README.md` | Version constants already at v2.9.0 since P2A (no re-bump in P5) |
