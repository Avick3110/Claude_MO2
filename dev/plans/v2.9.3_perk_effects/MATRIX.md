# v2.9.3 Verification Matrix

**Authoritative test specification for v2.9.3's verification pass.** Mirrors v2.9.2's MATRIX.md role for v2.9.1 → v2.9.2; this matrix serves the v2.9.2 → v2.9.3 transition. Anchored on **PERK.Effects writability** (the v2.9.3 capability) — Branch A extension to `ConvertJsonElementToListItem` (`PatchEngine.cs:1441`) special-casing `typeof(APerkEffect)` to a new `BuildPerkEffectFromJson` factory near line 2331, mirroring v2.8.0's `typeof(Condition)` → `BuildConditionFromJson` route. Bridge changes are confined to: (a) one `if` arm in `ConvertJsonElementToListItem`; (b) the new factory; (c) PERK addition to v2.8.0's Effects-list carrier set. v2.9.0's per-Condition-function parameter dispatcher (`RouteParameterSlot` + `KnownParameterizedFunctions`) composes UNTOUCHED for nested `PerkEntryPointEffect.PerkConditions[*].Conditions[*].parameters` — Phase 2 verifies this via probe rather than re-implementing.

**Methodology.** Every cell is one bridge invocation (Mutagen-direct functional probe in `tools/race-probe/` for Layer 1 / 1.D / 2 / 4, end-to-end MCP→bridge round-trip in `coverage-smoke/` for the regression band, `mo2_create_patch` + `mo2_record_detail` against the live Authoria modlist for Layer 3), with the listed input parameters against the listed source record(s), and a documented expected response shape. Layers 1 / 2 / 4 / 5 run via the existing test harnesses against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_create_patch` (write test patches) + `mo2_record_detail` (readback verification — no test patches retained) against the live Authoria modlist at `<live>`.

**Layer 1 vs Layer 3 disambiguation.** Layers 1 / 1.D / 2 / 4 are **bridge-mechanism verification on vanilla data** — they run in `tools/race-probe/` (Mutagen-direct functional probes) + `coverage-smoke/` (end-to-end MCP→bridge round-trips) against vanilla Skyrim.esm. The "carrier" FormID in a Layer 1 cell row (e.g. `Skyrim.esm:10FCFA` AugmentedShock60) names the vanilla source record whose binary shape the synthetic patch round-trips through; cells PASS or FAIL based on Mutagen's `CreateFromBinary` → patch → `WriteToBinary` → `CreateFromBinary` readback matching the expected payload. Layer 3 is **live workflow scenarios on the Authoria modlist** — the same vanilla FormID may appear as the canonical anchor (because Requiem-style modlists override vanilla PERK records like AugmentedShock60), but Layer 3 runs through real `mo2_create_patch` + `mo2_record_detail` MCP calls against the live install, and PASS/FAIL hinges on real-install round-trip + ESP cleanup verification. **Layer 1 = bridge correctness on vanilla data; Layer 3 = end-to-end correctness on the live Authoria install.** The FormID overlap is intentional (consistent anchor across release validations) — the test rigs and pass criteria differ.

**Synthetic-vs-vanilla-record carrier choice for Layer 1 / 2 / 4 cells.** Layer 1 / 2 / 4 cells name a vanilla FormID as the default test fixture, but Phase 2 (when actually wiring `coverage-smoke/Program.cs`) may swap to an in-memory synthetic record per cell if the synthetic simplifies the test rig (mirrors v2.9.2 P4's synthetic missing-master fixture pattern at `4.dsl.06`). The MATRIX cell spec is the test contract; the test fixture realization is Phase 2's call.

**Record selection.** Layer 1 / 2 / 4 anchor on **PERK** as the carrier record type. Phase 1's audit (the `tools/race-probe/Program.cs` v2.9.3 P1 inventory section) produces the authoritative `APerkEffect` concrete subclass list and per-subclass property surface. Phase 0 anchors on three subclasses **provisionally** per the user's task spec + PLAN.md § B/E read-side observation: `PerkEntryPointEffect` (the dominant on-disk shape, carries `EntryPoint` enum + `Modification` + `Value` + nested `PerkConditions`), `PerkAbility` (carries an `Ability` FormLink to SPEL), `PerkQuestEffect` (carries a `Quest` FormLink + `Stage` Int32). **Phase 1 confirms the actual concrete-subclass set** and adds `1.P.<Subclass>.<sub>` rows per additional concrete subclass enumerated. The PERK anchor record for the Layer 1.P.PerkEntryPointEffect cells is **AugmentedShock60** (`Skyrim.esm:10FCFA`) — single `PerkEntryPointEffect` with `EntryPoint=ModSpellMagnitude`, `Modification=Multiply`, `Value=1.5`, one nested `PerkCondition` group on tab index 1 with one `GetActorValue` ConditionFloat — matches PLAN.md § Background's read-side render exemplar. Phase 1 may swap if the audit surfaces a better-fit anchor.

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in the appropriate phase's handoff, including the actual response payload.

**Phase fill-in cadence.** **Phase 0 (this commit)** lays down the layer scaffold + cell-naming convention + Layer 3 scenario use-case description. Per-subclass rows are placeholders awaiting Phase 1's audit. **Phase 1** runs the `APerkEffect` inventory probe + record-shape sweep, lands the audit at `<plan>/APERK_EFFECTS_AUDIT.md`, and substitutes confirmed subclass names + adds rows for each additional concrete subclass discovered. Phase 2 wires the bridge + Python schema, lands per-subclass coverage-smoke cells (Tests 426–N), and finalizes Layer 1.D error wording. Phase 3 picks live FormIDs for the Layer 3 scenario(s). Phase 4 (conditional) lands matrix corrections per surfaced bugs.

---

## 🧭 Cell-naming convention

| Prefix | Layer | Pattern | Example |
|---|---|---|---|
| `1.P.<Subclass>.<sub>` | 1 — per-subclass positives | concrete `APerkEffect` subclass + sub-shape descriptor | `1.P.PerkEntryPointEffect.minimal`, `1.P.PerkEntryPointEffect.with_perk_conditions`, `1.P.PerkAbility.basic` |
| `1.D.<NN>` | 1.D — negatives + new explicit error paths | sequential within layer | `1.D.01`, `1.D.07` |
| `2.<NN>` | 2 — combinatorial | sequential | `2.01`, `2.04` |
| `3.<N>` | 3 — workflow scenarios | scenario number | `3.1`, `3.2` |
| `4.<sub>.<NN>` | 4 — edges | sub-grouping + sequential | `4.dsl.01`, `4.dsl.05` |
| `5.range` | 5 — regression | range row, mapped to v2.9.2's 425 cells (see § Layer 5) | `5.range` |

The `1.P.<Subclass>.<sub>` form anchors on **concrete subclass** (the v2.9.3 unit of work — one branch in the discriminator-routed factory) — different from v2.9.2's per-axis anchor (the read-side mechanism's unit was the optional parameter; v2.9.3's write-side mechanism's unit is the concrete subclass dispatched). `1.D.<NN>` carries v2.9.2's negative-band convention forward. Layer 4's only sub-grouping is `dsl` (parameter-value-form edges + DSL-shape edges) — v2.9.0/v2.9.1's other Layer 4 sub-groups (`slot` / `formid` / `enum` / `compat` / `carry`) don't apply here because v2.9.3 doesn't change the per-Condition or per-FormLink build pipeline; it only adds a new Branch A subclass-routed factory.

**Per-subclass carrier convention (Phase 0 baseline; Phase 1 confirms).** Layer 1.P's primary subclass is `PerkEntryPointEffect` (the dominant on-disk shape per PLAN.md § E read-side observation — three sub-shape cells: minimal / with_perk_conditions / with_v290_params). Layer 1.P also exercises `PerkAbility` (basic FormLink-to-SPEL shape) and `PerkQuestEffect` (basic FormLink-to-QUST + Stage shape). Phase 1's audit may add additional concrete subclasses; each gets a `1.P.<Subclass>.basic` row at minimum, with sub-shape rows added if the subclass's property surface warrants discriminator coverage.

**Source-of-truth for subclass names + property surfaces (Phase 1 deliverable, Mutagen 0.53.1):** Phase 1's `tools/race-probe/Program.cs` v2.9.3 P1 inventory section reflects over `IAPerkEffectGetter`-implementing concrete classes in `Mutagen.Bethesda.Skyrim` 0.53.1; enumerates Activator constructibility per subclass, dumps per-subclass property surface with `[base]` / `[subclass-specific]` annotation, and confirms `PerkConditions` element type (concrete `PerkCondition` LoquiObject per Phase 0 § C default, vs surprise-abstract). Phase 0 baseline anchors:
- `PerkEntryPointEffect` — `EntryPoint` enum (~140 distinct values per PLAN.md § Background), `Modification`/`Function` enum (SetValue / AddValue / MultiplyValue / etc.), `Value` Float, `PerkConditionTabCount` Int32, `Rank` Int32, `Priority` Int32, `PerkConditions` `ExtendedList<PerkCondition>`, `Flags` struct.
- `PerkAbility` — `Ability` FormLink to `ISpellGetter`. (Provisional shape — Phase 1 confirms.)
- `PerkQuestEffect` — `Quest` FormLink to `IQuestGetter`, `Stage` Int32. (Provisional shape — Phase 1 confirms.)
- `PerkCondition` (Phase 0 § C wrapper-object DSL anchor; Q7 default) — `RunOnTabIndex` Int32, `Conditions` `ExtendedList<Condition>`. Phase 1 confirms concrete vs abstract.

---

## Layer 1 — Per-subclass coverage (positives)

**v2.9.3 in-scope subclasses (Phase 0 baseline):** `PerkEntryPointEffect`, `PerkAbility`, `PerkQuestEffect`. Phase 1's audit confirms the authoritative set; each additional concrete subclass discovered gets a `1.P.<Subclass>.basic` row (and optional sub-shape rows if its property surface warrants). Layer 1 cells exercise each subclass's primary success path on canonical PERK records. Per-subclass + DSL combinatorial composition lives in Layer 2.

Each row's expected result follows the shape:

> `mo2_create_patch` → bridge response top-level `success: true`; readback via `mo2_record_detail` confirms PERK record's `Effects` array contains the requested concrete-subclass entries with the requested property values; v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 single-record / batch / projection / expansion paths bit-identical when only `set_fields: {Effects: [...]}` is supplied (composition proven cell-by-cell).

### 1.P.PerkEntryPointEffect — dominant on-disk shape

| # | Subclass | Carrier | Operation | Expected |
|---|----------|---------|-----------|----------|
| `1.P.PerkEntryPointEffect.minimal` | `PerkEntryPointEffect` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 0, Rank: 0, Priority: 0}]}` (no nested PerkConditions) | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkEntryPointEffect`; `EntryPoint = ModSpellMagnitude`, `Modification = Multiply`, `Value = 1.4`, `PerkConditions.Count = 0`. Verifies factory dispatches to concrete subclass + Activator-creates + per-property recursion sets scalar + enum fields correctly. **Phase 1 may swap anchor if AugmentedShock60's shape changes.** |
| `1.P.PerkEntryPointEffect.with_perk_conditions` | `PerkEntryPointEffect` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "GetActorValue", operator: ">=", value: 60, parameters: {ActorValue: "Destruction"}}]}]}]}` | symmetric to `1.P.PerkEntryPointEffect.minimal`; additionally readback confirms `PerkConditions.Count = 1`, entry's `RunOnTabIndex = 1`, nested `Conditions.Count = 1`, condition's `Data.Function = GetActorValue` + `ComparisonValue = 60` + `CompareOperator = GreaterThanOrEqualTo` + `Data.ActorValue = Destruction`. Verifies wrapper-object DSL per Q7 default, nested `BuildConditionFromJson` route via Branch A's `typeof(Condition)` special case, v2.9.0's `RouteParameterSlot` enum-slot dispatch on `ActorValue`. |
| `1.P.PerkEntryPointEffect.with_v290_params` | `PerkEntryPointEffect` | `Skyrim.esm:10FCFA` (AugmentedShock60) | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", operator: "==", value: 1, parameters: {Perk: "Skyrim.esm:058200"}}]}]}]}` | symmetric to `1.P.PerkEntryPointEffect.with_perk_conditions`; the nested condition uses a v2.9.0-dispatcher-routed parameter (`HasPerk` + `parameters: {Perk: <FormLink>}`); readback confirms condition's `Data.Reference` resolves to the supplied perk FormLink. **Composition probe** — verifies v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` compose UNTOUCHED for nested PerkConditions per § F default. Cell expectation locks: composition holds, no Phase 2 dispatcher code change. |

