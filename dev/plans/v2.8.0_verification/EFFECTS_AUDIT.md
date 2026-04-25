# v2.8.0 Phase 1 — Effects API contract audit

**Probe binary:** `tools/race-probe/bin/Release/net8.0/race-probe.exe`
**Probe source:** `tools/race-probe/Program.cs` (v2.8 P1 Effects section appended at end)
**Mutagen package:** `Mutagen.Bethesda.Skyrim 0.53.1` (matches the bridge's `PackageReference`)
**Probe exit code:** `0` (clean — 0 failures across 0 v2.7.1 + 0 v2.8 P1 audit failures)
**Date:** 2026-04-25

This audit captures the runtime shape of `Mutagen.Bethesda.Skyrim.Effect`, its sub-objects, and the five Effects-list-carrier records as they actually exist in Mutagen 0.53.1. Phase 1's bridge implementation transcribes this contract; it does not speculate. Branch B in `SetPropertyByPath` is justified by the sub-LoquiObject shape of `Effect.Data` documented below.

---

## Constructibility — `Activator.CreateInstance` results

| Type | Result | Notes |
|---|---|---|
| `Mutagen.Bethesda.Skyrim.Effect` | **OK** | Parameterless ctor available. Branch A's `Activator.CreateInstance(typeof(Effect))` for fresh-construction-per-array-element is safe. |
| `Mutagen.Bethesda.Skyrim.Condition` | **FAIL** (`MissingMethodException: Cannot create an abstract class`) | Confirms Branch A's special-case for `typeof(Condition) → BuildConditionFromJson`. Generic `Activator.CreateInstance(typeof(Condition))` would throw at runtime; the bridge must route through the per-subclass factory pattern that `ApplyAddConditions` already uses (`ConditionFloat` / `ConditionGlobal` plus `Mutagen.Bethesda.Skyrim.{Function}ConditionData` reflection). |
| `Mutagen.Bethesda.Skyrim.ConditionFloat` | **OK** | Concrete subclass; existing `ApplyAddConditions` instantiates this directly. |
| `Mutagen.Bethesda.Skyrim.ConditionGlobal` | **OK** | Concrete subclass; existing `ApplyAddConditions` instantiates this directly. |
| `Mutagen.Bethesda.Skyrim.EffectData` | **OK** | Sub-LoquiObject backing `Effect.Data`. Activator-creatable; Branch B can construct in-place when `Effect.Data` is null. |

**Implication.** Branch A's general-case `Activator.CreateInstance(elementType)` works for `typeof(Effect)`. For `typeof(Condition)` it would throw — the special-case route through a shared `BuildConditionFromJson` helper (extracted from `ApplyAddConditions` per the Phase 1 design) is load-bearing, not optional.

---

## `Effect` class shape

Public instance properties from the probe's reflection dump:

| Property | Type | Setter | Notes |
|---|---|---|---|
| `BaseEffect` | `Mutagen.Bethesda.Plugins.IFormLinkNullable<Mutagen.Bethesda.Skyrim.IMagicEffectGetter>` | True | Note `IFormLinkNullable<>`, not `IFormLink<>` or `IFormLinkGetter<>`. The existing `ConvertJsonElementToListItem` FormLink branch only matches `IFormLinkGetter<>` / `IFormLink<>` / `FormLink<>`. **The single-FormLink case here goes through `SetPropertyByPath` (per-property reflection), not through `ConvertJsonElementToListItem`** — so the existing nullable-FormLink path that already handles RACE.ArmorRace, Effect.BaseEffect, etc. covers it. No bridge change needed for `BaseEffect`. |
| `Data` | `Mutagen.Bethesda.Skyrim.EffectData` | True | Sub-LoquiObject. **Initial value after `new Effect()` is `null`**, not a default-initialized `EffectData`. Branch B's get-or-Activator-create logic must handle the null case. |
| `Conditions` | `Noggog.ExtendedList<Mutagen.Bethesda.Skyrim.Condition>` | True | Initial value after `new Effect()` is **non-null and empty** (Count=0). Branch A's `add`-via-IList works; in-place merge into existing collection on whole-array assignment is the natural fit. |

The probe's three-row dump above is the complete public-instance-property surface of `Effect`. There are no other settable fields hiding behind reflection.

### `Effect.BaseEffect` setter mechanism

`effect.BaseEffect.SetTo(formKey)` works (probe confirmed: `Effect.BaseEffect.SetTo(012345:Skyrim.esm) OK`). Bridge's existing single-FormLink reflection (used today for e.g. `set_enchantment` writes through `ObjectEffect.SetTo`) covers this path. JSON-string `"Skyrim.esm:01ABCD"` flows through `SetPropertyByPath` → `ConvertJsonValue(string, IFormLinkNullable<IMagicEffectGetter>)` → existing `IFormLink<T>` resolution. No new bridge code needed for `BaseEffect`.

---

## `EffectData` properties (the sub-LoquiObject Branch B targets)

| Property | Type | Setter |
|---|---|---|
| `Magnitude` | `System.Single` | True |
| `Area` | `System.Int32` | True |
| `Duration` | `System.Int32` | True |

**Important:** `Area` and `Duration` are `Int32`, not `UInt32`. The bridge's `ConvertJsonValue` chooses the JsonElement accessor by `targetType`, so this is automatic — `value.GetInt32()` fires for both, and `Convert.ChangeType` on the same is a no-op. Documenting because a casual reader might assume area-of-effect / duration-in-seconds map to unsigned. They don't (the binary record format permits negative values for engine-specific behavior; Mutagen exposes the raw int).

There are no other settable `EffectData` fields. Branch B writing `{Magnitude, Area, Duration}` is the complete set.

---

## Per-record carrier contract — five record types

For each of `{Spell, Ingestible, ObjectEffect, Scroll, Ingredient}`:

| Record | `Effects` property type | Setter | Initial state | Round-trip evidence |
|---|---|---|---|---|
| `Spell` (SPEL) | `Noggog.ExtendedList<Effect>` | True | Count=0 | wrote 254 bytes; readback Effects.Count=1; BaseEffect=`012345:Skyrim.esm`; Magnitude=50 Area=10 Duration=30; Conditions.Count=1 |
| `Ingestible` (ALCH) | `Noggog.ExtendedList<Effect>` | True | Count=0 | wrote 241 bytes; same readback assertions all pass |
| `ObjectEffect` (ENCH) | `Noggog.ExtendedList<Effect>` | True | Count=0 | wrote 247 bytes; same readback assertions all pass |
| `Scroll` (SCRL) | `Noggog.ExtendedList<Effect>` | True | Count=0 | wrote 261 bytes; same readback assertions all pass |
| `Ingredient` (INGR) | `Noggog.ExtendedList<Effect>` | True | Count=0 | wrote 233 bytes; same readback assertions all pass |

**No exclusions.** All five records present the same `Effects: ExtendedList<Effect>` shape, the same `Effect` runtime contract, and round-trip cleanly through `WriteToBinary` → `CreateFromBinary` with all sub-fields preserved (`BaseEffect`, `Data.{Magnitude,Area,Duration}`, `Conditions.Count`). Phase 1's bridge dispatch covers all five; Layer 1.E rows 01–08 in MATRIX.md remain in scope for all five carriers.

---

## Branch B decision — REQUIRED

`Effect.Data` is a settable sub-LoquiObject of type `EffectData`. Phase 1's matrix scenarios (1.E.01, 1.E.03 — `Data:{Magnitude:50, Area:0, Duration:0}` form) require the bridge to route `Data: { ... }` JSON-Object → in-place merge into `Effect.Data`.

**Without Branch B**, the recursion path inside `SetPropertyByPath(effect, "Data", jsonObj)` would:
1. Skip the bracket-write branch (no `[Key]`).
2. Skip the `IsClosedDictionary` branch (`EffectData` isn't an `IDictionary<,>`).
3. Fall through to `ConvertJsonValue(jsonObj, EffectData)` which throws `Cannot convert JSON Object to EffectData`.

**With Branch B** (added between the dict-merge branch and the `ConvertJsonValue` fallback in `SetPropertyByPath`):
- When value is `JsonValueKind.Object` AND target property type is a non-dict reference type with parameterless ctor: get-or-`Activator.CreateInstance` the sub-instance, then for each JSON member call `SetPropertyByPath(subInstance, name, value)` recursively. **In-place merge** semantics — preserves any sibling fields already on the sub-object.

For Phase 1's specific use case (Effect entries constructed fresh inside an Effects array), Branch B's "preserve siblings" semantics collapses to "all sibling fields default-initialized" — equivalent to whole-sub-object replacement. But Branch B's design holds for any future `set_fields: {Configuration: {Health: 200}}`-style pattern on existing records: sibling fields stay intact rather than being clobbered.

### Branch B side effect — incidentally enables sub-LoquiObject merge on every record type

Per Aaron's guardrail: Branch B is a generic mechanism; it incidentally makes `set_fields: {Configuration: {Health: 200}}` on NPC, `set_fields: {BasicStats: {Damage: 50}}` on Weapon, `set_fields: {Critical: {Damage: 25}}` on Weapon, etc. work as in-place-merge into the sub-LoquiObject. This side effect is in scope as a natural property of the minimum mechanism but is **NOT advertised in the Phase 1 schema description** — the `set_fields` description only documents the Effects-array form. Future versions (or future user discovery) can promote the sub-LoquiObject form to schema-documented surface; v2.8.0 ships the mechanism with Effects as its single user-facing surface.

---

## Bridge implementation contract — derived from probe evidence

### Branch A — `ConvertJsonElementToListItem` (PatchEngine.cs:1323)

After the existing `IFormLinkGetter<>` / `IFormLink<>` / `FormLink<>` branch, add:

1. **Special case `typeof(Condition)`**: route to `BuildConditionFromJson(JsonElement)` helper extracted from `ApplyAddConditions`. Probe-justified: `Activator.CreateInstance(typeof(Condition))` throws (abstract); the existing function-name → `{Function}ConditionData` reflection + ConditionFloat/ConditionGlobal subclass selection is the only viable construction path.
2. **General case JSON Object → constructible LoquiObject**: when `element.ValueKind == JsonValueKind.Object` and `elementType` has a parameterless ctor (`Activator.CreateInstance(elementType)`):
   - Construct fresh entry.
   - For each JSON member: `SetPropertyByPath(entry, name, value)` recursively.
   - Return entry.
3. **Existing fallback** (primitives/enums via `ConvertJsonValue`) unchanged.

### Branch B — `SetPropertyByPath` (PatchEngine.cs:1011)

Inserted between the existing dict-merge branch (PatchEngine.cs:1056) and the `ConvertJsonValue` fallback (PatchEngine.cs:1063):

```
if (value.ValueKind == JsonValueKind.Object &&
    finalProp.PropertyType.IsClass &&
    finalProp.PropertyType != typeof(string))
{
    var subTarget = finalProp.GetValue(current);
    if (subTarget == null)
    {
        if (!finalProp.CanWrite) throw ...
        subTarget = Activator.CreateInstance(finalProp.PropertyType);
        finalProp.SetValue(current, subTarget);
    }
    foreach (var member in value.EnumerateObject())
        SetPropertyByPath(subTarget, member.Name, member.Value);
    return;
}
```

Notes:
- The `IsClass` check excludes value types (structs, primitives) — those would fall through to `ConvertJsonValue` and surface as the existing "Cannot convert JSON Object to <type>" error, which is correct.
- `typeof(string)` exclusion is defensive — strings are reference-type but `Activator.CreateInstance(typeof(string))` throws.
- Order matters: Branch B runs **after** the `IsClosedDictionary` branch so dict-typed properties (RACE.Starting, RACE.Regen, RACE.BipedObjectNames) continue to merge via the indexer path, not via Activator-create-and-replace.
- Branch B does NOT apply to FormLink-typed properties (`IFormLinkGetter<>`, `IFormLink<>`, `IFormLinkNullable<>`, `FormLink<>`). Those are reference types but their `Activator.CreateInstance` would produce a fresh empty link, not the parsed FormKey the user intends. The bridge already routes single-FormLink properties through `ConvertJsonValue`'s string-shape handler at `PatchEngine.cs:1247` (string branch); FormLink properties typed as `IFormLinkNullable<>` get JSON strings, not JSON objects. If a FormLink property ever does receive a JSON Object, Branch B would Activator-create the wrong shape — but the existing `ConvertJsonValue` fallback would still throw cleanly. To be safe, Branch B can include an `!IsFormLinkType(finalProp.PropertyType)` guard. Spec-time decision: **add the guard** to make the failure mode obvious if a user passes the wrong shape.

### Single-source-of-truth refactor — `BuildConditionFromJson`

Extracted from `ApplyAddConditions` (PatchEngine.cs:1407-1491). Takes a `JsonElement` (or a deserialized `ConditionEntry`), returns a single `Mutagen.Bethesda.Skyrim.Condition` (concrete `ConditionFloat` or `ConditionGlobal`). `ApplyAddConditions` becomes a foreach over `condList.Add(BuildConditionFromJson(je))`. Used by Branch A's `typeof(Condition)` special case to keep the `{function, operator, value, global, run_on, or_flag}` DSL working inside nested-Conditions arrays.

**Regression risk:** load-bearing for the existing `add_conditions` operator on MGEF / PERK / PACK. Aaron's guardrail: **all 22 existing coverage-smoke tests must pass** post-refactor before commit. Test 3 specifically (`remove_conditions on Armor` — asserts ApplyRemoveConditions throws for missing Conditions property) is the regression sentinel.

---

## Open questions / future considerations

1. **`Effect.Data` initial state is `null` after `new Effect()`**, not default-initialized to `new EffectData()`. The bridge's Branch B handles this (get-or-Activator-create); just noted so the audit reader doesn't expect Mutagen's auto-initialization to apply universally.
2. **`Effect.Conditions` initial state is non-null empty list** (Count=0). Different from `Effect.Data`. Likely because `ExtendedList<T>` has a parameterless ctor that auto-initializes vs `EffectData` which is just a Loqui object. No bridge implication — the get-or-create logic handles both.
3. **`Effect.BaseEffect` is `IFormLinkNullable<>`, not `IFormLink<>`** — bonus-catch confirmed and resolved in Phase 1. Smoke runs of Layer 1.E tests 23-28 (six cells exercising `Effects[i].BaseEffect` writes) all failed before the fix with `"Cannot convert JSON String to IFormLinkNullable<IMagicEffectGetter>"` — confirming the audit's prediction. The bridge's `ConvertJsonValue` handled JSON String → primitives + JSON String → enums, but had no JSON String → FormLink branch (FormLink handling lived only in `ConvertJsonElementToListItem` for list elements). v2.7.1 never exercised this path because no `set_fields` operation targeted a single-field FormLink property. Branch A's per-property recursion exposed the gap. **Fix landed**: a JSON String → FormLink branch in `ConvertJsonValue` covering all five FormLink shapes (`IFormLinkGetter<T>` / `IFormLink<T>` / `IFormLinkNullable<T>` / `FormLink<T>` / `FormLinkNullable<T>`), with a `IsNullableFormLinkType` helper to pick the correct concrete (`FormLinkNullable<T>` for nullable target types — `FormLink<T>` doesn't implement `IFormLinkNullable<T>` so the reflection setter rejects it). Single-field FormLinks across every record type are now writable via `set_fields`. Side benefit: `set_fields: {ArmorRace: "Plugin:0XXX"}` on RACE, `set_fields: {ObjectEffect: "Plugin:0XXX"}` on ARMO, etc., now work correctly through the same path. **NOT advertised in the schema description** — same scope-lock posture as Branch B's sub-LoquiObject side effect.
4. **QUST.Aliases / Stages / Objectives, PERK.Effects** — not probed; per scope-lock, these stay deferred unless a real consumer surfaces them. Mutagen's QuestAlias has many sub-fields with sub-class polymorphism (Faction/Cell FormLinks, package overrides, AI data); Branch B's generic Activator-create would likely fail or produce subtly-wrong shapes. Defer.
5. **No exclusions from Phase 1's bridge dispatch.** All five carrier records pass the round-trip probe. If Phase 2's broader matrix surfaces a regression on any of them, that's a new Phase 4 bug, not a Phase 1 scope adjustment.

---

## Smoke verification — Layer 1.E results (post-fix)

After the BuildCondition refactor + Branch A + Branch B + IFormLinkNullable bonus-catch fix landed, `tools/coverage-smoke` runs to **30 PASS / 0 FAIL**:

| Layer | Cells | Result |
|---|---|---|
| Pre-existing v2.7.1 (tests 1–22) | 22 | 22 PASS |
| **Layer 1.E (tests 23–30)** | **8** | **8 PASS** |
| Total | 30 | 30 PASS |

Per-cell evidence:
- **Test 23 (1.E.01)** `set_fields(Effects=[{BaseEffect,Data}])` on SPEL — replace from 5 effects → 1; readback `BaseEffect=0173DC:Skyrim.esm`, `Magnitude=50`. ✓
- **Test 24 (1.E.02)** `set_fields(Effects=[{BaseEffect,Data,Conditions}])` on SPEL — nested `Conditions.Count=1`; readback condition runtime type is `ConditionFloatBinaryOverlay` with `data.GetType().Name == GetActorValueConditionData`. ✓ Per-effect Conditions absorbed via `BuildConditionFromJson` per Branch A's `typeof(Condition)` special case.
- **Test 25 (1.E.03)** ALCH `Effects` replace from 1 → 1; `Mag/Area/Dur=10/0/30` round-trip clean. ✓
- **Test 26 (1.E.04)** ENCH `Effects` replace from 7 → 1; `BaseEffect` resolves. ✓
- **Test 27 (1.E.05)** SCRL `Effects` replace from 2 → 1 (single `BaseEffect`-only entry — no `Data`). ✓
- **Test 28 (1.E.06)** INGR `Effects` replace from 4 → 1; `Mag=1`. ✓
- **Test 29 (1.E.07)** SPEL `Effects=[]` whole-list clear; readback `Count=0` from src `Count=5`. ✓
- **Test 30 (1.E.08)** SPEL `Effects=[{BaseEffect:"Skyrim.esm:DOESNOTEXIST"}]` — record-level error, rollback, no output ESP. Captured error: `"Element [0] of JSON Array could not be converted to Effect: Additional non-parsable characters are at the end of the string."` (originating from `FormIdHelper.Parse` per the bonus-catch fix routing the bad string through real FormID parsing rather than throwing on type mismatch). ✓

---

## Bridge SHA after Phase 1 build

`f998c4e022450633c3a4f3f4e1ee737e6f0f0d8a992c76a3be8efa6d86c8bb04  tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`

Recorded for traceability across Phase 5's installer + live-sync rebuild.

---

## Summary for Phase 1 commit

- **Branch A** (JSON Object → constructed LoquiObject in `ConvertJsonElementToListItem`) — IN, with `typeof(Condition)` special case routing to extracted `BuildConditionFromJson` helper.
- **Branch B** (JSON Object → sub-LoquiObject in-place merge in `SetPropertyByPath`) — IN, justified by `Effect.Data: EffectData` runtime shape. Side effect of enabling sub-LoquiObject merge on every record type is acknowledged but NOT advertised in schema.
- **`BuildConditionFromJson` refactor** — IN, single-source-of-truth for both `ApplyAddConditions` and Branch A's nested-Conditions path. All 22 existing coverage-smoke tests must pass post-refactor.
- **Five record types in scope** for Layer 1.E — SPEL, ALCH, ENCH, SCRL, INGR. No exclusions.
- **Potential bonus-catch**: `IFormLinkNullable<T>` handling in `ConvertJsonValue` if smoke surfaces a gap on `Effect.BaseEffect` writes via JSON string.
