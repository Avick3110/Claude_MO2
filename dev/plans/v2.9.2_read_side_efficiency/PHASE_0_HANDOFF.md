# Phase 0 Handoff — Plan + matrix scaffold + design proposal

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-28
**Session length:** ~1h
**Commits made:** `46c8474` (work) + this hash-record commit
**Live install synced:** No (Phase 0 is docs-only; live remains at v2.9.1 per CLAUDE.md exemption — Phase 0 doesn't touch the live install or invoke MCP tools)

## Working version slug

**`v2.9.2`** — confirmed at conductor kick-off (per CONDUCTOR_KICKOFF.md § Conductor decisions; restated in PHASE_0 kick-off prompt § Conductor decisions: "Slug: `v2.9.2` confirmed"). Phase 2 commits the version-bump constants (`config.py`, `.iss`, `README.md`); Phase 0 records the slug in this handoff only.

Plan dir name (`v2.9.2_read_side_efficiency/`) matches the slug — no rename pending.

## Conductor decisions inherited (locked)

These are pre-litigated and carry forward to Phase 1's executor without re-debate:

1. **Version slug = `v2.9.2`** (above).
2. **Q1–Q6 design questions are NOT pre-decided.** Aaron wants all six surfaced formally at Phase 0 hand-off; conductor relays to Aaron for explicit lock; Phase 1 doesn't open until the lock is in. Phase 0's role is to surface, not decide. Defaults are proposed below per PLAN § B–H rationales; Aaron can hold defaults or flip any.
3. **Single-mechanism scope.** v2.9.2 is three composable optional parameters on `mo2_record_detail`: `formids`, `fields`, `expand_links`. No other tool changes, no other bridge command additions, no `RecordReader.RenderValue` changes beyond the projection + expansion hooks. PERK.Effects = v2.9.3. v2.9.x carry-overs (Boolean dispatcher branch, sub-B 6 String functions, AMMO enchantment, replace-semantics dict, chained dict access, QUST.Aliases-Stages-Objectives, GetVATSValueUnknown Mutagen gap, etc.) stay deferred.
4. **Scope absorption posture.** Aaron is open to expansion for easy wins or where it makes sense. Phase 0 surfaces no candidates for absorption — the scoping session's PLAN already covers what's in scope. Phase 1+ executors fold in only load-bearing bonus catches per § H "Bonus-catch precedent" (>1 h additional or new operator surface → halt + conductor ask).
5. **Single-commit deliverable for Phase 0 per PLAN § I.** This Phase 0 commit force-adds PLAN.md + MATRIX.md + CONDUCTOR_KICKOFF.md + PHASE_0_HANDOFF.md in one work commit + one hash-record commit pair — distinct from prior plans' staged-each-artifact pattern. The scoping session that wrote PLAN.md + CONDUCTOR_KICKOFF.md left them untracked; Phase 0 force-adds them alongside the new MATRIX + handoff.

## What was done

- **`<plan>/MATRIX.md`** — NEW. Six-layer scaffold (Layer 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention + per-phase fill-in checklists. Mirrors v2.9.1's MATRIX.md structurally; anchored on **read-side efficiency for `mo2_record_detail`** (per-axis coverage rows in Layer 1.P, new strict-batch validation error paths in Layer 1.D, cross-axis composition in Layer 2, live workflow scenario for the consumer's 168-record case in Layer 3, parameter-value-form edges in Layer 4.dsl, full v2.9.1 regression band in Layer 5). 258 lines. Per-axis rows are placeholders awaiting Phase 1's record-shape sweep + perf probe; Layer 1.P expandable if Phase 1 + Aaron lock additional in-scope record types via the `1.P.expand.<Type>.<sub>` pattern.
- **Cell-ID convention** documented at MATRIX.md § Cell-naming convention:
  - `1.P.<axis>.<RecordType>[.<sub>]` — Layer 1 positives, anchored on axis (`batch` / `fields` / `expand`) + carrier record type (e.g. `1.P.batch.RACE`, `1.P.fields.RACE.list`, `1.P.expand.RACE.formlink`)
  - `1.D.<NN>` — Layer 1.D negatives + new explicit error paths (e.g. `1.D.01` for unknown field path validation; `1.D.07` for `formids` × `plugin_names` mutual-exclusion)
  - `2.<NN>` — Layer 2 combinatorial (cross-axis composition)
  - `3.<N>` — Layer 3 workflow scenarios (`3.1` consumer 168-record case mandatory; `3.2` NPC_ Factions optional)
  - `4.<sub>.<NN>` — Layer 4 edges (only `4.dsl.<NN>` sub-grouping needed for v2.9.2; v2.9.0/v2.9.1's other Layer 4 sub-groups don't apply because v2.9.2 doesn't change the per-Condition or per-FormLink build pipeline)
  - `5.<NN>` (or `5.range`) — Layer 5 regression (mapped 1:1 to v2.9.1's ~400 cells)
- **Layer 1.P pre-spec'd** with 7 cells covering each of the three axes' primary success paths: `1.P.batch.QUST` (formids on v2.9.1 carrier — composition coverage), `1.P.batch.RACE` (formids on consumer-signal anchor), `1.P.fields.RACE.scalar`, `1.P.fields.RACE.list`, `1.P.fields.RACE.nested`, `1.P.expand.RACE.formlink` (single FormLink), `1.P.expand.RACE.list` (list of FormLinks — consumer-signal headline shape).
- **Layer 1.D pre-spec'd** with 7 cells covering the new strict-batch validation surface from PLAN § D + the per-record formid-resolution partial-failure surface from § E + the request-shape mutual-exclusion error from § H: `1.D.01` (unknown field path), `1.D.02` (unknown expand target), `1.D.03` (expand target exists but not FormLink-typed), `1.D.04` (multi-error accumulation across all three failure modes), `1.D.05` (mixed-type batch validation per-type), `1.D.06` (per-record formid resolution partial failure — top-level `success: true` with one bad entry's `success: false`), `1.D.07` (formids × plugin_names mutual-exclusion request-shape error). Wording for new error messages locks the shape; Phase 2 finalizes exact strings.
- **Layer 2 pre-spec'd** with 4 combinatorial cells: all three axes composed on single record type (`2.01`); all three + `resolve_links: true` recursive annotation per § F (`2.02`); mixed-type batch with cross-type-valid projection (`2.03`); single-record path with new parameters (`2.04` — verifies the `formid: "..."` code path composes with `fields` / `expand_links` per § A).
- **Layer 3 workflow scenario pre-spec'd** with use-case description + assertions + placeholder FormIDs for Phase 3:
  - **Scenario 3.1 — Consumer's 168-record case: batched read with projection + expansion.** Real-world AI-driven patcher reads ~168 RACE records via single batched `mo2_record_detail` call with projection on `<EditorID> + <Skeleton> + <ActorEffect> + ...` + expansion on `<ActorEffect>` (or whatever Phase 1 confirms canonical). Headline cell — exercises all three axes simultaneously on the consumer-signal scale (~168 records × ~5–15 inline expansions per record = ~1000+ second-tier round-trips collapsed to 0). Assertions: per-record envelope shape; projected paths only; wrapper-form expansion at named position; perf within Phase 1 projection ±20% on subprocess wall-clock + response token-count.
  - **Scenario 3.2 — NPC_ batch with faction expansion (optional, conditional on Phase 1 precondition).** Symmetric secondary on different record type — verifies auto-traversal-into-list-of-structs path per Q1 default (`Factions.Faction` reads as "the Faction sub-property of each Factions entry"). Run only if Phase 1 confirms NPC_ has the canonical Factions structure AND Authoria has ~50+ NPC_ records; otherwise skipped with reason.
  - Both scenarios use `resolve_links: true` to exercise the recursive annotation composition per § F.
- **Layer 4.dsl pre-spec'd** with 6 parameter-value-form edge cells: empty `formids: []` (`4.dsl.01`), empty `fields: []` (`4.dsl.02`), empty `expand_links: []` (`4.dsl.03`), auto-traversal on dict-typed property per Q1 lock (`4.dsl.04`), shape-preserving rendering of always-null projected field (`4.dsl.05`), missing-master expansion target with uniform wrapper-form rendering per Q2 lock rationale point 4 (`4.dsl.06`).
- **Layer 5 regression band** pointer recorded — single range row covering v2.9.1's ~400 coverage-smoke cells unchanged (Phase 2 confirms the actual baseline against `coverage-smoke/Program.cs`).
- **Total assertion count + harness output convention + skip-with-reason** sections mirror v2.9.1's MATRIX.md structurally (~26 matrix rows, ~440 harness cells total).
- **Per-phase fill-in checklists** (Phase 1 hand-back, Phase 2 hand-back, Phase 3 hand-back) document exactly which placeholders each subsequent phase replaces — Phase 1 substitutes canonical FormLink-typed property names + RACE/QUST anchor FormIDs + perf-number anchors + Layer 3 anchor record-type confirmation + Scenario 3.2 precondition check; Phase 2 confirms Layer 5 cell count + finalizes Layer 1.D validation-error JSON shape + finalizes error-message wording + lands Q1–Q6 expectation flips if any lock differs from Phase 0 defaults; Phase 3 picks live FormIDs + lands per-scenario PASS/FAIL + lands Scenario 3.2 in-scope-or-skip.
- **`<plan>/PLAN.md` and `<plan>/CONDUCTOR_KICKOFF.md` force-added** in this same commit. The scoping session that wrote them left them untracked; Phase 0's single-commit deliverable per PLAN § I bundles all four artifacts together.
- **`<plan>/PHASE_0_HANDOFF.md`** — NEW (this file).

No production code touched. No version bump. Single-commit deliverable: PLAN.md + MATRIX.md + CONDUCTOR_KICKOFF.md + PHASE_0_HANDOFF.md force-added together in `46c8474` (work) + this hash-record commit.

## Verification performed

Phase 0 has no test runs — it's structural scaffolding. Verification = the structural mirror of v2.9.1's MATRIX.md adapted for v2.9.2's per-axis anchor.

| Check | v2.9.1 | v2.9.2 (this matrix) | Match |
|---|---|---|---|
| Header + methodology block | lines 1–11 | lines 1–11 | ✅ (anchor shifted to read-side efficiency) |
| Layer numbering | 1.P (per-list-target) + 1.D (explicit error paths) + 2 + 3 + 4 (dsl only) + 5 | 1.P (per-axis) + 1.D (strict-batch + partial-failure + mutual-exclusion) + 2 + 3 + 4 (dsl only) + 5 | ✅ (anchor shifted from list target to axis; Layer 4 sub-group set carried forward — only `dsl` because v2.9.2 doesn't change build pipelines) |
| Cell-ID convention documented | explicit § Cell-naming convention table | explicit § Cell-naming convention table | ✅ (different anchor — `1.P.<axis>.<RecordType>[.<sub>]` vs `1.P.<op>.<target>.<RecordType>`) |
| Per-row columns (axis / type / source / operation / expected) | yes | yes | ✅ |
| Layer 3 workflow scenarios | 2 scenarios pre-spec'd; live FormIDs deferred to Phase 3 | 1 mandatory + 1 optional scenario pre-spec'd; live FormIDs deferred to Phase 3 | ✅ (1+1 vs 2 — count differs but pattern preserved; Scenario 3.2 conditional on Phase 1 record-shape precondition) |
| Total assertion count section | yes (~400 harness cells) | yes (~440 harness cells; Layer 5 regression carries the bulk) | ✅ |
| Harness output convention | yes | yes (mirrors v2.9.1 example block) | ✅ |
| Skip-with-reason convention | yes (focused on QUST anchor fixture availability) | yes (focused on RACE anchor fixture availability) | ✅ |
| Phase fill-in checklists | three (Phase 1 + Phase 2 + Phase 3 hand-backs) | three (Phase 1 + Phase 2 + Phase 3 hand-backs) | ✅ |

State checks passed at session start:

- `git log -1 --oneline origin/main` → top hash `172ab26 [v2.9.1 post-ship] KNOWN_ISSUES.md staleness audit + cache-hygiene quirk entry` ✅ (matches kick-off prompt's "v2.9.1 ship commit" anchor — v2.9.1 ship is `172ab26` post-ship docs commit, the canonical baseline).
- `git status` → clean working tree, untracked files: `dev/plans/v2.9.2_read_side_efficiency/PLAN.md` + `dev/plans/v2.9.2_read_side_efficiency/CONDUCTOR_KICKOFF.md` (from scoping session — Phase 0 force-adds these alongside the new MATRIX + handoff per PLAN § I) ✅.
- `mo2_ping` skipped per CLAUDE.md exemption recorded in kick-off prompt § "CLAUDE.md exemption" — Phase 0 is doc/matrix scaffolding only, no MCP tool dependence; subagent of conductor session that also doesn't have `mo2_*` tools.

## Bugs surfaced

N/A. Phase 0 is scoping-only.

## Deviations from plan

None. Phase 0 ran exactly as PHASE_0 kick-off prompt and PLAN.md § Phase 0 specified. Cell-ID convention adapted to v2.9.2's per-axis anchor (`1.P.<axis>.<RecordType>[.<sub>]` — Phase 0 prerogative, defensible: anchors on the v2.9.2 unit of work which is the axis, not the list target v2.9.1 anchored on).

Layer 1.P count = 7 cells (vs PLAN § Phase 0 step 2 listing 7 cells: batch.QUST, batch.RACE [implicit anchor], fields.scalar, fields.list, fields.nested, expand.formlink, expand.list — matches).

Layer 1.D count = 7 cells (vs PLAN § Phase 0 step 2 listing 7 cells `1.D.01`–`1.D.07` — matches).

Layer 2 count = 4 cells (vs PLAN § Phase 0 step 2 listing 4 cells `2.01`–`2.04` — matches).

Layer 4.dsl count = 6 cells (vs PLAN § Phase 0 step 2 listing 6 cells `4.dsl.01`–`4.dsl.06` — matches).

Layer 3 scenario count = 1 mandatory + 1 optional (vs PLAN § Phase 0 step 2 mentioning "1 scenario mirroring the consumer's 168-record case" + "Optional 2nd scenario: NPC_ batch with expansion on `Factions.Faction`" — matches).

Q-numbering: PLAN § Phase 0 step 4 lists 6 design questions (Q1 path syntax, Q2 expansion shape, Q3 partial-failure formid lookup, Q4 validation timing, Q5 capacity caps, Q6 mutual-exclusion); kick-off prompt restates them as Q1–Q6 in identical ordering. Phase 0 surfaces all six in § Conductor asks below in the agreed bullet format. Note: PLAN.md § Phase 0 opening paragraph at one point references "design questions to Aaron via the conductor: Q1 path syntax... Q5 ... Q6 ..." — six questions total despite the literal phrase "5 design questions" appearing in v2.9.1's prior PHASE_0_HANDOFF.md (which had 5 Q's; v2.9.2 has 6 because the read-side mechanism's open-question surface is wider). The "6" count is authoritative per kick-off prompt § Q1–Q6.

## Known issues / open questions

None Phase 0 needs Phase 1 to know beyond the 6 design questions captured in § Conductor asks. PLAN.md § Phase 1 already covers Phase 1's responsibilities exhaustively.

Layer 1.D.04 expectation depends on Phase 2's read of how multi-error accumulation surfaces JSON-key-wise (§ D's locked structural contract is "validation_errors keyed by record type, three categories per type, valid-name lists per category context"; the literal JSON key names are Phase 2 finalization). The matrix locks the shape; Phase 2 confirms wording.

Layer 2.04 single-record-path response shape assumes the v2.9.1 single-record shape carries through with new parameters (per § A "Single-record path (`formid: "..."`) … Composes with `fields` / `expand_links`"). Phase 2's first inline smoke confirms; if the implementation routes single-`formid` + new parameters through the per-record envelope shape instead, 2.04 expectation flips and the matrix updates per Phase 2 hand-back checklist.

Layer 4.dsl.04 dict-typed-property carrier is RACE only at Phase 0 baseline. Phase 1's record-shape sweep confirms RACE has a dict-typed property worth anchoring on; if not, Phase 1 substitutes a different record type or recommends deleting the cell (auto-traversal-on-dict semantics tested elsewhere via list-typed coverage).

## Conductor asks

Six design questions awaiting Aaron's lock via the conductor relay. Phase 1 doesn't open until all six are locked. Phase 0 proposes a default for each per PLAN § B–H rationales; Aaron locks via the conductor's relay. Format per PLAN.md § Communicating with the conductor (lines 67–75).

```
CONDUCTOR ASK
Phase: 0
Topic: Q1 — Path syntax for fields and expand_links
Context:
  - PLAN § B names two options: auto-traversal (Effects.BaseEffect — walker auto-descends into lists/dicts) vs explicit bracket-empty (Effects[].BaseEffect — explicit "all elements" sentinel; requires extending ParsePathSegment to accept empty-bracket on intermediate segments, breaking set_fields's final-segment-only invariant).
  - Phase 0 default per § B rationale: read-side semantics differ from write-side; set_fields brackets are for dict keys; auto-traversal aligns with MongoDB/GraphQL/JSONPath conventions; cleaner mental model (Effects.BaseEffect reads naturally as "the BaseEffect of each Effect"); generalizes to dicts cleanly.
  - Alternative would matter if a future read-side use case needs to disambiguate "this specific list element" vs "all elements" — no such use case in v2.9.2 scope.
Question: How do fields and expand_links express "all elements of a list-typed property"?
Suggested options:
  A. Auto-traversal — dot-segmented paths only; walker auto-descends into lists/dicts mid-path. Phase 0 default per § B rationale points 1–4.
  B. Explicit bracket-empty — Effects[].BaseEffect; requires extending ParsePathSegment; breaks set_fields's final-segment-only invariant if parser shared.
Default if no response in 24h: A (auto-traversal).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q2 — Expansion output shape
Context:
  - PLAN § C names two shapes: wrapper form ({formid, EditorID, expanded: {...}} per FormLink position — backward-compat-friendly, FormID stays visible at wrapper level) vs replace-with-inlined-dict (just the linked record's detail, no wrapper key — lighter shape but loses FormID annotation outside the inlined record's own top-level fields).
  - Phase 0 default per § C rationale: wrapper form because (1) the FormID is the load-bearing identifier the caller used to ask for the expansion; (2) resolve_links composition is cleaner with explicit wrapper formid field; (3) schema discoverability — caller can tell "this is an expanded link" from shape; (4) symmetric with null-link rendering and missing-master rendering.
  - Alternative would matter if Phase 1's payload-size measurement shows the wrapper overhead is significant on small expansions — Phase 1's perf probe should annotate; auto-flip if wrapper overhead is >20% of expansion payload.
Question: When expand_links inlines a linked record's detail, does the response use wrapper form or replace-with-inlined-dict?
Suggested options:
  A. Wrapper form — { formid, EditorID, expanded: {...} } per FormLink position. Phase 0 default per § C rationale points 1–4.
  B. Replace-with-inlined-dict — just the linked record's detail; no wrapper key.
Default if no response in 24h: A (wrapper form).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q3 — Per-record formid-lookup partial failure
Context:
  - PLAN § E names two postures: per-record success/error envelope (top-level success: true; per-record success: true/false; matches existing read_records multi-plugin precedent at _handle_record_detail) vs strict-batch fail-the-whole-call (top-level success: false; consistent with Q4 = pre-flight on field paths).
  - Phase 0 default per § E rationale: per-record success/error matches the existing precedent; strict-batch is justified on fields/expand_links because those are batch-wide parameters but per-record formid resolution is inherently per-record (some succeed, some don't); both surfaces let the caller fix in one round-trip but at different granularity.
  - Alternative would matter if a real consumer needs strict-batch atomicity on formid resolution (e.g. "give me all 168 or none — partial responses confuse the patcher"). Tester signal doesn't indicate this preference.
Question: When formids: [a, b, c] is supplied and 'b' doesn't resolve, does the response return per-record envelope (a: detail, b: error, c: detail) or fail the whole call?
Suggested options:
  A. Per-record success/error envelope — top-level success: true; records[1].success: false; records[0].success and records[2].success: true with detail. Phase 0 default per § E precedent match.
  B. Strict-batch — top-level success: false; no records returned even for valid formids; caller fixes the bad formid and re-batches.
Default if no response in 24h: A (per-record success/error envelope).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q4 — Validation timing for fields and expand_links
Context:
  - PLAN § D names two timings: bridge-side pre-flight (validate fields/expand_links against the record type before any walking; reject strict-batch if any path is bad — multi-error accumulation per § D pseudocode) vs lazy mid-walk (start walking; fail at the point a bad path resolves; first-failure-wins or accumulate-during-walk).
  - Phase 0 default per § D rationale: pre-flight because (1) strict-batch error contract requires multi-error accumulation upfront — lazy mid-walk would surface failures non-deterministically depending on JSON property iteration order; (2) cheap to validate against the type's reflected property set before any expensive Mutagen overlay reads; (3) cleanest UX — caller fixes the entire bad list in one round-trip; (4) symmetric with v2.9.1 condition_target validation (rejected at slot lookup before any list write).
  - Alternative would matter if pre-flight validation overhead is measurable on large batches — Phase 1's perf probe should annotate validation cost; if non-trivial vs read cost, lazy may be better.
Question: When fields or expand_links is supplied, does the bridge validate paths pre-flight or lazy mid-walk?
Suggested options:
  A. Pre-flight — validate every path against the record type's reflected property set before any walking; reject strict-batch with multi-error accumulation if any bad. Phase 0 default per § D rationale points 1–4.
  B. Lazy mid-walk — start walking; fail at point a bad path resolves; either first-failure-wins or accumulate-during-walk.
Default if no response in 24h: A (pre-flight).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q5 — formids capacity caps
Context:
  - PLAN § H names two postures: unbounded (default — trust the caller; document the tested batch sizes from Phase 1's perf probe in the schema description) vs soft cap at the Python wrapper layer (e.g. 500 or whatever Phase 1 measures as the "graceful upper bound" before subprocess wall-clock or memory pressure becomes an issue).
  - Phase 0 default per § H rationale: unbounded — the consumer's 168-record case fits comfortably in any reasonable cap; soft caps are footgun-guard-only and add a cap-exceeded error path for marginal benefit; the tested-batch-size note in the schema description (e.g. "Tested up to 200 records per call.") gives callers a soft guidance without a hard reject.
  - Alternative would matter if Phase 1's perf probe surfaces a cliff (e.g. 1000-record batches OOM or exceed timeout); soft cap protects the caller from accidentally tripping the cliff. v2.9.x candidate to add post-release if real consumer reports.
Question: Does formids accept unbounded list size or a soft cap (e.g. 500) at the Python wrapper layer?
Suggested options:
  A. Unbounded — trust the caller; document tested batch sizes in schema description. Phase 0 default per § H rationale.
  B. Soft cap — reject with explicit error if formids count exceeds {N}; cap value tunable per Phase 1's measured cliff.
Default if no response in 24h: A (unbounded).
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q6 — Mutual-exclusion of formids vs plugin_names
Context:
  - PLAN § H + the Architecture table at PLAN § A name two postures: enforce XOR at the Python wrapper layer (matches existing plugin_name vs plugin_names exclusivity at tools_records.py:875 — clean precedent symmetry) vs allow combination (would mean: each plugin × each formid → N×M batch matrix; semantically distinct from formids alone since plugin_names is multi-plugin diff for one record while formids is multi-formid batch).
  - Phase 0 default per § H rationale: enforce XOR — the cross-product use case is rare and architecturally distinct (multi-plugin diff vs multi-formid batch are different mental models); allowing combination would add a 2×N×M dispatch matrix Phase 2 has to wire; the tester's signal doesn't include cross-product need.
  - Alternative would matter if a real consumer surfaces a cross-product need — e.g. "I want every override of these 168 RACE FormIDs across these 5 plugins all in one call." Possible v2.9.x candidate if it materializes.
Question: Are formids and plugin_names mutually exclusive (XOR) or composable (cross-product)?
Suggested options:
  A. Enforce XOR — request-shape error 1.D.07 fires if both supplied. Phase 0 default per § H rationale + plugin_name vs plugin_names precedent at tools_records.py:875.
  B. Allow combination — N×M cross-product batch; Phase 2 wires the dispatch matrix; new combinatorial surface in Layer 2.
Default if no response in 24h: A (enforce XOR).
```

## Preconditions for Phase 1

Phase 1's responsibilities (per PLAN.md § Phase 1):

- Perf probe extension in `tools/race-probe/Program.cs` — five measurement axes per § G: subprocess startup cost, per-record marginal cost (batch 1 / 5 / 20 / 50 / 100 / 200), per-record full-detail payload baselines per record type, projection payload-size impact ratio, expansion round-trip elimination ratio.
- Record-shape sweep section — reflect over every concrete `IMajorRecordGetter`-implementing interface in `Mutagen.Bethesda.Skyrim` 0.53.1; enumerate every FormLink-typed property (single + list); confirm canonical RACE FormLink-field names; resolve `ActorEffect` vs `ActorEffects` against Mutagen 0.53.1 ground truth.
- Threshold-acceptance proposal to conductor; auto-accept if numbers within band; escalate to Aaron if dramatically off.
- Layer 3 anchor record-type recommendation (RACE is Phase 0 baseline; Phase 1 confirms or proposes alternative based on Authoria's actual record-type counts + FormLink-chase pattern fit).
- Update MATRIX.md Layer 1.P / 1.D / 2 / 4 rows post-lock per the § Phase fill-in checklists at the bottom of MATRIX.md (substitute placeholder property names + RACE/QUST anchor FormIDs + perf-number anchors).

| Precondition | State |
|---|---|
| `tools/race-probe/Program.cs` editable + builds clean as-is | ✅ presumed (existing v2.9.1 P1 artifact; Phase 1's first step is to confirm with `cd tools/race-probe && dotnet build -c Release`) |
| MATRIX.md exists with Layer 1.P / 1.D / 2 / 3 / 4 / 5 scaffold + naming convention | ✅ landed in this commit |
| MATRIX.md § Phase fill-in checklists enumerate exact post-Phase-N edits | ✅ landed at MATRIX.md bottom (3 checklists: Phase 1 / Phase 2 / Phase 3 hand-back) |
| Conductor decisions inherited (slug=v2.9.2, single-mechanism scope, no scope absorption, single-commit deliverable for Phase 0 per § I) | ✅ recorded above |
| PLAN.md + CONDUCTOR_KICKOFF.md committed in `46c8474` (this commit's force-add) and readable | ✅ |
| 6 design questions awaiting Aaron lock | ✅ posted in § Conductor asks above |
| v2.9.1 PHASE_2_HANDOFF.md / PHASE_4_HANDOFF.md / PHASE_5_HANDOFF.md available as reference for Phase 2 wrapper-passthrough discipline + Phase 5 ship cadence | ✅ (`dev/plans/v2.9.1_quest_condition_disambiguation/`) |

**Phase 1 cannot open** until Aaron locks all 6 design questions via the conductor relay. The locks are inputs to Phase 1's kick-off prompt (which restates them as authoritative for Phase 1's executor to transcribe). If any lock is undecided when Phase 1 needs to open, the conductor either holds Phase 1 or spawns it with the Phase-0-default and a "lock-pending" annotation.

## Files of interest for Phase 1

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PLAN.md` § Phase 1 | Authoritative steps + § Conductor decisions for Phase 1 |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` § Phase fill-in checklist (Phase 1 hand-back) | Exact rows Phase 1 lands post-probe + post-record-shape-sweep |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/MATRIX.md` § Phase fill-in checklist | v2.9.1 reference for the format Phase 1 hand-back follows |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_1_HANDOFF.md` | Reference shape for Phase 1's perf-and-shape probe section + record-shape sweep table format |
| `Claude_MO2/tools/race-probe/Program.cs` | Probe extension target (append after existing v2.9.1 P1 sections); Phase 1 also reads existing v2.9.1 sections to understand the pattern for both perf-probe and reflection-sweep blocks |
| `Claude_MO2/tools/mutagen-bridge/RecordReader.cs` (existing `Read` + `ReadBatch` methods) | Phase 1 reads to understand the current per-record render path that Phase 2's projection + expansion hooks integrate into; informs Phase 1's record-shape sweep predicate (FormLink type matchers per `PatchEngine.cs:1182`) |
| `Claude_MO2/tools/mutagen-bridge/Models.cs` (existing `ReadRequest` + `ReadBatchRequest`) | Phase 1 reads to understand the current request model surface; Phase 2 extends with `Fields` + `ExpandLinks` |
| `Claude_MO2/mo2_mcp/tools_records.py` (existing `_handle_record_detail`, lines around 875 for `plugin_name` vs `plugin_names` exclusivity precedent) | Phase 2's Python wrapper extension reference; Phase 1 may skim to understand the existing batch precedent at `_handle_record_detail` |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.1) + `Claude_MO2/KNOWN_ISSUES.md` § "Patching write surface — current limitations" | Standard dev-startup orientation per `feedback_dev_startup.md`; v2.9.1's CHANGELOG documents the recent Quest condition disambiguation context; KNOWN_ISSUES § Patching documents the carry-over inventory v2.9.2 doesn't touch (read-side mechanism is orthogonal to write-side carry-overs) |

## Acceptance — Phase 0

Per CONDUCTOR_KICKOFF prompt § Acceptance criteria:

- ✅ `MATRIX.md` exists with five-layer scaffold (six layers: 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention mirroring v2.9.1's MATRIX shape. Per-axis rows are placeholders awaiting Phase 1's record-shape sweep + perf probe.
- ✅ Layer 3 scenarios named (3.1 consumer 168-record case mandatory; 3.2 NPC_ Factions optional, conditional on Phase 1 precondition) with use-case descriptions; live-FormID picks deferred to Phase 3.
- ✅ `git diff main^` (after work commit) shows: PLAN.md (NEW), MATRIX.md (NEW), CONDUCTOR_KICKOFF.md (NEW), PHASE_0_HANDOFF.md (NEW). No production code touched.
- ✅ Working version slug `v2.9.2` recorded in handoff (above § Working version slug).
- ✅ § Conductor asks populated with the 6 design questions in the agreed format (PLAN.md § Communicating with the conductor lines 67–75).
- ✅ Single work commit + single hash-record commit, both pushed (per PLAN § I single-commit deliverable lock + Conventions § "double-commit cadence per phase").
- ✅ Handoff under 400 lines.