### 1.P.PerkAbility — FormLink-to-SPEL shape

| # | Subclass | Carrier | Operation | Expected |
|---|----------|---------|-----------|----------|
| `1.P.PerkAbility.basic` | `PerkAbility` | (Phase 1 picks vanilla PERK with `PerkAbility` effect — likely a Werewolf/Vampire perk; Phase 0 placeholder) | `set_fields: {Effects: [{type: "PerkAbility", Ability: "Skyrim.esm:0CF788"}]}` (placeholder Ability FormLink — Phase 1 confirms canonical) | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkAbility`; `Ability` resolves to the supplied SPEL FormLink. Verifies factory dispatches to a non-`PerkEntryPointEffect` subclass + Branch B FormLink set works on a non-base property. **Phase 1 confirms PerkAbility shape + picks anchor.** |

### 1.P.PerkQuestEffect — FormLink-to-QUST + Stage shape

| # | Subclass | Carrier | Operation | Expected |
|---|----------|---------|-----------|----------|
| `1.P.PerkQuestEffect.basic` | `PerkQuestEffect` | (Phase 1 picks vanilla PERK with `PerkQuestEffect` effect; Phase 0 placeholder) | `set_fields: {Effects: [{type: "PerkQuestEffect", Quest: "Skyrim.esm:<placeholder>", Stage: 100}]}` | top-level `success: true`; readback confirms `Effects.Count = 1`; entry's concrete type is `PerkQuestEffect`; `Quest` resolves to the supplied QUST FormLink; `Stage = 100`. Verifies factory dispatches to a third subclass + per-property recursion sets a FormLink + Int32 in one call. **Phase 1 confirms PerkQuestEffect shape + picks anchor + canonical Stage value.** |

**Phase 1 extension placeholder.** If Phase 1's audit enumerates additional concrete `APerkEffect` subclasses beyond the three above, append `1.P.<Subclass>.basic` rows here with the canonical property surface per the audit. Per Q2 default (PLAN § E), v2.9.3 ships every concrete subclass — no Pareto trim.

---

## Layer 1.D — Negatives + new explicit error paths

Seven cells exercising the new discriminator validation surface from PLAN § A factory + § B Q1 lock + the existing v2.8.0 carrier-set rejection path. Multi-error accumulation is **not** a structural contract here (single-record set_fields is one bridge invocation; first-failure-wins is acceptable per v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 precedent on Branch A factories). Wording for the new error messages is finalized in Phase 2 implementation; this matrix locks the shape.

| # | Axis | Setup | Expected |
|---|------|-------|----------|
| `1.D.01` | discriminator unknown | `set_fields: {Effects: [{type: "BogusType", EntryPoint: "ModSpellMagnitude"}]}` against PERK | top-level `success: false`; per-record `error: "PerkEffect type 'BogusType' not found in Mutagen.Bethesda.Skyrim namespace"` (or equivalent — Phase 2 finalizes wording to match v2.8.0's `BuildConditionFromJson` "function not found" pattern); response names the valid concrete subclass set per Phase 1 audit (`PerkEntryPointEffect`, `PerkAbility`, `PerkQuestEffect`, plus any additional Phase 1 subclasses); rollback: source PERK's `Effects` is unchanged on disk |
| `1.D.02` | discriminator is abstract base | `set_fields: {Effects: [{type: "APerkEffect", EntryPoint: "ModSpellMagnitude"}]}` against PERK (the abstract base class supplied as type) | top-level `success: false`; per-record `error: "PerkEffect type 'APerkEffect' is abstract — must specify a concrete subclass"` (or equivalent); response names the valid concrete subclass set per Phase 1 audit; rollback identical |
| `1.D.03` | missing discriminator | `set_fields: {Effects: [{EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}]}` against PERK (no `type:` key) | top-level `success: false`; per-record `error: "PerkEffect entry missing required 'type' discriminator field"` (or equivalent); response names the valid concrete subclass set; rollback identical |
| `1.D.04` | nested condition v2.9.0-out-of-scope | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "<v2.9.0-not-yet-wired-fn>", parameters: {SomeSlot: "x"}}]}]}]}` against PERK (Phase 2 picks an actual v2.9.0-out-of-scope function — e.g. a Boolean-dispatcher-branch function or a sub-B 6 String-typed function) | top-level `success: false`; per-record error: v2.9.0's existing "function not yet wired" error from `RouteParameterSlot` (existing code path; v2.9.3 doesn't change v2.9.0 dispatcher). Verifies composition unchanged: v2.9.3's nested PerkConditions route to the same v2.9.0 dispatcher; v2.9.0's coverage gap surfaces in v2.9.3-context the same way it does in top-level Conditions / Effects.Conditions context. Rollback identical |
| `1.D.05` | unknown property on PerkEffect | `set_fields: {Effects: [{type: "PerkEntryPointEffect", BogusField: "x"}]}` against PERK | top-level `success: false`; per-record error from `SetPropertyByPath` "unknown property 'BogusField' on PerkEntryPointEffect" (existing Branch B / Tier C-shape error path, unchanged by v2.9.3). Rollback identical |
| `1.D.06` | non-carrier record | `set_fields: {Effects: [{type: "PerkEntryPointEffect", ...}]}` against an NPC_ record (a record type that is neither in v2.8.0's carrier set NOR PERK) | top-level `success: false`; per-record error: existing v2.8.0 carrier-rejection ("Effects array not supported on record type 'NPC_' — supported: SPEL, ALCH, ENCH, SCRL, INGR, PERK"). Verifies v2.9.3's PERK addition didn't break the gating on other record types. Rollback identical |
| `1.D.07` | type mismatch on non-PERK record (defensive) | `set_fields: {Effects: [{type: "PerkEntryPointEffect"}]}` against a SPEL record (where Effects-list is gated to concrete `Effect`, not `APerkEffect`) | top-level `success: false`; per-record error: would not normally reach the discriminator path because the SPEL Effects-list is already routed via the v2.8.0 concrete `Effect` Activator path (no discriminator needed); the `type:` key gets treated as an unknown property on `Effect` and surfaces as a `SetPropertyByPath` error (or similar). Cell verifies the gating remains correct — supplying `type:` on a v2.8.0 carrier doesn't accidentally route to `BuildPerkEffectFromJson`. Rollback identical |

Phase 2 may add Layer 1.D rows programmatically if test patterns surface (e.g. a per-subclass missing-required-field variant if Phase 1's audit identifies subclass-required properties; or a non-FormLink type-coercion error if Phase 2 implementation surfaces one). The matrix locks the structural error-path coverage above; bulk-pattern derivatives are implementation choice for the harness.

---

## Layer 2 — Combinatorial probes

Cross-subclass composition: heterogeneous concrete subclasses in one Effects array, plus replace-semantics composition with PERK top-level scalar writes, plus full-stack composition (Branch A → factory → wrapper → Condition factory → v2.9.0 dispatcher), plus empty-array clear (mirrors v2.8.0 Test 29 / 1.E.07).

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | heterogeneous subclasses in one array | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}, {type: "PerkAbility", Ability: "Skyrim.esm:<placeholder>"}, {type: "PerkQuestEffect", Quest: "Skyrim.esm:<placeholder>", Stage: 100}]}` against a PERK record (Phase 1 picks anchor; provisionally a Werewolf/Vampire perk that already has multi-subclass effects) | top-level `success: true`; readback confirms `Effects.Count = 3`; entry-by-entry concrete types match the requested discriminators (`PerkEntryPointEffect`, `PerkAbility`, `PerkQuestEffect`); per-entry property values match. Verifies the factory dispatches per-element correctly + the resulting `ExtendedList<APerkEffect>` carries heterogeneous concrete entries (Mutagen's polymorphism preserves per-entry types) |
| `2.02` | replace-semantics + Tier C scalar coexistence | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}], Level: 25, NumRanks: 3, Trait: true}` against `Skyrim.esm:10FCFA` (AugmentedShock60) | top-level `success: true`; readback confirms `Effects.Count = 1` (replaced) AND PERK top-level scalars `Level = 25`, `NumRanks = 3`, `Trait = true` set correctly. Verifies v2.9.3's Effects-array replace-semantics composes with v2.7.x's Tier C scalar set_fields path on the same record. Sibling fields outside the supplied set_fields keys (e.g. PERK.Description, PERK.Conditions, PERK.PerkSection) are preserved (Branch B in-place merge invariant for the top-level dict, Effects-array replace-semantics for Effects only) |
| `2.03` | full-stack composition (Branch A → factory → wrapper → Condition factory → v2.9.0 dispatcher) | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", operator: "==", value: 1, parameters: {Perk: "Skyrim.esm:058200"}}]}]}]}` against `Skyrim.esm:10FCFA` | top-level `success: true`; readback confirms full-stack write: `Effects[0]` is `PerkEntryPointEffect` with `Value = 1.4` AND `PerkConditions[0]` is concrete `PerkCondition` wrapper with `RunOnTabIndex = 1` AND nested `Conditions[0]` is concrete `Condition` (v2.8.0 BuildConditionFromJson route) with `Data.Function = HasPerk` AND `Data.Reference` resolves to the supplied perk FormLink (v2.9.0 RouteParameterSlot route). **Single cell exercising every layer of the v2.8.0 + v2.9.0 + v2.9.3 composed write surface.** |
| `2.04` | empty-array clear | `set_fields: {Effects: []}` against `Skyrim.esm:10FCFA` (AugmentedShock60) | top-level `success: true`; readback confirms `Effects.Count = 0`. Verifies v2.9.3's PERK Effects-array replace-semantics matches v2.8.0's empty-clear posture (Test 29 / 1.E.07) on the existing carrier set. PERK top-level scalar fields (Level, NumRanks, Trait, Description, Conditions, PerkSection) are preserved untouched (sibling-preservation invariant) |

