# KB_LeveledListPatching.md — Leveled List Conflict Resolution

Load this before creating any leveled list merge patch. Leveled list conflicts are among the most complex in Skyrim modding and require careful reasoning — the merge tool is a mechanism, not a decision-maker.

---

## How Leveled Lists Work

A leveled list (LVLI, LVLN, LVSP) is a table of entries. Each entry has:
- **Reference** — a FormID pointing to an item (ARMO, WEAP, ALCH, etc.) OR another leveled list
- **Level** — minimum player level for this entry to be eligible
- **Count** — how many to give

When the game rolls on a leveled list, it picks one eligible entry based on the player's level. If that entry points to another leveled list, it rolls recursively.

### Nesting

Leveled lists are trees, not flat tables. A top-level list like `LootBanditWeapon100` may contain only sub-list references:

```
LootBanditWeapon100 (LVLI)
  ├── LI_Loot_Weapon_BattleAxe (LVLI)
  │     ├── IronBattleAxe (WEAP)
  │     ├── SteelBattleAxe (WEAP)
  │     └── ...
  ├── LI_Loot_Weapon_Bow (LVLI)
  ├── LI_Loot_Weapon_Dagger (LVLI)
  └── ...
```

Each level in the tree is a separate record with its own FormID. Conflicts at one level are independent of conflicts at another — merge each conflicted list on its own.

### Weighting via Duplicates

Skyrim has no probability weights on leveled list entries. Modders control drop probability by duplicating entries. If `SteelShield` appears 8 times and `OrcishShield` appears 1 time, steel is 8x more likely. This is deliberate, not a data error.

---

## The Reasoning Framework

Before calling `mo2_create_patch` with a `merge_leveled_list` operation, work through these steps:

### Step 1: Pull the Conflict Chain

```
mo2_conflict_chain(formid="Skyrim.esm:03DF22")
```

Identify every plugin that touches this list, in load order.

### Step 2: Read Every Version

```
mo2_record_detail(formid="...", plugin_name="PluginA.esp")
```

For each plugin in the chain, read its version of the list. Note:
- How many entries it has
- What Level values it uses (leveled tiers? all level 1?)
- Which entries are new vs carried from vanilla
- Whether it restructured the weighting (duplicate counts)

### Step 3: Classify Each Plugin's Intent

Every plugin in the chain falls into one of these categories:

| Category | What It Does | Example | Merge Treatment |
|---|---|---|---|
| **Overhaul** | Completely restructures the list — changes levels, weighting, and entries. Represents a deliberate design decision. | Requiem deleveling all entries to level 1 | Use as the **base** for the merge |
| **Extension** | Builds on the overhaul — adds more entries following the overhaul's conventions | Requiem WAR adding buckler entries with Requiem-style weighting | Use as the **base** (it extends the overhaul) |
| **Content addition** | Adds new items to the list without caring about structure | Vikings Weaponry adding 2 shields | Merge its **unique entries** into the base |
| **ITM / Carry-forward** | Copied the list to add placed references or quest items, didn't intentionally change it | Quest mods carrying vanilla CELL data | **Ignore** — not a real edit |

### Step 4: Identify the Correct Base

The base is the plugin whose list structure should be preserved. This is almost never vanilla. The decision tree:

1. **Is there an overhaul?** (Requiem, YASH, etc.) → Its version is the starting base.
2. **Does a patch extend the overhaul?** (WAR, Minor Arcana, etc.) → Use the extension as the base instead — it incorporates the overhaul's changes plus its own.
3. **No overhaul?** → Use vanilla as the base. This is the only case where vanilla is correct.

If multiple overhauls conflict on the same list (rare but possible), this requires manual judgment — you cannot mechanically merge two incompatible design philosophies.

### Step 5: Identify What to Merge In

For each content addition mod:
- Find entries that are unique to that mod (not present in the base)
- These are the entries to add

