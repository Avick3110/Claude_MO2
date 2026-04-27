# Phase 2D Handoff — PrimitiveOnly closer (11 wired) + Phase 2 dispatcher feature-complete

**Phase:** 2D
**Status:** Complete — all 8 deliverables landed; coverage-smoke green; race-probe green; drift-detection clean; **Phase 2 closing summary captured**; ready for Phase 3 kickoff
**Date:** 2026-04-27
**Session length:** ~2h
**Commits made:** work commit + hash-record commit (this batch)
**Live install synced:** No (Phase 5 owns live sync; Phase 3 needs v2.8.0 → v2.9.0 sync before workflow scenarios — see § Preconditions)

## What was done

- **`KnownParameterizedFunctions` += 11 PrimitiveOnly function names** (`tools/mutagen-bridge/PatchEngine.cs:1986ff`) sourced verbatim from `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1532–1554. Comment header captures the per-function rationale (alias-index lookups for quest-alias-index gating in dialog/quest patchers, package-data accessors, GetVATSValueUnknown as the genuinely-Int32-typed VATS variant distinct from sub-A's IFormLink<T> family, GetPlayerControlsDisabled dual Int32, IsLimbGone). HashSet total: 188 P2C → **199** = max-band Pareto closed (113 FLI + 6 sub-A IFormLink + 41 Enum + 28 MultiSlot + 11 PrimitiveOnly).

- **RouteParameterSlot doc-comment refresh** (`PatchEngine.cs:1979-2024`) — folds P2D narrative into the unified Phase 2 explanation. Lifts to "Phase 2 feature-complete after P2D: 5 of 6 PLAN.md § A branches landed across 199 dispatcher-wired functions (Boolean is the single design-vs-implementation gap, deferred to first v2.9.x consumer trigger)." Per-branch function counts annotated (P2A 119, P2B 41, P2C 28, P2D 11). Multi-slot composition narrative explicitly extends to P2D: "P2C's 28 MultiSlot and P2D's 11 PrimitiveOnly functions wire purely via KnownParameterizedFunctions additions. No new dispatcher abstraction across either phase."

- **Outer HashSet doc-comment refresh** (`PatchEngine.cs:1737-1762`) — restructures from prose to a `<list type="bullet">` with per-phase entries (P2A → P2D), reflecting the closed Pareto. NoParam 219 + sub-B 6 deferral notes preserved.

- **Bridge build clean** (0 warnings, 0 errors). Build SHA captured at Stage 1 and held byte-identical through Stages 2–3 (test harness + docs work doesn't touch bridge code):
  - 2C baseline: `a96c90410b68cd8e338c11a4540c5b8908399666d6255320fc8f3b8d0668c188`
  - **2D (this build): `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e`**
  - mutagen-bridge.dll: `e1a1eafe8387e99c9f2e94893af5afafca032a35c2cc97c19c91a4ffdbbacf21`

- **+11 v2.9 P2D coverage-smoke cells** in `tools/coverage-smoke/Program.cs`:
  - Test 371 [1.P.GetIsAliasRef.MGEF] — PrimitiveOnly canary (1-slot Int32, ReferenceAliasIndex=42). Halt-1 rigor checkpoint with verbose halt-and-report formatting modeled on P2C's Test 339 GetEventData canary; readback confirmed `GetIsAliasRefConditionData.ReferenceAliasIndex==42` (NOT default 0), proving HashSet += name suffices to enable a function through the already-live P2C Int32 branch. Real-patcher use case: quest-alias-index gating in dialog/quest patchers.
  - Tests 372–380 — 9 bulk PrimitiveOnly positives via the existing `RunMultiSlotDispatcherCell` helper (P2C's helper handles Int32/Single/Boolean uniformly via dynamic slot discovery; PrimitiveOnly's all-Int32 signatures dispatch through the Int32 branch case — zero new helper code). GetPlayerControlsDisabled exercises the helper's dual-Int32 path; the other 8 are 1-slot Int32. Each cell's trace reduces to "{N}-slot: \<slot>{1}\<Int32>=42 [| \<slot>{2}\<Int32>=42]" — minimal trace surface matches the function shape.
  - **1.P.GetVATSValueUnknown.MGEF — SKIP-with-reason (NEW; bonus-catch absorbed).** See § Bugs surfaced.

- **Race-probe v2.9 P2D section added** (`tools/race-probe/Program.cs:2167ff`) — 1 representative Int32-only PrimitiveOnly probe (IsLimbGone, 1-slot Int32). Reuses P2C's `ProbeMultiSlot` helper since the dispatcher branch is the same code path; failure attribution delta-tracked via `p2cBefore`/`p2cFailures = p2cBefore` unwind so the per-phase scoreboard stays clean across the totalFailures rollup. Total race-probe surface: 7 P2A + 3 P2B + 4 P2C + 1 P2D = **15 PASS**.

- **`tools_patching.py` schema description updated** — `parameters` key extended with P2D content: 188 → 199 in-scope; reframed as "v2.9.0 covers 199 functions across five dispatcher branches (5 of 6 PLAN-named branches landed; Boolean is the single design-vs-implementation gap)"; added PrimitiveOnly enumeration with the alias-index / package-data / VATS-Int32 / IsLimbGone / GetPlayerControlsDisabled detail; added GetIsAliasRef canonical example.

- **`KNOWN_ISSUES.md` updated** — section header `(v2.9.0 P2A + P2B + P2C)` → `(v2.9.0)` (drop sub-phase qualifier — release feature-complete after P2D); intro count `188 functions across five branches` → `199 functions across five branches` + adds "Phase 2 feature-complete" lead; lifts PrimitiveOnly bullet from gap-list to covered-for; adds new entry under § Patching write surface for `GetVATSValueUnknown` Mutagen 0.53.1 schema gap (alongside AMMO enchantment + Outfit/Spell VMAD); gap-list shrunk to 3 entries (Boolean deferred design-only, sub-B 6 String-slot, NoParam 219 in-scope-no-op).

- **`mo2_mcp/CHANGELOG.md` updated** — top brief paragraph rewritten to mark Phase 2 feature-complete and the final 199-function dispatcher scope ("Phase 2 feature-complete: 199 dispatcher-wired functions across five branches"); appended P2D content under existing `## v2.9.0 — TBD` entry across Added — bridge / Architecture / Tests / Documentation sections; PrimitiveOnly bullet dropped from Out-of-scope (now covered); GetVATSValueUnknown bonus-catch documented inline under Tests (the SKIP-with-reason narrative).

