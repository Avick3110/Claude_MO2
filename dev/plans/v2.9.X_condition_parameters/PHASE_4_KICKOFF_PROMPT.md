# Phase 4 Kick-off — INFO override fix + line-180 error-message DX bonus-catch

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 4 executor** for the v2.9.0 Claude_MO2 release. Your job is to fix the pre-existing `info_override_missing_in_copyasoverride` gap Phase 3 surfaced (Scenario 3.1 BLOCKED), add a DX bonus-catch for the leaky internal-type-name error message at PatchEngine.cs:180, and land regression coverage (race-probe + coverage-smoke). **Phase 4 is a small, well-contained fix-and-regress sub-session** — single bug + single bonus-catch, mirroring v2.8.0 P4's `perk_quest_adapter_subclass` shape.

## Context (read this once, don't search for history)

v2.8.0 shipped `419a719`. v2.9.0 in flight: Phases 0/1 + 2A/2B/2C/2D + 3 complete (origin/main HEAD `def8fa8`). Phase 2 landed the dispatcher feature-complete (199 wired functions across 5 of 6 PLAN-named branches). Phase 3 ran live workflow scenarios; Scenario 3.1 (dialog GetIsID on INFO) BLOCKED at patch creation by a pre-existing CopyAsOverride switch gap — the switch in `PatchEngine.cs` at lines 2508–2571 dispatches by Mutagen getter interface across ~40 record types but is **missing the `IDialogResponsesGetter` branch**, causing INFO override attempts to fall through to `_ => null` and throw "Could not create override for DialogResponsesBinaryOverlay" with a leaky internal Mutagen-overlay type name. v2.7.1 + v2.8.0 had this gap too; Phase 3 v2.9 was the first phase to attempt INFO override in workflow scenarios. **The v2.9 dispatcher is unaffected** — pre-flight canary + Scenario 3.2's 12/12 PASS proved that. Phase 3 surfaced this as a Phase 4 fix item with the bonus-catch promotion you'll execute.

## Path conventions

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` |
| `<plan>` | `<repo>\dev\plans\v2.9.X_condition_parameters\` |

Quote paths in shell commands.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `def8fa8…` (Phase 3 tightening commit; final pre-Phase-4 state).
   - Working tree clean.
   - Repo bridge SHA (post-Phase-2D, untouched by Phase 3): `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e`. Confirm via `sha256sum <repo>/tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`. If SHA differs, halt — dirty inheritance.
   - `mo2_ping` returns `version: "2.9.0"` (Phase 3 synced; live install IS at v2.9.0). Phase 4 doesn't touch live until you re-sync the bridge after the fix lands (gated on Aaron's go-ahead, mirrors Phase 3's sync ritual).
2. **Read these files in full, in order:**
   - `<plan>/PLAN.md` § Session-start ritual + § Phase 4 + § Architecture + § Handoff template + § Communicating with the conductor + § Conventions (probe-first discipline).
   - `<plan>/PHASE_3_HANDOFF.md` — bug detail + repro + four-item fix plan + bonus-catch promotion. Source-of-truth for Phase 4's scope.
   - `<repo>/dev/plans/v2.8.0_verification/PHASE_4_HANDOFF.md` — canonical Phase 4 exemplar (perk_quest_adapter_subclass fix + bonus-catches + matrix corrections + docs hygiene). Mirrors structure, not content.
3. **Skim, don't memorize:**
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:2508–2571` — `CopyAsOverride` switch. Find the existing `IDialogTopicGetter` branch (~line 2549–2551 area per Phase 3 handoff); your INFO branch goes alongside it.
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:2581+` — `TryRemoveOverride` switch. Per its doc comment ("when CopyAsOverride learns a new record type, this switch must too"), add the matching INFO branch.
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:178–180` — the throw site for the leaky type name. The bonus-catch replaces `.GetType().Name` with `RecordTypeCode(sourceRecord)` (or whatever helper exists; if no helper, decide: introduce one or inline the equivalent).
   - `<repo>/tools/race-probe/Program.cs` (end of file) — Phase 4 INFO override regression goes here. Pattern: in-process Mutagen-direct write of an INFO override + add_conditions; mirror the structure of P2A's race-probe canaries.
   - `<repo>/tools/coverage-smoke/Program.cs` (end of file, after the v2.9 P2D section) — the new `1.P.GetIsID.INFO` cell goes here using the existing `RunFLIDispatcherCell` helper or a new `RunInfoOverrideCell` shape if INFO source-record selection is too custom for the FLI helper.

