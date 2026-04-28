# v2.9.3 Phase 1 — APerkEffect API contract audit

**Probe binary:** `tools/race-probe/bin/Release/net8.0/race-probe.exe`
**Probe source:** `tools/race-probe/Program.cs` (v2.9.3 P1 inventory section appended at lines 5132+, Phase 1.5 PEPM round-trip supplemental at lines 5897+)
**Probe outputs:**
- Original P1 inventory: `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` (160 KB, 2420 lines)
- P1.5 PEPM round-trip re-run: `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt` (170 KB, 2540 lines)
**Mutagen package:** `Mutagen.Bethesda.Skyrim 0.53.1` (matches the bridge's `PackageReference`)
**Probe exit code:** `1` (driven entirely by 2 v2.9.3 P1 architectural-rename surprises captured below; 0 v293p15Failures; all other sections clean)
**Date:** 2026-04-28

This audit captures the runtime shape of every concrete `Mutagen.Bethesda.Skyrim.APerkEffect` subclass, surfaces the architectural surprises that invalidate PLAN.md's pre-probe assumed naming, and frames the per-leaf property surface for Phase 2's bridge factory transcription. **Phase 2 transcribes this audit; it does not speculate on subclass names or property names.**

---

## Inventory totals — 12 concrete leaves + 1 abstract intermediate

Authoritative count for Mutagen 0.53.1. The PLAN.md user-task-spec estimate of "~13 concrete subclasses" is close to the actual 12; the overcount likely came from including the abstract intermediate `APerkEntryPointEffect`.

**Polymorphism shape (third-level):**
- `APerkEffect` (abstract base) — 5 base properties: `ButtonLabel: TranslatedString`, `Conditions: ExtendedList<PerkCondition>`, `Flags: PerkScriptFlag`, `Priority: Byte`, `Rank: Byte`.
- `APerkEntryPointEffect : APerkEffect` (abstract intermediate) — declares 2 additional properties shared by all PerkEntryPoint* concretes: `EntryPoint: APerkEntryPointEffect+EntryType` (91-member enum), `PerkConditionTabCount: Byte`.
- 12 concrete leaves (alphabetical):
  1. `PerkAbilityEffect` (1 subclass-specific prop: `Ability`)
  2. `PerkEntryPointAbsoluteValue` (3 subclass-specific props inc. inherited from APerkEntryPointEffect)
  3. `PerkEntryPointAddActivateChoice` (3)
  4. `PerkEntryPointAddLeveledItem` (3)
  5. `PerkEntryPointAddRangeToValue` (4)
  6. `PerkEntryPointModifyActorValue` (5)
  7. `PerkEntryPointModifyValue` (4)
  8. `PerkEntryPointModifyValues` (5)
  9. `PerkEntryPointSelectSpell` (3)
  10. `PerkEntryPointSelectText` (3)
  11. `PerkEntryPointSetText` (3)
  12. `PerkQuestEffect` (3)

**Sum check:** 1 abstract base + 1 abstract intermediate + 12 concrete leaves = 14 total `APerkEffect`-rooted types in Mutagen 0.53.1. ✓

---

## Architectural surprises (Q1/Q5/Q7 transcription corrections; conductor auto-accepted)

Two halt-trigger ARCH SURPRISE log lines fired correctly during the original P1 run; both resolve as **naming/location transcription corrections** rather than design re-litigation. Conductor auto-accepted these per audit-as-source-of-truth (mechanism design intact; surface naming changes per the actual schema).

### Surprise A — Third-level polymorphism via abstract `APerkEntryPointEffect` intermediate

PLAN.md § B implicitly assumed a flat polymorphism: `APerkEffect` (abstract) → 12-ish concrete leaves with `EntryPoint` enum on a single shared "PerkEntryPointEffect" parent. Probe evidence (P1 scratch line 2185–2186):

```
*** ARCH SURPRISE: 1 abstract APerkEffect subclass(es) found (third-level polymorphism — Q1 flat dispatcher would not cover them):
    - Mutagen.Bethesda.Skyrim.APerkEntryPointEffect
```

**Resolution.** `APerkEntryPointEffect` is the abstract intermediate — it declares `EntryPoint` + `PerkConditionTabCount` shared by the 10 `PerkEntryPoint*` concrete leaves. Q1's `type:` discriminator strategy holds **as a flat dispatch over the 12 concrete leaves** (NOT over a hypothetical "PerkEntryPointEffect" parent). The factory's `Mutagen.Bethesda.Skyrim.{TypeName}` reflection-lookup with abstract-rejection guard handles this correctly — abstract base + intermediate both reject, only the 12 concrete leaves construct. **Q1 = A holds; valid `type:` values are the 12 leaf class names.**

### Surprise B — `PerkEntryPointEffect` doesn't exist as a class in Mutagen 0.53.1

PLAN.md § A/B/C examples + MATRIX `1.P.PerkEntryPointEffect.*` cells + Q1/Q5 example payload `{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.5}` referenced a class name absent from Mutagen 0.53.1. Probe evidence (P1 scratch line 2356):

```
*** ARCH SURPRISE: PerkEntryPointEffect not found in Mutagen 0.53.1 — Layer 1.P anchor + Q1 example reference depend on this name; halt-worthy
```

**Resolution.** PLAN.md's naming was constructed from the read-side render (which flattens via Mutagen's display contract), not the actual write-side schema. Per-EntryPoint-shape decomposition is by concrete leaf class:
- `PerkEntryPointModifyValue` (60.3% dominant): EntryPoint enum + Modification (own per-class ModificationType enum) + `Value: Nullable<Single>` + PerkConditionTabCount.
- `PerkEntryPointModifyActorValue`: same + `ActorValue` enum.
- `PerkEntryPointAddRangeToValue`: `From` + `To` Singles (range shape).
- `PerkEntryPointSelectSpell` (22.7%): `Spell: IFormLink<ISpellGetter>`.
- `PerkEntryPointAddActivateChoice`: `Spell: IFormLinkNullable<ISpellGetter>` (nullable, NOT same as SelectSpell).
- `PerkEntryPointAddLeveledItem`: `Item: IFormLink<ILeveledItemGetter>`.
- `PerkEntryPointSelectText`: `Text: System.String` (plain).
- `PerkEntryPointSetText`: `Text: TranslatedString` (Sub-Loqui — different write contract).
- `PerkEntryPointAbsoluteValue`: `Negative: Boolean`.
- `PerkEntryPointModifyValues`: `Value` + `Value2` `Nullable<Single>`.

