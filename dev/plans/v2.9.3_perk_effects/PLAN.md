# v2.9.3 — PERK.Effects writability

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-28, post-v2.9.2 ship.
**Baseline:** v2.9.2 (shipped 2026-04-28 — read-side efficiency mechanism on `mo2_record_detail`).
**Target version:** v2.9.3 (slug locked by Aaron 2026-04-28 — not "working", final).
**Sessions estimated:** 6–8 phase sessions plus 1 conductor session running across them. Phase 4 is conditional (skipped if Phase 3 surfaces nothing). No Phase 2 split contemplated up-front; Phase 1's audit may surface a subclass-count surprise that justifies a 2A/2B split — escalate to conductor if so. Phase 1 is heavier than v2.9.2's because the inventory shape is unknown going in (v2.8.0's `Effect` was a single concrete class; PERK's `APerkEffect` is abstract with multiple subclasses).

**Mandate.** Close the heaviest write-surface carry-over from the v2.8.0 Effects-list mechanism. v2.8.0 wired `set_fields: {Effects: [...]}` on five record types (SPEL/ALCH/ENCH/SCRL/INGR) whose `Effects` is `ExtendedList<Effect>` — `Effect` is a single concrete LoquiObject with parameterless ctor, so Branch A's `Activator.CreateInstance(elementType)` works. PERK's `Effects` is `ExtendedList<APerkEffect>` — `APerkEffect` is **abstract** with multiple concrete subclasses (`PerkEntryPointEffect`, `PerkAbility`, `PerkQuestEffect`, possibly more — Phase 1's audit produces the authoritative inventory). Naive Activator throws (same failure mode v2.8.0 hit on `typeof(Condition)`). Solution shape is established: a per-subclass factory routed off a JSON discriminator, mirroring v2.8.0's `BuildConditionFromJson` extracted-from-`ApplyAddConditions` pattern.

This is a **single-mechanism, scope-locked** point release — one Branch A extension (special-case `typeof(APerkEffect)` in `ConvertJsonElementToListItem`), one new factory (`BuildPerkEffectFromJson`), one Python schema description update. v2.9.0's per-function condition-parameter dispatcher (`RouteParameterSlot` + `KnownParameterizedFunctions`) composes **untouched** for nested `PerkEntryPointEffect.PerkConditions[*].Conditions[*].parameters` — Phase 2 verifies this via probe rather than re-implementing.

Real consumer signal: Authoria's Requiem-derived modlist carries roughly 1900+ PERK records (Skyrim.esm has 375; Requiem overrides 179 of them; further mods add ~1500 more). Patching PERK records is currently impossible via `set_fields: {Effects: [...]}` — the bridge rejects with the v2.8.0 carrier-set check. Real-world PERK patching workflows (rebalancing perk magnitudes, swapping perk-granted spells, restructuring conditions on existing perks) are blocked.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |
| `<plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.3_perk_effects\` |
| `<v2.9.2-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.2_read_side_efficiency\` (shipped 2026-04-28; reference only — closed) |
| `<v2.8.0-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.8.0_verification\` (shipped 2026-04-25; the canonical Effects-list mechanism reference for this work) |
| `<v2.9.0-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.X_condition_parameters\` (shipped 2026-04-27; the canonical inventory-probe + per-subclass-dispatch reference) |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. The conductor session has already told you which phase you are via the kickoff prompt that spawned this session. **Before touching anything**, do this in order:

1. **Confirm your phase.** The conductor's kickoff prompt named your phase. If it didn't, halt and ask the conductor — don't infer it from the handoff numbering yourself (the conductor owns phase identification).

2. **Read the previous handoff** in full (if any). The conductor's kickoff prompt named which one. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, conductor decisions relevant to your phase, and what to write in your own handoff. **Do not read other phases' sections** — they're scoped to other executors and consume context for no benefit.

4. **Read `MATRIX.md`** in this directory. Phase 0 produces it; Phases 1–5 use it as the authoritative test specification. Phase 1 may extend it with whatever subclass-shape findings the inventory probe surfaces; Phase 2 onward reads the post-Phase-1 form.

5. **Read `APERK_EFFECTS_AUDIT.md`** in this directory if Phase 1 has produced one (Phase 2 onward). The audit captures the runtime shape of every concrete `APerkEffect` subclass per Mutagen 0.53.1 reflection — Phase 2's bridge implementation transcribes it; it does not speculate.

6. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.2)
   - `Claude_MO2/KNOWN_ISSUES.md` § Patching write surface (v2.9.3 closes the "QUST.Aliases / Stages / Objectives, PERK.Effects" carry-over for PERK only — the QUST sub-records remain deferred)
   - **Skim** `<v2.8.0-plan>/EFFECTS_AUDIT.md` and `<v2.8.0-plan>/PHASE_1_HANDOFF.md` for the canonical Branch A + `BuildConditionFromJson` shape — this is the structural template v2.9.3 follows. **Skim** `<v2.9.0-plan>/CONDITIONS_AUDIT.md` for the canonical inventory-probe pattern — Phase 1's `APERK_EFFECTS_AUDIT.md` mirrors its layout (per-shape categorization, padding-pattern filter, anchor sanity-check, Pareto framing).
   - Check `<workspace>/Live Reported Bugs/` root for anything new.

7. **Confirm phase identity + work plan with the user (Aaron) before any code changes.** Wait for go-ahead.

8. **At the end of your phase**, write `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` if Phase 4 spawns a sub-session) in this directory using the template at the bottom of this file. **Do not write the next phase's kickoff prompt** — the conductor owns that.

**One phase per session.** If you finish early, summarise and stop — don't roll into the next phase.

### Communicating with the conductor

The conductor session is a separate Claude Code session orchestrating this plan. It runs between phases (reading your handoff, writing the next phase's kickoff). If your phase needs guidance the plan doesn't already give you (scope ambiguity, an unexpected probe result that changes the mechanism shape, a Mutagen-schema surprise that needs Aaron's call to absorb-vs-defer), write a short note to the conductor.

**Token-efficient format** — bullets, no prose, no transcripts:

```
CONDUCTOR ASK
Phase: N
Topic: <one-line topic>
Context: <2-3 bullets max>
Question: <single specific question>
Suggested options: <A/B/C with one-line rationale each>
Default if no response in <X minutes>: <whatever the executor will do absent guidance>
```

Drop this at the bottom of your in-progress handoff under § Conductor asks. The conductor reads it, summarizes for Aaron if needed, and either replies (you proceed with that answer) or escalates to Aaron (who replies via the conductor or interjects directly).

---

## 📋 Background — why this plan exists

### v2.8.0 carry-over framing

KNOWN_ISSUES.md § Patching write surface has carried "QUST.Aliases / Stages / Objectives, PERK.Effects" as one entry since v2.8.0 ship. The shared shape: `ExtendedList<AbstractBaseClass>` where the abstract base has multiple concrete subclasses with diverging field surfaces. v2.8.0's bounded mechanism handled `Effect` (concrete, one shape, five carriers) cleanly; the abstract-sub-class case was deferred pending real-consumer signal.

PERK.Effects is the heavier of the two carry-overs by real-world write-volume:
- **PERK records**: 375 in vanilla Skyrim.esm; ~1900 in Authoria's Requiem-derived modlist; ~179 of vanilla's are overridden by Requiem alone. PERK is the primary tuning surface for any combat/magic/stealth overhaul.
- **QUST.Aliases/Stages/Objectives**: lower volume, niche workflows. Real-consumer signal hasn't surfaced.

Aaron's framing (post-v2.9.2 ship): land PERK.Effects as the next bridge-mechanism point release; QUST sub-records stay deferred until consumer signal arrives.

### Architecture: Branch A's special-case pattern is the established mechanism

The bridge already has the structural template via v2.8.0's `Condition` handling. Reproducing the contract from EFFECTS_AUDIT.md and PatchEngine.cs:

In `ConvertJsonElementToListItem` (PatchEngine.cs:1441):

```csharp
if (element.ValueKind == JsonValueKind.Object && elementType.IsClass && elementType != typeof(string))
{
    if (elementType == typeof(Condition))
        return BuildConditionFromJson(element);

    object? entry;
    try { entry = System.Activator.CreateInstance(elementType); }
    catch (Exception ex) { /* throw with FriendlyTypeName */ }

    foreach (var member in element.EnumerateObject())
        SetPropertyByPath(entry, member.Name, member.Value);
    return entry;
}
```

The pattern: if the abstract case requires sub-class selection, special-case it before the generic Activator path; otherwise fall through to Activator. v2.8.0 added `typeof(Condition)` route to `BuildConditionFromJson`. v2.9.3 adds `typeof(APerkEffect)` route to a new `BuildPerkEffectFromJson` factory.

`BuildPerkEffectFromJson` mirrors `BuildConditionFromJson`'s structure:
1. Read JSON Object, deserialize to a friendly DSL shape (e.g. `PerkEffectEntry`).
2. Inspect the discriminator (`type:` field per Phase 0 Q1 lock; see § Architecture B below).
3. Reflect the concrete subclass (`Mutagen.Bethesda.Skyrim.{Type}`) — analogous to v2.8.0's `Mutagen.Bethesda.Skyrim.{Function}ConditionData` lookup.
4. `Activator.CreateInstance` on the **concrete** subclass (works because the subclass is non-abstract).
5. Apply per-property recursion via `SetPropertyByPath` for each remaining JSON member — including nested `PerkConditions` (which have their own ExtendedList shape — see § Architecture C).

The `PerkEntryPointEffect` subclass is the architecturally-interesting one: it carries an `EntryPoint` enum field with ~140 distinct values (ModSpellMagnitude, ModBowDamage, etc.) and a `Function`/`Modification` enum field (SetValue, AddValue, MultiplyValue, etc.). v2.9.0's `RouteParameterSlot` already handles enum dispatch via `Enum.Parse(propType, value, ignoreCase: true)`. Mutagen's `PerkEntryPointEffect.PerkConditions` is a list of grouped-condition objects, each containing its own `ExtendedList<Condition>` — composing with v2.9.0's `BuildCondition` foreach untouched.

### Real-world signal — the consumer write-surface gap

Authoria's modlist patching workflows include:
- **Perk magnitude rebalancing** — change `ModSpellMagnitude` from 1.5× to 1.4× on AugmentedShock60 (`Skyrim.esm:10FCFA`), AugmentedFrost60, AugmentedFlames60, etc. Today: blocked.
- **Perk condition restructuring** — change the `HasPerk` / `GetActorValue` conditions on a perk effect (e.g. raise the Sneak skill floor on AssassinsBlade from 50 to 60). Today: condition writes go through `add_conditions` only, which targets the top-level `Perk.Conditions` list, not `Perk.Effects[i].PerkConditions[j].Conditions`. Read-side surfaces it (v2.9.2's `expand_links: ["Effects"]` shows the nested structure); write-side has no surface.
- **Spell-grant perk swaps** — change which Spell a perk grants on activation (e.g. PlayerWerewolfFeed's `EntryPoint=Activate` effect carries a `Spell` FormLink). Today: blocked unless the entire Effects list is rewritten manually.

A representative read-side render from `mo2_record_detail` against `Skyrim.esm:10FCFA` (AugmentedShock60) confirms the write target shape:

```json
"Effects": [
  {
    "Modification": "Multiply",
    "Value": 1.5,
    "EntryPoint": "ModSpellMagnitude",
    "PerkConditionTabCount": 3,
    "Rank": 0,
    "Priority": 0,
    "Conditions": [
      { "RunOnTabIndex": 1, "Conditions": [ { /* ConditionFloat with Data */ } ] }
    ],
    "Flags": { "Flags": "0", "FragmentIndex": 0 }
  }
]
```

The read-side renders the concrete shape; the write-side mirror is what v2.9.3 lands.

### Out of scope (locked at PLAN write-time)

- **Production code changes outside Phase 2 / 4.** Phase 0 + 1 don't touch bridge or Python. Phase 2 lands the implementation. Phase 3 reads only. Phase 4 is conditional.
- **QUST.Aliases / Stages / Objectives.** The other half of the v2.8.0 carry-over. The shape is similar (`ExtendedList<AbstractBase>` with per-subclass field divergence) but the surface is broader (aliases carry FormLinks to Faction/Cell, package overrides, AI data; stages carry log entries with VMAD; objectives carry FormLinks to Quest and target-types). Phase 0 default: keep PERK.Effects-bounded; surface to Aaron at PLAN review (Q6) in case a consumer signal makes a 2-for-1 absorb attractive.
- **v2.9.x deferreds** — Boolean dispatcher branch, sub-B 6 String-typed Condition functions, QuestAlias/QuestLogEntry nested conditions (a v2.9.x candidate but distinct from PERK.Effects' nested PerkConditions which compose with v2.9.0's existing dispatcher untouched), AMMO enchantment, replace-semantics whole-dict, chained dict access — separate scoping sessions.
- **Read-surface candidates** (reverse-link search, override-aware FormLink expansion, MaxDepth exposure, cross-call result caching) — Aaron's stated post-v2.9.3 review work; happens after this ships, not within this scope.
- **Pareto-Pareto-trimming.** v2.9.3 ships every concrete `APerkEffect` subclass Phase 1 enumerates. Per-subclass-defer is a Phase 1-conductor-ask if the inventory turns out to be larger than expected (e.g. >5 subclasses with diverging shapes) — Phase 0 default: ship the full subclass set unless Phase 1 surfaces a complexity cliff.
- **`add_perk_effects` / `remove_perk_effects` operators.** v2.9.3 lands `set_fields: {Effects: [...]}` only — replace-semantics, matching v2.8.0's posture for the Effects-list mechanism. Per-effect-add/remove is a v2.9.x candidate if a consumer surfaces it.
- **PerkConditions write surface beyond inline-nested.** `PerkConditions` is part of the per-`PerkEntryPointEffect` payload; writing it via `set_fields: {Effects: [{PerkConditions: [...], ...}]}` is in scope (it's a recursive `SetPropertyByPath` call, no new mechanism). A standalone `add_perk_conditions: [{effect_index, perk_conditions: [...]}]` operator targeting an existing perk's nested conditions is **not** in scope — would need a new operator surface.

---

## 🏗️ Architecture — PERK.Effects write mechanism (locked + open questions)

### A. Branch A extension — `typeof(APerkEffect)` route to `BuildPerkEffectFromJson`

The minimum surface change is a single special-case in `ConvertJsonElementToListItem`, mirroring v2.8.0's Condition route:

```csharp
if (element.ValueKind == JsonValueKind.Object && elementType.IsClass && elementType != typeof(string))
{
    if (elementType == typeof(Condition))
        return BuildConditionFromJson(element);

    if (elementType == typeof(APerkEffect))      // v2.9.3
        return BuildPerkEffectFromJson(element); // v2.9.3

    // existing generic Activator fallback
    ...
}
```

`BuildPerkEffectFromJson(JsonElement)` is a new factory in PatchEngine.cs near `BuildConditionFromJson` (~line 2331). Structure:

1. Validate JSON is an Object.
2. Read the `type:` discriminator (Phase 0 Q1 lock — see § B below).
3. Reflect the concrete subclass via `Mutagen.Bethesda.Skyrim.{Type}` lookup (Phase 1's audit confirms exact subclass names).
4. Validate the resolved type is non-abstract and assignable to `APerkEffect`.
5. `Activator.CreateInstance` on the concrete subclass.
6. For each remaining JSON member (excluding `type`), call `SetPropertyByPath(perkEffect, name, value)` — recursion hands nested FormLinks, lists, sub-LoquiObjects (per v2.8.0 Branch B), and nested `Conditions` (per v2.8.0 Branch A's `typeof(Condition)` special case) back through existing machinery.

The **factory pattern is identical to v2.8.0's `BuildCondition` extracted-from-`ApplyAddConditions`** — single-source-of-truth for any future PerkEffect-list write surface (e.g. an `add_perk_effects` operator if a consumer surfaces the need; out of scope for v2.9.3 per § Out of scope above).

### B. Discriminator strategy — open question (Phase 0 Q1)

The JSON DSL needs a way to specify which concrete `APerkEffect` subclass each list element constructs as. Three candidates:

- **Option A — Explicit `type:` field per element** (Phase 0 default proposal):
  ```jsonc
  {
    "Effects": [
      { "type": "PerkEntryPointEffect", "EntryPoint": "ModSpellMagnitude", "Modification": "Multiply", "Value": 1.5, "PerkConditions": [...] },
      { "type": "PerkAbility", "Ability": "Skyrim.esm:01ABCD" },
      { "type": "PerkQuestEffect", "Quest": "Skyrim.esm:02DEFA", "Stage": 100 }
    ]
  }
  ```
  Mirrors v2.8.0's `function:` discriminator on Condition entries (which selects `{Function}ConditionData`). Mutagen's subclass naming is public API and stable across point releases (verified in v2.9.0's CONDITIONS_AUDIT inventory probe — concrete names didn't change between Mutagen 0.51 and 0.53).

- **Option B — Distinguish by which fields are populated.** Inspect the JSON object's member set; route to the subclass whose property surface matches. **Fragile** — a typo of an inherited base property could collapse two subclasses' detection paths into one ambiguous branch. Breaks if Mutagen 0.54+ adds a property to one subclass that overlaps with another's.

- **Option C — Implicit via `EntryPoint:` field for `PerkEntryPointEffect`-specific effects, no discriminator for the others.** The EntryPoint enum is unique to `PerkEntryPointEffect`; PerkAbility carries `Ability` (FormLink to Spell); PerkQuestEffect carries `Quest` + `Stage`. Detect by mutually-exclusive field presence. Slightly less fragile than B because the discriminating fields are subclass-defining, not arbitrary. But still fails the "what about subclasses Phase 1 hasn't named yet?" generality test, and requires per-subclass-add code rather than the uniform reflection lookup Option A enables.

**Phase 0 proposal: Option A — explicit `type:` field.** Rationale:

1. **Discoverability** — a caller reading the schema description knows what to write. Option B/C require either field-presence-rules-list or per-subclass guessing.
2. **Stability** — Mutagen subclass names are public API. Phase 1's audit confirms exact names; Phase 2 transcribes; the discriminator value is Mutagen-rename-safe within a major version.
3. **Aligns with v2.8.0's Condition pattern** — `{function: "GetIsID", ...}` is structurally identical to `{type: "PerkEntryPointEffect", ...}`. Reading the schema, callers already mentally distinguish `function` (Condition discriminator) from `type` (PerkEffect discriminator) — both are reflection-property-name hooks.
4. **Generic factory implementation** — single reflection-lookup line; no per-subclass branching in `BuildPerkEffectFromJson`. Adding a new subclass in v2.9.x is a coverage-smoke cell + audit doc update, no factory code change.
5. **Footgun-safe** — supplying both `type` and an EntryPoint-style implicit discriminator surfaces a clean DSL error (Phase 2 implements; matches v2.9.0's `actor_value` + `parameters.ActorValue` ambiguous-DSL pattern).

If Phase 1's audit surfaces a subclass naming conflict (e.g. two subclasses with the same simple name in different namespaces — vanishingly unlikely in `Mutagen.Bethesda.Skyrim` but worth probing for), Phase 0 escalates Q1 to Aaron via conductor. Otherwise lock Option A.

### C. `PerkConditions` nested-list shape — open question (Phase 0 Q7)

`PerkEntryPointEffect.PerkConditions` is itself a list of grouped-condition objects. Each group carries:
- `RunOnTabIndex: int` — which "condition tab" the group binds to (PERK records have 1–3 condition tabs, indexed 0..N).
- `Conditions: ExtendedList<Condition>` — the actual condition entries for that tab.

Read-side render (from `mo2_record_detail` against Skyrim.esm:10FCFA):

```json
"PerkConditions": [
  {
    "RunOnTabIndex": 1,
    "Conditions": [
      { "ComparisonValue": 60, "Data": { "ActorValue": "Destruction", ... }, "CompareOperator": "GreaterThanOrEqualTo", ... }
    ]
  }
]
```

Phase 1's audit confirms whether `PerkConditions` element type is a single concrete class (e.g. `Mutagen.Bethesda.Skyrim.PerkCondition`) or another abstract — almost certainly the former per the read-side render shape. Assuming concrete:

**Write DSL candidate** (Phase 0 default):

```jsonc
"Effects": [
  {
    "type": "PerkEntryPointEffect",
    "EntryPoint": "ModSpellMagnitude",
    "Modification": "Multiply",
    "Value": 1.5,
    "PerkConditionTabCount": 3,
    "PerkConditions": [
      {
        "RunOnTabIndex": 1,
        "Conditions": [
          { "function": "GetActorValue", "operator": ">=", "value": 60, "parameters": { "ActorValue": "Destruction" } }
        ]
      }
    ]
  }
]
```

The inner `Conditions: [{function, operator, value, parameters, ...}]` shape is bit-identical to `add_conditions` and Effect.Conditions per v2.8.0 — the bridge routes via `BuildConditionFromJson` (Branch A's `typeof(Condition)` special case). v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` then routes the `parameters` map for each condition.

The wrapper object (`{RunOnTabIndex, Conditions}`) is a concrete `PerkCondition` LoquiObject — Activator-creates cleanly, fields set via SetPropertyByPath. **No new mechanism required**; this is Branch A + Branch B + the typeof(Condition) special case composing as designed.

**Phase 0 default: lock the wrapper-object DSL.** Phase 1's probe confirms concrete subclass + writability via round-trip. If `PerkCondition` turns out to be abstract too (unlikely per the read-side render but worth verifying), Phase 1 escalates Q7 to Aaron.

### D. Replace-semantics consistency — locked

v2.8.0's Effects-list uses **replace-semantics**: a JSON-Array `set_fields: {Effects: [...]}` clears the source list and writes the JSON-supplied entries. v2.9.3 PERK.Effects matches — write-time consistency across the family is a UX invariant.

Empty-array clear (`Effects: []`) lands a count-0 list, mirroring v2.8.0's Test 29 (1.E.07). Layer 4 cell verifies.

This is **not** an open question; it's a sanity-confirm at PLAN review (Phase 0 default). If Aaron lands on a different posture (e.g. merge-by-EntryPoint instead of replace), surface to Aaron and re-architect. Default: replace.

### E. Pareto vs full coverage — open question (Phase 0 Q2)

The user's task spec named ~13 concrete `APerkEffect` subclasses. The actual count is Phase 1's audit deliverable. Possibilities:

1. **Mutagen 0.53.1 has 3 concrete subclasses** (`PerkEntryPointEffect`, `PerkAbility`, `PerkQuestEffect`). Ship all three. No Pareto question.
2. **Mutagen 0.53.1 has 5–8 concrete subclasses** with a clear "common 3" + "obscure tail." Aaron decides whether to ship the full set or the Pareto subset.
3. **Mutagen 0.53.1 has 13 concrete subclasses** with diverging shapes (most likely if the Mutagen schema decomposed PerkEntryPointEffect into per-EntryPoint-variant subclasses — `PerkEntryPointModifyValue`, `PerkEntryPointActivate`, `PerkEntryPointAddText`, etc.).

**Phase 0 proposal: ship the full subclass set Phase 1 enumerates.** Rationale:
1. The factory pattern is uniform — adding a 13th subclass to `BuildPerkEffectFromJson`'s reflection lookup is zero new code; only the coverage-smoke cell count grows.
2. Pareto-defer creates a "supported subclass" / "not-yet-wired subclass" footgun; callers don't know up front which their workflow needs.
3. Real-world signal is uneven — Authoria's PERK records I sampled are dominated by one variant (`PerkEntryPointEffect` with EntryPoint enum), but Werewolf/Vampire-style perks use multiple variants in one record. Pareto-trimming would block those workflows.

**Escalation trigger:** if Phase 1's audit shows >5 subclasses with substantially-diverging shapes (each carrying its own property set with no shared structural backbone), or any subclass is itself abstract (a third level of polymorphism), Phase 1 escalates Q2 to Aaron via conductor — Pareto-defer becomes a real call rather than a default-no.

Read-side observation (informational, not Phase-0-binding): a sweep of 9 representative PERK records via `mo2_record_detail` against the live Authoria modlist showed 100% of effects rendering with the `EntryPoint` field present, suggesting `PerkEntryPointEffect` is the dominant on-disk shape. PerkAbility / PerkQuestEffect surfaces are present in vanilla Skyrim.esm via specific Bethesda patterns (standing-stone perks, Werewolf/Vampire perks) but quantitatively rare in modlist patching workflows. Phase 1's audit gives the authoritative inventory + frequency.

### F. Composition with v2.9.0 dispatcher — sanity-confirm (Phase 0 Q4)

v2.9.0's per-Condition-function parameter dispatch (`RouteParameterSlot` + `KnownParameterizedFunctions`) operates on `Condition` entries: each entry's `parameters: {SlotName: Value}` map routes through reflection on the function's `*ConditionData` type. The dispatcher is **agnostic** to where the condition lives — top-level `Conditions`, nested `Effect.Conditions` (v2.8.0), nested `PerkEntryPointEffect.PerkConditions[*].Conditions` (v2.9.3) — because composition routes through `BuildCondition`'s foreach over `ce.Parameters`.

Phase 2 verifies via probe — write a `set_fields: {Effects: [{type: "PerkEntryPointEffect", PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", operator: "==", value: 1, parameters: {Perk: "Skyrim.esm:058200"}}]}], ...}]}` against a synthetic PERK; round-trip via `CreateFromBinary` + `WriteToBinary`; confirm the dispatcher routed `parameters.Perk` through v2.9.0's IFormLinkOrIndex<T> branch unchanged.

**Phase 0 default: Q4 sanity-confirms at Phase 2 probe time, no separate question for Aaron unless the probe surfaces a composition gap.** Default lock: untouched composition. If Phase 2's probe shows a gap (e.g. `RouteParameterSlot` requires the condition to be at a specific depth or attached to a specific record type), escalate as a Phase 2 mid-session ask.

### G. Backward compatibility — locked (sanity-confirm)

`set_fields: {Effects: [...]}` against PERK is currently rejected — the v2.8.0 Effects-list bridge code checks the carrier type against the {SPEL, ALCH, ENCH, SCRL, INGR} set and surfaces a clean per-record error. Adding PERK is purely additive — no existing caller patterns change because no caller successfully writes Effects to PERK today.

Verify at Phase 2 acceptance — the existing v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 coverage-smoke cells must all stay green. The Effects-list write path on the five v2.8.0 carriers must be bit-identical (a trivial precondition; v2.9.3's bridge change is a Branch A extension, not a refactor).

### H. Out-of-scope candidates — open question (Phase 0 Q6)

**QUST.Aliases / Stages / Objectives.** Same "abstract sub-class polymorphism" structural bucket as PERK.Effects, but with broader sub-record surface. v2.8.0's KNOWN_ISSUES.md grouped them as one carry-over entry; v2.9.3 closes the PERK half. Should v2.9.3 absorb any of QUST sub-records via the same Branch A mechanism if cheap?

**Phase 0 proposal: keep bounded — defer all three QUST sub-records.** Rationale:
1. The Branch A factory pattern works for any abstract-list case structurally, but the **per-subclass field surface** for QuestAlias is much broader — Faction/Cell FormLinks, package overrides, AI data, conditions. The audit + per-subclass coverage-smoke would substantially expand Phase 1 + Phase 2 scope.
2. Carry-over framing matches v2.8.0's "Effects-list = 5 records, defer others" discipline. v2.9.3 is the PERK.Effects release; QUST sub-records is a separate v2.9.x scoping session.
3. No real-consumer signal for QUST sub-records yet. PERK has Authoria's modlist signal driving the prioritization.

**Escalation trigger:** if Aaron's session-driven response at PLAN review is "yes absorb QUST.Aliases too — the same plumbing closes both at once," reframe Phase 1's audit + Phase 2's bridge work to cover both. Plan currently bounds at PERK; Q6 surfaces the choice.

### I. Scope locks

- **One mechanism only.** Branch A extension to handle `typeof(APerkEffect)`. New `BuildPerkEffectFromJson` factory. No other tool changes, no other bridge command additions, no `RecordReader.RenderValue` changes (read-side already renders correctly per v2.9.2 audit; v2.9.3 is write-only).
- **Replace semantics.** Whole-array assignment clears source, writes new — same as v2.8.0.
- **No `add_perk_effects` / `remove_perk_effects` operators.** v2.9.3 lands `set_fields` only. Per-effect add/remove is a v2.9.x candidate if a consumer surfaces it.
- **`PerkConditions` write surface = inline-nested only.** Nested via `set_fields: {Effects: [{PerkConditions: [...], ...}]}`. No standalone `add_perk_conditions` operator targeting an existing perk's nested conditions.
- **v2.9.0 composition untouched.** `RouteParameterSlot` + `KnownParameterizedFunctions` compose unchanged for nested conditions inside PerkConditions. Phase 2 verifies via probe; no dispatcher code change.
- **Single-record-type extension** to the Effects-list mechanism. PERK is added to the carrier set; QUST.Aliases / Stages / Objectives stay deferred (Phase 0 Q6 default-defer; Aaron's call at review).
- **Defaults preserve v2.9.2 behavior.** All existing single `formid` / batch / projection / expansion / `mo2_create_patch` paths bit-identical. v2.9.3 is purely additive on the write path.
- **Probe-first discipline.** Phase 1 starts with the inventory probe + sub-class shape sweep before any bridge code lands. Phase 2 references Phase 1's audit; doesn't speculate on subclass names or property shapes.
- **Bonus-catch posture — STRICTER for v2.9.3 than legacy.** Two distinct categories:
  - **Latent-bug fixes in touched code** (e.g. a PerkAdapter VMAD-write bug spotted while wiring PERK.Effects, a Branch B sub-LoquiObject regression, a v2.9.0-dispatcher composition gap): fold in per the v2.7.1/v2.8.0/v2.9.0/v2.9.1/v2.9.2 precedent, with explicit handoff documentation. >1 h additional work → halt, ask conductor.
  - **Easy-win write-surface additions** (new write functions, new write-surface mechanisms, operator coverage expansions outside PERK.Effects, opportunistic absorptions of nearby carry-overs Phase 1's audit makes look cheap): **DO NOT auto-absorb, even when trivial.** Halt and surface to the conductor first; the conductor relays to Aaron; Aaron decides. The v2.9.3 bar (per Aaron 2026-04-28) is "any new write surface, regardless of cost → conductor → Aaron" — sharper than the legacy ">1 h or new operator surface → halt." Rationale: opportunistic write-surface absorptions can quietly mutate a release's framing and complicate ship-side scope discipline; the cost of pausing is low.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive.

### J. Conductor decisions (cross-phase, locked at PLAN write-time)

Things the conductor enforces or decides between phases without re-litigating:

- **Phase identification.** Conductor identifies current phase from highest-numbered handoff in `<plan>/`. Phase executors don't self-identify.
- **Design lock sign-off.** Phase 0's executor proposes the design questions (Q1 discriminator strategy, Q2 Pareto vs full coverage, Q3 replace semantics — sanity-confirm, Q4 v2.9.0 composition — sanity-confirm, Q5 discriminator value canonical form, Q6 QUST sub-records absorb-or-defer, Q7 PerkConditions nested-list shape). Conductor relays to Aaron for explicit lock. Phase 1 doesn't begin until the lock is in.
- **Inventory-shape sign-off.** Phase 1 surfaces the subclass count + per-subclass shape table; if the count is dramatically off the expected band (e.g. >10 with diverging shapes, or a third level of abstract polymorphism), the whole v2.9.3 mechanism's value proposition shifts. Conductor relays to Aaron; Phase 2 doesn't begin until the inventory shape is acceptable.
- **No Phase 2 split contemplated up-front.** v2.9.3's capability surface is single (Branch A extension + new factory). If Phase 1 surfaces an unexpectedly-large subclass set (>5 with diverging shapes), escalate to Aaron for a 2A/2B split decision — don't autonomously split.
- **Phase 4 spawn decision.** If Phase 2 + Phase 3 surface zero bridge bugs and zero matrix corrections, conductor skips Phase 4 directly to Phase 5. Otherwise spawns Phase 4 (single session, items 1–N model from v2.9.0/v2.9.1/v2.9.2 P4) or Phase 4 sub-sessions per bug if items don't fit one budget.
- **Live install sync timing.** Phase 0 + 1 don't touch live. Phase 3 reads via `mo2_record_detail` against live to confirm scenario inputs (read-only verification of Phase 2's bridge against Authoria PERK records), then writes test patches via `mo2_create_patch`. Phase 4 syncs to live only if a fix needs verification on the live install. Phase 5 syncs once and ships. Conductor confirms sync state before each Phase 3 / 4 / 5 kickoff.
- **Schema migration vs additive.** v2.9.3 is purely additive. `set_fields: {Effects: [...]}` on PERK is a new accepted carrier; no deprecation of existing fields. The discriminator (`type:` per Q1 lock) is a new accepted JSON-object key; absent on v2.8.0 SPEL/ALCH/ENCH/SCRL/INGR Effects entries (which use the concrete `Effect` class — no discriminator needed). Conductor rejects any phase proposing a schema break.
- **Single-commit deliverable for Phase 0.** Per the v2.9.2 § I precedent: Phase 0 commits `PLAN.md` + `MATRIX.md` + `CONDUCTOR_KICKOFF.md` (this scoping session's output) + `PHASE_0_HANDOFF.md` in **one work commit + one hash-record commit**. Force-add via one `git add -f` invocation.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Plan + matrix specification + design proposal | `PLAN.md` (this file), `MATRIX.md` (NEW), `CONDUCTOR_KICKOFF.md` (NEW), `PHASE_0_HANDOFF.md` (NEW); design questions (Q1–Q7) surfaced under § Conductor asks. **Already produced by the scoping session that wrote this plan** — Phase 0 in-session work is matrix scaffold + handoff + commit, not a fresh PLAN draft. | None |
| 1 | APerkEffect inventory probe + record-shape sweep + audit | `tools/race-probe/Program.cs` extended with v2.9.3 P1 inventory section (mirrors v2.9.0 P1's ConditionData inventory shape: Activator constructibility table, per-subclass property dump with `[base]`/`[function-specific]` annotation, EntryPoint enum dump if `PerkEntryPointEffect` is concrete, `PerkConditions` element-type confirmation); `dev/plans/v2.9.3_perk_effects/APERK_EFFECTS_AUDIT.md` (NEW; mirrors EFFECTS_AUDIT.md/CONDITIONS_AUDIT.md layout); MATRIX.md updated with confirmed subclass list + per-subclass cell breakdown; `PHASE_1_HANDOFF.md` | Phase 0 with design lock (Q1–Q7) |
| **2** | **Bridge implementation + Python wrapper + functional probes + coverage-smoke regression cells** | `PatchEngine.cs` (`ConvertJsonElementToListItem` Branch A extension for `typeof(APerkEffect)`; new `BuildPerkEffectFromJson` factory near line 2331; helper `IsPerkEffectAbstract`/`ResolvePerkEffectConcreteType` if Phase 1's audit shows nuance); `Models.cs` (new `PerkEffectEntry` if a strongly-typed wrapper is preferred over `JsonElement` deserialization, mirroring `ConditionEntry` — Phase 2 picks based on Phase 1's findings); `tools_patching.py` (`set_fields` schema description appended with the Effects-array form on PERK + the discriminator); `race-probe` per-subclass functional probes; `coverage-smoke` +N regression cells; CHANGELOG; `KNOWN_ISSUES.md`; **version bump to v2.9.3** (Phase 2's first commit) | Phase 1 with inventory-shape acceptance |
| 3 | Workflow scenario(s) on live install | Per-scenario assertions in `PHASE_3_HANDOFF.md`; bug list extended; `mo2_create_patch` against Authoria PERK records (Requiem perks the natural anchor); readback verification via `mo2_record_detail` post-patch | Phase 2 |
| 4 | Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL — conductor decides) | `PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` for sub-sessions); code commits; regression tests | Phase 3 with surfaced findings |
| 5 | Re-run + ship v2.9.3 | Final smoke run; installer + bridge artifact rebuilt; live sync; tag pushed; `gh release create`; memory updated | Phase 4 (or Phase 3 if Phase 4 skipped) |

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one or more commits per its scope. Commit messages start with `[v2.9.3 PN]` (e.g. `[v2.9.3 P2] PERK.Effects writability + version bump to v2.9.3`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its handoff via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
  - **Phase 0 exception** (per § J above): single-commit deliverable bundles `PLAN.md` + `MATRIX.md` + `CONDUCTOR_KICKOFF.md` + `PHASE_0_HANDOFF.md` together. Force-add via one `git add -f Claude_MO2/dev/plans/v2.9.3_perk_effects/{PLAN,MATRIX,CONDUCTOR_KICKOFF,PHASE_0_HANDOFF}.md` invocation, then one work commit + one hash-record commit.
  - **Phase 1 exception:** `APERK_EFFECTS_AUDIT.md` is a new file; force-add alongside the handoff.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. **Phase 2 bumps the version** on its first commit (PERK.Effects writability is the trigger). Subsequent phases don't re-bump. The version slug (`v2.9.3` vs further) is confirmed at PLAN review.
- **Live install sync:** Phases 0, 1, 2 do not touch the live install. Phase 3 reads via `mo2_record_detail` against the live install AND writes test patches via `mo2_create_patch` (test ESPs go to `<modlist>/mods/Claude Output/`, deleted post-verification). Phase 4 fix sessions live-sync only when the bug requires verification on the live install. Phase 5 live-syncs once and ships.
- **Probe-first discipline:** Phase 1 starts with the inventory probe + record-shape sweep. Any Phase 4 fix that touches PerkEffect factory logic begins with a probe demonstrating the failure mode.
- **One phase per session, with conductor-mediated handoff between phases.**
- **Don't touch out-of-phase files.** Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves you spot during work.
- **No changes to MCP tool request/response shapes** unless a Phase 4 fix requires it. Phase 2 adds capability via the existing `set_fields` field on PERK — no shape change beyond the new `type:` discriminator key on Effects entries.
- **Double-commit cadence per phase** (work commit + hash-record commit), matching v2.7.1/v2.8.0/v2.9.0/v2.9.1/v2.9.2.

---

## 🔁 Handoff template

Every phase ends by writing `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md`) in this directory. **Do not write the next phase's kickoff prompt — the conductor owns that.** Use this exact structure:

```markdown
# Phase N Handoff — <one-line summary>

**Phase:** N (or "4 — <slug>")
**Status:** Complete | Partial | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** <hashes or "none">
**Live install synced:** Yes/No (path: ...)

## What was done
<Bulleted list of concrete changes — file paths + one-line descriptions.>

## Verification performed
<What tests / smoke checks ran. What evidence shows it worked. For Phase 1: probe output + per-subclass shape table + Activator constructibility evidence. For Phase 2: per-subclass functional probe results + coverage-smoke counts + end-to-end MCP→bridge smoke + composition probe (v2.9.0 dispatcher × PerkConditions). For Phase 3: per-scenario assertion checklist + live-install readback evidence. For Phase 4: probe evidence pre-fix + post-fix.>

## Bugs surfaced (Phase 2, Phase 3 only)
<Per-bug entry: short slug; subclass + axis (factory / nested-conditions / composed); reproduction; failure mode; proposed fix angle.>

## Deviations from plan
<Anything you did differently from PLAN.md. Why. If you didn't deviate, write "None.">

## Known issues / open questions
<Bugs you found but didn't fix (with reason). Questions the next phase needs to answer. If none, write "None.">

## Conductor asks
<Optional. Use the format from § Communicating with the conductor at the top of PLAN.md. If none, omit.>

## Preconditions for next phase
<Confirm each precondition the next phase requires per PLAN.md. Flag any not met.>

## Files of interest for next phase
<List paths the next phase will most need to read.>
```

Keep handoffs short — under 400 lines.

---

# PHASES

---

## Phase 0 — Plan + matrix specification + design proposal

**Goal:** Produce `MATRIX.md`, the per-cell test specification scaffolding for v2.9.3. Pre-spec Layer 1 / 2 / 4 cells against vanilla Skyrim.esm and Layer 3 workflow scenarios against the live Authoria modlist (Requiem perk overrides as the canonical anchor). Surface design questions to Aaron via the conductor: Q1 discriminator strategy (default: explicit `type:` field), Q2 Pareto vs full coverage (default: ship the full subclass set), Q3 replace semantics (sanity-confirm; default: replace), Q4 v2.9.0 dispatcher composition (sanity-confirm; default: untouched composition verified at Phase 2 probe time), Q5 discriminator canonical form (default: full Mutagen subclass name e.g. "PerkEntryPointEffect"), Q6 QUST sub-records absorb-or-defer (default: defer), Q7 PerkConditions nested-list shape (default: wrapper-object DSL with RunOnTabIndex + nested Conditions). **No production code changes. No version bump.**

**Note on cadence.** This scoping session produced `PLAN.md` and `CONDUCTOR_KICKOFF.md` directly. The Phase 0 in-session work is: write `MATRIX.md`, write `PHASE_0_HANDOFF.md`, populate § Conductor asks with Q1–Q7, and bundle the four artifacts (PLAN + MATRIX + CONDUCTOR_KICKOFF + PHASE_0_HANDOFF) into a single work commit + hash-record commit pair.

**Files to touch:**
- `<plan>/PLAN.md` (this file — already written; force-add)
- `<plan>/MATRIX.md` (NEW)
- `<plan>/CONDUCTOR_KICKOFF.md` (NEW; already written by the scoping session — force-add)
- `<plan>/PHASE_0_HANDOFF.md` (NEW — written at end)

**Conductor decisions relevant to this phase:**
- The version slug `v2.9.3` is **locked** (Aaron 2026-04-28). Phase 0 records the locked slug; Phase 2 commits the actual version bump (no version bump in Phase 0).
- Phase 0 does not touch the inventory probe — that's Phase 1's deliverable.
- Phase 0's single-commit deliverable bundling is the conductor's structural lock per § J above.

### Steps

1. **Verify session start.** Confirm `origin/main` is at v2.9.2 ship commit (the conductor's kickoff prompt will name the exact hash) and clean. Live install at `<live>` running v2.9.2 (`mo2_ping` returns `version: "2.9.2"`).

2. **Draft `MATRIX.md`** with the five-layer scaffold mirroring v2.9.2's MATRIX.md but anchored on PERK.Effects' subclass-shaped axes (Phase 1 fills in concrete subclass names from the audit; Phase 0 lays the structural cells with placeholders):
   - **Layer 1 — Per-subclass coverage (positives).** Cells: `1.P.PerkEntryPointEffect.minimal` (single PerkEntryPointEffect with EntryPoint + Modification + Value, no nested conditions), `1.P.PerkEntryPointEffect.with_perk_conditions` (with single-tab nested conditions), `1.P.PerkEntryPointEffect.with_v290_params` (nested condition uses `parameters: {Object: ...}` — verifies v2.9.0 composition end-to-end), `1.P.PerkAbility.basic` (PerkAbility with a single Ability FormLink — assumes PerkAbility shape per Phase 1 audit), `1.P.PerkQuestEffect.basic` (PerkQuestEffect with Quest FormLink + Stage Int32), one cell per additional subclass Phase 1 enumerates. Each row: cell ID, subclass, source record, expected payload shape.
   - **Layer 1.D — Negatives + new explicit error paths.**
     - `1.D.01` — `Effects: [{type: "BogusType", ...}]` against PERK → discriminator-not-found error per § A factory.
     - `1.D.02` — `Effects: [{type: "APerkEffect", ...}]` (the abstract base supplied as type) → abstract-type rejection error.
     - `1.D.03` — `Effects: [{...}]` with NO `type:` field → missing-discriminator error.
     - `1.D.04` — `Effects: [{type: "PerkEntryPointEffect", PerkConditions: [{...}]}]` where the nested condition's `function:` is a v2.9.0-out-of-scope function with `parameters` → clean per-record "not yet wired" error from v2.9.0's existing dispatcher (composition unchanged).
     - `1.D.05` — `Effects: [{type: "PerkEntryPointEffect", BogusField: "x"}]` → SetPropertyByPath unknown-property error (Branch B / Tier C-shape error, existing path).
     - `1.D.06` — `set_fields: {Effects: [...]}` against a record type that's neither in v2.8.0's carrier set NOR PERK (e.g. NPC_) → carrier-rejection error (existing v2.8.0 code path; verify v2.9.3's PERK addition didn't break it).
     - `1.D.07` — `Effects: [{type: "PerkEntryPointEffect"}]` (mismatched type — PerkEntryPointEffect on a non-PERK record) → would not normally reach here because the Effects-array path is record-type-gated upstream; cell verifies the gating remains correct.
   - **Layer 2 — Combinatorial.**
     - `2.01` — Multi-effect array with mixed subclasses: `Effects: [{type: "PerkEntryPointEffect", ...}, {type: "PerkAbility", ...}, {type: "PerkQuestEffect", ...}]`. Verifies the factory dispatches per-element correctly and the resulting ExtendedList<APerkEffect> contains heterogeneous concrete entries.
     - `2.02` — `Effects: [{...}]` composed with `set_fields: {Level: 25, NumRanks: 3, Trait: true}` (PERK top-level scalars per KNOWN_ISSUES.md schema observation) on the same record. Verifies replace-semantics on Effects + Tier C scalar writes coexist.
     - `2.03` — `Effects: [{type: "PerkEntryPointEffect", PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", parameters: {Perk: "Skyrim.esm:..."}}]}]}]` — full composition: Branch A → BuildPerkEffectFromJson → PerkCondition wrapper → BuildConditionFromJson → v2.9.0 RouteParameterSlot. Single cell exercising every layer.
     - `2.04` — `Effects: []` empty-array clear on PERK → readback Effects.Count=0; mirrors v2.8.0 Test 29 (1.E.07).
   - **Layer 3 — Workflow scenario on live.** 1 scenario: patch a real Authoria PERK record (Requiem-style, e.g. `Skyrim.esm:10FCFA` AugmentedShock60) — replace its single PerkEntryPointEffect to change the Value from 1.5 → 1.4. Phase 3 picks the live FormID at execution time. Optional 2nd scenario: a multi-effect PERK (e.g. `Skyrim.esm:02BA1D` PlayerWerewolfFeed) — replace the Effects array preserving subclass mix.
   - **Layer 4 — Edges.**
     - `4.dsl.01` — Round-trip: write Effects, read back via `mo2_record_detail`, confirm payload shape matches expected per v2.9.2's read-side render.
     - `4.dsl.02` — Cross-master FormLink in nested condition (`parameters: {Perk: "OtherEsp:01ABCD"}`) — verifies v2.6.0's `WithLoadOrder` writes correct compacted FormLinks for ESL-flagged masters.
     - `4.dsl.03` — `Effects: [{type: "PerkEntryPointEffect", EntryPoint: "BogusEntryPoint"}]` → enum parse error per v2.9.0's enum branch (existing code path; verifies enum dispatch works on PerkEntryPointEffect.EntryPoint same as on ConditionData enum slots).
     - `4.dsl.04` — Empty PerkConditions list (`PerkConditions: []`) on a PerkEntryPointEffect — verifies non-failure.
     - `4.dsl.05` — Sibling preservation: write Effects without modifying PERK.Conditions or PERK.Description → readback confirms PERK top-level Conditions and Description sibling fields untouched (Branch B in-place merge invariant — but Effects is replace-semantics, not merge, so this verifies the array-replace doesn't bleed into top-level scalars).
   - **Layer 5 — Regression.** All v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 coverage-smoke cells run unchanged. Specifically: every existing `set_fields: {Effects: [...]}` invocation pattern on SPEL/ALCH/ENCH/SCRL/INGR stays bit-identical (the pre-existing carrier set is unaffected by adding PERK to it).
3. **Pre-spec Layer 3 workflow scenario** with placeholder FormIDs from the live Authoria modlist that Phase 3 will swap. Anchor on the consumer's PERK-rebalancing case: a Requiem-style perk override that changes a PerkEntryPointEffect's magnitude. Optional 2nd scenario: a multi-effect perk (Werewolf/Vampire-style) preserving subclass diversity.

4. **Surface design questions to Aaron via conductor ask** in PHASE_0_HANDOFF.md § Conductor asks (token-efficient bullets):
   - **Q1: Discriminator strategy.** Explicit `type:` field per element vs distinguish-by-fields-populated (Option B) vs implicit-via-EntryPoint-presence (Option C)? Phase 0 default: Option A (explicit `type:`). Rationale per § B.
   - **Q2: Pareto vs full coverage.** Ship every concrete `APerkEffect` subclass Phase 1 enumerates, OR Pareto-defer obscure tail subclasses? Phase 0 default: ship full set unless Phase 1 surfaces a complexity cliff. Rationale per § E.
   - **Q3: Replace semantics.** Whole-array assignment clears source + writes new (matches v2.8.0)? Phase 0 default: replace — sanity-confirm. Rationale per § D.
   - **Q4: v2.9.0 composition.** v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` compose untouched for nested PerkConditions[*].Conditions[*].parameters? Phase 0 default: untouched composition; Phase 2 verifies via probe. Rationale per § F.
   - **Q5: Discriminator canonical form.** Full Mutagen subclass name (e.g. `"PerkEntryPointEffect"`) vs short tag (e.g. `"entry_point"`)? Phase 0 default: full Mutagen subclass name — matches v2.8.0's `function:` reflection-property-name convention; Mutagen-rename-safe within a major version. Rationale per § B.5.
   - **Q6: QUST.Aliases / Stages / Objectives absorb-or-defer.** Absorb if Phase 1's audit shows the same Branch A pattern fits cheaply, OR keep PERK.Effects-bounded? Phase 0 default: defer all three QUST sub-records — separate v2.9.x scoping. Rationale per § H.
   - **Q7: PerkConditions nested-list shape.** Wrapper-object DSL `[{RunOnTabIndex, Conditions: [...]}]` (matches read-side render) vs flat `[{function, ..., RunOnTabIndex}]`? Phase 0 default: wrapper-object — matches Mutagen's actual `PerkCondition` LoquiObject shape. Rationale per § C.
5. **Single-commit deliverable per § J.** Force-add: `git add -f Claude_MO2/dev/plans/v2.9.3_perk_effects/{PLAN.md,MATRIX.md,CONDUCTOR_KICKOFF.md,PHASE_0_HANDOFF.md}`.

6. **Write `PHASE_0_HANDOFF.md`** confirming MATRIX scaffold landed, Layer 3 scenarios pre-spec'd, no production code touched, no version bump. Record the locked version slug `v2.9.3` (Aaron 2026-04-28). Include the design-question § Conductor asks block with Q1–Q7.

7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.3 P0] Plan + matrix scaffold + design proposal`
   - Hash-record commit: `[v2.9.3 P0] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 0

- `MATRIX.md` exists with five-layer scaffold + cell-naming convention. Per-subclass rows are placeholders awaiting Phase 1's audit-confirmed subclass names.
- Layer 3 scenario(s) named with use-case description (Requiem perk-rebalancing as the anchor); live-FormID picks deferred to Phase 3.
- `git diff main^` shows: PLAN.md (new), MATRIX.md (new), CONDUCTOR_KICKOFF.md (new), PHASE_0_HANDOFF.md (new). No production code touched.
- Locked version slug `v2.9.3` recorded in handoff (Aaron 2026-04-28).
- § Conductor asks populated with Q1–Q7 in the agreed format.

---

## Phase 1 — APerkEffect inventory probe + record-shape sweep + audit

**Goal:** Quantify the subclass set v2.9.3 must support — concrete subclass count, per-subclass writable property surface, Activator constructibility evidence, anchor sanity-checks. Mirror v2.9.0 P1's `CONDITIONS_AUDIT.md` layout for the audit doc; Phase 2's bridge implementation transcribes the audit, doesn't speculate. **No bridge code changes.** **No version bump.**

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (extend with v2.9.3 P1 inventory + per-subclass-shape sweep section)
- `<plan>/APERK_EFFECTS_AUDIT.md` (NEW — mirrors `<v2.8.0-plan>/EFFECTS_AUDIT.md` and `<v2.9.0-plan>/CONDITIONS_AUDIT.md` layout)
- `<plan>/MATRIX.md` (update post-Phase-1 with confirmed per-subclass cell breakdown + canonical subclass names)
- `<plan>/PHASE_1_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Phase 0's design lock (Q1–Q7) is recorded in Phase 0's handoff under § Conductor asks; the conductor's Phase 1 kickoff prompt restates it as the authoritative locked design. If the kickoff prompt lacks the lock, halt and ask conductor — don't infer from PHASE_0_HANDOFF.md.
- Inventory-shape sign-off is mandatory before Phase 2 begins. Phase 1 ends with the subclass count + per-subclass shape; Phase 1's executor writes the "inventory shape acceptable?" check to its handoff under § Conductor asks. Conductor relays to Aaron if the count is dramatically off the expected band (per § J).
- If the probe surfaces something architecturally unexpected — e.g. `PerkEntryPointEffect` is itself abstract with a third level of subclasses, or `PerkConditions` element type is abstract too, or Mutagen 0.53.1 renamed the abstract base from `APerkEffect` to something else — Phase 1 documents it in PHASE_1_HANDOFF.md and writes a CONDUCTOR ASK for whether to expand v2.9.3's scope or punt to a later release.

### Steps

1. **Read MATRIX.md** to understand the Layer 1 cell shape Phase 1 needs to validate subclass names + property shapes for.

2. **Extend `tools/race-probe/Program.cs` with a v2.9.3 P1 inventory section** appended after the existing v2.9.0 P1 ConditionData inventory block (around line 1490). Mirror the v2.9.0 inventory probe's structure:

   ```csharp
   // ─── Inventory dump ───────────────────────────────────────────────
   Section("v2.9.3 P1 — APerkEffect inventory dump");
   {
       var asm = typeof(ISkyrimMod).Assembly;
       var aPerkEffectBase = asm.GetType("Mutagen.Bethesda.Skyrim.APerkEffect");
       Console.WriteLine($"  APerkEffect base: {aPerkEffectBase?.FullName}  IsAbstract={aPerkEffectBase?.IsAbstract}");

       var concrete = asm.GetTypes()
           .Where(t => t.IsClass && !t.IsAbstract
                    && (t.Namespace?.StartsWith("Mutagen.Bethesda.Skyrim") ?? false)
                    && !t.Name.EndsWith("BinaryOverlay")
                    && aPerkEffectBase != null && aPerkEffectBase.IsAssignableFrom(t))
           .OrderBy(t => t.Name)
           .ToList();

       Console.WriteLine($"  Concrete APerkEffect subclasses: {concrete.Count}");

       // Per-subclass property dump — mirror v2.9.0's GetIsIDConditionData anchor.
       foreach (var t in concrete) {
           Console.WriteLine($"  Subclass: {t.FullName}");
           foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.GetIndexParameters().Length == 0)
                              .OrderBy(p => p.Name)) {
               // Annotate [base] / [function-specific] vs the abstract base.
               // Mark FormLink-typed / Enum-typed / List-typed / sub-LoquiObject-typed.
               ...
           }
       }
   }
   ```

   Goal: capture every concrete subclass's property surface so Phase 2's coverage-smoke cells can write valid JSON for each.

3. **Add Activator constructibility section** — for each concrete subclass, attempt `Activator.CreateInstance(subclassType)` and capture success/exception. Mirror v2.8.0 EFFECTS_AUDIT.md's Constructibility table. Confirms the factory's per-subclass `Activator.CreateInstance` path works for the in-scope subclasses (and that `APerkEffect` itself throws — consistent with `Condition` per v2.8.0).

4. **Add `PerkEntryPointEffect` anchor section** — if `PerkEntryPointEffect` is one of the enumerated concretes:
   - Dump its `EntryPoint` enum: full type name, member count, member names (first 20 + total).
   - Dump its `Function`/`Modification` enum: same.
   - Dump its `PerkConditions` property: type, element type. **Critical:** confirm element type is concrete (`PerkCondition` or whatever Mutagen names it) — if abstract, Phase 1 escalates Q7 to Aaron with revised default (would need its own per-subclass factory inside the wrapper).
   - Round-trip a synthetic `PerkEntryPointEffect` with `EntryPoint=ModSpellMagnitude`, `Modification=Multiply`, `Value=1.5`, one nested PerkCondition with one nested ConditionFloat (mirroring read-side AugmentedShock60 shape) through `WriteToBinary` + `CreateFromBinary` — confirm round-trip is clean.

5. **Add `PerkAbility` / `PerkQuestEffect` anchor sections** — if these subclasses are enumerated:
   - Dump their property surfaces.
   - Round-trip a synthetic instance per subclass through binary write/read to confirm the contract.

6. **Add real-world frequency probe (informational, not Phase-1-gating).** For each concrete subclass: scan vanilla Skyrim.esm + a representative subset of the Authoria load order via Mutagen overlay reads, count how many PERK records contain at least one effect of that subclass. Output a per-subclass-frequency table. Informs Phase 0 Q2 (Pareto vs full coverage) — if Phase 1's count shows e.g. PerkEntryPointEffect at 99% with 2 obscure subclasses at <1%, Aaron can re-evaluate the Q2 default. Phase 1 doesn't act on the frequency directly — that's a Phase-0-Q2-revisit if numbers are surprising.

7. **Build** `cd tools/race-probe && dotnet build -c Release` (zero warnings, zero errors). **Run** `dotnet run -c Release --no-build --project tools/race-probe`. Capture full output to `<workspace>/scratch/v2.9.3-phase-1-perk-inventory.txt`.

8. **Write `<plan>/APERK_EFFECTS_AUDIT.md`** capturing:
   - Probe binary path + source path (race-probe extension lines).
   - Mutagen package version (0.53.1; matches bridge).
   - Probe exit code + date.
   - Constructibility table (per-subclass Activator result; abstract base expected fail).
   - Concrete subclass count.
   - Per-subclass property-surface dump (one section per subclass, mirroring v2.8.0 EFFECTS_AUDIT.md's `Effect class shape` section).
   - PerkEntryPointEffect anchor — EntryPoint enum dump, Function/Modification enum dump, PerkConditions element-type confirmation, round-trip evidence.
   - PerkAbility / PerkQuestEffect anchor — same shape per subclass.
   - Real-world frequency table (informational).
   - Architectural surprises section (à la CONDITIONS_AUDIT.md) — list any Phase-0-default-vs-probe-evidence discrepancies (e.g. "Q1's discriminator default assumed Mutagen names are stable; verified across X subclasses in 0.53.1"). PLAN-amend candidates flagged here.
   - Open questions / future considerations.
   - Bridge SHA after Phase 1 build (none — Phase 1 doesn't build the bridge).

9. **Document findings in PHASE_1_HANDOFF.md:**
   - Concrete subclass count + summary.
   - Per-subclass writable-property summary.
   - Activator constructibility summary.
   - Real-world frequency table.
   - PLAN-amend candidates surfaced (any Q1–Q7 default the audit suggests revising).

10. **Write inventory-acceptance proposal to PHASE_1_HANDOFF.md § Conductor asks:**
    - Subclass count vs expected band — flag any surprises.
    - PLAN-amend candidates — list each with a rationale; conductor relays to Aaron.
    - Default-if-no-response: proceed to Phase 2 with the locked design + Phase 1's audit as the schema-description anchor.
11. **Halt and let the conductor relay to Aaron** if the inventory shape is dramatically off (per § J — conductor escalates only if the mechanism's value proposition shifts; otherwise auto-acceptance).

12. **Once the lock is in** (either via conductor relay or auto-accept), update MATRIX.md Layer 1 / 1.D / 2 / 4 rows with confirmed subclass names + canonical property names + Phase 1's frequency table as expected-coverage annotations.

13. **Force-add updated MATRIX.md + APERK_EFFECTS_AUDIT.md.**

14. **Write `PHASE_1_HANDOFF.md`** documenting:
    - Probe build + run evidence.
    - Audit doc summary (subclass count, key findings).
    - PLAN-amend candidates if any.
    - MATRIX update status (done in this session, or pending Phase 2 first-step depending on lock cadence).
15. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.3 P1] APerkEffect inventory probe + audit`
    - Hash-record commit: `[v2.9.3 P1] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 1

- Inventory probe runs to completion; concrete subclass count + per-subclass shape table captured.
- Activator constructibility table populated (abstract base expected fail; concrete subclasses expected pass).
- PerkEntryPointEffect anchor section: EntryPoint enum + Function/Modification enum + PerkConditions element type confirmed concrete + round-trip evidence.
- Real-world frequency table populated.
- `APERK_EFFECTS_AUDIT.md` exists at `<plan>/APERK_EFFECTS_AUDIT.md` with the full v2.8.0/v2.9.0-style structure.
- Race-probe build clean (0 warnings, 0 errors).
- MATRIX.md updated (or noted as pending Phase 2 first-step if lock landed too late in this session).
- Handoff under 400 lines; § Conductor asks populated only if a PLAN-amend candidate or count surprise needs Aaron's call (else auto-accept).

---

## Phase 2 — Bridge implementation + Python wrapper + functional probes + coverage-smoke regression cells

**Goal:** Implement the PERK.Effects write capability per § A–F. Add the `typeof(APerkEffect)` special-case to `ConvertJsonElementToListItem` in `PatchEngine.cs`. Add a new `BuildPerkEffectFromJson` factory near `BuildConditionFromJson` (line ~2331). Optionally add a strongly-typed `PerkEffectEntry` to `Models.cs` (Phase 2 picks based on Phase 1's findings — `JsonElement.Deserialize` may suffice). Add the carrier-type-set extension so PERK is accepted by the existing carrier check (Phase 1's audit informs whether the v2.8.0 carrier check needs explicit PERK addition or whether the dispatch is purely property-type-driven). Update `tools_patching.py` `set_fields` schema description with the PERK Effects-array form + discriminator. Lay down per-subclass functional probes in race-probe + coverage-smoke cells per MATRIX. Bump version to v2.9.3.

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (per-subclass functional probes; the inventory section from Phase 1 stays)
- `<repo>/tools/mutagen-bridge/PatchEngine.cs` (`ConvertJsonElementToListItem` Branch A extension for `typeof(APerkEffect)`; new `BuildPerkEffectFromJson` factory; carrier-type-set extension if Phase 1 found one)
- `<repo>/tools/mutagen-bridge/Models.cs` (optional: new `PerkEffectEntry` strongly-typed wrapper if Phase 2 picks that path; matches `ConditionEntry` structure)
- `<repo>/tools/coverage-smoke/Program.cs` (per-subclass + Layer 1.D + Layer 2 + Layer 4 cells per MATRIX)
- `<repo>/mo2_mcp/tools_patching.py` (`set_fields` schema description appended with PERK Effects-array form + discriminator + composition note)
- `<repo>/mo2_mcp/CHANGELOG.md` (new `## v2.9.3 — TBD` entry; Phase 2 bullet)
- `<repo>/mo2_mcp/config.py` (`PLUGIN_VERSION = (2, 9, 3)`)
- `<repo>/installer/claude-mo2-installer.iss` (`#define AppVersion "2.9.3"`)
- `<repo>/README.md` (installer download URL → v2.9.3 — both occurrences per v2.9.1/v2.9.2 P2 pattern)
- `<repo>/KNOWN_ISSUES.md` (move PERK.Effects out of carry-over; QUST.Aliases / Stages / Objectives stays carry-over; add a new entry under "Covered as of v2.9.3")
- `<plan>/PHASE_2_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- The Phase 0 design lock (Q1–Q7) and the Phase 1 inventory acceptance are recorded in their respective handoffs under § Conductor asks; the conductor's Phase 2 kickoff prompt restates both as authoritative. If the kickoff prompt lacks either, halt and ask conductor — don't infer from prior handoffs.
- **No expansion of mechanism scope beyond v2.9.3's PERK.Effects** without explicit conductor approval. If Phase 1 surfaced a tempting absorb of QUST sub-records, it stays deferred to v2.9.x — even if Phase 2's wiring would be cheap.
- **Existing Effects-list path (SPEL/ALCH/ENCH/SCRL/INGR) MUST stay bit-identical.** All v2.8.0/v2.9.0/v2.9.1/v2.9.2 coverage-smoke cells must stay green. The PERK addition is purely additive; existing carrier-set code paths (every patching test's Effects-array smoke) must behave bit-identically.
- **v2.9.0 dispatcher composition is read-only.** `RouteParameterSlot` + `KnownParameterizedFunctions` + `BuildCondition` foreach are NOT modified. If a composition gap surfaces during Phase 2's probe, halt and surface as a CONDUCTOR ASK — do not patch the dispatcher inside Phase 2's scope.
- **v2.9.1 P4 passthrough lesson.** v2.9.1 P4 caught a Python wrapper passthrough gap. v2.9.3's PERK addition routes through the existing `set_fields` → `passthrough_keys` → bridge model `SetFields` chain — already wired. **End-to-end MCP→bridge round-trip MUST be exercised in Phase 2's smoke before declaring acceptance**, not just direct-bridge race-probe + coverage-smoke. Specifically: a real `mo2_create_patch` call against a vanilla Skyrim PERK record with `set_fields: {Effects: [{type: "PerkEntryPointEffect", ...}]}` to exercise the full path.

### Steps

1. **Confirm Phase 0 + Phase 1 locks** from kickoff prompt. State both back to Aaron in your acknowledgement: design-lock summary (Q1–Q7) + inventory summary (Phase 1's enumerated subclass set + canonical names + frequency).

2. **Read APERK_EFFECTS_AUDIT.md** for the exact subclass names + property shapes Phase 2 transcribes. Phase 2 uses Phase 1's findings — don't speculate on subclass names or property names.

3. **Locate the v2.8.0 carrier-set check.** Phase 1 should have noted whether the v2.8.0 Effects-list dispatch is record-type-gated (carrier list) or property-type-driven (any record with an `Effects: ExtendedList<X>` property where X is constructible/has-special-case). If gated, the gate is in `PatchEngine.cs` near `ApplyEffectsListWrite` (search for the SPEL/ALCH/ENCH/SCRL/INGR list). Add PERK to the carrier set with a comment naming v2.9.3.

4. **Extend `ConvertJsonElementToListItem`** with the `typeof(APerkEffect)` special-case:

   ```csharp
   if (element.ValueKind == JsonValueKind.Object && elementType.IsClass && elementType != typeof(string))
   {
       if (elementType == typeof(Condition))
           return BuildConditionFromJson(element);

       // v2.9.3 — APerkEffect is abstract (PerkEntryPointEffect / PerkAbility / PerkQuestEffect / etc.).
       // Generic Activator path can't construct an abstract class; route to the per-subclass factory.
       // Mirrors v2.8.0's typeof(Condition) special case.
       if (elementType == typeof(APerkEffect))
           return BuildPerkEffectFromJson(element);

       // existing fallback ...
   }
   ```

5. **Add the `BuildPerkEffectFromJson` factory** near `BuildConditionFromJson` (line ~2331). Mirror its structure:

   ```csharp
   /// <summary>
   /// v2.9.3 — JsonElement entry-point for constructing concrete APerkEffect subclasses.
   /// Used by Branch A in <see cref="ConvertJsonElementToListItem"/> when the array
   /// element type is <c>typeof(APerkEffect)</c>.
   /// Discriminator: explicit "type" field naming the concrete subclass per Phase 0 Q1
   /// lock (e.g. "PerkEntryPointEffect", "PerkAbility", "PerkQuestEffect"). The
   /// canonical name matches the Mutagen.Bethesda.Skyrim.* class name.
   /// </summary>
   private static APerkEffect BuildPerkEffectFromJson(JsonElement entryJson)
   {
       if (entryJson.ValueKind != JsonValueKind.Object)
           throw new ArgumentException(
               $"PERK Effect entry must be a JSON object, got {entryJson.ValueKind}.");

       if (!entryJson.TryGetProperty("type", out var typeElem) || typeElem.ValueKind != JsonValueKind.String)
           throw new ArgumentException(
               "PERK Effect entry requires a 'type' field naming the concrete subclass " +
               "(e.g. \"PerkEntryPointEffect\", \"PerkAbility\", \"PerkQuestEffect\"). " +
               "See KNOWN_ISSUES.md § Patching write surface for the v2.9.3 in-scope subclass list.");

       var typeName = typeElem.GetString()!;
       var fullTypeName = $"Mutagen.Bethesda.Skyrim.{typeName}";
       var resolved = typeof(ISkyrimMod).Assembly.GetType(fullTypeName);
       if (resolved == null || resolved.IsAbstract || !typeof(APerkEffect).IsAssignableFrom(resolved))
           throw new ArgumentException(
               $"Unknown PERK Effect subclass: '{typeName}'. " +
               "Expected a concrete subclass of APerkEffect — see KNOWN_ISSUES.md § Patching write surface.");

       var entry = (APerkEffect)System.Activator.CreateInstance(resolved)!;

       foreach (var member in entryJson.EnumerateObject())
       {
           if (member.Name == "type") continue;  // discriminator consumed above
           SetPropertyByPath(entry, member.Name, member.Value);
       }
       return entry;
   }
   ```

6. **Build the bridge:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

7. **Extend `tools/race-probe/Program.cs` with per-subclass functional probes.** For each concrete subclass:
   - Construct a synthetic in-memory PERK record.
   - Build a `PatchRequest` with `set_fields: {Effects: [{type: "...", ...}]}`.
   - Pipe a synthetic `bridge_request` through `mutagen-bridge.exe`.
   - Read back the response JSON and assert the patch ESP's PERK record's Effects.Count + Effects[0] runtime type + Effects[0]'s subclass-specific properties.
   - Cover error paths: missing `type:`, bogus `type:`, abstract-type rejection, field-on-wrong-subclass, mixed-subclass array.
8. **Critical composition probe.** Build a `PerkEntryPointEffect` entry with nested `PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", parameters: {Perk: "Skyrim.esm:058200"}}]}]` and confirm v2.9.0's `RouteParameterSlot` routes the `Perk` slot through the IFormLinkOrIndex<T> branch unchanged. Round-trip via binary write+read; assert the inner ConditionData's Perk FormLink resolves to the supplied FormKey.

9. **Add coverage-smoke regression cells** per MATRIX § Layer 1 + 1.D + 2 + 4 rows. Use existing v2.8.0 Effects-list cells in `coverage-smoke/Program.cs` as templates (tests 23–30 from v2.8.0 P1 are the closest pattern). For each subclass: positive cell (basic shape) + negative cell (where applicable per § Layer 1.D) + at least one Layer 4 edge cell. Layer 2.03 cell composes Branch A → BuildPerkEffectFromJson → PerkCondition wrapper → BuildConditionFromJson → v2.9.0 RouteParameterSlot end-to-end. Keep cell IDs consistent with MATRIX.

10. **Update Python schema description** in `tools_patching.py` for `set_fields`. Append to the existing Effects-array description:

    ```python
    # Existing: SPEL/ALCH/ENCH/SCRL/INGR Effects form + replace semantics.
    # Append for PERK:
    "PERK records also accept set_fields: {Effects: [...]} — same JSON-Array replace-semantics, "
    "but each entry MUST carry a 'type' discriminator field naming the concrete APerkEffect "
    "subclass (e.g. {type: \"PerkEntryPointEffect\", EntryPoint: \"ModSpellMagnitude\", "
    "Modification: \"Multiply\", Value: 1.5, PerkConditions: [{RunOnTabIndex: 1, Conditions: "
    "[{function, operator, value, parameters, ...}]}]} — the inner Conditions take the same "
    "shape as the add_conditions operator and compose with v2.9.0's per-function parameter "
    "dispatcher untouched). v2.9.3 in-scope subclasses: {Phase 1's enumerated list}. "
    "Mutagen subclass names are case-sensitive; the discriminator value matches the "
    "Mutagen.Bethesda.Skyrim.* class name."
    ```

11. **End-to-end MCP→bridge smoke** (per § Conductor decisions — v2.9.1 P4 lesson). Spin up the local MCP server, call `mo2_create_patch` with: (a) a vanilla v2.8.0-shape SPEL Effects write (regression — must be bit-identical); (b) a PERK Effects write with `type: "PerkEntryPointEffect"` (basic case); (c) PERK with multi-effect mixed-subclass array (composition); (d) PERK with nested PerkConditions using v2.9.0 `parameters` dispatch (full composition). Confirm each call returns the expected shape end-to-end through the wrapper.

12. **Run coverage-smoke end-to-end.** `dotnet run -c Release --no-build --project tools/coverage-smoke`. Capture full output to `<workspace>/scratch/v2.9.3-phase-2-coverage.txt`. Expected: all v2.8.0/v2.9.0/v2.9.1/v2.9.2 cells pass + N new cells pass (~10–15 new cells per subclass count from Phase 1). All green.

13. **Update `KNOWN_ISSUES.md`:**
    - Move "PERK.Effects" off the carry-over line in § Patching write surface. The line currently reads "QUST.Aliases / Stages / Objectives, PERK.Effects." Update to "QUST.Aliases / Stages / Objectives" only (PERK.Effects closed).
    - Add new entry under "Covered as of v2.9.3": "PERK.Effects write capability — `set_fields: {Effects: [{type: <subclass>, ...}]}` accepts every concrete APerkEffect subclass per Phase 1's audit ({list}). Replace semantics, mirroring v2.8.0's SPEL/ALCH/ENCH/SCRL/INGR Effects-list. PerkEntryPointEffect's nested PerkConditions list compose with v2.9.0's per-function parameter dispatcher untouched."

14. **Add CHANGELOG entry:**

    ```markdown
    ## v2.9.3 — TBD

    <Phase 5 fills in date.>

    ### Added — bridge

    - **PERK.Effects writability (`set_fields: {Effects: [...]}` on PERK records).**
      Closes the heavier half of the v2.8.0 carry-over "QUST.Aliases / Stages /
      Objectives, PERK.Effects." `Perk.Effects` is `ExtendedList<APerkEffect>`
      where `APerkEffect` is abstract; v2.9.3's per-subclass factory
      (`BuildPerkEffectFromJson`) routes off an explicit `type:` discriminator
      naming the concrete subclass (e.g. `PerkEntryPointEffect`, `PerkAbility`,
      `PerkQuestEffect` — see KNOWN_ISSUES.md for the full v2.9.3 in-scope set).
      The factory mirrors v2.8.0's `BuildConditionFromJson` extracted-from-
      `ApplyAddConditions` pattern. Replace-semantics on the Effects array,
      matching v2.8.0's posture for SPEL/ALCH/ENCH/SCRL/INGR. Nested
      `PerkConditions` carry per-tab `RunOnTabIndex` + standard condition
      lists; the inner condition entries compose with v2.9.0's per-function
      parameter dispatcher (`RouteParameterSlot` + `KnownParameterizedFunctions`)
      untouched — `parameters: {Perk: <FormID>}` on a `HasPerk` condition
      inside `Effects[i].PerkConditions[j].Conditions[k]` works exactly as it
      does at top-level Conditions or inside Effect.Conditions. Real consumer
      signal: Authoria's Requiem-derived modlist carries ~1900 PERK records;
      perk magnitude rebalancing, condition restructuring, and spell-grant
      swaps are the unblocked workflows.

    <Subsequent phases append entries.>

    ---
    ```

15. **Bump version constants:**
    - `config.py`: `PLUGIN_VERSION = (2, 9, 3)`.
    - `claude-mo2-installer.iss`: `#define AppVersion "2.9.3"`.
    - `README.md`: replace v2.9.2 references at lines 7 and 59 with v2.9.3.

16. **Write `PHASE_2_HANDOFF.md`** documenting:
    - Branch A extension + `BuildPerkEffectFromJson` implementation hunks + signatures.
    - Per-subclass functional probe results.
    - Composition probe results (v2.9.0 dispatcher × PerkConditions).
    - End-to-end MCP→bridge smoke results (per § P4 lesson).
    - Coverage-smoke total counts (pre-existing + new = total; PASS / FAIL / SKIP).
    - Schema description diff.
    - CHANGELOG / KNOWN_ISSUES diffs (PERK.Effects off carry-over; new "Covered as of v2.9.3" entry).
    - Version bump landed.
    - Bonus-catch decisions (anything related the phase touched and folded in).
17. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.3 P2] PERK.Effects writability + version bump to v2.9.3`
    - Hash-record commit: `[v2.9.3 P2] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 2

- Phase 1-confirmed subclass names + property names transcribed into bridge code; no speculation.
- Bridge builds clean (0 warnings, 0 errors).
- Per-subclass functional probes pass via Mutagen-direct + bridge-subprocess round-trip.
- Composition probe confirms v2.9.0 dispatcher composes untouched.
- End-to-end MCP→bridge smoke confirms `set_fields: {Effects: [{type: <subclass>, ...}]}` routes through the wrapper to the bridge correctly (per v2.9.1 P4 lesson).
- Coverage-smoke runs to total (v2.8.0/v2.9.0/v2.9.1/v2.9.2 baseline + N v2.9.3), all PASS or documented SKIP.
- Version bumped in all four version-bearing files.
- Schema description, CHANGELOG, KNOWN_ISSUES updated.
- All v2.8.0/v2.9.0/v2.9.1/v2.9.2 coverage-smoke tests stay green (no regression).
- Handoff under 400 lines.

---

## Phase 3 — Workflow scenario(s) on live install

**Goal:** Run the live workflow scenario(s) against the Authoria modlist via `mo2_create_patch`. Patch real Authoria PERK records (Requiem-derived perks the natural anchor — perk magnitude rebalancing on AugmentedShock60-style records). Verify readback via `mo2_record_detail` post-patch. Capture any surfaced bugs.

**Files to touch:**
- `<plan>/PHASE_3_HANDOFF.md`
- (Test patches written to `<modlist>/mods/Claude Output/`; deleted post-verification.)

**Conductor decisions relevant to this phase:**
- Live install must be at v2.9.3 (the conductor's kickoff prompt will confirm this and tell you whether a sync was needed). If `mo2_ping` returns < v2.9.3, halt and ask conductor.
- Scenario is picked from MATRIX.md § Layer 3 (Phase 0 named the use case; Phase 1 confirmed subclass shapes; Phase 3 picks the live FormIDs at execution time). Aaron may swap during Phase 3 if a different PERK record is more representative.

### Steps

1. **Verify live install + MCP server.** `mo2_ping` returns `version: "2.9.3"`. If disconnected or wrong version: halt and ask conductor.

2. **Verify Phase 2's wrapper landed in the live install.** Pre-flight: build a single `mo2_create_patch` call exercising `set_fields: {Effects: [{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", Modification: "Multiply", Value: 1.0}]}` against a vanilla Skyrim.esm PERK FormID (1-effect minimal patch). If the call fails with a discriminator-not-found or carrier-rejection error, the live wrapper is stale — halt and ask conductor to re-sync.

3. **For the Layer 3 scenario in MATRIX.md:**
   - Confirm the target PERK record exists and is overridden meaningfully in the live modlist (Phase 1's anchor proposal ± live verification).
   - Build the `mo2_create_patch` call with `set_fields: {Effects: [...]}` per the scenario's intent (e.g. AugmentedShock60 magnitude change from 1.5 → 1.4).
   - Capture response.
   - **Readback:** call `mo2_record_detail` against the patch ESP's PERK record, confirm Effects array content matches expectation (subclass type, EntryPoint enum value, Modification + Value, nested PerkConditions structure).
   - **Sibling preservation:** confirm PERK top-level fields (Name, Description, Conditions, Trait, Level, NumRanks, Playable, Hidden) match source — only Effects was replaced.
   - Capture per-scenario result table in handoff.
4. **Optional 2nd scenario.** Multi-effect PERK (e.g. PlayerWerewolfFeed-style) — replace the entire Effects array preserving subclass diversity. Verifies mixed-subclass write path against live data.

5. **Cross-axis rollup.** Summarise pass/fail per Layer 3 assertion. If a pattern of failures emerges (e.g. expansion fails on all PerkEntryPointEffect with multi-tab PerkConditions), group by suspected root cause for Phase 4 triage.

6. **Triage failures.** For each FAIL: bug entry with slug, repro, failure mode, proposed Phase 4 fix angle.

7. **Cleanup.** Delete test ESPs from `<modlist>/mods/Claude Output/`; F5 in MO2 to refresh.

8. **Write `PHASE_3_HANDOFF.md`** documenting:
   - Per-scenario assertion table.
   - Bug list (extending Phase 2's, if any).
   - Live-install readback evidence.
   - § Conductor asks: any decisions for the conductor (e.g. "Phase 4 needed?" recommendation based on findings).
9. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.3 P3] Layer 3 workflow scenario — PERK rebalancing on Authoria, M bugs surfaced`
   - Hash-record commit: `[v2.9.3 P3] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 3

- Layer 3 scenario(s) executed against live Authoria modlist.
- Per-axis assertions documented as pass/fail with response evidence.
- Live-install readback confirms patch shape end-to-end.
- Bug list extended with workflow-scenario finds.
- Handoff § Conductor asks names whether Phase 4 is needed.

---

## Phase 4 — Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL)

**Goal:** Land all v2.9.3-bound bridge fixes, schema enhancements, matrix corrections, and docs hygiene that Phase 2 + Phase 3 surfaced. Conductor decides whether this phase runs at all (skip if zero findings) and whether it splits into sub-sessions per bug if findings don't fit one budget.

**Files to touch:** Variable per finding. Common candidates:
- `<repo>/tools/mutagen-bridge/PatchEngine.cs`
- `<repo>/tools/mutagen-bridge/Models.cs`
- `<repo>/tools/race-probe/Program.cs`
- `<repo>/tools/coverage-smoke/Program.cs`
- `<repo>/mo2_mcp/tools_patching.py`
- `<repo>/mo2_mcp/CHANGELOG.md`
- `<repo>/KNOWN_ISSUES.md`
- `<plan>/PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` per sub-session)

**No version bump in Phase 4** — Phase 2 already bumped.

**Conductor decisions relevant to this phase:**
- Conductor reads Phase 2 + Phase 3 handoffs and writes the kickoff naming the specific items in scope. If multiple items, conductor decides single-session-with-items-N vs sub-session-per-bug based on estimated complexity.
- **Scope-lock for Phase 4:** items the kickoff names are in scope. Other v2.7.1/v2.8.0/v2.9.0/v2.9.1/v2.9.2 carry-overs (Boolean dispatcher branch, sub-B 6 String functions, AMMO enchantment, replace-semantics dict, chained dict access, QUST.Aliases / Stages / Objectives, QuestAlias/QuestLogEntry nested conditions) stay deferred unless the kickoff explicitly absorbs them per Aaron's call. The discipline from v2.8.0 P4 + v2.9.0 P4-INFO + v2.9.1 P4 + v2.9.2 P4 holds: "don't punt v2.9.3-uncovered findings; pre-existing carry-overs not surfaced fresh stay deferred."
- **Bonus-catch precedent (v2.9.3-stricter):** latent-bug fixes in touched code → fold in only if load-bearing. **New write-surface additions of any cost → halt + conductor ask + Aaron decision** (per Aaron 2026-04-28; see § Scope locks bonus-catch posture). Latent-bug >1 h additional → halt + conductor ask + Aaron decision.

### Steps

(Per-item steps depend on what the conductor's kickoff names. The general shape mirrors v2.9.0 / v2.9.1 / v2.9.2 Phase 4: pre-fix probe → fix → regression test → build clean → coverage-smoke green. See v2.9.2 PLAN.md § Phase 4 for the canonical step structure.)

1. **Confirm scope from kickoff.** List the items in scope to Aaron in your acknowledgement.

2. **Per item:** probe → fix → regression test → smoke green.

3. **Build the bridge** post all fixes. Zero warnings, zero errors.

4. **Run coverage-smoke end-to-end.** All cells from prior phases + new regression cells, all PASS.

5. **Re-run end-to-end MCP→bridge smoke** if any item touched the Python wrapper (per v2.9.1 P4 lesson).

6. **Update CHANGELOG + KNOWN_ISSUES** per items landed.

7. **Write `PHASE_4_HANDOFF.md`** documenting per-item completion, smoke counts, change summaries.

8. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.3 P4] Bridge fixes + matrix corrections + docs hygiene`
   - Hash-record commit: `[v2.9.3 P4] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 4

- All items the kickoff named are landed (or partial state is documented in handoff with reason).
- Bridge builds clean.
- Coverage-smoke at total (v2.9.2 baseline + Phase 2 cells + Phase 4 regression cells), all PASS.
- End-to-end MCP→bridge smoke confirms wrapper passthrough integrity if any fix touched the Python layer.
- CHANGELOG + KNOWN_ISSUES updated.
- Handoff under 400 lines.

---

## Phase 5 — Re-run + ship v2.9.3

**Goal:** Final verification pass + ship the v2.9.3 release. Phase 2 guaranteed code changes; this is always a real release.

**Files to touch:**
- `<repo>/build-output/installer/claude-mo2-setup-v2.9.3.exe` (built artifact)
- `<repo>/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `<repo>/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `<plan>/PHASE_5_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Bridge SHA preservation chain matters. Phase 5's `dotnet publish` produces a NEW SHA (different from Phase 2/4's build SHA). That new SHA is the canonical v2.9.3 ship SHA. It must be byte-identical across smoke matrix, installer bundle, and live install. To preserve: build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller`, which rebuilds the bridge and breaks the chain).
- Layer 3 workflow re-run is required if Phase 4 ran (Phase 4 may have introduced bridge changes Phase 3 didn't see). If Phase 4 was skipped, Phase 3's runs satisfy the re-run requirement.
- Full MO2 process restart required after live sync (not just Tools menu Stop/Start). Conductor confirms this in kickoff.
- **End-to-end MCP→bridge smoke** required as part of Phase 5's live sanity check (per v2.9.1 P4 lesson — direct-bridge tests don't catch wrapper passthrough gaps).

### Steps

(Mirrors v2.9.2 Phase 5 — see v2.9.2 PHASE_5_HANDOFF.md for the canonical 12-step ship sequence with halt cadence.)

1. Verify session start (state checks per kickoff).

2. Final coverage-smoke run against latest bridge build. Confirm 100% pass.

3. **If Phase 4 ran:** re-run Layer 3 scenario(s) against the post-Phase-4 bridge. **If Phase 4 skipped:** skip this step.

4. Build production bridge via `dotnet publish`. Capture SHA.

5. Build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller` — preserves SHA chain). Capture installer SHA.

6. Live sync: copy bridge + Python files to `<live>/`. Aaron full-restarts MO2. `mo2_ping` returns v2.9.3.

7. Live sanity check: 3 distinct paths — (a) PERK.Effects write end-to-end (verifies the new mechanism works at SHIP_SHA); (b) v2.9.2 regression — single `formid` + `formids` batch + `expand_links` calls (verifies read-side stays bit-identical); (c) end-to-end MCP→bridge smoke from a live MCP-tool invocation (per v2.9.1 P4 lesson).

8. Insert ship date in CHANGELOG.

9. **Tag + push tag + GitHub release** (PUBLIC; hard to undo). MANDATORY HALT — show Aaron the prepared release-notes draft + exact command sequence; wait for explicit "ship" go-ahead.

10. Update memory (`project_capability_roadmap.md`).

11. Write `PHASE_5_HANDOFF.md`.

12. Final commit + handoff hash-record commit + push.

### Acceptance — Phase 5

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.3` resolves with installer attached.
- `<live>/` running v2.9.3 (`mo2_ping`).
- Memory reflects v2.9.3 shipped.
- SHAs captured.
- Bridge SHA matches across smoke matrix, installer bundle, and live install (single audit anchor).
- Live sanity 3-path check + end-to-end MCP→bridge smoke confirm wrapper integrity.

---

## ⚠️ Carry-overs (NOT addressed in v2.9.3; future-release candidates)

These are explicitly out of scope for v2.9.3 unless real-world testing surfaces them as actually-blocking. If Phase 2/3 surface them as bugs, conductor decides whether to promote to Phase 4 fix scope per the discipline from v2.9.0/v2.9.1/v2.9.2 P4.

1. **QUST.Aliases / Stages / Objectives.** The other half of v2.8.0's "QUST.Aliases / Stages / Objectives, PERK.Effects" carry-over. Same Branch A pattern fits structurally; per-subclass field surface is broader (Faction/Cell FormLinks, package overrides, AI data, log entries with VMAD, Quest+target FormLinks). Phase 0 Q6 default-defer; Aaron's call at PLAN review.
2. **Boolean dispatcher branch** (deferred from v2.9.0 — design-only, no in-scope consumer). PLAN.md v2.9.X § A names six branches; v2.9.0 ships five. First v2.9.x consumer trigger lands the branch + cell + name simultaneously.
3. **6 sub-B Condition functions with String-typed slots** (deferred from v2.9.0): GetGraphVariableFloat, GetGraphVariableInt, GetQuestVariable, GetScriptVariable, GetVMQuestVariable, GetVMScriptVariable. Routing requires accept-any-string operator-surface decision.
4. **QuestAlias / QuestLogEntry nested conditions** (deferred from v2.9.1 — KNOWN_ISSUES.md § Patching write surface). Different mechanism (`condition_path` for nested-major sub-records, similar to v2.9.0's INFO override pattern). v2.9.x candidate. Distinct from QUST.Aliases-write (which is the broader sub-record write mechanism); the nested-conditions surface is a narrower workstream.
5. **AMMO enchantment.** Mutagen schema gap; upstream change required.
6. **Replace-semantics whole-dict assignment** (Tier C dicts). Carried over from v2.7.1.
7. **Chained dict access** (`Foo[Key].Sub`). Carried over from v2.7.1.
8. **GetVATSValueUnknown Mutagen 0.53.1 schema gap.** Deferred from v2.9.0 — bridge dispatcher write is correct; downstream Mutagen serializer throws NotImplementedException. v2.9.x candidate when Mutagen 0.54+ implements the missing override.
9. **Read-surface candidates** (reverse-link search, override-aware FormLink expansion, MaxDepth exposure, cross-call result caching). v2.9.x candidates per the xEdit-clarity vision; sequencing depends on real-consumer signal post-v2.9.3.
10. **`add_perk_effects` / `remove_perk_effects` operators.** Per-effect add/remove on PERK without rewriting the whole array. v2.9.x candidate if a consumer surfaces it; v2.9.3 lands `set_fields` (replace) only.
11. **Standalone `add_perk_conditions` / `remove_perk_conditions` operators.** Targeting an existing perk's nested PerkConditions without rewriting the parent effect. v2.9.x candidate; v2.9.3 supports nested PerkConditions only via inline `set_fields: {Effects: [{PerkConditions: [...], ...}]}`.
12. **All v2.6.0 / v2.7.0 / v2.7.1 / v2.8.0 / v2.9.0 / v2.9.1 / v2.9.2 deferrals** — see prior plan handoffs.
