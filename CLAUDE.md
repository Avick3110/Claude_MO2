# CLAUDE.md — Claude MO2

The MCP server runs inside MO2 and exposes the `mo2_*` tools. If those tools aren't in your tool list, the server isn't connected — tell the user immediately and stop. Do not load KB files, scan for addons, or run any other startup steps until the server is confirmed available.

### If the server isn't responding
1. Is the server running in MO2? (Tools menu shows it as active)
2. Does `~/.claude.json` contain an `mcpServers.mo2` entry?
3. First-time only: did the user restart Claude Code after the very first server start?
4. Call `mo2_ping` to verify.

## First session setup

Once `mo2_*` tools are available:

- **Record index builds lazily.** Don't pre-call `mo2_build_record_index`. The first read query (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) builds it; later queries do an mtime freshness check and re-scan only changed plugins.
- **Cold build can exceed Claude Code's default 60 s MCP timeout** on ~3000+ plugin modlists (~76 s on reference hardware). If a query appears to time out, call `mo2_record_index_status`: `state: "done"` → retry the query; `state: "building"` → wait. Recommend `MCP_TIMEOUT=120000` for routine work on large modlists.
- **Call `mo2_build_record_index` explicitly only** for `force_rebuild=true`, when you want the status dict back, or to eat the cold-build cost up front before many queries.
- **Scan this directory for `CLAUDE_*.md` addon files** beyond this one. Load them — modlist-specific balance philosophy, conventions, and rules that extend these general instructions. No addon files = general mode (fully functional, just less context).

## Tool documentation

Each `mo2_*` tool's schema (visible in the tool registry at session start) is the authoritative documentation — name, description, and input parameters. Read the schema for any tool before bulk usage; it covers when to use the tool, batch parameters, and operational warnings (e.g. CELL/WRLD plugin caveats on `mo2_plugin_conflicts`, batch-read parameters on `mo2_record_detail`). Use `ToolSearch` to fetch a fresh schema if you've been working in a session a while and want to re-confirm a tool's surface.

- **`.claude/skills/`** — task-specific procedures and cross-tool patterns. Auto-load on trigger; don't manually invoke. The `session-strategy` skill covers parallel batching, batch-read patterns, and context management for any MCP-heavy work — load it proactively at session start.
- **User-provided tool prerequisites** (BSArch, PapyrusCompiler + Scripts.zip, nif-tool.exe) are configured per-install at `mo2_mcp/tool_paths.json`. Locations vary by install — check `tool_paths.json` and `KNOWN_ISSUES.md` rather than assuming defaults.
- **Modlist-specific KB files** referenced by an addon's routing table — load the relevant ones before analysis.

## Standing rules

### Investigate before advising
Before any recommendation about records, conflicts, or load order: query actual records via MCP tools, check the conflict chain, base the answer on data. Don't guess from mod names.

### Efficient conflict analysis
Work outward from the mod's own records:
1. `mo2_query_records` filtered to the mod's plugin — get its record list
2. `mo2_conflict_chain` only for records with `override_count > 1`
3. `mo2_record_detail` only where the chain involves plugins that matter

Do NOT call `mo2_plugin_conflicts` on plugins that touch CELL/WRLD heavily — output explodes. Use targeted queries instead.

### External filesystem changes require an MO2 refresh
MO2 doesn't auto-detect external `rm`/`cp`/`mv` of plugin files. After ANY external change (Bash, another tool, manual), ask the user to refresh MO2 (F5) before `mo2_create_patch`, `mo2_build_record_index`, or any read-back against the affected plugin. Skipping this leaves orphans in `loadorder.txt` and new plugins can be missing from the index entirely — symptoms include read-back returning empty even with `include_disabled: true`. Prefer `mo2_write_file` over Bash for plugin-adjacent writes; it routes through MO2's output mod and is detected immediately.

### Don't web-search when the records answer
For conflict analysis, the plugin data IS the documentation. Reserve Nexus research for compatibility gotchas before building a patcher.

### Review before recommending a mod install
If web research or conflict analysis surfaces a candidate mod as a fix:
1. Research the Nexus page (compatibility, known issues).
2. Ask the user to download but not yet install.
3. Examine contents: ESP records via MCP, source on GitHub if SKSE plugin, scripts, meshes.
4. Verify the mod targets the specific failure mode, not just the general problem space.
5. Then recommend.

Ten minutes of review beats an hour of install-and-test.

## Safety

### Never modify install files without permission
No edits to MO2 install / modlist / game-directory files without (1) full discussion and (2) explicit permission. Always prefer overrides — new patch ESPs, new scripts in the output mod — over in-place edits. Overrides are reversible; in-place edits can silently break mod updates and rollback.

Applies to ESP/ESM/ESL, .psc/.pex, INI/JSON configs, SKSE DLLs, meshes, textures, audio, `loadorder.txt`/`plugins.txt`, MO2 profile settings. For ESP binary data and load order files, route through `mo2_create_patch` or MO2 even with permission — never hand-edit.

### Always confirm before
- Writing files to the output mod
- Recommending load order changes affecting many plugins
- Any action that could affect modlist stability

## Building knowledge through use

When you discover something that would help future sessions, offer to save it (always ask first):
- **Modlist-specific rule** → `CLAUDE_[YourList].md` addon file
- **Reusable procedure** → new skill at `.claude/skills/<name>/SKILL.md` with a clear trigger description
- **Topic reference** → `kb/KB_[Topic].md`

At session end, review what was learned and offer to capture anything that would benefit future work.
