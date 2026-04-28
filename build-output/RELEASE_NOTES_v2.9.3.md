# Claude MO2 v2.9.3 — PERK.Effects writability

**Real consumer signal: Authoria's Requiem-derived Electromancy perk rebalanced from 1.2× to 1.05× ModSpellMagnitude with sibling preservation byte-identical — the kind of perk-magnitude tweak that was blocked before this release.**

This release closes the heavier half of v2.8.0's "QUST.Aliases / Stages / Objectives, PERK.Effects" carry-over. PERK records on Skyrim modlists average around ~1900 records on heavy overhauls (Authoria/Requiem-Reforged); perk magnitude rebalancing, condition restructuring, and spell-grant swaps are now unblocked via `set_fields: {Effects: [...]}` on PERK. Defaults preserve all v2.8.0–v2.9.2 behavior bit-identically; existing callers see no change.

## Headline

Patching a real Authoria perk end-to-end:

| State | EditorID | Lead Effect | Modification | Value | Description |
|---|---|---|---|---|---|
| **Vanilla** (Skyrim.esm) | AugmentedShock60 | PerkEntryPointModifyValue | Multiply | 1.5 | "Shock spells do 50% more damage." |
| **Authoria winner** (Requiem - Magic Redone.esp, load order 1187) | REQ_Destruction_Electromancy_050_Electromancy2 | PerkEntryPointModifyValue | Multiply | 1.2 | "Compared to your lightning spells, the worst tempests would look like a mild summer breeze.<br>[1.2x magnitude and duration, 0.8x cost for shock spells]" |
| **v2.9.3 patch target** (this release's sanity check) | (preserved from Requiem) | PerkEntryPointModifyValue | Multiply | **1.05** | (preserved from Requiem) |

The patch replaces the Effects array (replace-semantics) while preserving every top-level sibling: Requiem's renamed Description, the Requiem-lowered `GetActorValue Destruction >= 50` threshold (vanilla was `>= 60`), Trait/Level/NumRanks/Playable/Hidden/EditorID — all byte-identical. That's the kind of AI-driven patcher action this release unblocks.

## What's new

### `set_fields: {Effects: [...]}` on PERK records

`Perk.Effects` is `ExtendedList<APerkEffect>` where `APerkEffect` is abstract. v2.9.3 extends Branch A in `ConvertJsonElementToListItem` with a `typeof(APerkEffect)` special case routing to a new `BuildPerkEffectFromJson` factory (next to `BuildConditionFromJson`). The factory reflects `Mutagen.Bethesda.Skyrim.{TypeName}`, rejects abstract types + non-`APerkEffect`-assignable types, then walks each non-discriminator JSON member through `SetPropertyByPath`. Replace-semantics on the Effects array, mirroring v2.8.0's posture for SPEL/ALCH/ENCH/SCRL/INGR.

Mutagen 0.53.1 exposes **12 concrete `APerkEffect` leaves** under the abstract base + an abstract intermediate (`APerkEntryPointEffect`):

`PerkAbilityEffect` · `PerkEntryPointAbsoluteValue` · `PerkEntryPointAddActivateChoice` · `PerkEntryPointAddLeveledItem` · `PerkEntryPointAddRangeToValue` · `PerkEntryPointModifyActorValue` · `PerkEntryPointModifyValue` · `PerkEntryPointModifyValues` · `PerkEntryPointSelectSpell` · `PerkEntryPointSelectText` · `PerkEntryPointSetText` · `PerkQuestEffect`

Each Effects entry carries an explicit `type:` discriminator naming the concrete leaf. Per-leaf shapes documented in `KNOWN_ISSUES.md` § Covered as of v2.9.3.

### Composition with v2.9.0's condition-parameter dispatcher — UNTOUCHED

Per-effect Conditions on the `APerkEffect` base use a two-level nesting: outer `Conditions` is a list of `PerkCondition` wrappers each carrying `RunOnTabIndex` (int) plus an inner `Conditions` list whose entries take the same shape as the `add_conditions` operator. The inner condition entries compose with v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` dispatcher **without modification** — `parameters: {Perk: <FormID>}` on a `HasPerk` condition inside `Effects[i].Conditions[j].Conditions[k]` works exactly as it does at the top-level `add_conditions` operator. Phase 2's composition probe round-tripped a PEPM Effect with nested `HasPerk` parameters via bridge subprocess; readback walked the full chain Branch A → `BuildPerkEffectFromJson` → `SetPropertyByPath` (outer Conditions) → Branch A (PerkCondition wrapper) → `SetPropertyByPath` (inner Conditions) → Branch A (`typeof(Condition)`) → `BuildConditionFromJson` → `BuildCondition` → v2.9.0 `RouteParameterSlot` → `IFormLinkOrIndex<IPerkGetter>` and resolved the supplied perk FormLink.

### TranslatedString single-language convenience plumbing

`PerkEntryPointSetText.Text` is `Mutagen.Bethesda.Strings.TranslatedString` (a sub-LoquiObject), distinct from `PerkEntryPointSelectText.Text` which is plain `String`. `ConvertJsonValue` now accepts a JSON String for `TranslatedString`-typed slots and writes it as an English-language entry (mirrors the v2.8.0 IFormLinkNullable single-field FormLink branch directly above). Required for PEPSetText to satisfy v2.9.3's "ship all 12 leaves" promise; surface expansion is inert (no other operator advertises TranslatedString slot writes outside this case).

## Verification

- **Coverage-smoke 455/455 PASS or documented SKIP** at SHIP SHA — 425 baseline (v2.8.0 + v2.9.0 + v2.9.1 + v2.9.2) + 30 new v2.9.3 cells (12 Layer 1.P + 7 Layer 1.D negatives + 4 Layer 2 combinatorial + 5 Layer 4 edges + 2 matrix-completion). All 6 SKIPs are pre-v2.9.3 carry-overs.
- **Race-probe** ALL PASS — 12 per-leaf functional probes via bridge subprocess + 6 DSL error-path probes + composition probe (PEPM × nested HasPerk parameters) confirming Q4 lock structurally.
- **End-to-end MCP→bridge wrapper smoke** PASS — static schema↔passthrough cross-check confirms no v2.9.1 P4-class wrapper gap risk; v2.9.3 rides through the existing `set_fields` → `passthrough_keys` → bridge model `SetFields` chain wired since v2.7.x.
- **Live verification on Authoria** — Phase 3: 48/48 axis assertions PASS across two scenarios (Requiem-Electromancy single-leaf rebalance + Dawnguard PlayerWerewolfFeed heterogeneous 3-leaf array including VMAD sibling preservation). Phase 5: 3-path live sanity check at SHIP SHA reproduced Electromancy patch + v2.9.2 read-side regression (single formid + batch + expand_links) + Q6 cross-product MCP→bridge smoke.

## SHAs

- `mutagen-bridge.dll`: `3c003c9f2204e8f2ad4dafc6e98ab7cf54a5b9c5ecfb17cbde76b2b250be5429`
- `mutagen-bridge.exe`: `85835ec8f375700509e55e9011bc7c4c14ced6d9ee8fcc74ca278258dd9c9629`
- `claude-mo2-setup-v2.9.3.exe` (10,639,536 bytes): `83ab3715865d4faff2ce2ede2e217690dea7074b4e3c6644353d1ecbad1d6725`

Single byte-identical anchor across smoke matrix, installer bundle, and live install.

## Backward compatibility

All v2.9.2 callers see bit-identical responses. The PERK Effects-array surface is purely additive — no existing `set_fields` invocation pattern on SPEL/ALCH/ENCH/SCRL/INGR Effects-list changes. The TranslatedString convenience branch is reached only via PEPSetText.Text in the v2.9.3 release.

## What's still deferred

- **QUST.Aliases / Stages / Objectives** — the lighter half of v2.8.0's deferred carry-over. Same abstract-sub-class pattern PERK.Effects closed; broader sub-record surface (Faction/Cell FormLinks, package overrides, AI data, log entries with VMAD, Quest+target FormLinks). v2.9.x candidate; first real-consumer signal triggers scoping.
- **`add_perk_effects` / `remove_perk_effects` operators** — per-effect add/remove on PERK without rewriting the whole array. v2.9.3 lands `set_fields` (replace) only, matching v2.8.0's posture for SPEL/ALCH/ENCH/SCRL/INGR.
- **Standalone `add_perk_conditions` / `remove_perk_conditions`** — targeting an existing perk's nested PerkConditions without rewriting the parent effect. v2.9.3 supports nested PerkConditions only via inline `set_fields: {Effects: [{..., Conditions: [...]}]}`.
- **Read-surface candidates (v2.9.x)** — reverse-link search, override-aware FormLink expansion, MaxDepth MCP-configurable, cross-call result caching. Real-consumer-signal-driven sequencing.

See `KNOWN_ISSUES.md` for the full carry-over inventory.
