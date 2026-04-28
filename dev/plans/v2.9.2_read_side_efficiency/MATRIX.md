# v2.9.2 Verification Matrix

**Authoritative test specification for v2.9.2's verification pass.** Mirrors v2.9.1's MATRIX.md role for v2.9.0 → v2.9.1; this matrix serves the v2.9.1 → v2.9.2 transition. Anchored on **read-side efficiency for `mo2_record_detail`** (the v2.9.2 capability) — three composable optional parameters (`formids`, `fields`, `expand_links`) extending the existing tool surface, per PLAN.md § A. Bridge changes are confined to extending `ReadRequest` + `ReadBatchRequest` with new properties and integrating projection + expansion + pre-flight validation into the existing `Read` / `ReadBatch` per-record render path; no new bridge command, no new tool.

**Methodology.** Every cell is one bridge invocation (Mutagen-direct functional probe in `tools/race-probe/` for Layer 1 / 1.D / 2 / 4, end-to-end MCP→bridge round-trip in `coverage-smoke/` for the regression band, `mo2_record_detail` against the live Authoria modlist for Layer 3), with the listed input parameters against the listed source record(s), and a documented expected response shape. Layers 1 / 2 / 4 / 5 run via the existing test harnesses against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_record_detail` (read-only — no test patches written) against the live Authoria modlist at `<live>`.

**Record selection.** Layer 1 / 2 / 4 use the existing `coverage-smoke` `FirstOrDefault` predicate selection where possible. The canonical Layer 1.P anchor for the projection + expansion cells is **RACE** (matching the consumer's 168-record case framing per PLAN § Background) with `Skyrim.esm` as the source plugin; Phase 1's perf-and-shape probe confirms the canonical FormLink-typed property names (`Skeleton`, `ActorEffect` / `ActorEffects`, `Spells`, etc. — Phase 1 reads Mutagen 0.53.1 ground truth, not speculation). Layer 1.P also includes a QUST batch cell (`1.P.batch.QUST`) to exercise the formids path on the v2.9.1 carrier shape. Layer 3 anchors on the live Authoria modlist's RACE record set (or analogous record type if Phase 1 surfaces a better Authoria fit).

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in the appropriate phase's handoff, including the actual response payload.

**Phase fill-in cadence.** **Phase 0 (this commit)** lays down the layer scaffold + cell-naming convention + Layer 3 scenario use-case description (consumer's 168-record case). Phase 1's perf-and-shape probe surfaces the canonical FormLink-typed property names + per-record-type payload baselines + the Layer 3 anchor record-type recommendation; Phase 1 updates Layer 1.P / 1.D / 2 / 4 rows post-probe with confirmed property names and per-axis number anchors. Phase 2 runs the harness end-to-end after wiring `fields` / `expand_links` / `formids` in `Models.cs` + `RecordReader.cs` + `tools_records.py`. Phase 3 picks live FormIDs for the Layer 3 scenario(s).

---

## 🧭 Cell-naming convention

| Prefix | Layer | Pattern | Example |
|---|---|---|---|
| `1.P.<axis>.<RecordType>[.<sub>]` | 1 — per-axis positives | axis (`batch` / `fields` / `expand`) + carrier record type + optional sub-shape | `1.P.batch.RACE`, `1.P.fields.RACE.list`, `1.P.expand.RACE.formlink` |
| `1.D.<NN>` | 1.D — negatives + new explicit error paths | sequential within layer | `1.D.01`, `1.D.07` |
| `2.<NN>` | 2 — combinatorial | sequential | `2.01`, `2.04` |
| `3.<N>` | 3 — workflow scenarios | scenario number | `3.1`, `3.2` |
| `4.<sub>.<NN>` | 4 — edges | sub-grouping + sequential | `4.dsl.01`, `4.dsl.06` |
| `5.<NN>` | 5 — regression | range row, mapped to v2.9.1's ~400 cells (see § Layer 5) | `5.range` |

The `1.P.<axis>.<RecordType>[.<sub>]` form anchors on **axis** (the v2.9.2 unit of work — one of the three composable parameters) — not list target (v2.9.1's anchor) or function name (v2.9.0's). v2.9.2's mechanism dispatches via three orthogonal axes (batch invocation, projection walking, expansion-on-FormLink); per-axis coverage is the natural unit. `1.D.<NN>` carries v2.9.1's negative-band convention forward. Layer 4's only sub-grouping is `dsl` (parameter-value-form edges) — v2.9.0/v2.9.1's other Layer 4 sub-groups (`slot` / `formid` / `enum` / `compat` / `carry`) don't apply because v2.9.2 doesn't change the per-Condition or per-FormLink build pipeline; it only adds a render-side projection + a render-side expansion + a request-side batch.

**Per-axis carrier convention (Phase 0 baseline).** Layer 1.P's `batch` axis is exercised against both QUST (matches v2.9.1's anchor for cross-mechanism composition coverage — confirms `formids` path works when the records carry the disambiguation surface) and RACE (the consumer-signal anchor). Layer 1.P's `fields` and `expand` axes are exercised against RACE only at Phase 0 baseline; Phase 1's record-shape sweep confirms which other in-scope record types (NPC_, QUST, MGEF, PERK, ARMO, WEAP, SPEL — per PLAN § G #3) carry the FormLink shapes needed to add `1.P.expand.<Type>.<sub>` rows. Phase 1's record-shape sweep deliverable explicitly drives this expansion; if Phase 1 finds a record type with a clean single-FormLink-typed property and a clean list-of-FormLinks-typed property whose names the matrix should anchor on for documentation purposes, Phase 1 adds the row(s). Phase 0 keeps the matrix scaffold lean to avoid baking in Mutagen 0.53.1 property names that the probe must confirm.

**Source-of-truth for property names:** Phase 1's `tools/race-probe/Program.cs` record-shape sweep against `IMajorRecordGetter`-implementing interfaces in `Mutagen.Bethesda.Skyrim` 0.53.1. The matrix uses placeholder names in Phase 0 (`<Skeleton-or-confirmed-scalar>`, `<ActorEffect-or-confirmed-list>`); Phase 1 substitutes the canonical names against the probe output. v2.7.1's bridge code (`PatchEngine.cs:691`) named `ActorEffect` (singular); the task spec mentions `ActorEffects` (plural). Phase 1 confirms.

---

## Layer 1 — Per-axis coverage (positives)

**v2.9.2 in-scope axes:** batch (`formids: [...]`), projection (`fields: [...]`), expansion (`expand_links: [...]`). Seven cells exercise each axis's primary success path on canonical record shapes. Per-axis combinatorial composition lives in Layer 2.

Each row's expected result follows the shape:

> bridge response top-level `success: true`; per-axis assertion (batch: per-record envelope shape with N entries; projection: out-of-projection branches absent from response; expansion: wrapper form `{formid, EditorID, expanded}` per Phase 0 Q2 default at the named FormLink position); v2.9.1 single-record path bit-identical when only one new parameter is present (composability proven cell-by-cell).

### 1.P.batch — formids batch read

| # | Axis | Carrier | Operation | Expected |
|---|------|---------|-----------|----------|
| `1.P.batch.QUST` | `formids` | `Skyrim.esm` (3+ QUST records, e.g. `04C49D` / `0E3145` / a third Phase 1 picks) | `mo2_record_detail(formids: [<3 QUST FormIDs>])` | top-level `success: true`, top-level `records` is an array of 3 entries; each entry has `formid` matching the request, `success: true`, and a full v2.9.1-shape detail object; `records[].success` = `true` for all three; subprocess-startup amortization observable via wall-clock comparison vs three serial `formid: "..."` calls (Phase 1's perf probe quantifies; Phase 2's smoke just asserts shape) |
| `1.P.batch.RACE` | `formids` | `Skyrim.esm` (3+ RACE records, picked at Phase 1 from the schema sweep — anchor candidates: `NordRace` / `ImperialRace` / `RedguardRace` or whatever 3 Phase 1's probe surfaces with populated `<ActorEffect>` lists) | `mo2_record_detail(formids: [<3 RACE FormIDs>])` | symmetric to `1.P.batch.QUST`; per-record envelope shape with 3 entries; consumer-signal-anchor cell — Phase 3's Layer 3 scenario scales this to ~168 records on live |

### 1.P.fields — projection (RACE anchor)

| # | Axis | Carrier | Operation | Expected |
|---|------|---------|-----------|----------|
| `1.P.fields.RACE.scalar` | `fields` | `Skyrim.esm:<NordRace-or-Phase-1-anchor>` | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["EditorID"])` | response payload is shape-preserving but contains ONLY the projected paths — top-level dict has `EditorID` populated and no other top-level fields except framing metadata (FormID display, success flag); the rest of v2.9.1's full-detail payload is omitted. Verifies scalar-projection path |
| `1.P.fields.RACE.list` | `fields` | `Skyrim.esm:<RACE-anchor>` | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["<ActorEffect-or-confirmed-list-property>"])` | response contains `<ActorEffect>` (full list of FormLink entries — each rendered per existing v2.9.1 FormLink display rules, NOT yet expanded since this cell exercises projection only); other top-level fields omitted. Verifies list-projection: the walker descends into the list and renders every element, but does not auto-expand FormLinks |
| `1.P.fields.RACE.nested` | `fields` | `Skyrim.esm:<RACE-anchor>` | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["<NestedPath-Phase-1-confirms-e.g.-Stats.Health-or-equiv>"])` | response contains the nested-projected path with intermediate dict structure preserved — the walker auto-traverses per Q1 lock (Phase 0 default: auto-traversal). If the nested path resolves through a list-typed intermediate property, the response auto-traverses the list and returns a flattened or shape-preserving result per Phase 2's confirmed implementation. Phase 1's record-shape sweep picks the canonical RACE nested property for this cell |

