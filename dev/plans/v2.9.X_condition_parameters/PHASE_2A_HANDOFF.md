# Phase 2A Handoff — Generic Condition-parameter dispatch + 119 FormLinkOrIndex/IFormLink<T> wired + version bump v2.9.0

**Phase:** 2A
**Status:** Complete — all 12 deliverables landed; coverage-smoke green; ready for 2B kickoff
**Date:** 2026-04-26
**Session length:** ~3.5h
**Commits made:** `5a06179` (plan-amend) + work commit (this batch) + hash-record commit
**Live install synced:** No (Phase 5 owns live sync)

## What was done

- **`[v2.9 plan-amend]` first commit** at `5a06179` (PLAN.md + MATRIX.md only). Folded six architectural surprises CONDITIONS_AUDIT.md surfaced during Phase 1 into the plan: § A slot-list grew 5→6 (added `IFormLink<T>` for sub-A), § B example slot `Reference→Object` for GetIsID per audit §1, § Phase 1 step 2 dropped static skip list and points at the audit's dynamic detector + CTDA padding filter per audit §2/§3, § Carry-overs added new entry 7 naming the 6 sub-B deferred functions. MATRIX.md Scenario 3.1 + Layer 4.formid.* + harness output convention examples updated to use `Object` instead of `Reference`. 4.formid.02 reworded to match Mutagen's actual write-time-not-validate-time posture. 2 files / +16/−14 lines. No bridge code, no version bump.

- **`Models.cs ConditionEntry.Parameters` field added** — `Dictionary<string, JsonElement>?` per PLAN.md § Phase 2 step 3. Doc comment names the v2.9 dispatch contract, sub-A absorption, footgun-guard, and back-compat with `actor_value`. Existing `actor_value` field doc-comment refreshed to point at the parameters form.

