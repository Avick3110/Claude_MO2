# Phase 2D Kick-off — PrimitiveOnly closer (11 wired) + Phase 2 complete

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 2D executor** for the v2.9.0 Claude_MO2 release — the closer for Phase 2. Your job is to wire the final 11 PrimitiveOnly Condition functions to `KnownParameterizedFunctions`, add Layer 1.P.PrimitiveOnly cells to coverage-smoke, append docs, and write the handoff that marks v2.9.0's dispatcher feature-complete (199 functions wired, max-band Pareto fully landed). **2D is the smallest sub-session by far** — pure bulk wiring on the Int32 branch 2C already landed; zero bridge code changes.

## Context (read this once, don't search for history)

v2.8.0 shipped `419a719`. v2.9.0 in flight: Phases 0/1 + 2A + 2B + 2C complete (origin/main HEAD `45e64ad`). 2A laid the dispatcher (RouteParameterSlot + ConditionEntry.Parameters + KnownParameterizedFunctions + DSL-ambiguity + out-of-scope + footgun-guard) and wired 119 functions. 2B added the Enum branch and 41 functions. 2C added Int32 + Single branches and 28 MultiSlot functions including GetEventData. Bridge SHA at 2C = `a96c90410b68cd8e338c11a4540c5b8908399666d6255320fc8f3b8d0668c188` — your drift-detection baseline. Coverage-smoke at 371 cells, 0 FAIL, 5 SKIP (4 v2.8 baseline carryovers + 1 P2C UnknownConditionData round-trip artifact). KnownParameterizedFunctions = 188 names; 2D adds the final 11 PrimitiveOnly to reach **199 — the max-band Pareto Aaron locked at session start**. Phase 2D's contribution to the dispatcher: **zero bridge code**. The Int32 branch (which all 11 PrimitiveOnly functions need) is already live since 2C; all 11 are real CTDA functions, not catch-all types like Unknown, so no round-trip artifact carries forward.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `45e64ad…` (Phase 2C hash-record commit).
   - Working tree clean.
   - `mo2_ping` returns `version: "2.8.0"` (live untouched until Phase 5).
   - Bridge under your repo IS v2.9.0 P2C (built in 2C; SHA `a96c90410b68cd8e338c11a4540c5b8908399666d6255320fc8f3b8d0668c188`). Confirm via `sha256sum tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`. If SHA differs, halt — dirty build inheritance.
2. **Read these files in full, in order:**
   - `dev/plans/v2.9.X_condition_parameters/PLAN.md` § Session-start ritual + § Phase 2 + § Architecture A/B/C + § Handoff template + § Communicating with the conductor.
   - `dev/plans/v2.9.X_condition_parameters/PHASE_2C_HANDOFF.md` — most-recent state. Forward-carry to 2D in § Preconditions: Int32 branch already in place + all 11 PrimitiveOnly use Int32 only.
   - `dev/plans/v2.9.X_condition_parameters/MATRIX.md` § Layer 1.P.PrimitiveOnly + § Layer 1.D representative + § Total assertion count.