## Conductor decisions (locked — do not re-litigate)

1. **Version slug = `v2.9.0`.** No re-bump. Single bridge SHA per release.
2. **No plan-amend.** Phase 4 is fix-and-regress, no plan deviation. PLAN.md § Carry-overs already names the v2.7.1 / v2.8.0 deferrals; the INFO gap was unenumerated as a carry-over (it surfaces only when INFO is exercised), so no PLAN entry is being lifted-from-carryover. Phase 4's CHANGELOG entry under `## v2.9.0 — TBD § Fixed — bridge` documents the fix.
3. **Phase 4 scope is locked at 2 items**:
   - **Item 1**: `info_override_missing_in_copyasoverride` fix — `CopyAsOverride` + `TryRemoveOverride` switch extensions for `IDialogResponsesGetter`, INFO race-probe regression, `1.P.GetIsID.INFO` coverage-smoke cell.
   - **Item 2**: line-180 error-message DX bonus-catch — replace `.GetType().Name` with `RecordTypeCode(sourceRecord)` so the user-facing error names "INFO" (or the relevant 4-char code) instead of the leaky internal `DialogResponsesBinaryOverlay` Mutagen overlay class name.
4. **No other v2.7.1 / v2.8.0 carryovers absorbed.** PLAN.md § Phase 4 conductor decisions: "items the kickoff names are in scope. Other v2.7.1/v2.8.0 carry-overs (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access) stay deferred unless the kickoff explicitly absorbs them per Aaron's call." This kickoff does not absorb them.
5. **Probe-first discipline per PLAN.md § Conventions.** Item 1's race-probe lands BEFORE the fix — proves the failure mode reproduces in-process before any code changes. Then the fix lands and the probe re-runs to confirm PASS. Same probe-then-fix-then-regress pattern v2.7.1 + v2.8.0 P4 used.
6. **Bridge SHA changes; live re-sync gated on Aaron's go-ahead.** Phase 4's bridge build produces a new SHA (different from P2D's `2e3a1094…f8293975e`). Live install at `<live>/` stays at the P2D SHA until you propose a re-sync to Aaron — but Phase 4 doesn't NEED a live re-sync to complete (coverage-smoke + race-probe are local). Phase 5 owns the canonical ship sync via `dotnet publish`. Whether to re-sync live mid-Phase-4 vs leave for Phase 5 is your call based on whether you want to verify the INFO fix end-to-end against the live install (Scenario 3.1 re-run); recommend leaving live at P2D until Phase 5's sync since Scenario 3.1 re-run is Phase 5's territory.

## Phase 4 deliverables

| # | Item | Files |
|---|---|---|
| 1a | INFO race-probe regression (probe-first; lands BEFORE fix to prove repro) | `<repo>/tools/race-probe/Program.cs` |
| 1b | `CopyAsOverride` switch: add `IDialogResponsesGetter r => patchMod.DialogResponses.GetOrAddAsOverride(r)` branch | `<repo>/tools/mutagen-bridge/PatchEngine.cs:~2549–2551` (alongside IDialogTopicGetter) |
| 1c | `TryRemoveOverride` switch: matching INFO branch per "when CopyAsOverride learns a new record type, this switch must too" doc-comment convention | `<repo>/tools/mutagen-bridge/PatchEngine.cs:2581+` |
| 1d | Re-run race-probe — Item 1a's probe must lift from FAIL to PASS post-fix | `<repo>/tools/race-probe/Program.cs` |
| 1e | `1.P.GetIsID.INFO` coverage-smoke positive cell — covers Scenario 3.1's exact shape (vanilla INFO + add_conditions GetIsID with parameters.Object → readback proves slot resolved) | `<repo>/tools/coverage-smoke/Program.cs` |
| 2 | Bonus-catch: line-180 error-message DX — replace `.GetType().Name` with a record-type-code-aware substitute (`RecordTypeCode(sourceRecord)` if a helper exists; introduce one if not, ~5 LOC) | `<repo>/tools/mutagen-bridge/PatchEngine.cs:178–180` + helper site if introduced |
| 3 | Bridge build clean; new SHA captured | bridge artifacts |
| 4 | Coverage-smoke run end-to-end — 382 baseline + 1 new (1.P.GetIsID.INFO) = 383 cells, all PASS-or-documented-SKIP, no regression | `<repo>/tools/coverage-smoke/` run output |
| 5 | CHANGELOG: append `### Fixed — bridge` bullet for `info_override_missing_in_copyasoverride` under existing `## v2.9.0 — TBD` entry; append "Changed — error message" or similar bullet for the line-180 DX improvement | `<repo>/mo2_mcp/CHANGELOG.md` |
| 6 | KNOWN_ISSUES update if any prior entry needs lifting (likely none — INFO gap was never explicitly documented as a v2.9.0 limitation, so no entry to lift) | `<repo>/KNOWN_ISSUES.md` |
| 7 | `PHASE_4_HANDOFF.md` per template | `<plan>/PHASE_4_HANDOFF.md` |

## Double-commit cadence (no plan-amend, no version bump)

1. **Work commit:** `[v2.9 P4] INFO override fix (info_override_missing_in_copyasoverride) + line-180 error-message DX bonus-catch`. Bridge code + race-probe + coverage-smoke + CHANGELOG + (optional) KNOWN_ISSUES. Push.
2. **Hash-record commit:** `[v2.9 P4] Handoff: record commit hash <work-hash>`. PHASE_4_HANDOFF.md only. Push.

End each subject line with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line bodies.

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 4 executor" + confirm session-start state.
2. Recap the 2 items + 7 deliverables in your own words.
3. Propose your work order. Default sequence per PLAN § Conventions probe-first discipline: **Item 1a probe (FAIL expected) → Item 1b/1c fix lands → Item 1d probe re-runs (PASS) → Item 1e coverage-smoke cell → bridge build → coverage-smoke end-to-end → Item 2 bonus-catch (small enough to land in same work commit, not a separate cycle) → docs append → handoff.**
4. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **HALT 1 — Pre-fix probe FAIL confirms repro.** Race-probe attempt at INFO override throws "Could not create override for DialogResponsesBinaryOverlay" (or whatever the current error message renders to — your bonus-catch may change the wording mid-phase, but the probe runs against the unchanged-yet code first). Show Aaron the FAIL trace; confirms the bug repros in-process exactly as Phase 3 saw it through the live bridge. Probe-first discipline rigor checkpoint.
- **HALT 2 — Post-fix probe PASS + coverage-smoke green.** After Items 1b/1c fix lands + Item 1d re-runs + Item 1e cell added + bridge builds clean + coverage-smoke runs end-to-end. Show Aaron: probe PASS trace, new SHA, total cell count (383: 382 baseline + 1 new), 0 FAIL, 6 SKIPs unchanged, drift-detection diff confirming bridge changes scoped to CopyAsOverride + TryRemoveOverride + line-180.

## Mandatory halt-and-report triggers (any → halt immediately)

- Any of the 376 PASS cells (160 v2.8 baseline + 134 P2A + 45 P2B + 31 P2C + 10 P2D actual PASS counts; total 376) starts failing post-fix. Drift-detection: `git diff def8fa8 -- tools/mutagen-bridge/PatchEngine.cs` should show only the CopyAsOverride INFO branch + TryRemoveOverride INFO branch + line-180 error-message change. If unrelated code paths drifted, halt.
- Bridge build fails (warnings or errors).
- The fix shape in your work plan diverges from Phase 3's recommended fix (`IDialogResponsesGetter r => patchMod.DialogResponses.GetOrAddAsOverride(r)`) — escalate to conductor before deviating; the proposed fix is the established pattern for the switch.
- Bonus-catch surfaces > 1h additional or new operator surface.
- An assumption Phase 3's bug entry made about Mutagen's `SkyrimMod.DialogResponses` interface turns out wrong (the property exists but exposes a different override-add API, etc.).

## Acceptance criteria (Phase 4 complete)

- INFO race-probe lands; pre-fix run FAILs (proves repro); post-fix run PASSes.
- `CopyAsOverride` + `TryRemoveOverride` switches both grow `IDialogResponsesGetter` branch; bridge builds clean.
- `1.P.GetIsID.INFO` coverage-smoke cell PASSes (covers Scenario 3.1's exact shape: vanilla INFO + add_conditions GetIsID with parameters.Object).
- Bonus-catch error-message change at line 180 lands; user-facing error names the record type code (e.g. "INFO") instead of the leaky internal Mutagen overlay class name.
- Coverage-smoke total: 382 + 1 = 383 cells; 377 PASS (376 baseline + 1 new) + 6 SKIPs unchanged + 0 FAIL.
- All 376 baseline PASS cells stay green — drift-detection diff confirms scoped bridge changes.
- New bridge SHA captured (must differ from P2D's `2e3a1094…f8293975e`).
- CHANGELOG appended with the fix bullet + bonus-catch bullet under existing `## v2.9.0 — TBD` entry.
- Handoff under 400 lines.

## Out of scope for Phase 4

- **Other v2.7.1 / v2.8.0 carryovers** (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access) — explicitly deferred per PLAN.md § Carry-overs.
- **Boolean dispatcher branch** (deferred to v2.9.x first-consumer trigger).
- **Sub-B 6 String-slot Condition functions** (deferred to v2.9.x).
- **Live install sync** (Phase 5 owns the canonical ship sync; Phase 4 stays local-only unless Aaron approves a mid-phase sync for verification).
- **Re-running Layer 3 scenarios** (Phase 5 re-runs both 3.1 + 3.2 against the post-Phase-4 bridge per PHASE_3_HANDOFF.md § Preconditions for Phase 5).
- **Version bump** (2A bumped; no re-bump).
- **Plan-amend** (none expected; if you find a NEW architectural surprise, escalate via § Conductor asks).
- **Touching CONDITIONS_AUDIT.md / MATRIX.md** (source-of-truth + spec; Phase 4 doesn't change either).

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `<plan>/PHASE_4_HANDOFF.md` per PLAN.md § Handoff template:
   - **What was done** — Item 1 (a/b/c/d/e) + Item 2 + bridge build + cells + docs.
   - **Verification performed** — pre-fix probe FAIL trace + post-fix probe PASS trace + coverage-smoke counts (382 → 383, 376 → 377 PASS, 6 SKIPs unchanged) + drift-detection diff confirmation + new bridge SHA.
   - **Bugs surfaced** — any new bug surfaced during fix. Likely none; flag if anything.
   - **Deviations from plan** — anything different from this kickoff.
   - **Known issues / open questions** — anything Phase 5 needs to know.
   - **Conductor asks** — only if questions.
   - **Preconditions for Phase 5** — bridge built; new SHA captured; live install still at P2D SHA until Phase 5 syncs (or note if Aaron approved mid-Phase-4 sync); Layer 3 re-run is mandatory per PHASE_3_HANDOFF.md (Scenario 3.1 lifts from BLOCKED → PASS post-fix; Scenario 3.2 confirms no regression of existing 12/12 PASS).
   - **Files of interest for Phase 5** — bridge SHA + path; coverage-smoke and race-probe entry points; PHASE_3_HANDOFF.md scenarios 3.1 + 3.2 assertion checklists for re-run; PLAN.md § Phase 5 ship sequence.
3. **Do NOT write Phase 5's kickoff prompt.** Conductor owns the ship-sequence kickoff.
4. Force-add new file (`git add -f <plan>/{PHASE_4_HANDOFF.md,PHASE_4_KICKOFF_PROMPT.md}`).
5. Push the double-commit chain (work + hash-record).

## What "good" looks like

- A `[v2.9 P4]` work-commit diff that's narrowly scoped: 2 hunks in CopyAsOverride + TryRemoveOverride (one branch each) + 1 hunk at line 180 (error message) + race-probe addition + coverage-smoke addition + CHANGELOG appends. Nothing else moves.
- A pre-fix probe FAIL trace that mirrors Phase 3's live error narrative — the in-process and live-bridge failure modes line up, proving Phase 3's diagnosis was correct + the fix targets the right code path.
- A post-fix probe PASS trace that shows the INFO override succeeds + the new condition's slot resolves correctly. The same probe shape is what Phase 5 will lift to "Scenario 3.1 re-run PASSes" against the live install.
- An error-message change that — in a hypothetical future failure on a different record type missing from the switch — names "ARMO" or "RACE" or whatever clearly, not the internal Mutagen overlay class name. Cleanly demonstrates the leak fix.

---

Confirm you've identified yourself as Phase 4 executor + state-checks pass + bridge SHA matches P2D baseline + `mo2_ping` returns `2.9.0`, then propose your work order with the probe-first sequence explicit.
