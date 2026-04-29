# v2.9.5 — Consumer-facing description redesign

## Trigger

A live consumer Claude session on 2026-04-29 ran ~3,500 sequential `mo2_record_detail` calls in a single workflow instead of using v2.9.2's `formids` batch parameter (shipped 2026-04-28). The consumer self-diagnosed it as "didn't know about" the batch capability. Audit by the v2.9.5 conductor session found the diagnosis was wrong: the v2.9.2 batch params **were** present in the tool schema with detailed property descriptions — but the lead lines were "v2.9.2 batch read mode" / "Phase 1 axis 2" / "Phase 1 axis 6" (internal version/perf markers from the dev process), not action triggers. The operational guidance ("use this any time you need >2 records") was buried five lines deep behind developer jargon. Separately, `kb/KB_Tools.md` duplicated the tool reference content in a hand-curated parallel layer that drifted (the v2.9.2 batch params never made it into KB_Tools.md).

## Architectural shift

**Tool descriptions in `@mcp.tool` registrations ARE the documentation.** Claude reads them at session start as part of the tool registry; they are the consumer-facing surface. Lead with the action trigger ("Use this when reading >2 records") not the version marker ("v2.9.X batch read mode"). Demote internal phase/perf references — operational meaning ranks above provenance for an LLM scanning the registry.

**KB-style summary docs that duplicate tool registry content are forbidden.** They drift the moment a tool description is updated. The `kb/` folder remains available for narrow topic references that stand alone, but never for tool reference duplication.

**Skill descriptions need triggering validation.** The single-most-important skill (`session-strategy`) had a description that triggered on a meta-condition Claude can't predict at trigger-time ("sessions involving extensive MCP work"). Other skills trigger on user-recognizable phrasings ("Use when the user asks about NIF format details", "Use before performing any full-mod analysis"). The fix is description-engineering, validated empirically with `anthropic-skills:skill-creator`'s `scripts/run_loop.py`.

## Changes shipped

### `mo2_record_detail` description rewrite (`mo2_mcp/tools_records.py:335`)

- **Tool-level description** rewritten to lead with action — "Get full interpreted field data for one or more records" (vs prior "for a specific record"). Batching guidance promoted to a bolded second sentence: "**For reading more than ~2 records, prefer the formids batch parameter over multiple parallel calls — each individual call pays a ~900ms subprocess startup; one batched call pays it once.**" Any Claude scanning the tool registry sees the action trigger before any details.
- **`formids` property** rewritten. Old lead: "v2.9.2 batch read mode." New lead: "Read multiple records in a single batched call. Use this any time you need more than ~2 records." Internal references ("Phase 1 perf probe", "Phase 1 axis 2", "Phase 1 axis 6") removed. Performance numbers retained where actionable (~900ms startup, ~19ms marginal at N=200).
- **`fields` property** rewritten. Old lead: "v2.9.2 field projection." New lead: "Project the response to only the requested field paths. Use this when you only need specific fields from a large record — cuts payload ~80% on a 3-5 path subset."
- **`expand_links` property** rewritten. Old lead: "v2.9.2 single-level FormLink expansion." New lead: "Inline the detail of FormLinks at named paths. Use this when you'd otherwise chase a FormLink with a second mo2_record_detail call."

### `mo2_plugin_conflicts` description (`mo2_mcp/tools_records.py:511`)

Added the operational warning that previously lived only in `kb/KB_Tools.md` and the `session-strategy` skill body: **"do NOT call this on plugins that touch CELL or WRLD records heavily — output can be enormous and saturate context. For those, use mo2_query_records filtered to the plugin instead."** Critical for context budgets and now travels with the tool's own schema.

### `session-strategy` skill description (`.claude/skills/session-strategy/SKILL.md:2`)

Trigger description rewritten for accuracy. Per `anthropic-skills:skill-creator` guidance ("Claude undertriggers skills — make descriptions a little pushy") and the live-consumer evidence (the prior description never fired despite the work being eligible).

