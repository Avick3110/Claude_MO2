---
description: Create ESP patch plugins via mo2_create_patch — overrides (set_fields, set/clear_flags, add/remove_keywords/spells/perks/factions/inventory/packages/outfit_items/form_list_entries, add/remove_conditions, attach_scripts, set/clear_enchantment, add/remove_leveled_list_entries) and leveled list merging (LVLI/LVLN/LVSP). Use when the user wants to create a patch, override a record, add keywords to armor, modify NPC stats, attach Papyrus scripts to a record, or merge leveled lists across plugins.
---

# ESP Patching

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
- `merge_leveled_list` params: `base_plugin` (required — the overhaul whose restructuring to keep as-is), `overrides` (list of plugins whose unique entries to add). See the `leveled-list-patching` skill for how to pick the base.
- Returns: success, per-operation counters (`keywords_added`, `spells_added`, etc.), output ESP path, master count.

## Field Interpretation via Mutagen

As of v2.0.0, `mo2_record_detail` and ESP patching internals route through `spooky-bridge.exe` → Mutagen for engine-correct field interpretation.

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
