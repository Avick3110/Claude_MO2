# KB_Tools.md — Claude MO2 Tool Reference

Load this file at the start of every session that touches modlist files. Category-specific tools (ESP patching, Papyrus, BSA, NIF, audio) live in skills and load when their trigger matches — see the "Category Skills" section at the bottom.

---

## MO2 MCP Plugin

**Server:** `http://localhost:27015/mcp` (start via MO2 Tools menu)
**Transport:** Streamable HTTP (MCP spec 2025-03-26), JSON-RPC 2.0

### Startup Sequence
1. Start MO2
2. Tools > Start/Stop Claude Server
3. Claude Code auto-connects via the `~/.claude.json` entry the plugin writes on first start

### Important: After Code Changes
MO2 caches Python modules in memory AND bytecode on disk. After modifying any `.py` file in the plugin directory:
1. Delete `__pycache__/` in the plugin directory
2. Restart MO2 entirely

Both steps are required. Deleting `__pycache__` alone does nothing — the old modules are still loaded in memory. Server stop/start (via the Tools menu) does NOT reload Python modules; only a full MO2 restart does.

---

## Core Tools

The 17 tools documented below cover modlist queries, VFS access, writes, record indexing, record queries, and conflict analysis — the operations used in nearly every session.

### Mod & Plugin Queries
| Tool | Key Params | Returns |
|------|-----------|---------|
| `mo2_ping` | — | Server version, MO2 version, game, profile |
| `mo2_list_mods` | `filter`, `enabled_only`, `offset`, `limit` | Mod list with priority, enabled state |
| `mo2_mod_info` | `name` (required) | Version, Nexus ID, categories, file count |
| `mo2_list_plugins` | `filter`, `enabled_only`, `offset`, `limit` | Plugin list in load order with master/light flags |
| `mo2_plugin_info` | `name` (required) | Master chain, dependents, providing mod |
| `mo2_find_conflicts` | `mod_name` (required), `limit` | File conflicts: what it overwrites, what overwrites it |

### Virtual File System
| Tool | Key Params | Returns |
|------|-----------|---------|
| `mo2_resolve_path` | `path` (required) | Real disk path, providing mod, conflict losers |
| `mo2_list_files` | `directory` (required), `pattern`, `recursive`, `mod_name` | Files in VFS directory with origins |
| `mo2_read_file` | `path` (required), `encoding`, `max_size_kb`, `offset`, `limit` | Text file content through VFS. Supports line-range reads. |
| `mo2_analyze_dll` | `path` (required), `include_import_details`, `include_strings` | PE metadata, imports, exports, version info, SKSE detection, filtered strings |

### Write
| Tool | Key Params | Returns |
|------|-----------|---------|
| `mo2_write_file` | `path`, `content` (required) | Writes to the designated output mod folder |

### Index Management

**`mo2_record_index_status`** — Check if the index is built.
- Returns: `built`, `plugins`, `unique_records`, `conflicts`, `build_time_s`, `build_status`
- If not built, tells Claude to call `mo2_build_record_index` first

**`mo2_build_record_index`** — Scan all plugins in the active load order.
- Params: `force_rebuild` (bool, default false)
- Runs in background thread. Poll `mo2_record_index_status` for progress.
- Performance: ~18–20s fresh scan, ~6s cached reload for large modlists (~3,000+ plugins)
- Cache: `.record_index.pkl` in the plugin directory (~91 MB for large lists)

### Record Queries

**`mo2_query_records`** — Search records with filters.
- Params: `plugin_name`, `record_type` (ARMO/WEAP/NPC_/etc.), `editor_id_filter` (substring), `formid` (exact, format: `Skyrim.esm:012E49`), `limit` (default 50), `offset`
- Returns: list of `{formid, record_type, editor_id, winning_plugin, override_count}`

