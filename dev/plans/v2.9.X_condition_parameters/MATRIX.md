# v2.9.0 Verification Matrix

**Authoritative test specification for v2.9.0's verification pass.** Mirrors v2.8.0's MATRIX.md role for v2.7.1 → v2.8.0; this matrix serves the v2.8.0 → v2.9.0 transition. Anchored on **Condition-function parameter slots** (the v2.9 capability) — generalizing v2.8.0's `actor_value` handler into a generic-by-slot-type dispatcher per PLAN.md § A.

**Methodology.** Every cell is one bridge invocation (or one Mutagen direct call for race-probe functional probes), with the listed operation against the listed source record, and a documented expected result. Layers 1, 2, 4, 5 run via `tools/coverage-smoke/` against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_create_patch` against the live Authoria modlist, output to `<modlist>/mods/Claude Output/v2.9-scenario-N.esp`, deleted post-verification.

**Record selection.** Layer 1 / 2 / 4 use `coverage-smoke`'s existing `FirstOrDefault` predicate selection where possible. Where multiple records of a type are needed for depth, use the second record matching the predicate. Specific FormIDs are filled in by Phase 2 as the harness extends; this matrix locks the *what* and *how*, not the precise FormID.

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in the appropriate phase's handoff, including the actual response payload.

**Phase fill-in cadence.** Phase 0 (this commit) lays down the layer scaffold + cell-naming convention + Layer 3 scenario use-case descriptions. **Phase 1** populates Layer 1.P and Layer 1.D per-in-scope-function rows after Aaron's Pareto lock (anchored on Phase 1's inventory probe). **Phase 2** runs the harness end-to-end. **Phase 3** picks live FormIDs for Layer 3 scenarios.

---

## 🧭 Cell-naming convention

| Prefix | Layer | Pattern | Example |
|---|---|---|---|
| `1.P.<Function>.<RecordType>` | 1 — per-function positives | function name + carrier record type | `1.P.GetIsID.MGEF`, `1.P.HasPerk.PERK` |
| `1.D.<NN>` | 1.D — negatives + out-of-scope | sequential within layer | `1.D.01`, `1.D.07` |
| `2.<NN>` | 2 — combinatorial | sequential | `2.01`, `2.04` |
| `3.<N>` | 3 — workflow scenarios | scenario number | `3.1`, `3.2` |
| `4.<sub>.<NN>` | 4 — edges | sub-grouping + sequential | `4.dsl.01`, `4.slot.02`, `4.formid.01` |
| `5.<NN>` | 5 — regression | sequential (mapped 1:1 to v2.8.0 cells; see § Layer 5) | `5.01` |

The `1.P.<Function>.<RecordType>` form mirrors v2.8.0's `1.A.<NN>` "positive-case-row" spirit but anchors on the **function name** (the v2.9 unit of work) instead of an arbitrary row index. `1.D.<NN>` carries v2.8.0's negative-band convention forward unchanged. Phase 1 fills `<Function>` post-Pareto-lock; the in-scope function set comes from Phase 1's inventory probe + Aaron's lock.

---

## Layer 1 — Per-function coverage (positives)

**Pareto lock:** Option A (max-band) per conductor relay 2026-04-26. **199 dispatcher-wired functions** in-scope: 113 FormLinkOrIndex + 41 Enum + 28 MultiSlot (incl. GetEventData) + 11 PrimitiveOnly + 6 sub-A IFormLink<T>. Plus 219 NoParam functions are **in-scope-no-op** (no dispatcher wiring needed; back-compat preserved per PLAN § C). 6 sub-B functions deferred to v2.9.x. See [CONDITIONS_AUDIT.md](CONDITIONS_AUDIT.md).

Each row's expected result follows the shape:

> `mods.conditions_added=1`; readback `condition.Data.GetType() == {Function}ConditionData`; for each slot, the property's runtime value matches what was sent (NOT FormID 0 / enum index 0 default).

**Carrier convention.** MGEF is the canonical carrier for all Layer 1.P cells (mirrors v2.8.0's coverage-smoke pattern; MGEF is the most permissive `Conditions`-bearing record type). Phase 2's coverage-smoke harness may substitute alternative carriers (PERK / PACK / INFO / DLBR) for diversity per function — this is implementation choice, not test-spec mandate. Layer 3 scenarios test live carriers explicitly (INFO for 3.1, PERK for 3.2).

**Source-of-truth for slot signatures:** `<workspace>/scratch/v2.9-phase-1-inventory.txt` per-shape detail sections (line ranges below). The matrix names floor + stretch + sub-A + GetEventData explicitly; the 167 bulk-Pareto functions are enumerated as range cells citing scratch — Phase 2's harness generates one cell per function programmatically. This mirrors v2.8.0 MATRIX.md § Layer 5's range-notation precedent (160 cells in one row).

### 1.P.FormLink — IFormLinkOrIndex<T> + IFormLink<T> slots (119 in-scope)

113 native `IFormLinkOrIndex<T>` (single-slot) + 6 sub-A `IFormLink<T>` (single-slot). Routing pattern: existing `Global` handler (FormKey + parent ConditionData ctor) generalized via `RouteParameterSlot`; sub-A absorbs via a one-line `IFormLink<T>` branch (FormKey + simple ctor, no parent).

**Floor functions** (per PLAN.md baseline; `Object` substituted for `Reference` per [CONDITIONS_AUDIT.md § Architectural surprises §1](CONDITIONS_AUDIT.md)):

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.GetIsID.MGEF` | add_conditions | GetIsID | `Object` | `IFormLinkOrIndex<IReferenceableObjectGetter>` | MGEF | add 1 condition with `parameters: {Object: "Skyrim.esm:<NPC-FormID>"}` | mods.conditions_added=1; readback `condition.Data.Object.FormKey` matches; `condition.Data.GetType() == GetIsIDConditionData` |
| `1.P.GetInFaction.MGEF` | add_conditions | GetInFaction | `Faction` | `IFormLinkOrIndex<IFactionGetter>` | MGEF | as above with `{Faction: "<FACT-FormID>"}` | as above with Faction slot |
| `1.P.GetInCell.MGEF` | add_conditions | GetInCell | `Cell` | `IFormLinkOrIndex<ICellGetter>` | MGEF | as above with `{Cell: "<CELL-FormID>"}` | as above with Cell slot |
| `1.P.HasMagicEffect.MGEF` | add_conditions | HasMagicEffect | `MagicEffect` | `IFormLinkOrIndex<IMagicEffectGetter>` | MGEF | as above with `{MagicEffect: "<MGEF-FormID>"}` | as above with MagicEffect slot |
| `1.P.HasPerk.MGEF` | add_conditions | HasPerk | `Perk` | `IFormLinkOrIndex<IPerkGetter>` | MGEF | as above with `{Perk: "<PERK-FormID>"}` | as above with Perk slot |
| `1.P.HasSpell.MGEF` | add_conditions | HasSpell | `Spell` | `IFormLinkOrIndex<ISpellGetter>` | MGEF | as above with `{Spell: "<SPEL-FormID>"}` | as above with Spell slot |
| `1.P.GetIsRace.MGEF` | add_conditions | GetIsRace | `Race` | `IFormLinkOrIndex<IRaceGetter>` | MGEF | as above with `{Race: "<RACE-FormID>"}` | as above with Race slot |