### 1.P.expand — single-level FormLink expansion (RACE anchor)

| # | Axis | Carrier | Operation | Expected |
|---|------|---------|-----------|----------|
| `1.P.expand.RACE.formlink` | `expand_links` | `Skyrim.esm:<RACE-anchor>` | `mo2_record_detail(formid: "<RACE-FormID>", expand_links: ["<Skeleton-or-Phase-1-confirmed-single-FormLink>"])` | response's `<single-FormLink>` field renders as the wrapper form `{formid: "<Plugin>:<HexID>", EditorID: "<linked-record-EditorID>", expanded: { ...full RecordReader walk of the linked record... }}` per Q2 default (wrapper form); the linked record's interior FormLinks render as plain FormID strings (single-level lock per § H — no recursion). If `resolve_links: true` is also supplied, the wrapper's `formid` and the expanded content's FormIDs all annotate via `_enrich_formids`. Verifies the canonical expansion happy-path on a scalar-FormLink-typed property |
| `1.P.expand.RACE.list` | `expand_links` | `Skyrim.esm:<RACE-anchor>` | `mo2_record_detail(formid: "<RACE-FormID>", expand_links: ["<ActorEffect-or-confirmed-list-property>"])` | response's `<ActorEffect>` field renders as a list of wrapper-form objects, one per FormLink entry: `[{formid, EditorID, expanded}, ...]`; each entry's `expanded` payload is the full RecordReader walk of the linked SPEL (or whatever the linked record type is); single-level locked — interior FormLinks of each expanded SPEL render as plain FormID strings. Consumer-signal headline cell — Phase 3's Layer 3 scenario hits this on ~168 RACE records simultaneously via the batch axis composition |