## Verification performed

- **Bridge build:** clean (0/0). Stage 1 SHA `2e3a1094…f8293975e` captured and unchanged through Stages 2–3.

- **Inline canary smoke (Test 371 [1.P.GetIsAliasRef.MGEF]) — halt-and-report #1:**
  ```
  ── Test 371 [1.P.GetIsAliasRef.MGEF]: GetIsAliasRef + parameters: {ReferenceAliasIndex: 42} (1-slot Int32 PrimitiveOnly canary) ──
    source: Skyrim.esm:0173DC (BanishDmgHealthFFTargetActor)
    target: parameters.ReferenceAliasIndex=42 (PrimitiveOnly Int32 — KnownParameterizedFunctions name added P2D, dispatcher branch landed P2C)
    exit: 0
    readback: GetIsAliasRefConditionData.ReferenceAliasIndex=42 ✓ (Int32 P2C branch)
    PrimitiveOnly canary verified — KnownParameterizedFunctions += 'GetIsAliasRef' lets caller's parameters dispatch through the Int32 branch (P2C). Zero new dispatcher code.
    PASS
  ```
  Aaron cleared halt-1 with: "Confirmed. Canary proves the architectural promise — HashSet += name suffices, dispatcher is feature-complete."

- **Race-probe v2.9 P2D section: 1/1 PASS:**
  ```
  === v2.9 P2D — PrimitiveOnly dispatcher functional probe (in-process Mutagen-direct) ===
    [IsLimbGone                    ] PASS  MultiSlot 1-slot: Limb<Int32>=42 ✓
  === v2.9 P2D probes: ALL PASS ===
  ```
  All 7 P2A + 3 P2B + 4 P2C probes still PASS; total **15/15**.