- **`PatchEngine.cs` dispatcher infrastructure landed** in three pieces:
  1. **`KnownParameterizedFunctions`** static `HashSet<string>` with 119 names (113 single-`IFormLinkOrIndex<T>` + 6 sub-A single-`IFormLink<T>`). Sourced verbatim from `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1162–1207 (sub-A) + 1220–1447 (FLI). Doc comment names the 2B/2C/2D extension points and the NoParam-NOT-in-set design.
  2. **`RouteParameterSlot(condData, condDataType, functionName, slotName, jsonValue)`** generic dispatcher. Footgun-guard at top rejects any slot name containing `"Unused"` per audit §3. Reflection-property lookup on the ConditionData class. Branches: `IFormLinkOrIndex<T>` (parent + FormKey ctor — generalized from v2.8.0's Global handler at line 1697ff) and `IFormLink<T>` (single-FormKey ctor — sub-A absorption). Other shapes (Enum, Int32, Single, Boolean) throw "shape not yet wired in P2A" — guarded by `KnownParameterizedFunctions` so 2B/2C/2D extending the set is the trigger for those branches landing.
  3. **`BuildCondition` integration** — DSL-ambiguity check (`actor_value` + `parameters.ActorValue` both supplied → throw) + out-of-scope check (function not in set + `parameters` supplied → throw with v2.9.0 in-scope reference) + foreach over `ce.Parameters` calling `RouteParameterSlot`. Existing `actor_value` handler at lines 1631–1645 unchanged, runs before the new dispatcher block (back-compat preserved).

- **Bridge build clean** (0 warnings, 0 errors). New SHAs:
  - `mutagen-bridge.exe`: `5541734d2c4086a38830547a59cb8751405109c56bfa3b452b48d9c8e3338d23`
  - `mutagen-bridge.dll`: `f7f667baf84e1186eb8488639b2bcd8740116e21c24be4fcbf436c971b8aa62b`
  - v2.8.0 ship baseline was `f998c4e0…6c8bb04`; 2A diverges as expected. 2B/2C/2D's SHAs will diff against this baseline to surface drift outside their declared scope. Phase 5 produces the canonical ship SHA.

- **Coverage-smoke regression cells +134** (`tools/coverage-smoke/Program.cs`):
  - **Test 161 [1.P.GetIsID.MGEF]** — canary cell, hand-written. Pipes a synthetic `bridge_request` with `parameters: {Object: "Skyrim.esm:0001A6E8"}` through the bridge subprocess; reads back the output ESP via `SkyrimMod.CreateFromBinary`; asserts `condition.Data.GetType() == GetIsIDConditionData` and `Object.Link.FormKey` resolves to `01A6E8:Skyrim.esm` (NOT default FormID 0). Halt-and-report point per kickoff; PASS confirmed before bulk wiring.
  - **Tests 162–279 [1.P.<Function>.MGEF]** — 118 bulk cells via helper `RunFLIDispatcherCell` (defined near `RunBridge` in the same file). Driven by a `(Function, Slot, Branch)` tuple list. Each cell: build single-condition `add_conditions`, pipe through bridge, read back, walk slot's FormKey. Branch-aware readback (`.Link.FormKey` for FLI vs `.FormKey` direct for `IFormLink<T>`). All 118 PASS.
  - **Tests 280–294 [1.D / 2 / 4]** — 12 PASS-asserting cells + 2 SKIP-with-reason:
    - 280 [1.D.01] FLI bad FormID → record-level error
    - 281 [1.D.02] sub-A IFormLink<T> bad FormID → record-level error
    - 282 [1.D.50] sub-B `GetVMScriptVariable` + parameters → out-of-scope error (named function + version reference)
    - 283 [1.D.51] GetIsID + unknown SlotName → no-such-slot error
    - 284 [1.D.53] NoParam `GetDead` w/o parameters → v2.7.1+ back-compat preserved
    - 285 [1.D.54] NoParam `GetDead` + bogus parameters → out-of-scope error (validates NoParam-NOT-in-set design)
    - 286 [2.03] SPEL `set_fields: {Effects: [{... Conditions: [{HasPerk parameters: {Perk: ...}}]}]}` — composition probe; v2.8 Effects-list × v2.9 dispatcher integrated through shared `BuildCondition` factory. Readback asserts nested `HasPerkConditionData.Perk.Link.FormKey` resolved.
    - **2.05** SKIP-with-reason: back-compat coexistence (record A `actor_value` + record B `parameters: {ActorValue}`) requires 2B Enum branch; record A side covered by 4.dsl.02.
    - 288 [4.dsl.01] both forms supplied → unambiguous-DSL error (proves the static-string-name check fires before slot routing — 2B-independent).
    - 289 [4.dsl.02] `actor_value` alone with v2.9 dispatcher present → v2.8 path preserved; readback `ActorValue=Stamina`.
    - **4.dsl.03** SKIP-with-reason: parameters: {ActorValue: ...} alone needs 2B's Enum branch + GetActorValue in `KnownParameterizedFunctions`.
    - 291 [4.formid.01] non-hex FormID → parse error (FormIdHelper rejection).
    - 292 [4.formid.02] unresolved-plugin FormID → documenting Mutagen's actual posture (PASS documented).
    - 293 [4.formid.03] well-formed-but-absent FormID → write succeeds (matches v2.8 write-time-not-validate posture).
    - 294 [4.slot.04] footgun-guard probe — `parameters: {SecondUnusedIntParameter: 42}` → guard fires with audit §3 reference.

- **Race-probe v2.9 P2A canary section +7 probes** (`tools/race-probe/Program.cs`). In-process Mutagen-direct round-trip probes complementing coverage-smoke's bridge-subprocess path. Each probe constructs a `*ConditionData` instance, simulates `RouteParameterSlot`'s logic inline (FormLinkOrIndex<T>(parent, formKey) for FLI; FormLink<T>(formKey) for IFormLink<T>), reads back via reflection, asserts FormKey round-trip:
  - `GetIsID/Object` (IFormLinkOrIndex<IReferenceableObjectGetter>)
  - `HasMagicEffect/MagicEffect` (IFormLinkOrIndex<IMagicEffectGetter>)
  - `GetInFaction/Faction` (IFormLinkOrIndex<IFactionGetter>)
  - `GetVATSValueWeapon/Value` (IFormLink<IWeaponGetter>)
  - `GetVATSValueTarget/Value` (IFormLink<INpcGetter>)
  - 2 footgun-guard probes (SecondUnusedIntParameter + FirstUnusedStringParameter on GetIsID — confirms the guard is load-bearing: Mutagen does expose these slots via reflection, so without the guard they'd be writable).

- **`tools_patching.py` add_conditions schema description** updated. New `parameters` key documented with the per-shape slot-type table, the sub-A absorption note, the footgun-guard note, and a CONDITIONS_AUDIT.md pointer for full slot signatures. The `actor_value` field's description rewritten as v2.8 back-compat sugar with explicit cross-ref to the unambiguous-DSL contract. Top-level `add_conditions` description updated to mention the SPEL Effects-list composition path.

- **`KNOWN_ISSUES.md`** updated. Moved "Other Condition-function parameter slots" from carry-over to a new section: **`## Condition-parameter coverage (v2.9.0 P2A)`** naming the 119-function in-scope set, the DSL contract, the footgun-guard, and the gaps still open in v2.9.0 (2B Enum, 2C MultiSlot, 2D PrimitiveOnly, sub-B deferred, NoParam in-scope-no-op). Top-line "Current as of v2.8.0" → "Current as of v2.9.0".

