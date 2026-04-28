# v2.9.3 Verification Matrix

**Authoritative test specification for v2.9.3's verification pass.** Mirrors v2.9.2's MATRIX.md role for v2.9.1 → v2.9.2; this matrix serves the v2.9.2 → v2.9.3 transition. Anchored on **PERK.Effects writability** (the v2.9.3 capability) — Branch A extension to `ConvertJsonElementToListItem` (`PatchEngine.cs:1441`) special-casing `typeof(APerkEffect)` to a new `BuildPerkEffectFromJson` factory near line 2331, mirroring v2.8.0's `typeof(Condition)` → `BuildConditionFromJson` route. Bridge changes are confined to: (a) one `if` arm in `ConvertJsonElementToListItem`; (b) the new factory; (c) PERK addition to v2.8.0's Effects-list carrier set. v2.9.0's per-Condition-function parameter dispatcher (`RouteParameterSlot` + `KnownParameterizedFunctions`) composes UNTOUCHED for nested `APerkEffect.Conditions[*].Conditions[*].parameters` — Phase 1.5 round-trip verified (PerkEntryPointModifyValue + GetActorValueConditionData round-tripped clean); Phase 2 reconfirms with parameterized HasPerk via probe rather than re-implementing.

**Methodology.** Every cell is one bridge invocation (Mutagen-direct functional probe in `tools/race-probe/` for Layer 1 / 1.D / 2 / 4, end-to-end MCP→bridge round-trip in `coverage-smoke/` for the regression band, `mo2_create_patch` + `mo2_record_detail` against the live Authoria modlist for Layer 3), with the listed input parameters against the listed source record(s), and a documented expected response shape. Layers 1 / 2 / 4 / 5 run via the existing test harnesses against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_create_patch` (write test patches) + `mo2_record_detail` (readback verification — no test patches retained) against the live Authoria modlist at `<live>`.

**Layer 1 vs Layer 3 disambiguation.** Layers 1 / 1.D / 2 / 4 are **bridge-mechanism verification on vanilla data** — they run in `tools/race-probe/` (Mutagen-direct functional probes) + `coverage-smoke/` (end-to-end MCP→bridge round-trips) against vanilla Skyrim.esm. The "carrier" FormID in a Layer 1 cell row (e.g. `Skyrim.esm:10FCFA` AugmentedShock60) names the vanilla source record whose binary shape the synthetic patch round-trips through; cells PASS or FAIL based on Mutagen's `CreateFromBinary` → patch → `WriteToBinary` → `CreateFromBinary` readback matching the expected payload. Layer 3 is **live workflow scenarios on the Authoria modlist** — the same vanilla FormID may appear as the canonical anchor (because Requiem-style modlists override vanilla PERK records like AugmentedShock60), but Layer 3 runs through real `mo2_create_patch` + `mo2_record_detail` MCP calls against the live install, and PASS/FAIL hinges on real-install round-trip + ESP cleanup verification. **Layer 1 = bridge correctness on vanilla data; Layer 3 = end-to-end correctness on the live Authoria install.** The FormID overlap is intentional (consistent anchor across release validations) — the test rigs and pass criteria differ.

**Synthetic-vs-vanilla-record carrier choice for Layer 1 / 2 / 4 cells.** Layer 1 / 2 / 4 cells name a vanilla FormID as the default test fixture, but Phase 2 (when actually wiring `coverage-smoke/Program.cs`) may swap to an in-memory synthetic record per cell if the synthetic simplifies the test rig (mirrors v2.9.2 P4's synthetic missing-master fixture pattern at `4.dsl.06`). The MATRIX cell spec is the test contract; the test fixture realization is Phase 2's call. **For the 4 zero-vanilla-instance leaves** (`PerkEntryPointAbsoluteValue`, `PerkEntryPointAddLeveledItem`, `PerkEntryPointAddRangeToValue`, `PerkEntryPointModifyValues` — see § Real-world frequency in APERK_EFFECTS_AUDIT.md), no vanilla anchor exists; Layer 1.P cells for these leaves use **synthetic round-trip only** per Phase 2's call.

**Record selection — final per Phase 1 audit.** Layer 1 / 2 / 4 anchor on **PERK** as the carrier record type. Phase 1's audit (see `APERK_EFFECTS_AUDIT.md`) is the authoritative source-of-truth: **12 concrete `APerkEffect` leaves enumerated, plus 1 abstract intermediate `APerkEntryPointEffect` documented**. The dominant on-disk shape is `PerkEntryPointModifyValue` (60.3% of vanilla+DLC PERK effects, AugmentedShock60-style ModSpellMagnitude). Layer 1.P ships every concrete leaf per Q2 lock = A (Aaron 2026-04-28). The PERK anchor record for Layer 1.P.PerkEntryPointModifyValue cells is **AugmentedShock60** (`Skyrim.esm:10FCFA`) — single `PerkEntryPointModifyValue` with `EntryPoint=ModSpellMagnitude`, `Modification=Multiply`, `Value=1.5`, one nested `PerkCondition` group on tab index 1 with one `GetActorValue` ConditionFloat — matches PLAN.md § Background's read-side render exemplar.

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in the appropriate phase's handoff, including the actual response payload.

**Phase fill-in cadence.** **Phase 0** laid down the layer scaffold + cell-naming convention + Layer 3 scenario use-case description (using PLAN-time provisional names). **Phase 1 (this commit)** ran the `APerkEffect` inventory probe + record-shape sweep + Phase 1.5 PEPM round-trip supplemental, landed `APERK_EFFECTS_AUDIT.md`, and updated this matrix with **confirmed leaf class names + frequency-ordered Layer 1.P expansion to 12 rows + 2-level Conditions nesting clarification + per-leaf shape annotations**. Phase 2 wires the bridge + Python schema, lands per-leaf coverage-smoke cells (Tests 426–N), and finalizes Layer 1.D error wording. Phase 3 picks live FormIDs for the Layer 3 scenario(s). Phase 4 (conditional) lands matrix corrections per surfaced bugs.

---

## 🧭 Cell-naming convention

| Prefix | Layer | Pattern | Example |
|---|---|---|---|
| `1.P.<LeafSubclass>.<sub>` | 1 — per-leaf positives | concrete `APerkEffect` leaf class + sub-shape descriptor | `1.P.PerkEntryPointModifyValue.minimal`, `1.P.PerkEntryPointModifyValue.with_perk_conditions`, `1.P.PerkAbilityEffect.basic` |
| `1.D.<NN>` | 1.D — negatives + new explicit error paths | sequential within layer | `1.D.01`, `1.D.07` |
| `2.<NN>` | 2 — combinatorial | sequential | `2.01`, `2.04` |
| `3.<N>` | 3 — workflow scenarios | scenario number | `3.1`, `3.2` |
| `4.<sub>.<NN>` | 4 — edges | sub-grouping + sequential | `4.dsl.01`, `4.dsl.05` |
| `5.range` | 5 — regression | range row, mapped to v2.9.2's 425 cells | `5.range` |

The `1.P.<LeafSubclass>.<sub>` form anchors on **concrete leaf class** (the v2.9.3 unit of work — one branch in the discriminator-routed factory dispatched over 12 leaves). `1.D.<NN>` carries v2.9.2's negative-band convention forward. Layer 4's only sub-grouping is `dsl` (parameter-value-form edges + DSL-shape edges).

**Per-leaf carrier convention (Phase 1 confirmed).** The 12 concrete leaves are (alphabetical): `PerkAbilityEffect`, `PerkEntryPointAbsoluteValue`, `PerkEntryPointAddActivateChoice`, `PerkEntryPointAddLeveledItem`, `PerkEntryPointAddRangeToValue`, `PerkEntryPointModifyActorValue`, `PerkEntryPointModifyValue`, `PerkEntryPointModifyValues`, `PerkEntryPointSelectSpell`, `PerkEntryPointSelectText`, `PerkEntryPointSetText`, `PerkQuestEffect`. Layer 1.P below orders rows by descending vanilla+DLC frequency (8 with vanilla data; 4 with zero-instance, marked `[no vanilla anchor — synthetic round-trip only]`).

**Source-of-truth for leaf names + property surfaces (Mutagen 0.53.1):** `APERK_EFFECTS_AUDIT.md` § Per-subclass property surface. Probe scratch: `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt` + `<workspace>/scratch/v2.9.3-phase-1-perk-inventory-pepm-rt.txt`.

**Two-level `Conditions` nesting in DSL (Q7 confirmed shape).** `Conditions` appears at TWO different depths in the write payload:
- OUTER: `APerkEffect.Conditions: ExtendedList<PerkCondition>` (the wrapper-list field on the abstract base, declared on every concrete leaf via inheritance).
- INNER: `PerkCondition.Conditions: ExtendedList<Condition>` (the actual condition entries, routed via Branch A's `typeof(Condition)` special case + v2.9.0 dispatcher).

```jsonc
{
  "type": "PerkEntryPointModifyValue",
  "EntryPoint": "ModSpellMagnitude",
  "Modification": "Multiply",
  "Value": 1.5,
  "Conditions": [                                  // OUTER
    {
      "RunOnTabIndex": 1,
      "Conditions": [                              // INNER
        { "function": "HasPerk", "operator": "==", "value": 1, "parameters": { "Perk": "Skyrim.esm:058200" } }
      ]
    }
  ]
}
```

Phase 2 schema description for `set_fields: {Effects: [...]}` on PERK must call out the 2-level nesting + the 12 valid `type:` discriminator values explicitly.

---

## Layer 1 — Per-leaf coverage (positives)

**v2.9.3 in-scope leaves (Phase 1 final, Q2 = A locked):** all 12. Layer 1 cells exercise each leaf's primary success path on canonical PERK records ordered by descending vanilla+DLC frequency. Per-leaf + DSL combinatorial composition lives in Layer 2.

Each row's expected result follows the shape:

> `mo2_create_patch` → bridge response top-level `success: true`; readback via `mo2_record_detail` confirms PERK record's `Effects` array contains the requested concrete-leaf entries with the requested property values; v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 single-record / batch / projection / expansion paths bit-identical when only `set_fields: {Effects: [...]}` is supplied.

### 1.P.PerkEntryPointModifyValue — dominant on-disk shape (60.3% of vanilla+DLC effects)

Anchor: `Skyrim.esm:10FCFA` (AugmentedShock60). Subclass-specific property surface: `EntryPoint: APerkEntryPointEffect+EntryType` (91-member enum), `Modification: PerkEntryPointModifyValue+ModificationType` (3 members: `Set`, `Add`, `Multiply`), `PerkConditionTabCount: Byte`, `Value: Nullable<Single>`.

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointModifyValue.minimal` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 0, Rank: 0, Priority: 0}]}` (no nested Conditions) | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointModifyValue`; `EntryPoint = ModSpellMagnitude`, `Modification = Multiply`, `Value = 1.4`, outer `Conditions.Count = 0`. Verifies factory dispatches to concrete leaf + Activator-creates + per-property recursion sets scalar + enum + Nullable<Single> fields correctly. |
| `1.P.PerkEntryPointModifyValue.with_perk_conditions` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, Conditions: [{RunOnTabIndex: 1, Conditions: [{function: "GetActorValue", operator: ">=", value: 60, parameters: {ActorValue: "Destruction"}}]}]}]}` | symmetric to `.minimal`; additionally readback confirms outer `Conditions.Count = 1`, entry's `RunOnTabIndex = 1`, inner `Conditions.Count = 1`, condition's `Data.Function = GetActorValue` + `ComparisonValue = 60` + `CompareOperator = GreaterThanOrEqualTo` + `Data.ActorValue = Destruction`. Verifies wrapper-object DSL per Q7, nested `BuildConditionFromJson` route via Branch A's `typeof(Condition)` special case, v2.9.0's `RouteParameterSlot` enum-slot dispatch on `ActorValue`. **Phase 1.5 PEPM round-trip already confirmed structurally.** |
| `1.P.PerkEntryPointModifyValue.with_v290_params` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, Conditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", operator: "==", value: 1, parameters: {Perk: "Skyrim.esm:058200"}}]}]}]}` | symmetric to `.with_perk_conditions`; the inner condition uses a v2.9.0-dispatcher-routed parameter (`HasPerk` + `parameters: {Perk: <FormLink>}`); readback confirms condition's `Data.Reference` resolves to the supplied perk FormLink. **Composition probe** — verifies v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` compose UNTOUCHED for nested PerkCondition Conditions per Q4. |

