# v2.7.1 Bridge Coverage Audit

**Methodology:** Read PatchEngine.cs (v2.7.0 baseline at `e77afcd`) operator handlers — `GetKeywordsList`, `ApplyModifications`, `ApplyAddConditions`, `ApplyRemoveConditions`, `ApplyAttachScripts`, `ApplySetFields`, the enchantment block, and the leveled-list `add_items` block — and cross-referenced against Mutagen 0.53.1's Skyrim record schemas at `https://github.com/Mutagen-Modding/Mutagen/tree/dev/Mutagen.Bethesda.Skyrim/Records/Major%20Records`. Every "wire up in P3" row below has its property type verified by Loqui XML schema; every "out of scope" row is backed by explicit Mutagen schema absence (the record genuinely does not expose the property), not by assumption. Phase 0 extends `tools/race-probe/` with a runtime verification block per "wire up in P3" record type — anything that fails the probe gets reclassified.

**Loqui-to-CLR mapping:** `<List>` of `<FormLink refName="X"/>` becomes `ExtendedList<IFormLinkGetter<IXGetter>>` on the getter side / `ExtendedList<IFormLink<IX>>` on the setter side. `<FormLink><Interface>X</Interface></FormLink>` becomes `IFormLinkGetter<IXGetter>` (the getter-side interface is suffixed). `<RefList>` of LoquiObject becomes `ExtendedList<EntryType>` where the entry is itself a generated class.

**Operator-to-mods-key mapping** (Phase 1 codifies this in code as `OperatorModsKeys`). Listed here for cross-reference:

| Operator | mods key |
|---|---|
| `add_keywords` | `keywords_added` |
| `remove_keywords` | `keywords_removed` |
| `add_spells` | `spells_added` |
| `remove_spells` | `spells_removed` |
| `add_perks` | `perks_added` |
| `remove_perks` | `perks_removed` |
| `add_packages` | `packages_added` |
| `remove_packages` | `packages_removed` |
| `add_factions` | `factions_added` |
| `remove_factions` | `factions_removed` |
| `add_inventory` | `inventory_added` |
| `remove_inventory` | `inventory_removed` |
| `add_outfit_items` | `outfit_items_added` |
| `remove_outfit_items` | `outfit_items_removed` |
| `add_form_list_entries` | `form_list_added` |
| `remove_form_list_entries` | `form_list_removed` |
| `add_items` | `items_added` |
| `add_conditions` | `conditions_added` |
| `remove_conditions` | `conditions_removed` |
| `attach_scripts` | `scripts_attached` |
| `set_enchantment` | `enchantment_set` |
| `clear_enchantment` | `enchantment_cleared` |
| `set_fields` | `fields_set` |
| `set_flags` | `flags_changed` |
| `clear_flags` | `flags_changed` (shared) |

---

## Operator: add_keywords / remove_keywords

### Currently dispatched

`PatchEngine.GetKeywordsList` (PatchEngine.cs:1242) — type switch:

- Armor, Weapon, Npc, Ingestible, Ammunition, Book, Flora, Ingredient, MiscItem, Scroll

### Mutagen-supported but not dispatched (ACTION: wire up in P3)

| Record | Mutagen property | Type (getter) | Probe-verified? |
|---|---|---|---|
| Race | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Yes (existing race-probe) |
| Furniture | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Pending (P0 probe extension) |
| Activator | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Pending (P0 probe extension) |
| Location | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Pending (P0 probe extension) |
| Spell | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Pending (P0 probe extension) |
| MagicEffect | `Keywords` | `ExtendedList<IFormLinkGetter<IKeywordGetter>>` | Pending (P0 probe extension) |

All six match the Armor/Weapon dispatch pattern verbatim — the existing `??= new ExtendedList<...>()` idiom transfers without modification.

### Out of scope — Mutagen does not expose Keywords

