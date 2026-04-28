# v2.9.2 — Read-side efficiency for `mo2_record_detail`

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-28, post-v2.9.1 ship.
**Baseline:** v2.9.1 (shipped 2026-04-28 — Quest condition disambiguation).
**Target version:** v2.9.2 (working slug — confirm at PLAN review).
**Sessions estimated:** 5–7 phase sessions plus 1 conductor session running across them. Phase 4 is conditional (skipped if Phase 3 surfaces nothing). No Phase 2 split contemplated — capability surface is three composable parameters on a single existing tool, not a function inventory.

**Mandate.** A real consumer (Authoria tester) reported that AI-driven workflows hit token-cost ceilings on read-heavy patching tasks — a 168-race patching pass costs ~600+ tool calls today, dominated by per-record `mo2_record_detail` round-trips. v2.9.2 lands three composable read-side improvements to the existing `mo2_record_detail` tool. They **multiply, not add** — combined, the tester's 168-race scenario collapses from ~600+ tool calls to roughly 1.

1. **Batch (`formids: [...]`).** Accept a list of FormIDs alongside the existing single `formid`. One bridge subprocess invocation reads N records and returns a JSON array. Amortizes the ~1.3 s .NET CLR + Mutagen JIT subprocess startup across N records — confirmed in v2.6.0 P3's bridge-scan amortization (the existing `RecordReader.ReadBatch` plugin-load cache already supports this on the bridge side; v2.9.2 exposes it through the MCP tool surface).
2. **Field projection (`fields: [...]`).** Walk only the requested property paths. Shrinks per-record payload by ~80 % on big records (RACE / NPC_ / QUST). Pure read-side — same Mutagen objects, but the JSON-rendering walker skips out-of-projection branches.
3. **FormLink expansion (`expand_links: [...]`).** When the reader encounters a FormLink in a named field, descend into the linked record and inline its detail. **Single-level only** (no recursion, no cycle detection). Saves a second round-trip otherwise needed to fetch each linked record's detail.

All three composable on a single call: `record_detail(formids=[...], fields=[...], expand_links=[...])`. **Generic mechanism**, not per-record-type tools (no `get_race_with_spells`-style proliferation). **Optional parameters**; defaults preserve v2.9.1 single-record / full-payload / no-expansion behavior bit-identically. **Backward compatible.**

