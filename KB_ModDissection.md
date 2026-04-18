# KB_ModDissection.md — Mod Analysis Playbook

Load this before performing a full analysis of an unfamiliar mod. Defines the optimal tool order, script reading strategy, and context budget rules.

---

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