Schema-confirmed absence (record's `baseClass="SkyrimMajorRecord"` with no `IKeyworded` interface, matches the binary record format which has no KSIZ/KWDA subrecords):

- Container, Door, Light, Perk, Quest, Cell, Worldspace, Region

PLAN.md mentioned investigating Container/Door/Light for keywords — schema-confirmed they genuinely lack the property. Honoring "no new operators" by not inventing artificial keyword storage on records that don't have one.

---

## Operator: add_spells / remove_spells

### Currently dispatched

`ApplyModifications` Npc block (PatchEngine.cs:419) — `npc.ActorEffect`.

### Mutagen-supported but not dispatched (ACTION: wire up in P3)

| Record | Mutagen property | Type (getter) | Probe-verified? |
|---|---|---|---|
| Race | `ActorEffect` | `ExtendedList<IFormLinkGetter<ISpellRecordGetter>>` | Yes (existing race-probe) |

### Out of scope — Mutagen does not expose ActorEffect elsewhere

`ActorEffect` is a property name on NPC and Race only — both are actor-shaped records that hold a list of innate/racial spells. Spell records expose `Effects` (a different shape, `RefList<Effect>` carrying nested LoquiObjects), not an addable spell-link list. Nothing else has the relevant slot.

---

## Operator: add_perks / remove_perks

### Currently dispatched

`ApplyModifications` Npc block (PatchEngine.cs:427) — `npc.Perks` of type `ExtendedList<PerkPlacement>`.

### Mutagen-supported but not dispatched

None. Perks-as-actor-state lives only on NPC. (`Perk.NextPerk` exists as a scalar `FormLink<Perk>` for ranked perk chains, but that's a single-perk reference, not an addable list.)

---

## Operator: add_packages / remove_packages

### Currently dispatched

`ApplyModifications` Npc block (PatchEngine.cs:454) — `npc.Packages` (BehaviorGraph wrapper around package links).

### Mutagen-supported but not dispatched

None. Packages-as-AI-assignment lives only on NPC.

---

## Operator: add_factions / remove_factions

### Currently dispatched

`ApplyModifications` Npc block (PatchEngine.cs:482) — `npc.Factions` of type `ExtendedList<RankPlacement>`.

### Mutagen-supported but not dispatched

None. Faction membership is per-NPC; FACT records themselves carry inter-faction relationships, not a list of members.

---

## Operator: add_inventory / remove_inventory

### Currently dispatched

`ApplyModifications` (PatchEngine.cs:512, 544) — Npc.Items + Container.Items (both `ExtendedList<ContainerEntry>`).

### Mutagen-supported but not dispatched

None. Inventory-shaped lists exist only on NPC and Container in Skyrim. (LeveledItem entries are a separate shape; that's `add_items`.)

---

## Operator: add_outfit_items / remove_outfit_items

### Currently dispatched

`ApplyModifications` Outfit block (PatchEngine.cs:599) — `outfit.Items` of type `ExtendedList<IFormLinkGetter<IOutfitTargetGetter>>`.

### Mutagen-supported but not dispatched

None. Outfit-item lists exist only on Outfit.

---

## Operator: add_form_list_entries / remove_form_list_entries

### Currently dispatched

`ApplyModifications` FormList block (PatchEngine.cs:627) — `formList.Items` of type `ExtendedList<IFormLinkGetter<ISkyrimMajorRecordGetter>>`.

### Mutagen-supported but not dispatched

None. FormList items exist only on FLST by definition.

---

## Operator: add_items

### Currently dispatched

`ApplyModifications` LeveledItem block (PatchEngine.cs:578) — `leveledItem.Entries` of type `ExtendedList<LeveledItemEntry>`. Entry's `Reference` is `IFormLinkGetter<IItemGetter>`.

### Mutagen-supported but not dispatched (ACTION: wire up in P3)

| Record | Mutagen property | Entry type | Reference target | Probe-verified? |
|---|---|---|---|---|
| LeveledNpc | `Entries` | `LeveledNpcEntry` | `IFormLinkGetter<INpcSpawnGetter>` | Pending (P0 probe extension) |
| LeveledSpell | `Entries` | `LeveledSpellEntry` | `IFormLinkGetter<ISpellRecordGetter>` | Pending (P0 probe extension) |

Entry struct shape is consistent across all three leveled-list types: `Data.{Level (Int16), Count (Int16), Reference}`. `ChanceNone` lives on the parent (LVLN/LVSP) at the record level, not per-entry — different from LVLI which carries it per-entry. The P3 wire-up does not need to set ChanceNone (current LVLI handling doesn't either; that's a `set_fields` operation if ever needed).

`MergeLeveledNpcs` and `MergeLeveledSpells` (PatchEngine.cs:289, 339) already construct these entry types correctly — the P3 wire-up mirrors that construction shape under the `add_items` path.

### Out of scope

No other record type carries a `<RefList>` of leveled-list-style entries. CONT.Items uses a different entry shape (`ContainerEntry` = inventory) and is wired via `add_inventory`.

---

## Operator: add_conditions / remove_conditions

### Currently dispatched

`ApplyAddConditions` / `ApplyRemoveConditions` (PatchEngine.cs:950, 1036) — generic via reflection on a property literally named `Conditions` typed `ExtendedList<Condition>`. Throws "does not support conditions" if the property is missing.

### Mutagen-supported via the existing reflection path (no wire-up needed — already covered)

| Record | Property | Reflection-compatible? |
|---|---|---|
| MagicEffect | `Conditions` | Yes — exact name match, type match |
| Perk | `Conditions` | Yes — exact name match, type match |
| Package | `Conditions` | Yes — Package has a top-level Conditions list |

These three already work today through `ApplyAddConditions`'s reflection lookup. No P3 wire-up needed; the bug-class fix in Tier D ensures any record without `Conditions` returns a clean error rather than silently succeeding.

### Out of scope (v2.8 candidates) — Mutagen exposes condition lists under non-standard names

| Record | Property name(s) | Reason out of scope |
|---|---|---|
| Quest | `DialogConditions`, `EventConditions` | Two condition lists, not one. The current operator can't disambiguate which list to target. Would need an operator parameter (e.g. `condition_target: "dialog" \| "event"`). New parameter = new operator surface; v2.7.1 scope-locked at "no new operators." |
| Spell | (per-Effect) `Effects[i].Conditions` | Spell-level conditions don't exist; conditions live nested inside each magic effect. Per-effect mutation is a deeper traversal not covered by Tier C's terminal-bracket-only rule. v2.8. |

The v2.7.0 KNOWN_ISSUES note about "Record type Spell does not support conditions" remains accurate — and Tier D will surface it as a clean unmatched-operator error rather than the today behavior where the throw bubbles up as an opaque InvalidOperationException.

---

## Operator: attach_scripts

### Currently dispatched

`ApplyAttachScripts` (PatchEngine.cs:1083) — generic via reflection on a property literally named `VirtualMachineAdapter` typed `VirtualMachineAdapter`. Throws "does not support scripts" if the property is missing.

### Mutagen-supported via the existing reflection path (no wire-up needed — already covered)

NPC, Quest, Armor, Weapon, Outfit, Container, Door, Activator, Furniture, Light, MagicEffect, Spell, and many others expose `VirtualMachineAdapter`. Existing reflection covers them.

### Out of scope (v2.8 candidate) — adapter type variation

Perk uses `PerkAdapter` (subclass of VirtualMachineAdapter); Quest uses `QuestAdapter` (subclass). The existing `vmadProp.GetValue(record) as VirtualMachineAdapter` cast succeeds for the base, but the construction `new VirtualMachineAdapter()` won't match what those records expect when the property is null. Real-world impact: if a user invokes `attach_scripts` on a Perk record with no existing scripts, the auto-create path constructs the wrong adapter type. Triage as v2.8 — needs a per-type adapter factory, distinct from the v2.7.1 wire-up theme. Tier D will not catch this because the property exists and the operation reports `scripts_attached: N`; the failure mode is "scripts attached but adapter shape is wrong."

---

## Operator: set_enchantment / clear_enchantment

### Currently dispatched

`ApplyModifications` enchantment block (PatchEngine.cs:665) — Armor.ObjectEffect + Weapon.ObjectEffect.

### Mutagen-supported but not dispatched

None. **Ammunition has no `ObjectEffect` slot in Mutagen's schema** — the AMMO Data block carries Projectile / Flags / Damage / Value / Weight only, no enchantment FormLink. Schema-confirmed absence; honoring the absence rather than inventing one.

### Out of scope (v2.8 candidate)

If a future Skyrim record gains an enchantment-shaped slot in a later Mutagen schema bump, re-audit. Today there are zero gaps.

---

## Operator: set_fields / set_flags / clear_flags

### Currently dispatched

Generic via reflection (set_fields walks `SetPropertyByPath` against any property name; set_flags/clear_flags handles NPC.Configuration.Flags + SkyrimMajorRecord.SkyrimMajorRecordFlags). Always matches a handler — never silently no-ops.

**However:** `set_fields` against `RACE.Starting` and `RACE.Regen` rejects with "Cannot convert JSON" today because:

1. `Starting` and `Regen` are `Dict<BasicStat, float>` properties with **no public setter** — `prop.SetValue(target, converted)` fails because the dict can only be mutated via its indexer.
2. `ConvertJsonValue` has no `JsonValueKind.Object` branch, so a JSON-object-shaped value (`{"Health": 100, "Magicka": 200}`) doesn't convert to a dict.

Both are addressed by Tier C in Phase 2 — bracket-indexer path syntax (`Starting[Health]`) and JSON-object-to-dict conversion. No P3 wire-up needed; Tier C is generic and works on any Mutagen `IDictionary<,>` property.

### Mutagen-supported via Tier C (Phase 2) — bracket-indexer or whole-dict syntax

| Record | Property | Type | Use case |
|---|---|---|---|
| Race | `Starting` | `Dict<BasicStat, float>` | Per-stat starting Health/Magicka/Stamina |
| Race | `Regen` | `Dict<BasicStat, float>` | Per-stat regen rate |
| Race | `BipedObjectNames` | `Dict<BipedObject, string>` (custom binary) | Slot name overrides — freebie when Tier C lands; same key-parsing path |

`BipedObjectNames` is a custom-binary-encoded dict per the schema, but its in-memory CLR shape is a regular `IDictionary<BipedObject, string>` — Tier C's bracket-indexer path handles it identically to `Starting`. No special-case code needed.

### Tier B aliases (Phase 4) — RACE only

| Alias | Resolves to |
|---|---|
| `BaseHealth` | `Starting[Health]` |
| `BaseMagicka` | `Starting[Magicka]` |
| `BaseStamina` | `Starting[Stamina]` |
| `HealthRegen` | `Regen[Health]` |
| `MagickaRegen` | `Regen[Magicka]` |
| `StaminaRegen` | `Regen[Stamina]` |

Plain-float fields on RACE (`UnarmedDamage`, `UnarmedReach`, `BaseMass`, etc.) need no aliases — they already work via canonical name through the existing reflection path (probe-verified).

---

## Summary — Phase 3 wire-up totals

**Record types touched:** 9 — Race, Furniture, Activator, Location, Spell, MagicEffect, LeveledNpc, LeveledSpell, (and Race appears in two operators).

**(operator, record-type) pairs to wire:**

| Operator | Records added | Pair count |
|---|---|---|
| add_keywords | Race, Furniture, Activator, Location, Spell, MagicEffect | 6 |
| remove_keywords | Race, Furniture, Activator, Location, Spell, MagicEffect | 6 |
| add_spells | Race | 1 |
| remove_spells | Race | 1 |
| add_items | LeveledNpc, LeveledSpell | 2 |
| **Total** | | **16** |

Each pair lands as one switch case (keywords) or one `if (record is X x)` block (spells, add_items). Phase 3's smoke matrix exercises one representative record per pair — 16 smoke rows.

**Probe extensions needed in Phase 0:** 7 new verification blocks — 6 keyword carriers (Furniture, Activator, Location, Spell, MagicEffect, plus Race already covered) and 2 leveled-list types. Race is already done. The new probe blocks each: (1) construct the record in-memory, (2) confirm the property exists with the expected name and reflected type, (3) mutate it (Add to the list / construct the entry), (4) round-trip through `WriteToBinary` / `CreateFromBinary` and confirm read-back.

---

## Carry-overs explicitly noted (NOT wired in v2.7.1)

These are flagged so v2.8 can pick them up without re-discovery:

1. **Quest condition lists** (`DialogConditions`, `EventConditions`) — needs operator parameter to disambiguate.
2. **Per-effect spell conditions** (`Spell.Effects[i].Conditions`) — needs nested-LoquiObject mutation; deeper than Tier C's terminal-bracket scope.
3. **Adapter-subclass attach_scripts** (`PerkAdapter` on PERK, `QuestAdapter` on QUST) — needs per-type adapter factory.
4. **AMMO enchantment** — Mutagen's schema has no slot; would require Mutagen-side change.
5. **Replace-semantics whole-dict assignment** (Tier C ships merge-only).
6. **Chained dict access** (`Foo[Key].Sub`) — Tier C ships terminal-bracket-only.
