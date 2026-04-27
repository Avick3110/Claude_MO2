# Phase 3 Kick-off — Live workflow scenarios on Authoria (sync v2.8.0 → v2.9.0 + 2 patcher scenarios)

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 3 executor** for the v2.9.0 Claude_MO2 release. Your job is to **(1) sync the live install at `<live>` from v2.8.0 to v2.9.0** with Aaron's explicit go-ahead at every filesystem-touching step, then **(2) run two realistic patcher scenarios** against the Authoria modlist via `mo2_create_patch`, verifying the v2.9 Condition-parameter dispatcher under real-world dialog and perk gating use cases. Phase 3 closes with a per-scenario assertion table + any surfaced bugs forwarded to Phase 4 (conditional) or directly to Phase 5 (ship).

## Context (read this once, don't search for history)

v2.8.0 shipped `419a719`. v2.9.0 in flight: Phases 0/1 + 2A/2B/2C/2D complete (origin/main HEAD `5ccd974`). **Phase 2 is feature-complete**: 199 dispatcher-wired functions across 5 of 6 PLAN-named branches (FLI / IFormLink<T> / Enum / Int32 / Single; Boolean deferred to v2.9.x). Final P2 bridge SHA `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e`. Coverage-smoke at 382 cells (376 PASS + 6 SKIP + 0 FAIL). Two bonus-catches absorbed (P2C UnknownConditionData round-trip artifact, P2D GetVATSValueUnknown Mutagen 0.53.1 schema gap — both KNOWN_ISSUES.md, neither blocks Phase 3 since Layer 3 scenarios target GetIsID + HasPerk/HasSpell, not those edge functions). **Live install at `<live>` is currently v2.8.0** — `mo2_ping` will confirm. Phase 3's first task is the v2.8.0 → v2.9.0 sync; everything downstream depends on it.