This is a **single-mechanism, scope-locked** point release — three additive parameters on one tool surface, three handler extensions in `RecordReader`, and the corresponding Python passthrough plumbing. Not a refactor. v2.9.1's mechanisms (Condition dispatcher, list-target dispatch, Tier-D Q3/Q4 sentinels) are untouched — v2.9.2 changes only the read side.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |
| `<plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.2_read_side_efficiency\` |
| `<v2.9.1-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.1_quest_condition_disambiguation\` (shipped 2026-04-28; reference only — closed) |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. The conductor session has already told you which phase you are via the kickoff prompt that spawned this session. **Before touching anything**, do this in order:

1. **Confirm your phase.** The conductor's kickoff prompt named your phase. If it didn't, halt and ask the conductor — don't infer it from the handoff numbering yourself (the conductor owns phase identification).

2. **Read the previous handoff** in full (if any). The conductor's kickoff prompt named which one. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, conductor decisions relevant to your phase, and what to write in your own handoff. **Do not read other phases' sections** — they're scoped to other executors and consume context for no benefit.

4. **Read `MATRIX.md`** in this directory. Phase 0 produces it; Phases 1–5 use it as the authoritative test specification. Phase 1 may extend it with whatever record-shape findings the perf probe surfaces; Phase 2 onward reads the post-Phase-1 form.

5. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.1)
   - `Claude_MO2/KNOWN_ISSUES.md` § Patching write surface (carry-over context — not the v2.9.2 fix surface, but the latest known-state document the executor should know about)
   - **Skip** the prior-plan handoff sweep — `<v2.9.1-plan>` is closed; the v2.9.1 PHASE_4 passthrough fix is the relevant cross-cutting reference for any Python-side schema changes (`tools_patching.py`'s `passthrough_keys` whitelist; v2.9.2's read-side parameters route through `tools_records.py` instead — different surface, but the lesson "MCP wrapper passthrough is a separate plumbing layer from the bridge models" applies).
   - Check `<workspace>/Live Reported Bugs/` root for anything new.

6. **Confirm phase identity + work plan with the user (Aaron) before any code changes.** Wait for go-ahead.

7. **At the end of your phase**, write `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` if Phase 4 spawns a sub-session) in this directory using the template at the bottom of this file. **Do not write the next phase's kickoff prompt** — the conductor owns that.

**One phase per session.** If you finish early, summarise and stop — don't roll into the next phase.

### Communicating with the conductor

The conductor session is a separate Claude Code session orchestrating this plan. It runs between phases (reading your handoff, writing the next phase's kickoff). If your phase needs guidance the plan doesn't already give you (scope ambiguity, an unexpected probe result that changes the mechanism shape, a bridge bug that needs Aaron's call to absorb-vs-defer), write a short note to the conductor.

**Token-efficient format** — bullets, no prose, no transcripts:

```
CONDUCTOR ASK
Phase: N
Topic: <one-line topic>
Context: <2-3 bullets max>
Question: <single specific question>
Suggested options: <A/B/C with one-line rationale each>
Default if no response in <X minutes>: <whatever the executor will do absent guidance>
```

Drop this at the bottom of your in-progress handoff under § Conductor asks. The conductor reads it, summarizes for Aaron if needed, and either replies (you proceed with that answer) or escalates to Aaron (who replies via the conductor or interjects directly).

---

## 📋 Background — why this plan exists

### Real consumer signal (2026-04-28, Authoria tester)

A live consumer reported that AI-driven patching workflows hit token-cost ceilings on read-heavy tasks. The named example: patching 168 RACE records (vanilla Skyrim + DLC + Authoria/Requiem additions) costs ~600+ tool calls today. The dominant cost is per-record `mo2_record_detail` round-trips: each record requires its own subprocess invocation against the bridge (~1.3 s startup + JIT each), plus second-tier round-trips when the patcher needs to inspect a linked record (FormLink chase via a follow-up `mo2_record_detail` call).

Aaron preempted v2.9.x's planned next item (PERK.Effects, formerly v2.9.x carry-over #7 in the v2.9.1 KNOWN_ISSUES schedule) in response to this signal. PERK.Effects pushes to v2.9.3; this work lands as **v2.9.2**.

### Why three improvements, not one

The three improvements **multiply, not add** — they target different cost components:

- **Batch (`formids`)** amortizes subprocess startup. 168 records × 1.3 s startup = ~220 s of overhead today; 1 × 1.3 s + 168 × <10 ms reads ≈ ~3 s with batching. **~70× speedup on subprocess overhead alone.**
- **Field projection (`fields`)** shrinks per-record payload. RACE record full-detail JSON is ~10–30 kB; a typical patcher needs 3–5 field paths (~1–2 kB). **~80 % token-cost reduction** on the response side, independent of subprocess count.
- **FormLink expansion (`expand_links`)** removes a second-tier round-trip. Without it, the patcher reads RACE → sees FormLink to a linked SPEL → makes a second `mo2_record_detail` call → repeat per FormLink. With it, the linked record's detail inlines into the original walk. For RACE.ActorEffect (list of ~5–15 spells per race), this collapses ~5–15 second-tier calls per record into 0.

Combined, the 168-race scenario:
- Today: 168 first-tier calls + ~1000 second-tier FormLink chase calls = ~1200 tool calls; ~600 s subprocess overhead; ~3–5 MB of response tokens.
- With v2.9.2: 1 batched call with `fields` + `expand_links`; ~3 s subprocess overhead; ~50–200 kB of response tokens. **Roughly three orders of magnitude reduction.**

The "1 call" figure in the mandate is the headline; the actual call count depends on how the patcher decomposes its work (one big batched read followed by N create_patch calls is the typical shape — the read collapse is the v2.9.2 win).

### Architecture: why extend `mo2_record_detail` and not add new tools

Per the locked design (mandate § 1–6):

- **Generic mechanism**, not per-record-type tools. A `get_race_with_spells` family would scale poorly (every new record-type-with-FormLinks needs its own tool); generic projection + expansion handles arbitrary record types via reflection on Mutagen's typed schema.
- **Extend `mo2_record_detail`**, not a new tool. Optional parameters; defaults preserve v2.9.1 behavior. New tool surface would force consumers to switch — an additive parameter set keeps the existing API stable while adding the efficiency win.
- **Bridge already has the plumbing.** `RecordReader.ReadBatch` exists (used by the multi-plugin path: `plugin_names: [...]`); the bridge already caches plugin-loads by path within a batch. v2.9.2 reuses this — Python resolves formids → winning-plugin paths → sends the existing `read_records` request shape with new `fields` / `expand_links` parameters. Bridge changes are confined to extending `ReadBatchRequest` with the new parameters and applying them in the per-record reader, not adding a new bridge command.

### Out of scope (locked at PLAN write-time)

- **Production code changes outside Phase 2 / 4.** Phase 0 + 1 don't touch bridge or Python. Phase 2 lands the implementation. Phase 3 reads only. Phase 4 is conditional.
- **Recursive expansion.** Single-level only — locked. A FormLink in an expanded record's body renders as a FormID string (or `"Plugin:HexID (EditorID)"` if `resolve_links=true`); the walker does NOT descend further. Cycle detection therefore unnecessary.
- **Per-record-type "convenience" tools** (`get_race_with_spells`, etc.). Generic mechanism — locked.
- **Caching across sessions** (the bridge subprocess is per-call; cross-call state is a different workstream — v2.9.x candidate if a real consumer surfaces it).
- **#7 PERK.Effects** — pushed to v2.9.3.
- **v2.9.x deferreds** — Boolean dispatcher branch, sub-B String functions, QuestAlias/QuestLogEntry nested conditions — separate scoping sessions.
- **Replacing the depth limit** (`ReadRequest.MaxDepth = 6`). Not exposed to MCP today; v2.9.2 leaves it as-is. Any consumer hitting the depth limit on a projected/expanded read continues to hit `"...[max depth reached]"` strings in the same places. v2.9.x candidate to expose if a real consumer surfaces it.
- **Cross-record-type heterogeneity in projection / expansion validation strict-batch.** v2.9.2 validates per record-type within the batch (a batch of [QUST × 50, NPC_ × 20, RACE × 98] runs three separate validations, accumulates all invalid entries across types). The single-record-type batch is the common case; mixed-type strict-batch error reporting is the architecturally interesting case Phase 0 / Phase 2 nail down.

---

## 🏗️ Architecture — read-side efficiency mechanism (locked + open questions)

### A. Reuse existing `read_records` bridge command (don't add a new command)

The bridge's `RecordReader.ReadBatch(ReadBatchRequest)` exists and is wired through the `read_records` command in `Program.cs` (today's consumer: the `plugin_names: [...]` multi-plugin path). It already caches plugin loads by path within a batch via `modCache`, so "many records from one plugin" costs only one plugin load per batch.

For v2.9.2's `formids: [...]` batch:
- **Python wrapper resolves each formid to its winning plugin path** via the existing `_resolve_target` index lookup (extended to walk a list).
- **Build the existing `read_records` request shape** with N items (`{plugin_path, formid}` pairs), plus the new top-level `fields` / `expand_links` parameters on `ReadBatchRequest`.
- **Bridge applies projection + expansion per item** during the existing per-record render.

This is the **lightest-touch architecture** — the bridge model gets two new properties on `ReadBatchRequest` (and matching ones on `ReadRequest` for the single-record path); no new request type, no new command, no new dispatch in `Program.cs`. The Python tool surface gets three new optional parameters.

The orthogonal axes:

| Axis | Today's surface | v2.9.2 extension |
|---|---|---|
| Subprocess invocation | 1 record per call | N records per call (`formids: [...]`) |
| Per-record payload | Full Mutagen object walk | Projected to N field paths (`fields: [...]`) |
| FormLink in result | String FormID (or annotated via `resolve_links`) | Inlined detail of linked record (`expand_links: [...]`) |
| FormID resolution | `Plugin:HexID` (raw) or `Plugin:HexID (EditorID)` (`resolve_links`) | Composes with all three new axes — no change |
| Single-record path (`formid: "..."`) | Existing — bit-identical preserved | Composes with `fields` / `expand_links` |
| Multi-plugin path (`plugin_names: [...]`) | Existing — multi-plugin diff for one record | **Mutually exclusive with `formids`** — Phase 0 lock; matches existing `plugin_name` vs `plugin_names` exclusivity |

### B. Path syntax for `fields` and `expand_links` — open question (Phase 0 lock)

The existing path-syntax surface is `set_fields` (Tier C, PatchEngine.cs:1025 `SetPropertyByPath`):

- Dot-separated segments (`Foo.Bar.Baz`).
- Bracket-key form on the **final segment only** (`Foo.Bar[Key]` for dict access; `Foo[Key].Sub` rejected with "Bracket syntax is not supported on intermediate path segment" per PatchEngine.cs:1036).
- For collection traversal, there is **no** `[]` (empty-bracket) syntax.

For v2.9.2's `fields` and `expand_links`, the open question is: how do we express "all elements of a list-typed property"?

- **Option A — Dot-notation auto-traversal** (Phase 0 default proposal). When the walker hits a list-typed property mid-path, it auto-descends into each element. `fields: ["ActorEffect"]` returns the full list of FormLinks; `fields: ["Effects.BaseEffect"]` returns each Effect's BaseEffect (the walker auto-traverses `Effects` because it's a list). No new bracket syntax. Aligns with the JSON-projection convention used by MongoDB / GraphQL / JSONPath. Doesn't change `set_fields`'s parser invariants.
- **Option B — Bracket-empty for collections** (`Effects[].BaseEffect`). Explicit "all elements" sentinel. Requires extending `ParsePathSegment` to accept empty-bracket — and crucially, accepting it on **intermediate** segments (today's parser rejects intermediate brackets). Breaks the existing `set_fields` "final-segment-only" invariant if the same parser is shared.

**Phase 0 proposal: Option A — dot-notation auto-traversal.** Rationale:

1. Read-side semantics differ from write-side. `set_fields` brackets are for **dict keys** (writing `Foo.Bar[Key]: value` writes a dict entry); the read side has no dict-key analogue ("read everything in this dict" is the natural fallback when the path resolves to a dict-typed property — auto-traversal handles this too).
2. No parser-invariant break — `set_fields` keeps its final-segment-only bracket rule unchanged; v2.9.2's `fields` parser is a **separate, simpler walker** that only understands dot-segmented paths.
3. Cleaner mental model for callers — `Effects.BaseEffect` reads naturally as "the BaseEffect of each Effect"; `Effects[].BaseEffect` adds syntax for no semantic gain on the read side.
4. Auto-traversal generalizes to dicts cleanly: a dot-segment after a dict-typed property iterates over values (returns a flattened list). Phase 1's perf probe should sanity-check that no in-scope record types have nested dicts where this generalization would surprise.

If Phase 0 surfaces a real consumer needing the explicit form (or Phase 1 finds a record-type ambiguity that auto-traversal can't disambiguate), Phase 0 escalates to Aaron via conductor relay. Otherwise lock Option A.

### C. Expansion output shape — open question (Phase 0 lock)

When `expand_links: ["ActorEffect"]` and a RACE record's `ActorEffect` field is a list of FormLinks to SPEL records, the expanded output for each FormLink can take one of two shapes:

- **Option A — Wrapper form** (Phase 0 default proposal):
  ```json
  "ActorEffect": [
    {
      "formid": "Skyrim.esm:01ABCD",
      "EditorID": "FlamesSpell",
      "expanded": { ... full SPEL detail per RecordReader walk ... }
    },
    ...
  ]
  ```
  Backward-compat-friendly: the `formid` field still appears (as today's behavior surfaces); the `expanded` key adds the inline detail. `resolve_links: true` enriches the `formid` and recursively the `expanded` content.

- **Option B — Replace-with-inlined-dict form**:
  ```json
  "ActorEffect": [
    { ... full SPEL detail per RecordReader walk ... },
    ...
  ]
  ```
  Lighter shape (no wrapper key), but loses the FormID annotation outside the inlined record's own top-level fields.

**Phase 0 proposal: Option A — wrapper form.** Rationale:

1. The FormID is the load-bearing identifier the caller used to ask for the expansion. Keeping it visible at the wrapper level matches the mental model "I asked for ActorEffect; I see the FormLink IDs plus their expansion."
2. `resolve_links` enrichment is cleaner — the wrapper's `formid` field annotates per existing semantics; the `expanded` content's internal FormIDs annotate via the recursive walker. Mixed-form (some entries expanded, some not — happens if a FormLink target plugin isn't in the load order) renders naturally.
3. Schema discoverability — a caller reading the response can tell "this is an expanded link, the formid is X, the detail is in `expanded`" without inferring from context.
4. Symmetric with `null`-link rendering: today, an unresolved FormLink renders as `null`; with expansion, an unresolved-or-expansion-failed FormLink renders as `{ formid: null, expanded: null }` — uniform shape.

If a real consumer surfaces a strong preference for Option B (or if Phase 1's payload-size measurement shows the wrapper overhead is significant on small expansions), surface to Aaron via conductor.

### D. Strict-batch error shape — locked (clarifications below)

Per mandate § 5: "Strict-batch errors with full valid-targets list. Any invalid field name or invalid expansion target fails the whole call. Multiple invalid entries are reported together (not first-failure-wins) so the caller fixes the list in one round-trip."

Three failure modes for strict-batch error surface:

1. **Unknown field path.** A path in `fields` doesn't resolve to a property on the record's type. E.g. `fields: ["Spells", "BogusField"]` against a RACE record. Error response names every bad path, the record type involved, and the list of valid top-level field names for that type.
2. **`expand_links` target is not a FormLink-typed field.** A path in `expand_links` resolves to a non-FormLink property (e.g. a string, int, list of non-FormLink items). E.g. `expand_links: ["EditorID"]`. Error response names every bad target, the actual type of each, and the list of valid FormLink-typed field names for that record type.
3. **`expand_links` target doesn't exist.** Same shape as case 1, but flagged as "expansion target not found" rather than "field path not found" — distinct error class because the caller's intent (expand a link) is different from the field-path-not-found case.

**Multi-error accumulation.** All three failure modes accumulate within a single response — the caller fixes the entire list in one round-trip. Pseudocode for the bridge-side validation:

```csharp
List<string> badFieldPaths = new();
List<string> badExpansionTargets = new();
List<string> nonFormLinkExpansionTargets = new();

foreach (var path in request.Fields ?? Enumerable.Empty<string>())
    if (!ValidatePathExists(recordType, path)) badFieldPaths.Add(path);

foreach (var path in request.ExpandLinks ?? Enumerable.Empty<string>())
{
    if (!ValidatePathExists(recordType, path)) { badExpansionTargets.Add(path); continue; }
    if (!ValidateIsFormLinkPath(recordType, path)) nonFormLinkExpansionTargets.Add(path);
}

if (badFieldPaths.Count + badExpansionTargets.Count + nonFormLinkExpansionTargets.Count > 0)
    return ValidationError(recordType, badFieldPaths, badExpansionTargets, nonFormLinkExpansionTargets, validFieldNames, validFormLinkFieldNames);
