# Known Issues & Limitations

Current as of v2.9.0. These are known limitations, not bugs. For the full version history see `mo2_mcp/CHANGELOG.md`.

---

## Condition-parameter coverage (v2.9.0)

v2.9.0 wires the **generic Condition-function parameter dispatch surface** for 199 functions across five slot-shape branches — Phase 2 feature-complete. 5 of 6 PLAN.md § A branches landed; Boolean is the single design-vs-implementation gap (deferred to first v2.9.x consumer trigger).

- **113 single-`IFormLinkOrIndex<T>` functions (P2A)** — `GetIsID` (Object), `GetInFaction` (Faction), `GetInCell` (Cell), `HasMagicEffect` (MagicEffect), `HasPerk` (Perk), `HasSpell` (Spell), `GetIsRace` (Race), `HasKeyword` (Keyword), `IsInList` (FormList), `GetItemCount` (ItemOrList), `GetEquipped` (ItemOrList), `WornHasKeyword` (Keyword), `GetGlobalValue` (Global), `GetStage` (Quest), `GetQuestRunning` (Quest), `GetIsCurrentWeather` (Weather), `GetIsCurrentPackage` (Package), `IsScenePlaying` (Scene), `GetEquippedShout` (Shout), `IsLastIdlePlayed` (IdleAnimation), `IsPlayerInRegion` (Region), and many others. See `dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` for the full per-function slot signatures.
- **6 sub-A single-`IFormLink<T>` functions (P2A)** — `GetVATSValueCriticalEffect`, `GetVATSValueCriticalEffectOrList`, `GetVATSValueTarget`, `GetVATSValueTargetOrList`, `GetVATSValueWeapon`, `GetVATSValueWeaponOrList` (all with the `Value` slot, distinct concrete shape from the FLI branch).
- **41 single-Enum functions (P2B)** — ActorValue family (`GetActorValue`, `GetBaseActorValue`, `GetActorValuePercent`, `GetPermanentActorValue`, `GetVATSValueTargetPart`, `IsWeaponSkillType`, `EPMagic_IsAdvanceSkill`, `EPMagic_SpellHasSkill` — all `ActorValue` enum), Axis family (`GetAngle`, `GetPos`, `GetVelocity`, `GetStartingAngle`, `GetStartingPos`, `GetPathing*` × 4), CastSource family (`GetCurrentCastingType`, `GetCurrentDeliveryType`, `GetEquippedItemType`, `GetReplacedItemType`, `HasBoundWeaponEquipped`, `HasEquippedSpell`), `GetIsSex`/`GetPCIsSex` (MaleFemaleGender), `GetIsObjectType`/`GetIsUsedItemType` (FormType), `GetIsAlignment` (Alignment), `GetPCMiscStat` (MiscStatEnum), `EPModSkillUsage_IsAdvanceAction` (AdvanceAction), the 6 GetVATSValue* enum-shape functions (CastType/TargetType/WeaponAnimationType + nested GetVATSValueActionConditionData+Action / Projectile+TypeEnum), `IsFurnitureAnimType`/`IsInFurnitureState` (FurnitureAnimType), `IsFurnitureEntryType` (FurnitureEntryType), `IsInCriticalStage` (CriticalStage), `IsPlayerActionActive` (PlayerAction), `IsWardState` (WardState). 18 distinct enum types total. Routing via `Enum.Parse(propType, value, ignoreCase: true)` — case-insensitive parse; numeric input rejected per the documented string-only contract.
- **28 multi-slot functions (P2C)** — `GetStageDone` (Quest FLI + Stage Int32 — Layer 2.01 canonical), `GetEventData` (Function nested-Enum + Member nested-Enum + Record IFormLink — 3-slot mixed-shape, the dispatcher's most architecturally interesting case in v2.9.0), `GetCrime` (CrimeType Enum + Criminal FLI), `GetFactionCombatReaction`/`GetFactionRankDifference`/`GetInCellParam`/`GetKeywordDataForLocation`/`GetRefTypeAliveCount`/`GetRefTypeDeadCount`/`HasAssociationType`/`HasSameEditorLocAsRef`/`IsCellOwner`/`IsCloserToAThanB`/`IsInSameCurrentLocAsRef`/`IsLinkedTo` (dual-FLI), `GetKeywordDataForAlias`/`GetLocAliasRefTypeAliveCount`/`GetLocAliasRefTypeDeadCount`/`HasSameEditorLocAsRefAlias`/`IsInSameCurrentLocAsRefAlias`/`IsSceneActionComplete`/`LocAliasHasKeyword`/`LocAliasIsLocation` (FLI + Int32), `GetRelativeAngle` (Axis Enum + Target FLI), `GetWithinDistance` (Distance Single + Target FLI — only Single-bearing function), `IsCurrentSpell`/`SpellHasKeyword` (FLI + CastSource Enum), `Unknown` (Function Condition+Function nested-Enum + ParameterOne/Two Int32×2 — Mutagen's generic-fallback type for unknown CTDA function codes; bridge dispatch is correct but harness round-trip readback can't anchor on type name because Mutagen reclassifies on read — coverage-smoke registers it as SKIP-with-reason, see [PHASE_2C_HANDOFF.md](dev/plans/v2.9.X_condition_parameters/PHASE_2C_HANDOFF.md)). Multi-slot composition routes through `BuildCondition`'s foreach over `ce.Parameters` — each slot dispatches independently through whichever branch matches its prop type.
- **11 PrimitiveOnly functions (P2D)** — alias-index lookups (`GetIsAliasRef`/ReferenceAliasIndex, `GetInCurrentLocAlias`/`GetIsEditorLocAlias`/`GetLocationAliasCleared`/`IsLocAliasLoaded`/LocationAliasIndex — quest-alias-index gating used by dialog/quest patchers), package-data accessors (`GetNumericPackageData`/`GetWithinPackageLocation`/`IsNullPackageData`/PackageDataIndex), `GetVATSValueUnknown` (Value+ValueType — the genuinely-Int32-typed VATS variant; bridge dispatcher-correct, but Mutagen 0.53.1 schema-gap-blocked at binary write — see § Patching write surface for the missing-`GetValueFunction()`-override gap), `GetPlayerControlsDisabled` (PlayerControlsParameterOne+Two), `IsLimbGone` (Limb). All single- or dual-slot Int32. Wired through the existing P2C Int32 branch — pure `KnownParameterizedFunctions` extension, zero new dispatcher code.
- **`Int32` and `Single` primitive branches (P2C)** — direct `JsonElement.GetInt32()` / `GetSingle()` conversion. `Int32` is reused across 10 multi-slot functions' secondary slots (Stage / *AliasIndex / SceneActionIndex / Unknown's ParameterOne+Two) and 11 PrimitiveOnly functions (all-Int32 P2D set). `Single` is exercised by exactly one in-scope function (GetWithinDistance.Distance).