### 1.P.PerkEntryPointSelectSpell — second-most-common (22.7%)

Anchor: Phase 2 picks vanilla PERK with PerkEntryPointSelectSpell entry (45 records candidates per § Real-world frequency). Subclass-specific surface: `EntryPoint`, `PerkConditionTabCount`, `Spell: IFormLink<ISpellGetter>` (NOT nullable).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointSelectSpell.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkEntryPointSelectSpell", EntryPoint: "<entry-point>", Spell: "Skyrim.esm:0CF788"}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointSelectSpell`; `Spell` resolves to the supplied SPEL FormLink. Verifies plain-IFormLink set on a leaf with no Modification/Value enum slots. |

### 1.P.PerkEntryPointModifyActorValue — third (6.4%)

Anchor: Phase 2 picks vanilla PERK (3 records candidates). Subclass-specific surface: `ActorValue: ActorValue` (enum), `EntryPoint`, `Modification: PerkEntryPointModifyActorValue+ModificationType` (per-class enum, distinct from PerkEntryPointModifyValue's), `PerkConditionTabCount`, `Value: Single` (plain, NOT nullable).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointModifyActorValue.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkEntryPointModifyActorValue", EntryPoint: "<entry-point>", ActorValue: "Destruction", Modification: "<modification>", Value: 1.5}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointModifyActorValue`; `ActorValue = Destruction`; `Value = 1.5` (plain Single); per-class `Modification` enum value matches. Verifies multi-enum leaf + plain-Single Value path. |

### 1.P.PerkAbilityEffect — fourth (4.0%)

Anchor: Phase 2 picks vanilla PERK with PerkAbilityEffect (34 record candidates; Werewolf/Vampire perk family per APERK_EFFECTS_AUDIT.md). Subclass-specific surface: `Ability: IFormLink<ISpellGetter>`.

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkAbilityEffect.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkAbilityEffect", Ability: "Skyrim.esm:0CF788"}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkAbilityEffect`; `Ability` resolves to the supplied SPEL FormLink. Verifies factory dispatches to a non-EntryPoint-family leaf (single subclass-specific FormLink slot, no inherited APerkEntryPointEffect props). |

### 1.P.PerkQuestEffect — fifth (3.3%)

Anchor: Phase 2 picks vanilla PERK with PerkQuestEffect (28 record candidates). Subclass-specific surface: `Quest: IFormLink<IQuestGetter>`, `Stage: Byte` (NOT Int32), `Unknown: MemorySlice<Byte>` (opaque blob — not write-target).

**Phase 1 round-trip evidence:** PASS — synthetic `PerkQuestEffect` with `Quest = Skyrim.esm:000200`, `Stage = 100` round-tripped via WriteToBinary → CreateFromBinary clean (197-byte ESP).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkQuestEffect.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkQuestEffect", Quest: "Skyrim.esm:<quest-formid>", Stage: 100}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkQuestEffect`; `Quest` resolves to the supplied QUST FormLink; `Stage = 100` (Byte conversion via `Convert.ChangeType` from JSON Int32). Verifies Byte-typed Stage + per-leaf FormLink-to-QUST + opaque `Unknown` MemorySlice handling (not asserted writable). |

### 1.P.PerkEntryPointAddActivateChoice — sixth (2.5%)

Anchor: Phase 2 picks vanilla PERK (15 record candidates). Subclass-specific surface: `EntryPoint`, `PerkConditionTabCount`, `Spell: IFormLinkNullable<ISpellGetter>` (NOTE: nullable, distinct from PerkEntryPointSelectSpell's non-nullable Spell).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointAddActivateChoice.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkEntryPointAddActivateChoice", EntryPoint: "Activate", Spell: "Skyrim.esm:0CF788"}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointAddActivateChoice`; nullable `Spell` resolves correctly. Verifies `IFormLinkNullable<T>` write contract distinct from PerkEntryPointSelectSpell's plain `IFormLink<T>`. |

### 1.P.PerkEntryPointSetText — seventh (0.5%)

Anchor: Phase 2 picks vanilla PERK (4 record candidates). Subclass-specific surface: `EntryPoint`, `PerkConditionTabCount`, `Text: TranslatedString` (Sub-Loqui — different write contract from PerkEntryPointSelectText's plain String).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointSetText.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkEntryPointSetText", EntryPoint: "<entry-point>", Text: "<some-string>"}]}` (Phase 2 picks JSON-string-to-TranslatedString convenience path or sub-LoquiObject Branch B merge) | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointSetText`; `Text` (TranslatedString) round-trips. Verifies Sub-Loqui write contract on a non-Effect-Data Sub-Loqui slot. |

### 1.P.PerkEntryPointSelectText — eighth (0.4%)

Anchor: Phase 2 picks vanilla PERK (3 record candidates). Subclass-specific surface: `EntryPoint`, `PerkConditionTabCount`, `Text: System.String` (plain, distinct from PerkEntryPointSetText's TranslatedString).

| # | Carrier | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointSelectText.basic` | (Phase 2 picks vanilla anchor) | `set_fields: {Effects: [{type: "PerkEntryPointSelectText", EntryPoint: "<entry-point>", Text: "<some-string>"}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointSelectText`; `Text` (plain String) round-trips. Verifies plain-String contract — distinct from cousin SetText's TranslatedString. |

### Zero-vanilla-instance leaves (Q2 = A ship-full-set; synthetic-only)

**4 leaves with zero vanilla+DLC instances.** No vanilla anchor exists for these in Skyrim/DLC ESMs (modders may use). Per the synthetic-vs-vanilla preamble, Layer 1.P cells use **synthetic round-trip only** — Phase 2 builds an in-memory PERK fixture per cell.

| # | Leaf | Operation | Expected |
|---|---------|-----------|----------|
| `1.P.PerkEntryPointAbsoluteValue.basic` | `PerkEntryPointAbsoluteValue` | `set_fields: {Effects: [{type: "PerkEntryPointAbsoluteValue", EntryPoint: "<entry-point>", Negative: false}]}` against synthetic PERK | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointAbsoluteValue`; `Negative = false`. Verifies Boolean leaf surface. |
| `1.P.PerkEntryPointAddLeveledItem.basic` | `PerkEntryPointAddLeveledItem` | `set_fields: {Effects: [{type: "PerkEntryPointAddLeveledItem", EntryPoint: "<entry-point>", Item: "Skyrim.esm:<lvli-formid>"}]}` against synthetic PERK | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointAddLeveledItem`; `Item` resolves to LVLI FormLink. Verifies LVLI-typed FormLink slot. |
| `1.P.PerkEntryPointAddRangeToValue.basic` | `PerkEntryPointAddRangeToValue` | `set_fields: {Effects: [{type: "PerkEntryPointAddRangeToValue", EntryPoint: "<entry-point>", From: 0.5, To: 1.5}]}` against synthetic PERK | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointAddRangeToValue`; `From = 0.5`, `To = 1.5`. Verifies range-shape (two Single slots, distinct from per-Modification single-Value pattern). |
| `1.P.PerkEntryPointModifyValues.basic` | `PerkEntryPointModifyValues` | `set_fields: {Effects: [{type: "PerkEntryPointModifyValues", EntryPoint: "<entry-point>", Modification: "Multiply", Value: 1.5, Value2: 0.8}]}` against synthetic PERK | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointModifyValues`; `Value = 1.5`, `Value2 = 0.8` (both Nullable<Single>). Verifies dual-Value Nullable<Single> shape (distinct from sibling PerkEntryPointModifyValue's single Value). |

---

## Layer 1.D — Negatives + new explicit error paths

Seven cells exercising the new discriminator validation surface from PLAN § A factory + § B Q1 lock + the existing v2.8.0 carrier-set rejection path. Multi-error accumulation is **not** a structural contract here (single-record set_fields is one bridge invocation; first-failure-wins is acceptable per v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 precedent on Branch A factories). Wording for the new error messages is finalized in Phase 2 implementation; this matrix locks the shape.

| # | Axis | Setup | Expected |
|---|------|-------|----------|
| `1.D.01` | discriminator unknown | `set_fields: {Effects: [{type: "BogusType", EntryPoint: "ModSpellMagnitude"}]}` against PERK | top-level `success: false`; per-record `error: "PerkEffect type 'BogusType' not found in Mutagen.Bethesda.Skyrim namespace"` (or equivalent — Phase 2 finalizes wording); response names the 12 valid concrete leaf names; rollback: source PERK's `Effects` is unchanged on disk |
| `1.D.02` | discriminator is abstract base or intermediate | `set_fields: {Effects: [{type: "APerkEffect", ...}]}` OR `{type: "APerkEntryPointEffect", ...}` against PERK | top-level `success: false`; per-record `error: "PerkEffect type 'APerkEffect' is abstract — must specify a concrete leaf subclass"` (or equivalent); covers BOTH abstract types (base + intermediate) per third-level polymorphism; response names the 12 valid concrete leaves; rollback identical |
| `1.D.03` | missing discriminator | `set_fields: {Effects: [{EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}]}` against PERK (no `type:` key) | top-level `success: false`; per-record `error: "PerkEffect entry missing required 'type' discriminator field"` (or equivalent); response names the 12 valid concrete leaves; rollback identical |
| `1.D.04` | nested condition v2.9.0-out-of-scope | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, Conditions: [{RunOnTabIndex: 1, Conditions: [{function: "<v2.9.0-not-yet-wired-fn>", parameters: {SomeSlot: "x"}}]}]}]}` against PERK (Phase 2 picks an actual v2.9.0-out-of-scope function) | top-level `success: false`; per-record error: v2.9.0's existing "function not yet wired" error from `RouteParameterSlot`. Verifies composition unchanged: v2.9.3's nested Conditions route to the same v2.9.0 dispatcher; v2.9.0's coverage gap surfaces in v2.9.3-context the same way. Rollback identical |
| `1.D.05` | unknown property on PerkEffect | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", BogusField: "x"}]}` against PERK | top-level `success: false`; per-record error from `SetPropertyByPath` "unknown property 'BogusField' on PerkEntryPointModifyValue" (existing Branch B / Tier C-shape error path, unchanged by v2.9.3). Rollback identical |
| `1.D.06` | non-carrier record | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", ...}]}` against an NPC_ record (a record type that is neither in v2.8.0's carrier set NOR PERK) | top-level `success: false`; per-record error: existing v2.8.0 carrier-rejection ("Effects array not supported on record type 'NPC_' — supported: SPEL, ALCH, ENCH, SCRL, INGR, PERK"). Verifies v2.9.3's PERK addition didn't break the gating on other record types. Rollback identical |
| `1.D.07` | type mismatch on non-PERK record (defensive) | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue"}]}` against a SPEL record (where Effects-list is gated to concrete `Effect`, not `APerkEffect`) | top-level `success: false`; per-record error: would not normally reach the discriminator path because the SPEL Effects-list is already routed via the v2.8.0 concrete `Effect` Activator path (no discriminator needed); the `type:` key gets treated as an unknown property on `Effect` and surfaces as a `SetPropertyByPath` error. Cell verifies the gating remains correct — supplying `type:` on a v2.8.0 carrier doesn't accidentally route to `BuildPerkEffectFromJson`. Rollback identical |

Phase 2 may add Layer 1.D rows programmatically if test patterns surface (e.g. a per-leaf missing-required-field variant per audit; or a non-FormLink type-coercion error if Phase 2 implementation surfaces one; or `1.D.<NN>.unknown_blob` for `PerkQuestEffect.Unknown` MemorySlice write rejection). The matrix locks the structural error-path coverage above; bulk-pattern derivatives are implementation choice for the harness.

---

## Layer 2 — Combinatorial probes

Cross-leaf composition: heterogeneous concrete leaves in one Effects array, plus replace-semantics composition with PERK top-level scalar writes, plus full-stack composition (Branch A → factory → wrapper → Condition factory → v2.9.0 dispatcher), plus empty-array clear.

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | heterogeneous leaves in one array | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}, {type: "PerkAbilityEffect", Ability: "Skyrim.esm:<spel>"}, {type: "PerkQuestEffect", Quest: "Skyrim.esm:<qust>", Stage: 100}]}` against a PERK record (Phase 1 confirmed PlayerWerewolfFeed family carries multi-leaf; Phase 2 confirms specific anchor) | top-level `success: true`; readback confirms `Effects.Count = 3`; entry-by-entry concrete types match the requested discriminators (`PerkEntryPointModifyValue`, `PerkAbilityEffect`, `PerkQuestEffect`); per-entry property values match. Verifies the factory dispatches per-element correctly + the resulting `ExtendedList<APerkEffect>` carries heterogeneous concrete leaves (Mutagen's polymorphism preserves per-entry types) |
| `2.02` | replace-semantics + Tier C scalar coexistence | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}], Level: 25, NumRanks: 3, Trait: true}` against `Skyrim.esm:10FCFA` (AugmentedShock60) | top-level `success: true`; readback confirms `Effects.Count = 1` (replaced) AND PERK top-level scalars `Level = 25`, `NumRanks = 3`, `Trait = true` set correctly. Verifies v2.9.3's Effects-array replace-semantics composes with v2.7.x's Tier C scalar set_fields path on the same record. Sibling fields outside the supplied set_fields keys (e.g. PERK.Description, PERK.Conditions, PERK.PerkSection) are preserved (Branch B in-place merge invariant for the top-level dict, Effects-array replace-semantics for Effects only) |
| `2.03` | full-stack composition (Branch A → factory → wrapper → Condition factory → v2.9.0 dispatcher) | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, Conditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", operator: "==", value: 1, parameters: {Perk: "Skyrim.esm:058200"}}]}]}]}` against `Skyrim.esm:10FCFA` (note: TWO `Conditions` keys — outer APerkEffect.Conditions, inner PerkCondition.Conditions) | top-level `success: true`; readback confirms full-stack write: `Effects[0]` is `PerkEntryPointModifyValue` with `Value = 1.4` AND outer `Conditions[0]` is concrete `PerkCondition` wrapper with `RunOnTabIndex = 1` AND inner `Conditions[0]` is concrete `Condition` (v2.8.0 BuildConditionFromJson route) with `Data.Function = HasPerk` AND `Data.Reference` resolves to the supplied perk FormLink (v2.9.0 RouteParameterSlot route). **Single cell exercising every layer of the v2.8.0 + v2.9.0 + v2.9.3 composed write surface, with explicit 2-level Conditions nesting.** |
| `2.04` | empty-array clear | `set_fields: {Effects: []}` against `Skyrim.esm:10FCFA` (AugmentedShock60) | top-level `success: true`; readback confirms `Effects.Count = 0`. Verifies v2.9.3's PERK Effects-array replace-semantics matches v2.8.0's empty-clear posture (Test 29 / 1.E.07) on the existing carrier set. PERK top-level scalar fields (Level, NumRanks, Trait, Description, Conditions, PerkSection) are preserved untouched (sibling-preservation invariant) |

