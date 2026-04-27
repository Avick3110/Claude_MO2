# Phase 0 Handoff — Plan + matrix scaffold + design proposal

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-27
**Session length:** ~1h
**Commits made:** `<work-hash>` (this commit) + the hash-record commit that follows
**Live install synced:** No (Phase 0 is docs-only; live remains at v2.9.0 per `mo2_ping`)

## Working version slug

**`v2.9.1`** — locked at PLAN review (per CONDUCTOR_KICKOFF.md § Conductor decisions; restated in PHASE_0_KICKOFF prompt § Conductor decisions). Phase 2 commits the version-bump constants (`config.py`, `.iss`, `README.md`); Phase 0 records the slug in this handoff only.

Plan dir name (`v2.9.1_quest_condition_disambiguation/`) matches the slug — no rename pending.

## Conductor decisions inherited (locked)

These are pre-litigated and carry forward to Phase 1's executor without re-debate:

1. **Version slug = `v2.9.1`** (above).
2. **Operator-level placement is the leading recommendation for Q1.** Aaron's steer: "operator-level if it works." Phase 0's Q1 surface in § Conductor asks bias toward operator-level (per PLAN § B rationale: single logical group, simpler validation, narrower dispatch site, symmetric with `remove_conditions`). Q1 still gets surfaced for explicit lock — recommendation already named, so the conductor relay is "default holds unless Aaron flips."
3. **Generality scope steer: QUST-only is the default for v2.9.1.** Phase 1's probe surfaces easy-win recommendations for any additional multi-condition record types it finds. Phase 0 doesn't probe — Phase 1 does. MATRIX.md's Layer 1 scaffold is QUST-anchored without baking out room for additional record-type cells; the "Per-list-target coverage" extensibility note documents the Phase-1-and-Aaron-can-extend mechanism explicitly (per PLAN § D + CONDUCTOR_KICKOFF § Conductor decisions).
4. **No scope absorption.** All v2.9.0 carry-overs (Boolean dispatcher branch / 6 sub-B String functions / AMMO enchantment / replace-semantics dict / chained dict access / QUST.Aliases-Stages-Objectives / PERK.Effects / GetVATSValueUnknown Mutagen gap / etc.) stay deferred. v2.9.1 is single-mechanism: `condition_target` operator parameter only.
5. **PLAN.md and CONDUCTOR_KICKOFF.md are already force-added at `4cf417e`.** Phase 0's commit force-adds MATRIX.md + this handoff only.

## What was done

