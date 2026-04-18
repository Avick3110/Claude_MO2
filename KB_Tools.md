# KB_Tools.md — Claude MO2 Tool Reference

Load this file at the start of every session that touches modlist files.

---

## MO2 MCP Plugin

**Server:** `http://localhost:27015/mcp` (start via MO2 Tools menu)
**Transport:** Streamable HTTP (MCP spec 2025-03-26), JSON-RPC 2.0

### Startup Sequence
1. Start MO2
2. Tools > Start/Stop Claude Server
3. Claude Code auto-connects via `.mcp.json`

### Important: After Code Changes
MO2 caches Python modules in memory AND bytecode on disk. After modifying any `.py` file in the plugin directory:
1. Delete `__pycache__/` in the plugin directory
2. Restart MO2 entirely

Both steps are required. Deleting `__pycache__` alone does nothing — the old modules are still loaded in memory. Server stop/start (via the Tools menu) does NOT reload Python modules; only a full MO2 restart does.

---

## Tools (29 total)

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
| `mo2_list_files` | `directory` (required), `pattern`, `recursive` | Files in VFS directory with origins |
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
- Params: `formid` or `editor_id` (at least one required), `plugin_name` (optional, default: winner)
- Returns: All subrecord fields with named values, resolved FormIDs, enum labels, flag names
- Uses direct file offset seek — ~0.3-0.5ms per record

### Conflict Analysis

**`mo2_conflict_chain`** — All plugins modifying a record, in load order.
- Params: `formid` or `editor_id`
- Returns: ordered chain with winner marked, load order indices

**`mo2_plugin_conflicts`** — All records a plugin overrides from its masters.
- Params: `plugin_name` (required)
- Returns: overrides grouped by record type with counts and sample records

**`mo2_conflict_summary`** — High-level conflict overview.
- Params: `record_type` (optional filter)
- Returns: total conflicts, counts by type, top overriding plugins

### ESP Patch Creation

**`mo2_create_patch`** — Write an ESP patch via the Mutagen-backed `spooky-bridge.exe`. One call per patch file, regardless of operation count.
- Params: `output_name` (required), `operations` (array)
- Each operation is either an `override` (on an existing record) or a `merge_leveled_list` (LVLI/LVLN/LVSP).
- Override modifications supported (use as many as needed per operation):
  - `set_fields` — named field aliases (e.g., `Health`, `Stamina`, `Value`, `Weight`, `ArmorRating`) resolve to Mutagen paths via reflection.
  - `set_flags` / `clear_flags` — general flags or NPC-specific (Essential, Protected, Female, etc.)
  - `add_keywords` / `remove_keywords`
  - `add_spells` / `add_perks` / `add_packages` / `add_factions` (faction accepts `{faction, rank}`) / `add_inventory` / `add_outfit_items` / `add_form_list_entries`
  - `add_leveled_list_entries` / `remove_leveled_list_entries`
  - `add_conditions` / `remove_conditions` — works on any record with a `Conditions` property (PERK, MGEF, PACK, etc.). Spells carry conditions per effect; use the MGEF. Supports `ConditionFloat` (numeric) and `ConditionGlobal` (via `global: "Plugin:FormID"`).
  - `attach_scripts` — VMAD scripts with typed properties (Object / Int / Float / Bool / String / Alias).
  - `set_enchantment` / `clear_enchantment`
- `merge_leveled_list` params: `base_plugin` (required — the overhaul whose restructuring to keep as-is), `overrides` (list of plugins whose unique entries to add). See `KB_LeveledListPatching.md` for how to pick the base.
- Returns: success, per-operation counters (`keywords_added`, `spells_added`, etc.), output ESP path, master count.

### Papyrus

**`mo2_compile_script`** — Compile a user-provided `.psc` into `.pex` (requires Creation Kit for `PapyrusCompiler.exe` + base-Skyrim script sources).
- Params: `script_name`, `source` (the .psc text)
- Output lands in `Claude Output/Scripts/<name>.pex`

### BSA / BA2 archives (require BSArch.exe)

**`mo2_list_bsa`** — List contents of an archive. Params: `archive_path`, `filter` (glob, optional), `limit`.
**`mo2_extract_bsa`** — Extract files matching a filter. Params: `archive_path`, `filter` (required — guards against accidental full-archive dumps), `output_subdir`.
**`mo2_extract_bsa_file`** — Pull one specific file. Params: `archive_path`, `file_in_archive`.
**`mo2_validate_bsa`** — Integrity report. Params: `archive_path`.