---

## Layer 3 — Workflow scenario(s) on live install

Run via `mo2_create_patch` (write test patches) + `mo2_record_detail` (readback verification) against the live Authoria modlist. Output: per-scenario assertion table + readback evidence in `PHASE_3_HANDOFF.md`. Test ESPs go to `<modlist>/mods/Claude Output/`, deleted post-verification per `Claude_MO2/CLAUDE.md` § live install sync.

**Phase 0 pre-spec'd use case + assertions; Phase 3 picks live FormIDs at execution time.** Aaron may swap the named record during Phase 3 if Phase 1's audit + the live Authoria modlist surface a better Authoria fit.

### Scenario 3.1 — Requiem-style PERK magnitude rebalance (mandatory)

**Use case.** Real-world AI-driven patcher: an Authoria tester rebalancing Requiem's perk magnitudes — change `ModSpellMagnitude` from 1.5× to 1.4× on AugmentedShock60 (`Skyrim.esm:10FCFA`), AugmentedFrost60, AugmentedFlames60, etc. Today: blocked — `set_fields: {Effects: [...]}` against PERK is rejected by the v2.8.0 carrier-set check. v2.9.3 unblocks: a single `mo2_create_patch` call with `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, Conditions: [<existing tabs preserved>]}]}` rewrites the Effects array with the new magnitude. The Conditions structure (RunOnTabIndex tabs + nested Conditions per tab) is read first via `mo2_record_detail` (v2.9.2 mechanism) and round-tripped into the write payload.