```

**Per-record-type validation.** When a batch contains mixed record types (e.g. `formids: [QUST, NPC_, RACE]`), validation runs **per unique record type** and accumulates errors per type. The error response names which type(s) had which bad paths. Cleanest shape: error response object keyed by record type code:

```json
{
  "success": false,
  "error": "Field path / expansion target validation failed.",
  "validation_errors": {
    "RACE": {
      "bad_field_paths": ["BogusField"],
      "bad_expansion_targets": [],
      "non_formlink_expansion_targets": ["EditorID"],
      "valid_field_names": [...],
      "valid_formlink_field_names": [...]
    },
    "NPC_": { ... }
  }
}
```

Phase 2 finalizes the exact JSON shape; Phase 0 locks the structural contract: **all errors surface in one response, keyed by record type, with the type's full valid-name list for context.**

### E. Batch + per-record formid resolution — locked

Per mandate / spec § 4 (open question): "If `formids: [a, b, c]` and `b` doesn't resolve, do we fail the whole call (consistent with strict-batch on field validation), or return `{a: detail, b: error, c: detail}`?"

**Phase 0 lock: per-record success/error**, matching the existing `read_records` (multi-plugin) precedent:

- The existing `_handle_record_detail` plugin_names path returns per-plugin success/error entries in `out_records[].success / .error`. Top-level envelope `success: true` even if individual records errored.
- Strict-batch on `fields` / `expand_links` is justified because those parameters are batch-wide (one bad parameter affects every record's ability to read meaningfully). Per-record formid resolution is inherently per-record (some succeed, some don't); the existing batch precedent is per-record success.
- A formid-not-found failure is recoverable by the caller (drop the bad formid, re-batch); a `fields`/`expand_links` validation failure is recoverable by fixing the parameter (re-batch with the corrected list). Both surfaces let the caller fix in one round-trip — they're just keyed at different granularity.

Schema:

```json
{
  "success": true,
  "records": [
    { "formid": "Skyrim.esm:01ABCD", "success": true, "fields": { ... } },
    { "formid": "Bogus.esp:FFFFFF", "success": false, "error": "FormID not found in load order index" },
    { "formid": "Skyrim.esm:02DEFA", "success": true, "fields": { ... } }
  ]
}
```

Top-level `success: false` is reserved for: (a) all records failed; (b) `fields`/`expand_links` validation failed (strict-batch); (c) request shape malformed.

### F. `resolve_links` interaction — locked

`resolve_links: true` (existing) annotates FormID strings in the response with their EditorIDs from the load-order index. Implementation: `_enrich_formids` walks the JSON tree post-bridge-response and applies a regex to FormID-shaped strings.

Composition with v2.9.2 axes:

- **`fields` × `resolve_links`**: Projection narrows the field set; `_enrich_formids` walks whatever fields are present. Orthogonal — no new code.
- **`expand_links` × `resolve_links`**: The expanded inline records contain their own FormIDs (via the recursive `RecordReader.RenderValue` walk); `_enrich_formids` recursively walks them. Already orthogonal — `_enrich_formids`'s recursion handles the deeper tree without any v2.9.2 change.
- **All three composed (`formids`, `fields`, `expand_links`, `resolve_links`)**: Every level of the response tree gets enriched. No new code — v2.9.2 inherits this composition for free from the existing `_enrich_formids` recursion.

Phase 2 acceptance includes a Layer 2 cell exercising all four composed.

### G. Performance baseline — Phase 1 deliverable (numbers required, not approximated)

Phase 1's perf probe must produce concrete numbers anchoring v2.9.2's claims:

1. **Subprocess startup cost.** Time to first JSON response on `read_record` of one trivial record (e.g. a vanilla GMST). Measured via Python wall-clock around the `subprocess.run` call. Expected: ~1.2–1.4 s on Aaron's hardware (matches v2.6.0 P3 bridge-scan numbers).
2. **Per-record marginal cost.** Time to add one more record in a `read_records` batch. Measured by running batches of {1, 10, 50, 100} of identical records and computing the per-record delta over batch-1. Expected: ~5–20 ms per record once the subprocess is hot.
3. **Per-record payload size baselines.** Full-detail JSON byte size on a representative record per type: RACE, NPC_, QUST, MGEF, PERK, ARMO, WEAP, SPEL. Probe outputs a size table.
4. **Projection payload-size impact.** For RACE specifically: full-detail vs `fields: ["Skeleton", "ActorEffect", "Spells"]` (or whatever names Phase 1's schema-sweep confirms). Compute the byte-reduction ratio.
5. **Expansion round-trip elimination.** Without expansion: time to `read_record(RACE) + read_record(each linked SPEL)` sequentially via N subprocess calls. With expansion: time to `read_record(RACE, expand_links=["ActorEffect"])` in one call. Compute the speedup ratio.

The numbers anchor the schema description ("`formids: [...]` amortizes startup; tested up to N records"), the CHANGELOG entry, and Phase 3's live-workflow scenario success criteria.

### H. Scope locks

- **One mechanism only.** Three composable parameters on `mo2_record_detail`: `formids`, `fields`, `expand_links`. No other tool changes, no other bridge command additions, no `RecordReader.RenderValue` changes beyond the projection + expansion hooks.
- **Single-level expansion only.** No recursion. A FormLink inside an expanded record renders as a FormID string (annotated by `resolve_links` if set), NOT as a deeper expansion. The walker explicitly stops at depth-1.
- **No cycle detection needed.** Single-level expansion ⇒ no cycle hazard.
- **Mutual-exclusion of `formids` and `plugin_names`.** Both are batch-shaped but with different semantics. v2.9.2 enforces XOR at the Python wrapper layer (matches existing `plugin_name` vs `plugin_names` exclusivity per `tools_records.py:875`). **(Locked at conductor sign-off post-Phase-0 — flipped to allow combination per Q6 lock 2026-04-28; cross-product semantics. See PHASE_0_HANDOFF.md § Conductor asks Q6 + PHASE_1_HANDOFF.md for amendment scope. Consumer-driven: Aaron's reasoning is "this will be less rare than you think for building consistency patches across large modlists" — patcher reads each formid across N plugins to compare states. Original wording above kept for design-history; Phase 2 implements cross-product fan-out at the Python wrapper layer; Phase 1 cross-product timing probe Axis 6 confirmed no cliff up to N×M=1000.)**
- **Defaults preserve v2.9.1 behavior.** When all three new parameters are absent, the response is bit-identical to v2.9.1. Coverage-smoke regression band must verify.
- **Probe-first discipline.** Phase 1 starts with the perf probe before anything else lands. Phase 2 references Phase 1's numbers, doesn't speculate.
- **Bonus-catch precedent.** If a phase fix surfaces a related latent issue in the touched code (e.g. a `RecordReader` rendering bug spotted while wiring projection, a Tier-D-equivalent error on the read side, depth-limit DX), fold in (with explicit handoff documentation), per v2.7.1/v2.8.0/v2.9.0/v2.9.1 pattern. >1 h additional work or new operator surface → halt, ask conductor.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive.

### I. Conductor decisions (cross-phase, locked at PLAN write-time)

Things the conductor enforces or decides between phases without re-litigating:

- **Phase identification.** Conductor identifies current phase from highest-numbered handoff in `<plan>/`. Phase executors don't self-identify.
- **Design lock sign-off.** Phase 0's executor proposes the design questions (Q1 path syntax, Q2 expansion shape, Q3 partial-failure for formid lookup, Q4 validation timing, Q5 `formids` capacity caps if any, Q6 mutual-exclusion of `formids` vs `plugin_names`). Conductor relays to Aaron for explicit lock. Phase 1 doesn't begin until the lock is in.
- **Performance threshold sign-off.** Phase 1 surfaces the numbers; if any number is dramatically off (e.g. per-record marginal cost is 200 ms instead of 5–20 ms), the whole v2.9.2 mechanism's value proposition shifts. Conductor relays to Aaron; Phase 2 doesn't begin until the perf shape is acceptable.
- **No Phase 2 split contemplated.** v2.9.2's capability surface is single (read-side parameter additions). If Phase 1 surfaces an unexpectedly-large schema-sweep finding (e.g. a record type with thousands of properties that breaks the validation infrastructure assumption), escalate to Aaron — don't autonomously split.
- **Phase 4 spawn decision.** If Phase 2 + Phase 3 surface zero bridge bugs and zero matrix corrections, conductor skips Phase 4 directly to Phase 5. Otherwise spawns Phase 4 (single session, items 1–N model from v2.9.0/v2.9.1 P4) or Phase 4 sub-sessions per bug if items don't fit one budget.
- **Live install sync timing.** Phase 0 + 1 don't touch live. Phase 3 reads via `mo2_record_detail` against live (no test patches needed — read-only). Phase 4 syncs to live only if a fix needs verification on the live install. Phase 5 syncs once and ships. Conductor confirms sync state before each Phase 3 / 4 / 5 kickoff.
- **Schema migration vs additive.** v2.9.2 is purely additive. No deprecation of existing fields. The new parameters are optional on every code path. Conductor rejects any phase proposing a schema break.
- **Single-commit deliverable for Phase 0.** Per Aaron's task spec ("Single commit when both land"): Phase 0 commits `PLAN.md` + `MATRIX.md` + `CONDUCTOR_KICKOFF.md` (this scoping session's output) + `PHASE_0_HANDOFF.md` in **one work commit + one hash-record commit** — not the prior plans' force-add cycle that staged each artifact separately.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Plan + matrix specification + design proposal | `PLAN.md` (this file), `MATRIX.md` (NEW), `CONDUCTOR_KICKOFF.md` (NEW), `PHASE_0_HANDOFF.md` (NEW); design questions (Q1–Q6) surfaced under § Conductor asks. **Already produced by the scoping session that wrote this plan** — Phase 0 in-session work is matrix scaffold + handoff + commit, not a fresh PLAN draft. | None |
| 1 | Performance baseline probe + record-shape sweep | `tools/race-probe/Program.cs` extended with read-side perf section (subprocess startup time, per-record marginal cost, payload-size baselines, projection-impact, expansion-round-trip-elimination); record-shape sweep of in-scope record types' FormLink-typed fields enumerated via reflection on `IMajorRecordGetter` interfaces in `Mutagen.Bethesda.Skyrim`; `PHASE_1_HANDOFF.md` with numbers + record-shape findings + Layer 3 anchor proposal | Phase 0 with design lock (Q1–Q6) |
| **2** | **Bridge implementation + Python wrapper + functional probes + coverage-smoke regression cells** | `Models.cs` (`ReadRequest.Fields`, `ReadRequest.ExpandLinks`, `ReadBatchRequest.Fields`, `ReadBatchRequest.ExpandLinks`); `RecordReader.cs` (projection walker, expansion resolver, validation pre-flight); `Program.cs` (no command additions; existing `read_records` consumes the new parameters); `tools_records.py` (`mo2_record_detail` schema extension + `formids` / `fields` / `expand_links` plumbing through `_handle_record_detail`); `race-probe` per-axis functional probes; `coverage-smoke` +N regression cells; CHANGELOG; `KNOWN_ISSUES.md`; **version bump to v2.9.2** (Phase 2's first commit) | Phase 1 with perf-shape acceptance |
| 3 | Workflow scenario(s) on live install | Per-scenario assertions in `PHASE_3_HANDOFF.md` (the consumer's 168-record case or close analogue); bug list extended; performance comparison vs Phase 1 baseline | Phase 2 |
| 4 | Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL — conductor decides) | `PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` for sub-sessions); code commits; regression tests | Phase 3 with surfaced findings |
| 5 | Re-run + ship v2.9.2 | Final smoke run; installer + bridge artifact rebuilt; live sync; tag pushed; `gh release create`; memory updated | Phase 4 (or Phase 3 if Phase 4 skipped) |

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one or more commits per its scope. Commit messages start with `[v2.9.2 PN]` (e.g. `[v2.9.2 P2] Read-side efficiency mechanism + version bump to v2.9.2`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its handoff via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
  - **Phase 0 exception** (per § I above): single-commit deliverable bundles `PLAN.md` + `MATRIX.md` + `CONDUCTOR_KICKOFF.md` + `PHASE_0_HANDOFF.md` together. Force-add via one `git add -f Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/{PLAN,MATRIX,CONDUCTOR_KICKOFF,PHASE_0_HANDOFF}.md` invocation, then one work commit + one hash-record commit.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. **Phase 2 bumps the version** on its first commit (read-side efficiency mechanism is the trigger). Subsequent phases don't re-bump. The version slug (`v2.9.2` vs further) is confirmed at PLAN review.
- **Live install sync:** Phases 0, 1, 2 do not touch the live install. Phase 3 reads via `mo2_record_detail` against the live install (read-only — no test patches written). Phase 4 fix sessions live-sync only when the bug requires verification on the live install. Phase 5 live-syncs once and ships.
- **Probe-first discipline:** Phase 1 starts with the perf probe + record-shape sweep. Any Phase 4 fix that touches `RecordReader` projection / expansion logic begins with a probe demonstrating the failure mode.
- **One phase per session, with conductor-mediated handoff between phases.**
- **Don't touch out-of-phase files.** Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves you spot during work.
- **No changes to MCP tool request/response shapes** unless a Phase 4 fix requires it. Phase 2 adds capability via three new optional parameters on `mo2_record_detail`; no shape change beyond the new optional parameters.
- **Double-commit cadence per phase** (work commit + hash-record commit), matching v2.7.1/v2.8.0/v2.9.0/v2.9.1.

---

## 🔁 Handoff template

Every phase ends by writing `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md`) in this directory. **Do not write the next phase's kickoff prompt — the conductor owns that.** Use this exact structure:

```markdown
# Phase N Handoff — <one-line summary>

