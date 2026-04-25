# Phase 0 Handoff — Plan + matrix specification + record selection

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1h
**Commits made:** `[v2.8 P0] Plan + matrix specification + record selection` (this commit)
**Live install synced:** No (Phase 0 is docs-only)

## What was done

- **PLAN.md** — Full v2.8.0 plan, 5-phase shape (P0 plan/matrix → P1 Effects-list capability → P2 verification harness → P3 workflow scenarios → P4 per-bug fixes → P5 ship). Mirrors v2.7.1's plan structure (path conventions, session-start ritual, handoff template, per-phase steps + acceptance criteria).
- **MATRIX.md** — ~186 assertion test specification across four layers. Layer 1 = coverage matrix (Tier A wire-ups + Tier B aliases + Tier C bracket/merge + new Layer 1.E Effects-list + pre-existing handler regression band + Tier D negatives). Layer 2 = combinatorial probes. Layer 3 = 5 workflow scenarios against live modlist. Layer 4 = malformed/idempotency/chained/replace edges + carry-over candidate probes.
- **Memory updated** — `project_capability_roadmap.md` body + `MEMORY.md` index reflect "v2.7.1 shipped + v2.8 in planning (verification + Effects-list write)" rather than the original "v2.8 = no new capabilities."

### Re-scope event captured

The original v2.8 plan was framed as pure verification/hardening per `project_capability_roadmap.md`'s v2.7.1-ship state. **Re-scoped on 2026-04-25** (same day as v2.7.1 ship) when a real-world consumer hit a SPEL.Effects-write gap during a custom-race rebuild patch. The bridge could not convert a JSON Effects array into Mutagen's `RefList<Effect>`. Promoted from KNOWN_ISSUES carry-over candidate ("Per-effect spell conditions … v2.8 if a real consumer surfaces") to v2.8 headline capability addition.

**Bounded scope:** ONE new mechanism (JSON Array → list-of-LoquiObject in `set_fields`); FIVE record types (SPEL/ALCH/ENCH/SCRL/INGR Effects); per-effect Conditions absorbed via the same recursion. The other v2.7.1 KNOWN_ISSUES carry-overs (Quest condition disambiguation, AMMO enchantment, replace-semantics whole-dict, chained dict access) remain deferred. PerkAdapter/QuestAdapter `attach_scripts` stays in scope as a Phase 4 fix-it.

### Conductor decisions captured at scoping

- **QUST.Aliases:** keep conditional in Phase 1 — fold in if AUDIT shows trivial reach, defer otherwise.
- **Scenario 3.1 target records:** Phase 3 picks equivalent records from the live modlist; no consumer-session-specific FormIDs required.
- **Commit cadence:** architect-conductor session lands Phase 0 (this commit). Phase 1 onward = fresh executor sessions.

## Verification performed

- `git status` clean before commit; `origin/main` at `2799789` (handoff hash record from v2.7.1 P5).
- v2.7.1 tag present at `c698e16`.
- No production code touched — verified via `git diff` showing only `dev/plans/v2.8.0_verification/{PLAN.md,MATRIX.md,PHASE_0_HANDOFF.md}` in the staged tree.
- No version bump in Phase 0 (per plan — bump moves to first Phase 1 commit).
- Plan files force-added per dev/ gitignore convention (`git add -f`).
- Memory files updated outside the repo (`~/.claude/projects/.../memory/`); no commit needed.

## Deviations from plan

None. Phase 0 ran exactly as the plan's own Phase 0 section specifies, with the addition that the architect-conductor session also wrote PLAN.md (PLAN was a pre-Phase-0 artifact in the v2.7.1 pattern; for v2.8.0 the architect drafted PLAN + MATRIX + handoff in one architect-conductor session, then committed as Phase 0).

## Known issues / open questions

None. Phase 0 is scoping-only.

## Preconditions for Phase 1

- ✅ MATRIX.md exists with Layer 1.E (Effects-list) cells specified.
- ✅ PLAN.md Phase 1 section is detailed enough for a fresh executor to act on without needing architect input mid-phase.
- ✅ `tools/race-probe/` exists and builds (existing artifact from v2.7.1).
- ✅ `tools/coverage-smoke/` exists and runs (1122-line, 22-test C# harness from v2.7.1).
- ✅ `tools/mutagen-bridge/PatchEngine.cs` is the file Phase 1 extends (`ConvertJsonValue` + possibly `SetPropertyByPath`).
- ✅ Conductor decisions on QUST.Aliases scope (conditional) and Scenario 3.1 record selection (equivalent picks) are captured here.

## Files of interest for Phase 1

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.8.0_verification/PLAN.md` § Phase 1 | Authoritative steps |
| `Claude_MO2/dev/plans/v2.8.0_verification/MATRIX.md` § Layer 1.E | Phase 1 deliverable contract — these cells must pass when Phase 2 runs |
| `Claude_MO2/tools/race-probe/Program.cs` | Probe extension target |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` | `ConvertJsonValue` (line ~777 in v2.7.1) is the primary extension point; `SetPropertyByPath` (line ~745) may need updates |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | Lay down the SPEL.Effects + ALCH.Effects regression test |
| `Claude_MO2/mo2_mcp/tools_patching.py` | Schema description for `set_fields` (line ~82 in v2.7.1) |
| `Claude_MO2/mo2_mcp/{config.py,CHANGELOG.md}`, `installer/claude-mo2-installer.iss`, `README.md`, `KNOWN_ISSUES.md` | Phase 1 first-commit version-bump targets |

## Acceptance — Phase 0

- ✅ MATRIX.md exists with per-cell spec for all four layers, including Layer 1.E (8 Effects-list cells).
- ✅ Pre-selected records / predicates documented in MATRIX.md (using coverage-smoke's `FirstOrDefault` predicate-selection pattern).
- ✅ `git diff main^` shows: PLAN.md (new), MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched. `config.py` / `.iss` / `README.md` / `CHANGELOG.md` unchanged.
- ✅ Phase 0 is docs-only; no version bump.
