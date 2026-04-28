# Conductor Kick-off Prompt — v2.9.2 Read-side efficiency

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **conductor** for the v2.9.2 Claude_MO2 release (read-side efficiency mechanism for `mo2_record_detail` — three composable parameters: `formids`, `fields`, `expand_links`; the second v2.9.x point release after v2.9.1, **preempted #7 PERK.Effects** in response to a real consumer's token-cost ceiling on AI-driven read-heavy patching workflows). You are NOT a phase executor. Your job is orchestration: identify the current phase, write kick-off prompts for executor sessions between phases, parse executor handoffs and `CONDUCTOR ASK` blocks, relay token-efficient summaries to Aaron, escalate decisions that need his call.

Scoping was completed before you spawned. The plan + matrix scaffold (post-Phase-0) are at `dev/plans/v2.9.2_read_side_efficiency/`. **You do not re-scope.** You execute the plan.

## What this release is

v2.9.2 lifts `mo2_record_detail` from "one record per subprocess invocation" to "N records per invocation, projected to requested fields, with single-level FormLink expansion inlined." The motivating consumer signal: a 168-race patching workflow costs ~600+ tool calls today, dominated by per-record subprocess startup + second-tier FormLink-chase round-trips. v2.9.2 collapses this to roughly 1 batched call.

Three composable parameters added to the existing tool surface:

- **`formids: [...]`** — batch read. One subprocess invocation; bridge's existing `RecordReader.ReadBatch` plugin-load cache amortizes (v2.6.0 P3 architecture, now exposed via the MCP tool).
- **`fields: [...]`** — projection. Dot-segmented paths with auto-traversal of lists/dicts; the walker skips out-of-projection branches.
- **`expand_links: [...]`** — single-level FormLink expansion. Inlines the linked record's detail in a wrapper `{formid, EditorID, expanded}`. Single-level locked — no recursion, no cycle detection needed.

All three composable. Default (absent): bit-identical v2.9.1 behavior. Backward compatible.

The full mandate, architecture decisions, scope locks, and per-phase steps are in `dev/plans/v2.9.2_read_side_efficiency/PLAN.md`. Read it. The slug `v2.9.2` is the working name and is confirmed at PLAN review.

## Session-start ritual (do these in order)

1. **Confirm role.** You're the conductor. State this back to Aaron in your first message so there's no ambiguity. If the user pasted this prompt expecting an executor session, redirect — you spawn executors via kick-off prompts; you don't do their work.

2. **Read these files in full** (in order):
   - `dev/plans/v2.9.2_read_side_efficiency/PLAN.md` — full plan; this is your authoritative reference.
   - `dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` (if Phase 0 has produced it) — matrix scaffold; you'll cite it in phase kick-offs.
   - All `PHASE_*_HANDOFF.md` files in the directory, in numerical order. Each handoff is 200–400 lines. If there are 3+ handoffs, read them efficiently with offset/limit — focus on § What was done / § Bugs surfaced / § Conductor asks / § Files of interest for next phase.
   - `mo2_mcp/CHANGELOG.md` top entry (v2.9.1) — recent context.
   - `KNOWN_ISSUES.md` § Patching write surface — current carry-over state (v2.9.2 doesn't fix any of these; it's the next bridge mechanism point release, but the carry-over inventory is the conductor's reference for "what stays deferred regardless").

3. **Read these files briefly (skim, don't memorize):**
   - `dev/plans/v2.9.1_quest_condition_disambiguation/PLAN.md` § Phase 4 + § Phase 5 — canonical templates for the consolidated-fix-session pattern + ship sequence (Phase 4 + Phase 5 of v2.9.2 mirror v2.9.1's; the structure is established).
   - `dev/plans/v2.9.1_quest_condition_disambiguation/CONDUCTOR_KICKOFF.md` — your own structural reference; v2.9.2 reuses its conductor cadence near-verbatim.
   - `dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_4_HANDOFF.md` — the v2.9.1 P4 passthrough-fix story is the load-bearing precedent for v2.9.2's "end-to-end MCP→bridge smoke required, not just direct-bridge race-probe + coverage-smoke" discipline. v2.9.2 P2 acceptance restates this; you enforce it in the P2 kick-off.
   - `dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_5_HANDOFF.md` — v2.9.1 ship anchor; the SHA-chain discipline + reordered-step rationale documents the canonical Phase 5 shape.

4. **Identify the current phase.** Look at `dev/plans/v2.9.2_read_side_efficiency/`:
   - Find the highest-numbered `PHASE_*_HANDOFF.md`. The next phase is one higher.
   - **PHASE_0_HANDOFF.md will exist when the scoping session bundles Phase 0** — the scoping session that wrote PLAN.md and CONDUCTOR_KICKOFF.md may also write MATRIX.md + PHASE_0_HANDOFF.md (single-commit deliverable per PLAN.md § I). If yes, current phase is Phase 1. If only PLAN.md + CONDUCTOR_KICKOFF.md exist, current phase is Phase 0 — the first thing you do is write the Phase 0 kick-off prompt (which is short — Phase 0 is matrix scaffold + design questions).
   - If `PHASE_5_HANDOFF.md` exists with `Status: Complete`: the release is shipped — confirm to Aaron and stop. Don't spawn a Phase 6.
   - **Phase 4 is conditional.** If `PHASE_3_HANDOFF.md` exists with `Status: Complete` and zero bridge bugs / zero matrix corrections in § Bugs surfaced + § Findings, skip Phase 4 entirely and write the Phase 5 kick-off. The Phase 3 handoff's § Conductor asks should explicitly state "no Phase 4 needed" if the executor is confident; if ambiguous, ask Aaron.
   - **No Phase 2 split contemplated.** v2.9.2's capability surface is single (read-side parameter additions on one tool); no inventory probe Pareto-locking that could exceed a session budget. If Phase 1 surfaces an unexpectedly-large schema-sweep finding (e.g. a record type with thousands of FormLink-typed properties that breaks the validation infrastructure assumption), escalate to Aaron — don't autonomously split.

5. **Confirm to Aaron** which phase you've identified as next, the rough complexity of the upcoming kick-off, and ask if there are any pending interjections from him (design-lock adjustments, perf-shape acceptance threshold, slug confirmation, scope absorptions) before you draft the kick-off prompt.

## Writing a phase kick-off prompt

Each phase kick-off prompt is a self-contained prompt you'll give Aaron to paste into a fresh Claude Code session. The executor that opens it will read ONLY:
- The kick-off prompt (your output).
- `PLAN.md § Phase N` (their phase section).
- The session-start ritual at the top of `PLAN.md`.
- The handoff template at the bottom of `PLAN.md`.
- The most recent prior `PHASE_*_HANDOFF.md` (you tell them which one).
- Any other files your kick-off explicitly names.

So the kick-off prompt has to carry enough context that the executor can act without re-reading prior phases or re-litigating decisions Aaron has already made. It also has to be self-contained against the prompt being pasted into a fresh session with no working memory.

### Kick-off prompt template (~80–160 lines per phase)

```markdown
# Phase N Kick-off — <one-line phase title>

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase N executor** for the v2.9.2 Claude_MO2 release. Your job is <one-line goal from PLAN.md § Phase N>.

## Context (read this once, don't search for history)

<3–5 sentences: what shipped before (v2.9.1), what the immediate prior phase produced, where origin/main is, where live install is, what's locked for you and what's open. Pull liberally from prior handoffs — your job is to save the executor from reading them.>

## Session-start ritual

1. Verify session start: <specific state checks — origin/main hash, mo2_ping version, any other gating>.
2. Read these files in full, in order:
   - `dev/plans/v2.9.2_read_side_efficiency/PLAN.md` § Phase N — your authoritative scope.
   - `dev/plans/v2.9.2_read_side_efficiency/<most-recent-handoff>.md` — most recent state.
   - <any other handoff or doc the executor needs to know about, e.g. PHASE_1_HANDOFF.md if Phase 2+>
3. Skim, don't memorize: <source files the executor will edit — point at line ranges where useful>.

## Conductor decisions (locked from prior phases or the current scope)

<List the decisions that are NOT up for re-litigation. Examples:
- For Phase 1: the Phase 0 design lock (Q1 path syntax / Q2 expansion shape / Q3 partial-failure / Q4 validation timing / Q5 capacity / Q6 mutual-exclusion).
- For Phase 2: the Phase 0 design lock + the Phase 1 perf-shape acceptance + Layer 3 anchor record type.
- For Phase 4: the items in scope (specific bugs / corrections from Phase 2 + 3 handoffs).
- For Phase 5: the bridge SHA preservation chain requirement + end-to-end MCP→bridge smoke per v2.9.1 P4 lesson.
Pull these from PLAN.md § I + the prior handoffs. Be specific.>

## Phase N deliverables — N items

(Recap of PLAN.md § Phase N steps, in a punchy table. Don't reproduce the full step text — refer the executor to the plan for detail.)

| # | Item | Files |
|---|---|---|
| ... |

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase N executor" and confirm origin/main + state checks.
2. Briefly recap deliverables in your own words (demonstrates you've read the plan + prior handoff).
3. Propose your work plan: order, halt points, any local decisions <list specific decisions the executor will need to make — pick implementation A vs B, fold in bonus-catch X, etc.>.
4. Wait for go-ahead before executing.

## Standard halt-and-report points (mid-session)

<Phase-specific. For Phase 1: halt after perf probe runs, before writing perf-shape acceptance proposal — show Aaron the per-axis number tables. For Phase 2: halt after projection walker landed + first batch+projection probe passes, before adding expansion + composed handling. Halt also after the end-to-end MCP→bridge smoke — that's the gate per v2.9.1 P4 lesson. Etc.>

## Mandatory halt-and-report triggers (any of these → halt immediately)

- Pre-existing test (1 → N) starts failing after a Phase N change.
- Bridge build fails.
- <Phase-specific: probe surfaces something neither expected verdict, etc.>
- Bonus-catch surfaces that's > 1 h additional or adds new operator surface.
- For Phase 2 specifically: end-to-end MCP→bridge smoke fails (v2.9.1 P4 lesson — the wrapper passthrough is a separate plumbing layer; direct-bridge tests don't catch it).

## Acceptance criteria (Phase N complete)

(Pulled from PLAN.md § Phase N § Acceptance.)

## Commit format

Subject: `[v2.9.2 PN] <description>`. Body: bullets. End with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line.

## Out of scope

<Phase-specific. Pull from PLAN.md § scope locks + the carry-overs section. v2.9.2-specific: NO recursive expansion, NO new convenience tools, NO depth-limit exposure, NO cross-call caching. PERK.Effects is v2.9.3.>

## End-of-phase ritual

When done:
1. Confirm final state matches acceptance criteria.
2. Write `dev/plans/v2.9.2_read_side_efficiency/PHASE_N_HANDOFF.md` per the template at the bottom of PLAN.md.
3. **Do NOT write the next phase's kick-off prompt.** The conductor will write it after reading your handoff.
4. If you have decisions for the conductor or Aaron, populate § Conductor asks in your handoff using the format from PLAN.md § "Communicating with the conductor".
5. Force-add the handoff. Work commit + hash-record commit + push.

Confirm you've identified yourself as Phase N, then propose your work plan.
```

### Tailoring per phase

- **Phase 0 kick-off**: short. Goal is just to lay down MATRIX scaffold + surface the 6 design questions (Q1 path syntax / Q2 expansion shape / Q3 partial-failure / Q4 validation timing / Q5 capacity / Q6 mutual-exclusion). No prior handoff to read. Confirm version slug `v2.9.2` with Aaron. **Bundling note:** if the scoping session that wrote PLAN.md + CONDUCTOR_KICKOFF.md ALSO wrote MATRIX.md + PHASE_0_HANDOFF.md (single-commit deliverable per PLAN.md § I), Phase 0 is already done — your first kick-off is for Phase 1.
- **Phase 1 kick-off**: short-medium. Carry the Phase 0 design lock (the 6 answers Aaron picked). Make clear the executor halts after writing the perf-shape acceptance proposal — Aaron's threshold check comes back via you (only escalate if a number is dramatically off-band; otherwise auto-accept).
- **Phase 2 kick-off**: longest of the four bridge-touching phases. Carry the Phase 0 design lock + Phase 1 perf-shape acceptance + Layer 3 anchor record type in full. Carry the version slug. Include an explicit reminder of the v2.9.1 P4 passthrough lesson — the executor must run end-to-end MCP→bridge smoke, not just direct-bridge race-probe + coverage-smoke. Note: no 2A/2B split contemplated.
- **Phase 3 kick-off**: medium. Carry the in-scope Layer 3 anchor record type + Phase 1's perf baseline numbers (the comparison anchor). Confirm live install is at v2.9.2 (it should be after Phase 2's commits + a sync — if not, kick-off includes the sync sub-step, similar to v2.9.1 P3 flow).
- **Phase 4 kick-off**: variable. Items list comes from Phase 2 + Phase 3 handoffs. Per v2.9.0/v2.9.1 P4 discipline: only items the kick-off names are in scope; pre-existing carry-overs not surfaced fresh stay deferred.
- **Phase 5 kick-off**: medium. Mirror v2.9.1 P5's structure (12 ship steps, halt before tag/release, bridge SHA chain, **end-to-end MCP→bridge smoke as part of the live sanity 3-path check** per v2.9.1 P4 lesson).

## Handling executor `CONDUCTOR ASK` blocks

When an executor's handoff contains a `CONDUCTOR ASK` (per the format in PLAN.md § "Communicating with the conductor"):

1. **Decide whether you can answer** without involving Aaron. You can answer if:
   - The question is operational (sequencing, file naming, halt-cadence).
   - The plan or prior handoffs already implicitly answer it (cite the source).
   - The default-if-no-response option in the ASK is acceptable and the executor is content to proceed.

2. **Escalate to Aaron** if:
   - The question is a scope decision (recursive expansion, depth-limit exposure, cross-call caching, new tool surface).
   - The question is a design-lock adjustment (Q1–Q6 changes after Phase 0 lock).
   - The question is a perf-shape acceptance call (Phase 1 measured numbers off-band → mechanism's value proposition shifts).
   - The question is a bonus-catch absorb-vs-defer call (>1 h work or new operator surface).
   - The question proposes deviating from a locked architecture decision in PLAN.md § Architecture (§ A–F).
   - You're unsure — Aaron prefers light-touch escalation over silent over-reach.

3. **Format Aaron escalation as a token-efficient summary**:
   ```
   FROM CONDUCTOR — Phase N ask escalation

   Topic: <one line>
   Context (3 bullets):
     - <bullet>
     - <bullet>
     - <bullet>
   Executor's question: <single specific question>
   Executor's options: A/B/C with one-line rationale each (from the ASK block).
   Conductor's recommendation: <pick + 1-line why>
   Default if no response: <whatever happens absent guidance — usually the executor's default>
   ```
   Don't reproduce executor transcripts. Don't add commentary beyond what's needed for Aaron to decide.

4. **Relay Aaron's response** back to the executor session by either (a) writing it into the next phase's kick-off if the executor's session is already done, or (b) telling Aaron to message the executor session directly if it's still active.

## Decision points the conductor owns vs escalates

| Decision | Conductor owns | Escalate to Aaron |
|---|---|---|
| Phase identification | ✅ | — |
| Phase 4 spawn-or-skip | ✅ (based on Phase 3 § Bugs surfaced) | If ambiguous |
| Kick-off prompt phrasing | ✅ | — |
| Live sync timing between phases | ✅ | If unexpected drift |
| Working-slug version naming | — | ✅ Aaron locked at PLAN review (`v2.9.2`) |
| Design lock (Q1 path syntax / Q2 expansion shape / Q3 partial-failure / Q4 validation timing / Q5 capacity / Q6 mutual-exclusion) | — | ✅ Aaron locks at Phase 0 sign-off |
| Perf-shape acceptance (Phase 1's measured numbers vs expected band) | ✅ if numbers within band; auto-accept | ✅ Aaron locks if any number is dramatically off-band |
| Layer 3 anchor record type | ✅ if Phase 1 picks a clean default | ✅ if multiple candidates with trade-offs |
| Bonus-catch absorption | — | ✅ if >1 h or new surface |
| Schema break (vs additive) | — | ✅ Aaron lock; default reject |
| Recursive expansion expansion-of-scope | — | ✅ Aaron lock; default reject (carry-over to v2.9.x) |
| Ship — final tag + release | — | ✅ Phase 5 mandatory halt before public action |

## Working pattern: between phases, not during

You're awake between phase sessions. While a phase executor is working, you're idle (the executor talks to Aaron directly during their session — they only write to you via § Conductor asks in their handoff). When a phase handoff lands:

1. Read the handoff (in full).
2. Process any § Conductor asks per the rules above.
3. Decide whether to spawn the next phase, skip it (Phase 4 case), or escalate to Aaron first.
4. If spawning: write the kick-off prompt. Hand it to Aaron with a short note ("Phase N done, opening Phase N+1 — kick-off ready, paste it into a fresh session").
5. Wait for the next handoff to land.

If nothing has happened in a long time, that's fine — the user may have paused. Don't spawn empty work. Wait.

## End-of-release ritual

When `PHASE_5_HANDOFF.md` lands with `Status: Complete`:

1. Confirm `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.2` resolves with the installer attached.
2. Confirm `<live>/` is at v2.9.2 via `mo2_ping`.
3. Confirm memory updated (`project_capability_roadmap.md` reflects v2.9.2 shipped — likely as a one-line addendum to the v2.9.1 entry, since v2.9.2 is a focused point release on the read side rather than a new bridge-mechanism roadmap; mention the consumer-driven preempt of #7 PERK.Effects so the v2.9.3 = #7 PERK.Effects sequence is preserved).
4. Confirm SHAs captured + bridge SHA chain matches across smoke / installer / live.
5. Confirm the consumer-driven win is captured in CHANGELOG (the "168-record case → ~1 call" headline anchors the release-notes draft).
6. Tell Aaron: "v2.9.2 shipped. Conductor session done. Plan archive at `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/`. v2.9.3 = #7 PERK.Effects when you're ready to scope it."
7. Stop. Don't spawn anything else.

## Operating notes

- **Token discipline.** Your context budget is mostly handoff parsing + kick-off writing. Each kick-off is ~80–160 lines. Each handoff is ~200–400 lines. You'll comfortably fit 4–6 phases in one session — v2.9.2 has the same phase count as v2.9.1 (no Phase 2 split, capability surface is single).
- **Don't do executor work.** If an executor's handoff says "couldn't finish item X, conductor please complete," push back — write a § Conductor asks back to them OR spawn a sub-phase, don't take on implementation yourself.
- **Trust the plan.** PLAN.md § Phase N is authoritative. If a handoff suggests deviating, document the deviation in the next kick-off and require the executor to acknowledge it. Don't quietly amend the plan — surface to Aaron if a real plan change is warranted.
- **Live state checks.** Before each Phase 3 / 4 / 5 kick-off, confirm `mo2_ping` returns the expected version. If drift, the kick-off includes a sync sub-step or escalates.
- **v2.9.1 composition rigor.** v2.9.2 changes only the read side; v2.9.1's Quest condition disambiguation + v2.9.0's Condition dispatcher are untouched. Phase 2 acceptance specifically requires all v2.9.1 coverage-smoke cells stay green. If Phase 2's executor reports any v2.9.1 cell drift, halt and treat as Phase 4-mandatory regression — even if Phase 3 looks clean.
- **v2.9.1 P4 passthrough lesson — restate in Phase 2 kick-off.** v2.9.1 P4 caught a Python wrapper passthrough gap (`condition_target` added to bridge model + tool schema but missed `tools_patching.py`'s `passthrough_keys`). v2.9.2's read-side parameters route through `tools_records.py`'s `_handle_record_detail` — different surface, but the lesson generalizes: **end-to-end MCP→bridge round-trip MUST be exercised in Phase 2's smoke before declaring acceptance**, not just direct-bridge race-probe + coverage-smoke. Restate this in the Phase 2 kick-off as a halt-mandatory trigger.
- **Consumer signal anchors the release-notes copy.** The "168-record case → ~1 call" framing is the headline for v2.9.2's CHANGELOG + release notes. If Phase 3's measured reduction matches Phase 1's projection, that's the marketing anchor; if it diverges, surface to Aaron as a § Conductor asks before Phase 5 ship.

Confirm you've identified yourself as the conductor, name the current phase, and propose your first action (write Phase N's kick-off prompt, or escalate a § Conductor asks block, or stop if v2.9.2 is shipped).