3. **Skim, don't memorize:**
   - `tools/mutagen-bridge/PatchEngine.cs` — current `RouteParameterSlot` (post-2C): FLI + IFormLink<T> + Enum + Int32 + Single branches all landed; Boolean deferred. The catch-all error message lists 5 covered branches. **2D does NOT modify the dispatcher** — purely adds to `KnownParameterizedFunctions`.
   - `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1532–1554 — PrimitiveOnly per-function full slot signatures (11 functions, all Int32-typed slots per 2C inventory). Source of truth for the 11 names.
   - `tools/coverage-smoke/Program.cs` end of file — 2C's `RunMultiSlotDispatcherCell` is the latest helper; for PrimitiveOnly's single-Int32-slot pattern you can either (a) reuse it with a single-slot-tuple input, or (b) write a tiny `RunPrimitiveOnlyDispatcherCell` that's even simpler. Both are fine; pick whichever is cleaner against 2C's helper shape.

## Conductor decisions inherited (locked — do not re-litigate)

1. **Version slug = `v2.9.0`.** No re-bump.
2. **No plan-amend.** None needed.
3. **No bridge code changes.** Int32 branch live since 2C; 11 PrimitiveOnly all use Int32 only. Bridge diff scoped to `KnownParameterizedFunctions` HashSet (+11 names + comment header). Other bridge code byte-identical to 2C baseline `a96c9041…0668c188`. If diff shows changes elsewhere, halt.
4. **Boolean stays deferred.** No PrimitiveOnly function uses Boolean. Boolean branch lands in v2.9.x when a real consumer surfaces. KNOWN_ISSUES already documents the deferral footnote (landed in 2C).
5. **No new SKIPs expected.** All 11 PrimitiveOnly are real CTDA functions; no UnknownConditionData-style round-trip reclassification artifact applies. If a 2D cell SKIPs unexpectedly, halt.
6. **Race-probe is OPTIONAL for 2D.** 2C's GetStageDone probe already exercises the Int32 branch (FLI + Int32 composition). A pure-PrimitiveOnly Int32 probe is forms-completeness rigor but not strictly needed since the dispatcher branch is the same code path. **Author's call**: include 1 representative PrimitiveOnly race-probe (~5 LOC) for archival completeness, OR skip and note the rationale in the handoff. Either is acceptable.

## Phase 2D deliverables

| # | Item | Files |
|---|---|---|
| 1 | `KnownParameterizedFunctions` += 11 PrimitiveOnly names sourced verbatim from scratch lines 1532–1554 | `tools/mutagen-bridge/PatchEngine.cs:1879ff` (the HashSet block) |
| 2 | Layer 1.P.PrimitiveOnly positive cells (1 canary + 10 bulk via helper) | `tools/coverage-smoke/Program.cs` |
| 3 | Layer 1.D PrimitiveOnly representative: bad Int32 type-coercion (e.g. supplying string for Int32 slot) — OR confirm 2C's Test 369 Int32-coercion negative already covers PrimitiveOnly's path; if so, SKIP duplicate | `tools/coverage-smoke/Program.cs` |
| 4 | _Optional_: 1 race-probe v2.9 P2D representative | `tools/race-probe/Program.cs` |
| 5 | Schema description: lift PrimitiveOnly from "v2.9.0 candidate" / Int32 from "via 2C" to comprehensive list; mark dispatcher 5/6 branches landed; document Boolean as the only design-vs-implementation gap | `mo2_mcp/tools_patching.py` |
| 6 | KNOWN_ISSUES.md update: header `(v2.9.0 P2A + P2B + P2C)` → `(v2.9.0)` (drop sub-phase qualifier — release is feature-complete after 2D); lift PrimitiveOnly from gap-list; restate the v2.9.0 final scope (199 wired + 219 NoParam in-scope-no-op + 6 sub-B deferred + Boolean branch deferred) | `KNOWN_ISSUES.md` |
| 7 | CHANGELOG: append 2D section under existing `## v2.9.0 — TBD` entry; rewrite the Top brief paragraph to reflect Phase 2 complete and the final 199-function dispatcher scope | `mo2_mcp/CHANGELOG.md` |
| 8 | `PHASE_2D_HANDOFF.md` per template — **MUST capture**: Phase 2 complete state, full v2.9.0 dispatcher scope summary, Phase 3 live-sync precondition (live install needs to move from v2.8.0 to v2.9.0 before Phase 3 runs scenarios) | `<plan>/PHASE_2D_HANDOFF.md` |

## Double-commit cadence (no plan-amend, no version bump)

1. **Work commit:** `[v2.9 P2D] PrimitiveOnly closer (11 functions wired) + Phase 2 dispatcher feature-complete`. Bridge code (HashSet only) + coverage-smoke + race-probe (if included) + tools_patching.py + CHANGELOG + KNOWN_ISSUES. Push.
2. **Hash-record commit:** `[v2.9 P2D] Handoff: record commit hash <work-hash>`. PHASE_2D_HANDOFF.md only. Push.

End each subject line with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line bodies.

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 2D executor" + confirm session-start state (HEAD + bridge SHA + tree clean).
2. Recap deliverables in your own words.
3. Propose work order. Default sequence: **read scratch lines 1532–1554 for the 11 PrimitiveOnly function names → KnownParameterizedFunctions += 11 names → bridge build clean → ONE PrimitiveOnly canary smoke → halt-and-report → bulk wire 10 + Layer 1.D representative + (optional race-probe) → schema/KNOWN_ISSUES/CHANGELOG → coverage-smoke run end-to-end → drift-detection diff → handoff.**
4. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **After PrimitiveOnly canary smokes through end-to-end** (BEFORE bulk wiring of remaining 10): pick a recognizable canary — `GetItemCount` is a reasonable choice if it's PrimitiveOnly (it may actually be FLI per scratch — pick whatever the audit confirms; ideally something with a real-patcher use case). Show Aaron the trace.
- **After coverage-smoke green**: confirm counts (~371 + ~12-15 P2D = ~383-386 cells, 0 FAIL, 5 SKIPs persisting unchanged). Drift-detection diff confirming changes scoped to HashSet only.

## Mandatory halt-and-report triggers (any → halt immediately)