**Phase:** N (or "4 — <slug>")
**Status:** Complete | Partial | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** <hashes or "none">
**Live install synced:** Yes/No (path: ...)

## What was done
<Bulleted list of concrete changes — file paths + one-line descriptions.>

## Verification performed
<What tests / smoke checks ran. What evidence shows it worked. For Phase 1: probe output + perf numbers + record-shape sweep findings. For Phase 2: per-axis functional probe results + coverage-smoke counts + perf comparison vs Phase 1 baseline. For Phase 3: per-scenario assertion checklist + readback evidence + perf comparison. For Phase 4: probe evidence pre-fix + post-fix.>

## Bugs surfaced (Phase 2, Phase 3 only)
<Per-bug entry: short slug; record type + axis (batch / projection / expansion / composed); reproduction; failure mode; proposed fix angle.>

## Deviations from plan
<Anything you did differently from PLAN.md. Why. If you didn't deviate, write "None.">

## Known issues / open questions
<Bugs you found but didn't fix (with reason). Questions the next phase needs to answer. If none, write "None.">

## Conductor asks
<Optional. Use the format from § Communicating with the conductor at the top of PLAN.md. If none, omit.>

## Preconditions for next phase
<Confirm each precondition the next phase requires per PLAN.md. Flag any not met.>

## Files of interest for next phase
<List paths the next phase will most need to read.>
```

Keep handoffs short — under 400 lines.

---

# PHASES

---

## Phase 0 — Plan + matrix specification + design proposal

**Goal:** Produce `MATRIX.md`, the per-cell test specification scaffolding for v2.9.2. Pre-spec Layer 1 / 2 / 4 cells against vanilla Skyrim.esm and Layer 3 workflow scenarios against the live Authoria modlist (the consumer's 168-record case as the canonical anchor). Surface design questions to Aaron via the conductor: Q1 path syntax (auto-traversal vs explicit `[]`), Q2 expansion output shape (wrapper vs replace), Q3 per-record formid-lookup partial failure (locked default: per-record success), Q4 validation timing (locked default: pre-flight), Q5 `formids` capacity caps (locked default: unbounded; document tested batch sizes), Q6 mutual-exclusion of `formids` vs `plugin_names` (locked default: enforce XOR). **No production code changes. No version bump.**

**Note on cadence.** This scoping session produced `PLAN.md` and `CONDUCTOR_KICKOFF.md` directly. The Phase 0 in-session work is: write `MATRIX.md`, write `PHASE_0_HANDOFF.md`, populate § Conductor asks with Q1–Q6, and bundle the four artifacts (PLAN + MATRIX + CONDUCTOR_KICKOFF + PHASE_0_HANDOFF) into a single work commit + hash-record commit pair.

**Files to touch:**
- `<plan>/PLAN.md` (this file — already written; force-add)
- `<plan>/MATRIX.md` (NEW)
- `<plan>/CONDUCTOR_KICKOFF.md` (NEW; already written by the scoping session — force-add)
- `<plan>/PHASE_0_HANDOFF.md` (NEW — written at end)

**Conductor decisions relevant to this phase:**
- The version slug `v2.9.2` is decided at PLAN review (this phase). If Aaron hasn't decided yet, Phase 0 records the working slug and notes the decision is open; Phase 2 commits the actual version bump.
- Phase 0 does not touch the perf probe — that's Phase 1's deliverable.
- Phase 0's single-commit deliverable bundling is the conductor's structural lock per § I above.

### Steps

1. **Verify session start.** Confirm `origin/main` is at v2.9.1 ship commit (the conductor's kickoff prompt will name the exact hash) and clean. Live install at `<live>` running v2.9.1 (`mo2_ping` returns `version: "2.9.1"`).

2. **Draft `MATRIX.md`** with the five-layer scaffold mirroring v2.9.1's MATRIX.md but anchored on v2.9.2's three composable axes:
   - **Layer 1 — Per-axis coverage (positives).** Cells: `1.P.batch.QUST` (formids batch on QUST), `1.P.batch.RACE` (formids batch on RACE — anchor for the 168-record case), `1.P.fields.RACE.scalar` (projection on a scalar field), `1.P.fields.RACE.list` (projection on a list-typed field; auto-traversal verified), `1.P.fields.RACE.nested` (projection on a nested-property path), `1.P.expand.RACE.formlink` (expansion on a single-FormLink field), `1.P.expand.RACE.list` (expansion on a list-of-FormLinks field — RACE.ActorEffect canonical). Each row: cell ID, axis, source record(s), expected payload shape.
   - **Layer 1.D — Negatives + new explicit error paths.**
     - `1.D.01` — `fields: ["BogusField"]` against RACE → strict-batch validation error per § D #1.
     - `1.D.02` — `expand_links: ["BogusField"]` against RACE → strict-batch validation error per § D #3.
     - `1.D.03` — `expand_links: ["EditorID"]` against RACE (target exists but not FormLink-typed) → strict-batch validation error per § D #2.
     - `1.D.04` — `fields: ["BogusField", "AlsoBogus"]` + `expand_links: ["EditorID", "Spells"]` (one bad expand on existing-but-not-FormLink + one valid expand) → multi-error accumulation per § D's pseudocode; valid expand IGNORED, all bad entries surface together.
     - `1.D.05` — Mixed-type batch (`formids: [QUST, RACE]`) with `fields` valid for one type but not the other → per-record-type error envelope per § D's per-type validation lock.
     - `1.D.06` — `formids: [valid, bogus, valid]` (one bogus formid that doesn't resolve) → top-level success: true; per-record success: [true, false, true]; per § E.
     - `1.D.07` — `formids: ["..."]` AND `plugin_names: ["..."]` (mutual-exclusion violation) → request-shape error per § H mutual-exclusion lock.
   - **Layer 2 — Combinatorial.**
     - `2.01` — All three axes composed: `formids: [N records of same type]` + `fields: [paths]` + `expand_links: [paths]`. Verifies each axis applies independently per record.
     - `2.02` — Composition with `resolve_links: true` per § F. Expanded inline records' FormIDs annotated; projected fields' FormIDs annotated.
     - `2.03` — Mixed-type batch `formids: [QUST × 2, RACE × 2]` + `fields` valid across both types (e.g. `["EditorID"]` — top-level on every record) → per-record application with no validation error.
     - `2.04` — Single-record path (`formid: "..."`) with `fields` + `expand_links` — verifies single-record code path composes with the new parameters.
   - **Layer 3 — Workflow scenario on live.** 1 scenario mirroring the consumer's 168-record case as closely as the live Authoria modlist allows. Phase 0 names the scenario + describes the patcher use case (read 168 RACE records' Skeleton + ActorEffect + Spells + selected nested fields with `expand_links: ["ActorEffect"]` for inline spell detail). Phase 3 picks the live FormIDs at execution time (verifies all 168 in scope; substitute analogous record type if RACE count ≠ 168 in Authoria — the use-case is "many records of one type with FormLink expansions", not specifically RACE). Optional 2nd scenario: NPC_ batch with expansion on `Factions.Faction` for inline faction record detail.
   - **Layer 4 — Edges.**
     - `4.dsl.01` — Empty `formids: []` → request-shape error or empty-success (Phase 0 default: error — empty batch is request-author-error not a valid no-op; matches v2.9.0's "empty list rejected" posture).
     - `4.dsl.02` — `fields: []` → request-shape error symmetric (empty projection vs all-fields default? Phase 0 default: empty-list = error; absence-of-key = full payload).
     - `4.dsl.03` — `expand_links: []` → request-shape error symmetric.
     - `4.dsl.04` — Auto-traversal on a dict-typed property (e.g. RACE.Stats which is dict-shaped per v2.9.0 schema observations) — verifies Q1-locked auto-traversal behavior across dict elements (flatten to list).
     - `4.dsl.05` — `fields` with a path that resolves to a property always-null on the source record → projected output renders the field as `null` (not absent — projection is shape-preserving).
     - `4.dsl.06` — `expand_links` on a FormLink whose target doesn't exist in the load order (e.g. a missing-master FormID) → expanded entry renders as `{ formid: "Missing.esp:01ABCD", expanded: null, error: "..." }` — uniform shape.
   - **Layer 5 — Regression.** All v2.9.1 coverage-smoke cells run unchanged (current count to be confirmed at start-of-Phase-2 against the harness; v2.9.1 P5 ship was 400 cells per CHANGELOG). Specifically: every existing single-`formid` `mo2_record_detail` invocation pattern (used implicitly by patching tests' readbacks) stays bit-identical, and the existing `plugin_names` multi-plugin path stays unchanged.
3. **Pre-spec Layer 3 workflow scenario** with placeholder FormIDs from the live modlist that Phase 3 will swap. Anchor on the consumer's 168-record case: read 168 RACE records (or an analogous Authoria record-type batch) with field projection + expansion in one call; assert the per-record-type token-cost reduction matches Phase 1's projection ratio + Phase 1's per-record marginal cost.

4. **Surface design questions to Aaron via conductor ask** in PHASE_0_HANDOFF.md § Conductor asks (token-efficient bullets):
   - **Q1: Path syntax.** Auto-traversal (`Effects.BaseEffect`) vs explicit bracket-empty (`Effects[].BaseEffect`)? Phase 0 default: auto-traversal. Rationale per § B.
   - **Q2: Expansion output shape.** Wrapper form (`{formid, EditorID, expanded: {...}}`) vs replace-with-inlined-dict? Phase 0 default: wrapper. Rationale per § C.
   - **Q3: Partial failure on formid lookup.** Per-record success/error vs strict-batch fail-the-whole-call? Phase 0 default: per-record success. Rationale per § E (matches existing `read_records` precedent).
   - **Q4: Validation timing for `fields` / `expand_links`.** Bridge-side pre-flight (validate-then-read) vs lazy mid-walk (read-then-fail)? Phase 0 default: pre-flight. Rationale per § D.
   - **Q5: `formids` capacity caps.** Unbounded (default: trust the caller) or impose a soft limit (e.g. 500) at the Python layer? Phase 0 default: unbounded; document the tested batch sizes from Phase 1's perf probe in the schema description. Tester's 168 fits comfortably in any reasonable cap; soft cap is footgun-guard-only.
   - **Q6: Mutual-exclusion of `formids` vs `plugin_names`.** Enforce XOR (Phase 0 default; matches `plugin_name` vs `plugin_names` precedent) vs allow combination (would mean: each plugin × each formid → N×M batch matrix)? Phase 0 default: enforce XOR — the cross-product use case is rare and architecturally distinct (multi-plugin diff vs multi-formid batch). Surface to Aaron in case the consumer's signal includes a cross-product need.
5. **Single-commit deliverable per § I.** Force-add: `git add -f Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/{PLAN.md,MATRIX.md,CONDUCTOR_KICKOFF.md,PHASE_0_HANDOFF.md}`.

6. **Write `PHASE_0_HANDOFF.md`** confirming MATRIX scaffold landed, Layer 3 scenario pre-spec'd, no production code touched, no version bump. Record the working version slug + open-or-decided status. Include the design-question § Conductor asks block.

7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.2 P0] Plan + matrix scaffold + design proposal`
   - Hash-record commit: `[v2.9.2 P0] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 0

- `MATRIX.md` exists with five-layer scaffold + cell-naming convention. Per-axis rows are placeholders awaiting Phase 1's perf-probe-confirmed baselines + record-shape findings.
- Layer 3 scenario named with use-case description (consumer's 168-record case); live-FormID picks deferred to Phase 3.
- `git diff main^` shows: PLAN.md (new), MATRIX.md (new), CONDUCTOR_KICKOFF.md (new), PHASE_0_HANDOFF.md (new). No production code touched.
- Working version slug recorded in handoff.
- § Conductor asks populated with Q1–Q6 in the agreed format.

---

## Phase 1 — Performance baseline probe + record-shape sweep

**Goal:** Quantify the perf gains v2.9.2's three axes deliver — concrete numbers, not estimates. Sweep the in-scope record types' FormLink-typed fields via reflection on `IMajorRecordGetter` interfaces in `Mutagen.Bethesda.Skyrim`. Identify the canonical Layer 3 anchor record type (RACE if Authoria has ~168 RACE records; analogous record type if not). **No bridge code changes.** **No version bump.**

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (extend with read-side perf section + FormLink-field schema sweep)
- `<plan>/MATRIX.md` (update post-Phase-1 with confirmed per-axis baselines + record-shape findings + Layer 3 anchor record type)
- `<plan>/PHASE_1_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Phase 0's design lock (Q1–Q6) is recorded in Phase 0's handoff under § Conductor asks; the conductor's Phase 1 kickoff prompt restates it as the authoritative locked design. If the kickoff prompt lacks the lock, halt and ask conductor — don't infer from PHASE_0_HANDOFF.md.
- Performance-shape sign-off is mandatory before Phase 2 begins. Phase 1 ends with the numbers + record-shape findings; Phase 1's executor writes the "perf shape acceptable?" check to its handoff under § Conductor asks. Conductor relays to Aaron if any number is dramatically off the expected band.
- If the probe surfaces something architecturally unexpected — e.g. RACE.ActorEffect has a renamed-in-0.53.1 property name, or a record type with hundreds of properties that breaks validation infrastructure assumptions — Phase 1 documents it in PHASE_1_HANDOFF.md and writes a CONDUCTOR ASK for whether to expand v2.9.2's scope or punt to a later release.

