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

Phase 1 populates this layer with one row per (in-scope-function × test-record-type) pair. Group by parameter shape (the inventory probe's categorization per PLAN.md § Phase 1 step 2). The **floor proposal** (PLAN.md § Phase 1 conductor decisions): `GetIsID`, `GetInFaction`, `GetInCell`, `HasMagicEffect`, `HasPerk`, `HasSpell`, `GetIsRace` + ActorValue carryover. **Stretch candidates** if Pareto evidence supports: `GetItemCount`, `IsInList`, `WornHasKeyword`, `GetEquipped`. Aaron has signalled aggressive Pareto guidance — Phase 1's executor should not default-conservative.

Each row's expected result follows the shape:

> `mods.conditions_added=1`; readback `condition.Data.GetType() == {Function}ConditionData`; for each slot, the property's runtime value matches what was sent (NOT FormID 0 / enum index 0 default).

Test-record-type = the carrier record on which the Conditions list lives (MGEF for spell-effect conditions, PERK for perk conditions, PACK for package conditions, DLBR/INFO for dialog conditions, etc.). Phase 1 picks one carrier per function based on what real patchers exercise.

### 1.P.FormLink — IFormLinkOrIndex<T>-typed slots

Functions whose parameter is a single `IFormLinkOrIndex<T>` slot. Routing pattern: existing `Global` handler (FormKey + parent ConditionData ctor) generalized via `RouteParameterSlot`.

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.<F>.<RT>` | add_conditions | _post-Pareto-lock_ | _slot name_ | `IFormLinkOrIndex<T>` | _carrier_ | add 1 condition with `parameters: {<Slot>: "<plugin>:<localID>"}` | per shape above; readback slot resolves to the supplied FormKey |

**Phase 1 placeholders to fill (anchored on floor):**
- `1.P.GetIsID.<RT>` — `Reference: IFormLinkOrIndex<ISkyrimMajorRecordGetter>`
- `1.P.GetInFaction.<RT>` — `Faction: IFormLinkOrIndex<IFactionGetter>`
- `1.P.GetInCell.<RT>` — `Cell: IFormLinkOrIndex<ICellGetter>`
- `1.P.HasMagicEffect.<RT>` — `MagicEffect: IFormLinkOrIndex<IMagicEffectGetter>`
- `1.P.HasPerk.<RT>` — `Perk: IFormLinkOrIndex<IPerkGetter>`
- `1.P.HasSpell.<RT>` — `Spell: IFormLinkOrIndex<ISpellGetter>`
- `1.P.GetIsRace.<RT>` — `Race: IFormLinkOrIndex<IRaceGetter>`
- _stretch — only if Phase 1 Pareto evidence supports:_ `1.P.IsInList.<RT>`, `1.P.WornHasKeyword.<RT>`, `1.P.GetEquipped.<RT>`

### 1.P.Enum — enum-typed slots

Functions whose parameter is one or more enum-typed slots. Routing pattern: `Enum.Parse(prop.PropertyType, value, ignoreCase: true)`. The v2.8.0 `actor_value` handler is the working precedent — v2.9 generalizes via `RouteParameterSlot`.

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.GetActorValue.MGEF` | add_conditions | GetActorValue (carryover) | `ActorValue` | `ActorValue` enum | MGEF | add 1 condition with `parameters: {ActorValue: "Health"}` (generic dispatch path) | mods.conditions_added=1; readback ActorValue=Health |
| `1.P.GetActorValueMax.MGEF` | add_conditions | _post-Pareto-lock — GetActorValueMax / GetActorValuePercentage / etc. (Phase 1 inventory enumerates the family)_ | `ActorValue` | enum | MGEF | as above with the family member | as above |
| `1.P.<F>.<RT>` | add_conditions | _other enum-slot functions Phase 1 surfaces (e.g. Sex)_ | _slot name_ | _enum type_ | _carrier_ | add 1 condition with `parameters: {<Slot>: "<EnumName>"}` | as above with the enum value resolved |

### 1.P.MultiSlot — multiple slots of any types

Functions with two or more parameter slots (e.g. `GetStageDone(Quest: IFormLinkOrIndex<IQuestGetter>, Stage: Int32)`). Each slot routed independently per its type.

| # | Op | Function | Slots | Carrier | Operation | Expected |
|---|----|----------|-------|---------|-----------|----------|
| `1.P.<F>.<RT>` | add_conditions | _post-Pareto-lock_ | _slot 1: type, slot 2: type, ..._ | _carrier_ | add 1 condition with `parameters: {<Slot1>: ..., <Slot2>: ...}` | mods.conditions_added=1; readback all slots resolve correctly |

**Phase 1 placeholders to fill (if multi-slot functions are in-scope post-lock):**
- `1.P.GetStageDone.<RT>` — `Quest: IFormLinkOrIndex<IQuestGetter>` + `Stage: Int32` (canonical multi-slot example)
- _other multi-slot functions Phase 1 surfaces_

### 1.P.PrimitiveOnly — int / float / bool slots only

Functions whose parameter slots are pure primitives (`Int32`, `Single`, `Boolean`) — no FormLinks, no enums. Routing pattern: direct conversion (`JsonElement.GetInt32()` / `GetSingle()` / `GetBoolean()`).

| # | Op | Function | Slot | Type | Carrier | Operation | Expected |
|---|----|----------|------|------|---------|-----------|----------|
| `1.P.<F>.<RT>` | add_conditions | _post-Pareto-lock — only if Phase 1 surfaces a primitive-only function in the locked Pareto set_ | _slot name_ | `Int32` / `Single` / `Boolean` | _carrier_ | add 1 condition with `parameters: {<Slot>: <number_or_bool>}` | mods.conditions_added=1; readback slot value matches |

---

## Layer 1.D — Per-function negatives + out-of-scope errors

Phase 1 populates the per-in-scope-function bad-parameter rows. The structural error rows (out-of-scope function, unknown SlotName, unsupported slot type) are pre-specced here and don't depend on which functions are locked.

### 1.D.in-scope — bad parameter values for in-scope functions

| # | Op | Function | Bad input | Expected |
|---|----|----------|-----------|----------|
| `1.D.<NN>` | add_conditions | _post-Pareto-lock_ | bad FormID inside `IFormLinkOrIndex<T>` slot (e.g. `parameters: {Reference: "Skyrim.esm:DOESNOTEXIST"}`) | record-level error with named function + slot; rollback; clean message |
| `1.D.<NN>` | add_conditions | _post-Pareto-lock_ | bad enum name inside enum slot (e.g. `parameters: {ActorValue: "BogusStat"}`) | record-level error from `Enum.Parse` failure; rollback; clean message identifying the slot type |

Phase 1 fills one row per in-scope function for each parameter shape it carries (FormLink → bad FormID; enum → bad name; multi-slot → bad slot 1; primitive → bad type coercion).

### 1.D.out-of-scope — structural errors for v2.9.0 architecture

These cells exist regardless of which functions Phase 1 locks. They exercise § C of PLAN.md (out-of-scope handling).

| # | Op | Function | Setup | Expected |
|---|----|----------|-------|----------|
| `1.D.50` | add_conditions | function NOT in v2.9.0 in-scope set | caller supplies `parameters: {SomeSlot: <value>}` | record-level error: `"Condition function '{function}' has parameter slots ({list}) that v2.9 does not yet wire. Authoring this function today produces a structurally-valid but always-false condition. v2.9 in-scope set: {list}. Please file a Live Reported Bug if you need this function added."` |
| `1.D.51` | add_conditions | function IN v2.9.0 in-scope set | caller supplies `parameters: {NotARealSlot: <value>}` | record-level error: `"Function {function} has no parameter slot named 'NotARealSlot' on its Mutagen ConditionData."` |
| `1.D.52` | add_conditions | function IN v2.9.0 in-scope set with slot type the dispatcher can't route | caller supplies `parameters: {<exoticSlot>: <value>}` (only fires if Phase 1 surfaced an exotic shape and Aaron deferred) | record-level error: `"Condition function '{function}' parameter slot '{slotName}' has type {slotType} which the bridge doesn't yet route. v2.9 covers IFormLinkOrIndex<T>, enum, int, float, bool. Please file a Live Reported Bug if you need this slot."` (SKIP-with-reason if no exotic in scope) |
| `1.D.53` | add_conditions | function NOT in v2.9.0 in-scope set | caller supplies NO `parameters` (parameterless invocation, v2.7.1+ behavior) | no error, no warning; back-compat preserved per § C "warning-not-error" rationale; condition lands with default slots (FormID 0 / enum 0 — structurally-valid but always-false, as today) |

---

## Layer 2 — Combinatorial probes

Multi-slot functions, multiple conditions per record, and v2.8.0 × v2.9.0 surface composition (Effects-list cells with v2.9 in-scope conditions).

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | Multi-slot single condition | `add_conditions` on MGEF: one condition for a multi-slot function (Phase 1 picks; canonical: `GetStageDone(Quest, Stage)`) with both slots populated | mods.conditions_added=1; readback both slot properties resolve to supplied values |
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

**Use case.** Real-world dialog patcher: a dialog topic (DIAL/INFO record, surfaced via DialogConditions on the INFO) gates a topic to a specific NPC by adding a `GetIsID` Condition with `Reference` pointing at the target NPC. Today the bridge accepts `function: "GetIsID"` but leaves Reference at FormID 0 — the condition is structurally-valid but functionally always-false, so the dialog never fires for the target NPC. v2.9 lands the Reference slot via the generic dispatcher.

**Target (Phase 3 picks):**
- 1 INFO record from the live modlist with an existing DialogConditions list (or empty — bridge's `add_conditions` works either way).
- 1 NPC_ FormID for the GetIsID Reference slot.

**Operations:**
- `add_conditions` on the INFO record: one ConditionFloat with `function: "GetIsID"`, `operator: "=="`, `value: 1`, `parameters: {Reference: "<plugin>:<localID-of-NPC>"}`.

**Assertions:**
- mods.conditions_added=1.
- Readback `condition.Data.GetType() == GetIsIDConditionData`.
- Readback `condition.Data.Reference.FormKey` matches the supplied NPC FormKey (NOT FormID 0).
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
| `4.slot.03` | In-scope function called with `parameters` containing the right slot name BUT a value of the wrong JSON-type (e.g. `parameters: {Reference: 42}` where Reference is `IFormLinkOrIndex<T>` expecting a FormID string) | record-level error: `"Parameter slot '{slotName}' on function '{function}' expects a {expected-type}; got {actual-type}."` |

### 4.formid — FormID resolution edges inside IFormLinkOrIndex<T>

| # | Setup | Expected |
|---|-------|----------|
| `4.formid.01` | `parameters: {Reference: "Skyrim.esm:DOESNOTEXIST"}` (malformed FormID — non-hex) | record-level error: clean message identifying the malformed FormID and the slot |
| `4.formid.02` | `parameters: {Reference: "NotARealPlugin.esp:0001A696"}` (plugin not in load order) | record-level error: clean message identifying the unresolved plugin |
| `4.formid.03` | `parameters: {Reference: "Skyrim.esm:00FFFFFF"}` (well-formed but record absent) | document behavior: bridge writes the FormKey as supplied; readback shows the FormID; xEdit may flag at runtime but bridge does not validate record existence (matches v2.8.0's FormLink-bonus-catch behavior — write-time, not validate-time) |

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

## Total assertion count (estimate)

| Layer | Pre-spec'd in Phase 0 | Phase 1 fills | Estimate (post-fill) |
|---|---:|---:|---:|
| 1.P.FormLink | scaffold | 7 floor + 0–3 stretch = 7–10 functions × 1–2 carriers each | ~10–18 |
| 1.P.Enum | 1 carryover row + scaffold | 0–N additional family members | ~1–4 |
| 1.P.MultiSlot | scaffold | 0–N functions (Phase 1 inventory dictates) | ~0–3 |
| 1.P.PrimitiveOnly | scaffold | 0–N functions (Phase 1 inventory dictates) | ~0–2 |
| 1.D.in-scope | scaffold | 1 row per in-scope function × parameter shape | ~10–15 |
| **1.D.out-of-scope** | **4 cells (1.D.50–53)** | n/a | **4** |
| 2 — Combinatorial | **6 cells (2.01–2.06)** | n/a (Phase 1 may add multi-slot if exotic surfaces) | 6+ |
| 3 — Workflow scenarios | **2 scenarios (~10 assertions)** | Phase 3 picks FormIDs | ~10 |
| 4.dsl | **3 cells** | n/a | 3 |
| 4.slot | **3 cells** | n/a | 3 |
| 4.formid | **3 cells** | n/a | 3 |
| 4.enum | **3 cells** | n/a | 3 |
| 4.compat | **2 cells** | n/a | 2 |
| 4.carry | **4 cells** | n/a | 4 |
| 5 — Regression | **160 cells (referenced from v2.8.0)** | n/a | 160 |
| **Total** | — | — | **~220–240** |

Phase 2 may dedupe / merge rows where the same code path is exercised twice. The estimate range is wide because the post-Pareto-lock function count is open until Phase 1.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.8.0:

```
[1.P.GetIsID.MGEF]      add_conditions GetIsID    MGEF FirstMgef          PASS (Reference resolved to Skyrim.esm:000A2C8E)
[1.P.HasPerk.PERK]      add_conditions HasPerk    PERK FirstPerk          PASS (Perk resolved to Skyrim.esm:000C44C0)
[1.P.GetActorValue.MGEF] add_conditions GetActorValue MGEF FirstMgef     PASS (ActorValue=Health via generic dispatcher)
[1.D.50]                add_conditions OutOfScopeFunc + parameters       PASS (out-of-scope error rolled back; in-scope set named in error)
[1.D.51]                add_conditions GetIsID + bad SlotName            PASS (no-such-slot error)
[2.01]                  multi-slot GetStageDone(Quest+Stage)             PASS (both slots resolved)
[3.1]                   dialog GetIsID gating                            PASS (Reference resolved on live INFO)
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

## Phase fill-in checklist (Phase 1 hand-back)

Phase 1 closes with these MATRIX edits (per PLAN.md § Phase 1 step 7):

- [ ] Layer 1.P.FormLink — one row per in-scope FormLink-shape function × carrier record type.
- [ ] Layer 1.P.Enum — one row per in-scope enum-shape function (extend ActorValue carryover row + add family members + Sex etc. if locked).
- [ ] Layer 1.P.MultiSlot — one row per in-scope multi-slot function with all slots populated.
- [ ] Layer 1.P.PrimitiveOnly — one row per in-scope primitive-slot function (skip section if no primitive-only functions in lock).
- [ ] Layer 1.D.in-scope — one row per in-scope function for each parameter shape it carries (FormLink → bad FormID; enum → bad name; multi-slot → bad slot 1; primitive → bad type coercion).
- [ ] Confirm Layer 1.D.out-of-scope cells (`1.D.50`–`1.D.53`) accommodate the locked in-scope set wording.
- [ ] Confirm Layer 2 multi-slot cell (`2.01`) names the canonical multi-slot function (likely `GetStageDone` if locked; otherwise pick from Phase 1's surfaced multi-slot list).
- [ ] Confirm Layer 4.slot.03 wording matches the locked exotic-slot status (skip-with-reason if no exotic in Phase 1, or fill in the exotic shape if Aaron expanded scope).
