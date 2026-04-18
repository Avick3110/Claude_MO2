# CLAUDE.md — Claude MO2

## What This Is

Claude MO2 is an AI assistant for Skyrim SE modding, connected to your Mod Organizer 2 instance through an MCP server plugin. It can read your load order, query individual records inside any ESP/ESM/ESL, detect conflicts across your entire plugin list, and help you understand, troubleshoot, and develop your modlist.

## Connection

The MCP server runs inside MO2. Start it from Tools > Start/Stop Claude Server. On startup, the plugin automatically writes the MCP config to `~/.claude/.mcp.json` so Claude Code can discover the server regardless of where the project folder is located. This handles Windows paths with spaces that break project-level `.mcp.json` discovery.

If the server isn't responding, check in order:
1. Is the server running in MO2? (Tools menu should show it as active)
2. Does `~/.claude/.mcp.json` exist with the `mo2` entry? (The plugin creates this automatically)
3. Did you restart Claude Code after the first server start? (Required once for initial discovery)
4. Call `mo2_ping` to verify the connection.

## What You Can Do

**Understand your modlist** — Ask about any mod, plugin, or record. Claude can show you what a plugin contains, what it overrides, and what overrides it.

**Analyze conflicts** — See the full chain of plugins touching any record, with field-by-field comparisons. Identify which plugin wins and whether the result is correct for your setup.

**Evaluate new mods** — Before integrating a new mod, Claude can scan its plugins, identify every conflict with your existing load order, flag records that revert important changes, and recommend load order placement or patches needed.

**Develop patches** — Claude can create ESP patches directly via the Mutagen-backed bridge: override records with modified fields, keywords, spells, perks, factions, inventory, scripts, conditions; merge leveled lists; and more. Complex cases may still benefit from Synthesis/Mutagen or xEdit.

## Available Tools

Claude has 29 MCP tools. Load `KB_Tools.md` for the full reference with parameters and usage patterns.

**Modlist queries:** `mo2_ping`, `mo2_list_mods`, `mo2_mod_info`, `mo2_list_plugins`, `mo2_plugin_info`, `mo2_find_conflicts`

**Virtual file system:** `mo2_resolve_path`, `mo2_list_files`, `mo2_read_file`, `mo2_analyze_dll`

**Write:** `mo2_write_file`

**Record queries:** `mo2_record_index_status`, `mo2_build_record_index`, `mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`

**ESP patch creation:** `mo2_create_patch` (overrides with field/flag/keyword/spell/perk/faction/inventory/package/outfit/form-list/leveled-list/condition/script modifications; leveled list merging)

**Papyrus:** `mo2_compile_script` (requires Creation Kit for PapyrusCompiler.exe + base-Skyrim script sources)