**Target (Phase 3 picks at execution):**
- **Anchor record:** AugmentedShock60 (`Skyrim.esm:10FCFA`) — single `PerkEntryPointModifyValue` with `EntryPoint = ModSpellMagnitude`, `Modification = Multiply`, `Value = 1.5`, one nested `PerkCondition` group on tab index 1 with one `GetActorValue` ConditionFloat. Phase 3 confirms shape via pre-write `mo2_record_detail`.
- **Override target:** patch ESP at `<modlist>/mods/Claude Output/v2.9.3-test-perk-rebalance.esp` (or analogous; deleted post-verification).

**Operations:**
- Pre-write read: `mo2_record_detail(formid: "Skyrim.esm:10FCFA", expand_links: ["Effects"])`.
- Write: `mo2_create_patch(plugin: "v2.9.3-test-perk-rebalance.esp", overrides: [{formid: "Skyrim.esm:10FCFA", set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, <Conditions: [...] from pre-write read, preserved>}]}}])`.
- Post-write readback: `mo2_record_detail(formid: "<patch-esp>:10FCFA", expand_links: ["Effects"])`.

**Assertions:**
- Pre-write read returns `Effects.Count = 1`, `Effects[0].EntryPoint = ModSpellMagnitude`, `Effects[0].Value = 1.5` (or live actual — Phase 3 documents).
- Write call returns top-level `success: true`; per-record success: true.
- Post-write readback confirms patch ESP carries an override for the PERK with `Effects.Count = 1`, `Effects[0]` concrete type = `PerkEntryPointModifyValue`, `EntryPoint = ModSpellMagnitude`, `Value = 1.4`, outer `Conditions` round-trip-identical to pre-write.
- All v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 single-record / batch / projection / expansion paths bit-identical when called against any non-PERK record — Phase 3 spot-checks one representative call against an Authoria SPEL or RACE without `Effects` writes.
- Test ESP cleanup: post-verification, the patch ESP is deleted; the live install state is restored.

