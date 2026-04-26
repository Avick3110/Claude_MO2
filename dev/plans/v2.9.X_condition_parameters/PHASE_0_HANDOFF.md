# Phase 0 Handoff — Plan + matrix scaffold + record selection

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-26
**Session length:** ~1h
**Commits made:** `b5edf14` (work) + this hash-record commit
**Live install synced:** No (Phase 0 is docs-only)

## Working version slug

**`v2.9.0`** — locked at PLAN review (per PHASE_0_KICKOFF_PROMPT.md § Conductor decisions). Phase 2 commits the version-bump constants (`config.py`, `.iss`, `README.md`); Phase 0 records the slug in this handoff only.

Plan dir name (`v2.9.X_condition_parameters/`) stays unchanged — internal working slug, no rename.

## Conductor decisions inherited (locked)

These are pre-litigated and carry forward to Phase 1's executor without re-debate:

1. **Version slug = `v2.9.0`** (above).
2. **Aggressive Pareto guidance.** Aaron prefers an aggressive Pareto pick. Phase 1's executor should not default-conservative — pull stretch candidates and beyond if Phase 1's evidence supports. Floor (PLAN.md § Phase 1): `GetIsID`, `GetInFaction`, `GetInCell`, `HasMagicEffect`, `HasPerk`, `HasSpell`, `GetIsRace` + ActorValue carryover. Stretch: `GetItemCount`, `IsInList`, `WornHasKeyword`, `GetEquipped`. Floor is a starting point, not a ceiling.
3. **Slot-type expansion pre-authorized within RouteParameterSlot envelope.** If Phase 1's inventory probe surfaces an exotic slot type (multi-FormLink, custom Loqui type, etc.) that Phase 2 can absorb cheaply (small extension to `RouteParameterSlot`, no new operator surface), Aaron has pre-authorized absorption. >1h additional or new operator surface still requires conductor escalation per the standard PLAN.md § E + § Phase 2 conductor-decision rules.

## What was done