---

## Layer 3 — Workflow scenario(s) on live install

Run via `mo2_create_patch` (write test patches) + `mo2_record_detail` (readback verification) against the live Authoria modlist. Output: per-scenario assertion table + readback evidence in `PHASE_3_HANDOFF.md`. Test ESPs go to `<modlist>/mods/Claude Output/`, deleted post-verification per `Claude_MO2/CLAUDE.md` § live install sync.

**Phase 0 pre-specs use case + assertions; Phase 3 picks live FormIDs at execution time.** Aaron may swap the named record during Phase 3 if Phase 1's audit + the live Authoria modlist surface a better Authoria fit (e.g. if a different Requiem perk override is more representative of the rebalancing workflow shape).

### Scenario 3.1 — Requiem-style PERK magnitude rebalance (mandatory)

**Use case.** Real-world AI-driven patcher: an Authoria tester rebalancing Requiem's perk magnitudes — change `ModSpellMagnitude` from 1.5× to 1.4× on AugmentedShock60 (`Skyrim.esm:10FCFA`), AugmentedFrost60, AugmentedFlames60, etc. Today: blocked — `set_fields: {Effects: [...]}` against PERK is rejected by the v2.8.0 carrier-set check. v2.9.3 unblocks: a single `mo2_create_patch` call with `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditionTabCount: 3, PerkConditions: [<existing tabs preserved>]}]}` rewrites the Effects array with the new magnitude. The PerkConditions structure (RunOnTabIndex tabs + nested Conditions per tab) is read first via `mo2_record_detail` (v2.9.2 mechanism) and round-tripped into the write payload.