Do NOT merge entries from:
- The overhaul (it's already the base)
- ITM carry-forwards (no real changes)
- Vanilla (the overhaul intentionally replaced it)

### Step 6: Construct the Merge Call

```json
{
  "op": "merge_leveled_list",
  "formid": "Skyrim.esm:03DF22",
  "base_plugin": "Requiem - Weapons and Armor Redone.esp",
  "override_plugins": ["Vikings Weaponry LL - Johnskyrim.esp"]
}
```

The base is the overhaul/extension. The overrides are only the content mods whose additions should be preserved.

---

## Common Mistakes

### Starting from vanilla when an overhaul exists

**Wrong:** Merge Vikings + WAR against Skyrim.esm base.
**Result:** Resurrects vanilla leveled tiers that Requiem intentionally removed. Produces a nonsensical hybrid of vanilla structure with Requiem entries.
**Right:** Use WAR as base, merge only Vikings' additions.

### Treating an overhaul's restructure as "additions"

**Wrong:** See Requiem adds entries not in vanilla, classify them as additions to merge.
**Reality:** Requiem replaced the entire list. Its entries aren't additions to vanilla — they ARE the new list. Treating them as additions duplicates them on top of themselves.

### Ignoring duplicate weighting

**Wrong:** Deduplicate entries during merge (remove "duplicate" SteelShield entries).
**Reality:** Those duplicates are intentional weighting. 8 copies of SteelShield means steel drops 8x more often. Removing "duplicates" destroys the probability distribution.

### Merging content from a mod that depends on vanilla structure

Some content mods add entries with specific level tiers (e.g., Level 25 = ebony-tier shield). If the overhaul delevels everything to level 1, adding a Level 25 entry makes that item never appear (Requiem NPCs don't have vanilla-scaled levels). The correct action is to re-level the added entry to match the overhaul's convention (level 1) or skip it entirely.

**Check:** Does the content mod's entry use a Level value > 1? Does the base use all level 1? If so, the entry needs its Level changed to 1, not just blindly added.

### Not checking what entries reference

A leveled list entry can reference an item (ARMO/WEAP) or another leveled list (LVLI). Before merging, check what the new entries actually point to:
- If it's a specific item → straightforward addition
- If it's a sub-list → that sub-list might also have conflicts that need merging separately
- If it's a sub-list from a mod that isn't loaded → the reference is broken

---

## Recursive Conflicts (Nested List Merging)

When two mods both add items at different levels of the tree:

```
LootBanditWeapon100                    ← Conflict: Mod A adds a new sub-list here
  └── LI_Loot_Weapon_Sword            ← Conflict: Mod B adds a new sword here
```

These are two independent merge operations:
1. Merge `LootBanditWeapon100` — add Mod A's sub-list
2. Merge `LI_Loot_Weapon_Sword` — add Mod B's sword

Query both leveled lists' conflict chains independently. Do NOT try to merge them in a single operation.

---

## Verification Checklist

After creating a leveled list merge patch, verify in xEdit:

1. **Entry count** — does the merged list have the expected number of entries?
2. **Level values** — are all entries using the overhaul's convention (e.g., all level 1 for Requiem)?
3. **Weighting preserved** — does the base's duplicate pattern survive (not deduplicated)?
4. **New entries present** — are the content mod's additions visible?
5. **No vanilla ghosts** — are vanilla entries that the overhaul removed still gone?
6. **References valid** — do all entry references point to records that exist in the load order?
7. **Flags preserved** — are LVLF flags (Calculate from all levels, Calculate for each item) correct?

---

## Real Example: LItemBanditBossShield

**Conflict chain:**
```
Skyrim.esm          → 6 entries, leveled tiers (1, 6, 12, 25, 32)
Vikings Weaponry LL → 8 entries, vanilla + 2 viking shields
Requiem.esp         → 9 entries, ALL level 1, restructured
Requiem WAR         → 27 entries, Requiem structure + bucklers + weighting
Existing merge patch → 27 entries, WAR + Vikings (WINNER — hand-built correct merge)
```

**Analysis:**
- Requiem = overhaul (delevels, restructures)
- WAR = extension (extends Requiem with bucklers)
- Vikings = content addition (adds 2 shields)
- Existing merge patch = reference answer

**Correct merge:**
- Base: `Requiem - Weapons and Armor Redone.esp` (the extension, which incorporates the overhaul)
- Override: `Vikings Weaponry LL - Johnskyrim.esp` (content additions only)
- Result: WAR's 27 entries + Vikings' 2 unique shields = 29 entries

**Wrong merge (what happens if you use vanilla as base):**
- Base: Skyrim.esm (6 vanilla entries with level tiers)
- Result: vanilla structure + 5 new entries = 11 entries with broken leveling
