---
description: Create ESP patch plugins via mo2_create_patch — overrides (set_fields, set/clear_flags, add/remove_keywords/spells/perks/factions/inventory/packages/outfit_items/form_list_entries, add/remove_conditions, attach_scripts, set/clear_enchantment, add/remove_leveled_list_entries) and leveled list merging (LVLI/LVLN/LVSP). Use when the user wants to create a patch, override a record, add keywords to armor, modify NPC stats, attach Papyrus scripts to a record, or merge leveled lists across plugins.
---

# ESP Patching

**`mo2_create_patch`** — Write an ESP patch via the Mutagen-backed `mutagen-bridge.exe`. One call per patch file, regardless of operation count.

- Params: `output_name` (required), `operations` (array)
- Each operation is either an `override` (on an existing record) or a `merge_leveled_list` (LVLI/LVLN/LVSP).
- Override modifications supported (use as many as needed per operation):
  - `set_fields` — named field aliases (e.g., `Health`, `Stamina`, `Value`, `Weight`, `ArmorRating`) resolve to Mutagen paths via reflection.
  - `set_flags` / `clear_flags` — general flags or NPC-specific (Essential, Protected, Female, etc.)
  - `add_keywords` / `remove_keywords`
  - `add_spells` / `remove_spells` / `add_perks` / `remove_perks` / `add_packages` / `remove_packages` / `add_factions` / `remove_factions` (faction accepts `{faction, rank}`) / `add_inventory` / `remove_inventory` / `add_outfit_items` / `remove_outfit_items` / `add_form_list_entries` / `remove_form_list_entries`
  - `add_leveled_list_entries` / `remove_leveled_list_entries`
  - `add_conditions` / `remove_conditions` — works on any record with a `Conditions` property (PERK, MGEF, PACK, etc.). Spells carry conditions per effect; use the MGEF. Supports `ConditionFloat` (numeric) and `ConditionGlobal` (via `global: "Plugin:FormID"`).
  - `attach_scripts` — VMAD scripts with typed properties (Object / Int / Float / Bool / String / Alias).
  - `set_enchantment` / `clear_enchantment`
- `merge_leveled_list` params: `base_plugin` (required — the overhaul whose restructuring to keep as-is), `override_plugins` (list of plugins whose unique entries to add). See the `leveled-list-patching` skill for how to pick the base.
- Returns: success, per-operation counters (`keywords_added`, `spells_added`, etc.), output ESP path, master count.

## Field Interpretation via Mutagen

`mo2_record_detail` and ESP patching internals route through `mutagen-bridge.exe` → Mutagen for engine-correct field interpretation.

What this means in practice:
- Localized strings (`.STRINGS` / `.DLSTRINGS` / `.ILSTRINGS`) resolve to actual text, not `[lstring ID]` placeholders.
- VMAD script attachments render with script names and typed properties.
- Union-typed fields use Mutagen's deciders, not our guesses.
- FormIDs can be annotated with EditorIDs on demand (`resolve_links: true` on `mo2_record_detail`).

## Notes

- Spells carry conditions per magic effect, not at the record level. To condition a SPEL, attach the condition to its MGEF instead. The bridge rejects `add_conditions` on SPEL records with "Record type Spell does not support conditions."
- `attach_scripts` supports 6 property types: Object, Int, Float, Bool, String, Alias. The `Alias` type links a quest's named alias (JSON: `{"quest": "Plugin:FormID", "alias_id": int}`).
- Output ESP lands in the configured output mod (default `Claude Output`). Can be ESL-flagged.
- `mo2_record_detail` walks Mutagen object graphs with a default depth limit of 6. Extremely deep QUST/PACK/CELL structures could truncate as `"...[max depth reached]"`. Depth is tunable via `ReadRequest.MaxDepth` in the bridge source.

## Post-write workflow

A successful `mo2_create_patch` call returns with the patch file written to the output mod's folder AND registered in MO2's load order — but the plugin's **right-pane checkbox is OFF by default** (unless MO2 auto-enables it, which can happen). The patch is not loaded in-game until the user ticks that checkbox.

Before returning, `mo2_create_patch` fires MO2's directory refresh and waits for MO2's `onRefreshed` signal to fire (up to 30 s). This means MO2's in-memory plugin list has picked up the new patch by the time the tool returns, so the response carries `refresh_status: "complete"` and `refresh_elapsed_ms` for the caller's logs.

What this means for you:

1. **Always surface the `next_step` field** from the `mo2_create_patch` response to the user. It tells them what the next action is — typically "tick the plugin's checkbox in MO2's right pane when you're ready to load the patch in-game." On a refresh timeout, `next_step` tells them to press F5 in MO2 to force the refresh.
2. **Pre-enable read-back works — chaining is safe.** Immediately after a successful write you can call `mo2_record_detail` / `mo2_query_records` against records in the new patch and they will resolve, even if the checkbox isn't ticked yet. By default the patch lands in the index with `enabled: false` and is filtered out of default query results (which reflect runtime reality), so pass `include_disabled: true` to see it pre-enable. Post-enable (after the user ticks the checkbox), default queries see it normally. No manual `mo2_build_record_index` is needed between the write and the read-back.
3. **Refresh timeouts are rare but possible.** If `refresh_status: "timeout"` comes back, the patch is still on disk and MO2 is still refreshing asynchronously; the next query's freshness check will usually pick it up within a few more seconds. Surfacing the F5 instruction is still the safest fallback.
4. **Don't claim the patch is "active" or "live" in the response.** Until the user ticks the checkbox, it's written but not loaded. A fair summary is "patch written — ready for review; tick the checkbox in MO2 when you want it in-game, and tell me when it's enabled so I can verify."
5. **Fresh patches don't win conflicts until enabled.** The record index and conflict chains filter by plugin enable state. A just-written patch (not yet ticked) will not appear as the winning plugin in default `mo2_conflict_chain` / `mo2_conflict_summary` / `mo2_query_records` output — that reflects runtime reality. Pass `include_disabled: true` to preview how the chain will look post-enable, or wait for the user to confirm the tick.
6. **External file operations still need MO2's F5.** MO2 does not auto-detect `rm` / `cp` / `mv` of plugin files done outside its API. If you (or the user) have recently moved plugin files via Bash or any other tool, ask the user to refresh MO2 (F5) before the next `mo2_create_patch` or read-back — without the manual refresh, orphans can disrupt the write-time refresh and the new plugin may be missing from the index entirely. `mo2_write_file` is detected immediately (routes through MO2's output mod); prefer it over Bash for plugin-adjacent writes.