**Target (Phase 3 picks at execution):**
- **Anchor record:** AugmentedShock60 (`Skyrim.esm:10FCFA`) — single `PerkEntryPointEffect` with `EntryPoint = ModSpellMagnitude`, `Modification = Multiply`, `Value = 1.5`, one nested `PerkCondition` group on tab index 1 with one `GetActorValue` ConditionFloat (per PLAN.md § Background read-side render). Phase 3 confirms shape via pre-write `mo2_record_detail`.
- **Override target:** patch ESP at `<modlist>/mods/Claude Output/v2.9.3-test-perk-rebalance.esp` (or analogous; deleted post-verification).

**Operations:**
- Pre-write read: `mo2_record_detail(formid: "Skyrim.esm:10FCFA", expand_links: ["Effects"])` (v2.9.2 mechanism — get current Effects array for round-trip basis).
- Write: `mo2_create_patch(plugin: "v2.9.3-test-perk-rebalance.esp", overrides: [{formid: "Skyrim.esm:10FCFA", set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, <PerkConditions: [...] from pre-write read, preserved>}]}}])`.
- Post-write readback: `mo2_record_detail(formid: "<patch-esp>:10FCFA", expand_links: ["Effects"])`.

**Assertions:**
- Pre-write read returns `Effects.Count = 1`, `Effects[0].EntryPoint = ModSpellMagnitude`, `Effects[0].Value = 1.5` (or live actual — Phase 3 documents).
- Write call returns top-level `success: true`; per-record success: true.
- Post-write readback confirms patch ESP carries an override for the PERK with `Effects.Count = 1`, `Effects[0].EntryPoint = ModSpellMagnitude`, `Effects[0].Value = 1.4`, `Effects[0].PerkConditions` round-trip-identical to pre-write.
- All v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 single-record / batch / projection / expansion paths bit-identical when called against any non-PERK record — Phase 3 spot-checks one representative call against an Authoria SPEL or RACE without `Effects` writes; the response matches the v2.9.2 response shape.
- Test ESP cleanup: post-verification, the patch ESP is deleted; the live install state is restored.