---

## Layer 1.D — Negatives + new explicit error paths

Seven cells exercising the new strict-batch validation surface from PLAN § D + § E + the request-shape error from § H mutual-exclusion lock. Multi-error accumulation is the structural contract — the harness asserts the response surfaces ALL bad entries together, not first-failure-wins. Wording for the new error messages is finalized in Phase 2 implementation; this matrix locks the shape and the rollback contract.

| # | Axis | Setup | Expected |
|---|------|-------|----------|
| `1.D.01` | `fields` | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["BogusField"])` (path doesn't resolve to any property on RACE) | top-level `success: false`; `error: "Field path / expansion target validation failed."`; `validation_errors.RACE.bad_field_paths: ["BogusField"]`; `validation_errors.RACE.bad_expansion_targets: []`; `validation_errors.RACE.non_formlink_expansion_targets: []`; `validation_errors.RACE.valid_field_names: [...exhaustive list of RACE's top-level property names]`; rollback: no projected response is built (validation runs pre-flight per Q4 default = pre-flight) |
| `1.D.02` | `expand_links` | `mo2_record_detail(formid: "<RACE-FormID>", expand_links: ["BogusField"])` (target doesn't resolve) | symmetric to 1.D.01 with the path under `bad_expansion_targets` and `valid_formlink_field_names` populated instead of `valid_field_names`; rollback identical |
| `1.D.03` | `expand_links` | `mo2_record_detail(formid: "<RACE-FormID>", expand_links: ["EditorID"])` (target exists but not FormLink-typed) | top-level `success: false`; `validation_errors.RACE.non_formlink_expansion_targets: ["EditorID"]`; the response names `EditorID`'s actual type (string) and the list of valid FormLink-typed property names for RACE; rollback identical |
| `1.D.04` | `fields` + `expand_links` (multi-error accumulation) | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["BogusField", "AlsoBogus"], expand_links: ["EditorID", "<valid-FormLink-property>"])` (two bad fields, one bad-target-but-not-FormLink expand, one VALID expand) | top-level `success: false`; `validation_errors.RACE.bad_field_paths: ["BogusField", "AlsoBogus"]`; `validation_errors.RACE.non_formlink_expansion_targets: ["EditorID"]`; `validation_errors.RACE.bad_expansion_targets: []`; the valid expand is IGNORED in the validation phase but does NOT cause projection to run anyway — pre-flight rejects strict-batch per Q4 default. All three categories accumulate per § D pseudocode; one round-trip lets the caller fix all bad entries. Phase 2 confirms exact JSON shape; Phase 0 locks the structural contract: validation_errors keyed by record type, three categories per type, valid-name lists per category context |
| `1.D.05` | mixed-type batch validation | `mo2_record_detail(formids: [<QUST-FormID>, <RACE-FormID>], fields: ["DialogConditions", "<RACE-only-property>"])` (each path is valid for one type but not the other) | top-level `success: false`; `validation_errors.QUST.bad_field_paths: ["<RACE-only-property>"]`; `validation_errors.RACE.bad_field_paths: ["DialogConditions"]`; per-record-type accumulation per § D's per-type lock; the response names BOTH bad-path entries grouped by their respective failing type, with each type's valid-name list. Verifies cross-type strict-batch contract — a heterogeneous batch validates each unique type separately and aggregates errors per type |
| `1.D.06` | per-record formid resolution | `mo2_record_detail(formids: [<valid-FormID-A>, <bogus-FormID-that-does-not-resolve>, <valid-FormID-C>])` | top-level `success: true` (per § E lock — per-record success/error envelope, NOT strict-batch); top-level `records` array has 3 entries: `records[0].success: true` with detail; `records[1].success: false` with `error: "FormID not found in load order index"` (or equivalent — Phase 2 finalizes wording); `records[2].success: true` with detail. Caller fixes the bad formid in the next round-trip; the two valid records' detail is delivered in this round-trip per the existing `read_records` (multi-plugin) precedent |
| `1.D.07` | mutual-exclusion request-shape error | `mo2_record_detail(formids: ["<X>"], plugin_names: ["<Y.esm>"])` (both batch-shaped parameters supplied — Q6 default = enforce XOR) | top-level `success: false`; `error: "formids and plugin_names are mutually exclusive — supply one or the other."` (or equivalent — Phase 2 finalizes wording); the response cites the existing `plugin_name` vs `plugin_names` exclusivity precedent at `tools_records.py:875`. Verifies Q6 lock surfaces a clean request-shape error before any bridge invocation |

Phase 2 may add Layer 1.D rows programmatically if test patterns surface (e.g. a per-axis empty-list rejection variant for `formids: []` separate from the Layer 4.dsl coverage). The matrix locks the structural error-path coverage above; bulk-pattern derivatives are implementation choice for the harness.

---

## Layer 2 — Combinatorial probes

Cross-axis composition: all three v2.9.2 axes together, plus composition with the existing `resolve_links` axis, plus mixed-type batch with valid-across-types projection, plus single-record-path composition (verifies the v2.9.1 single-`formid` code path composes with the new parameters bit-identically when the new parameters are present individually).

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | All three axes composed (single record type) | `mo2_record_detail(formids: [<RACE-FormID-A>, <RACE-FormID-B>, <RACE-FormID-C>], fields: ["<projected-list-property>"], expand_links: ["<expanded-list-property>"])` | top-level `success: true`, `records` array with 3 entries; each entry's `success: true`; each entry's payload contains ONLY the projected path AND the expanded-link wrapper-shape entries at the expanded path; out-of-projection branches absent. Verifies each axis applies independently per record within one batch — projection narrows what the walker emits, expansion inlines named FormLinks within whatever the projection emits |
| `2.02` | All three axes + `resolve_links: true` (existing axis composition per § F) | `mo2_record_detail(formids: [<3 RACE FormIDs>], fields: ["<list-property>"], expand_links: ["<expanded-list-property>"], resolve_links: true)` | symmetric to 2.01; additionally, EVERY FormID-shaped string in the response — including the wrapper `formid`, the wrapper EditorID's parent FormID display, and every FormID-shaped string inside the expanded payload — is annotated to the `Plugin:HexID (EditorID)` shape via `_enrich_formids`. Verifies the existing `_enrich_formids` recursion handles the deeper expanded tree without v2.9.2 changes (orthogonal composition per § F) |
| `2.03` | Mixed-type batch with cross-type-valid projection | `mo2_record_detail(formids: [<QUST-FormID-1>, <QUST-FormID-2>, <RACE-FormID-1>, <RACE-FormID-2>], fields: ["EditorID"])` (`EditorID` is valid on every IMajorRecordGetter — top-level on every record type) | top-level `success: true`, `records` array with 4 entries; each entry's `success: true` with payload containing `EditorID` and no other top-level field; cross-type validation passes uniformly because the projected path resolves on every type in the batch. Verifies per-type validation runs but produces zero errors when paths are universally valid |
| `2.04` | Single-record path with new parameters | `mo2_record_detail(formid: "<RACE-FormID>", fields: ["<projected-property>"], expand_links: ["<expanded-property>"])` (single-`formid` shape, NOT batch; both projection and expansion supplied) | top-level shape is the v2.9.1 single-record response shape (NOT the per-record envelope shape) but with projection and expansion applied; `success: true`; payload contains only the projected path with expansion at the named position. Verifies the single-record code path composes with the new parameters per § A "Single-record path (`formid: "..."`) … Composes with `fields` / `expand_links`" |

---

## Layer 3 — Workflow scenario(s) on live install

Run via `mo2_record_detail` against the live Authoria modlist (read-only — no test patches written). Output: per-scenario assertion table + perf comparison vs Phase 1 baseline in `PHASE_3_HANDOFF.md`. No file-output side-effects.

**Phase 0 pre-specs use case + assertions; Phase 3 picks live FormIDs at execution time.** Aaron may swap the named record type during Phase 3 if Phase 1's record-shape sweep surfaces a better Authoria fit (e.g. if RACE count in Authoria differs materially from the consumer's 168 figure, or another record type with FormLink-chase patterns is more representative).

### Scenario 3.1 — Consumer's 168-record case: batched read with projection + expansion

**Use case.** Real-world AI-driven patcher: an Authoria tester reported a 168-RACE patching workflow costs ~600+ tool calls today, dominated by per-record `mo2_record_detail` round-trips (~1.3 s subprocess startup × 168 records) plus second-tier round-trips for FormLink-chase patterns (each RACE's `<ActorEffect>` points at SPEL records the patcher needs to inspect — ~5–15 spells per race × 168 races = ~1000 second-tier calls). The patcher needs every RACE record's `<EditorID>` + `<Skeleton>` (or whatever scalar) + `<ActorEffect>` (with each linked SPEL's detail inlined) + any other Phase-1-probe-confirmed projected paths the consumer's actual workflow needs. v2.9.2's three composable axes collapse this from ~1200 tool calls (168 first-tier + ~1000 second-tier FormLink-chase) to roughly 1 batched call. Phase 1's perf probe quantifies the projected reduction; Phase 3 measures the actual reduction on the live modlist and asserts within ±20% of projection.

**Target (Phase 3 picks at execution):**
- ~168 RACE records from the live Authoria modlist (vanilla Skyrim + DLC + Authoria/Requiem additions). If Authoria's actual RACE count differs materially from 168 (e.g. 120 or 200), Phase 3 batches the actual count and documents the live count in PHASE_3_HANDOFF.md. If RACE doesn't match the consumer's workflow shape on Authoria specifically (e.g. the tester's modlist has a different focus and "168 records" refers to a different record type), Phase 3 substitutes the analogous record type — the use-case framing is "many records of one type with FormLink expansions", not specifically RACE.
- Phase 1's record-shape sweep confirms canonical projected paths (e.g. `EditorID`, `<Skeleton>`, `<ActorEffect>`, `<Spells>`) and canonical expansion targets (the FormLink-typed list properties pointing at SPEL or other linked records).

**Operations:**
- Single `mo2_record_detail` call: `formids: [<live RACE FormIDs from Authoria>]` (~168 entries) + `fields: [<Phase-1-confirmed-projected-paths>]` + `expand_links: [<Phase-1-confirmed-list-of-FormLinks-property>]` + `resolve_links: true`.

**Assertions:**
- Top-level `success: true`; `records` array length matches the input formids count.
- Every input formid resolves: per-record `records[].success: true` for all entries; if any per-record entry fails, the failure is documented as a Phase 3 finding (potential bug in formid resolution against the live Authoria load order).
- Each entry's payload contains EXACTLY the projected paths + the expansion at the named FormLink position (wrapper form per Q2 default).
- Each entry's expanded list contains one wrapper-form object per FormLink in the source list, with `expanded` populated to a full SPEL detail (or whatever the linked record type is). Single-level lock holds — interior FormLinks of each expanded SPEL render as plain FormID strings (or `Plugin:HexID (EditorID)` annotations via `resolve_links: true`).
- Wall-clock reduction matches Phase 1's projection: subprocess wall-clock is roughly Phase 1's `subprocess startup + N × per-record marginal cost` rather than `N × subprocess startup`. Within ±20% of projection — substantial deviation surfaces a bug in Phase 4 triage.
- Response token-count reduction matches Phase 1's projection: combined batch + projection + expansion payload is roughly Phase 1's `N × (projected payload size + per-record expansion size)` — significantly smaller than `N × full-detail size + Σ(linked record full-detail sizes)`. Within ±20%.
- All v2.9.1 single-`formid` patterns continue to work bit-identically — Phase 3 spot-checks one representative single-`formid` call against an Authoria record without any new parameters; the response matches the v2.9.1 response shape.

### Scenario 3.2 — Optional secondary scenario: NPC_ batch with faction expansion

**Use case.** A symmetric scenario exercising the same three axes on a different record type. NPC_ records carry a `Factions` list of `{Faction: FormLink → FCTN, Rank: int}` entries; a faction-aware patcher needs each NPC's faction memberships with the linked Faction record's detail inlined. Provides cross-type coverage of the expansion mechanism on a list-of-structs-with-FormLinks shape (vs Layer 3.1's list-of-direct-FormLinks shape). Phase 1's record-shape sweep confirms the canonical NPC_ Factions property name and the FormLink-typed sub-property.

**Phase 3 conditional execution.** Run only if Phase 1's record-shape sweep confirms NPC_ has a `Factions.Faction` (or equivalent) projection-walkable nested-FormLink path AND the live Authoria modlist has enough NPC_ records to exercise the batch axis meaningfully (~50+). If either precondition fails, document as "Scenario 3.2 skipped — precondition not met" in PHASE_3_HANDOFF.md and rely on Scenario 3.1 alone for Layer 3 coverage.

**Operations:**
- Single `mo2_record_detail` call: `formids: [<live NPC_ FormIDs from Authoria, ~50+>]` + `fields: ["EditorID", "<Factions-or-confirmed-list-property>"]` + `expand_links: ["<Factions.Faction-or-confirmed-nested-FormLink-path>"]` + `resolve_links: true`.

**Assertions:**
- Symmetric to Scenario 3.1: per-record envelope shape; projected paths only; nested-FormLink expansion in wrapper form at the named path; perf within Phase 1's projection ±20%.
- Specifically verifies the auto-traversal-into-list-of-structs path per Q1 default (`Factions.Faction` reads as "the Faction sub-property of each Factions entry"); the walker descends into the Factions list, walks each entry's struct, identifies the Faction sub-property as the expansion target, and inlines the linked FCTN record's detail.

---

## Layer 4 — Edges

DSL-form edges of the three new optional parameters' value forms. v2.9.2's mechanism doesn't change v2.9.1's per-list-target dispatch or v2.9.0's per-Condition build pipeline — those surfaces stay exercised via Layer 5 regression. v2.9.2's new edges are parameter-value-form (empty list, null, auto-traversal-on-dict, missing-link target) only.

### 4.dsl — parameter-value-form edges

| # | Setup | Expected |
|---|-------|----------|
| `4.dsl.01` | `mo2_record_detail(formids: [])` (empty list) | top-level `success: false`; `error: "formids must contain at least one FormID — empty list rejected. Omit the parameter for single-record mode."` (or equivalent — Phase 2 finalizes wording). Phase 0 default per PLAN § Phase 0 step 2: empty list = error (request-author-error, not a valid no-op); matches v2.9.0's "empty list rejected" posture. Distinct from absence-of-key (which routes to single-record mode if `formid` is supplied) |
| `4.dsl.02` | `mo2_record_detail(formid: "<X>", fields: [])` (empty fields list) | top-level `success: false`; `error: "fields must contain at least one path — empty list rejected. Omit the parameter for full-payload mode."` Phase 0 default: empty list = error; absence-of-key = full payload (v2.9.1 default). Symmetric to 4.dsl.01 |
| `4.dsl.03` | `mo2_record_detail(formid: "<X>", expand_links: [])` (empty expand_links list) | top-level `success: false`; `error: "expand_links must contain at least one path — empty list rejected. Omit the parameter for no-expansion mode."` Symmetric to 4.dsl.01 / 4.dsl.02 |
| `4.dsl.04` | `mo2_record_detail(formid: "<RACE-with-dict-typed-property>", fields: ["<dict-property-path>"])` (auto-traversal on dict-typed property per Q1 default = auto-traverse) | response contains the dict-property path with the walker auto-traversing dict elements per Q1 lock — flattens to a list (or shape-preserves the dict, depending on Phase 2's confirmed implementation). Phase 1's record-shape sweep picks a canonical dict-typed property on RACE for this cell; if RACE has none, Phase 1 substitutes a different record type and updates the cell's carrier. Verifies Q1's "Auto-traversal generalizes to dicts cleanly" lock per § B rationale point 4 |
| `4.dsl.05` | `mo2_record_detail(formid: "<X-with-always-null-property>", fields: ["<always-null-path>"])` | response contains the projected field rendered as `null` (NOT absent — projection is shape-preserving per Phase 0 default; if Q4 lock flips to "lazy mid-walk", the same expectation holds because the path resolves on the type). Verifies the shape-preserving contract — a projected field that resolves on the type but is null on the source record renders as null, distinct from a path that doesn't resolve on the type at all (which is 1.D.01) |
| `4.dsl.06` | `mo2_record_detail(formid: "<X>", expand_links: ["<FormLink-property-pointing-at-missing-master>"])` (target is a FormLink whose pointed-at record doesn't exist in the load order — e.g. a missing-master FormID) | response's named expansion field renders as the wrapper form with `formid` populated to the missing FormID, `expanded: null`, and `error` populated to the missing-master message: `{formid: "Missing.esp:01ABCD", EditorID: null, expanded: null, error: "FormID target not in load order"}` (or equivalent — Phase 2 finalizes wording). Uniform shape per Q2 default rationale point 4 (symmetric with `null`-link rendering); the caller can detect the failure per-entry without ambiguity. The bridge does NOT fail the whole call; expansion-target-missing is a per-entry partial failure within an otherwise successful call |

---

## Layer 5 — Regression band

All v2.9.1 coverage-smoke cells run unchanged. v2.9.2 must not regress any v2.9.1 behavior — the three new parameters are additive, defaulting to the v2.9.1 single-record / full-payload / no-expansion behavior when absent.

| Cell range | Source | Expected |
|---|---|---|
| `5.range` | `dev/plans/v2.9.1_quest_condition_disambiguation/MATRIX.md` Layer 1.P + 1.D + 2 + 4 + 5 (~400 v2.9.1 cells per CHANGELOG; Phase 2 confirms the actual baseline against `coverage-smoke/Program.cs`'s run-time enumeration before adding v2.9.2's new Layer 1 / 1.D / 2 / 4 cells) | each cell PASS as it did in v2.9.1 P5 (and v2.9.1's own Layer 5 = 382 v2.9.0 cells, transitively green). The ~400 figure is the Phase 0 estimate from v2.9.1 ship; Phase 2 reads the actual baseline |

Specifically: every v2.9.1 single-`formid` `mo2_record_detail` invocation pattern (used implicitly by every patching test's readback assertion) stays bit-identical, the v2.9.1 `condition_target` operator-parameter dispatch stays untouched (different surface — write side), and the existing `plugin_names` multi-plugin path stays unchanged when called without `formids`. This is the core back-compat assertion of v2.9.2.

---

## Total assertion count (Phase 0 baseline)

**v2.9.2 capability surface is three composable axes on one tool surface.** No Pareto pull, no function inventory, no slot-shape branches. Cell counts below are Phase 0 baseline; Phase 1 only adjusts if the record-shape sweep surfaces additional in-scope record-type cells Aaron locks in (e.g. Layer 1.P expansion rows on NPC_ / QUST / MGEF if Phase 1 finds clean canonical FormLink shapes worth documenting separately from the RACE anchor).

| Layer | Matrix rows | Harness cells | Source |
|---|---:|---:|---|
| 1.P (per-axis positives) | 7 | 7 | this doc |
| 1.D (negatives + new explicit error paths) | 7 | 7 | this doc |
| 2 (combinatorial) | 4 | 4 | this doc |
| 3 (workflow scenarios) | 1 mandatory + 1 optional (3.1 RACE, 3.2 NPC_ if precondition met) | ~10–14 assertions | this doc; Phase 3 picks live FormIDs |
| 4.dsl | 6 | 6 | this doc |
| 5 (regression) | 1 (range row) | ~400 | v2.9.1 baseline |
| **Total** | **~26 matrix rows** | **~440 harness cells** | — |

Phase 2 may dedupe or merge cells where the same code path is exercised twice. v2.9.1's MATRIX.md is the source of truth for the Layer 5 regression count; Phase 2 reads from `coverage-smoke/Program.cs`'s actual cell enumeration rather than from this matrix doc when running the full regression band.

**Extensibility note.** If Phase 1's record-shape sweep surfaces additional in-scope record types with canonical FormLink shapes worth anchoring (e.g. NPC_.Factions, QUST.Aliases, MGEF.Effects, PERK.Effects [carry-over to v2.9.3 — out of scope for v2.9.2 anyway]) and Aaron locks them in v2.9.2 scope via the conductor relay, Layer 1.P extends with one `1.P.expand.<Type>.<sub>` cell per anchor. Layer 1.D's structural error rows generalize unchanged (the bad-path / bad-target / non-FormLink-target validation paths apply to any record type). Phase 1's handoff documents the probe finding; Phase 1 or Phase 2 extends the matrix if scope generalized.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.9.1:

```
[1.P.batch.QUST]                 mo2_record_detail formids QUST  <3 anchors>          PASS (3 records returned; envelope shape matches; subprocess invocation count = 1)
[1.P.batch.RACE]                 mo2_record_detail formids RACE  <3 anchors>          PASS (3 records returned; envelope shape matches)
[1.P.fields.RACE.scalar]         mo2_record_detail fields=[EditorID]                  PASS (only EditorID present; full v2.9.1 payload absent)
[1.P.fields.RACE.list]           mo2_record_detail fields=[<list-property>]           PASS (list rendered; out-of-projection branches absent)
[1.P.expand.RACE.formlink]       mo2_record_detail expand_links=[<scalar-FormLink>]   PASS (wrapper form { formid, EditorID, expanded } at named field)
[1.P.expand.RACE.list]           mo2_record_detail expand_links=[<list-property>]     PASS (list of wrapper-form objects; single-level lock holds)
[1.D.01]                         mo2_record_detail fields=[BogusField]                PASS (validation_errors.RACE.bad_field_paths populated; rolled back)
[1.D.04]                         multi-error accumulation                             PASS (3 categories accumulated; one round-trip)
[1.D.06]                         formids per-record partial failure                   PASS (per-record envelope; success=true top-level; bad entry success=false)
[1.D.07]                         formids+plugin_names mutual-exclusion                PASS (request-shape error pre-bridge)
[2.01]                           all three axes composed                              PASS (each axis independent; per-record application)
[2.02]                           all three axes + resolve_links                       PASS (FormIDs annotated throughout expanded tree)
[3.1]                            live: 168-record case (RACE batch + projection + expansion)  PASS (perf within Phase 1 projection ±20%)
[4.dsl.04]                       fields auto-traversal on dict                        PASS (Q1 lock: walker auto-descends into dict)
[5.range]                        v2.9.1 regression band                               ~400/~400 PASS
```

Failures embed enough context for handoff to lift into the bug list directly. Per-cell PASS/FAIL is the harness contract; per-axis assertions are inlined in each Layer 1 / 2 cell's PASS string.

---

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a record meeting the test fixture requirements (e.g. anchor needs RACE with a populated `<ActorEffect>` list and the picked anchor lacks one), the harness prints:

```
[1.P.<axis>.RACE.<sub>]  <axis> RACE <none-meeting-fixture>  SKIP: anchor RACE lacks <property> populated
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture (build a synthetic RACE in-memory via Mutagen) or accept the gap. Phase 1's record-shape sweep is expected to identify a vanilla RACE with the canonical FormLink-typed properties populated; if no such vanilla RACE exists, Phase 1's handoff names the gap and Phase 2 falls back to a synthetic-RACE fixture pattern.

---

## Phase fill-in checklist (Phase 1 hand-back)

Phase 1 closes with these MATRIX edits landed:

- [ ] **Canonical FormLink-typed property names per record type** — Phase 1's record-shape sweep enumerates every `IMajorRecordGetter`-implementing interface in `Mutagen.Bethesda.Skyrim` 0.53.1 + every property whose type matches the FormLink predicates. Phase 1 substitutes the placeholder names in the matrix (`<Skeleton-or-confirmed-scalar>`, `<ActorEffect-or-confirmed-list-property>`, etc.) with the canonical Mutagen 0.53.1 names against the probe output.
- [ ] **`ActorEffect` vs `ActorEffects` resolution** — v2.7.1's bridge code names `ActorEffect` (singular); the v2.9.2 task spec mentions `ActorEffects` (plural). Phase 1 confirms the canonical Mutagen 0.53.1 name and updates the matrix.
- [ ] **RACE anchor FormID(s) for Layer 1.P** — Phase 1 picks 1+ vanilla RACE record(s) with populated `<ActorEffect>` list (and ideally a non-null `<Skeleton>` scalar) for the projection + expansion cells. Phase 1's MATRIX edit substitutes the placeholder FormIDs in `1.P.batch.RACE` / `1.P.fields.RACE.*` / `1.P.expand.RACE.*` rows.
- [ ] **QUST anchor FormID(s) for Layer 1.P.batch.QUST** — Phase 1 picks 3+ vanilla QUST records for the batch cell (anchor candidates: `Skyrim.esm:04C49D` / `0E3145` from v2.9.1's MATRIX or whatever 3+ Phase 1's probe surfaces).
- [ ] **Per-axis number anchors** — Phase 1's perf probe outputs subprocess startup cost (median + range), per-record marginal cost table (batch 1, 5, 20, 50, 100, 200 → per-record delta), per-record full-detail payload baselines per record type, projection payload-size-impact ratio per record type, expansion round-trip-elimination ratio. Phase 1 annotates the matrix's Layer 1.P / 2 / 3 expected-payload-size columns with these numbers as anchor expectations.
- [ ] **Layer 3 anchor record type confirmation** — Phase 1 confirms RACE is the right Layer 3 anchor for Authoria (count ≈ 168, FormLink-chase pattern matches consumer signal), or proposes a different record type. Phase 1's PHASE_1_HANDOFF.md § Conductor asks captures the proposal; conductor relays to Aaron if multiple candidates with trade-offs.
- [ ] **Layer 4.dsl.04 dict-property selection** — Phase 1's record-shape sweep picks a canonical dict-typed property on RACE (or a different record type if RACE has no dict-typed property) for the auto-traversal-on-dict cell. If no in-scope record type has a dict-typed property the matrix can anchor on, Phase 1 documents and Phase 2 may delete the cell (auto-traversal-on-dict semantics tested instead via 4.dsl coverage on list-typed properties).
- [ ] **Scenario 3.2 precondition check** — Phase 1 confirms NPC_'s canonical Factions property name + the FormLink-typed sub-property. If both confirmed, Scenario 3.2 stays in scope for Phase 3. If either is missing or differently-named, Phase 1 documents and Phase 3 either substitutes a different secondary scenario or skips Layer 3.2 entirely.

---

## Phase fill-in checklist (Phase 2 hand-back)

Phase 2 closes with these MATRIX edits:

- [ ] **Layer 5 cell count** — confirm the ~400 figure against the actual `coverage-smoke` baseline at start-of-Phase-2 (the ~400 is a Phase 0 estimate from v2.9.1 ship; the actual count depends on `coverage-smoke`'s run-time enumeration). Update `5.range` row's count if different.
- [ ] **Layer 4 / 1.D expectation flips** if Q1–Q6 lock differs from Phase 0 defaults — update the affected rows per the actual lock (e.g. if Q2 lock flips to replace-with-inlined-dict, Layer 1.P.expand rows + 4.dsl.06 expectation update; if Q3 lock flips to strict-batch, 1.D.06 expectation flips to top-level `success: false`; if Q4 lock flips to lazy mid-walk, validation timing in 1.D.01–.05 expected outputs adjust; if Q5 lock flips to soft cap, 4.dsl.07 cell may be added for cap-exceeded-error; if Q6 lock flips to allow combination, 1.D.07 deletes and a new 2.05 cell tests the cross-product).
- [ ] **Layer 2.04 single-record path response shape** — confirm whether single-`formid` + new parameters returns the v2.9.1 single-record shape or the new per-record envelope shape. Phase 0 default: v2.9.1 single-record shape (per § A "Composes with `fields` / `expand_links`"). Phase 2 reads the actual response and locks the matrix expectation.
- [ ] **Error message wording finalization** — Layer 1.D and Layer 4.dsl rows reference Phase 2-finalized wording placeholders. Replace with the actual strings from `RecordReader.cs` + `tools_records.py` validation paths once landed.
- [ ] **Layer 1.D validation-error JSON shape** — Phase 0 locked the structural contract (validation_errors keyed by record type, three categories per type, valid-name lists per category context); Phase 2 finalizes the exact JSON key names and updates 1.D.01–.05 rows accordingly.

---

## Phase fill-in checklist (Phase 3 hand-back)

Phase 3 closes with:

- [ ] **Live FormIDs** — replace placeholder FormIDs in Layer 3 scenarios with the FormIDs picked from the live Authoria modlist at execution time.
- [ ] **Per-scenario PASS/FAIL** — annotate each scenario row with the readback evidence + result + measured perf vs Phase 1 projection (subprocess wall-clock, response token-count).
- [ ] **Scenario 3.2 in-scope-or-skip** — confirm Scenario 3.2's precondition (NPC_ Factions structure + ~50+ NPC_ records on Authoria) and either land the assertions or document the skip with reason.