## Path conventions (resolve before any filesystem command)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` |
| `<plan>` | `<repo>\dev\plans\v2.9.X_condition_parameters\` |

Quote paths in shell commands — they contain spaces.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `5ccd974…` (Phase 2D hash-record commit).
   - Working tree clean.
   - `mo2_ping` returns `version: "2.8.0"` ← **this is expected at session start**; Step 1 of work plan changes it to `2.9.0`.
   - Bridge under `<repo>` IS v2.9.0 P2D (built in 2D; SHA `2e3a1094e07b39c532d82370dbc6a886deea2a2f3ea97c9dcb0914af8293975e`). Confirm via `sha256sum <repo>/tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`. If SHA differs, halt — dirty build inheritance.
2. **Read these files in full, in order:**
   - `<plan>/PLAN.md` § Session-start ritual + § Phase 3 + § Architecture A/B/C + § Handoff template + § Communicating with the conductor + § E (cross-phase decisions).
   - `<plan>/PHASE_2D_HANDOFF.md` — Phase 2 closing summary + § Preconditions for Phase 3 + § Files of interest for Phase 3.
   - `<plan>/MATRIX.md` § Layer 3 (Scenario 3.1 dialog GetIsID + Scenario 3.2 perk HasPerk/HasSpell with use-case descriptions + assertion templates).
   - `<plan>/CONDITIONS_AUDIT.md` § Floor + Stretch slot signatures (GetIsID = `Object: IFormLinkOrIndex<IReferenceableObjectGetter>`, HasPerk = `Perk: IFormLinkOrIndex<IPerkGetter>`, HasSpell = `Spell: IFormLinkOrIndex<ISpellGetter>`).
3. **Skim, don't memorize:**
   - `<repo>/kb/KB_Tools.md` — `mo2_create_patch` + `mo2_record_detail` reference.
   - `<repo>/dev/plans/v2.8.0_verification/PHASE_3_HANDOFF.md` — canonical workflow-scenario handoff exemplar from v2.8.0 (per-scenario assertion table shape, F5 cleanup discipline, bug triage format).
   - `<repo>/KNOWN_ISSUES.md` § Condition-parameter coverage (v2.9.0) — authoritative source for what's in-scope (199 functions, 5 branches) vs. deferred.

## Conductor decisions inherited (locked — do not re-litigate)

1. **Live-sync IS Phase 3's first step.** Per PLAN § Phase 3 step 1: live install must be at v2.9.0 before scenarios run. Conductor confirms: live IS at v2.8.0 at session start; sync directive lands here. Execute under Aaron's explicit go-ahead at each filesystem-touching step (CLAUDE.md "Always confirm before" + "Never modify install files without permission" — both apply; the bridge .exe + Python files are install files).
2. **Layer 3 scenarios are locked at 2 per MATRIX § Layer 3.** No expansion mid-phase. If a scenario's named record isn't ideal in the live modlist (e.g. INFO has no existing DialogConditions making it a poor demo), Aaron may swap the live FormID at scenario-build time; document in handoff.
3. **Test patches live in `<modlist>/mods/Claude Output/` and are deleted between scenarios + at end of phase.** Aaron F5s in MO2 after each delete (external filesystem changes per CLAUDE.md require manual MO2 refresh). Every patch follows the `v2.9-scenario-<N>.esp` naming convention.
4. **Bridge SHA preservation.** Phase 3's sync copies `<repo>/tools/mutagen-bridge/bin/Release/net8.0/*` → `<live>/tools/mutagen-bridge/`. Live bridge SHA must match P2D `2e3a1094…f8293975e` byte-for-byte after sync — no re-build during sync, just file copy. Phase 5 produces the canonical ship SHA via `dotnet publish` later (different SHA, same source).
5. **Phase 4 spawn-or-skip is conductor's call** based on Phase 3's bug surface. Your handoff names a recommendation in § Conductor asks; conductor decides + spawns 4 (or skips to 5).
6. **No bridge code changes in Phase 3.** Pure live-side verification. If you find a bridge bug requiring a code fix, halt-and-report — that's Phase 4 territory, not Phase 3.

## Phase 3 deliverables

| # | Item | Files |
|---|---|---|
| 1 | **Live install sync v2.8.0 → v2.9.0**: copy bridge .exe + .dll + .runtimeconfig.json from `<repo>/tools/mutagen-bridge/bin/Release/net8.0/` → `<live>/tools/mutagen-bridge/`; copy updated `mo2_mcp/tools_patching.py` (and any other v2.9-touched .py files — likely just that one) from `<repo>/mo2_mcp/` → `<live>/`; delete `<live>/__pycache__/`; Aaron full-restarts MO2 (NOT just Tools menu Stop/Start) | live filesystem |
| 2 | **Sync verification**: `mo2_ping` returns `version: "2.9.0"`; live bridge SHA matches P2D byte-for-byte | mo2_ping + sha256sum |
| 3 | **Pre-flight canary**: build one `mo2_create_patch` call exercising one in-scope function with `parameters` (e.g. GetIsID with `parameters: {Object: "<known-FormID>"}` against a test MGEF). Verify response + readback shows the slot resolved (NOT default FormID 0). Confirms live dispatcher is wired. | `mo2_create_patch` + `mo2_record_detail` |
| 4 | **Scenario 3.1 — Dialog GetIsID topic gating**: pick live INFO + live NPC_ FormIDs; build patch; readback assertions per MATRIX § Layer 3 Scenario 3.1; capture per-assertion PASS/FAIL | `<modlist>/mods/Claude Output/v2.9-scenario-1.esp` (created + deleted within phase) |
| 5 | **Cleanup between scenarios**: delete `v2.9-scenario-1.esp`; Aaron F5s in MO2 | Bash `rm` + Aaron F5 |
| 6 | **Scenario 3.2 — Perk HasPerk/HasSpell prerequisite gate**: pick live PERK + prereq PERK (and optionally prereq SPEL) FormIDs; build patch; readback assertions per MATRIX § Layer 3 Scenario 3.2; capture per-assertion PASS/FAIL | `<modlist>/mods/Claude Output/v2.9-scenario-2.esp` (created + deleted within phase) |
| 7 | **Cleanup at end**: delete `v2.9-scenario-2.esp`; Aaron F5s in MO2; modlist clean | Bash `rm` + Aaron F5 |
| 8 | **Cross-scenario rollup + triage**: per-scenario PASS/FAIL counts; group failures by suspected root cause if any pattern emerges; bug entries with slug + record type + repro + failure mode + proposed Phase 4 fix angle | handoff narrative |
| 9 | `PHASE_3_HANDOFF.md` per template — **MUST capture**: Phase 4 spawn-or-skip recommendation in § Conductor asks based on bug surface | `<plan>/PHASE_3_HANDOFF.md` |

## Double-commit cadence (no version bump, no plan-amend)

1. **Work commit:** `[v2.9 P3] Layer 3 workflow scenarios — N bugs surfaced` (N = whatever your scenarios surface; 0 is a clean number). No bridge code changes; commit captures handoff + any plan-archive notes if you create any. **The live-sync itself is NOT committed** (live install is outside the repo). Push.
2. **Hash-record commit:** `[v2.9 P3] Handoff: record commit hash <work-hash>`. Push.

End each subject line with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line bodies.

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 3 executor" + confirm session-start state (HEAD + repo bridge SHA + tree clean + `mo2_ping` returns `2.8.0` as expected pre-sync).
2. Recap deliverables in your own words.
3. **Propose live-sync work plan in detail** — specific source paths, specific destination paths, exact Bash `cp` commands you'll run, the `rm -rf <live>/__pycache__/` step, and the explicit ask for Aaron to full-restart MO2 after copies land. Aaron must approve each filesystem-touching step before you execute.
4. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **HALT 1 — Post-sync + pre-flight canary**: after the live-sync completes + Aaron full-restarts MO2 + `mo2_ping` returns `2.9.0` + live bridge SHA verified + pre-flight canary call PASSes (slot resolved, not default-zero). Show Aaron the trace. Confirms the entire downstream Phase 3 + Phase 5 chain rests on a working live dispatcher. **Do not proceed to Scenario 3.1 without this halt clearing.**
- **HALT 2 — Post-Scenario 3.1 + cleanup**: scenario built + readback captured + assertions tabled + test patch deleted + Aaron F5'd. Show Aaron the per-assertion table + any surfaced bugs. Aaron may swap Scenario 3.2's named record at this point if Scenario 3.1 surfaced something interesting about the live modlist's PERK availability.
- **HALT 3 — Post-Scenario 3.2 + cleanup + pre-handoff**: scenario built + readback captured + assertions tabled + test patch deleted + Aaron F5'd + modlist clean. Show Aaron the cross-scenario rollup + triage + the Phase 4 spawn-or-skip recommendation you'll put in § Conductor asks.

## Mandatory halt-and-report triggers (any → halt immediately)

- `mo2_ping` returns anything other than `2.9.0` after sync (sync didn't take — investigate before proceeding).
- Live bridge SHA differs from P2D byte-for-byte after sync (file copy went wrong — investigate).
- Pre-flight canary returns `success=true` but readback shows default FormID 0 in the slot (live bridge accepted `parameters` but didn't dispatch — sync may have only delivered the Python schema, not the bridge .exe; or the bridge .exe didn't actually update).
- Pre-flight canary returns `success=false` with "no such field 'parameters'" (live bridge is still v2.8.0 — sync failed; halt before scenarios run).
- Any external filesystem action (`cp`, `rm`, `mv`) without Aaron's prior explicit go-ahead.
- An MGEF/INFO/PERK record in the live modlist isn't where MATRIX § Layer 3 expected it; Aaron's call on whether to swap or halt.
- Any scenario assertion FAILs (capture trace; halt for triage before next scenario or before handoff).
- Any bridge-side error that suggests a code bug (Phase 4 territory; do not attempt fix in Phase 3).
- F5 step skipped between scenarios (orphans in `loadorder.txt`; can corrupt second scenario's read-back).

## Acceptance criteria (Phase 3 complete)

- Live install at v2.9.0; `mo2_ping` confirms; live bridge SHA matches P2D `2e3a1094…f8293975e` byte-for-byte.
- Pre-flight canary PASSes (slot resolved, not default).
- Scenario 3.1 (dialog GetIsID) executed end-to-end; assertion table captured.
- Scenario 3.2 (perk HasPerk/HasSpell) executed end-to-end; assertion table captured.
- Test patches deleted; modlist clean (no `v2.9-scenario-*.esp` in `<modlist>/mods/Claude Output/`); Aaron F5'd post-cleanup.
- Bug list extending P2C/P2D's, if any.
- Handoff under 400 lines.
- Handoff § Conductor asks names whether Phase 4 is needed (default: skip if zero bridge bugs surfaced; spawn if any new bug).

## Out of scope for Phase 3

- Bridge code changes (Phase 4 if needed; Phase 5 ship).
- Schema changes (Phase 4 if needed).
- CHANGELOG / KNOWN_ISSUES updates (Phase 4 absorbs surfaced findings; Phase 5 ships).
- Version bump (Phase 5 captures ship date in CHANGELOG; version constants stayed v2.9.0 since 2A).
- Plan-amend (none expected).
- Tag / GitHub release (Phase 5 with hard halt before public action).
- Coverage-smoke re-runs (Phase 2 verified the smoke matrix; Phase 5 re-runs against final ship SHA).
- Race-probe re-runs (Phase 2 verified).
- Touching CONDITIONS_AUDIT.md / MATRIX.md (source-of-truth + spec).
- Live-side mods other than the test patches (do NOT enable/disable existing mods, do NOT touch loadorder, do NOT modify any non-Claude-Output content).

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `<plan>/PHASE_3_HANDOFF.md` per PLAN.md § Handoff template:
   - **What was done** — sync + pre-flight + 2 scenarios + cleanup.
   - **Verification performed** — `mo2_ping` post-sync + bridge SHA verified + pre-flight canary trace + per-scenario readback evidence.
   - **Bugs surfaced** — slug + record type + repro + failure mode + proposed Phase 4 fix angle. Zero is a clean number.
   - **Deviations from plan** — anything different from this kickoff (e.g. Aaron swapped a live FormID; document the swap rationale).
   - **Known issues / open questions** — anything Phase 4 / 5 needs to know.
   - **Conductor asks** — **REQUIRED**: Phase 4 spawn-or-skip recommendation. If zero bridge bugs + zero matrix corrections, recommend skip-to-Phase-5. If any surfaced, recommend spawn with item list.
   - **Preconditions for Phase 4 (if recommended)** — bug list, repro paths, expected fix angles.
   - **Preconditions for Phase 5** — final ship SHA needs `dotnet publish` (Phase 5 owns); live install at v2.9.0 (already there post-Phase 3 sync); Layer 3 re-run if Phase 4 lands bridge changes (Phase 5 owns).
   - **Files of interest for Phase 4 / Phase 5** — handoff path lists.
3. **Do NOT write Phase 4 / Phase 5's kickoff prompt.** Conductor owns those.
4. Force-add new files (`git add -f <plan>/{PHASE_3_HANDOFF.md,PHASE_3_KICKOFF_PROMPT.md}`).
5. Push the double-commit chain (work + hash-record).

## What "good" looks like

- A live-sync that's reversible if anything goes wrong: bridge .exe was at the v2.8.0 SHA before sync; if you discover a sync issue you can roll back by re-copying from a v2.8.0 build (Aaron has the v2.8.0 ship artifact at the GitHub release). Stage the sync as discrete go-ahead-then-execute steps so any halt is recoverable.
- A pre-flight canary trace that anchors the entire phase — if the canary PASSes, scenarios are downstream verification; if FAILs, you've isolated the issue to sync mechanics, not v2.9.0 code.
- Per-scenario assertion tables that read like a v2.8.0 PHASE_3_HANDOFF.md sibling — same column structure (Assertion / Expected / Actual / PASS-FAIL), same triage format for any FAILs.
- A handoff whose § Conductor asks gives the conductor a clear spawn-or-skip call without ambiguity. "0 bugs surfaced → recommend skip to Phase 5" is the cleanest finish; "N bugs surfaced → spawn Phase 4 covering items 1–N" is the alternative.
- Modlist clean post-Phase-3 — no orphans in `loadorder.txt`, no `v2.9-scenario-*.esp` lingering, Aaron F5'd after every delete.

---

Confirm you've identified yourself as Phase 3 executor + state-checks pass + repo bridge SHA matches P2D baseline + `mo2_ping` returns the expected pre-sync `2.8.0`, then propose your live-sync work plan with specific source/destination paths and the explicit Aaron-go-ahead step before any filesystem touch.