**Stretch functions:**

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.GetItemCount.MGEF` | add_conditions | GetItemCount | `ItemOrList` | `IFormLinkOrIndex<IItemOrListGetter>` | MGEF | add 1 condition with `parameters: {ItemOrList: "<MISC-or-FLST-FormID>"}` | per-row pattern; readback ItemOrList slot |
| `1.P.IsInList.MGEF` | add_conditions | IsInList | `FormList` | `IFormLinkOrIndex<IFormListGetter>` | MGEF | as above with `{FormList: "<FLST-FormID>"}` | as above with FormList slot |
| `1.P.WornHasKeyword.MGEF` | add_conditions | WornHasKeyword | `Keyword` | `IFormLinkOrIndex<IKeywordGetter>` | MGEF | as above with `{Keyword: "<KYWD-FormID>"}` | as above with Keyword slot |
| `1.P.GetEquipped.MGEF` | add_conditions | GetEquipped | `ItemOrList` | `IFormLinkOrIndex<IItemOrListGetter>` | MGEF | as above with `{ItemOrList: "<WEAP-or-FLST-FormID>"}` | as above with ItemOrList slot |

**Sub-A functions** (`IFormLink<T>` — note: NOT `IFormLinkOrIndex<T>`; type column distinct):

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.GetVATSValueCriticalEffect.MGEF` | add_conditions | GetVATSValueCriticalEffect | `Value` | `IFormLink<ISpellGetter>` | MGEF | add 1 condition with `parameters: {Value: "<SPEL-FormID>"}` | per-row pattern; readback Value slot via IFormLink<T> branch |
| `1.P.GetVATSValueCriticalEffectOrList.MGEF` | add_conditions | GetVATSValueCriticalEffectOrList | `Value` | `IFormLink<ISpellOrListGetter>` | MGEF | as above with SPEL or FLST FormID | as above |
| `1.P.GetVATSValueTarget.MGEF` | add_conditions | GetVATSValueTarget | `Value` | `IFormLink<INpcGetter>` | MGEF | as above with NPC_ FormID | as above |
| `1.P.GetVATSValueTargetOrList.MGEF` | add_conditions | GetVATSValueTargetOrList | `Value` | `IFormLink<INpcOrListGetter>` | MGEF | as above with NPC_ or FLST FormID | as above |
| `1.P.GetVATSValueWeapon.MGEF` | add_conditions | GetVATSValueWeapon | `Value` | `IFormLink<IWeaponGetter>` | MGEF | as above with WEAP FormID | as above |
| `1.P.GetVATSValueWeaponOrList.MGEF` | add_conditions | GetVATSValueWeaponOrList | `Value` | `IFormLink<IWeaponOrListGetter>` | MGEF | as above with WEAP or FLST FormID | as above |

**Bulk Pareto pull** — remaining 102 FormLinkOrIndex functions (113 - 7 floor - 4 stretch = 102):

| Cell range | Functions | Source | Operation | Expected |
|---|---|---|---|---|
| `1.P.<F>.MGEF` × 102 | per scratch lines 1220–1447 (FormLinkOrIndex full slot detail) | aggressive Pareto pull (max-band) | per row: add 1 condition with `parameters: {<slot>: <test-FormID>}` (slot name + type per scratch) | per-row pattern; readback Data type + slot value |

