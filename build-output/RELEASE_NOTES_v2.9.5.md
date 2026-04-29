# Claude MO2 v2.9.5 — Consumer-facing description redesign

**Release date:** 2026-04-29 (evening)

**Type:** Description-and-docs-only release. No code changes, no bridge changes, no behavior changes to any tool.

## Summary

Tool descriptions in `@mcp.tool` registrations and skill `description:` frontmatter are now treated as the authoritative consumer-facing documentation, replacing the parallel `kb/KB_Tools.md` summary layer that drifted.

## Why

A live consumer Claude session on 2026-04-29 ran ~3,500 sequential `mo2_record_detail` calls in a single workflow instead of using v2.9.2's `formids` batch parameter (shipped 2026-04-28). The audit found the v2.9.2 batch params **were** present in the tool schema with detailed property descriptions — but the lead lines were "v2.9.2 batch read mode" / "Phase 1 axis 2" / "Phase 1 axis 6" (internal version/perf markers from the dev process), not action triggers. The operational guidance ("use this when reading more than ~2 records") was buried five lines deep behind developer jargon. Separately, `kb/KB_Tools.md` duplicated the tool reference content in a hand-curated parallel layer that drifted (the v2.9.2 batch params never made it into KB_Tools.md).

## What changed

### Tool description rewrites

- **`mo2_record_detail`** — tool-level description now leads with action: "Get full interpreted field data for one or more records" + a bolded second sentence promoting the `formids` batch parameter for >2 records. Property descriptions for `formids`, `fields`, `expand_links` rewritten to lead with action triggers ("Read multiple records in a single batched call. Use this any time you need more than ~2 records") instead of internal version/phase markers. Performance numbers retained where actionable.
- **`mo2_plugin_conflicts`** — gained the operational warning that previously lived only in `kb/KB_Tools.md` and the `session-strategy` skill body: do NOT call on plugins that touch CELL or WRLD records heavily (output saturates context). Use `mo2_query_records` filtered to the plugin instead.

### Skill description rewrite

- **`session-strategy`** — `description:` frontmatter rewritten for trigger reliability. Old description triggered on a meta-condition Claude can't predict at trigger-time ("sessions involving extensive MCP work"). New description triggers on user-recognizable phrasings ("Use this whenever the user mentions modlists, mods, plugins, conflicts, ESP patches, NPCs, leveled lists, BSAs, NIF meshes, FUZ audio, Papyrus scripts, or record investigations") with the "even if you think you only need a few calls" pushy framing per `anthropic-skills:skill-creator` guidance. Skill body content unchanged — it was already current with v2.9.2 batch-read patterns.

### Documentation

- **`CLAUDE.md`** — replaced the "Knowledge base" section with "Tool documentation" pointing consumers at the MCP tool registry schemas as authoritative. Three-bucket "Building knowledge through use" scheme preserved.
- **`README.md`** — install link bumped, "Tool Reference" section repointed at MCP tool registry, "Addon System" section gained `.claude/skills/<name>/SKILL.md` bullet and clarifies `kb/KB_[Topic].md` is for narrow topic references not comprehensive tool reference.
- **`mo2_mcp/CHANGELOG.md`** — full v2.9.5 entry.

### Removed

- `kb/KB_Tools.md` (160 lines) — duplicated tool registry content; drifted.
- `KNOWLEDGEBASE.md` (10 lines) — index that pointed at nothing once KB_Tools was gone.
- Two `Source:` lines in `installer/claude-mo2-installer.iss` for the retired files.

## Architectural rule going forward

Tool descriptions in `@mcp.tool` registrations and skill `description:` frontmatter are Claude-facing documentation, not changelog entries. Lead with the action trigger ("Use this when reading >2 records") not the version marker ("v2.9.X batch read mode"). Demote internal phase/perf references — operational meaning ranks above provenance for an LLM scanning the tool registry. KB-style comprehensive summary docs that duplicate tool registry content are forbidden.

This rule is captured as a feedback memory on the conductor's machine for all subsequent Claude_MO2 dev sessions.

## Validation

`session-strategy`'s new description was engineered following the proven pattern from the other 10 working skills (concrete user-recognizable phrasings, "Use when..." action lead, pushy "even if" framing). Empirical validation via `anthropic-skills:skill-creator`'s `scripts/run_loop.py` was attempted but blocked by a Windows-environment incompatibility (`select()` cannot poll subprocess pipes on Windows; `run_eval.py` line 11). Validation infrastructure works on Linux/macOS. Manual reasoning recorded in `dev/plans/v2.9.5_descriptions_redesign/PHASE_1_HANDOFF.md`.

## Upgrade hygiene

Upgrading over v2.9.4 leaves the old `kb/KB_Tools.md` and `KNOWLEDGEBASE.md` in `<MO2>/plugins/mo2_mcp/` as orphans — Inno Setup only removes files it installed, not files that are no longer in the [Files] manifest. They're harmless (CLAUDE.md no longer routes to them) but can be manually deleted for tidiness. A clean reinstall (delete the plugin folder first, then run the installer) avoids this entirely.

## SHIP SHAs

- `mutagen-bridge.dll` SHA256: `8acd969abff44f8275549c1b383105f1a3e3fbd941c6688f75b3facd061aaaaf`
- `mutagen-bridge.exe` SHA256: `80e980c058927a88320187b134bca8fdbf615e2a8497043fa1f8da7ed228f8a4`
- `claude-mo2-setup-v2.9.5.exe` (10,632,936 bytes) SHA256: `27e0e8c50dbc36b4e51b7412e5265c00480e5bdd897e1923905472eefeedef37`

3-way SHA chain (publish == build-output == live install) verified for bridge. Bridge re-published per Q7 lock for SHA-chain hygiene; v2.9.5 has zero bridge code changes at the source level. Installer SHA recorded fresh.