DSL: each `add_conditions` entry accepts a `parameters: {SlotName: Value}` map. SlotName is the Mutagen reflection property name on the function's `{Function}ConditionData` class. The v2.8 `actor_value` field is preserved as back-compat syntactic sugar for `parameters: {ActorValue: ...}`; supplying both surfaces an unambiguous-DSL error.

Functions outside this set called with `parameters` surface a clean per-record "not yet wired" error naming the function and pointing at this section. Called without `parameters` they preserve v2.7.1+ behavior (structurally-valid but always-false).

Footgun-guard: any slot name containing `"Unused"` is rejected (CTDA padding pattern — these slots exist in Mutagen's schema as a mirror of CTDA's 4-parameter binary format but are never set in practice).

**Boolean primitive — design-only, unimplemented in v2.9.0.** PLAN.md § A names Boolean as one of the dispatcher's six branches, but zero v2.9.0 in-scope functions need it (verified across 199 dispatcher-wired functions). Landing the branch without a coverage-smoke cell or race-probe means an untested path that future Mutagen drift could silently break, so v2.9.0 defers the addition. First v2.9.x consumer trigger lands the branch + cell + name simultaneously. The catch-all error message names "v2.9.0 covers IFormLinkOrIndex<T> + IFormLink<T> + System.Enum + Int32 + Single" — Boolean would only surface there if a Boolean-bearing function were added to `KnownParameterizedFunctions`, which is impossible against the current in-scope set.