### Steps

1. **Read MATRIX.md** to understand the Layer 1 cell shape Phase 1 needs to validate FormIDs and record-shape assumptions for.

2. **Extend `tools/race-probe/Program.cs` with a read-side perf section** appended after the existing v2.9.1 P2 multi-condition sweep block:
   - **Subprocess startup cost.** Wall-clock the Python `subprocess.run` invocation for `read_record` against a trivial vanilla GMST or similar minimal record. Repeat 5×; take median.
   - **Per-record marginal cost.** Build `read_records` batches of N = {1, 5, 20, 50, 100, 200} of the same RACE record. Time each batch end-to-end; compute per-record delta over batch-1 baseline. Surface a table.
   - **Per-record full-detail payload size.** Render full-detail JSON for one representative record per type: RACE, NPC_, QUST, MGEF, PERK, ARMO, WEAP, SPEL. Record byte size + field count + max nesting depth.
   - **Projection payload-size impact.** For RACE specifically: full-detail vs projected `fields: [a representative 3–5 path subset]`. Compute byte-reduction ratio. Project the ratio for the other in-scope record types based on their schema shape.
   - **Expansion round-trip elimination.** For RACE.ActorEffect (list of FormLinks to SPEL): time `read_record(RACE) + read_record(each linked SPEL)` sequentially via Python (multiple subprocess invocations); time `read_record(RACE, expand_links=["ActorEffect"])` in one bridge call (Phase 1's probe synthesizes the Phase 2 schema by passing through to a temporary handler — OR, since Phase 2 hasn't landed, instead measures the "without expansion" baseline today + projects the "with expansion" cost as `1 × subprocess startup + N × per-record marginal cost`).
3. **Extend `tools/race-probe/Program.cs` with a record-shape sweep section.** Reflect over every concrete `IMajorRecordGetter`-implementing interface in `Mutagen.Bethesda.Skyrim`:
   - For each: enumerate every public-instance property whose type matches the FormLink predicates from `PatchEngine.cs:1182` (`IFormLinkGetter<>`, `IFormLink<>`, `IFormLinkNullable<>`, `FormLink<>`, `FormLinkNullable<>`) OR a list/collection of such (`IReadOnlyList<IFormLinkGetter<...>>`, `ExtendedList<IFormLink<...>>`, etc.).
   - Print: record type code (4-char ESP code), Mutagen interface name, property name, declared type, "single" or "list" classification.
   - Confirm canonical RACE FormLink-typed fields (`Skeleton` if scalar, `ActorEffect` for the spell list, `Spells`, etc.) and their actual Mutagen 0.53.1 names. v2.7.1's bridge code (PatchEngine.cs:691) named `ActorEffect` (singular) on RACE; the task spec calls it "ActorEffects" (plural) — Phase 1 confirms which is correct against Mutagen 0.53.1.
4. **Build** `cd tools/race-probe && dotnet build -c Release` (zero warnings, zero errors). **Run** `dotnet run -c Release --no-build --project tools/race-probe`. Capture full output to `<workspace>/scratch/v2.9.2-phase-1-perf-and-shape.txt`.

5. **Document findings in PHASE_1_HANDOFF.md:**
   - Subprocess startup cost (median + range).
   - Per-record marginal cost table (batch 1, 5, 20, 50, 100, 200 → per-record delta).
   - Per-record full-detail payload baselines per record type.
   - Projection payload-size-impact ratio per record type.
   - Expansion round-trip-elimination ratio.
   - FormLink-field record-shape sweep table.
   - Canonical RACE FormLink-field names (Mutagen 0.53.1 ground truth).
   - Layer 3 anchor proposal: which record type to target in Phase 3, with rationale (consumer's 168-record case vs Authoria's actual record-type counts).
6. **Write threshold-acceptance proposal to PHASE_1_HANDOFF.md § Conductor asks:**
   - Numbers vs expected band — flag any surprises (e.g. per-record marginal cost > 50 ms suggests Mutagen overlay reads aren't free; Phase 2's projection-skipping might need to be more aggressive).
   - Layer 3 anchor recommendation — which record type, expected count in Authoria, expected token-cost reduction.
   - Default-if-no-response: proceed to Phase 2 with the locked design + Phase 1's measured numbers as the schema-description anchors.
7. **Halt and let the conductor relay to Aaron** if any number is dramatically off (per § I — conductor escalates only if the perf shape's value proposition shifts; otherwise auto-acceptance).

8. **Once the lock is in** (either via conductor relay or auto-accept), update MATRIX.md Layer 1 / 1.D / 2 / 4 rows with confirmed FormID anchors + canonical field names + Phase 1's projection/expansion ratio numbers as expected-payload-size annotations.

9. **Force-add updated MATRIX.md.**

10. **Write `PHASE_1_HANDOFF.md`** documenting:
    - Probe build + run evidence.
    - Perf number tables (5 measurement axes per § G).
    - Record-shape sweep findings + canonical FormLink-field names per in-scope record type.
    - Layer 3 anchor proposal.
    - MATRIX update status (done in this session, or pending Phase 2 first-step depending on lock cadence).
11. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.2 P1] Read-side perf baseline + record-shape sweep`
    - Hash-record commit: `[v2.9.2 P1] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 1

