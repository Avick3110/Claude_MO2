# Phase 1 Handoff — ConditionData inventory probe + Pareto proposal

**Phase:** 1
**Status:** Complete — Pareto lock A (max-band) received via conductor relay; MATRIX update landed in same session; ready for commit + push
**Date:** 2026-04-26
**Session length:** ~3.5h
**Commits made:** `6944372` (work) + this hash-record commit
**Live install synced:** No (Phase 1 is probe + audit doc + MATRIX update only; live untouched at v2.8.0)

## What was done

- **`tools/race-probe/Program.cs` extended** with v2.9 P1 inventory section (lines 1488–1747 in this commit, ~260 LOC). New section enumerates every concrete `Mutagen.Bethesda.Skyrim.*ConditionData` subclass via reflection, applies a dynamic base-slot filter (walks `DeclaringType` against `typeof(ConditionData)`), applies a CTDA padding-slot filter (`p.Name.Contains("Unused")`), categorizes by parameter shape (NoParam / Enum / FormLinkOrIndex / MultiSlot / PrimitiveOnly / Exotic), and dumps per-shape full slot signatures + floor/stretch detail + Exotic detail + 4 architectural-surprise anchors (PLAN-vs-dynamic skip-list diff, GetIsID anchor, GetEventData re-triage anchor, padding-pattern statistics).
- **Probe artifact captured:** `<workspace>/scratch/v2.9-phase-1-inventory.txt` (1622 lines). Exit 0 — 0 v2.7.1 + 0 v2.8 P1 + 0 v2.9 P1 audit failures. Authoritative source-of-truth for Phase 2's coverage-smoke construction.
- **`CONDITIONS_AUDIT.md` written** (NEW — ~280 lines). Mirrors `EFFECTS_AUDIT.md`'s role for v2.8.0. Captures inventory totals, per-shape categorization, six architectural surprises (see § Architectural surprises below) with Phase 2 plan-amend implications, sub-A/sub-B/GetEventData triage decisions, floor+stretch slot signatures (with `Object` substituted for `Reference` per ARCH NOTE), error-template confirmation, Pareto framing, and Phase 2 sub-session split proposal.
- **MATRIX.md updated** post-lock per conductor closeout direction (Pareto lock A relayed mid-session). Layer 1.P fully repopulated with per-shape sections (FormLink 119 / Enum 41 / MultiSlot 28 / PrimitiveOnly 11 / NoParam 219-no-op reference); Layer 1.D.in-scope landed with 6 representative rows per shape (bulk negatives Phase 2 generates programmatically); Layer 1.D.50 names `GetVMScriptVariable` (sub-B representative); Layer 1.D.52 → SKIP-with-reason (max-band absorbed all routable shapes); new Layer 1.D.54 added (NoParam-with-bogus-slot, confirms NoParam-NOT-in-KnownParameterizedFunctions design); Layer 2.01 names `GetStageDone` explicitly as canonical multi-slot; Layer 4.slot.03 example updated `Reference` → `Object`; Total assertion count table fully repopulated (65 matrix rows / ~597 harness cells); Phase fill-in checklist marked complete with deviation notes for items routed to Phase 2A's `[v2.9 plan-amend]` first commit. MATRIX.md grew 316 → 389 lines.
- **No PLAN.md edits.** Per conductor side-question confirmation, all PLAN.md/MATRIX.md content discrepancies are surfaced in CONDITIONS_AUDIT.md with enough fidelity that Phase 2A's first commit (a `[v2.9 plan-amend]` per v2.8.0 precedent at `407c5e3` / `ca62e44`) writes itself from the audit.

## Verification performed