### Scenario 3.2 — Optional secondary scenario: multi-effect PERK preserving subclass mix

**Use case.** A symmetric scenario exercising the heterogeneous-subclass composition on a real PERK. PlayerWerewolfFeed (`Skyrim.esm:02BA1D`, or analogous Werewolf/Vampire-style perk) is the canonical anchor — these perks carry a mix of `PerkEntryPointEffect` (`Activate` entry-point modifying activation behavior) + `PerkAbility` (granted spells/abilities) + occasionally `PerkQuestEffect` entries. The patcher rewrites the Effects array preserving the subclass mix but adjusting one entry's property (e.g. swap a PerkAbility's granted Spell to a different one, or change a PerkEntryPointEffect's Value). Verifies the heterogeneous-subclass write surface on a real-world PERK shape, not a synthetic one.

**Phase 3 conditional execution.** Phase 3 confirms PlayerWerewolfFeed (or whatever Authoria-side analog) actually carries a multi-subclass Effects array via pre-write `mo2_record_detail`. If the picked anchor only carries one subclass (e.g. all `PerkEntryPointEffect` entries), Phase 3 picks a different multi-subclass PERK from the live modlist or documents the skip with reason.

**Operations:**
- Pre-write read: `mo2_record_detail(formid: "Skyrim.esm:02BA1D", expand_links: ["Effects"])`.
- Write: `mo2_create_patch(plugin: "v2.9.3-test-perk-multisubclass.esp", overrides: [{formid: "Skyrim.esm:02BA1D", set_fields: {Effects: [<round-trip array with one entry's property adjusted>]}}])`.
- Post-write readback: `mo2_record_detail(formid: "<patch-esp>:02BA1D", expand_links: ["Effects"])`.

**Assertions:**
- Symmetric to Scenario 3.1: per-subclass round-trip preservation across the heterogeneous Effects array.
- Specifically verifies factory dispatches per-element on a real-world PERK (not a synthetic test fixture); the resulting `ExtendedList<APerkEffect>` carries the same concrete-subclass mix as pre-write, with the adjusted entry's property reflecting the write payload.
- Test ESP cleanup: post-verification, the patch ESP is deleted.