- Perf probe runs to completion; all 5 measurement axes' numbers captured.
- Record-shape sweep table populated (every in-scope `IMajorRecordGetter` × every FormLink-typed field).
- Canonical RACE FormLink-field names confirmed against Mutagen 0.53.1 (probe output, not speculation).
- Layer 3 anchor record type proposed.
- Race-probe build clean.
- MATRIX.md updated (or noted as pending Phase 2 first-step if lock landed too late in this session).
- Handoff under 400 lines; § Conductor asks populated only if a number is dramatically off-band (else auto-accept).

---

## Phase 2 — Bridge implementation + Python wrapper + functional probes + coverage-smoke regression cells

**Goal:** Implement the three composable axes per § A–F. Add `Fields` + `ExpandLinks` to `ReadRequest` and `ReadBatchRequest` in `Models.cs`; extend `RecordReader.cs` with the projection walker, expansion resolver, and pre-flight validation. Extend `tools_records.py`'s `mo2_record_detail` schema with `formids` / `fields` / `expand_links` parameters; add the formid-to-plugin-path resolution loop and route the new parameters into the existing `read_records` batch shape. Lay down per-axis functional probes in race-probe (Mutagen-direct + bridge-subprocess round-trip — projection, expansion, batch, composed). Lay down coverage-smoke regression cells per MATRIX Layer 1 + 1.D + 2 + 4 rows. Bump version to v2.9.2 (this phase's first commit).

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (per-axis functional probes + the perf-and-shape sections from Phase 1 stay)
- `<repo>/tools/mutagen-bridge/Models.cs` (`ReadRequest.Fields` + `ReadRequest.ExpandLinks`; `ReadBatchRequest.Fields` + `ReadBatchRequest.ExpandLinks`)
- `<repo>/tools/mutagen-bridge/RecordReader.cs` (projection walker `RenderValueProjected`; expansion resolver `ExpandFormLinks`; pre-flight validator `ValidateFieldsAndExpandLinks`; integration into existing `Read` and `ReadBatch` per-record render path)
- `<repo>/tools/coverage-smoke/Program.cs` (per-axis regression cells per MATRIX)
- `<repo>/mo2_mcp/tools_records.py` (`mo2_record_detail` schema extension; `_handle_record_detail` extension for formids batch + new params; formid-to-plugin-path resolution loop)
- `<repo>/mo2_mcp/CHANGELOG.md` (new `## v2.9.2 — TBD` entry; Phase 2 bullet)
- `<repo>/mo2_mcp/config.py` (`PLUGIN_VERSION = (2, 9, 2)`)
- `<repo>/installer/claude-mo2-installer.iss` (`#define AppVersion "2.9.2"`)
- `<repo>/README.md` (installer download URL → v2.9.2 — both occurrences per v2.9.1 P2 pattern)
- `<repo>/KNOWN_ISSUES.md` (new entry: "Read-side efficiency (v2.9.2)" describing the three new parameters; depth limit reference unchanged but cross-link to the new schema)
- `<plan>/PHASE_2_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- The Phase 0 design lock (Q1–Q6) and the Phase 1 perf-shape acceptance are recorded in their respective handoffs under § Conductor asks; the conductor's Phase 2 kickoff prompt restates both as authoritative. If the kickoff prompt lacks either, halt and ask conductor — don't infer from prior handoffs.
- **No expansion of mechanism scope beyond v2.9.2's three axes** without explicit conductor approval. If Phase 1 surfaced an interesting fourth read-side optimization (e.g. cross-call result caching, depth-limit exposure), it stays deferred to v2.9.x — even if Phase 2's wiring would be cheap.
- **Single-record `formid` path MUST stay bit-identical when the new parameters are absent.** All v2.9.1 coverage-smoke cells must stay green. The new code path is additive; existing `mo2_record_detail` callers (every patching test's readback assertions) must behave bit-identically.
- **v2.9.1 passthrough lesson (PHASE_4_HANDOFF.md).** v2.9.1 P4 caught a Python wrapper passthrough gap — `condition_target` was added to the bridge model and tool schema but missed `tools_patching.py`'s `passthrough_keys` whitelist. v2.9.2's read-side parameters route through `tools_records.py`'s `_handle_record_detail` instead — different surface, but the lesson generalizes: **end-to-end MCP→bridge round-trip MUST be exercised in Phase 2's smoke before declaring acceptance**, not just direct-bridge race-probe + coverage-smoke.

### Steps

1. **Confirm Phase 0 + Phase 1 locks** from kickoff prompt. State both back to Aaron in your acknowledgement: design-lock summary (Q1–Q6) + perf-shape summary (Phase 1's measured numbers + Layer 3 anchor record type).

2. **Read PHASE_1_HANDOFF.md** for the exact record-shape table Phase 2 transcribes. Phase 2 uses Phase 1's findings — don't speculate on field names.

3. **Extend `Models.cs`** with the new optional fields:

   ```csharp
   public class ReadRequest
   {
       // ... existing fields ...

       /// <summary>
       /// v2.9.2 — optional projection list. Each path is dot-segmented
       /// (e.g. "EditorID", "ActorEffect", "Effects.BaseEffect"). The
       /// reader walks only the requested paths; out-of-projection branches
       /// are omitted from the response. Auto-traverses lists and dicts
       /// per Phase 0 Q1 lock. Empty list: rejected at validation time.
       /// Absence: full payload (v2.9.1 default).
       /// </summary>
       [JsonPropertyName("fields")]
       public List<string>? Fields { get; set; }

       /// <summary>
       /// v2.9.2 — optional FormLink expansion list. Each path is dot-
       /// segmented (e.g. "ActorEffect"). When the walker encounters a
       /// FormLink in a named field, descends into the linked record and
       /// inlines its detail. Single-level only — links inside expanded
       /// records render as FormID strings. Output shape per Phase 0 Q2
       /// lock (wrapper form: { formid, EditorID, expanded }).
       /// </summary>
       [JsonPropertyName("expand_links")]
       public List<string>? ExpandLinks { get; set; }
   }

   // Symmetric extension on ReadBatchRequest.
   ```

4. **Extend `RecordReader.cs`** with the projection walker, expansion resolver, and pre-flight validator:

   ```csharp
   /// <summary>
   /// v2.9.2 — pre-flight validate fields + expand_links against the
   /// record's type. Returns null if valid; an error response if any
   /// path is bad (multi-error accumulation per § D).
   /// </summary>
   private static ReadResponse? ValidateFieldsAndExpandLinks(
       Type recordType,
       List<string>? fields,
       List<string>? expandLinks) { ... }

   /// <summary>
   /// v2.9.2 — render a record with projection applied. When
   /// fields is null/empty, behaves exactly like RenderValue
   /// (the v2.9.1 walker). When fields is non-empty, walks only
   /// the requested paths; out-of-projection branches are omitted.
   /// Auto-traverses lists/dicts per Q1 lock.
   /// </summary>
   private static object? RenderValueProjected(
       object? value,
       List<string>? fields,
       int depth,
       int maxDepth) { ... }

   /// <summary>
   /// v2.9.2 — expand FormLinks in named fields by descending into
   /// the linked record and inlining its detail. Single-level only.
   /// Returns the wrapper form { formid, EditorID, expanded } per
   /// Q2 lock.
   /// </summary>
   private static object? ExpandFormLinkValue(
       IFormLinkGetter link,
       Dictionary<string, ISkyrimModGetter> modCache,
       int depth,
       int maxDepth) { ... }
   ```

   Integrate into `Read` and `ReadBatch`:
   - At the top of `Read` (and per-item in `ReadBatch.ReadOne`): call `ValidateFieldsAndExpandLinks`. If non-null, return the validation error response.
   - Replace `RenderValue(record, ...)` with `RenderValueProjected(record, request.Fields, ...)`.
   - In the projected walker, when a property's path is in `expandLinks` AND the property is FormLink-typed, swap to `ExpandFormLinkValue`.

5. **Extend `Program.cs`** with passthrough plumbing for the new fields. The `read_record` and `read_records` commands deserialize via `JsonSerializer.Deserialize<ReadRequest>` / `<ReadBatchRequest>` — the new fields auto-deserialize via `[JsonPropertyName]`. **No new command added.**

6. **Build the bridge:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

7. **Extend `tools/race-probe/Program.cs` with per-axis functional probes.** For each axis (batch / projection / expansion / composed):
   - Construct a synthetic in-memory mod (or load from Skyrim.esm).
   - Build a `ReadRequest` (or `ReadBatchRequest`) with the new parameters.
   - Pipe a synthetic `bridge_request` through `mutagen-bridge.exe`.
   - Read back the response JSON and assert payload shape matches expected.
   - Cover error paths: bad field path, bad expand target, mixed-type batch with per-type validation, partial-failure formid resolution.
8. **Inline smoke test.** Pick the canonical batch+projection+expansion case (RACE × 10, fields=[3 paths], expand_links=["ActorEffect"]), build a bridge_request, pipe to bridge, parse response, assert per-record-type-correct payload shape. Repeat for the strict-batch error path (one bogus field, one bogus expand target → multi-error response shape).

9. **Add coverage-smoke regression cells** per MATRIX § Layer 1 + 1.D + 2 + 4 rows. Use the existing `read_record` test patterns in `coverage-smoke/Program.cs` as templates (if any exist; otherwise establish the pattern). For each axis: positive cell + negative cell + at least one Layer 4 edge cell. Layer 2.02 cell composes with `resolve_links: true` end-to-end. Keep cell IDs consistent with MATRIX.

10. **Update Python schema description** in `tools_records.py` for `mo2_record_detail`. Add `formids`, `fields`, `expand_links` parameter descriptions:

    ```python
    "formids": {
        "type": "array",
        "items": {"type": "string"},
        "description": (
            "Batch read mode. List of FormIDs ('Plugin:LocalID') to read in "
            "one call — the bridge subprocess starts once and reads N records, "
            "amortizing the ~1.3 s startup across the batch. Output shape "
            "becomes {'records': [...]} with per-record success/error fields "
            "(matching the existing plugin_names path's per-plugin shape). "
            "Mutually exclusive with formid + plugin_name + plugin_names. "
            "Tested up to {N from Phase 1's perf probe} records per call."
        ),
    },
    "fields": {
        "type": "array",
        "items": {"type": "string"},
        "description": (
            "Project the response to only the requested field paths. Each path "
            "is dot-segmented; the walker auto-traverses lists and dicts "
            "(e.g. 'Effects.BaseEffect' descends into each Effect's "
            "BaseEffect property). Default (absent): full payload. Empty "
            "list: rejected. Validation is strict-batch — invalid paths surface "
            "in one error response with the type's full valid-name list."
        ),
    },
    "expand_links": {
        "type": "array",
        "items": {"type": "string"},
        "description": (
            "Inline the detail of FormLinks in named fields. Each path "
            "names a FormLink-typed property; the walker descends into the "
            "linked record and inlines its detail in a wrapper "
            "{'formid', 'EditorID', 'expanded': {...}}. Single-level only — "
            "links inside expanded records render as FormID strings. "
            "Composes with resolve_links: true (the expanded inline records' "
            "FormIDs are also annotated). Validation is strict-batch — invalid "
            "paths or non-FormLink targets surface together."
        ),
    },
    ```

11. **Extend `_handle_record_detail`** with the formids batch path:
    - Detect `formids` parameter; if present + `plugin_name`/`plugin_names`/`formid` also present → mutual-exclusion error.
    - Resolve each formid → winning plugin path via existing index lookup (loop over `_resolve_target` for each).
    - Build `ReadBatchRequest` with `records: [{plugin_path, formid}, ...]` + new `fields` / `expand_links` top-level.
    - Pipe to bridge; parse response.
    - For per-record errors (formid not resolved), include in `out_records[].success: false`.
    - Apply `_enrich_formids` recursively when `resolve_links: true`.
12. **Update `KNOWN_ISSUES.md`:**
    - Add new entry under "Covered as of v2.9.2": "Read-side efficiency mechanism — `mo2_record_detail` accepts `formids`, `fields`, `expand_links` parameters for batch / projection / single-level FormLink expansion. Composes with `resolve_links`. See CHANGELOG v2.9.2 for the schema."
    - Update the existing "RecordReader depth limit" entry — note that projection narrows the walker so depth-limit hits are less likely on projected reads.
13. **Add CHANGELOG entry:**
    ```markdown
    ## v2.9.2 — TBD

    <Phase 5 fills in date.>

    ### Added — bridge + MCP

    - **Read-side efficiency mechanism on `mo2_record_detail`.** Three composable
      optional parameters cut AI-driven workflow token cost by roughly three
      orders of magnitude on read-heavy patching tasks (real consumer signal:
      168-record patching collapsed from ~600+ tool calls to ~1):
      - **`formids: [...]`** — batch read. One subprocess invocation reads N
        records, amortizing the ~1.3 s startup. Per-record success/error
        envelope (matches existing `plugin_names` precedent). Mutually
        exclusive with `formid` / `plugin_name` / `plugin_names`. Tested up
        to {N from Phase 1's perf probe} records per call.
      - **`fields: [...]`** — projection. Dot-segmented paths; walker
        auto-traverses lists and dicts. Shrinks per-record payload by ~80%
        on big records. Default (absent): full payload (v2.9.1 behavior
        preserved).
      - **`expand_links: [...]`** — single-level FormLink expansion. Inlines
        the linked record's detail at the wrapper position
        `{formid, EditorID, expanded: {...}}`. Eliminates second-tier round-
        trips for FormLink-chase patterns.
      All three composable on a single call and orthogonal to `resolve_links`
      (which annotates FormID strings recursively, including inside expanded
      records). Validation is strict-batch — bad field paths and bad
      expansion targets accumulate into one error response per record-type.
      Per-record formid-resolution failures are partial (matches existing
      `read_records` precedent). Single-record path (`formid: "..."`)
      composes with the new parameters bit-identically. v2.9.1 callers using
      single `formid` / `editor_id` / `plugin_name` / `plugin_names` see
      bit-identical responses to v2.9.1.

    <Subsequent phases append entries.>

    ---
    ```

14. **Bump version constants:**
    - `config.py`: `PLUGIN_VERSION = (2, 9, 2)`.
    - `claude-mo2-installer.iss`: `#define AppVersion "2.9.2"`.
    - `README.md`: replace v2.9.1 references at lines 7 and 59 with v2.9.2.

15. **End-to-end MCP→bridge smoke** (per § Conductor decisions — v2.9.1 P4 lesson). Spin up the local MCP server, call `mo2_record_detail` with each of: (a) plain v2.9.1-shape `formid` (regression — must be bit-identical); (b) `formids` batch; (c) `fields` projection; (d) `expand_links` expansion; (e) all three composed with `resolve_links: true`. Confirm each call returns the expected shape end-to-end through the wrapper (catches passthrough gaps that direct-bridge race-probe + coverage-smoke would miss).

16. **Run coverage-smoke end-to-end.** `dotnet run -c Release --no-build --project tools/coverage-smoke`. Capture full output to `<workspace>/scratch/v2.9.2-phase-2-coverage.txt`. Expected: all v2.9.1 cells pass + N new cells pass (N = Layer 1 per-axis cells + Layer 1.D negatives + Layer 2 combinatorial + Layer 4 edges, ~15–20 new cells). All green.

17. **Write `PHASE_2_HANDOFF.md`** documenting:
    - Projection walker + expansion resolver + pre-flight validator implementation hunks + signatures.
    - Per-axis functional probe results.
    - Inline smoke results.
    - End-to-end MCP→bridge smoke results (per § P4 lesson).
    - Coverage-smoke total counts (pre-existing + new = total; PASS / FAIL / SKIP).
    - Schema description diff.
    - CHANGELOG / KNOWN_ISSUES diffs.
    - Version bump landed.
    - Bonus-catch decisions (anything related the phase touched and folded in).
18. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.2 P2] Read-side efficiency mechanism + version bump to v2.9.2`
    - Hash-record commit: `[v2.9.2 P2] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 2