- **`CHANGELOG.md`** new `## v2.9.0 — TBD` entry inserted ahead of v2.8.0. Sections: Added (parameters dispatcher + footgun-guard + back-compat preservation), Architecture (RouteParameterSlot + KnownParameterizedFunctions), Tests (+134 coverage-smoke, +7 race-probe), Documentation (schema + KNOWN_ISSUES + plan-amend), Out of scope (2B/2C/2D + sub-B), Carry-overs from v2.8.0.

- **Version bumped to v2.9.0** in:
  - `mo2_mcp/config.py`: `PLUGIN_VERSION = (2, 9, 0)`
  - `installer/claude-mo2-installer.iss`: `#define AppVersion "2.9.0"`
  - `README.md` line 7 (download URL) + line 59 (manual install reference)

## Verification performed

- **Bridge build:** clean across multiple iterations. Final SHA captured (above).
- **Race-probe v2.9 P2A section:** 7/7 PASS.
  ```
  === v2.9 P2A — dispatcher functional probes (in-process Mutagen-direct) ===
    [GetIsID                       ] PASS  FLI Object<IReferenceableObjectGetter> round-trip ✓
    [HasMagicEffect                ] PASS  FLI MagicEffect<IMagicEffectGetter> round-trip ✓
    [GetInFaction                  ] PASS  FLI Faction<IFactionGetter> round-trip ✓
    [GetVATSValueWeapon            ] PASS  IFormLink<IWeaponGetter> round-trip ✓
    [GetVATSValueTarget            ] PASS  IFormLink<INpcGetter> round-trip ✓
    [footgun-guard (SecondUnusedIntParameter )] PASS  guard recognizes *Unused* + Mutagen exposes the slot (load-bearing) ✓
    [footgun-guard (FirstUnusedStringParameter)] PASS  guard recognizes *Unused* + Mutagen exposes the slot (load-bearing) ✓
  === v2.9 P2A probes: ALL PASS ===
  ```
- **Inline canary smoke (Test 161 [1.P.GetIsID.MGEF])** verified end-to-end before bulk wiring. Halt-and-report trace from coverage-smoke output:
  ```
  ── Test 161 [1.P.GetIsID.MGEF]: MGEF + add_conditions GetIsID via parameters.Object (v2.9 P2A dispatcher canary) ──
    source: Skyrim.esm:0173DC (BanishDmgHealthFFTargetActor)
    target: Skyrim.esm:0001A6E8 (parameters.Object — IFormLinkOrIndex<IReferenceableObjectGetter>)
    exit: 0
    readback: appended condition is GetIsIDConditionData with Object.Link.FormKey=01A6E8:Skyrim.esm ✓ (v2.9 dispatcher canary verified — Object slot resolved through RouteParameterSlot's IFormLinkOrIndex<T> branch, NOT default FormID 0)
    PASS
  ```