- **Race-probe build:** clean (0 warnings, 0 errors). Multiple iterations across the session — every iteration clean.
- **Race-probe run:** exit 0. 1622 lines captured. 0 v2.7.1 audit failures + 0 v2.8 P1 + 0 v2.9 P1.
- **Inventory totals (probe ground truth):**
  - 424 concrete *ConditionData types (vs v2.8.0's research-note estimate of ~157 — probe is authoritative for v2.9 + future v2.9.x).
  - Per-shape: NoParam 219, FormLinkOrIndex 113, Enum 41, MultiSlot 27, PrimitiveOnly 11, Exotic 13. Sum 424 ✓.
  - 1436 *Unused* padding slots filtered across 424 functions; useful-slot histogram: 219 zero-useful, 171 one-useful, 32 two-useful, 2 three-useful (GetEventData + Unknown).
- **Architectural-surprise anchors all surfaced and adjudicated mid-session via conductor relays:**
  - GetIsID anchor: `Reference` is BASE (RunOnType: Reference mode); `Object` is GetIsID's actual function-specific slot (`IFormLinkOrIndex<IReferenceableObjectGetter>`). Surfaced run 1; conductor adjudicated run 2; clean ARCH NOTE in run 3.
  - GetEventData re-triage anchor: both nested `EventFunction` (5 values) and `EventMember` (8 values) are standard `System.Enum` subclasses. GetEventData absorbs into v2.9.0 as 3-slot MultiSlot (2 enums + 1 IFormLink covered by sub-A).
  - PLAN-vs-dynamic skip-list diff: 4-of-6 PLAN names wrong (Function, Unknown1/2/3 absent or function-specific); 3 base props missed (RunOnTypeIndex, UseAliases, UsePackageData). Captured.
  - CTDA padding pattern (universal-with-exceptions): documented; 24 non-uniform exceptions surfaced (GetEventData, Unknown, 22 GetVATSValue*).
- **Mutagen-bridge SHA snapshot:** preserved at v2.8.0 ship's `f998c4e0…6c8bb04`. Phase 1 doesn't touch the bridge; recorded for traceability.

## Bugs surfaced

Phase 1 doesn't run bridge code, so no behavior bugs. **Six architectural surprises** with Phase 2 plan-amend implications (full detail in CONDITIONS_AUDIT.md § Architectural surprises):

1. PLAN.md § Architecture B uses `Reference` as GetIsID's slot — actually `Object`. Plan-amend: PLAN.md § Architecture B example, MATRIX.md scenario 3.1 assertion, Phase 2 dispatcher schema description, CHANGELOG entry.
2. PLAN.md § Phase 1 step 2 static skip list is wrong (4-of-6 names). Plan-amend: drop static list, point at this audit's dynamic detector.
3. CTDA padding pattern (1436 *Unused* slots filtered universally) — encode as `IsPaddingSlot(p) := p.Name.Contains("Unused")` in dispatcher; Phase 2 footgun-guard recommendation: explicitly reject `*Unused*` slot names in user `parameters` maps.
4. `GetActorValuePercentage` doesn't exist; `GetActorValuePercent` is canonical. Plan-amend: drop from PLAN.md floor-AV list.
5. `IItemOrListGetter` is a Mutagen union interface — IS routable through existing Global-handler pattern. Documentation-only (saves Phase 2 from re-discovering).
6. 424-vs-157 count discrepancy. CONDITIONS_AUDIT.md is authoritative going forward.

Plus the `Unknown` ConditionData (3 useful slots: `Function: Condition+Function` enum + `ParameterOne: Int32` + `ParameterTwo: Int32`) — Mutagen's forward-compat catch-all for unknown CTDA function codes. In-scope automatically as MultiSlot.

## Deviations from plan

- **Categorizer required mid-halt re-spec.** Probe run 1 surfaced two halt-worthy issues: GetIsID's `Reference` slot is base-not-function-specific (PLAN.md § Architecture B example wrong) AND every function lands in Exotic because Mutagen's universal CTDA-4-parameter padding shape uses non-routable `String` slots. Conductor mid-halt adjudicated Option C (apply `*Unused*Parameter*` filter + record padding pattern in audit) + drop GetActorValuePercentage + capture surprises in CONDITIONS_AUDIT.md (don't edit PLAN.md). Probe re-extended, re-built, re-ran clean. **No deviation from PLAN.md as written** — the deviations are the conductor-adjudicated surprises captured in the audit for Phase 2's plan-amend.
- **Pre-amble work plan amended after conductor pre-execution review.** Conductor flagged PLAN.md's static skip list as a real risk (would have misclassified GetIsID as NoParam silently) and the namespace exact-match as a minor watch-out. Both refinements landed in the probe code before first run — caught the GetIsID architectural surprise on run 1 as designed.
- **Probe extended with full per-shape detail dump** (~420 extra lines in scratch). Beyond PLAN.md's "per-shape categorization" requirement, this provides Phase 2 with every in-scope function's slot signature in one reference file — avoids Phase 2 needing to re-extend the probe.

## Known issues / open questions

- **Pareto lock pending conductor relay.** See § Conductor asks below. Phase 2A's kickoff is gated on this.
- **MATRIX.md update queued.** Either Phase 1 closing task post-lock, OR Phase 2A's first step. Conductor's call based on session-cadence convenience.
- **Phase 2 footgun-guard decision.** Should the dispatcher explicitly reject `parameters` keys whose names match `*Unused*Parameter*`? CONDITIONS_AUDIT.md § Error template recommends YES (one-line guard in RouteParameterSlot) — typo'd intentional slot name could otherwise land on padding silently. Phase 2A decides; documenting here so it's not lost.
- **Sub-B deferral list** (6 functions) needs to land in PLAN.md § Carry-overs as part of Phase 2A's plan-amend commit. Listed in CONDITIONS_AUDIT.md § Sub-B deferral.
- **NoParam handling:** dispatcher should NOT include 219 NoParam functions in KnownParameterizedFunctions (relies on natural slot-name-not-found error path). Documented in CONDITIONS_AUDIT.md § NoParam handling. Phase 2A confirms.

## Conductor asks (RESOLVED)

**Pareto lock = Option A (max-band).** Relayed by conductor 2026-04-26. Sub-A absorbed via IFormLink<T> branch; GetEventData absorbed as 3-slot MultiSlot; sub-B (6 functions) deferred to v2.9.x; NoParam (219) in-scope-no-op (NOT in KnownParameterizedFunctions). Phase 2 split locked at 4 sub-sessions (2A infra+FLI+sub-A → 2B Enum → 2C MultiSlot+GetEventData → 2D PrimitiveOnly). Footgun-guard for `*Unused*Parameter*` slot names lands in 2A. Original ask preserved below for traceability:

```
CONDUCTOR ASK
Phase: 1
Topic: v2.9.0 in-scope function set — Pareto lock
Context:
  - Inventory total: 424 concrete *ConditionData types in Mutagen 0.53.1 (vs v2.8.0 research-note's ~157 — probe is authoritative).
  - Per-shape distribution (post-Unused-filter): NoParam 219, FormLinkOrIndex 113, Enum 41, MultiSlot 27, PrimitiveOnly 11, Exotic 13.
  - Exotic 13 triaged: 6 absorb under sub-A (IFormLink<T> branch in RouteParameterSlot, ~30min Phase 2 cost, no new operator surface — fits pre-auth envelope), 1 absorbs (GetEventData — both nested EventFunction/EventMember are System.Enum, becomes 3-slot MultiSlot), 6 defer to v2.9.x (sub-B — String-typed VariableName/GraphVariable slots need accept-any-string operator surface decision).
  - Floor (PLAN.md § Phase 1): GetIsID + GetInFaction + GetInCell + HasMagicEffect + HasPerk + HasSpell + GetIsRace + ActorValue family carryover (GetActorValue + GetBaseActorValue + GetActorValuePercent — GetActorValuePercentage doesn't exist).
  - Stretch (PLAN.md): GetItemCount + IsInList + WornHasKeyword + GetEquipped.
  - Aaron has signalled aggressive Pareto guidance + slot-type expansion pre-authorized within RouteParameterSlot envelope.
Question: Lock the in-scope function set for v2.9.0?
Suggested options:
  A: max-band — all 113 FormLinkOrIndex + all 41 Enum + all 27 MultiSlot + all 11 PrimitiveOnly + 6 sub-A + GetEventData = 199 dispatcher-wired functions; plus 219 NoParam in-scope-no-op = 418 total in-scope. Rationale: per Aaron's "ship the full routable Condition-parameter surface in v2.9.0" directive; the dispatcher is generic so per-function cost is purely "extend KnownParameterizedFunctions + add coverage-smoke cells" once 2A's infrastructure lands. Estimated 4 Phase 2 sub-sessions (see split proposal below).
  B: moderate aggressive — ~60 functions (top frequency-weighted slice across FormLinkOrIndex + Enum + MultiSlot). Rationale: escape valve if audit-time per-function review surfaces unexpected per-shape complexity; ~2 sub-sessions.
  C: floor + stretch only — ~14 functions per PLAN.md baseline. Rationale: most conservative; preserves Phase 2 scope at PLAN-original; 1 sub-session.
Default if no response in 24h: A (Aaron's pre-relayed pick).
Recommendation: A — locked at conductor mid-halt relay. Audit found no per-shape complexity surprises that would warrant scope contraction; the 199-function set is uniform single-or-multi-slot reflection writes through one generic mechanism.
```

### Phase 2 sub-session split proposal (informational — conductor uses this to spawn 2A/2B/2C/2D kickoffs in order)

**Recommended split: 4 sub-sessions, infra-first by parameter shape.**

| Sub-phase | Scope | Wired | Coverage-smoke +cells | Why grouped |
|---|---|---:|---:|---|
| **2A** | Dispatcher infrastructure (RouteParameterSlot, ConditionEntry.Parameters, KnownParameterizedFunctions, BuildCondition integration, Models.cs schema) + IFormLink<T> branch + all FormLinkOrIndex + sub-A | **119** | ~120–240 | Both groups go through Global-handler pattern; sub-A is one-line extension; biggest sub-session because infra costs amortize. **Bumps version to v2.9.0.** First commit is `[v2.9 plan-amend]` (per v2.8.0 precedent at 407c5e3 / ca62e44) folding CONDITIONS_AUDIT.md surprises into PLAN.md + MATRIX.md. |
| **2B** | All Enum (carryover ActorValue + 40 others) | **41** | ~80 | Pure "extend KnownParameterizedFunctions table + add coverage-smoke cells" — Enum.Parse path already in v2.8.0's actor_value handler. |
| **2C** | All MultiSlot + GetEventData absorbed | **28** | ~60–80 | Tests dispatcher per-slot composition; GetEventData (3 mixed-shape slots) is the most complex case and exercises the dispatcher's per-slot routing fully. |
| **2D** | All PrimitiveOnly | **11** | ~22 | Direct primitive conversion path; smallest sub-session, good closer. |

**Total:** 199 wired functions. Coverage-smoke baseline: 160 v2.8.0 + ~290 v2.9 = ~450 cells.

**Alternative 5-session split** (if conductor prefers smaller 2A): split 2A into 2A-infra-with-canaries + 2B-rest-of-FormLinkOrIndex. Recommend NOT splitting — infra and the largest shape group co-exercise the dispatcher under load, and the canary split adds session-overhead without commensurate risk reduction.

## Preconditions for Phase 2A

| Precondition | State |
|---|---|
| Pareto lock from Aaron via conductor relay | ✓ Option A (max-band): 199 wired + 219 NoParam in-scope-no-op = 418 total |
| MATRIX.md updated with in-scope function rows | ✓ Layer 1.P fully repopulated; Layer 1.D.in-scope representative rows; 1.D.50 names sub-B; 1.D.52 SKIP; new 1.D.54; 2.01 names GetStageDone; 4.slot.03 uses Object; Total count repopulated; checklist marked complete |
| CONDITIONS_AUDIT.md written with full audit + sub-A/sub-B/GetEventData decisions + Phase 2 split | ✓ |
| Race-probe inventory artifact in scratch (slot signatures source-of-truth for coverage-smoke) | ✓ 1622 lines |
| PatchEngine.cs:1608 BuildCondition working-precedent (actor_value + Global handlers) understood | ✓ — captured in CONDITIONS_AUDIT.md § References |
| v2.8.0 baseline coverage-smoke 160 cells assumed green | ✓ presumed (last verified at v2.8.0 ship; Phase 2A first action should re-confirm) |
| `[v2.9 plan-amend]` first-commit pattern from v2.8.0 understood (407c5e3 / ca62e44) | Conductor passes via Phase 2A kickoff |
| Phase 2 sub-session split locked | ✓ 4 sessions per handoff § Conductor asks (2A infra+FLI+sub-A → 2B Enum → 2C MultiSlot+GetEventData → 2D PrimitiveOnly) |

## Files of interest for Phase 2A

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` | **Source-of-truth** — six architectural surprises with plan-amend implications, sub-A/sub-B/GetEventData triage decisions, floor+stretch slot signatures (corrected for Object), error-template confirmation, footgun-guard recommendation, Phase 2 split proposal |
| `<workspace>/scratch/v2.9-phase-1-inventory.txt` (1622 lines) | **Per-function slot signatures** for the 199-function in-scope set; Phase 2's coverage-smoke harness construction reads from here. Per-shape full detail at lines 1136–1554 |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs:1608` `BuildCondition` | Working-precedent the dispatcher generalizes — `actor_value` handler at 1631–1645 (Enum.Parse pattern), `Global` handler at 1657–1667 (FormLinkOrIndex<T> ctor pattern with parent + FormKey args) |
| `Claude_MO2/tools/mutagen-bridge/Models.cs` | `ConditionEntry` definition; new `Parameters: Dictionary<string, JsonElement>?` field goes here per PLAN.md § Phase 2 step 3 |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/PLAN.md` § Phase 2 + § Architecture A/B/C | Phase 2A's working spec; plan-amend commit corrects the surprises CONDITIONS_AUDIT.md surfaces |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/MATRIX.md` § Phase fill-in checklist | Cell rows post-Pareto-lock; either Phase 1 closing task or Phase 2A first step |
| `Claude_MO2/tools/race-probe/Program.cs` lines 1488–1747 | v2.9 P1 inventory section; Phase 2 may extend with functional probes per in-scope function (one round-trip Mutagen-direct test per function — PLAN.md § Phase 2 step 7) |
| `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` | Audit-doc structural sibling — CONDITIONS_AUDIT.md mirrors its narrative-with-evidence pattern |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_4_HANDOFF.md` (if it exists) — actor_value handler land + bonus-catch | Reference for the Enum-shape working precedent |

## Acceptance — Phase 1 (per kickoff prompt)

- ✓ Inventory probe runs to completion; CONDITIONS_AUDIT.md captures total + per-shape categorization + per-floor/stretch slot signatures (with `Object` correction) + architectural surprises + error-template confirmation + Pareto framing + Phase 2 split.
- ✓ Pareto proposal written in handoff § Conductor asks (CONDUCTOR ASK format with options A/B/C, Aaron's pick recommended, default named) — RESOLVED: Option A locked.
- ✓ Race-probe builds clean (0 warnings, 0 errors).
- ✓ MATRIX.md updated with in-scope function cells per Phase fill-in checklist (closing task post-lock — Layer 1.P / 1.D / 2.01 / 4.slot.03 / Total count / checklist all landed).
- ✓ Handoff under 400 lines.

## End-of-phase ritual

Per kickoff prompt:
1. ✓ Final state matches acceptance criteria.
2. ✓ Handoff written per template (this file) + updated post-lock to reflect MATRIX-landed state.
3. ✓ Did NOT write Phase 2A's kickoff prompt — conductor owns that after this handoff lands.
4. ⏳ Force-add files (next): CONDITIONS_AUDIT.md, PHASE_1_HANDOFF.md, MATRIX.md, PHASE_1_KICKOFF_PROMPT.md (if not already tracked); race-probe Program.cs.
5. ⏳ Work commit `[v2.9 P1] Condition-parameter inventory probe + Pareto max-band lock + MATRIX update` + hash-record commit + push (next).