### Scenario 3.2 — Optional secondary scenario: multi-effect PERK preserving leaf mix

**Use case.** A symmetric scenario exercising the heterogeneous-leaf composition on a real PERK. PlayerWerewolfFeed (`Skyrim.esm:02BA1D`, or analogous Werewolf/Vampire-style perk) is the canonical anchor — these perks carry a mix of `PerkEntryPointModifyValue` (entries modifying Activate or other entry-points) + `PerkAbilityEffect` (granted spells/abilities) + occasionally `PerkQuestEffect` entries. The patcher rewrites the Effects array preserving the leaf mix but adjusting one entry's property.

**Phase 3 conditional execution.** Phase 3 confirms the picked anchor actually carries a multi-leaf Effects array via pre-write `mo2_record_detail`. If the picked anchor only carries one leaf type, Phase 3 picks a different multi-leaf PERK from the live modlist or documents the skip with reason.

**Operations:**
- Pre-write read: `mo2_record_detail(formid: "Skyrim.esm:02BA1D", expand_links: ["Effects"])`.
- Write: `mo2_create_patch(plugin: "v2.9.3-test-perk-multileaf.esp", overrides: [{formid: "Skyrim.esm:02BA1D", set_fields: {Effects: [<round-trip array with one entry's property adjusted>]}}])`.
- Post-write readback: `mo2_record_detail(formid: "<patch-esp>:02BA1D", expand_links: ["Effects"])`.

**Assertions:**
- Symmetric to Scenario 3.1: per-leaf round-trip preservation across the heterogeneous Effects array.
- Specifically verifies factory dispatches per-element on a real-world PERK (not a synthetic test fixture); the resulting `ExtendedList<APerkEffect>` carries the same concrete-leaf mix as pre-write, with the adjusted entry's property reflecting the write payload.
- Test ESP cleanup: post-verification, the patch ESP is deleted.

---

## Layer 4 — Edges