---

## Layer 4 — Edges

DSL-form edges of the new `Effects: [...]` payload's value forms + cross-master FormLink composition + sibling-preservation invariants. v2.9.3's mechanism doesn't change v2.8.0's per-Effects-list dispatch on the existing 5 carriers, v2.9.0's per-Condition-function dispatch, v2.9.1's QUST condition disambiguation, or v2.9.2's read-side mechanism — those surfaces stay exercised via Layer 5 regression. v2.9.3's new edges are PERK-specific factory + DSL + cross-master + sibling.

### 4.dsl — DSL-shape + cross-master + sibling edges

| # | Setup | Expected |
|---|-------|----------|
| `4.dsl.01` | Round-trip: write Effects via `set_fields: {Effects: [...]}`, then read back via `mo2_record_detail` against the same patch ESP | response payload shape matches v2.9.2's read-side render exactly: `Effects` is a list of dict entries, each entry's keys match the concrete subclass's property surface (e.g. `PerkEntryPointEffect` entries carry `EntryPoint` + `Modification` + `Value` + `PerkConditions` + `PerkConditionTabCount` + `Rank` + `Priority` + `Flags`; `PerkAbility` entries carry `Ability`; `PerkQuestEffect` entries carry `Quest` + `Stage`). Verifies the v2.9.3 write path produces a record whose v2.9.2 read-side render is identical to a vanilla PERK with the same effect shape — write/read symmetry invariant |
| `4.dsl.02` | Cross-master FormLink in nested condition (`parameters: {Perk: "OtherEsp:01ABCD"}` where `OtherEsp.esp` is a synthetic master with a known Perk FormLink) | top-level `success: true`; readback confirms the nested condition's `Data.Reference` carries a compacted FormLink (v2.6.0's `WithLoadOrder` writes ESL-flagged-master-aware compacted FormIDs). Verifies v2.6.0's ESL-flagged FormLink writability composes with v2.9.3's new nested-condition write path. **Phase 2 builds synthetic two-plugin fixture if one isn't already in coverage-smoke** (mirrors v2.9.2 P4's synthetic missing-master fixture pattern at `4.dsl.06`) |
| `4.dsl.03` | Enum parse error on PerkEntryPointEffect.EntryPoint | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "BogusEntryPoint", Modification: "Multiply", Value: 1.4}]}` against `Skyrim.esm:10FCFA` | top-level `success: false`; per-record error from v2.9.0's enum branch in `RouteParameterSlot` / Branch B's enum-set: "EntryPoint value 'BogusEntryPoint' is not a valid PerkEntryPointType — valid values: ModSpellMagnitude, ModBowDamage, …" (existing code path; verifies enum dispatch works on `PerkEntryPointEffect.EntryPoint` same as on `ConditionData` enum slots). Rollback identical |
| `4.dsl.04` | Empty PerkConditions list | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4, PerkConditions: []}]}` against `Skyrim.esm:10FCFA` | top-level `success: true`; readback confirms `Effects[0].PerkConditions.Count = 0`. Verifies non-failure on empty nested list (mirrors v2.8.0 Test 29 empty-array clear, scoped to the nested PerkConditions list) |
| `4.dsl.05` | Sibling preservation invariant | `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.4}]}` against `Skyrim.esm:10FCFA` (no Description / Conditions / NumRanks / Level / PerkSection write) | top-level `success: true`; readback confirms `Effects` was REPLACED (v2.9.3 replace-semantics on the array); ALSO confirms PERK top-level Description, Conditions, NumRanks, Level, PerkSection sibling fields are UNTOUCHED (carry through from the source record). Verifies array-replace-semantics doesn't bleed into top-level scalars; Branch B in-place merge invariant on the dict + Effects-array replace-semantics on Effects only |

---

## Layer 5 — Regression band

All v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 coverage-smoke cells run unchanged. v2.9.3 must not regress any prior behavior — the new PERK Effects-array path is purely additive (PERK was previously rejected by the v2.8.0 carrier-set check; v2.9.3 adds it to the carrier set).

| Cell range | Source | Expected |
|---|---|---|
| `5.range` | `dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` Layer 1.P + 1.D + 2 + 4 + 5 (**425 v2.9.2 cells confirmed via coverage-smoke** post-Phase-4 — 382 v2.9.0 + 18 v2.9.1 + 24 v2.9.2 P2 + 1 v2.9.2 P4 cross-master positive; 6 SKIP-with-reason) | each cell PASS as it did in v2.9.2 P5; Phase 2 adds N new cells (Tests 426–N — count locked at Phase 1 audit + Phase 2 implementation; per-subclass coverage-smoke + multi-error + composition cells per Layer 1.P/1.D/2/4); Phase 4 (conditional) flips any SKIP→PASS or adds positive cells per surfaced bugs. **Post-Phase-2 total = 425 + N cells** (Phase 1 audit determines N; baseline = 7 Layer 1.P + 7 Layer 1.D + 4 Layer 2 + 5 Layer 4 = 23 new cells minimum, plus per-additional-subclass cells from Phase 1 audit) |