- **Coverage-smoke end-to-end:** **294 cells, 288 PASS + 6 SKIP, 0 FAIL.** Final run output: `=== smoke complete: ALL PASS ===`.
  - 160 v2.8.0 baseline (156 PASS + 4 documented SKIP carryover: 1.r.40 OTFT, 1.r.47 SPEL, 1.D.04 CELL, 4.esl.01 ESL).
  - 134 v2.9 P2A new (132 PASS + 2 SKIP awaiting 2B): 119 Layer 1.P.FormLink + 6 Layer 1.D + 1 Layer 2.03 + 1 Layer 2.05 SKIP + 3 Layer 4.dsl + 3 Layer 4.formid + 1 Layer 4.slot.04 footgun.
  - All 22 pre-v2.8.0 + 138 v2.8.0 baseline cells stay green — zero regression.
- **Inventory-probe-confirmed slot signatures** transcribed verbatim from CONDITIONS_AUDIT.md scratch lines 1162–1207 + 1220–1447. No speculation; the dispatcher uses runtime reflection so the bridge-side `KnownParameterizedFunctions` set is the only place that names them.

## Bugs surfaced

None during P2A. The dispatcher landed cleanly on the first build; only one mid-development assertion bug (in coverage-smoke's bulk-test helper, not the bridge) — `FirstOrDefault` looking up the appended condition by ConditionData type returned a pre-existing same-type condition for MGEFs that happened to carry one (Test 237 [1.P.HasKeyword.MGEF] surfaced this). Switched to `LastOrDefault` since `add_conditions` appends; bridge behavior was always correct. Captured in coverage-smoke as a `// LastOrDefault matches even when source carries pre-existing same-type condition` comment for future test-helper consumers.

## Deviations from plan

- **Coverage-smoke harness pattern.** The kickoff's default sequence was "race-probe canaries → inline smoke 1 function → coverage-smoke cells." I delivered Test 161 (the canary) directly inside coverage-smoke (rather than as a standalone race-probe canary that writes its own ESP), then immediately added the bulk loop + 1.D + 2 + 4 cells as the same coverage-smoke run grew. Race-probe's v2.9 P2A section landed afterward as in-process Mutagen-direct probes complementing the bridge-subprocess coverage. Outcome equivalent to the kickoff intent (one canary verified end-to-end before bulk wiring); structure differs slightly. The race-probe canary section also doesn't write ESPs — it tests the Mutagen API surface in isolation, which is arguably a cleaner separation than another bridge-subprocess round-trip.
- **Test 161 readback assertion needed two iterations.** First version tried `slotVal.GetType().GetProperty("FormKey")` directly on `FormLinkOrIndex<T>` — that's null. Mutagen's `FormLinkOrIndex<T>` exposes `.Link.FormKey` (where `.Link` is `IFormLinkNullable<T>`), not `.FormKey` directly. Second version walks the two-step path. Captured in the helper's comment so 2B's Enum-branch readback (which has its own runtime shape) doesn't repeat the discovery.
- **MGEF FormKey assertion uses 6-hex-digit form.** Mutagen's `FormKey.ToString()` produces `HEXID:plugin.esm` with a 24-bit local ID (master-flag bits stripped), so the bridge input `Skyrim.esm:0001A6E8` round-trips as `01A6E8:Skyrim.esm`. The 119-cell substring assertion uses `1A6E8` (without leading zero) to be robust across both renderings.
- **Layer 2.05 + Layer 4.dsl.03 SKIP-with-reason** instead of PASS in P2A. Both cells assert `parameters: {ActorValue: ...}` lands successfully; neither can pass until 2B adds GetActorValue to `KnownParameterizedFunctions` AND lands the Enum branch in `RouteParameterSlot`. Documented in the SKIP messages as "lifts to PASS in 2B." Conductor confirmed this clustering at halt-and-report; not a deviation from the kickoff per se, more an explicit phase boundary call-out.
- **No `[v2.9 plan-amend]` modification of PLAN.md § Phase 1 conductor decisions floor-AV** for the alleged `GetActorValuePercentage`-vs-`GetActorValuePercent` typo. Conductor confirmed at halt-and-report that PLAN.md never named GetActorValuePercentage in any version — the typo lived in CONDITIONS_AUDIT.md § Architectural surprises §4's miscredit, not in PLAN.md itself. Substantive fix already locked in the audit; not worth a separate plan-amend.

## Known issues / open questions

- **Sub-B deferral list captured.** PLAN.md § Carry-overs entry 7 (added in plan-amend) names the 6 functions; KNOWN_ISSUES.md § Condition-parameter coverage subsection references them. Real consumer surfacing will trigger a v2.9.x follow-up (probably via a new accept-any-string operator surface decision).
- **2 SKIP-with-reason cells** (Layer 2.05 + Layer 4.dsl.03) are 2B's first lift targets. Both require GetActorValue in `KnownParameterizedFunctions` + Enum branch in `RouteParameterSlot`.

## Conductor asks

None — all halt-and-report points landed cleanly:
- Plan-amend diff confirmed before dispatcher work.
- Canary smoke confirmed before bulk wiring.
- Coverage-smoke green confirmed before handoff.

## Preconditions for Phase 2B

| Precondition | State |
|---|---|
| `RouteParameterSlot` exists with `IFormLinkOrIndex<T>` + `IFormLink<T>` branches landed; Enum-branch stub state ready for 2B extension | ✓ — at `PatchEngine.cs` lines 1880ff. Adding the Enum branch is `else if (propType.IsEnum) { ... Enum.Parse(propType, jsonValue.GetString()!, ignoreCase: true) ... }` — 5–10 LOC. |
| `Models.cs ConditionEntry.Parameters` field landed | ✓ — `Dictionary<string, JsonElement>?` at lines 521–542. |
| `KnownParameterizedFunctions` extension pattern demonstrated | ✓ — 119 names in `PatchEngine.cs` `KnownParameterizedFunctions` HashSet. 2B adds 41 Enum function names; doc comment at the top of the field anchors the convention. |
| `BuildCondition` integration: foreach + DSL-ambiguity check + out-of-scope check | ✓ — at `PatchEngine.cs` lines 1657–1690. The DSL-ambiguity check is already correct for 2B's GetActorValue case (covered by Test 288 [4.dsl.01]); 2B's Enum-branch routing simply needs to hit the same check before the foreach reaches `RouteParameterSlot`. |
| 2 SKIP-with-reason cells (Layer 2.05 + Layer 4.dsl.03) ready to be lifted to PASS in 2B | ✓ — coverage-smoke `Skip("2.05", ...)` and `Skip("4.dsl.03", ...)` calls at the end of the v2.9 P2A section. Lifting them = removing the Skip calls and writing the assertion blocks (template: 4.dsl.02 for the actor_value-only path; new 4.dsl.03 mirrors but uses `parameters` instead of `actor_value`). |
| Bridge SHA snapshot for cross-sub-phase drift detection | ✓ — `5541734d2c4086a38830547a59cb8751405109c56bfa3b452b48d9c8e3338d23` (v2.9.0 P2A). 2B's build SHA must change (Enum branch + 41 names added); but other-shape branches should NOT change (FLI, IFormLink, footgun-guard) — diff against this baseline to surface drift. |

## Files of interest for Phase 2B

| Path | Why |
|---|---|
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` lines 1739–1849 | `KnownParameterizedFunctions` set — extend with 41 Enum function names per scratch lines 1136–1219. |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` lines 1880–1970 | `RouteParameterSlot` helper — add Enum branch between IFormLink<T> branch and the catch-all. Pattern: `if (propType.IsEnum) { var parsed = Enum.Parse(propType, jsonValue.GetString()!, ignoreCase: true); prop.SetValue(condData, parsed); return; }`. v2.8 actor_value handler (lines 1631–1645) stays as-is — it's the back-compat sugar path; the new Enum branch will route the same property through the dispatcher when `parameters: {ActorValue: ...}` is supplied directly. |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` lines 1657–1690 | DSL-ambiguity check + foreach over `ce.Parameters`. 2B doesn't modify; just confirms it covers the actor_value case (it does — Test 288 4.dsl.01 PASSes). |
| `Claude_MO2/tools/coverage-smoke/Program.cs` (end of file, in the v2.9 P2A section) | Lift `Skip("2.05", ...)` and `Skip("4.dsl.03", ...)` → real assertion blocks. Add 41 Layer 1.P.Enum positive cells via the same bulk-loop pattern (helper would need a separate `RunEnumDispatcherCell` since the readback is `prop.GetValue(condData)?.ToString()` rather than the FormLinkOrIndex two-step walk; ~30 LOC). Add 1 Layer 1.D.03 [Enum representative negative] cell — bad enum name. |
| `Claude_MO2/tools/race-probe/Program.cs` lines 1880ff (v2.9 P2A section) | Add 2–3 Enum representative probes complementing coverage-smoke's bridge-subprocess path. Pattern: construct ConditionData → `Enum.Parse(propType, value, ignoreCase: true)` → reflection setter → readback `.ToString()`. |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` § Architectural surprises §4 + Floor + stretch slot signatures | Source-of-truth for 2B Enum-shape function list and per-function slot signatures. |
| `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1136–1219 | Per-Enum-function full slot detail (41 functions) — 2B's KnownParameterizedFunctions extension reads from here. |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` v2.9.0 entry | 2B appends to the existing v2.9.0 — TBD entry rather than creating a new top-level entry (single bridge SHA per release; sub-phases bundle into one ship per Phase 5). |

## Acceptance — Phase 2A (per kickoff)

- ✓ Plan-amend commit landed first (`5a06179`); PLAN.md + MATRIX.md fold all 6 architectural surprises.
- ✓ `ConditionEntry.Parameters` field added with the documentation comment per PLAN.md § Phase 2 step 3.
- ✓ `KnownParameterizedFunctions` populated with 119 P2A function names. (2B/2C/2D extend this set; 2A doesn't pre-populate their names.)
- ✓ `RouteParameterSlot` handles `IFormLinkOrIndex<T>` + `IFormLink<T>` + footgun-guard. Other shapes throw clean "shape not yet wired in P2A — landing in 2B/2C/2D" error; absent-by-design rather than stubbed (per kickoff: "or just absent — depends on whether you want 2B/2C/2D's KnownParameterizedFunctions extension to be the trigger, simpler").
- ✓ `BuildCondition` integration: foreach over `ce.Parameters` after the existing actor_value handler; DSL-ambiguity check; out-of-scope check.
- ✓ Bridge builds 0 warnings / 0 errors.
- ✓ Inline smoke (Test 161 canary): synthetic `bridge_request` exercising GetIsID pipes through bridge and reads back via Mutagen-direct with `Object.Link.FormKey` resolved to `01A6E8:Skyrim.esm` (NOT default FormID 0).
- ✓ Race-probe canaries cover the dispatcher's branches (5 representative + 2 footgun-guard probes; in-process Mutagen-direct).
- ✓ Coverage-smoke total: 160 v2.8.0 + 134 v2.9 = 294 cells, 288 PASS + 6 SKIP, 0 FAIL.
- ✓ All 22 pre-v2.8.0 + 138 v2.8.0 = 160 baseline cells stay green (no regression).
- ✓ Schema description, CHANGELOG, KNOWN_ISSUES updated per the Phase 2A bullets.
- ✓ Version bumped in all four version-bearing files (`config.py`, `.iss`, `README.md` lines 7 + 59).
- ✓ Handoff under 400 lines.

## End-of-phase ritual

Per kickoff:
1. ✓ Final state matches acceptance criteria.
2. ✓ Handoff written per template (this file).
3. ✓ Did NOT write Phase 2B's kickoff prompt — conductor owns that after this handoff lands.
4. ⏳ Force-add new file (next): PHASE_2A_HANDOFF.md.
5. ⏳ Push the triple-commit chain (next): plan-amend already pushed at `5a06179`; work commit + hash-record commit follow.