- **`<plan>/MATRIX.md`** — NEW. Six-layer scaffold (Layer 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention + per-phase fill-in checklists. Mirrors v2.9.0's MATRIX.md structurally; anchored on Quest condition disambiguation list-target dispatch (per-list-target coverage rows in Layer 1.P, new explicit error paths in Layer 1.D, surface composition in Layer 2, live workflow scenarios in Layer 3, parameter-value-form edges in Layer 4.dsl, full v2.9.0 regression band in Layer 5). ~290 lines. Per-list-target rows are placeholders awaiting Phase 1's probe-confirmed FormID; Layer 1.P extensible if Phase 1 + Aaron lock additional multi-condition record types.
- **Cell-ID convention** documented at MATRIX.md § Cell-naming convention:
  - `1.P.<op>.<target>.<RecordType>` — Layer 1 positives, anchored on list target (e.g. `1.P.add.dialog.QUST`, `1.P.remove.event.byfunc.QUST`)
  - `1.D.<NN>` — Layer 1.D negatives + new explicit error paths (e.g. `1.D.01` for missing-target on add)
  - `2.<NN>` — Layer 2 combinatorial
  - `3.<N>` — Layer 3 workflow scenarios (`3.1` dialog, `3.2` event)
  - `4.<sub>.<NN>` — Layer 4 edges (only `4.dsl.<NN>` sub-grouping needed for v2.9.1; v2.9.0's slot/formid/enum/compat/carry sub-groups don't apply because the per-Condition build pipeline doesn't change)
  - `5.<NN>` (or `5.range`) — Layer 5 regression (mapped 1:1 to v2.9.0's 382 cells)
- **Layer 1.P pre-spec'd** with 6 cells covering the matrix of {add, remove-by-index, remove-by-function} × {dialog, event} on QUST. Round-trip-distinguishability assertion (the "non-targeted list unchanged" check) is baked into every Expected column.
- **Layer 1.D pre-spec'd** with 5 cells covering the new explicit error paths from PLAN § C: missing target on add (`1.D.01`), missing target on remove (`1.D.02`), bad target value (`1.D.03`), strict reject on PERK with `condition_target` (`1.D.04` — flips per Q4 lock), Tier D fallthrough on ARMO (`1.D.05`). Wording for new error messages locks the shape; Phase 2 finalizes exact strings.
- **Layer 2 pre-spec'd** with 3 combinatorial cells: multi-condition single op, two-ops opposing-targets one record (with structural-limitation footnote per PLAN § B), v2.9.0 × v2.9.1 surface composition.
- **Layer 3 workflow scenarios pre-spec'd** with use-case descriptions + assertions + placeholder FormIDs for Phase 3:
  - **Scenario 3.1 — Quest DialogConditions: perk-gated quest visibility.** Real-world dialog patcher gates a QUST's dialog visibility via `HasPerk` on DialogConditions. Today the bridge surfaces `unmatched_operators: ["add_conditions"]` Tier D; v2.9.1 routes via `condition_target: "dialog"`.
  - **Scenario 3.2 — Quest EventConditions: Story Manager perk-eligibility gating.** Symmetric — gates a quest's eligibility for a Story Manager event via `HasPerk` on EventConditions. Routes via `condition_target: "event"`.
  - Both scenarios use `HasPerk` to exercise v2.9.0's generic dispatcher composition under v2.9.1's list-target dispatch. 3.1 includes a pre-flight regression check (same call without `condition_target` → 1.D.01 error live).
  - 3.2 includes an opt-in cross-scenario isolation assertion if 3.1 and 3.2 share the same QUST anchor (live-install equivalent of Layer 2.02).
- **Layer 4.dsl pre-spec'd** with 4 parameter-value-form edge cells: empty string (`4.dsl.01`), JSON null (`4.dsl.02`), case-variance per Q5 (`4.dsl.03`), supplied alongside unrelated operator (`4.dsl.04`).
- **Layer 5 regression band** pointer recorded — single range row covering v2.9.0's 382 coverage-smoke cells unchanged (Phase 2 confirms the actual baseline against `coverage-smoke/Program.cs`).
- **Total assertion count + harness output convention + skip-with-reason** sections mirror v2.9.0's MATRIX.md structurally.
- **Per-phase fill-in checklists** (Phase 1 hand-back, Phase 2 hand-back, Phase 3 hand-back) document exactly which placeholders each subsequent phase replaces and which Layer-4 expectation flips align with Q4/Q5 lock outcomes.
- **`<plan>/PHASE_0_HANDOFF.md`** — NEW (this file).

No production code touched. No version bump. PLAN.md + CONDUCTOR_KICKOFF.md already committed in `4cf417e` (`[v2.9.1 scope] PLAN.md + CONDUCTOR_KICKOFF.md`) — Phase 0's commit only adds MATRIX.md + this handoff.

## Verification performed

Phase 0 has no test runs — it's structural scaffolding. Verification = the structural mirror of v2.9.0's MATRIX.md adapted for v2.9.1's anchor.

| Check | v2.9.0 | v2.9.1 (this matrix) | Match |
|---|---|---|---|
| Header + methodology block | lines 1–11 | lines 1–11 | ✅ (anchor shifted to Quest condition disambiguation) |
| Layer numbering | 1.P (FormLink/Enum/MultiSlot/PrimitiveOnly/NoParam) + 1.D (in-scope + out-of-scope) + 2 + 3 + 4 (dsl/slot/formid/enum/compat/carry) + 5 | 1.P (per-list-target × QUST) + 1.D (explicit error paths) + 2 + 3 + 4 (dsl only) + 5 | ✅ (anchor shifted from function inventory to list target; Layer 4 sub-groups collapsed because the per-Condition build pipeline doesn't change in v2.9.1) |
| Cell-ID convention documented | explicit § Cell-naming convention table | explicit § Cell-naming convention table | ✅ (different anchor — `1.P.<op>.<target>.<RecordType>` vs `1.P.<Function>.<RecordType>`) |
| Per-row columns (op / type / source / operation / expected) | yes | yes | ✅ |
| Layer 3 workflow scenarios | 2 scenarios pre-spec'd; live FormIDs deferred to Phase 3 | 2 scenarios pre-spec'd (one dialog, one event); live FormIDs deferred to Phase 3 | ✅ (parallel scenario count; v2.9.0 also pre-spec'd 2) |
| Total assertion count section | yes (~597 harness cells) | yes (~400 harness cells; Layer 5 regression band carries the bulk) | ✅ |
| Harness output convention | yes | yes (mirrors v2.9.0 example block) | ✅ |
| Skip-with-reason convention | yes | yes (focused on QUST anchor fixture availability) | ✅ |
| Phase fill-in checklists | one (Phase 1 hand-back) | three (Phase 1 + Phase 2 + Phase 3 hand-backs) | ✅ (more granular per phase fits v2.9.1's narrower mechanism — each phase has a smaller, more specific set of placeholders to replace) |

State checks passed at session start:

- `git log -3 --oneline` → top hash `4cf417e [v2.9.1 scope] PLAN.md + CONDUCTOR_KICKOFF.md` ✅ (matches kickoff prompt's expected hash).
- `git status` → clean working tree ✅.
- `mo2_ping` → `version: "2.9.0"` ✅ (live install untouched at v2.9.0 baseline).

## Bugs surfaced

N/A. Phase 0 is scoping-only.

## Deviations from plan

None. Phase 0 ran exactly as PHASE_0 kickoff prompt and PLAN.md § Phase 0 specified. Cell-ID convention adapted to v2.9.1's list-target anchor (`1.P.<op>.<target>.<RecordType>` — Phase 0 prerogative, defensible: anchors on the v2.9.1 unit of work which is the list target, not the function name v2.9.0 anchored on).

Layer 3 scenario count = 2 (dialog + event) per the kickoff prompt's "Optional 2nd EventConditions scenario flagged" steer + Aaron's "no objections" go-ahead during the work-plan proposal. Both scenarios use `HasPerk` for symmetry and v2.9.0-dispatcher exercise; the canonical follower-perk-gating use case from PLAN § Background is preserved as the framing for 3.1.

## Known issues / open questions

None Phase 0 needs Phase 1 to know beyond the 5 design questions captured in § Conductor asks. PLAN.md § Phase 1 already covers Phase 1's responsibilities exhaustively.

Layer 4.dsl.04 expectation depends on Phase 2's read of how the bridge handles `condition_target` supplied alongside an unrelated operator on the same record op (e.g. `add_keywords`). The matrix locks an expectation (silently ignored at op level when no conditions sub-operator is present; consumed when one is) but Phase 2 confirms and may flip.

Layer 2.02 framing assumes the request shape supports "two ops, one record" — i.e. a single `mo2_create_patch` call carrying two op blocks targeting the same FormID, one with `condition_target: "dialog"` and one with `condition_target: "event"`. PLAN § B's "structural limitation worth surfacing" footnote acknowledges this but doesn't lock the supportability. Phase 2's first inline smoke confirms; if rejected at request-shape level, 2.02 reframes as "two `mo2_create_patch` invocations" without changing the test intent.

## Conductor asks

Five design questions awaiting Aaron's lock via the conductor relay. Phase 1 doesn't open until all five are locked. Phase 0 proposes a default for each; Aaron locks via the conductor's relay. Format per PLAN.md § Communicating with the conductor (lines 55–71).

```
CONDUCTOR ASK
Phase: 0
Topic: Q1 — `condition_target` placement
Context:
  - PLAN § B names two options: operator-level (sibling field on the per-record op, applies to entire add/remove list) vs entry-level (each ConditionEntry carries its own).
  - Phase 0 proposal biases operator-level per Aaron's "operator-level if it works" steer.
  - Operator-level rationale: single logical group; simpler validation; narrower dispatch site; symmetric with `remove_conditions` (entry-level remove with index-based removal would be ambiguous — index relative to which list?).
Question: Is `condition_target` placed at operator-level (on `ScopeOps`) or entry-level (on `ConditionEntry`)?
Suggested options:
  A. Operator-level — one `condition_target` per `add_conditions` / `remove_conditions` op. Aaron's steer + Phase 0 default.
  B. Entry-level — each ConditionEntry carries its own `condition_target`. Allows mixed dialog+event in a single op; entry-level remove ambiguous on index basis.
Default if no response in 24h: A (operator-level).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q2 — parameter naming
Context:
  - PLAN § B baseline: `condition_target`. Alternatives: `target_list`, `list_target`, `conditions_on`.
  - `condition_target` reads cleanly alongside `add_conditions` ("condition target = where these conditions go").
  - Alternatives have ambiguity (`target_list` could be leveled-list-merge target) or awkward order (`list_target` reverses natural noun phrase).
Question: What name does the parameter take?
Suggested options:
  A. `condition_target` — Phase 0 default per PLAN § B baseline.
  B. `target_list` — shorter; ambiguous with leveled-list operator surface.
  C. `list_target` — reversed noun-phrase order; less natural read.
  D. `conditions_on` — verbose; preposition-form less consistent with peer operator-parameter names.
Default if no response in 24h: A (`condition_target`).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q3 — default behavior on QUST when condition_target is omitted
Context:
  - PLAN § A names two postures: error explicitly (Phase 0 default) vs implicit-default to one list (e.g. always DialogConditions).
  - "Erroring is safer" — picking a default silently mis-routes conditions intended for the other list.
  - The error message names the parameter and lists valid targets (per § C #2 wording).
Question: When a caller invokes `add_conditions` / `remove_conditions` against a QUST without supplying `condition_target`, does the bridge error or pick a default?
Suggested options:
  A. Error explicitly — new per-record error per § C #2 naming the missing parameter and available targets. Phase 0 default per § A "explicit choice required."
  B. Implicit-default to DialogConditions — silently routes; risk of mis-routing event-intended writes.
  C. Implicit-default to EventConditions — symmetric to B; same mis-routing risk in opposite direction.
Default if no response in 24h: A (error).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q4 — non-QUST records receiving condition_target
Context:
  - PLAN § A names two postures: ignore (no-op, condition lands in `Conditions`) vs reject (explicit error).
  - Phase 0 proposes reject — symmetric with v2.9.0 footgun-guard discipline (`*Unused*Parameter*` slot rejection); ambiguity surfaced not absorbed.
  - Ignore is more permissive (matches "DSL flexibility" posture).
Question: When a non-QUST record (PERK / PACK / IDLE / MGEF / INFO) receives `condition_target`, does the bridge ignore it or reject?
Suggested options:
  A. Reject — explicit error: "Record type Perk does not support condition_target. PERK uses a single Conditions list — omit condition_target." Phase 0 default per § A footgun-guard symmetry.
  B. Ignore — silent no-op; condition lands in the single `Conditions` list as if the parameter wasn't supplied. More permissive.
Default if no response in 24h: A (reject).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q5 — case sensitivity for condition_target value
Context:
  - PLAN § B notes case-insensitive matches v2.9.0 enum-parse posture (`Enum.Parse(propType, value, ignoreCase: true)`).
  - Case-sensitive matches schema literal posture (the JSON value is a literal string, not an enum name; mismatched case is a typo).
  - v2.9.1 mechanism is enum-like (the value maps to one of {dialog, event} — a closed set), favoring the v2.9.0-precedent posture.
Question: Is `condition_target: "Dialog"` accepted as a synonym for `"dialog"`?
Suggested options:
  A. Case-insensitive — `"Dialog"`, `"DIALOG"`, `"dialog"` all map to DialogConditions. Phase 0 default per v2.9.0 enum-parse symmetry.
  B. Case-sensitive — only `"dialog"` (lowercase canonical) accepted; `"Dialog"` errors per § C #3 ("Unknown condition_target").
Default if no response in 24h: A (case-insensitive).
```

## Preconditions for Phase 1

Phase 1's responsibilities (per PLAN.md § Phase 1):

- Schema probe in `tools/race-probe/Program.cs` (extends existing v2.9.0 P4 probe section) — sweep every concrete `Mutagen.Bethesda.Skyrim.I*Getter` interface for properties whose name ends in `"Conditions"` (case-insensitive); specifically dump QUST and confirm exactly DialogConditions + EventConditions.
- Generality lock proposal to Aaron via conductor; lock returned via Phase 2 kickoff.
- Update MATRIX.md Layer 1.P + 1.D rows post-lock per the § Phase fill-in checklists at the bottom of MATRIX.md.

| Precondition | State |
|---|---|
| `tools/race-probe/Program.cs` editable + builds clean as-is | ✅ presumed (existing v2.9.0 P4 artifact; Phase 1's first step is to confirm with `cd tools/race-probe && dotnet build -c Release`) |
| MATRIX.md exists with Layer 1.P / 1.D / 2 / 3 / 4 / 5 scaffold + naming convention | ✅ landed in this commit |
| MATRIX.md § Phase fill-in checklists enumerate exact post-Phase-N edits | ✅ landed at MATRIX.md bottom (3 checklists: Phase 1 / Phase 2 / Phase 3 hand-back) |
| Conductor decisions inherited (slug=v2.9.1, operator-level steer for Q1, QUST-only generality scope, no scope absorption) | ✅ recorded above |
| PLAN.md + CONDUCTOR_KICKOFF.md committed in `4cf417e` and readable | ✅ |
| 5 design questions awaiting Aaron lock | ✅ posted in § Conductor asks above |
| v2.9.0 PHASE_2C_HANDOFF.md / PHASE_4_INFO_HANDOFF.md available as schema-probe + child-DeepCopy reference | ✅ (`dev/plans/v2.9.X_condition_parameters/`) |

**Phase 1 cannot open** until Aaron locks all 5 design questions via the conductor relay. The locks are inputs to Phase 1's kickoff prompt (which restates them as authoritative for Phase 1's executor to transcribe). If any lock is undecided when Phase 1 needs to open, the conductor either holds Phase 1 or spawns it with the Phase-0-default and a "lock-pending" annotation.

## Files of interest for Phase 1

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PLAN.md` § Phase 1 | Authoritative steps + § Conductor decisions for Phase 1 |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/MATRIX.md` § Phase fill-in checklist (Phase 1 hand-back) | Exact rows Phase 1 lands post-probe + post-generality-lock |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/MATRIX.md` § Phase fill-in checklist | v2.9.0 reference for the format Phase 1 hand-back follows |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/PHASE_2C_HANDOFF.md` | Mutagen schema probe reference shape (P2C surfaced the GetEventData mixed-shape via similar reflection sweep) |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/PHASE_4_INFO_HANDOFF.md` | "child major nested under organizational GRUP parent" reference shape — relevant only if Phase 1's probe surfaces nested-condition record types (alias-level / stage-level conditions) and Aaron decides scope handling |
| `Claude_MO2/tools/race-probe/Program.cs` | Schema probe extension target (append after existing v2.9.0 P4 sections) |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (`ApplyAddConditions` line 1573 + `ApplyRemoveConditions` line 2262) | The reflection lookup call sites Phase 2 will extend; Phase 1 reads to confirm the current property-lookup pattern that `ResolveConditionListProperty` will generalize |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry + `Claude_MO2/KNOWN_ISSUES.md` line 42 | Standard dev-startup orientation per `feedback_dev_startup.md`; KNOWN_ISSUES line 42 is the carry-over entry Phase 2 will move to "covered" |

## Acceptance — Phase 0

Per CONDUCTOR_KICKOFF prompt § Acceptance criteria:

- ✅ `MATRIX.md` exists with four-layer scaffold (six layers: 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention. Per-list-target rows are placeholders awaiting Phase 1's probe-confirmed FormID.
- ✅ Layer 3 scenarios named (3.1 dialog perk-gating, 3.2 event perk-gating) with use-case descriptions; live-FormID picks deferred to Phase 3.
- ✅ `git diff 4cf417e` shows: MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched. (PLAN.md + CONDUCTOR_KICKOFF.md already at `4cf417e`.)
- ✅ Working version slug `v2.9.1` recorded in handoff (above § Working version slug).
- ✅ § Conductor asks populated with the 5 design questions in the agreed format (PLAN.md § Communicating with the conductor).