**Q5 = A holds; canonical names rename to leaf-class names.** Phase 2 schema description must list the 12 valid `type:` discriminator values explicitly (no `PerkEntryPointEffect` umbrella).

### Surprise C — Q7 field-name + location correction

`Conditions` is on `APerkEffect` BASE (`Noggog.ExtendedList<PerkCondition>`), NOT named `PerkConditions` and NOT nested on a hypothetical PEPE. The Q7 wrapper-object direction holds — `PerkCondition` IS the wrapper with `RunOnTabIndex` + nested `Conditions` — but PLAN.md's field name was wrong.

**Resolution.** Actual write DSL has TWO `Conditions` keys at different nesting depths: outer `APerkEffect.Conditions: ExtendedList<PerkCondition>`, inner `PerkCondition.Conditions: ExtendedList<Condition>`. Phase 2 schema description must call out the 2-level nesting clearly.

```jsonc
{
  "type": "PerkEntryPointModifyValue",
  "EntryPoint": "ModSpellMagnitude",
  "Modification": "Multiply",
  "Value": 1.5,
  "Conditions": [                                  // OUTER — APerkEffect.Conditions
    {
      "RunOnTabIndex": 1,
      "Conditions": [                              // INNER — PerkCondition.Conditions
        { "function": "HasPerk", "operator": "==", "value": 1, "parameters": { "Perk": "Skyrim.esm:058200" } }
      ]
    }
  ]
}
```

**Q7 = A holds; field name + location corrected.**

---

## Constructibility — `Activator.CreateInstance` results

Mirrors v2.8.0 EFFECTS_AUDIT shape for `Condition`. Abstract base + abstract intermediate fail expected; all 12 concrete leaves construct cleanly.