**`mo2_record_detail`** — Full field interpretation for one record.
- Params: `formid` or `editor_id` (at least one required), `plugin_name` (optional, default: winner), `plugin_names` (list, batch variant), `resolve_links` (bool, annotates FormIDs with EditorIDs)
- Returns: All subrecord fields with named values, resolved FormIDs, enum labels, flag names
- Routes through Mutagen (via `spooky-bridge.exe`) for engine-correct field interpretation — localized strings resolve, VMAD scripts render, union types use Mutagen's deciders

### Conflict Analysis

**`mo2_conflict_chain`** — All plugins modifying a record, in load order.
- Params: `formid` or `editor_id`
- Returns: ordered chain with winner marked, load order indices

**`mo2_plugin_conflicts`** — All records a plugin overrides from its masters.
- Params: `plugin_name` (required)
- Returns: overrides grouped by record type with counts and sample records
- **Warning:** do NOT use for plugins that touch CELL/WRLD heavily — output can be enormous. Use `mo2_query_records` filtered to the plugin instead. See the `mod-dissection` skill for the targeted conflict-analysis workflow.

**`mo2_conflict_summary`** — High-level conflict overview.
- Params: `record_type` (optional filter)
- Returns: total conflicts, counts by type, top overriding plugins

---

## FormID Format

FormIDs use the format `PluginName:LocalID`:
- `Skyrim.esm:012E49` = ArmorIronCuirass
- `Requiem.esp:AD3A25` = a Requiem keyword
- `NULL` = zero FormID

The local ID is the lower 24 bits of the raw FormID. The plugin name is resolved from the file's master list.

---

## Field Interpretation Output Types

`mo2_record_detail` returns interpreted values:

| Field Type | Output |
|-----------|--------|
| Integers (uint8/16/32, int8/16/32) | Python int |
| Floats | Python float |
| Strings (non-localized) | Python string |
| Strings (localized) | Resolved text from `.STRINGS` / `.DLSTRINGS` / `.ILSTRINGS` |
| FormIDs | `PluginName:LocalID` string (annotated with EditorID if `resolve_links: true`) |
| Enums | Label string (e.g., `"Heavy Armor"`, `"Unaggressive"`) |
| Flags | List of active flag names (e.g., `["Female", "Essential"]`) |
| Structs | Dict of named fields |
| Arrays | List of values |
| Bytes (raw) | Hex string (e.g., `"0200000002000000..."`) |

---

## Category Skills (auto-loaded by Claude Code on task match)

Tools outside the core above live in skills. Claude Code pattern-matches the current task against each skill's description and loads the relevant one(s) on demand.

- **`esp-patching`** — `mo2_create_patch` (override records, merge leveled lists, attach scripts, etc.)
- **`papyrus-compilation`** — `mo2_compile_script`
- **`bsa-archives`** — `mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, `mo2_validate_bsa`
- **`nif-meshes`** — `mo2_nif_info`, `mo2_nif_list_textures`, `mo2_nif_shader_info`
- **`audio-voice`** — `mo2_audio_info`, `mo2_extract_fuz`
- **`mod-dissection`** — full-mod analysis playbook, efficient conflict analysis workflow, CELL/WRLD ITM handling, script health check procedure
- **`leveled-list-patching`** — reasoning framework for LVLI/LVLN/LVSP merges (base plugin selection, intent classification)
- **`crash-diagnostics`** — crash and freeze triage (hard freeze vs CTD)
- **`npc-analysis`** / **`npc-outfit-investigation`** — NPC investigation routers
- **`session-strategy`** — parallel tool use rules, agent delegation, context management

---

## Known Limitations

See `KNOWN_ISSUES.md` in the plugin root for the current list. Highlights:

- `mo2_compile_script` needs Creation Kit (`PapyrusCompiler.exe` + base-Skyrim script sources).
- BSA tools need `BSArch.exe` (user-provided, ships with xEdit).
- NIF extras (`list-textures`, `shader-info`) need `nif-tool.exe` (user-provided).
- Spells carry conditions per effect (MGEF), not at the record level — condition a SPEL via its MGEF.
- Leveled list merge requires caller judgment on `base_plugin` — see the `leveled-list-patching` skill.
- `mo2_record_detail` has a reflection depth cap (default 6) that can truncate extremely deep objects.