### 1.P.Enum — enum-typed slots (41 in-scope)

Routing pattern: `Enum.Parse(prop.PropertyType, value, ignoreCase: true)`. The v2.8.0 `actor_value` handler is the working precedent — v2.9 generalizes via `RouteParameterSlot`.

**Floor-AV functions** (carryover from v2.8.0; one row per family member):

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.GetActorValue.MGEF` | add_conditions | GetActorValue | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` | MGEF | add 1 condition with `parameters: {ActorValue: "Health"}` (generic dispatch path) | mods.conditions_added=1; readback `condition.Data.ActorValue == ActorValue.Health` |
| `1.P.GetBaseActorValue.MGEF` | add_conditions | GetBaseActorValue | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` | MGEF | as above | as above |
| `1.P.GetActorValuePercent.MGEF` | add_conditions | GetActorValuePercent | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` | MGEF | as above | as above (note: `GetActorValuePercentage` doesn't exist in Mutagen 0.53.1 per CONDITIONS_AUDIT.md § Architectural surprises §4) |

**Bulk Pareto pull** — remaining 38 Enum functions:

| Cell range | Functions | Source | Operation | Expected |
|---|---|---|---|---|
| `1.P.<F>.MGEF` × 38 | per scratch lines 1136–1219 (Enum full slot detail; e.g. GetIsSex/Sex enum, GetEquippedItemType/EquippedItemType enum, IsInCriticalStage/CriticalStage enum, etc.) | aggressive Pareto pull | per row: add 1 condition with `parameters: {<slot>: "<EnumName>"}` (slot name + enum type per scratch) | per-row pattern; readback enum value |

### 1.P.MultiSlot — multiple slots of any types (28 in-scope)

27 native MultiSlot + GetEventData absorbed (3 mixed-shape slots: 2 nested System.Enum + 1 IFormLink<T>). Each slot routed independently via `RouteParameterSlot` foreach over `parameters`.

**Canonical multi-slot example** (cited by Layer 2.01):

| # | Op | Function | Slots | Carrier | Operation | Expected |
|---|----|----------|-------|---------|-----------|----------|
| `1.P.GetStageDone.MGEF` | add_conditions | GetStageDone | `Quest: IFormLinkOrIndex<IQuestGetter>` + `Stage: Int32` | MGEF | add 1 condition with `parameters: {Quest: "<QUST-FormID>", Stage: 50}` | mods.conditions_added=1; readback Quest FormKey resolves + readback Stage == 50 |

**GetEventData absorption** (3 slots, exercises mixed-shape per-slot routing fully):

| # | Op | Function | Slots | Carrier | Operation | Expected |
|---|----|----------|-------|---------|-----------|----------|
| `1.P.GetEventData.MGEF` | add_conditions | GetEventData | `Function: EventFunction (enum, 5 vals)` + `Member: EventMember (enum, 8 vals)` + `Record: IFormLink<ISkyrimMajorRecordGetter>` | MGEF | add 1 condition with `parameters: {Function: "GetIsID", Member: "Form", Record: "Skyrim.esm:<FormID>"}` | mods.conditions_added=1; readback all three slots resolve (validates 2A's IFormLink<T> branch under multi-slot composition) |

**Bulk Pareto pull** — remaining 26 MultiSlot functions (27 native + GetEventData = 28; GetStageDone + GetEventData explicit above):

| Cell range | Functions | Source | Operation | Expected |
|---|---|---|---|---|
| `1.P.<F>.MGEF` × 26 | per scratch lines 1448–1531 (MultiSlot full slot detail; includes `Unknown` 3-slot generic-fallback at 1527–1530) | aggressive Pareto pull | per row: add 1 condition with `parameters: {<slot1>: ..., <slot2>: ..., ...}` (slot names + types per scratch) | per-row pattern; readback all slots resolve |

### 1.P.PrimitiveOnly — int / float / bool slots only (11 in-scope)

Routing pattern: direct conversion (`JsonElement.GetInt32()` / `GetSingle()` / `GetBoolean()`). Strict per PLAN.md § A — wider primitive types (long, double, byte, short, uint) would land in Exotic; none surfaced in v2.9.0's in-scope set.

**Bulk Pareto pull** — all 11 PrimitiveOnly functions (no individual call-outs; floor and stretch had no PrimitiveOnly representatives):

| Cell range | Functions | Source | Operation | Expected |
|---|---|---|---|---|
| `1.P.<F>.MGEF` × 11 | per scratch lines 1532–1554 (PrimitiveOnly full slot detail; e.g. GetInCurrentLocAlias/LocationAliasIndex Int32, GetIsAliasRef/ReferenceAliasIndex Int32, etc.) | aggressive Pareto pull | per row: add 1 condition with `parameters: {<slot>: <number_or_bool>}` (slot name + type per scratch) | per-row pattern; readback slot value matches |

### 1.P.NoParam — parameterless functions (219 in-scope-no-op)

219 functions take no parameters; the dispatcher has nothing to route. They were already accepting parameterless `function: "X"` syntax in v2.7.1+ (the existing `Activator.CreateInstance(condDataType)` path) and continue unchanged. **NOT in `KnownParameterizedFunctions`** per Phase 2A's design (per [CONDITIONS_AUDIT.md § NoParam handling](CONDITIONS_AUDIT.md)).

| Cell range | Functions | Coverage |
|---|---|---|
| n/a (no Layer 1.P cells) | per scratch per-shape NoParam list (lines ~705–905; e.g. GetDead, GetCannibal, GetGold, GetAlarmed, IsLocationLoaded — 219 total) | Implicit via Layer 5 regression band — these all worked in v2.7.1+ and must continue to work in v2.9.0 |

---

## Layer 1.D — Per-function negatives + out-of-scope errors

**Per-shape representative negatives only** — bulk negatives (one per in-scope function) Phase 2's coverage-smoke generates programmatically; the matrix doesn't enumerate 199 negative rows. Structural error rows (out-of-scope function, unknown SlotName, unsupported slot type, parameterless back-compat) are pre-specced and remain.

### 1.D.in-scope — bad parameter values (representative per shape)

| # | Op | Function | Bad input | Expected |
|---|----|----------|-----------|----------|
| `1.D.01` | add_conditions | GetIsID (FormLinkOrIndex representative) | `parameters: {Object: "Skyrim.esm:DOESNOTEXIST"}` (malformed FormID) | record-level error from `FormIdHelper.Parse` naming the function + slot + bad FormID; rollback; output ESP omits the failed record |
| `1.D.02` | add_conditions | GetVATSValueWeapon (sub-A IFormLink<T> representative) | `parameters: {Value: "Skyrim.esm:DOESNOTEXIST"}` (malformed FormID through IFormLink<T> branch) | as above with Value slot via IFormLink<T> branch |
| `1.D.03` | add_conditions | GetActorValue (Enum representative) | `parameters: {ActorValue: "BogusStatThatDoesntExist"}` (bad enum name) | record-level error from `Enum.Parse` naming the enum type (`ActorValue`) and the bad name; rollback |
| `1.D.04` | add_conditions | GetStageDone (MultiSlot representative) | `parameters: {Quest: "Skyrim.esm:DOESNOTEXIST", Stage: 50}` (bad slot 1 — Quest FormID malformed) | as `1.D.01` pattern with Quest slot named; Stage slot doesn't matter (validation halts at first bad slot) |
| `1.D.05` | add_conditions | GetEventData (MultiSlot mixed-shape representative) | `parameters: {Function: "BogusEventFunction", Member: "Form", Record: "Skyrim.esm:00FFFFFF"}` (bad enum on Function slot) | record-level error from `Enum.Parse` naming `EventFunction` enum type and the bad name |
| `1.D.06` | add_conditions | GetIsAliasRef (PrimitiveOnly Int32 representative) | `parameters: {ReferenceAliasIndex: "not-a-number"}` (string supplied where Int32 expected) | record-level error: `"Parameter slot 'ReferenceAliasIndex' on function 'GetIsAliasRef' expects a Int32; got String."` (per Phase 2 dispatcher type-coercion path) |

Phase 2's coverage-smoke generates one bad-input cell per in-scope function programmatically using these patterns as templates. Coverage range estimate: 199 cells (one per in-scope function).

### 1.D.out-of-scope — structural errors for v2.9.0 architecture

These cells exercise § C of PLAN.md (out-of-scope handling). Wording aligned with [CONDITIONS_AUDIT.md § Error template confirmation](CONDITIONS_AUDIT.md).

| # | Op | Function | Setup | Expected |
|---|----|----------|-------|----------|
| `1.D.50` | add_conditions | `GetVMScriptVariable` (sub-B deferred function, picked as representative — see CONDITIONS_AUDIT.md § Sub-B deferral for the full 6-function list) | caller supplies `parameters: {Target: "<PlacedSimple-FormID>", VariableName: "MyScriptVar"}` | record-level error: `"Condition function 'GetVMScriptVariable' has parameter slots (Target, VariableName) that v2.9 does not yet wire. Authoring this function today produces a structurally-valid but always-false condition. v2.9 in-scope set: see KNOWN_ISSUES.md § v2.9.0 Condition-parameter coverage. Please file a Live Reported Bug if you need this function added."` (in-scope set elided to doc reference because 199 names is too long for inline error message) |
| `1.D.51` | add_conditions | GetIsID (in-scope) | caller supplies `parameters: {NotARealSlot: <value>}` | record-level error: `"Function GetIsID has no parameter slot named 'NotARealSlot' on its Mutagen ConditionData."` (natural slot-name-lookup-failed path; covers the `*Unused*Parameter*` footgun-guard case if Phase 2A implements it — see CONDITIONS_AUDIT.md § Error template) |
| `1.D.52` | add_conditions | n/a — SKIP-with-reason | n/a | **SKIP**: max-band Pareto absorbed all routable slot types (sub-A IFormLink<T> + GetEventData + native shapes). No in-scope function has an unsupported slot type in v2.9.0. This cell becomes live only if Mutagen 0.54+ adds a new slot type the dispatcher doesn't route. Phase 2 records as SKIP per the convention at MATRIX.md § Skip-with-reason. |
| `1.D.53` | add_conditions | NoParam function (in-scope-no-op; e.g. GetDead) | caller supplies NO `parameters` field at all (parameterless invocation, v2.7.1+ behavior) | no error, no warning; back-compat preserved per § C "warning-not-error" rationale; condition lands with default slots (structurally-valid; semantically identical to v2.7.1 behavior) |
| `1.D.54` | add_conditions | NoParam function (in-scope-no-op; e.g. GetDead) | caller supplies `parameters: {Foo: 1}` for a function with no parameter slots | record-level error per `1.D.51` natural path: `"Function GetDead has no parameter slot named 'Foo' on its Mutagen ConditionData."` (NoParam functions naturally fail any slot-name lookup; no special case in dispatcher — confirms NoParam-NOT-in-KnownParameterizedFunctions design from CONDITIONS_AUDIT.md § NoParam handling) |

---

## Layer 2 — Combinatorial probes

Multi-slot functions, multiple conditions per record, and v2.8.0 × v2.9.0 surface composition (Effects-list cells with v2.9 in-scope conditions).

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | Multi-slot single condition (canonical) | `add_conditions` on MGEF: one `GetStageDone` condition with `parameters: {Quest: "Skyrim.esm:<QUST-FormID>", Stage: 50}` — the Phase-1-locked canonical multi-slot example (FormLink + Int32 mixed shape, exercises per-slot routing through `RouteParameterSlot` foreach) | mods.conditions_added=1; readback `condition.Data.GetType() == GetStageDoneConditionData`; readback `Quest.FormKey` matches + readback `Stage == 50` |
| `2.02` | Multiple conditions, mixed in-scope functions | `add_conditions` on MGEF: 3 conditions, each a different in-scope function (e.g. GetIsID + HasPerk + GetActorValue), each with its own `parameters` | mods.conditions_added=3; readback each condition's Data type + slot values match |
| `2.03` | Effects-list write + nested Conditions using v2.9 slots | `set_fields` on SPEL: `Effects=[{BaseEffect, Data, Conditions:[{function:"HasPerk", parameters:{Perk:"<formid>"}}]}]` (v2.8 P1 + v2.9 P2 surface composition) | mods.fields_set=1; readback Effects.Count=1; nested Conditions[0].Data is `HasPerkConditionData` with `Perk` slot resolved |
| `2.04` | Multi-record patch: in-scope + out-of-scope mix | 3 records: 2 with in-scope-function `add_conditions`, 1 with out-of-scope-function + `parameters` (Tier-D-style fail) | response.successful_count=2; failed_count=1; output ESP contains the 2 successful records, NOT the failed one |
| `2.05` | Both `actor_value` AND `parameters: {ActorValue: ...}` on different records (back-compat coexistence) | 2 records: record A uses v2.8 `actor_value` field; record B uses v2.9 `parameters: {ActorValue: ...}` | both succeed; mods.conditions_added=1 each; readback ActorValue resolves correctly on both (proves back-compat path stays live alongside generic dispatcher) |
| `2.06` | Multi-condition single record with multi-slot function | `add_conditions` on PERK: 2 conditions, one multi-slot (e.g. GetStageDone) + one single-slot (e.g. HasSpell) | mods.conditions_added=2; readback both Data types correct + all slots resolved |

---

## Layer 3 — Workflow scenarios on live install

Run via `mo2_create_patch` against the live Authoria modlist. Output filenames `v2.9-scenario-N.esp`. Test patches deleted post-verification.

**Phase 0 pre-specs use cases + assertions; Phase 3 picks live FormIDs at execution time.** Aaron may swap the named records during Phase 3 if better targets exist in the live modlist.

### Scenario 3.1 — Dialog `GetIsID` topic gating

**Use case.** Real-world dialog patcher: a dialog topic (DIAL/INFO record, surfaced via DialogConditions on the INFO) gates a topic to a specific NPC by adding a `GetIsID` Condition with `Object` pointing at the target NPC. Today the bridge accepts `function: "GetIsID"` but leaves the `Object` slot at FormID 0 — the condition is structurally-valid but functionally always-false, so the dialog never fires for the target NPC. v2.9 lands the `Object` slot via the generic dispatcher. (`Object` is GetIsID's function-specific slot — `Reference` is a base prop used for `RunOnType: Reference` mode; see CONDITIONS_AUDIT.md § Architectural surprises §1.)

**Target (Phase 3 picks):**
- 1 INFO record from the live modlist with an existing DialogConditions list (or empty — bridge's `add_conditions` works either way).
- 1 NPC_ FormID for the GetIsID `Object` slot.

**Operations:**
- `add_conditions` on the INFO record: one ConditionFloat with `function: "GetIsID"`, `operator: "=="`, `value: 1`, `parameters: {Object: "<plugin>:<localID-of-NPC>"}`.

**Assertions:**
- mods.conditions_added=1.
- Readback `condition.Data.GetType() == GetIsIDConditionData`.
- Readback `condition.Data.Object.FormKey` matches the supplied NPC FormKey (NOT FormID 0).
- Existing DialogConditions on the INFO are preserved (add, not replace).
- Output ESP contains the INFO override; xEdit reads cleanly with no unresolved-FormID warning.

### Scenario 3.2 — Perk `HasPerk` / `HasSpell` prerequisite gate

**Use case.** Real-world perk patcher: a PERK record in a perk-overhaul mod gates an effect (PERK.Effects entry's PerkConditions, OR the perk's top-level Conditions list) on the player having a prerequisite perk or spell. Today the bridge accepts `function: "HasPerk"` / `"HasSpell"` but leaves Perk / Spell at FormID 0 — the condition is structurally-valid but functionally always-false, so the perk's effect never activates regardless of prerequisites. v2.9 lands the Perk / Spell slots.

**Target (Phase 3 picks):**
- 1 PERK record from the live modlist (or 2 if both HasPerk + HasSpell variants tested in same scenario).
- 1 prerequisite PERK FormID for the HasPerk slot.
- _optional:_ 1 prerequisite SPEL FormID for the HasSpell slot, exercising both functions in one scenario.

**Operations:**
- `add_conditions` on the PERK record: one ConditionFloat with `function: "HasPerk"`, `operator: "=="`, `value: 1`, `parameters: {Perk: "<plugin>:<localID-of-prereq-perk>"}`.
- _optional second condition:_ `function: "HasSpell"`, `parameters: {Spell: "<plugin>:<localID-of-prereq-spell>"}`.

**Assertions:**
- mods.conditions_added=1 (or 2 if both variants tested).
- Readback `condition.Data.GetType() == HasPerkConditionData` (and `HasSpellConditionData` if tested).
- Readback `condition.Data.Perk.FormKey` matches the supplied prereq-perk FormKey.
- _if HasSpell tested:_ readback `condition.Data.Spell.FormKey` matches the prereq-spell FormKey.
- Existing PERK Conditions preserved.
- Output ESP contains the PERK override; xEdit clean.

---

## Layer 4 — Edges + carry-over probes

Architectural edges of the v2.9 dispatcher that don't fit Layer 1's per-function-positive shape. Each cell exercises a specific edge of PLAN.md § A / § C.

### 4.dsl — DSL ambiguity / both-forms supplied

| # | Setup | Expected |
|---|-------|----------|
| `4.dsl.01` | ConditionEntry with both `actor_value: "Health"` (v2.8 path) AND `parameters: {ActorValue: "Health"}` (v2.9 path) supplied for the same condition | record-level error: `"Both 'actor_value' and 'parameters: {ActorValue: ...}' supplied — choose one. The v2.8 'actor_value' field is back-compat syntactic sugar for the v2.9 'parameters: {ActorValue: ...}' form."` (exact wording finalized in Phase 2) |
| `4.dsl.02` | ConditionEntry with `actor_value: "Health"` alone (v2.8 back-compat path) | success; mods.conditions_added=1; readback ActorValue=Health (back-compat preserved) |
| `4.dsl.03` | ConditionEntry with `parameters: {ActorValue: "Health"}` alone (v2.9 generic-dispatch path) | success; mods.conditions_added=1; readback ActorValue=Health (proves generic dispatcher reaches the ActorValue slot) |

### 4.slot — slot dispatch edges

| # | Setup | Expected |
|---|-------|----------|
| `4.slot.01` | In-scope function called with `parameters: {}` (empty object, no slots) on a function known to have parameter slots | document actual behavior: silent default-slot-zero, OR warning, OR error. Locked at Phase 2 implementation per § C (likely silent — empty `parameters` indistinguishable from no `parameters` field). |
| `4.slot.02` | In-scope function called with `parameters: {NotARealSlot: <value>}` (covered also by `1.D.51` — duplicated here for layer-4 visibility) | record-level error per `1.D.51` |
| `4.slot.03` | In-scope function called with `parameters` containing the right slot name BUT a value of the wrong JSON-type (e.g. `parameters: {Object: 42}` for GetIsID, where `Object` is `IFormLinkOrIndex<IReferenceableObjectGetter>` expecting a FormID string per CONDITIONS_AUDIT.md § Architectural surprises §1) | record-level error: `"Parameter slot '{slotName}' on function '{function}' expects a {expected-type}; got {actual-type}."` |

### 4.formid — FormID resolution edges inside IFormLinkOrIndex<T>

| # | Setup | Expected |
|---|-------|----------|
| `4.formid.01` | GetIsID + `parameters: {Object: "Skyrim.esm:DOESNOTEXIST"}` (malformed FormID — non-hex) | record-level error: clean message identifying the malformed FormID and the slot |
| `4.formid.02` | GetIsID + `parameters: {Object: "NotARealPlugin.esp:0001A696"}` (plugin not in load order at write-time) | document behavior: bridge writes the FormKey as supplied (Mutagen accepts unresolved plugins; load-order validation happens at WriteToBinary time, not at FormKey-construction time). Matches v2.8.0's write-time-not-validate-time posture. |
| `4.formid.03` | GetIsID + `parameters: {Object: "Skyrim.esm:00FFFFFF"}` (well-formed but record absent) | document behavior: bridge writes the FormKey as supplied; readback shows the FormID; xEdit may flag at runtime but bridge does not validate record existence (matches v2.8.0's FormLink-bonus-catch behavior — write-time, not validate-time) |

### 4.enum — enum slot edges

| # | Setup | Expected |
|---|-------|----------|
| `4.enum.01` | `parameters: {ActorValue: "BogusStatThatDoesntExist"}` | record-level error from `Enum.Parse`; clean message identifying the enum type and the bad name |
| `4.enum.02` | `parameters: {ActorValue: "health"}` (lowercase — `Enum.Parse(... ignoreCase: true)` per § A) | success; mods.conditions_added=1; readback ActorValue=Health (case-insensitive parse confirmed) |
| `4.enum.03` | `parameters: {ActorValue: 24}` (numeric instead of string — depends on Phase 2's JSON-element handling) | document Phase 2 decision: accept numeric (cast to enum index) OR error. Likely error — strings are the documented form. |

### 4.compat — back-compat preservation

| # | Setup | Expected |
|---|-------|----------|
| `4.compat.01` | Out-of-scope function called with NO `parameters` field at all (v2.7.1 / v2.8.0 baseline behavior) | no error, no warning; condition lands with default slots (structurally-valid but always-false, as today). Per § C "warning-not-error" rationale — the warning surfaces silent-default risk only if the caller signals intent via `parameters`. |
| `4.compat.02` | All 22 pre-v2.8.0 + 138 v2.8.0 coverage-smoke cells run (covered in Layer 5) | all green; no regression. |

### 4.carry — Carry-overs from v2.8.0 + earlier

These remain deferred per PLAN.md § Carry-overs. Cells exist to confirm the surface shape stays unchanged.

| # | Carry-over | Setup | Expected |
|---|------------|-------|----------|
| `4.carry.01` | Quest condition disambiguation (DialogConditions / EventConditions) | `add_conditions` on QUST | Tier D error: unmatched_operators=["add_conditions"] (unchanged from v2.7.1 / v2.8.0) |
| `4.carry.02` | AMMO enchantment | AMMO: set_enchantment | Tier D error (unchanged) |
| `4.carry.03` | Replace-semantics whole-dict assignment (Tier C dicts) | covered in v2.8.0 4.r.* | merge-only confirmed (unchanged) |
| `4.carry.04` | Chained dict access | covered in v2.8.0 4.chained.* | rejection confirmed (unchanged) |

---

## Layer 5 — Regression band

All 160 v2.8.0 coverage-smoke cells run unchanged. v2.9 must not regress any v2.8.0 behavior.

| Cell range | Source | Expected |
|---|---|---|
| `5.01`–`5.160` | `dev/plans/v2.8.0_verification/MATRIX.md` Layer 1 (1.A + 1.B + 1.C + 1.E + 1.regression + 1.D) + Layer 2 + Layer 4 | each cell PASS as it did in v2.8.0 P5 |

The v2.8.0 baseline was 22 pre-v2.8.0 + 138 v2.8.0 = 160. Counts may shift slightly if Phase 2 dedupes or merges where same code path is exercised. Phase 2's coverage-smoke harness is the authoritative source of truth for the regression count.

---

## Total assertion count (post-Pareto-lock)

**Pareto:** Option A (max-band) — 199 dispatcher-wired functions in-scope. Two counts below: **matrix rows** (specification rows in this doc) and **harness cells** (what `coverage-smoke/Program.cs` runs — programmatically generated from CONDITIONS_AUDIT.md scratch + the matrix specification).

| Layer | Matrix rows | Harness cells | Source |
|---|---:|---:|---|
| 1.P.FormLink (incl. sub-A IFormLink<T>) | 18 (7 floor + 4 stretch + 6 sub-A + 1 bulk-range) | 119 | scratch lines 1220–1447 + sub-A in Exotic detail 1162–1207 |
| 1.P.Enum | 4 (3 floor-AV + 1 bulk-range) | 41 | scratch 1136–1219 |
| 1.P.MultiSlot (incl. GetEventData absorbed) | 3 (GetStageDone + GetEventData + 1 bulk-range) | 28 | scratch 1448–1531 + GetEventData in Exotic detail |
| 1.P.PrimitiveOnly | 1 (bulk-range) | 11 | scratch 1532–1554 |
| 1.P.NoParam | 1 (reference) | 0 (covered implicitly via Layer 5 regression) | scratch ~705–905 |
| 1.D.in-scope | 6 (per-shape representatives) | ~199 (Phase 2 generates programmatically — 1 bad-input cell per in-scope function using the 6 templates) | this doc + scratch |
| 1.D.out-of-scope | 5 (1.D.50–54; was 4 in Phase 0; added 1.D.54 for NoParam-with-bogus-slot) | 5 (1.D.52 SKIP-with-reason; max-band absorbed all routable shapes) | this doc |
| 2 — Combinatorial | 6 | 6 | this doc |
| 3 — Workflow scenarios | 2 scenarios | ~10 assertions | this doc; Phase 3 picks live FormIDs |
| 4.dsl | 3 | 3 | this doc |
| 4.slot | 3 | 3 | this doc (4.slot.03 example uses GetIsID `Object` per audit) |
| 4.formid | 3 | 3 | this doc |
| 4.enum | 3 | 3 | this doc |
| 4.compat | 2 | 2 (cross-references Layer 5) | this doc |
| 4.carry | 4 | 4 | this doc |
| 5 — Regression | 1 (range row) | 160 | v2.8.0 baseline |
| **Total** | **65 matrix rows** | **~597 harness cells** | — |

Phase 2 sub-session split (per [PHASE_1_HANDOFF.md § Conductor asks](PHASE_1_HANDOFF.md)): 4 sessions — 2A (infra + 119 FormLinkOrIndex/sub-A, ~120–240 cells) → 2B (41 Enum, ~80) → 2C (28 MultiSlot+GetEventData, ~60–80) → 2D (11 PrimitiveOnly, ~22).

Cell counts may shift slightly if Phase 2 dedupes / merges where the same code path is exercised twice. CONDITIONS_AUDIT.md scratch (1622 lines) is the authoritative slot-signature reference; the harness reads from it directly rather than from this matrix doc.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.8.0:

```
[1.P.GetIsID.MGEF]      add_conditions GetIsID    MGEF FirstMgef          PASS (Object resolved to Skyrim.esm:000A2C8E)
[1.P.HasPerk.PERK]      add_conditions HasPerk    PERK FirstPerk          PASS (Perk resolved to Skyrim.esm:000C44C0)
[1.P.GetActorValue.MGEF] add_conditions GetActorValue MGEF FirstMgef     PASS (ActorValue=Health via generic dispatcher)
[1.D.50]                add_conditions OutOfScopeFunc + parameters       PASS (out-of-scope error rolled back; in-scope set named in error)
[1.D.51]                add_conditions GetIsID + bad SlotName            PASS (no-such-slot error)
[2.01]                  multi-slot GetStageDone(Quest+Stage)             PASS (both slots resolved)
[3.1]                   dialog GetIsID gating                            PASS (Object slot resolved on live INFO)
[4.dsl.01]              both actor_value AND parameters supplied         FAIL: expected unambiguous-DSL error, got success
[5.01]…[5.160]          v2.8.0 regression band                           160/160 PASS
```

Failures embed enough context for handoff to lift into the bug list directly.

---

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a record with the right shape (e.g. an MGEF carrier without an existing Conditions list when the test wants empty-Conditions starting state), the harness prints:

```
[1.P.<F>.<RT>]  add_conditions <F> <RT> <none-with-empty-Conditions>  SKIP: no vanilla <RT> has empty Conditions list
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture or accept the gap.

Phase 1 may also skip Layer 1.P rows for stretch-candidate functions if Pareto evidence doesn't support them. Locked-out stretches go in CONDITIONS_AUDIT.md as deferred-to-future-v2.9.x candidates.

---

## Phase fill-in checklist (Phase 1 hand-back) — COMPLETED 2026-04-26

Phase 1 closed with these MATRIX edits (per conductor closeout direction post-Pareto-lock):

- [x] **Layer 1.P.FormLink** — 18 matrix rows (7 floor + 4 stretch + 6 sub-A + 1 bulk-range cell covering 102 remaining); 119 harness cells. Sub-A type column distinguishes `IFormLink<T>` from `IFormLinkOrIndex<T>`. `Object` substituted for `Reference` on GetIsID per CONDITIONS_AUDIT.md § Architectural surprises §1.
- [x] **Layer 1.P.Enum** — 4 matrix rows (3 floor-AV explicit: GetActorValue / GetBaseActorValue / GetActorValuePercent + 1 bulk-range covering 38); 41 harness cells. `GetActorValuePercentage` dropped (doesn't exist in Mutagen 0.53.1).
- [x] **Layer 1.P.MultiSlot** — 3 matrix rows (GetStageDone explicit as Layer 2.01 canonical + GetEventData explicit (sub-A absorbed, exercises mixed-shape per-slot routing) + 1 bulk-range covering 26); 28 harness cells.
- [x] **Layer 1.P.PrimitiveOnly** — 1 bulk-range row; 11 harness cells. No floor/stretch representatives surfaced; full enumeration in scratch.
- [x] **Layer 1.P.NoParam** (new section) — 1 reference row; 0 harness cells (covered implicitly via Layer 5 regression). 219 functions in-scope-no-op per CONDITIONS_AUDIT.md § NoParam handling.
- [x] **Layer 1.D.in-scope** — 6 representative rows per shape (1 FormLinkOrIndex + 1 sub-A IFormLink<T> + 1 Enum + 1 MultiSlot single-shape + 1 MultiSlot mixed-shape + 1 PrimitiveOnly Int32). Bulk negatives (~199 cells) Phase 2 generates programmatically using these as templates.
- [x] **Layer 1.D.out-of-scope** — 1.D.50 names `GetVMScriptVariable` (sub-B representative); 1.D.51 unchanged; **1.D.52 → SKIP-with-reason** (max-band absorbed all routable slot types); 1.D.53 unchanged; **new 1.D.54** added (NoParam function with bogus-slot — confirms NoParam-NOT-in-KnownParameterizedFunctions design via natural slot-name-lookup-failed path). Wording aligned with CONDITIONS_AUDIT.md § Error template confirmation.
- [x] **Layer 2.01** — names `GetStageDone(Quest, Stage)` explicitly as the canonical multi-slot example.
- [x] **Layer 4.slot.03** — example slot updated from `Reference` to `Object` (GetIsID's actual function-specific slot per audit).
- [x] **Total assertion count** — table fully repopulated (matrix rows + harness cells split, total ~597 harness cells across all layers).

**Out of Phase 1 scope** (per conductor side-question — handled by Phase 2A's `[v2.9 plan-amend]` first commit, mirroring v2.8.0's precedent at `407c5e3` / `ca62e44`):
- PLAN.md § Architecture B example (`Reference` → `Object`)
- MATRIX.md § Layer 3 Scenario 3.1 (still says `Reference.FormKey` in lines 138–153 — Phase 2A folds this into the plan-amend so the matrix and PLAN move together)
- PLAN.md § Phase 1 step 2 static skip list (drop, point at audit's dynamic detector)
- PLAN.md § Phase 1 floor-AV (drop GetActorValuePercentage)
- PLAN.md § Carry-overs (add sub-B 6-function deferral)