| Type | Result | Notes |
|---|---|---|
| `Mutagen.Bethesda.Skyrim.APerkEffect` (abstract base) | **FAIL** (`MissingMethodException: Cannot dynamically create an instance of type 'Mutagen.Bethesda.Skyrim.APerkEffect'. Reason: Cannot create an abstract class.`) | Confirms factory's abstract-rejection guard works; mirrors v2.8.0 `Condition` shape. |
| `Mutagen.Bethesda.Skyrim.APerkEntryPointEffect` (abstract intermediate) | **FAIL** (would throw same MissingMethodException; not directly probed in Activator table — third-level polymorphism scan flagged it as ARCH SURPRISE, sufficient evidence) | Documented as ARCH NOTE; valid `type:` rejects this name with abstract-class error. |
| `PerkAbilityEffect` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointAbsoluteValue` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointAddActivateChoice` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointAddLeveledItem` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointAddRangeToValue` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointModifyActorValue` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointModifyValue` | **OK** | parameterless ctor available; Activator-constructible. **Anchor for Phase 1.5 round-trip.** |
| `PerkEntryPointModifyValues` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointSelectSpell` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointSelectText` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkEntryPointSetText` | **OK** | parameterless ctor available; Activator-constructible. |
| `PerkQuestEffect` | **OK** | parameterless ctor available; Activator-constructible. **Anchor for original P1 round-trip.** |

**Q2 = A holds (Aaron 2026-04-28):** all 12 concrete leaves are factory-routable. No subclass requires a non-parameterless ctor.

---

## Per-subclass property surface

Each row: property name, ShapeTag (`[FormLink]` / `[Enum]` / `[List]` / `[Sub-Loqui]` / `[Primitive]` / `[Other]`), full type, declaring class. `[base]` properties (5 inherited from `APerkEffect`/ancestors) elided per-subclass below; they are uniformly: `ButtonLabel: TranslatedString [Sub-Loqui]`, `Conditions: ExtendedList<PerkCondition> [List]`, `Flags: PerkScriptFlag [Sub-Loqui]`, `Priority: Byte [Primitive]`, `Rank: Byte [Primitive]`.

### `PerkAbilityEffect` — 1 subclass-specific property

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `Ability` | [FormLink] | `IFormLink<ISpellGetter>` | PerkAbilityEffect |

### `PerkEntryPointAbsoluteValue` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `Negative` | [Primitive] | `System.Boolean` | PerkEntryPointAbsoluteValue |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |

### `PerkEntryPointAddActivateChoice` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Spell` | [FormLink] | `IFormLinkNullable<ISpellGetter>` | PerkEntryPointAddActivateChoice |

### `PerkEntryPointAddLeveledItem` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `Item` | [FormLink] | `IFormLink<ILeveledItemGetter>` | PerkEntryPointAddLeveledItem |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |

### `PerkEntryPointAddRangeToValue` — 4 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `From` | [Primitive] | `System.Single` | PerkEntryPointAddRangeToValue |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `To` | [Primitive] | `System.Single` | PerkEntryPointAddRangeToValue |

### `PerkEntryPointModifyActorValue` — 5 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `ActorValue` | [Enum] | `Mutagen.Bethesda.Skyrim.ActorValue` | PerkEntryPointModifyActorValue |
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `Modification` | [Enum] | `PerkEntryPointModifyActorValue+ModificationType` (per-class) | PerkEntryPointModifyActorValue |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Value` | [Primitive] | `System.Single` (plain, NOT nullable) | PerkEntryPointModifyActorValue |

### `PerkEntryPointModifyValue` — 4 subclass-specific properties **(60.3% dominant; Phase 1.5 round-trip anchor)**

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `Modification` | [Enum] | `PerkEntryPointModifyValue+ModificationType` (3 members: `Set`, `Add`, `Multiply`) | PerkEntryPointModifyValue |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Value` | [Other] | `System.Nullable<System.Single>` | PerkEntryPointModifyValue |

### `PerkEntryPointModifyValues` — 5 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `Modification` | [Enum] | `PerkEntryPointModifyValue+ModificationType` (shared with sibling) | PerkEntryPointModifyValues |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Value` | [Other] | `System.Nullable<System.Single>` | PerkEntryPointModifyValues |
| `Value2` | [Other] | `System.Nullable<System.Single>` | PerkEntryPointModifyValues |

### `PerkEntryPointSelectSpell` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Spell` | [FormLink] | `IFormLink<ISpellGetter>` (NOT nullable, vs AddActivateChoice's nullable variant) | PerkEntryPointSelectSpell |

### `PerkEntryPointSelectText` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Text` | [Other] | `System.String` (plain string, NOT TranslatedString) | PerkEntryPointSelectText |

