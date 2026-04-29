---
description: Operational rules for any session that uses the Claude_MO2 MCP tools (mo2_*). Use this whenever the user mentions modlists, mods, plugins, conflicts, ESP patches, NPCs, leveled lists, BSAs, NIF meshes, FUZ audio, Papyrus scripts, or record investigations — even if you think you only need a few calls or can answer directly. Covers parallel batching (never mix Bash + MCP in one batch), batch-read patterns on mo2_record_detail (formids/fields/expand_links — prefer over multiple parallel calls for >2 records), agent delegation for bulk work, and context management. Many tasks that look simple expand into bulk work — load this before scoping the investigation, not after sequential calls add up.
---

# MCP Session Operational Rules

## Parallel Execution Rules

### Safe: MCP-to-MCP parallelism
Multiple MCP tool calls in the same batch are safe and encouraged. Examples:
- `mo2_mod_info` + `mo2_plugin_info` + `mo2_find_conflicts` (mod overview)
- `mo2_record_detail` on multiple records simultaneously
- `mo2_query_records` + `mo2_list_files` (independent queries)

### Unsafe: Bash + MCP in the same batch
Never mix Bash tool calls with MCP tool calls in the same parallel batch. A Bash failure (timeout, error) cascades and cancels all sibling MCP calls in the same batch, wasting those results.

**Do this:**
```
Batch 1: [mo2_mod_info, mo2_plugin_info]  ← MCP only
Batch 2: [pip install something]           ← Bash only
```

**Not this:**
```
Batch 1: [mo2_mod_info, pip install something]  ← mixed, Bash failure kills MCP result
```

### Sequential when dependent
If one call's output feeds another's input (e.g., getting a FormID then querying its detail), run them sequentially. Don't guess parameter values.

---

## Agent Delegation Rules

### When to delegate
- Bulk reads: 10+ scripts, large file sets, or repetitive queries
- Work that would consume >30% of context if done inline
- Independent research tasks that don't need conversation context

### How to delegate
Give the agent a **complete, self-contained task**:
- Full list of file paths or record IDs to process
- A specific question to answer or information to extract
- Output format (summary, table, list of findings)
- Don't assume the agent has conversation context — it doesn't

### Never duplicate work
If you delegate script reading to an agent, do NOT also read those scripts yourself. Pick one path:
- **You read:** Small number of files, need immediate results for follow-up questions
- **Agent reads:** Large batch, results can be summarized independently

---

## Context Management

### Phase your work
If a session involves both MCP tool use and external file processing (docx, PDF, spreadsheets), structure as separate phases:

1. **External processing phase** — convert files, extract content, note key findings
2. **Summarize** — write down what you learned before moving on
3. **MCP analysis phase** — use tools with the external context already internalized

Interleaving external processing with MCP analysis wastes context on format conversion overhead mixed with tool results.

### Priority order when approaching limits
When context is getting tight, prioritize keeping:
1. **Record data** — actual game data, conflict chains, field values
2. **Script analysis** — function signatures, key logic findings
3. **External doc content** — design docs, feature lists, Nexus notes

Drop external doc content first. Record data is the ground truth.

### Summarize at milestones
After completing each analysis phase, write a brief summary of findings before starting the next phase. This creates checkpoints that survive context compression and prevent re-reading.

---

## Tool-Specific Session Notes

### Record index
- The index builds lazily on the first read query. No preflight `mo2_build_record_index` call is required.
- Cache-hit reload is ~8 s on a ~3000-plugin modlist; cold force-rebuild (`force_rebuild=true`) is ~76 s on the same list. Force-rebuilds can exceed Claude Code's default 60 s MCP timeout — set `MCP_TIMEOUT=120000` before launching Claude Code to avoid it, or recover via `mo2_record_index_status` if the client-side call appears to time out (server-side build completes regardless).
- After writes via `mo2_create_patch`, read-back queries just work — the tool waits on MO2's `onRefreshed` signal before returning, so chaining a read in the same turn is safe.

### Large plugin conflicts
- Do NOT call `mo2_plugin_conflicts` for plugins that touch CELL/WRLD records heavily — the output can be enormous.
- Use targeted `mo2_query_records` + `mo2_conflict_chain` instead.

### File listing at root
- `mo2_list_files` with directory `"."` lists root-level VFS files. Always use `mod_name` filter when listing root to avoid scanning the entire VFS.
- For recursive file inventories, query specific subdirectories rather than root.

### Editor ID searches
- `editor_id_filter` on `mo2_query_records` can timeout for common substrings. Use direct FormID lookups when possible.
- If you need to search by editor ID, use specific prefixes (e.g., `"ST_"` for Sanguine's Trade scripts) rather than broad terms.

### Batch reads (v2.9.2+)
`mo2_record_detail` accepts three composable optional parameters that collapse read-heavy workflows:
- **`formids: [list of FormIDs]`** — read N records in one bridge subprocess invocation. Amortizes the ~889 ms subprocess startup across the batch (per-record marginal ~19 ms at N=200). For multi-record reads (>2 records), prefer this over N parallel `mo2_record_detail` calls — each parallel call pays its own startup; one batched call pays once.
- **`fields: [list of dot-paths]`** — projection. Walker auto-traverses lists and dicts mid-path (e.g. `Voices.Male` reads as the male side of the gendered struct; `Factions.Faction` reads as the Faction sub-property of each Factions entry). Shrinks payload ~80% on big records (RACE full-detail 8.7 kB → ~1.7 kB on a 3–5 path subset).
- **`expand_links: [list of FormLink-typed paths]`** — inline single-level FormLink expansion at named positions. Wrapper shape `{formid, EditorID, expanded: {...}}`. Eliminates second-tier FormLink-chase round-trips (5.11× speedup on a 3-spell race; scales with link-count).

All three composable on a single call and orthogonal to `resolve_links: true` (which annotates FormID strings recursively, including inside expanded records). Defaults (all three absent) preserve v2.9.1 single-record / full-payload / no-expansion behavior bit-identically.

For "what does this record look like across plugins" (multi-plugin diff), `formids × plugin_names` returns the cross-product (each formid × each plugin = one cell with own per-cell envelope) — useful for building consistency patches across large modlists. Tested cliff-free up to N×M=1000.