- Phase 1-confirmed property names transcribed into bridge code; no speculation.
- Bridge builds clean (0 warnings, 0 errors).
- Inline smoke + per-axis functional probes pass via Mutagen-direct + bridge-subprocess round-trip.
- End-to-end MCP→bridge smoke confirms every new parameter routes through the wrapper to the bridge correctly (per v2.9.1 P4 lesson).
- Coverage-smoke runs to total (v2.9.1 baseline + N v2.9.2), all PASS or documented SKIP.
- Version bumped in all four version-bearing files.
- Schema description, CHANGELOG, KNOWN_ISSUES updated.
- All v2.9.1 coverage-smoke tests stay green (no regression).
- Handoff under 400 lines.

---

## Phase 3 — Workflow scenario(s) on live install

**Goal:** Run the live workflow scenario(s) against the Authoria modlist via `mo2_record_detail`. Mirror the consumer's 168-record case (or close analogue from Phase 1's record-type recommendation). Verify the per-axis reductions match Phase 1's projected numbers. Capture any surfaced bugs.

**Files to touch:**
- `<plan>/PHASE_3_HANDOFF.md`
- (No test patches written — read-only phase.)

**Conductor decisions relevant to this phase:**
- Live install must be at v2.9.2 (the conductor's kickoff prompt will confirm this and tell you whether a sync was needed). If `mo2_ping` returns < v2.9.2, halt and ask conductor.
- Scenario is picked from MATRIX.md § Layer 3 (Phase 0 named it; Phase 1 confirmed the anchor record type; Phase 3 picks the live FormIDs at execution time). Aaron may swap during Phase 3 if a different record type/count is more representative of the consumer's actual workflow.

### Steps

1. **Verify live install + MCP server.** `mo2_ping` returns `version: "2.9.2"`. If disconnected or wrong version: halt and ask conductor.

2. **Verify Phase 2's wrapper landed in the live install.** Pre-flight: build a single `mo2_record_detail` call exercising `formids: ["<vanilla Skyrim FormID>"]` (1-element batch). If the call fails with "no such field 'formids'" or accepts `formids` but treats it as `formid` (returns single-record shape), the live wrapper is stale — halt and ask conductor to re-sync.

3. **For the Layer 3 scenario in MATRIX.md:**
   - Confirm the target record type + expected count exists in the live modlist (Phase 1's anchor proposal ± live verification).
   - Build the `mo2_record_detail` call with `formids: [N FormIDs]` + `fields: [Phase 1's representative paths]` + `expand_links: [Phase 1's representative paths]`. If the consumer scenario was 168 records, target 168; substitute analogous count if the anchor record type's live count differs.
   - Capture response.
   - Assert per-axis: (a) every formid resolved (or the per-record-error envelope surfaces correctly for unresolved); (b) projected payload contains exactly the requested fields per record; (c) expanded FormLinks have inline detail in the wrapper shape per Q2 lock; (d) `resolve_links: true` annotates FormIDs throughout.
   - Measure end-to-end wall-clock and response token-count. Compare to Phase 1's projection — confirm the reduction shape matches (within ±20%; substantial deviation surfaces a bug).
   - Capture per-scenario result table in handoff.
4. **Stress test.** Run a worst-case batch — every in-scope FormID for the anchor record type, full projection + expansion. Confirm the bridge subprocess completes in under whichever timeout (`mo2_record_detail` uses `timeout=max(15, 5*len(batch_items))`) — Phase 1's perf numbers anchor the expected wall-clock.

5. **Cross-axis rollup.** Summarise pass/fail per Layer 3 assertion. If a pattern of failures emerges (e.g. expansion fails on all FormLinks pointing at a specific master), group by suspected root cause for Phase 4 triage.

6. **Triage failures.** For each FAIL: bug entry with slug, repro, failure mode, proposed Phase 4 fix angle.

7. **Write `PHASE_3_HANDOFF.md`** documenting:
   - Per-scenario assertion table.
   - Bug list (extending Phase 2's, if any).
   - Performance comparison vs Phase 1 baseline (subprocess wall-clock + response token-count).
   - § Conductor asks: any decisions for the conductor (e.g. "Phase 4 needed?" recommendation based on findings).
8. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.2 P3] Layer 3 workflow scenario — N records, M bugs surfaced`
   - Hash-record commit: `[v2.9.2 P3] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 3

- Layer 3 scenario(s) executed against live Authoria modlist.
- Per-axis assertions documented as pass/fail with response evidence.
- Performance comparison vs Phase 1 baseline within ±20%; deviations triaged.
- Bug list extended with workflow-scenario finds.
- Handoff § Conductor asks names whether Phase 4 is needed.

---

## Phase 4 — Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL)

**Goal:** Land all v2.9.2-bound bridge fixes, schema enhancements, matrix corrections, and docs hygiene that Phase 2 + Phase 3 surfaced. Conductor decides whether this phase runs at all (skip if zero findings) and whether it splits into sub-sessions per bug if findings don't fit one budget.

**Files to touch:** Variable per finding. Common candidates:
- `<repo>/tools/mutagen-bridge/RecordReader.cs`
- `<repo>/tools/mutagen-bridge/Models.cs`
- `<repo>/tools/race-probe/Program.cs`
- `<repo>/tools/coverage-smoke/Program.cs`
- `<repo>/mo2_mcp/tools_records.py`
- `<repo>/mo2_mcp/CHANGELOG.md`
- `<repo>/KNOWN_ISSUES.md`
- `<plan>/PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` per sub-session)

**No version bump in Phase 4** — Phase 2 already bumped.

**Conductor decisions relevant to this phase:**
- Conductor reads Phase 2 + Phase 3 handoffs and writes the kickoff naming the specific items in scope. If multiple items, conductor decides single-session-with-items-N vs sub-session-per-bug based on estimated complexity.
- **Scope-lock for Phase 4:** items the kickoff names are in scope. Other v2.7.1/v2.8.0/v2.9.0/v2.9.1 carry-overs (Boolean dispatcher branch, sub-B 6 String functions, AMMO enchantment, replace-semantics dict, chained dict access, QUST.Aliases / Stages / Objectives, PERK.Effects, QuestAlias/QuestLogEntry nested conditions) stay deferred unless the kickoff explicitly absorbs them per Aaron's call. The discipline from v2.8.0 P4 + v2.9.0 P4-INFO + v2.9.1 P4 holds: "don't punt v2.9.2-uncovered findings; pre-existing carry-overs not surfaced fresh stay deferred."
- **Bonus-catch precedent:** fold in only if load-bearing for the current item. >1 h additional or new operator surface → halt + conductor ask + Aaron decision.

### Steps

(Per-item steps depend on what the conductor's kickoff names. The general shape mirrors v2.9.0 / v2.9.1 Phase 4: pre-fix probe → fix → regression test → build clean → coverage-smoke green. See v2.9.1 PLAN.md § Phase 4 for the canonical step structure.)

1. **Confirm scope from kickoff.** List the items in scope to Aaron in your acknowledgement.

2. **Per item:** probe → fix → regression test → smoke green.

3. **Build the bridge** post all fixes. Zero warnings, zero errors.

4. **Run coverage-smoke end-to-end.** All cells from prior phases + new regression cells, all PASS.

5. **Re-run end-to-end MCP→bridge smoke** if any item touched the Python wrapper (per v2.9.1 P4 lesson).

6. **Update CHANGELOG + KNOWN_ISSUES** per items landed.

7. **Write `PHASE_4_HANDOFF.md`** documenting per-item completion, smoke counts, change summaries.

8. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.2 P4] Bridge fixes + matrix corrections + docs hygiene`
   - Hash-record commit: `[v2.9.2 P4] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 4

- All items the kickoff named are landed (or partial state is documented in handoff with reason).
- Bridge builds clean.
- Coverage-smoke at total (v2.9.1 baseline + Phase 2 cells + Phase 4 regression cells), all PASS.
- End-to-end MCP→bridge smoke confirms wrapper passthrough integrity if any fix touched the Python layer.
- CHANGELOG + KNOWN_ISSUES updated.
- Handoff under 400 lines.

---

## Phase 5 — Re-run + ship v2.9.2

**Goal:** Final verification pass + ship the v2.9.2 release. Phase 2 guaranteed code changes; this is always a real release.

**Files to touch:**
- `<repo>/build-output/installer/claude-mo2-setup-v2.9.2.exe` (built artifact)
- `<repo>/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `<repo>/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `<plan>/PHASE_5_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Bridge SHA preservation chain matters. Phase 5's `dotnet publish` produces a NEW SHA (different from Phase 2/4's build SHA). That new SHA is the canonical v2.9.2 ship SHA. It must be byte-identical across smoke matrix, installer bundle, and live install. To preserve: build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller`, which rebuilds the bridge and breaks the chain).
- Layer 3 workflow re-run is required if Phase 4 ran (Phase 4 may have introduced bridge changes Phase 3 didn't see). If Phase 4 was skipped, Phase 3's runs satisfy the re-run requirement.
- Full MO2 process restart required after live sync (not just Tools menu Stop/Start). Conductor confirms this in kickoff.
- **End-to-end MCP→bridge smoke** required as part of Phase 5's live sanity check (per v2.9.1 P4 lesson — direct-bridge tests don't catch wrapper passthrough gaps).