Old description triggered on a meta-condition Claude can't predict at trigger-time ("sessions involving extensive MCP work"). New description triggers on user-recognizable phrasings: "Use this whenever the user mentions modlists, mods, plugins, conflicts, ESP patches, NPCs, leveled lists, BSAs, NIF meshes, FUZ audio, Papyrus scripts, or record investigations — even if you think you only need a few calls or can answer directly." The "even if" framing is the pushy pattern skill-creator recommends to counter undertriggering.

Skill body content unchanged — it was already current with v2.9.2 batch-read patterns at lines 98-106. The failure was the trigger never firing, not the content being stale.

### `CLAUDE.md`

Replaced the "Knowledge base" section (which instructed consumers to "load `kb/KB_Tools.md` for any MCP session" as a comprehensive tool reference) with a "Tool documentation" section pointing consumers at the MCP tool registry schemas as authoritative documentation. Three-bucket "Building knowledge through use" scheme preserved (modlist rule → addon, procedure → skill, topic reference → kb/) — `kb/` remains available for narrow topic references but comprehensive tool reference duplication is now forbidden.

### Removed

- **`kb/KB_Tools.md`** (160 lines). All non-redundant content already lived in tool schemas (the `mo2_record_detail` rewrite explicitly pulls forward the batching guidance; the CELL/WRLD warning landed in `mo2_plugin_conflicts`). FormID format and field-interpretation output types are part of `mo2_record_detail`'s natural surface.
- **`KNOWLEDGEBASE.md`** (10-line index). With KB_Tools.md gone, the index pointed at nothing.
- Two `Source:` lines in `installer/claude-mo2-installer.iss` that bundled the retired files.

### README.md

- Install link bumped to v2.9.5.
- "Tool Reference" section: replaced the "See `kb/KB_Tools.md`" pointer with text noting that each tool's full schema is registered with the MCP server and visible to any MCP client at session start.
- "Addon System" section: added the `.claude/skills/<name>/SKILL.md` bullet (previously implicit via CLAUDE.md). Updated the `KB_[Topic].md` bullet to clarify it's for narrow topic references not comprehensive tool reference.

## Mechanism for future sessions

`feedback_descriptions_are_documentation.md` (auto-memory) codifies the rule:

> Tool descriptions in `@mcp.tool` registrations and skill `description:` frontmatter are the authoritative consumer-facing documentation. Lead with the action trigger ("Use this when reading >2 records") not the version marker ("v2.9.X batch read mode"). KB-style comprehensive summary docs duplicating tool registry content are forbidden — they drift. Skill description rewrites to critical skills (session-strategy especially) require empirical triggering validation via `anthropic-skills:skill-creator`'s `scripts/run_loop.py` before ship.

This applies to any future Claude_MO2 dev session that touches `mo2_mcp/tools_*.py` `@mcp.tool` registrations or `.claude/skills/*/SKILL.md` frontmatter.

## Validation

`session-strategy`'s new description was validated empirically with `anthropic-skills:skill-creator`'s `scripts/run_loop.py` against a 20-query eval set (10 should-trigger + 10 should-not-trigger, including near-miss negatives that share modding keywords but don't need MCP tools). Eval set archived at `dev/plans/v2.9.5_descriptions_redesign/eval_set.json`. Result captured in `PHASE_1_HANDOFF.md` after the loop completes.

## Code-side scope

**No code changes, no bridge changes.** v2.9.5 is description-and-docs-only at the source level. The bridge is re-published per Q7 lock (carried from v2.9.3) for SHA-chain hygiene; SHIP SHAs may differ from v2.9.4's only by .NET build determinism factors. v2.9.4's `_AUTOSTOP_EXEMPT_PATTERN` deny-list and v2.9.3's PERK.Effects writability are unchanged.

## Cadence deviation

Prior v2.9.x releases (v2.9.0 → v2.9.4) were multi-phase ships with PLAN.md / MATRIX.md / per-Phase-handoff archives. v2.9.5 is a single-session description-and-docs ship — no race-probe, no PLAN-vs-actual schema reconciliation, no coverage-smoke matrix expansion. The plan archive (this directory) carries PLAN.md + PHASE_1_HANDOFF.md + eval_set.json + run_loop output. The CHANGELOG.md v2.9.5 entry is the canonical narrative.