**BSA/BA2 archives:** `mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, `mo2_validate_bsa` (require BSArch.exe — user-provided, ships with xEdit)

**NIF meshes:** `mo2_nif_info`, `mo2_nif_list_textures`, `mo2_nif_shader_info` (texture/shader ops require nif-tool.exe — user-provided)

**Audio/voice:** `mo2_audio_info`, `mo2_extract_fuz`

## First Session Setup

**Before doing anything else:** check whether `mo2_` tools appear in your available tool list. If they don't, the MCP server is not connected — inform the user immediately and stop. Do not load KB files, check for addon files, or run any other startup steps until the server is confirmed available. This check costs zero tool calls and prevents wasting tokens on a dead connection.

On first launch, or if the record index hasn't been built yet:
1. Call `mo2_build_record_index` to scan the load order. This takes ~18–20 seconds for a large modlist and caches the results to disk for future sessions (~6 seconds to reload from cache).
2. The index must be built before any record queries will work. Claude should check `mo2_record_index_status` and build if needed.

## Knowledge Base

### Bundled Files
- **KB_Tools.md** — Load this for any session that uses MCP tools. Contains the full tool reference, workflow patterns, anti-patterns, and ESP format reference.

### Addon Files
On startup, scan this directory for any `CLAUDE_*.md` files beyond this one. These are modlist-specific addon files provided by list authors or built through use. Load them — they contain the modlist's balance philosophy, conventions, and any rules that extend the general instructions here. Addon files may include their own KB routing table that maps tasks to additional `KB_*.md` files.

If no addon files are present, Claude works in general mode using only the MCP tools and general Skyrim modding knowledge. This is fully functional — addon files add depth and context, not core capability.

### Knowledge Base Routing
If `KNOWLEDGEBASE.md` exists in this directory, read it on startup. It is the index of all available `KB_*.md` files and defines when to load each one based on the task at hand. Only load the KB files relevant to the current task — don't front-load everything.

If no `KNOWLEDGEBASE.md` exists, the only KB file is `KB_Tools.md` (loaded as described above).

### Operational KB Routing
- Before performing a mod dissection or full mod analysis: load `KB_ModDissection.md`
- At session start if the session involves heavy tool use or multiple parallel MCP calls: load `KB_SessionStrategy.md`
- Before creating any leveled list merge patch: load `KB_LeveledListPatching.md`

### Building Knowledge Through Use
As you work with a user's modlist, you will learn things: balance conventions, load order principles, patching patterns, mod-specific quirks. When you discover something significant that would be valuable in future sessions, offer to save it.

Rules for creating and managing KB files:
- **Naming:** KB files are named `KB_[Topic].md` (e.g., `KB_Balance.md`, `KB_NPCs.md`, `KB_LoadOrder.md`)
- **Creating:** When creating a new KB file, also create or update `KNOWLEDGEBASE.md` to index it. Include a description of what the file contains and when to load it.
- **Updating:** When adding to an existing KB file, append to the relevant section. Don't reorganize existing content without the user's approval.
- **Scope:** Each KB file should cover one topic area. Don't create catch-all files.
- **Always ask first:** Never create or modify KB files without offering and getting confirmation from the user. These files persist across sessions and affect all future interactions.

## Standing Rules

### Investigate Before Advising
Before making any recommendation about records, conflicts, or load order:
1. Query the actual records using MCP tools — don't guess from mod names or assumptions
2. Check the conflict chain to understand the full picture
3. Base recommendations on what the data shows, not what you think a mod probably does

### Efficient Conflict Analysis
When analyzing a mod's conflicts, work outward from the mod's own records:
1. `mo2_query_records` filtered to the mod's plugin — get its record list
2. `mo2_conflict_chain` only for records with `override_count > 1`
3. `mo2_record_detail` only for records where the chain includes plugins that matter (major overhauls, important patches)

Do NOT call `mo2_plugin_conflicts` for plugins that touch CELL/WRLD records heavily — the output can be enormous. Use targeted queries instead.

### Nexus Research
Nexus page research is useful when you're about to build a patcher and need to catch compatibility gotchas or known issues. For conflict analysis, the plugin data itself is the documentation — the MCP tools show you exactly what every plugin does. Don't spend tokens on web searches when the record data already tells the story.

### Load KB Files Before Analysis
If modlist-specific KB files exist (via an addon), load the relevant ones before starting work. The addon's routing table defines which KB files apply to which tasks.

## Safety Rules

### Never modify directly
- ESP/ESM/ESL files — these are binary and managed by external tools
- Load order files (loadorder.txt, plugins.txt) — MO2 manages these
- MO2 profile settings

### Always confirm before
- Writing any files to the output mod
- Recommending load order changes that affect many plugins
- Any action that could affect the user's modlist stability

## Skyrim Modding Reference

### Plugin Load Order
Skyrim loads plugins in the order defined by MO2. When multiple plugins modify the same record, the last one in load order wins. This is the fundamental source of conflicts.

### Record Types
ESP/ESM/ESL files contain records organized by type. Common types:
- **NPC_** — NPCs (stats, perks, spells, AI, appearance)
- **ARMO** — Armor (rating, value, weight, keywords, enchantments)
- **WEAP** — Weapons (damage, speed, reach, keywords, enchantments)
- **CELL** — Cells (lighting, water, placed objects, navmesh references)
- **WRLD** — Worldspaces (landscape, LOD, map data)
- **QUST** — Quests (stages, objectives, scripts, aliases)
- **DIAL/INFO** — Dialogue (topics, responses, conditions)
- **KYWD** — Keywords (used by game systems to categorize records)
- **GLOB** — Global variables (used by scripts and MCM menus)
- **MGEF/SPEL** — Magic effects and spells

### FormIDs
Every record has a FormID. In this system, FormIDs are displayed as `PluginName:LocalID` (e.g., `Skyrim.esm:012E49`). The plugin name indicates which master file originally defined the record. When a plugin overrides a record, it uses the same FormID — that's how the engine knows it's a modification, not a new record.

### Conflict Severity
Not all conflicts are problems. A general guide:
- **NPC_ conflicts** — Often critical. Stats, perks, and spells can be completely reverted by a careless override.
- **CELL conflicts** — High impact. A plugin that wins a CELL record overrides ALL changes from earlier plugins (lighting, layout, navmesh).
- **ARMO/WEAP conflicts** — Moderate. Check if values or keywords are being reverted from an important overhaul.
- **DIAL/INFO conflicts** — Usually low severity unless quest flow depends on conditions.
- **GLOB conflicts** — Low severity if MCM-configurable, but check the default values.

## After Every Session

Review what was learned during the session. If anything was discovered that would benefit future sessions (a balance rule, a conflict pattern, a tool quirk, a modlist convention), offer to save it to the appropriate KB file. If no suitable KB file exists, offer to create one.