### Steps

(Mirrors v2.9.1 Phase 5 — see v2.9.1 PHASE_5_HANDOFF.md for the canonical 12-step ship sequence with halt cadence.)

1. Verify session start (state checks per kickoff).

2. Final coverage-smoke run against latest bridge build. Confirm 100% pass.

3. **If Phase 4 ran:** re-run Layer 3 scenario(s) against the post-Phase-4 bridge. **If Phase 4 skipped:** skip this step.

4. Build production bridge via `dotnet publish`. Capture SHA.

5. Build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller` — preserves SHA chain). Capture installer SHA.

6. Live sync: copy bridge + Python files to `<live>/`. Aaron full-restarts MO2. `mo2_ping` returns v2.9.2.

7. Live sanity check: 3 distinct paths — (a) batch + projection + expansion + resolve_links composed against the Layer 3 anchor record type (verifies all axes work end-to-end at SHIP_SHA); (b) v2.9.1 regression — single `formid` + `editor_id` + `plugin_names` calls (verifies single-record + multi-plugin paths stay bit-identical); (c) end-to-end MCP→bridge smoke from a live MCP-tool invocation (per v2.9.1 P4 lesson).

8. Insert ship date in CHANGELOG.

9. **Tag + push tag + GitHub release** (PUBLIC; hard to undo). MANDATORY HALT — show Aaron the prepared release-notes draft + exact command sequence; wait for explicit "ship" go-ahead.

10. Update memory (`project_capability_roadmap.md`).

11. Write `PHASE_5_HANDOFF.md`.

12. Final commit + handoff hash-record commit + push.

### Acceptance — Phase 5

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.2` resolves with installer attached.
- `<live>/` running v2.9.2 (`mo2_ping`).
- Memory reflects v2.9.2 shipped.
- SHAs captured.
- Bridge SHA matches across smoke matrix, installer bundle, and live install (single audit anchor).
- Live sanity 3-path check + end-to-end MCP→bridge smoke confirm wrapper integrity.

---

## ⚠️ Carry-overs (NOT addressed in v2.9.2; future-release candidates)

These are explicitly out of scope for v2.9.2 unless real-world testing surfaces them as actually-blocking. If Phase 2/3 surface them as bugs, conductor decides whether to promote to Phase 4 fix scope per the discipline from v2.9.0/v2.9.1 P4.

1. **#7 PERK.Effects (preempted).** v2.9.2 preempted PERK.Effects in response to read-side efficiency consumer signal. PERK.Effects is the planned **v2.9.3** target (the next bridge-mechanism point release after v2.9.2). The KNOWN_ISSUES.md "QUST.Aliases / Stages / Objectives, PERK.Effects" entry stays carry-over until v2.9.3 lands.
2. **Boolean dispatcher branch** (deferred from v2.9.0 — design-only, no in-scope consumer). PLAN.md v2.9.X § A names six branches; v2.9.0 ships five. First v2.9.x consumer trigger lands the branch + cell + name simultaneously.
3. **6 sub-B Condition functions with String-typed slots** (deferred from v2.9.0): GetGraphVariableFloat, GetGraphVariableInt, GetQuestVariable, GetScriptVariable, GetVMQuestVariable, GetVMScriptVariable. Routing requires accept-any-string operator-surface decision.
4. **QuestAlias / QuestLogEntry nested conditions** (deferred from v2.9.1 — KNOWN_ISSUES.md § Patching write surface). Different mechanism (`condition_path` for nested-major sub-records, similar to v2.9.0's INFO override pattern). v2.9.x candidate.
5. **AMMO enchantment.** Mutagen schema gap; upstream change required.
6. **Replace-semantics whole-dict assignment** (Tier C dicts). Carried over from v2.7.1.
7. **Chained dict access** (`Foo[Key].Sub`). Carried over from v2.7.1.
8. **QUST.Aliases / Stages / Objectives.** Out of scope for v2.8.0's bounded Effects-list mechanism — sub-class polymorphism harder; defer until real consumer surfaces. Distinct from v2.9.1's QUST top-level `DialogConditions` / `EventConditions` (shipped) and v2.9.2's read-side mechanism (this release).
9. **GetVATSValueUnknown Mutagen 0.53.1 schema gap.** Deferred from v2.9.0 — bridge dispatcher write is correct; downstream Mutagen serializer throws NotImplementedException. v2.9.x candidate when Mutagen 0.54+ implements the missing override.
10. **Recursive expansion** beyond single-level. v2.9.2 hard-locked at single-level (no cycle detection needed). If a real consumer surfaces recursive-expansion need, it's a v2.9.x candidate with cycle-detection scope.
11. **Cross-call result caching.** v2.9.2 amortizes within a single batched call; cross-call state is a different workstream. Real-consumer-driven if it surfaces.
12. **`ReadRequest.MaxDepth` exposure.** Currently hardcoded to 6; not exposed via MCP. v2.9.x candidate if a real consumer surfaces a depth-limit hit on projected/expanded reads.
13. **All v2.6.0 / v2.7.0 / v2.7.1 / v2.8.0 / v2.9.0 / v2.9.1 deferrals** — see prior plan handoffs.