- **Coverage-smoke end-to-end — halt-and-report #2:** **382 cells, 376 PASS, 6 SKIPs, 0 FAIL.** Final run: `=== smoke complete: ALL PASS ===` (exit 0).
  - 160 v2.8.0 baseline (156 PASS + 4 carryover SKIPs unchanged).
  - 134 v2.9 P2A (134 PASS).
  - 45 v2.9 P2B (45 PASS).
  - 32 v2.9 P2C: 31 PASS + 1 SKIP (1.P.Unknown.MGEF — round-trip reclassification).
  - 11 v2.9 P2D: 1 canary PASS + 9 bulk PASS + 1 SKIP (1.P.GetVATSValueUnknown.MGEF — Mutagen 0.53.1 schema gap; see § Bugs surfaced).
  - 6 SKIPs: 4 v2.8 baseline carryovers (1.r.40, 1.r.47, 1.D.04, 4.esl.01) + 1 P2C UnknownConditionData round-trip + 1 NEW P2D GetVATSValueUnknown Mutagen-schema-gap.

- **Drift-detection diff:** `git diff HEAD -- tools/mutagen-bridge/` shows ONLY `PatchEngine.cs` modified (1 file, +72/-27 lines), with **2 hunks**:
  - `@@ -1737` — outer HashSet doc-comment (P2A/P2B → unified P2A/P2B/P2C/P2D structure with per-phase bullet list).
  - `@@ -1973` — HashSet body (11-name append after the P2C MultiSlot block) + RouteParameterSlot doc-comment refresh.
  - Other-shape branches (FLI / IFormLink / Enum / Int32 / Single at PatchEngine.cs:2049–2186) **byte-identical to 2C baseline**. v2.8 actor_value handler at lines 1631–1645 + BuildCondition foreach at lines 1681–1685 **byte-identical to 2C**. No other bridge files touched.

- **Phase 2 cumulative cell count:** P2A (134) + P2B (45) + P2C (32) + P2D (11) = **222 v2.9-new cells** added on top of v2.8.0's 160 baseline. 382 total cells. Phase 2 cumulative race-probe count: P2A (7) + P2B (3) + P2C (4) + P2D (1) = **15 race-probes**. All across 4 sub-sessions over 2026-04-26 → 2026-04-27.

## Bugs surfaced

### GetVATSValueUnknown — Mutagen 0.53.1 schema gap (bonus-catch absorbed)

**Mandatory halt-2 trigger** per kickoff: "An UnknownConditionData-style round-trip artifact surfaces on a PrimitiveOnly function (per 2C handoff this isn't expected, but escalate if it does)." Surfaced during P2D bulk wiring at Test 377 (alphabetical position of GetVATSValueUnknown in the original 10-bulk array). Aaron approved Option A absorption (SKIP-with-reason + KNOWN_ISSUES + CHANGELOG bonus-catch note; HashSet count holds at 199).

**Repro:** bridge stdin pipe with `function: "GetVATSValueUnknown"` + `parameters: {Value: 42, ValueType: 42}` against any Conditions-bearing record. Bridge dispatcher write succeeds (both Int32 slots land via reflection). Mutagen's `SkyrimMod.WriteToBinary` throws at the CTDA serialization step.

**Stack trace (key frames, captured to scratch):**
```
RecordException => BanishDmgHealthFFTargetActor (0173DC:Skyrim.esm<MagicEffect>):
  The method or operation is not implemented.
System.NotImplementedException: The method or operation is not implemented.
   at Mutagen.Bethesda.Skyrim.AGetVATSValueConditionData.GetValueFunction(IAGetVATSValueConditionDataGetter obj)
   at Mutagen.Bethesda.Skyrim.AGetVATSValueConditionDataBinaryWriteTranslation.WriteBinaryValueFunctionParseCustom(MutagenWriter, IAGetVATSValueConditionDataGetter)
   at Mutagen.Bethesda.Skyrim.GetVATSValueUnknownConditionDataBinaryWriteTranslation.WriteEmbedded(...)
```