### NIF meshes

**`mo2_nif_info`** — Format version, file size, header string. Library-native, works without external tools.
**`mo2_nif_list_textures`** — Texture paths referenced by the mesh (requires `nif-tool.exe`).
**`mo2_nif_shader_info`** — Shader flags per `BSLightingShaderProperty` block (requires `nif-tool.exe`).

### Audio / voice

**`mo2_audio_info`** — Format metadata for `.fuz` / `.xwm` / `.wav`. FUZ parsing is handled in-bridge (Spooky's upstream parser rejects valid FUZes — known upstream bug).
**`mo2_extract_fuz`** — Split a `.fuz` into its `.xwm` audio + `.lip` sync files.

---

## FormID Format

Throughout the tools, FormIDs use the format `PluginName:LocalID`:
- `Skyrim.esm:012E49` = ArmorIronCuirass
- `Requiem.esp:AD3A25` = a Requiem keyword
- `NULL` = zero FormID

The local ID is the lower 24 bits of the raw FormID. The plugin name is resolved from the file's master list.

---

## Field Interpretation

The record detail tool returns interpreted values:

| Field Type | Output |
|-----------|--------|
| Integers (uint8/16/32, int8/16/32) | Python int |
| Floats | Python float |
| Strings (non-localized) | Python string |
| Strings (localized) | `[lstring ID]` placeholder |
| FormIDs | `PluginName:LocalID` string |
| Enums | Label string (e.g., `"Heavy Armor"`, `"Unaggressive"`) |
| Flags | List of active flag names (e.g., `["Female", "Essential"]`) |
| Structs | Dict of named fields |
| Arrays | List of values |
| Bytes (raw) | Hex string (e.g., `"0a 1b 2c"`) |

---

## ESP Binary Format Quick Reference

```
ESP File
  TES4 Record (24B header + subrecords)
    HEDR (version, record count, next object ID)
    MAST (master file list, one per master)
    CNAM (author), SNAM (description)
  GRUP (24B header, type 0 = top-level)
    Record (24B header + subrecords)
      EDID (Editor ID), then type-specific subrecords
    GRUP (nested: types 1-10 for cells, worldspaces, etc.)
```

- Record header: type(4) + dataSize(4) + flags(4) + FormID(4) + revision(4) + version(2) + unk(2) = 24 bytes
- GRUP header: "GRUP"(4) + groupSize(4) + label(4) + groupType(4) + stamp(2) + unk(2) + ver(2) + unk(2) = 24 bytes
- Subrecord: type(4) + size(2) + data(size) = 6 + size bytes
- Compressed records: flag `0x00040000`, data starts with uint32 decompressed size then zlib stream

---

## Field Interpretation via Mutagen

As of v2.0.0, `mo2_record_detail` routes through `spooky-bridge.exe` → Mutagen for engine-correct field interpretation. The old Python schema walker (`esp_fields.py` + `esp_schema.py` + `schema/SSE.json`) was retired.

What this means in practice:
- Localized strings (`.STRINGS` / `.DLSTRINGS` / `.ILSTRINGS`) resolve to actual text, not `[lstring ID]` placeholders.
- VMAD script attachments render with script names and typed properties.
- Union-typed fields use Mutagen's deciders, not our guesses.
- FormIDs can be annotated with EditorIDs on demand (`resolve_links: true`).

See `Field Interpretation` above for the output types you can expect.

---

## Efficient Conflict Analysis Workflow

When reviewing a new mod for conflicts with your modlist's overhaul, **query outward from the mod's own records** rather than pulling bulk conflict dumps.

### The Right Way (fast, targeted)

1. **`mo2_query_records`** with `plugin_name` set to the mod — gets the mod's full record list with types, EditorIDs, and `override_count` for each.
2. **Filter to overrides only** — skip any record with `override_count == 0` (new records unique to the mod, no conflicts possible).
3. **`mo2_conflict_chain`** only for records with `override_count > 1` — shows who else edits each record and who wins.
4. **`mo2_record_detail`** only for records where the conflict chain includes plugins that matter (major overhauls, important patches). Compare field values across the chain to understand what's being changed.

### Anti-Patterns (avoid these)

- **Do NOT call `mo2_plugin_conflicts`** for plugins that touch CELL or WRLD records — the output can be enormous (hundreds of thousands of characters). Use `mo2_query_records` filtered to the plugin instead.
- **Do NOT pull `mo2_record_detail` speculatively** — only fetch full field data for records you've already identified as conflicting with important plugins.
- **Do NOT launch web search agents for Nexus research** during conflict analysis — the plugin data already tells you what the mod does. Save Nexus research for when you're about to build a patcher and need compatibility notes.

### Decision Checklist Per Record Type

When you find a conflict, assess it based on the record type:

- **NPC_** — Check stats, perks, spells, class. Is the overhaul's version being reverted? If yes, patch required.
- **ARMO/WEAP** — Check values, keywords, enchantments. Are overhaul stats being reverted? If yes, patch required.
- **QUST/DIAL/INFO** — Check quest stages, conditions, script attachments. Usually safe unless quest flow is broken.
- **GLOB** — Check if MCM-configurable. If yes, usually low priority — note the default values and move on.
- **CELL/WRLD** — See the dedicated section below.

---

## CELL and Worldspace Conflict Handling

This is the most common source of false alarms in conflict analysis. Understand this well.

### How CELL/WRLD Overrides Work

When a plugin adds or modifies ANY placed reference inside a cell (an object, NPC, light, navmesh connection, etc.), the entire CELL record shows as an override. This is how the engine works — there is no way around it. A mod that simply places a barrel in Dragonsreach will show as overriding the entire Dragonsreach CELL record, including all the lighting, water, and layout data.

### ITM Overrides vs Genuine Edits

Most CELL/WRLD overrides from quest and gameplay mods are **Identical To Master (ITM)** for the cell-level data. The mod copied the CELL record to add its references, but didn't change the cell's own properties (lighting, water height, image space, etc.). These ITMs become a problem only when they load AFTER a plugin that makes real edits to those same cell properties.

**Example:** Lux makes genuine lighting edits to Dragonsreach. A quest mod places a new NPC in Dragonsreach and carries an ITM copy of the cell data. If the quest mod loads after Lux, its ITM reverts Lux's lighting to vanilla. The fix is always load order — move the quest mod before Lux.

### What To Do

1. **When you see CELL/WRLD overrides, check whether the mod makes genuine edits to the cell properties** (lighting, image space, water, encounter zone) or is just carrying ITM data from adding placed references.
2. **If the override is an ITM** — the fix is load order placement. Move the mod before the plugin whose edits are being reverted (Lux, JK's, etc.). Flag this as a load order recommendation, NOT a patch requirement. Severity: LOW.
3. **If the mod makes genuine edits to cell properties** that conflict with another mod's genuine edits (e.g., two mods both changing Dragonsreach lighting for different reasons) — this is a real conflict that may need a patch to merge the changes. Severity: HIGH.
4. **For the persistent worldspace cell** (Tamriel, `Skyrim.esm:000D74`) — this cell is overridden by almost every mod that places anything in the open world. Conflict chains of 400+ plugins are normal and expected. Do NOT flag this as a problem. Load order handles it.

### How To Tell the Difference

Use `mo2_record_detail` on the CELL record from the mod in question, then compare to the version from the mod being overridden. If the cell-level fields (XCLL lighting, XCLW water height, XCIM image space, XEZN encounter zone) are identical, it's an ITM. If they differ, it's a genuine edit.

---

## When to Research a Mod's Nexus Page

**Skip Nexus research** for conflict analysis and mod reviews. The plugin data from MCP tools is the ground truth — record types, EditorIDs, conflict chains, and field comparisons tell you exactly what's happening. Script source files (.psc) can be read directly to understand behavior and MCM options.

**Do Nexus research** only when:
- You're about to **build a patcher or patch ESP** for a mod — check for known issues, existing patches, and compatibility notes before writing code
- The mod has **complex runtime behavior** that isn't evident from records or scripts alone (e.g., DLL-based mods, engine-level hooks)
- The user specifically asks for Nexus page information

---

## Script Health Check Workflow

When analyzing a mod's scripts for performance issues (persistent load, polling, heavy patterns), follow this streamlined procedure.

### Prerequisites
- MO2 MCP server must be running
- If the record index isn't built, start `mo2_build_record_index` immediately. With a warm cache (~6s) this completes during Step 1-2. A cold first-ever scan takes ~18–20s. Don't look for workarounds while it builds; just proceed with script file analysis.

### Step 1: List the mod's scripts (1 call)

```bash
find "<mod_path>/scripts/source" -name "*.psc" | sort
```

If scripts are packed in a BSA and no source is available, ask the user to extract the BSA. There is no BSA extraction tool built in.

### Step 2: Batch grep for red-flag patterns (1 call)

Search ALL .psc files in the mod for these patterns in a single grep:

```
RegisterForUpdate[^(]|OnUpdate|Extends ActiveMagicEffect|Extends Quest|OnInit
```

This catches:
- **`RegisterForUpdate`** (not Single) — persistent polling loops, the #1 script load cause
- **`OnUpdate`** — where polling logic lives
- **`OnInit`** — initialization that may start polling
- **`Extends ActiveMagicEffect`** — scripts that persist as long as a spell is active
- **`Extends Quest`** — scripts that persist as long as a quest runs

`RegisterForSingleUpdate` is fine — it fires once and stops. Only flag `RegisterForUpdate` (recurring).

### Step 3: Read only the flagged scripts

Only read scripts that matched Step 2. Prioritize:
1. Scripts with `RegisterForUpdate` (recurring) — these are the primary suspects
2. Scripts extending `ActiveMagicEffect` — check if the parent spell is permanent (Ability)
3. Quest scripts with `OnInit` — check what they start

For each flagged script, check:
- Does it poll? (`RegisterForUpdate` with a recurring re-register in `OnUpdate`)
- Does it iterate large arrays in `OnUpdate`?
- Does it register for global events (`OnCombatStateChanged`, `OnHit`, etc.) across many aliases?

### Step 4: Query quest records via MCP (index must be built)

```
mo2_query_records(plugin_name="ModName.esp", record_type="QUST")
```

Then for each quest, pull `mo2_record_detail` and check:
- **DNAM Flags** — look for "Start Game Enabled" (always running)
- **ANAM** — Next Alias ID, tells you roughly how many aliases exist
- **ALST** — count the entries for exact alias count

VMAD in quest records now parses script names and properties correctly. However, QUST-specific fragment data beyond the scripts section may still be incomplete. For detailed script logic, .psc source files remain the best source.

### Step 5: If a spell is involved, check its record

If a script extends `ActiveMagicEffect`, find the parent spell FormID and query it:

```
mo2_record_detail(formid="PluginName.esp:XXXXXX")
```

Check the SPIT subrecord:
- **Type**: "Ability" = permanent (persists forever). "Spell" = temporary.
- **Cast Type**: "Constant Effect" = always active. "Fire and Forget" = one-shot.
- **Duration**: 0 on an Ability = infinite. Any positive value = temporary.

A permanent Ability with a heavy script is a real problem. A Fire and Forget spell with duration 2 is not.

### What Constitutes "Fixable" Script Load

| Pattern | Fixable? | How |
|---------|----------|-----|
| `RegisterForUpdate` polling loop | Yes | Replace with `RegisterForSingleUpdate` or event-driven design |
| Permanent Ability with heavy AME script | Yes | Change spell to Fire and Forget with short duration, or move logic to a quest event |
| Always-running quest with many aliases | No (without mod restructuring) | Inherent to mods that manage many NPCs |
| Global event listeners on many aliases | No (without mod restructuring) | Inherent to alias-based NPC management |
| Many ObjectReference scripts on placed objects | No | Inherent to interactive city overhauls |

---

## External tools referenced by this plugin

These tools are invoked by MCP tools via subprocess or bundled CLIs — listed here for awareness:

- **spooky-bridge.exe** (bundled, our build) — .NET 8 CLI that references Mutagen + Spooky's ESP library. Handles `mo2_create_patch` and `mo2_record_detail`. Also contains our local FUZ parser for `mo2_audio_info` / `mo2_extract_fuz`.
- **Spooky CLI** (bundled) — `spookys-automod.exe`. Handles Papyrus, Archive, NIF, Audio ops via subprocess with `--json` output.
- **BSArch.exe** (user-provided) — required by BSA tools. Obtain from xEdit release.
- **nif-tool.exe** (user-provided) — required by `mo2_nif_list_textures` / `mo2_nif_shader_info`. `mo2_nif_info` works without it.
- **PapyrusCompiler.exe** (user-provided) — required by `mo2_compile_script`. Part of the Creation Kit.

---

## Known Limitations

See `KNOWN_ISSUES.md` in the plugin root for the current list. Highlights:

- `mo2_compile_script` needs base-Skyrim script sources (ships with Creation Kit).
- BSA tools need `BSArch.exe` (xEdit release).
- NIF extras (`list-textures`, `shader-info`) need `nif-tool.exe`.
- Spells carry conditions per effect (MGEF), not at the record level — condition SPEL via its MGEF.
- Leveled list merge requires caller judgment on `base_plugin` — see `KB_LeveledListPatching.md`.
- `mo2_record_detail` has a reflection depth cap (default 6) that can truncate extremely deep objects.