### `PerkEntryPointSetText` — 3 subclass-specific properties

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `EntryPoint` | [Enum] | `APerkEntryPointEffect+EntryType` | APerkEntryPointEffect |
| `PerkConditionTabCount` | [Primitive] | `System.Byte` | APerkEntryPointEffect |
| `Text` | [Sub-Loqui] | `Mutagen.Bethesda.Strings.TranslatedString` (different from SelectText's plain String) | PerkEntryPointSetText |

### `PerkQuestEffect` — 3 subclass-specific properties **(original P1 round-trip anchor)**

| Property | Tag | Type | Declared on |
|---|---|---|---|
| `Quest` | [FormLink] | `IFormLink<IQuestGetter>` | PerkQuestEffect |
| `Stage` | [Primitive] | `System.Byte` (NOT Int32) | PerkQuestEffect |
| `Unknown` | [Other] | `Noggog.MemorySlice<System.Byte>` (opaque blob, NOT a write-target) | PerkQuestEffect |

---

## PerkEntryPointModifyValue anchor — Phase 1.5 round-trip evidence

`PerkEntryPointModifyValue` is the 60.3% dominant on-disk shape (511 effects across 375 vanilla+DLC PERK records). It replaces PLAN.md's hypothetical "PerkEntryPointEffect" as the canonical Layer 1.P anchor. P1.5 supplemental probe extended race-probe with a re-targeted round-trip after the original P1 PEPE anchor halt-triggered on the missing class name.

### EntryPoint enum dump

`Mutagen.Bethesda.Skyrim.APerkEntryPointEffect+EntryType` — **91 members.** PLAN.md § Background guessed ~140; actual is 91. `ModSpellMagnitude` is at index [29].

First 20 members (full list at P1.5 scratch lines 2425–2515): `CalculateWeaponDamage`, `CalculateMyCriticalHitChance`, `CalculateMyCriticalHitDamage`, `CalculateMineExplodeChance`, `AdjustLimbDamage`, `AdjustBookSkillPoints`, `ModRecoveredHealth`, `GetShouldAttack`, `ModBuyPrices`, `AddLeveledListOnDeath`, `GetMaxCarryWeight`, `ModAddictionChance`, `ModAddictionDuration`, `ModPositiveChemDuration`, `Activate`, `IgnoreRunningDurationDetection`, `IgnoreBrokenLock`, `ModEnemyCriticalHitChance`, `ModSneakAttackMult`, `ModMaxPlacableMinex`. (Members 20–90 in scratch.)

### Modification enum dump

`Mutagen.Bethesda.Skyrim.PerkEntryPointModifyValue+ModificationType` — **3 members: `Set`, `Add`, `Multiply`.** Per-class enum (NOT shared across PerkEntryPoint*-family — `PerkEntryPointModifyActorValue` has its own `+ModificationType` nested type, and `PerkEntryPointModifyValues` shares with sibling `PerkEntryPointModifyValue`).

### Synthetic round-trip

**Literal Modification enum value used:** `Multiply` (preferred match — no fallback fired).

Wrote synthetic 212-byte ESP at `Path.GetTempPath()/raceprobe-v2.9.3-p15-pepm/PerkRT_PEPM.esp`. Round-trip readback assertions all PASS:

| Assertion | Expected | Observed | Result |
|---|---|---|---|
| Effects.Count | 1 | 1 | ✓ |
| Effects[0] runtime type | PerkEntryPointModifyValue | PerkEntryPointModifyValue | ✓ |
| EntryPoint | ModSpellMagnitude | ModSpellMagnitude | ✓ |
| Modification | Multiply | Multiply | ✓ |
| Value (Nullable<Single>) | 1.5 | 1.5 | ✓ |
| Conditions.Count (outer) | 1 | 1 | ✓ |
| Conditions[0].RunOnTabIndex | 1 | 1 | ✓ |
| Conditions[0].Conditions.Count (inner) | 1 | 1 | ✓ |
| Conditions[0].Conditions[0] runtime type | ConditionFloat | ConditionFloat | ✓ |
| Inner Condition.Data runtime type | GetActorValueConditionData | GetActorValueConditionData | ✓ |
| Inner Condition.Data.ActorValue | Destruction | Destruction | ✓ |

**Q4 (v2.9.0 dispatcher composition) sanity-confirm.** The inner `ConditionFloat.Data` round-tripped as concrete `GetActorValueConditionData` with `ActorValue = Destruction` enum slot intact — confirming v2.9.0's RouteParameterSlot composition holds untouched for nested conditions inside `APerkEffect.Conditions[i].Conditions[j]`. Phase 2 cell `1.P.PerkEntryPointModifyValue.with_v290_params` (composition probe with `parameters: {Perk: ...}` on `HasPerk`) is expected to land cleanly through the same path.

---

## PerkAbilityEffect anchor — original P1 informational-skip note

Original P1 anchor helper looked up `PerkAbility` (the PLAN.md name) and reported informational SKIP since `PerkAbility` is not enumerated in Mutagen 0.53.1. Actual class name: `PerkAbilityEffect`. Property surface dump (per § Per-subclass property surface above) is sufficient for Phase 2 transcription; round-trip verification deferred to Phase 2's coverage-smoke (cell `1.P.PerkAbilityEffect.basic`).

```
Note: PerkAbility not enumerated in concrete subclass set; skipping anchor (Phase 2 schema description omits it).
```

(P1 scratch line 2359.)

---

## PerkQuestEffect anchor — original P1 round-trip evidence

Original P1 PerkQuestEffect anchor hit the helper's success path. Wrote synthetic 197-byte ESP at `Path.GetTempPath()/raceprobe-v2.9.3-p1-PerkQuestEffect/PerkRT_PerkQuestEffect.esp`. Round-trip readback assertions all PASS:

| Assertion | Expected | Observed | Result |
|---|---|---|---|
| Effects.Count | 1 | 1 | ✓ |
| Effects[0] runtime type | PerkQuestEffect | PerkQuestEffect | ✓ |
| Quest.FormKey | 000200:Skyrim.esm | 000200:Skyrim.esm | ✓ |
| Stage (Byte) | 100 | 100 | ✓ |

(P1 scratch lines 2362–2367.)

`Stage` is `System.Byte` (not `Int32` per PLAN.md's spec); my probe used `Convert.ChangeType(100, stageProp.PropertyType)` which round-tripped cleanly. Phase 2 bridge `ConvertJsonValue` handles `JsonElement.GetInt32 → Byte` conversion via the existing `Convert.ChangeType` path.

---

## Real-world frequency table — vanilla + 4 DLC ESMs

523 PERK records scanned across `Skyrim.esm` (375) + `Update.esm` (16) + `Dawnguard.esm` (57) + `HearthFires.esm` (1) + `Dragonborn.esm` (74). Per-plugin scan all OK; no missing files.

**Aggregate totals:**

| Subclass | Effects | % | Distinct Records |
|---|---:|---:|---:|
| `PerkEntryPointModifyValue` | 511 | **60.3%** | 375 |
| `PerkEntryPointSelectSpell` | 192 | **22.7%** | 45 |
| `PerkEntryPointModifyActorValue` | 54 | 6.4% | 3 |
| `PerkAbilityEffect` | 34 | 4.0% | 34 |
| `PerkQuestEffect` | 28 | 3.3% | 28 |
| `PerkEntryPointAddActivateChoice` | 21 | 2.5% | 15 |
| `PerkEntryPointSetText` | 4 | 0.5% | 4 |
| `PerkEntryPointSelectText` | 3 | 0.4% | 3 |
| **Total (8 with vanilla data)** | **847** | **100.0%** | — |

**4 of 12 subclasses have ZERO vanilla+DLC instances** (informational; modders may use; no anchor for Layer 1.P live FormID): `PerkEntryPointAbsoluteValue`, `PerkEntryPointAddLeveledItem`, `PerkEntryPointAddRangeToValue`, `PerkEntryPointModifyValues`. Phase 2 covers these via synthetic round-trip cells (cf. v2.9.2 P4's synthetic missing-master fixture pattern at `4.dsl.06`).

Per-plugin breakdown: see P1 scratch lines 2376–2406. Authoria signal (per PLAN.md § E read-side observation: 9 representative PERK records, 100% PerkEntryPoint*-family rendering) is consistent with the vanilla+DLC 92.4% PerkEntryPoint*-family share (sum of 60.3 + 22.7 + 6.4 + 2.5 + 0.5 + 0.4 = 92.8%; 4.0% PerkAbilityEffect + 3.3% PerkQuestEffect = 7.3% non-PEPE-family).

---

## Phase 2 implications (informational; Phase 2 audit reference)

Schema-divergence items the Phase 2 bridge factory + `ConvertJsonValue` chain must handle. Each is a property-surface gotcha that surfaced during the per-subclass dump but is NOT a halt-trigger.

1. **`Nullable<Single>` Value slot on `PerkEntryPointModifyValue` / `PerkEntryPointModifyValues`.** The bridge `ConvertJsonValue` must handle `JsonValueKind.Null` → `null` for nullable primitive targets. Existing v2.7.x bridge handling for `IFormLinkNullable<T>` may already cover the pattern; Phase 2 audits + adds a positive test cell. P1.5 round-trip evidence: assigning `(float?)1.5f` boxed as `Nullable<Single>` round-tripped cleanly via reflection setter.
2. **`Byte` Stage slot on `PerkQuestEffect`.** Caller-supplied JSON int (`100`) auto-converts via `Convert.ChangeType(targetType=Byte)`. P1 round-trip evidence: `Stage = 100` round-tripped cleanly. Phase 2 cell verifies the same path through the bridge subprocess.
3. **Plain `String` (`PerkEntryPointSelectText.Text`) vs `TranslatedString` Sub-Loqui (`PerkEntryPointSetText.Text`).** Different write contracts per cousin class. `PerkEntryPointSelectText` accepts JSON-string → `String` directly. `PerkEntryPointSetText` needs Branch B sub-LoquiObject merge (`{Text: {String: "...", ...}}` shape per v2.8.0 EFFECTS_AUDIT § Branch B for `EffectData`) OR a plain-string-to-TranslatedString convenience path. Phase 2 picks the contract.
4. **2-level `Conditions` nesting in DSL.** Outer `APerkEffect.Conditions: ExtendedList<PerkCondition>`; inner `PerkCondition.Conditions: ExtendedList<Condition>`. Phase 2 schema description must call this out explicitly. Layer 2.03 cell exercises full-stack composition through both levels.
5. **`PerkEntryPointAddActivateChoice.Spell` is `IFormLinkNullable<ISpellGetter>`** (different from `PerkEntryPointSelectSpell.Spell`'s plain `IFormLink<ISpellGetter>`). Phase 2 schema docs for these cousins differ on nullability.
6. **`PerkQuestEffect.Unknown: Noggog.MemorySlice<System.Byte>`** is an opaque binary blob — NOT a write-target. Phase 2 schema description must not advertise it. Bridge factory `SetPropertyByPath` falling through `ConvertJsonValue` on this property would throw (no handler for MemorySlice); explicit reject-with-clean-error is the safer Phase 2 choice (cell `1.D.<NN>` candidate).
7. **`Modification` enum is per-leaf, not shared.** Mutagen 0.53.1 nests separate `+ModificationType` enums on `PerkEntryPointModifyValue`, `PerkEntryPointModifyValues` (shares the former's enum), and `PerkEntryPointModifyActorValue`. Phase 2 schema description lists the valid values per leaf. PEPM/PEPMs's enum is `{Set, Add, Multiply}`; PEPMA's enum needs a Phase 2 dump (not captured by P1.5).
8. **`Conditions` list element type is concrete `PerkCondition`** (Q7 lock confirmed). Wrapper-object DSL holds. PerkCondition itself is Activator-constructible per the P1.5 confirmation.

---

## Bridge SHA snapshot

Phase 1 doesn't touch the bridge. SHA preserved from v2.9.2 P5 ship — Phase 2's first build will produce the new v2.9.3 SHA per PLAN § Conventions ("Phase 2 bumps the version on its first commit").

---

## References

- **Probe scratch (original P1):** `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` (160 KB, 2420 lines) — authoritative inventory + per-subclass dump + frequency table source.
- **Probe scratch (P1.5 PEPM re-run):** `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt` (170 KB, 2540 lines) — supplemental EntryPoint + Modification enum dumps + round-trip evidence.
- **Probe source:** `Claude_MO2/tools/race-probe/Program.cs` lines 5132+ (v2.9.3 P1 inventory section) + lines 5897+ (Phase 1.5 PEPM round-trip supplemental).
- **PatchEngine working precedent:** `tools/mutagen-bridge/PatchEngine.cs` — `ConvertJsonElementToListItem` line 1441 (Branch A target), `BuildConditionFromJson` line ~2331 (factory pattern reference).
- **v2.8.0 audit-doc template:** `dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md`.
- **v2.9.0 audit-doc template:** `dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md`.
- **Phase 0 design lock + handoff:** `dev/plans/v2.9.3_perk_effects/PHASE_0_HANDOFF.md` § Conductor asks (Q1–Q7).
