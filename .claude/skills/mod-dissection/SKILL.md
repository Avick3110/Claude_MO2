---
description: Playbook for analyzing an unfamiliar mod — optimal tool call order (overview, record inventory, quest structure, scripts/assets, conflict analysis, external docs), Papyrus script reading strategy using offset/limit, script health check procedure for persistent-load detection (RegisterForUpdate, OnUpdate, Ability + AME scripts), efficient conflict analysis workflow (query-outward rather than bulk dumps), CELL/WRLD ITM handling, and context budget framework (40/60% thresholds). Use before performing any full-mod analysis, evaluating a new mod, dissecting a mod's behavior, auditing what a mod actually does, analyzing script performance, or assessing conflicts for a specific plugin.
---

# Mod Analysis Playbook

## Optimal Tool Call Order

### Step 1: Overview (parallel batch)
Call all three in one batch — they're independent:
- `mo2_mod_info` — mod metadata, Nexus ID, file count
- `mo2_plugin_info` — master chain, load order, flags, description
- `mo2_find_conflicts` — file-level conflicts (takes mod folder name, not plugin filename)

### Step 2: Record Inventory
- `mo2_query_records` filtered to the mod's plugin — get the full record list in one call
- Note record types and counts. Focus on the types that define the mod's behavior (QUST, NPC_, ARMO, WEAP, MGEF, SPEL, KYWD, GLOB).

### Step 3: Quest Structure
- `mo2_record_detail` on QUST records — quests define a mod's operational structure (scripts, aliases, stages, objectives)
- Quest records reveal which scripts are attached and what they manage. This tells you what the mod actually does before reading any source code.

### Step 4: Scripts and Assets
- `mo2_list_files` to inventory scripts and assets (use `mod_name` filter)
- `mo2_read_file` with `offset`/`limit` targeting function signatures and key logic blocks — NOT full scripts
- See "Script Reading Strategy" below for how to read efficiently

### Step 5: Conflict Analysis
- `mo2_conflict_chain` for records with `override_count > 1` (from step 2)
- `mo2_conflict_summary` for a high-level view if the mod touches many record types
- `mo2_record_detail` to compare specific field values between conflicting plugins
- See "Efficient Conflict Analysis Workflow" below for the fast-and-targeted procedure

### Step 6: External Documentation (only if needed)
- Design docs, Nexus pages, feature trackers — only if steps 1-5 leave specific gaps
- The plugin data itself is the documentation. Don't spend tokens on web searches when record data already tells the story.

---

## Script Reading Strategy

Large Papyrus scripts (1,000+ lines) waste massive context when read in full. Use targeted reads.

### First Pass: Shape of the Script
Read the first ~50 lines (`offset=0, limit=50`):
- `ScriptName ... extends` clause — what does it extend?
- Property declarations — what objects does it reference?
- Top-level comments — any architecture notes?

This alone tells you the script's role and dependencies.

### Second Pass: Function Index
Search for `Function` and `Event` signatures to build an index of what the script does. Use `mo2_read_file` with offset/limit to scan sections, or grep the file if available on disk.

Key patterns to look for:
- `Event OnInit()` — startup behavior
- `Event OnUpdate()` — periodic tick logic
- `Function` signatures — the mod's API surface
- `Event OnStoryScript()` / quest stage events — quest-driven logic

### Third Pass: Targeted Reads
Read only the functions relevant to your analysis goal. Use offset/limit to grab specific blocks. If a function is 10 lines, read 15 (for context).

### Bulk Script Reads
If 10+ scripts need full inspection, delegate to a background agent. Give the agent:
- A complete list of file paths to read
- A specific question to answer about each script
- Instructions to return a summary, not raw content

Never duplicate reads between main context and the agent. If you delegate scripts, don't also read them yourself.

---

## Script Health Check Workflow

When analyzing a mod's scripts for performance issues (persistent load, polling, heavy patterns), follow this streamlined procedure.

### Prerequisites
- MO2 MCP server must be running
- The record index builds itself on the first query that needs it (lazy auto-build, ~8 s cache-hit on a ~3000-plugin modlist, ~76 s cold). You don't need to pre-build it; just proceed with Step 1 and the first query will trigger the build. On cold rebuilds from scratch, consider `MCP_TIMEOUT=120000` in your environment so the client-side call doesn't time out.

### Step 1: List the mod's scripts (1 call)

```bash
find "<mod_path>/scripts/source" -name "*.psc" | sort
```

If scripts are packed in a BSA and no source is available, extract with `mo2_extract_bsa` using a `*.psc` filter (see the `bsa-archives` skill).

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

## Context Budget Guidelines

A single mod dissection should fit in **~40% of a context window**.

| Threshold | Action |
|-----------|--------|
| Under 40% | Normal analysis — expand as needed |
| 40-60% | Wrap up analysis, summarize findings, don't start new reads |
| Over 60% | Stop expanding immediately. Summarize what you have. |

### What Costs the Most Context
1. Full script reads of large files (1,000+ lines = ~30KB+ of content)
2. Record detail dumps for many records
3. External file content (docx, PDF conversions)

### How to Stay Under Budget
- Use offset/limit on `mo2_read_file` — never read a full 3,000-line script
- Query records by type, not all at once
- Summarize each phase's findings before starting the next
- External file conversion should be a standalone step, not interleaved with MCP analysis

---

## What to Skip

These waste context without proportional value in a typical mod analysis:

- **Google Sheets / feature trackers** — unless specifically auditing planned-vs-implemented features
- **Design docs** — unless the scripts leave unanswered questions about intent
- **Full script reads** — when function signatures tell the story
- **CELL/WRLD record details** — unless the mod specifically modifies world layout (these records are huge)
- **Nexus page research** — the plugin data shows what the mod does; save Nexus for compatibility gotchas when building a patcher