- **`<plan>/MATRIX.md`** — NEW. Four-layer scaffold (Layer 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention. Mirrors v2.8.0's MATRIX.md structurally; anchored on Condition-function parameter slots (slot dispatch, multi-slot combinatorics, in-scope vs out-of-scope error rows in Layer 1.D). 316 lines. Per-function rows are placeholders awaiting Phase 1's Pareto lock.
- **Cell-ID convention** documented at MATRIX.md § Cell-naming convention:
  - `1.P.<Function>.<RecordType>` — Layer 1 positives (e.g. `1.P.GetIsID.MGEF`)
  - `1.D.<NN>` — Layer 1.D negatives + out-of-scope errors (e.g. `1.D.50` for out-of-scope-function-with-parameters)
  - `2.<NN>` — Layer 2 combinatorial
  - `3.<N>` — Layer 3 workflow scenarios (`3.1` dialog, `3.2` perk)
  - `4.<sub>.<NN>` — Layer 4 edges (`4.dsl.<NN>`, `4.slot.<NN>`, `4.formid.<NN>`, `4.enum.<NN>`, `4.compat.<NN>`, `4.carry.<NN>`)
  - `5.<NN>` — Layer 5 regression (mapped 1:1 to v2.8.0 cells)
- **Layer 3 workflow scenarios pre-spec'd** with use-case descriptions only; live-FormID picks deferred to Phase 3:
  - **Scenario 3.1 — Dialog `GetIsID` topic gating.** Real-world dialog patcher gates a topic to a specific NPC via `GetIsID(Reference)`. Today the bridge produces a structurally-valid but always-false condition; v2.9 lands the Reference slot.
  - **Scenario 3.2 — Perk `HasPerk` / `HasSpell` prerequisite gate.** Real-world perk patcher gates a perk effect on a prerequisite perk/spell. Today's always-false condition makes the perk un-unlockable; v2.9 lands the Perk/Spell slot.
- **Layer 1.D out-of-scope error cells pre-spec'd** (`1.D.50`–`1.D.53`) with the exact error wording from PLAN.md § C — these don't depend on which functions Phase 1 locks.
- **Layer 4 edge cells pre-spec'd** — DSL ambiguity (both `actor_value` AND `parameters: {ActorValue: ...}`), slot-dispatch edges (unknown SlotName, wrong JSON-type), FormID-resolution edges, enum case-insensitivity, back-compat preservation, and carry-over surface confirmation.
- **Layer 5 regression band** pointer recorded — all 160 v2.8.0 coverage-smoke cells run unchanged in Phase 2.
- **`<plan>/PHASE_0_HANDOFF.md`** — NEW (this file).
- **`<plan>/PHASE_0_KICKOFF_PROMPT.md`** — already-existing (conductor wrote it). Force-added in Phase 0's commit if not already tracked.

No production code touched. No version bump. PLAN.md was already committed in `0565ce7` ([v2.9 scoping] Plan + conductor kickoff …) so it's already in git — Phase 0's commit only adds MATRIX.md + PHASE_0_HANDOFF.md (+ PHASE_0_KICKOFF_PROMPT.md if not yet tracked).

## Verification performed

Phase 0 has no test runs — it's structural scaffolding. Verification = the structural mirror of v2.8.0's MATRIX.md.

| Check | v2.8.0 | v2.9.0 (this matrix) | Match |
|---|---|---|---|
| Header + methodology block | lines 1–13 | lines 1–13 | ✅ |
| Layer numbering | 1 (1.A/B/C/E/regression/D) + 2 + 3 + 4 (sub-grouped) | 1 (1.P.FormLink/Enum/MultiSlot/PrimitiveOnly) + 1.D (in-scope + out-of-scope) + 2 + 3 + 4 (sub-grouped: dsl/slot/formid/enum/compat/carry) + 5 | ✅ (anchor shifted from operator-tier to parameter-shape, layer count parallel) |
| Cell-ID convention documented | implicit (e.g. `1.A.07`, `1.D.04`, `4.c.01`) | explicit § Cell-naming convention table | ✅ (more explicit per Phase 0 prerogative) |
| Per-row columns (op / type / source / operation / expected) | yes | yes | ✅ |
| Layer 3 workflow scenarios | 5 scenarios pre-spec'd; live FormIDs deferred to Phase 3 | 2 scenarios pre-spec'd (PLAN.md § Phase 0 step 2 floor); live FormIDs deferred to Phase 3 | ✅ |
| Total assertion count section | yes (~186) | yes (~220–240 estimate, range because post-Pareto-lock function count is open) | ✅ |
| Harness output convention | yes | yes (mirrors v2.8.0 example block) | ✅ |
| Skip-with-reason convention | yes | yes | ✅ |

State checks passed at session start:

- `git rev-parse HEAD` → `0565ce7d7ea273003a4468cda19f64fedc39990e` ✅ (matches scoping commit one above v2.8.0 ship `419a719`).
- Working tree clean ✅.
- `mo2_ping` → `version: "2.8.0"` ✅ (live install untouched).

## Bugs surfaced

N/A. Phase 0 is scoping-only.

## Deviations from plan

None. Phase 0 ran exactly as PHASE_0_KICKOFF_PROMPT.md and PLAN.md § Phase 0 specified. Cell-ID convention chosen as Phase 0 prerogative per kickoff prompt § "What good looks like" (`1.P.<Function>.<RecordType>` for positives, `1.D.<NN>` for negatives, sub-grouped Layer 4) — defensible in one sentence and unambiguous.

## Known issues / open questions

None Phase 0 needs Phase 1 to know beyond what PLAN.md § Phase 1 already covers.

## Conductor asks

None. Phase 0's deliverables fit the kickoff prompt without architectural ambiguity. The conductor can proceed directly to Phase 1's kickoff after reading this handoff.

## Preconditions for Phase 1

Phase 1's responsibilities (per PLAN.md § Phase 1):

- Inventory probe in `tools/race-probe/Program.cs` (extends existing v2.8 P4 probe section).
- CONDITIONS_AUDIT.md (NEW; mirrors v2.8.0's EFFECTS_AUDIT.md role).
- Pareto proposal to Aaron via conductor; lock returned via Phase 2 kickoff.
- Update MATRIX.md Layer 1.P + 1.D rows post-lock per the § Phase fill-in checklist at the bottom of MATRIX.md.

| Precondition | State |
|---|---|
| `tools/race-probe/Program.cs` editable + builds clean as-is | ✅ presumed (existing v2.8 P4 artifact; Phase 1's first step is to confirm with `cd tools/race-probe && dotnet build -c Release`) |
| MATRIX.md exists with Layer 1.P / 1.D scaffold + naming convention | ✅ landed in this commit |
| MATRIX.md § Phase fill-in checklist enumerates exact post-Pareto-lock edits | ✅ landed at MATRIX.md bottom |
| Conductor decisions inherited (slug=v2.9.0, aggressive Pareto, slot-type expansion pre-authorized) | ✅ recorded above |
| PLAN.md committed in `0565ce7` and readable | ✅ |
| v2.8.0 EFFECTS_AUDIT.md exists as audit-doc template | ✅ (`dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md`) |

## Files of interest for Phase 1

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/PLAN.md` § Phase 1 | Authoritative steps + § Conductor decisions for Phase 1 |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/MATRIX.md` § Phase fill-in checklist | Exact rows Phase 1 lands post-Pareto-lock |
| `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` | Audit-doc template for CONDITIONS_AUDIT.md (per-shape categorization, per-function slot signatures, architectural-surprise capture) |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_4_HANDOFF.md` (the v2.8 P4 actor_value handoff if one exists) | Working-precedent the v2.9 dispatcher generalizes — the `actor_value` reflection write into ActorValueConditionData is the pattern `RouteParameterSlot` extends |
| `Claude_MO2/tools/race-probe/Program.cs` | Inventory probe extension target (append after existing v2.8 P4 sections) |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (`BuildCondition`) | The dispatcher entry-point Phase 2 will extend; Phase 1 reads to confirm the `actor_value` precedent shape |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry + `Claude_MO2/KNOWN_ISSUES.md` | Standard dev-startup orientation per `feedback_dev_startup.md` |

## Acceptance — Phase 0

Per PHASE_0_KICKOFF_PROMPT.md § Acceptance criteria:

- ✅ `MATRIX.md` exists with four-layer scaffold + cell-naming convention. Per-function rows are placeholders awaiting Phase 1's Pareto lock.
- ✅ Layer 3 scenarios named (3.1 dialog GetIsID, 3.2 perk HasPerk/HasSpell) with use-case descriptions; live-FormID picks deferred to Phase 3.
- ✅ `git diff main^` shows: PLAN.md (already tracked from `0565ce7`), MATRIX.md (NEW), PHASE_0_HANDOFF.md (NEW), PHASE_0_KICKOFF_PROMPT.md (force-added if not already tracked). No production code touched.
- ✅ Working version slug `v2.9.0` recorded in handoff (above § Working version slug).