### Condition-parameter coverage — gaps still open in v2.9.0

These are deferred within the v2.9.x release line:

- **Boolean dispatcher branch** (design-only in v2.9.0, deferred to v2.9.x). PLAN.md § A names Boolean as one of six dispatcher branches; zero v2.9.0 in-scope functions need it. First v2.9.x consumer trigger lands the branch + cell + name simultaneously.
- **6 sub-B Condition functions with String-typed slots** (deferred to v2.9.x point release). `GetGraphVariableFloat`, `GetGraphVariableInt`, `GetQuestVariable`, `GetScriptVariable`, `GetVMQuestVariable`, `GetVMScriptVariable` — each carries a String-typed `VariableName` or `GraphVariable` slot referencing a Papyrus / Behavior-Graph runtime identifier. Routing requires either a new accept-any-string operator surface or an MCP shape for Papyrus introspection round-trip; defer until a real consumer surfaces.
- **219 NoParam Condition functions** are in-scope-no-op — they accept parameterless invocation as v2.7.1+ behavior; supplying `parameters` for a NoParam function surfaces the same "not yet wired" error path (since they're not in the in-scope set; the dispatcher correctly identifies that "function has no parameter slots" maps to the same out-of-scope rejection).

---

## Patching write surface — current limitations

These write-surface gaps are not yet covered by the bridge; future-release candidates.

- **Replace-semantics whole-dict assignment** (Tier C dicts). The Effects-list array path uses replace-semantics, but the Tier C dict form (`Starting: {Health: 100, Magicka: 200}`) is uniform merge — keys not present in the JSON are preserved at their source values. A clear-then-set surface for dicts would need a new operator parameter or sentinel value.
- **Chained dict access.** `Foo[Key].Sub` paths are not supported — Tier C is terminal-bracket-only. `set_fields` rejects chained brackets explicitly with a clear error rather than producing wrong behavior.
- **Quest condition disambiguation.** QUST records carry `DialogConditions` and `EventConditions` rather than a single `Conditions` list. `add_conditions`/`remove_conditions` cannot disambiguate without a new operator parameter (e.g. `condition_target: "dialog" | "event"`).
- **Outfit/Spell `attach_scripts`.** The bridge errors with `"Record type Outfit/Spell does not support scripts"` because the concrete `Outfit` and `Spell` types don't expose a `VirtualMachineAdapter` property — Mutagen 0.53.1 schema gap, and Bethesda's vanilla data has no VMAD subrecord on SPEL or OTFT records. Resolving requires an upstream Mutagen schema change.
- **AMMO enchantment.** Mutagen's schema does not expose an `ObjectEffect` slot on Ammunition records. `set_enchantment`/`clear_enchantment` are restricted to ARMO/WEAP. Resolving requires an upstream Mutagen schema change.
- **`GetVATSValueUnknown` Condition function.** The function is in v2.9.0's dispatcher in-scope set (`KnownParameterizedFunctions` carries it; bridge writes `Value` and `ValueType` Int32 slots successfully via reflection), but Mutagen 0.53.1 forgot to override the abstract `AGetVATSValueConditionData.GetValueFunction()` on the `Unknown` subclass — the other six `AGetVATSValue*` concrete subclasses (sub-A IFormLink<T> family) implement it; the Int32-typed Unknown variant does not. Binary serialization throws `NotImplementedException` at the CTDA write step regardless of slot values. Real callers attempting `function: "GetVATSValueUnknown"` get a clean per-record write-time error today. Resolving requires an upstream Mutagen schema change (Mutagen 0.54+ candidate when the missing override lands). Distinct from P2C's `Unknown`-CTDA-round-trip artifact (which was a read-side reclassification — write succeeded; harness type-name lookup couldn't anchor on read).
- **QUST.Aliases / Stages / Objectives, PERK.Effects.** Out of scope for the current Effects-list mechanism even though the schema shape is similar — sub-class polymorphism makes them harder, and no real consumer has surfaced yet.

### Schema observations (useful when authoring patches)

- **PERK has no `Configuration` sub-object.** Unlike NPC (where `set_fields: {Configuration: {Health: 200}}` works via Tier B aliases or sub-LoquiObject merge), Mutagen 0.53.1's PERK schema declares its writable scalars at the top level: `Level`, `NumRanks`, `Trait`, `Playable`, `Hidden`. Single-field FormLink: `NextPerk` (`IFormLinkNullable<IPerkGetter>`). For PERK reflection writes use `set_fields: {Level: 25, NumRanks: 3, NextPerk: "Skyrim.esm:058214"}` directly — there's no `Configuration.PerkType` path.
- **LVSP merge data-shape constraint on Authoria-style modlists.** `merge_leveled_list` against LVSP records that carry uniform `SPEL References` across plugins (vanilla / USSEP / Requiem-style overhauls that rebalance levels but preserve the entry set) will correctly dedup to `entries_merged: 0`. This is the right outcome — the merge mechanism is verifying no new entries are introduced — but matrix expectations of `entries_merged > 0` won't be met against this modlist topology. Test patches should accept `entries_merged: 0` as a valid pass for LVSP, or use a modlist with diverse LVSP entry sets to exercise the additive path.

---

## User-provided prerequisites

These are by design — we don't bundle proprietary or license-undecidable tools. Missing any of these disables only the capabilities that depend on them; everything else continues to work.

### Papyrus compilation requires Creation Kit

`mo2_compile_script` uses Bethesda's `PapyrusCompiler.exe` and needs the base-Skyrim script sources (`Scripts.zip` ships inside the Creation Kit install). Without them, the compiler fails with "unknown type" errors on SKSE's and base-Skyrim's `.psc` files whenever your script extends `Actor` / `Quest` / etc. or calls anything like `Debug.Notification`.

**Workaround:** Install Creation Kit. Extract `Scripts.zip` into a mod that MO2 sees, so the VFS includes the base headers.

**Impact:** Affects `mo2_compile_script` only. All other MCP tools work without the Creation Kit.

### BSA tools require BSArch.exe

`mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, and `mo2_validate_bsa` shell out through Spooky's CLI to `BSArch.exe`, which we do not redistribute.

**Where to get it:** BSArch ships inside [xEdit](https://github.com/TES5Edit/TES5Edit)'s release archive. Extract it and place `bsarch.exe` at `<plugin>/tools/spooky-cli/tools/bsarch/bsarch.exe`.

**Impact:** Those four archive tools fail clearly until BSArch is installed. Everything else works.

### NIF extras require nif-tool.exe

`mo2_nif_list_textures` and `mo2_nif_shader_info` invoke `nif-tool.exe`, a Rust binary created for Spooky's toolkit. Its license is currently undetermined, so we don't redistribute it.

**Where to get it:** Shipped in Spooky's v1.11.1 release 7z. Place at `<plugin>/tools/spooky-cli/tools/nif-tool/nif-tool.exe`.

**Impact:** Those two tools fail with guidance if the binary is missing. `mo2_nif_info` works without it (library-native via Spooky).

---

## User-provided tools — how they're configured

Since v2.7.0, all four user-provided tool surfaces are configurable through the installer's Optional Tools wizard page. The page detects existing state on upgrade and pre-populates the Edit fields — leave a path as-is to keep, edit it to swap, clear it to skip. JSON-based config lives at `<plugin>/mo2_mcp/tool_paths.json` and survives uninstall.

| Tool | How configured | Refresh method |
|---|---|---|
| BSArch | Installer picker; copied into plugin dir | Re-run installer with new binary |
| nif-tool | Installer picker; copied into plugin dir | Re-run installer with new binary |
| PapyrusCompiler | Installer picker (copy by default; JSON-reference via checkbox) | Re-run installer OR edit `tool_paths.json` |
| Papyrus Scripts sources | `tool_paths.json` (additive to VFS) | Edit `tool_paths.json` + restart MO2 |

**JSON-reference mode for PapyrusCompiler.** The installer's PapyrusCompiler row has a "Reference this path at runtime (don't copy into plugin folder)" checkbox. Checked: the installer writes the picked path into `tool_paths.json["papyrus_compiler"]` and the binary stays in place at the user's existing CK install. Unchecked (default): the picked binary is copied into `<plugin>/tools/spooky-cli/tools/papyrus-compiler/`. JSON-reference wins at runtime if both a copied binary and a JSON path are detected.

**Papyrus Scripts sources is additive.** A configured `papyrus_scripts_dir` does not replace MO2's VFS-aggregated `findFiles` chain — it appends to it. Users who keep an extracted-`Scripts.zip` mod active in MO2 don't need to configure this at all; the JSON path supplements that VFS source for users who prefer to point at a non-MO2-managed extraction (e.g. directly at `<Steam>\Skyrim Special Edition\Data\Source\Scripts`).

---

## Design-trade-off limitations

### Leveled list merges require user judgment

`mo2_create_patch` can merge LVLI / LVLN / LVSP entries across conflicting plugins, but the **base plugin** (whose records are used as-is) must be chosen by the caller. For an overhaul conflict with a content mod, using the vanilla master as base would revert the overhaul's intentional restructuring (deleveling, reweighting) — you want the overhaul as the base and the content mod's unique entries merged in.

See the `leveled-list-patching` skill (`.claude/skills/leveled-list-patching/SKILL.md`) for the reasoning framework.

### Spell conditions apply at effect level

Skyrim spells carry conditions per magic effect, not on the spell record itself — `add_conditions` on a SPEL throws "Record type Spell does not support conditions" because the property doesn't exist at the record level. Per-effect conditions are writable through the Effects-list surface: `set_fields: {Effects: [{BaseEffect: ..., Data: {...}, Conditions: [{function, operator, value, ...}]}]}` puts conditions on each effect entry. Conditioning the underlying MGEF directly works as the alternative.

### RecordReader depth limit

`mo2_record_detail` walks Mutagen object graphs with a depth limit of 6 via reflection. Most records fit easily; extremely deep QUST/PACK/CELL structures could truncate as `"...[max depth reached]"`. If you encounter this, the depth is tunable in the bridge source (`ReadRequest.MaxDepth`).

### `mo2_record_detail` FormID resolution is opt-in

By default, FormIDs in the output are rendered as `Plugin:HexID`. Pass `resolve_links: true` to annotate each with its EditorID via the record index (`"Skyrim.esm:000019"` → `"Skyrim.esm:000019 (NordRace)"`). Opt-in because the extra lookup takes time on large records and most callers don't need it.

### Record queries default to enabled plugins only

By default, the five query tools (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) filter out plugins whose right-pane checkbox is unticked. Rationale: "winning plugin" claims and conflict chains should reflect what the game actually loads at runtime, not every plugin that ever touched the record.

Pass `include_disabled: true` for diagnostic queries ("was this record ever overridden, even by disabled mods?", "what would change if I enabled this plugin?"). When a record only exists in disabled plugins, the error distinguishes "not found" from "found but disabled" and tells the caller how to recover.

Implicit-load plugins (Skyrim.esm, DLC ESMs, Creation Club masters listed in `<game_root>/Skyrim.ccc`) are classified as enabled regardless of `plugins.txt` state — the engine auto-loads them.

---

## Environmental quirks (not code bugs, but worth knowing)

- **Claude Code v2.1.73+ required for skills auto-discovery.** The plugin ships procedures and tool-category references as skills under `.claude/skills/`. Claude Code auto-discovers these when the working directory contains the `.claude/` folder. Versions older than v2.1.73 may not support auto-discovery — the plugin still installs and the MCP tools still work, but task-specific skills (crash diagnostics, mod dissection, category-specific tool reference, etc.) won't fire automatically.
- **Claude Code caches the MCP tool list at session start.** If you start the server in MO2 mid-session, Claude Code doesn't see the new tools until you restart Claude Code.
- **MO2 doesn't reload Python modules on server stop/start.** After editing any `.py` inside the plugin, delete `__pycache__/` AND fully restart MO2 (not just the Tools > Start/Stop Claude Server toggle).
- **Claude Code reconnects to the MCP server automatically** after MO2's auto-stop-on-launch cycle (Skyrim / xEdit / etc.) or after a full MO2 restart, as long as the server comes back on the same HTTP URL. No CC restart needed for reconnection — this is an HTTP transport property; the old stdio-era requirement no longer applies. Only restart CC if you've added new MCP tools (server version change → cached tool list is stale) or changed the server port.
- **External filesystem changes require a manual MO2 refresh.** MO2 does not auto-detect `rm`/`cp`/`mv` of plugin files made outside its API. After any external change to plugin files (via Bash, another tool, or manual intervention), press F5 in MO2 (or use the Refresh button) before calling `mo2_create_patch`, `mo2_build_record_index`, or any read-back against the affected plugin. Skipping this leaves orphans in `loadorder.txt` and new plugins may be missing from the index entirely — symptoms include read-back returning empty even with `include_disabled: true`. Prefer `mo2_write_file` (routes through MO2's output mod, detected immediately) over Bash for plugin-adjacent writes.
- **Large modlists can exceed Claude Code's default MCP timeout on cold force-rebuild.** Claude Code's default MCP tool-call timeout is 60 s; `mo2_build_record_index(force_rebuild=true)` on ~3000+ plugin modlists takes roughly 76 s on reference hardware. The server-side build completes regardless — a follow-up `mo2_record_index_status` call will show `state: "done"` — but the client call appears to time out. **Set `MCP_TIMEOUT=120000` in your environment before launching Claude Code** to avoid the timeout entirely. Normal queries and cache-hit rebuilds stay well under the default.
- **Some plugins are rejected by Mutagen's strict parser.** The record index builds by handing every plugin to Mutagen for enumeration. Mutagen is stricter than xEdit about format conformance — plugins with malformed records (e.g. `DATA` subrecord length mismatches) can scan clean in xEdit but fail in Mutagen. Those plugins are absent from the record index; `mo2_record_index_status` lists them in the `errors` array. If a plugin you care about doesn't appear in query results, run xEdit's **Check for Errors** on it to confirm the state, then have the mod author fix it (or auto-clean via xEdit if feasible). Two plugins in the reference test modlist (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) are known to hit this — ~0.06% scan loss on a 3,384-plugin load order.
- **Inno registry hygiene on multi-instance installs.** v2.7.0's installer uses a static `AppId` and `CreateUninstallRegKey=yes`, so each install to a different MO2 directory writes the same HKCU uninstall key. The most-recent install's path wins for "uninstall this" lookups. For users with one MO2 install: invisible. For users with multiple MO2 instances installing the plugin to each: a clean uninstall reads the latest install path, not necessarily the one being uninstalled. Workaround: uninstall via the MO2-instance-specific `unins000.exe` directly (the per-install copy in each plugin dir always points at the right target). Permanent fix candidate for v2.9: dynamic `AppId` per install path so each install gets its own registry entry.
- **Back-navigation from Dir → Optional Tools preserves user edits.** v2.7.0's Optional Tools picker page seeds its Edit fields once on first entry and keeps them populated through Back-navigation. If you click Back from the Optional Tools page, change the target MO2 directory, then click Next: the picker's Edit values stay populated from the original detection — they don't re-detect against the new target dir. This is intentional (edit survival across Back is the priority); to get fresh detection against a new target, Cancel and restart the installer.

---

## Upstream (Spooky) issues we work around

These are reported or reportable to Spooky upstream; our wrappers already work around them:

- **`archive extract --filter` is ignored upstream.** Our `mo2_extract_bsa` full-extracts to a temp dir then filters on our side. Disk-usage trade-off for correctness; cleanup is automatic.
- **`audio info` rejects valid FUZ files.** Our bridge includes a local FUZ parser (`AudioCommands.cs`) so `mo2_audio_info` and `mo2_extract_fuz` don't depend on Spooky's broken path. XWM/WAV still go through Spooky's CLI.
- **`tools/` resolution is 5-up from the CLI exe.** Spooky's CLI looks for external tools at an unusual relative path. Our direct `PapyrusCompiler.exe` invocation sidesteps this, but if Spooky CLI is ever used directly, the user should be aware.

---

## Not yet implemented

**Papyrus save-file reading.** Can't yet read `.ess` save files to inspect script state at runtime — which scripts are loaded, variable values on suspended stacks, orphan script instances. Planned for Phase G of the roadmap. Static `.psc`/`.pex` analysis (via `mo2_compile_script` + Creation Kit) works today; only in-save runtime state is unavailable.