DSL-form edges of the new `Effects: [...]` payload's value forms + cross-master FormLink composition + sibling-preservation invariants. v2.9.3's mechanism doesn't change v2.8.0's per-Effects-list dispatch on the existing 5 carriers, v2.9.0's per-Condition-function dispatch, v2.9.1's QUST condition disambiguation, or v2.9.2's read-side mechanism — those surfaces stay exercised via Layer 5 regression. v2.9.3's new edges are PERK-specific factory + DSL + cross-master + sibling.

### 4.dsl — DSL-shape + cross-master + sibling edges

| # | Setup | Expected |
|---|-------|----------|
| `4.dsl.01` | Round-trip: write Effects via `set_fields: {Effects: [...]}`, then read back via `mo2_record_detail` against the same patch ESP | response payload shape matches v2.9.2's read-side render exactly: `Effects` is a list of dict entries, each entry's keys match the concrete leaf's property surface (e.g. `PerkEntryPointModifyValue` entries carry `EntryPoint` + `Modification` + `Value` + `Conditions` + `PerkConditionTabCount` + `Rank` + `Priority` + `Flags`; `PerkAbilityEffect` entries carry `Ability`; `PerkQuestEffect` entries carry `Quest` + `Stage`). Verifies the v2.9.3 write path produces a record whose v2.9.2 read-side render is identical to a vanilla PERK with the same effect shape — write/read symmetry invariant |
| `4.dsl.02` | Cross-master FormLink in nested condition (`parameters: {Perk: "OtherEsp:01ABCD"}` where `OtherEsp.esp` is a synthetic master with a known Perk FormLink) | top-level `success: true`; readback confirms the nested condition's `Data.Reference` carries a compacted FormLink (v2.6.0's `WithLoadOrder` writes ESL-flagged-master-aware compacted FormIDs). Verifies v2.6.0's ESL-flagged FormLink writability composes with v2.9.3's new nested-condition write path. **Phase 2 builds synthetic two-plugin fixture if one isn't already in coverage-smoke** (mirrors v2.9.2 P4's synthetic missing-master fixture pattern at `4.dsl.06`) |
| `4.dsl.03` | Enum parse error on EntryPoint | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "BogusEntryPoint", Modification: "Multiply", Value: 1.4}]}` against `Skyrim.esm:10FCFA` | top-level `success: false`; per-record error from v2.9.0's enum branch in `RouteParameterSlot` / Branch B's enum-set: "EntryPoint value 'BogusEntryPoint' is not a valid APerkEntryPointEffect+EntryType — valid values: ModSpellMagnitude, ModBowDamage, …" (existing code path; verifies enum dispatch works on `APerkEntryPointEffect+EntryType` same as on `ConditionData` enum slots; 91 valid members per Phase 1.5 dump). Rollback identical |
| `4.dsl.04` | Empty Conditions list | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, Conditions: []}]}` against `Skyrim.esm:10FCFA` | top-level `success: true`; readback confirms outer `Effects[0].Conditions.Count = 0`. Verifies non-failure on empty outer wrapper-list (mirrors v2.8.0 Test 29 empty-array clear, scoped to the outer Conditions list on APerkEffect) |
| `4.dsl.05` | Sibling preservation invariant | `set_fields: {Effects: [{type: "PerkEntryPointModifyValue", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}]}` against `Skyrim.esm:10FCFA` (no Description / NumRanks / Level / PerkSection write) | top-level `success: true`; readback confirms `Effects` was REPLACED (v2.9.3 replace-semantics on the array); ALSO confirms PERK top-level Description, NumRanks, Level, PerkSection sibling fields are UNTOUCHED (carry through from the source record). Verifies array-replace-semantics doesn't bleed into top-level scalars; Branch B in-place merge invariant on the dict + Effects-array replace-semantics on Effects only |

---

## Layer 5 — Regression band

All v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 coverage-smoke cells run unchanged. v2.9.3 must not regress any prior behavior — the new PERK Effects-array path is purely additive (PERK was previously rejected by the v2.8.0 carrier-set check; v2.9.3 adds it to the carrier set).

| Cell range | Source | Expected |
|---|---|---|
| `5.range` | `dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` Layer 1.P + 1.D + 2 + 4 + 5 (**425 v2.9.2 cells confirmed via coverage-smoke** post-Phase-4) | each cell PASS as it did in v2.9.2 P5; **Phase 2 added 30 new v2.9.3 cells (Tests 426–455: 14 Layer 1.P + 7 Layer 1.D + 4 Layer 2 + 5 Layer 4)** — final post-Phase-2 total = **425 + 30 = 455 cells, ALL PASS or documented SKIP**. The +2 over the original 28-cell minimum target are explicit Layer 4 rows landed (4.dsl.02 cross-master synthetic two-plugin fixture + 4.dsl.05 sibling preservation), accepted by conductor as matrix-completion. |