**Root cause:** `AGetVATSValueConditionData.GetValueFunction()` is abstract on the parent class. The other six `AGetVATSValue*` concrete subclasses (sub-A IFormLink<T> family — CriticalEffect/Target/Weapon ± OrList) override it to return their CTDA function code. Mutagen 0.53.1 forgot to implement the override on the Int32-typed `GetVATSValueUnknownConditionData` subclass. Hard write-time failure regardless of slot values.

**Distinct from P2C's Unknown CTDA round-trip artifact:** P2C's was a read-side reclassification (write succeeded, harness type-name lookup couldn't anchor). P2D's is a write-time hard fail: Mutagen can't serialize the object at all.

**Bridge dispatcher correctness:** unaffected — the per-slot reflection writes (Value=42, ValueType=42 both Int32 → SetValue) are correct and would survive a future Mutagen 0.54+ release where the override lands.

**Resolution applied (Option A per Aaron's halt-2 confirmation):**
- `KnownParameterizedFunctions` retains `GetVATSValueUnknown` (199 functions stay) — bridge IS dispatcher-correct; the limitation is downstream Mutagen.
- Coverage-smoke: SKIP-with-reason at the bottom of the P2D bulk block (mirrors P2C's UnknownConditionData SKIP shape). Inline comment captures the full Mutagen-internal-frames analysis.
- KNOWN_ISSUES.md: new bullet under § Patching write surface alongside AMMO enchantment + Outfit/Spell VMAD ("Mutagen 0.53.1 schema gap, v2.9.x candidate when upstream lands the missing override").
- CHANGELOG.md: bonus-catch note under v2.9.0 P2D Tests section (the per-function explanation + SKIP rationale).

**Forward-carry guidance:**
- Phase 3 (live workflow scenarios): unaffected — Layer 3 scenarios target GetIsID + HasPerk/HasSpell, not GetVATSValueUnknown. No real-patcher Authoria-style mod calls GetVATSValueUnknown.
- Phase 5 (live sync + ship): unaffected — the SKIP-with-reason cell + KNOWN_ISSUES + CHANGELOG narrative ship as part of v2.9.0; future v2.9.x point release lands the cell as PASS when Mutagen upgrades.

## Deviations from plan

- **Layer 1.D PrimitiveOnly representative deliberately SKIPPED as duplicate** (per kickoff §3 deliverable: "OR confirm 2C's Test 369 Int32-coercion negative already covers PrimitiveOnly's path; if so, SKIP duplicate"). The Int32 branch's `ValueKind != Number` guard fires uniformly inside the Int32 branch regardless of caller function; PrimitiveOnly functions exercise the same code path. P2C's Test 369 (GetStageDone Stage as string → Int32 type-coercion failure) covers the dispatcher path; a duplicate cell adds no coverage. Documented inline at the P2D section header in coverage-smoke + here in handoff.

- **Cell count delta vs kickoff target.** Kickoff projected 12–15 P2D new cells (~383–386 total) accommodating an optional Layer 1.D representative + race-probe. Actual: **11 P2D cells (382 total)** — 1 canary + 9 bulk PASS + 1 SKIP. The 1-cell delta below the lower bound stems from (a) the Layer 1.D representative SKIP-duplicate decision and (b) the Mutagen-schema-gap conversion of GetVATSValueUnknown from PASS-bulk to SKIP. Both within the kickoff's "5 SKIPs persist unchanged" → "6 SKIPs after P2D" envelope (the new SKIP being the bonus-catch).

- **6 SKIPs vs kickoff's "5 SKIPs persist unchanged" assumption.** Kickoff acceptance section said "5 SKIPs persist unchanged (4 v2.8 baseline + 1 P2C UnknownConditionData artifact)". Actual: **6 SKIPs** — the original 5 stayed unchanged + 1 new bonus-catch (1.P.GetVATSValueUnknown.MGEF). The new SKIP is the Mutagen 0.53.1 schema gap surfaced at halt-2 + absorbed via Aaron's Option A confirmation. The original 5 SKIPs (1.r.40, 1.r.47, 1.D.04, 4.esl.01, 1.P.Unknown.MGEF) all still PASS-PASSing or SKIP-SKIPping unchanged — drift-confirmation passes.

- **Race-probe failure attribution.** P2D's IsLimbGone probe reuses P2C's `ProbeMultiSlot` helper directly. To keep per-phase scoreboard accounting clean (separate `p2cFailures` from `p2dFailures` in the totalFailures rollup) the probe block uses delta-tracking: snapshot `p2cBefore`, run probe, compute `p2dFailures = p2cFailures - p2cBefore`, restore `p2cFailures = p2cBefore`. This is more boilerplate than a direct counter, but avoids refactoring `ProbeMultiSlot`'s internals and keeps the probe block to ~12 LOC including the section header.

## Known issues / open questions

- **GetVATSValueUnknown Mutagen 0.53.1 schema gap** (see § Bugs surfaced) — v2.9.x candidate when upstream Mutagen 0.54+ lands the missing override. Real consumers attempting `function: "GetVATSValueUnknown"` get a clean per-record write-time error today.

- **No new architectural surprises beyond GetVATSValueUnknown.** The 10 other PrimitiveOnly functions all wired clean via the existing P2C Int32 branch — kickoff's "zero bridge code changes" prediction held end-to-end for those 10. Zero non-Int32 slots surprised at extension time.

- **Boolean dispatcher branch + sub-B String functions remain deferred** to v2.9.x as documented in PLAN.md § Carry-overs entries 7 + 8. Both are first-consumer-trigger items; nothing in v2.9.0's coverage surface forces their absorption today.

## Conductor asks

None — all halt-and-report points landed cleanly:

- Halt-1 (canary trace + drift-detection clean): confirmed by Aaron with "Canary proves the architectural promise — HashSet += name suffices, dispatcher is feature-complete."
- Halt-2 (Mutagen schema gap on GetVATSValueUnknown + Option A vs B vs C): confirmed by Aaron with "A" (SKIP-with-reason + KNOWN_ISSUES schema-gap entry + CHANGELOG bonus-catch note + HashSet count 199 holds).

## Phase 2 closing summary

**Phase 2 — Bridge dispatch infrastructure + functional probes + coverage-smoke regression cells — feature-complete after P2D.**

| Sub-phase | Date | Functions wired | Branches added | Coverage-smoke new | Race-probes |
|---|---|---|---|---|---|
| P2A | 2026-04-26 | 119 (113 FLI + 6 sub-A IFormLink) | IFormLinkOrIndex<T> + IFormLink<T> | 134 | 7 |
| P2B | 2026-04-27 | 41 (Enum) | System.Enum | 45 | 3 |
| P2C | 2026-04-27 | 28 (MultiSlot incl. GetEventData absorbed) | Int32 + Single | 32 | 4 |
| P2D | 2026-04-27 | 11 (PrimitiveOnly) | (none — pure HashSet extension) | 11 | 1 |
| **Total** | — | **199** | **5 of 6 PLAN-named branches** | **222 v2.9-new** | **15** |

**Final v2.9.0 dispatcher capability surface:**
- **199 dispatcher-wired functions** in `KnownParameterizedFunctions` — the max-band Pareto Aaron locked at session start.
- **5 dispatcher branches landed**: IFormLinkOrIndex<T> (P2A) + IFormLink<T> (P2A) + System.Enum (P2B) + Int32 (P2C) + Single (P2C). The 5 branches are byte-stable across P2A → P2D; P2B/P2C/P2D added function names to the HashSet that route through whichever already-live branch their slot prop type matches (P2B Enum-only adds, P2C MultiSlot composes existing branches per-slot, P2D Int32-only reuses P2C).
- **Boolean dispatcher branch deferred** to v2.9.x as the single design-vs-implementation gap (PLAN.md § A names six branches; v2.9.0 ships five). Zero v2.9.0 in-scope functions need it; first v2.9.x consumer trigger lands the branch + cell + name simultaneously.
- **6 sub-B Condition functions deferred** to v2.9.x (String-typed `VariableName`/`GraphVariable` slots — `GetGraphVariableFloat`, `GetGraphVariableInt`, `GetQuestVariable`, `GetScriptVariable`, `GetVMQuestVariable`, `GetVMScriptVariable`). Routing requires either accept-any-string operator surface decision or MCP shape for Papyrus-introspection round-trip; defer until first real consumer.
- **219 NoParam Condition functions remain in-scope-no-op** — they accept parameterless invocation as v2.7.1+ behavior; supplying `parameters` for a NoParam function fires the natural slot-name-not-found path or the out-of-scope check. Back-compat preserved.
- **6 total SKIPs across the 382-cell smoke matrix** are accounted for: 4 v2.8 baseline carryovers (Outfit/Spell VMAD x2, CellBinaryOverlay override, ESL master interaction deferred to live), 1 P2C UnknownConditionData round-trip reclassification artifact, 1 P2D GetVATSValueUnknown Mutagen 0.53.1 schema gap. All documented under KNOWN_ISSUES.md + the relevant phase handoffs.
- **Bridge SHA progression:** 2A `e7…` (post-2A) → 2B `69f6…` → 2C `a96c…` → **2D `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e`** (final P2 SHA). Phase 5 produces the canonical v2.9.0 ship SHA via `dotnet publish` later.

**Phase 2 architecture stability claim verified:** the dispatcher's per-function-extension model scaled down to "11 names + cells, no bridge code" for P2D — proves the generic-by-slot-type design (per-slot dispatch via reflection PropertyType match) lets future v2.9.x point releases extend coverage by HashSet additions only, with zero dispatcher changes, as long as the slot prop type matches one of the 5 already-live branches.

## Preconditions for Phase 3

| Precondition | State |
|---|---|
| Bridge built + dispatcher feature-complete + ready for live workflow scenarios | ✓ — Phase 2 complete; SHA `2e3a1094…f8293975e` is the final P2 SHA. |
| Live install at v2.9.0 | ✗ — Live install at `<live>` (`E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`) is currently v2.8.0 (mo2_ping confirmed at session start). **Phase 3 cannot run workflow scenarios against the live bridge until v2.8.0 → v2.9.0 sync lands.** Conductor's Phase 3 kickoff prompt owns the sync directive (per PLAN.md § Phase 3 step 1: "Verify live install + MCP server. mo2_ping returns version: 2.9.X. If disconnected or wrong version: halt and ask conductor."). Sync involves copying bridge .exe + Python files from repo → `<live>/` and a full MO2 process restart. |
| Layer 3 scenarios pre-spec'd in MATRIX.md | ✓ — MATRIX.md § Layer 3: Scenario 3.1 (dialog GetIsID topic gating, INFO carrier) + Scenario 3.2 (perk HasPerk/HasSpell prerequisite gate, PERK carrier). Use-case descriptions + assertion templates pre-spec'd; live FormIDs picked at execution time. |
| MultiSlot composition path verified end-to-end | ✓ — P2C's GetEventData 3-slot canary + GetStageDone canonical composition probe + P2D's per-slot Int32 dispatch all PASS. Phase 3 can rely on per-slot dispatch under realistic dialog/perk conditions. |
| GetVATSValueUnknown Mutagen schema gap forward-carry | ✓ — not applicable to Phase 3 scenarios (Layer 3 targets GetIsID + HasPerk/HasSpell, not GetVATSValueUnknown). No real-patcher Authoria-style mod calls GetVATSValueUnknown. |

## Files of interest for Phase 3

| Path | Why |
|---|---|
| `dev/plans/v2.9.X_condition_parameters/MATRIX.md` § Layer 3 | Pre-spec'd Scenario 3.1 (dialog GetIsID) + Scenario 3.2 (perk HasPerk/HasSpell) with use-case descriptions + assertion templates. Phase 3 picks live FormIDs at execution time. |
| `dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` § Floor + Stretch | Per-floor-function slot signatures (slot name + IFormLinkOrIndex<T> generic-T) for GetIsID/HasPerk/HasSpell — direct reference for Phase 3 patcher calls. |
| `tools/mutagen-bridge/PatchEngine.cs:1608ff` (`BuildCondition` entry-point) | In-process verification reference if Phase 3 needs to debug a live-patcher call against the dispatcher's branch routing. |
| `<live>/` — live install state | Phase 3 reads via `mo2_create_patch` against this. Pre-flight check: build a single `mo2_create_patch` call exercising one in-scope function with `parameters` — if bridge errors with "no such field 'parameters'" or accepts but writes default-zero, the live bridge is stale (v2.8.0 still). Conductor handles sync directive in Phase 3 kickoff. |
| `KNOWN_ISSUES.md` § Condition-parameter coverage (v2.9.0) | Authoritative source for what's in-scope (199 functions across 5 branches) + what's deferred (Boolean, sub-B, NoParam) — Phase 3 frames bug triage against this. |
| `KNOWN_ISSUES.md` § Patching write surface | GetVATSValueUnknown Mutagen 0.53.1 schema gap entry for forward-carry awareness (not expected to surface in Phase 3 scenarios but documented for completeness). |
| `dev/plans/v2.9.X_condition_parameters/PHASE_2C_HANDOFF.md` § Bugs surfaced | P2C's UnknownConditionData round-trip reclassification artifact — Phase 3's harness shape doesn't trigger it (live workflow scenarios use named functions whose CTDA codes round-trip cleanly), documented for awareness. |

## Acceptance — Phase 2D (per kickoff)

- ✓ 11 PrimitiveOnly function names added to `KnownParameterizedFunctions` sourced verbatim from scratch lines 1532–1554. Final HashSet size: **199**.
- ✓ Bridge builds 0 warnings / 0 errors; new SHA `2e3a1094…f8293975e` differs from 2C's `a96c9041…0668c188`.
- ✓ Inline canary (Test 371): GetIsAliasRef pipes through dispatcher; readback proves Int32 slot landed (NOT default 0).
- ✓ Coverage-smoke total: 371 + 11 P2D new = **382 cells**. PASS counts: 156 v2.8 + 134 P2A + 45 P2B + 31 P2C + 10 P2D = **376 PASS**, **6 SKIPs** (4 v2.8 baseline carryovers + 1 P2C UnknownConditionData artifact + 1 NEW P2D GetVATSValueUnknown Mutagen schema gap), **0 FAIL**.
- ✓ All 371 v2.9 P2A/P2B/P2C / v2.8 baseline cells stay green — drift-detection diff confirms HashSet + doc-comment-only changes (2 hunks in PatchEngine.cs).
- ✓ Schema description, KNOWN_ISSUES, CHANGELOG appended with P2D content + Phase-2-feature-complete narrative updates.
- ✓ Handoff under 400 lines (this file).
- ✓ Phase 2 closing summary captured (above).
- ✓ Phase 3 prerequisite explicitly captured: live install at v2.8.0 needs sync to v2.9.0 before workflow scenarios run; conductor's Phase 3 kickoff handles the sync directive.
- ✓ Bridge SHA progression captured (2A → 2B → 2C → 2D = final P2 SHA; Phase 5 produces canonical ship SHA via `dotnet publish`).

## End-of-phase ritual

Per kickoff:
1. ✓ Final state matches acceptance criteria.
2. ✓ Handoff written per template (this file).
3. ✓ Did NOT write Phase 3's kickoff prompt — conductor owns that, including the live-sync directive.
4. ⏳ Force-add new file (next): `PHASE_2D_HANDOFF.md`.
5. ⏳ Push the double-commit chain (next): work commit + hash-record commit.