Specifically: every v2.8.0 `set_fields: {Effects: [...]}` invocation pattern on SPEL/ALCH/ENCH/SCRL/INGR stays bit-identical (the pre-existing carrier set is unaffected by adding PERK to it; the discriminator `type:` key is absent on those carriers, so Branch B's existing concrete-`Effect` Activator path stays untouched). Every v2.9.0 condition-parameter dispatcher pattern stays untouched. Every v2.9.1 QUST condition disambiguation pattern stays untouched. Every v2.9.2 read-side mechanism (`formids` / `fields` / `expand_links`) on `mo2_record_detail` stays bit-identical. This is the core back-compat assertion of v2.9.3.

---

## Total assertion count (Phase 0 baseline; Phase 1 finalizes)

**v2.9.3 capability surface is one Branch A factory + N concrete subclasses Phase 1 enumerates.** No new operator, no new tool, no new bridge command. Phase 0 baselines on three subclasses (PerkEntryPointEffect, PerkAbility, PerkQuestEffect); Phase 1's audit adds rows per additional concrete subclass discovered.

| Layer | Matrix rows (Phase 0 baseline) | Harness cells (Phase 0 baseline) | Source |
|---|---:|---:|---|
| 1.P (per-subclass positives) | 5 (3 PerkEntryPointEffect sub-shapes + 1 PerkAbility + 1 PerkQuestEffect) | 5 + Phase 1 additions | this doc |
| 1.D (negatives + new explicit error paths) | 7 | 7 | this doc |
| 2 (combinatorial) | 4 | 4 | this doc |
| 3 (workflow scenarios) | 1 mandatory + 1 conditional (3.1 Requiem perk rebalance, 3.2 multi-effect PERK — Phase 3 confirms preconditions) | ~6–10 assertions | this doc; Phase 3 picks live FormIDs |
| 4.dsl | 5 | 5 | this doc |
| 5 (regression) | 1 (range row) | 425 (v2.9.2 baseline) | v2.9.2 baseline |
| **Total** | **~23 matrix rows + Phase 1 additions** | **~452 + Phase 1 additions harness cells** | — |

Phase 2 may dedupe or merge cells where the same code path is exercised twice. v2.9.2's MATRIX.md is the source of truth for the Layer 5 regression count; Phase 2 reads from `coverage-smoke/Program.cs`'s actual cell enumeration rather than from this matrix doc when running the full regression band.

**Extensibility note.** If Phase 1's audit enumerates additional concrete `APerkEffect` subclasses (Pareto-default per Q2 lock = ship full set), Layer 1.P extends with one `1.P.<Subclass>.basic` row per additional subclass. Layer 1.D's structural error rows generalize unchanged (the discriminator-not-found / abstract-base / missing-discriminator validation paths apply to any concrete subclass surface). Phase 1's handoff documents the audit findings; Phase 1 or Phase 2 extends the matrix accordingly.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.9.2:

```
[1.P.PerkEntryPointEffect.minimal]      mo2_create_patch PERK Effects=[PerkEntryPointEffect]    PASS (1 effect; concrete type matches; Value=1.4)
[1.P.PerkEntryPointEffect.with_perk_conditions]  mo2_create_patch PERK Effects + nested PerkConditions  PASS (1 effect; 1 PerkCondition tab; 1 nested Condition; v2.9.0 enum-slot dispatch routed)
[1.P.PerkEntryPointEffect.with_v290_params]      mo2_create_patch PERK Effects + nested HasPerk param   PASS (composition probe; v2.9.0 RouteParameterSlot routed FormLink)
[1.P.PerkAbility.basic]                  mo2_create_patch PERK Effects=[PerkAbility]            PASS (1 effect; concrete type matches; Ability FormLink resolved)
[1.P.PerkQuestEffect.basic]              mo2_create_patch PERK Effects=[PerkQuestEffect]        PASS (1 effect; concrete type matches; Quest FormLink + Stage)
[1.D.01]                                 mo2_create_patch type='BogusType'                       PASS (discriminator-not-found error; rolled back)
[1.D.02]                                 mo2_create_patch type='APerkEffect' (abstract)          PASS (abstract-base rejection; rolled back)
[1.D.03]                                 mo2_create_patch missing 'type' key                     PASS (missing-discriminator error; rolled back)
[1.D.06]                                 mo2_create_patch Effects on NPC_                        PASS (carrier-rejection error preserved; rolled back)
[2.01]                                   heterogeneous subclasses in one Effects array           PASS (3 effects; concrete-type-per-element preserved)
[2.03]                                   full-stack composition (Branch A → factory → Condition factory → v2.9.0)  PASS (every layer composed)
[2.04]                                   Effects: [] empty-clear on PERK                         PASS (Effects.Count=0; sibling fields preserved)
[3.1]                                    live: Requiem perk magnitude rebalance (AugmentedShock60)  PASS (1.5 → 1.4; readback matches; ESP cleanup verified)
[4.dsl.01]                               write/read symmetry round-trip                          PASS (v2.9.2 read-side render matches vanilla shape)
[4.dsl.05]                               sibling preservation invariant                          PASS (Effects replaced; top-level scalars untouched)
[5.range]                                v2.9.2 regression band                                  ~425/~425 PASS
```

Failures embed enough context for handoff to lift into the bug list directly. Per-cell PASS/FAIL is the harness contract; per-subclass assertions are inlined in each Layer 1 / 2 cell's PASS string.

---

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a record meeting the test fixture requirements (e.g. anchor needs PERK with a populated `PerkAbility` effect entry and the picked anchor lacks one), the harness prints:

```
[1.P.<Subclass>.<sub>]  PERK Effects <none-meeting-fixture>  SKIP: anchor PERK lacks <Subclass> entry populated
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture (build a synthetic PERK in-memory via Mutagen) or accept the gap. Phase 1's audit identifies vanilla PERKs with each canonical subclass populated; if no such vanilla PERK exists for a given subclass, Phase 1's handoff names the gap and Phase 2 falls back to a synthetic-PERK fixture pattern (mirrors v2.9.2 P4's `4.dsl.06` synthetic missing-master fixture pattern).

---

## Phase fill-in checklist (Phase 1 hand-back)

Phase 1 closes with these MATRIX edits landed:

- [ ] **Concrete `APerkEffect` subclass enumeration** — Phase 1's `tools/race-probe/Program.cs` v2.9.3 P1 inventory section reflects over `IAPerkEffectGetter`-implementing concrete classes in `Mutagen.Bethesda.Skyrim` 0.53.1; produces the authoritative list. Substitute Phase 0 placeholders: confirm `PerkEntryPointEffect` / `PerkAbility` / `PerkQuestEffect` are concrete (or rename per actual Mutagen subclass names); add `1.P.<Subclass>.basic` rows for each additional concrete subclass discovered. Document the full sweep in `<plan>/APERK_EFFECTS_AUDIT.md`.
- [ ] **Activator constructibility per subclass** — confirm each concrete subclass has a parameterless ctor that Activator can construct (mirrors v2.8.0 `Effect` shape). If any subclass requires a non-parameterless ctor or carries a no-default-value-required-property, surface in audit + handoff + escalate Q2 to conductor (Pareto-defer that subclass).
- [ ] **Per-subclass property surface dump** — each subclass's full property list with `[base]` / `[subclass-specific]` annotation per CONDITIONS_AUDIT.md format. Substitute Phase 0 placeholder shapes (PerkAbility's `Ability`, PerkQuestEffect's `Quest` + `Stage`) with audit-confirmed actual property names + types.
- [ ] **`PerkConditions` element type confirmation** — confirm `PerkEntryPointEffect.PerkConditions` element type is concrete `PerkCondition` LoquiObject (Phase 0 § C default); if abstract, escalate Q7 to conductor for re-architect.
- [ ] **PERK anchor FormID for Layer 1.P.PerkEntryPointEffect** — confirm AugmentedShock60 (`Skyrim.esm:10FCFA`) qualifies (single PerkEntryPointEffect with the canonical EntryPoint + nested PerkCondition shape per PLAN.md § Background); document, or substitute if Phase 1 finds a cleaner anchor.
- [ ] **PERK anchor FormID for Layer 1.P.PerkAbility** — Phase 0 placeholder; Phase 1 picks from vanilla PERKs with PerkAbility entries (likely Werewolf/Vampire perk family per PLAN.md § E read-side observation).
- [ ] **PERK anchor FormID for Layer 1.P.PerkQuestEffect** — Phase 0 placeholder; Phase 1 picks from vanilla PERKs with PerkQuestEffect entries (likely Standing Stone perks per PLAN.md § E read-side observation).
- [ ] **Heterogeneous-subclass anchor for Layer 2.01** — Phase 0 placeholder PlayerWerewolfFeed (`Skyrim.esm:02BA1D`); Phase 1 confirms it actually carries a multi-subclass Effects array, or picks a different multi-subclass PERK.
- [ ] **EntryPoint enum dump (informational)** — Phase 1 dumps the `PerkEntryPointType` enum's full value set (~140 values per PLAN.md § Background). Informational; Phase 2 transcribes representative values into Layer 4.dsl.03 enum-error wording.
- [ ] **Q1–Q7 expectation flips audit** — confirm all seven design-question locks held per the conductor's Phase 1 kick-off prompt; if any lock differs from Phase 0 default (or if Phase 1's audit surfaces a complexity cliff that warrants escalation), surface in handoff + escalate.

---

## Phase fill-in checklist (Phase 2 hand-back)

Phase 2 closes with these MATRIX edits landed:

- [ ] **Layer 5 cell count confirmed** — pre-v2.9.3 baseline confirmed at 425 cells from v2.9.2 P5 handoff. Phase 2 adds N new cells (per-subclass coverage-smoke + multi-error + composition cells per Layer 1.P/1.D/2/4). New total: 425 + N. Layer 5 row updated accordingly.
- [ ] **Q1–Q7 expectation flips audit (post-implementation)** — confirm all seven locks held at implementation time; if any flipped (e.g. Q5 discriminator value canonical form needed adjusting), document in handoff.
- [ ] **Layer 1.D validation-error JSON shape locked** — exact key names per the new bridge error model in `Models.cs` (e.g. discriminator-not-found vs abstract-base vs missing-discriminator distinct error codes). Rows 1.D.01–1.D.07 update with confirmed key names.
- [ ] **Error message wording finalized** — Phase 2 locks exact strings (transcribed from `PatchEngine.cs` factory + carrier-rejection paths). Symmetric across Python wrapper + bridge.
- [ ] **4.dsl.02 cross-master synthetic fixture** — Phase 2 builds (or reuses if v2.9.2 P4's fixture covers this case) the synthetic two-plugin fixture for cross-master FormLink in nested condition. Document the fixture path + cleanup.
- [ ] **Layer 2.04 empty-clear regression** — confirm PERK top-level scalar fields preserved on `Effects: []` write (sibling-preservation invariant). v2.8.0 Test 29's posture mirrored here.

---

## Phase fill-in checklist (Phase 3 hand-back)

Phase 3 closes with:

- [ ] **Live FormIDs** — replace placeholder FormIDs in Layer 3 scenarios with the FormIDs picked from the live Authoria modlist at execution time.
- [ ] **Per-scenario PASS/FAIL** — annotate each scenario row with the readback evidence + result + measured behavior vs Phase 0 expectation.
- [ ] **Scenario 3.2 in-scope-or-skip** — confirm Scenario 3.2's precondition (PlayerWerewolfFeed or analog has multi-subclass Effects array on Authoria) and either land the assertions or document the skip with reason.
- [ ] **Test ESP cleanup verification** — confirm post-verification `<modlist>/mods/Claude Output/v2.9.3-test-*.esp` cleanup; live install state restored to v2.9.2 baseline + v2.9.3 bridge artifact.