Specifically: every v2.8.0 `set_fields: {Effects: [...]}` invocation pattern on SPEL/ALCH/ENCH/SCRL/INGR stays bit-identical (the pre-existing carrier set is unaffected by adding PERK to it; the discriminator `type:` key is absent on those carriers, so Branch B's existing concrete-`Effect` Activator path stays untouched). Every v2.9.0 condition-parameter dispatcher pattern stays untouched. Every v2.9.1 QUST condition disambiguation pattern stays untouched. Every v2.9.2 read-side mechanism (`formids` / `fields` / `expand_links`) on `mo2_record_detail` stays bit-identical. This is the core back-compat assertion of v2.9.3.

---

## Total assertion count (Phase 1 final; Phase 2 confirms post-implementation)

**v2.9.3 capability surface is one Branch A factory + 12 concrete leaves (Q2 = A locked Aaron 2026-04-28).** No new operator, no new tool, no new bridge command.

| Layer | Matrix rows (Phase 1 final) | Harness cells (Phase 1 final) | Source |
|---|---:|---:|---|
| 1.P (per-leaf positives) | 12 (3 PerkEntryPointModifyValue sub-shapes + 1 each for SelectSpell / ModifyActorValue / PerkAbilityEffect / PerkQuestEffect / AddActivateChoice / SetText / SelectText + 4 zero-vanilla synthetic-only) | ≥14 (3 PEPM sub-shapes + 8 single-shape leaves + 4 zero-instance synthetic) | this doc |
| 1.D (negatives + new explicit error paths) | 7 | 7 | this doc |
| 2 (combinatorial) | 4 | 4 | this doc |
| 3 (workflow scenarios) | 1 mandatory + 1 conditional (3.1 Requiem perk rebalance, 3.2 multi-leaf PERK — Phase 3 confirms preconditions) | ~6–10 assertions | this doc; Phase 3 picks live FormIDs |
| 4.dsl | 5 | 5 | this doc |
| 5 (regression) | 1 (range row) | 425 (v2.9.2 baseline) + 30 (v2.9.3 P2 new) = 455 | Phase 2 final |
| **Total** | **~30 matrix rows** | **~460 harness cells** | — |

Phase 2 may dedupe or merge cells where the same code path is exercised twice. v2.9.2's MATRIX.md is the source of truth for the Layer 5 regression count; Phase 2 reads from `coverage-smoke/Program.cs`'s actual cell enumeration rather than from this matrix doc when running the full regression band.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.9.2:

```
[1.P.PerkEntryPointModifyValue.minimal]              mo2_create_patch PERK Effects=[PerkEntryPointModifyValue]    PASS (1 effect; concrete type matches; Value=1.4)
[1.P.PerkEntryPointModifyValue.with_perk_conditions] mo2_create_patch PERK Effects + nested Conditions  PASS (1 effect; 1 PerkCondition tab; 1 inner Condition; v2.9.0 enum-slot dispatch routed)
[1.P.PerkEntryPointModifyValue.with_v290_params]     mo2_create_patch PERK Effects + nested HasPerk param   PASS (composition probe; v2.9.0 RouteParameterSlot routed FormLink)
[1.P.PerkAbilityEffect.basic]                  mo2_create_patch PERK Effects=[PerkAbilityEffect]            PASS (1 effect; concrete type matches; Ability FormLink resolved)
[1.P.PerkQuestEffect.basic]              mo2_create_patch PERK Effects=[PerkQuestEffect]        PASS (1 effect; concrete type matches; Quest FormLink + Stage Byte)
[1.D.01]                                 mo2_create_patch type='BogusType'                       PASS (discriminator-not-found error; rolled back)
[1.D.02]                                 mo2_create_patch type='APerkEffect' (abstract)          PASS (abstract-base rejection; rolled back; covers APerkEntryPointEffect intermediate too)
[1.D.03]                                 mo2_create_patch missing 'type' key                     PASS (missing-discriminator error; rolled back)
[1.D.06]                                 mo2_create_patch Effects on NPC_                        PASS (carrier-rejection error preserved; rolled back)
[2.01]                                   heterogeneous leaves in one Effects array               PASS (3 effects; concrete-type-per-element preserved)
[2.03]                                   full-stack composition (Branch A → factory → Condition factory → v2.9.0)  PASS (every layer composed; 2-level Conditions nesting verified)
[2.04]                                   Effects: [] empty-clear on PERK                         PASS (Effects.Count=0; sibling fields preserved)
[3.1]                                    live: Requiem perk magnitude rebalance (AugmentedShock60)  PASS (1.5 → 1.4; readback matches; ESP cleanup verified)
[4.dsl.01]                               write/read symmetry round-trip                          PASS (v2.9.2 read-side render matches vanilla shape)
[4.dsl.05]                               sibling preservation invariant                          PASS (Effects replaced; top-level scalars untouched)
[5.range]                                v2.9.2 regression band                                  ~425/~425 PASS
```

Failures embed enough context for handoff to lift into the bug list directly. Per-cell PASS/FAIL is the harness contract; per-leaf assertions are inlined in each Layer 1 / 2 cell's PASS string.

---

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a record meeting the test fixture requirements (e.g. anchor needs PERK with a populated `PerkAbilityEffect` effect entry and the picked anchor lacks one), the harness prints:

```
[1.P.<LeafSubclass>.<sub>]  PERK Effects <none-meeting-fixture>  SKIP: anchor PERK lacks <LeafSubclass> entry populated
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture (build a synthetic PERK in-memory via Mutagen) or accept the gap. **The 4 zero-vanilla-instance leaves** (PerkEntryPointAbsoluteValue, PerkEntryPointAddLeveledItem, PerkEntryPointAddRangeToValue, PerkEntryPointModifyValues) **always require synthetic fixtures** per the Real-world frequency table in APERK_EFFECTS_AUDIT.md — Phase 2 builds these per cell.

---

## Phase fill-in checklist (Phase 1 hand-back) — COMPLETE

Phase 1 closed with these MATRIX edits landed (this commit):

- [x] **Concrete `APerkEffect` leaf enumeration** — Phase 1's race-probe extension produced the authoritative list of 12 concrete leaves + 1 abstract intermediate. Documented in APERK_EFFECTS_AUDIT.md § Inventory totals.
- [x] **Activator constructibility per leaf** — confirmed each concrete leaf has a parameterless ctor that Activator can construct; abstract base + intermediate both correctly fail. Documented in APERK_EFFECTS_AUDIT.md § Constructibility.
- [x] **Per-leaf property surface dump** — each leaf's full property list with `[base]` / `[subclass-specific]` annotation + ShapeTag per slot. Documented in APERK_EFFECTS_AUDIT.md § Per-subclass property surface.
- [x] **Outer `Conditions` element type confirmation** — confirmed `APerkEffect.Conditions` element type is concrete `PerkCondition` LoquiObject (Q7 lock holds; field name corrected from PLAN's `PerkConditions`). Documented.
- [x] **PERK anchor for Layer 1.P.PerkEntryPointModifyValue** — AugmentedShock60 (`Skyrim.esm:10FCFA`) confirmed via Phase 1.5 PEPM round-trip evidence (read-side render shape matches; structural round-trip clean).
- [x] **PERK anchors for Layer 1.P.PerkAbilityEffect / PerkQuestEffect / SelectSpell / ModifyActorValue / AddActivateChoice / SetText / SelectText** — Phase 2 picks specific FormIDs from the 8 leaves with vanilla data (per § Real-world frequency in APERK_EFFECTS_AUDIT.md); record candidate counts noted per leaf.
- [x] **Heterogeneous-leaf anchor for Layer 2.01** — PlayerWerewolfFeed family confirmed in audit; Phase 3 picks specific FormID at execution time.
- [x] **EntryPoint enum dump** — `APerkEntryPointEffect+EntryType` 91 members captured in P1.5 scratch lines 2425–2515. ModSpellMagnitude is at index 29.
- [x] **Q1–Q7 expectation flips audit** — Q2 = A locked by Aaron 2026-04-28 (ship all 12); Q1/Q5/Q7 transcription corrections auto-accepted by conductor (mechanism intact, naming + field-location corrected per Mutagen 0.53.1 actual schema).

---

## Phase fill-in checklist (Phase 2 hand-back) — COMPLETE

Phase 2 closed with these MATRIX edits landed:

- [x] **Layer 5 cell count confirmed** — pre-v2.9.3 baseline confirmed at 425 cells (coverage-smoke run shows all 425 PASS). Phase 2 added 30 new cells (Tests 426–455: 14 Layer 1.P + 7 Layer 1.D + 4 Layer 2 + 5 Layer 4). **New total: 425 + 30 = 455 cells, ALL PASS or documented SKIP.** 6 SKIPs preserved unchanged from v2.9.2 (none v2.9.3-introduced).
- [x] **Q1–Q7 expectation flips audit (post-implementation)** — all seven locks held at implementation time. Q1/Q5/Q7 audit-as-source-of-truth corrections from Phase 1 transcribed faithfully into the bridge factory + 12-leaf list + 2-level Conditions nesting. Q4 verified end-to-end via composition probe (race-probe v2.9.3 P2 + coverage-smoke Test 449 + Test 455). One audit-completion follow-up: PEPMA Modification enum dumped at probe-time per § Phase 2 implications #7.
- [x] **Layer 1.D validation-error JSON shape locked** — bridge errors surface via `details[0].error` (existing v2.7.x shape). Rows 1.D.01–1.D.07 + 1.D.unknown_blob assertion harness verifies error contains discriminator-specific substrings (e.g. "BogusType"+"not found", "APerkEffect"+"abstract", "type"+"requires", "Unknown"+"opaque"/"MemorySlice").
- [x] **Error message wording finalized** — `BuildPerkEffectFromJson` factory (PatchEngine.cs ~:2354) carries embedded 12-leaf valid-name lists in each error message: discriminator-not-found / abstract-type-rejection / non-APerkEffect-assignable. PEPSetText TranslatedString contract documented in CHANGELOG + KNOWN_ISSUES + tools_patching.py schema description.
- [x] **4.dsl.02 cross-master synthetic fixture** — Phase 2 built a new synthetic two-plugin fixture (CSV293Master.esp + CSV293Override.esp) at coverage-smoke Test 455, mirroring v2.9.2 P4's Test 425 pattern. Master plugin defines a Perk; override plugin's PERK has a v2.9.3 PerkEntryPointModifyValue with nested HasPerk condition referencing the master's perk FormLink. Verifies v2.6.0's load-order-aware compacted FormID write composes with v2.9.3's nested PerkConditions write path.
- [x] **Layer 2.04 empty-clear** — verified `set_fields:{Effects:[]}` results in `Effects.Count = 0` on readback (Test 450 PASS). Test 454 (4.dsl.05) further verifies sibling-preservation: PERK top-level Level/NumRanks/Trait fields untouched when only Effects is supplied via set_fields.
- [x] **Per-leaf vanilla anchor FormIDs picked** — universal carrier strategy adopted: AugmentedShock60 (`Skyrim.esm:10FCFA`) used for all Layer 1.P/1.D/2/4 cells. Replace-semantics on Effects array means any vanilla PERK works as carrier (the source's own Effects gets replaced by the test payload). Universal FormLink anchor `Skyrim.esm:0001A6E8` used for Spell/Quest/Item slots — bridge doesn't validate FormLink target record types at write-time, so any vanilla FormID round-trips for FormKey-persistence verification (mirrors coverage-smoke § Tests 162–279 pattern). The 4 zero-vanilla-instance leaves (PerkEntryPointAbsoluteValue / AddLeveledItem / AddRangeToValue / ModifyValues) write into the same AugmentedShock60 carrier via replace-semantics — no synthetic-PERK-fixture needed.
- [x] **PerkEntryPointSetText TranslatedString contract** — Phase 2 picked **plain-string-to-TranslatedString convenience path** per local decision D + Aaron 2026-04-28 sign-off. Implementation: `ConvertJsonValue` (PatchEngine.cs:1343-1361) adds a String-to-TranslatedString branch — JSON String fed to a slot typed `Mutagen.Bethesda.Strings.TranslatedString` writes as `new TranslatedString(Language.English, value.GetString())`. Cell `1.P.PerkEntryPointSetText.basic` (Test 434) confirms round-trip clean.
- [x] **PerkQuestEffect.Unknown MemorySlice rejection** — Phase 2 implemented **explicit reject-with-clean-error** in `BuildPerkEffectFromJson` factory (PatchEngine.cs ~:2354 member-walk guard). Rather than letting the JSON value fall through to `ConvertJsonValue` and produce a confusing throw, the factory emits: "PerkQuestEffect.Unknown is an opaque binary blob (MemorySlice<Byte>) and is not a writable field. Omit it from the Effects entry; the source record's value carries through unchanged." Coverage-smoke cell 1.D.unknown_blob (Test 446) PASS.

---

## Phase fill-in checklist (Phase 3 hand-back) — COMPLETE

Phase 3 closed with these MATRIX live-FormID substitutions + scenario outcomes recorded:

- [x] **Live FormIDs** — Scenario 3.1 anchor confirmed live as `Skyrim.esm:10FCFA` overridden by **Requiem - Magic Redone.esp** (load_order 1187) to `REQ_Destruction_Electromancy_050_Electromancy2` ("Electromancy") — the PLAN-named AugmentedShock60 vanilla anchor IS overridden in the Authoria load order to a renamed Requiem-derived perk; chain_length 3 (Skyrim.esm → Requiem.esp → Requiem - Magic Redone.esp). Scenario 3.2 anchor confirmed live as `Skyrim.esm:02BA1D` (PlayerWerewolfFeed) winning natively in Dawnguard.esm — NOT overridden in Authoria; chain_length 2.
- [x] **Per-scenario PASS/FAIL** — Scenario 3.1 magnitude rebalance Value 1.5×(vanilla)→1.2×(Authoria-Requiem winner)→1.1×(v2.9.3 patch) PASS with 21/21 assertions clean (Effects-array shape + concrete leaf + property writes + 12 sibling-preservation axes including Requiem-specific Description string + Requiem's lowered Destruction>=50 condition threshold preserved). Scenario 3.2 heterogeneous 3-leaf write (PEPM + PEPAddActivateChoice + PerkAbilityEffect spanning PEPE-family + non-PEPE-family in one Effects array) PASS with 16/16 assertions clean (per-element concrete type preservation + FormLink resolution + VirtualMachineAdapter sibling passthrough).
- [x] **Scenario 3.2 in-scope-or-skip** — IN SCOPE; precondition met (Dawnguard.esm's PlayerWerewolfFeed has 8-effect heterogeneous Effects array; the 3-leaf REPLACEMENT exercises factory dispatch on 3 distinct concrete leaf classes including a non-PEPE-family leaf). Cross-family coverage stronger than the original PLAN-text expectation.
- [x] **Test ESP cleanup verification** — `rm` against `<modlist>/mods/Claude Output/v293-preflight.esp` + `v293-test-perk-rebalance.esp` + `v293-test-perk-multileaf.esp`; Aaron F5'd MO2; post-F5 `mo2_query_records(plugin_name=...)` for each test ESP returns `total: 0`. Live install state restored to v2.9.3 baseline.