- Any of the 371 v2.9 P2A/P2B/P2C / v2.8 baseline cells starts failing.
- Bridge build fails (warnings or errors).
- A function categorized as PrimitiveOnly turns out to have a non-Int32 slot at extension time.
- Bridge diff shows changes outside `KnownParameterizedFunctions` HashSet.
- Bonus-catch surfaces > 1h additional or new operator surface.
- An UnknownConditionData-style round-trip artifact surfaces on a PrimitiveOnly function (per 2C handoff this isn't expected, but escalate if it does).

## Acceptance criteria (Phase 2D complete = Phase 2 complete)

- 11 PrimitiveOnly function names added to `KnownParameterizedFunctions` sourced verbatim from scratch lines 1532–1554. Final HashSet size: **199**.
- Bridge builds 0 warnings / 0 errors; new SHA captured (must differ from 2C's `a96c9041…0668c188`).
- Inline canary: PrimitiveOnly function pipes through dispatcher; readback proves Int32 slot landed (NOT default 0).
- Coverage-smoke total: 371 + ~12-15 P2D new = ~383-386 cells. PASS counts confirm. **5 SKIPs persist unchanged** (4 v2.8 baseline + 1 P2C UnknownConditionData artifact). 0 FAIL.
- All 371 v2.9 P2A/P2B/P2C / v2.8 baseline cells stay green — drift-detection diff shows HashSet-only changes.
- Schema description, KNOWN_ISSUES, CHANGELOG appended with 2D content + v2.9.0-feature-complete narrative updates.
- Handoff under 400 lines and **explicitly captures**:
  - **Phase 2 closing summary** — 199 functions wired across 4 sub-sessions, 5 dispatcher branches landed, Boolean deferred to v2.9.x, sub-B 6 functions deferred to v2.9.x, NoParam 219 in-scope-no-op preserved.
  - **Phase 3 prerequisite**: live install at `<live>` is currently v2.8.0; Phase 3 needs sync to v2.9.0 before workflow scenarios run. Phase 3's kickoff (conductor-written) handles the sync directive.
  - Bridge SHA (final P2 SHA — Phase 5 produces the canonical ship SHA via `dotnet publish` later).

## Out of scope for 2D

- **Sub-B deferred functions** (v2.9.x — not wired).
- **Boolean dispatcher branch** (v2.9.x first-consumer trigger — explicit deferral per Stage-1 directive in 2C).
- **Live install sync** (Phase 5 — conductor handles cross-phase via Phase 3 kickoff).
- **Workflow scenarios on live** (Phase 3).
- **Plan-amend** (none expected).
- **Version bump** (2A bumped).
- **Touching CONDITIONS_AUDIT.md** (source-of-truth).
- **Modifying any other dispatcher branch** (FLI / IFormLink / Enum / Int32 / Single all stay byte-identical to 2C baseline).
- **Modifying v2.8 actor_value handler at lines 1631–1645** — back-compat sugar must stay byte-identical.

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `dev/plans/v2.9.X_condition_parameters/PHASE_2D_HANDOFF.md` per PLAN.md § Handoff template:
   - **What was done** — 11 names + cells + (optional) race-probe + docs + Phase 2 closing summary.
   - **Verification performed** — bridge build clean + new SHA + canary trace + coverage-smoke counts + drift-detection diff confirmation + Phase 2 cumulative cell count.
   - **Bugs surfaced** — none expected; document anything if surfaced.
   - **Deviations from plan** — anything different from this kickoff.
   - **Known issues / open questions** — anything Phase 3 needs to know.
   - **Conductor asks** — only if questions; format per PLAN.md.
   - **Phase 2 closing summary section** — required. Restate the v2.9.0 dispatcher's final scope (199 wired functions broken down by branch, total cells across all 4 sub-sessions, total race-probes, sub-B + Boolean deferrals, all SKIPs accounted for).
   - **Preconditions for Phase 3** — bridge built; live install at v2.8.0 needs sync to v2.9.0 (conductor-mediated via Phase 3 kickoff); Layer 3 scenarios pre-spec'd in MATRIX.md (dialog GetIsID + perk HasPerk/HasSpell with use-case descriptions; live FormIDs picked at execution time).
   - **Files of interest for Phase 3** — MATRIX.md § Layer 3 (scenarios), CONDITIONS_AUDIT.md § Floor + stretch (slot-name reference for live patcher calls), PatchEngine.cs (BuildCondition entry-point for in-process verification if needed), `<live>/` for live install state.
3. **Do NOT write Phase 3's kickoff prompt.** Conductor owns that, including the live-sync directive.
4. Force-add new files (`git add -f dev/plans/v2.9.X_condition_parameters/{PHASE_2D_HANDOFF.md,PHASE_2D_KICKOFF_PROMPT.md}`).
5. Push the double-commit chain (work + hash-record).

## What "good" looks like

- A `[v2.9 P2D]` work-commit diff that's the cleanest of all four sub-phases: PatchEngine.cs HashSet additions only (no other bridge changes), ~12-15 cells, ~5 LOC race-probe (or none), schema/CHANGELOG/KNOWN_ISSUES appends marking Phase 2 complete and the dispatcher feature-complete.
- A coverage-smoke output where the v2.9 P2D section reads as a structural sibling of P2A's bulk pattern but smaller — proves the dispatcher's per-function-extension model scales down to "11 names + cells, no bridge code."
- A handoff whose Phase 2 closing summary makes the v2.9.0 capability surface unambiguous to Phase 3's executor (and to whoever reviews the eventual GitHub release notes): 199 dispatcher-wired functions, 5 of 6 dispatcher branches, Boolean and sub-B as documented v2.9.x candidates, NoParam back-compat preserved.

---

Confirm you've identified yourself as Phase 2D executor + state-checks pass + bridge SHA matches 2C baseline, then propose your work order. The closer phase is intentionally short — your handoff signals Phase 2 complete and clears the path for Phase 3.
