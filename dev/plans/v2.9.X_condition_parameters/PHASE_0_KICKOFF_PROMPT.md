# Phase 0 Kick-off — Plan + matrix scaffold + record selection

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 0 executor** for the v2.9.0 Claude_MO2 release (Condition-function parameter slots). Your job is to lay down `MATRIX.md`'s four-layer scaffold + cell-naming convention, pre-spec the Layer 3 workflow scenarios, and write `PHASE_0_HANDOFF.md`. **No production code. No version bump.** Phase 1 fills in the per-function rows after Aaron locks the Pareto pick.

## Context (read this once, don't search for history)

v2.8.0 shipped 2026-04-26 (commit `419a719`); origin/main is now at `0565ce7` (scoping commit, one above ship). v2.8.0's `actor_value` Condition-parameter handler is the working precedent the v2.9.0 dispatcher generalizes — single `Enum.Parse<ActorValue>` reflection write today, becoming a generic slot router across `IFormLinkOrIndex<T>` / enum / int / float / bool. v2.8.0's plan dir (`dev/plans/v2.8.0_verification/`) is your structural template — its `MATRIX.md`, `EFFECTS_AUDIT.md`, and `PHASE_0_HANDOFF.md` are the exemplars to mirror. The plan dir for this release is named `v2.9.X_condition_parameters/` — leave the dir name alone (it's the internal working slug); user-facing version is **v2.9.0**, baked into config.py / .iss / README in Phase 2's bump commit.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `0565ce7…` (scoping commit; v2.8.0 ship is one prior at `419a719`).
   - Working tree clean.
   - `mo2_ping` returns `version: "2.8.0"` (live install untouched in Phase 0).
2. **Read these files in full, in order:**
   - `dev/plans/v2.9.X_condition_parameters/PLAN.md` § Session-start ritual + § Phase 0 + § Handoff template.
   - `dev/plans/v2.8.0_verification/MATRIX.md` — your structural template. Note the four-layer scaffold (Layer 1 / 1.D / 2 / 3 / 4 / 5), cell-ID convention (e.g. `1.A.07`, `1.D.04`, `4.c.01`), and the placeholder-vs-filled distinction for live-FormID-bearing rows.
   - `dev/plans/v2.8.0_verification/PHASE_0_HANDOFF.md` — exemplar Phase 0 handoff length + structure.
3. **Skim, don't memorize:**
   - `dev/plans/v2.8.0_verification/PLAN.md` § Phase 0 (~50 lines) — confirm scope shape matches v2.9's.

## Conductor decisions (locked — do not re-litigate)

- **Version slug locked: v2.9.0.** Aaron locked at PLAN review. Phase 0 records `v2.9.0` as the canonical slug in the handoff under "Working version slug." Phase 2 bumps the version constants. Plan dir name (`v2.9.X_condition_parameters/`) stays unchanged — internal working slug, no rename.
- **Pareto guidance to Phase 1 (record in handoff for Phase 1's executor to inherit):** Aaron prefers an aggressive Pareto pick — pull stretch candidates and beyond if Phase 1's evidence supports. Phase 1's executor should not default-conservative; the floor is a starting point, not a ceiling.
- **Slot-type expansion pre-authorized (record in handoff):** if Phase 1's inventory probe surfaces an exotic slot type (multi-FormLink, custom Loqui type, etc.) that Phase 2 can absorb cheaply (small extension to `RouteParameterSlot`, no new operator surface), Aaron has pre-authorized absorption. >1h additional or new operator surface still requires conductor escalation.

## Phase 0 deliverables

| # | Item | Files |
|---|---|---|
| 1 | MATRIX.md scaffold with four-layer structure + cell-naming convention | `<plan>/MATRIX.md` (NEW) |
| 2 | Layer 3 workflow scenarios pre-spec'd with use-case descriptions (FormID picks deferred to Phase 3) | `<plan>/MATRIX.md` § Layer 3 |
| 3 | Phase 0 handoff recording slug, conductor decisions, scaffold state | `<plan>/PHASE_0_HANDOFF.md` (NEW) |
| 4 | Force-add PLAN.md and MATRIX.md to git | git index |

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 0 executor" and confirm the session-start state checks (HEAD hash, clean tree, `mo2_ping`).
2. Recap deliverables in your own words (demonstrates you've read the plan).
3. Propose your work plan: order of MATRIX sections, the cell-ID convention you'll use (Phase 0's prerogative — propose one based on v2.8.0's pattern), the two Layer 3 scenarios you'll pre-spec (PLAN.md § Phase 0 step 2 names dialog GetIsID + perk HasPerk/HasSpell as the anchors).
4. Wait for go-ahead before writing.

## Halt-and-report points

- **Mid-session:** none expected — Phase 0 is single-flow scaffolding.
- **Mandatory halt-and-report triggers:**
  - Working tree found dirty at session start, or HEAD differs from `0565ce7…`.
  - `mo2_ping` returns anything other than `"2.8.0"` or fails to respond (live install drift).
  - You discover MATRIX.md already exists with non-trivial content (someone else already started Phase 0 — halt, ask conductor before overwriting).

## Acceptance criteria (Phase 0 complete — from PLAN.md § Phase 0)

- `MATRIX.md` exists with four-layer scaffold + cell-naming convention. Per-function rows are placeholders awaiting Phase 1's Pareto lock.
- Layer 3 scenarios named with use-case descriptions; live-FormID picks deferred to Phase 3.
- `git diff main^` shows: PLAN.md (existing — force-add only if not yet tracked), MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched.
- Working version slug `v2.9.0` recorded in handoff.

Note: PLAN.md was already committed in `0565ce7` ([v2.9 scoping] Plan + conductor kickoff …) so it's already in git. Phase 0's commit only adds MATRIX.md + PHASE_0_HANDOFF.md (and PHASE_0_KICKOFF_PROMPT.md, which the conductor wrote — force-add it too if not yet tracked).

## Commit format

Subject: `[v2.9 P0] <description>`. Body: bullets. End with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line.

Double-commit cadence:
- Work commit: `[v2.9 P0] Plan + matrix scaffold + record selection`
- Hash-record commit: `[v2.9 P0] Handoff: record commit hash <work-hash>`

Push both.

## Out of scope for Phase 0

- Touching any production code (`tools/`, `mo2_mcp/`, `installer/`).
- Bumping version constants (Phase 2 owns this).
- Filling in per-function MATRIX rows (Phase 1 owns this — post-Pareto-lock).
- Picking live FormIDs for Layer 3 scenarios (Phase 3 owns this — execution-time).
- Renaming the plan dir (`v2.9.X_condition_parameters/` stays).
- Producing CONDITIONS_AUDIT.md (Phase 1 owns this).
- Modifying `KNOWN_ISSUES.md` / `CHANGELOG.md` / `README.md` (Phase 2 owns this).

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `dev/plans/v2.9.X_condition_parameters/PHASE_0_HANDOFF.md` per the template at the bottom of `PLAN.md` § Handoff template. Sections you must populate:
   - **What was done** — MATRIX.md sections landed, cell-ID convention, Layer 3 scenarios pre-spec'd.
   - **Verification performed** — Phase 0 has no test runs; document the structural mirror of v2.8.0's MATRIX (column counts match, layer numbering matches).
   - **Bugs surfaced** — N/A for Phase 0.
   - **Deviations from plan** — anything that drifted from PLAN.md § Phase 0. Default: "None."
   - **Known issues / open questions** — anything the next phase needs to know.
   - **Conductor asks** — only if you have questions; format per PLAN.md § Communicating with the conductor.
   - **Preconditions for next phase** (Phase 1):
     - Inventory probe entry-point in `tools/race-probe/Program.cs` is the established v2.8.0 P4 location — confirm the file is editable and builds clean as-is.
     - Document the conductor decisions inherited from this kick-off (version slug = v2.9.0, aggressive Pareto guidance, slot-type expansion pre-authorized).
   - **Files of interest for next phase** — paths Phase 1 will need (PLAN.md § Phase 1, this MATRIX, v2.8.0 EFFECTS_AUDIT.md as audit-doc template, race-probe Program.cs as probe entry-point).
3. **Do NOT write the next phase's kick-off prompt.** The conductor will write Phase 1's after reading your handoff.
4. Force-add the new files (`git add -f Claude_MO2/dev/plans/v2.9.X_condition_parameters/{MATRIX.md,PHASE_0_HANDOFF.md,PHASE_0_KICKOFF_PROMPT.md}`).
5. Work commit + hash-record commit + push.

## What "good" looks like

A Phase 0 handoff under 300 lines. A MATRIX.md that — opened side-by-side with v2.8.0's MATRIX.md — is structurally familiar but anchored on Condition-function parameter cells (slot dispatch, multi-slot combinatorics, in-scope-vs-out-of-scope error rows in Layer 1.D). Cell IDs follow a convention you can defend in one sentence (e.g. "`1.P.<Function>.<RecordType>` for Layer 1 positives, `1.D.<NN>` for Layer 1.D errors" — exact convention is your call as long as it's unambiguous and matches v2.8.0's spirit).

---

Confirm you've identified yourself as Phase 0 executor + state-checks pass, then propose your work plan.
