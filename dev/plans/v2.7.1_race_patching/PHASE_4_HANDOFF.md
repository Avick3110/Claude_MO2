# Phase 4 Handoff — Tier B aliases + Python schema reconciliation + docs roll-up + production bridge rebuild

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1.25h
**Commits made:** `8d1b190` (`[v2.7.1 P4] Tier B aliases + Python schema + docs roll-up + bridge rebuild`), pushed to `origin/main` alongside this handoff hash-record commit.
**Live install synced:** No (Phase 4 doesn't sync — Phase 5 does the only live sync of v2.7.1).

## What was done

### Bridge changes (`tools/mutagen-bridge/PatchEngine.cs`)

- **Tier B `FieldAliases["RACE"]` block** added immediately after the existing `["ALCH"]` block. Six aliases mapping the user-friendly names to bracket-indexer paths:
  ```csharp
  ["RACE"] = new()
  {
      ["BaseHealth"]   = "Starting[Health]",
      ["BaseMagicka"]  = "Starting[Magicka]",
      ["BaseStamina"]  = "Starting[Stamina]",
      ["HealthRegen"]  = "Regen[Health]",
      ["MagickaRegen"] = "Regen[Magicka]",
      ["StaminaRegen"] = "Regen[Stamina]",
  },
  ```
  Resolution chain: `ApplySetFields` → `RecordTypeCode(record)` returns `"RACE"` (default-fallback `Race.ToUpperInvariant()`) → `FieldAliases["RACE"]` lookup → terminal-bracket path → Tier C's `SetPropertyByPath` dispatches via `WriteDictEntry`. Both halves of the chain are independently smoke-verified (Tier C tests 4–5 hit the bracket path; FieldAliases lookup is the same mechanism used for NPC_/ARMO/WEAP/ALCH today). Plain-float RACE fields (`UnarmedDamage`, `UnarmedReach`, `BaseMass`, etc.) need no aliases — they already work via canonical name through reflection.

- **`RecordTypeCode` 3-case expansion** at the switch arm. Three new cases inserted before the default fallback:
  ```csharp
  IFurnitureGetter => "FURN", IActivatorGetter => "ACTI", ILocationGetter => "LCTN",
  ```
  RACE stays in the default-fallback path because `Race.ClassType.Name.ToUpperInvariant() == "RACE"` happens to be the canonical Skyrim record code. SPEL/MGEF/LVLN/LVSP were already explicit in the switch from earlier work.

  This was a Phase 3 bonus catch — Phase 3 confirmed that Furniture/Activator/Location records returned `"FURNITURE"` / `"ACTIVATOR"` / `"LOCATION"` instead of the canonical 4-character Skyrim record codes. Cosmetic only at the response-shape level; doesn't affect operator dispatch (the 16 wire-ups already pass via the switch's interface-pattern matching, not the response field). Adding the 3 explicit cases costs 3 lines and keeps `record_type` consistent with xEdit/Mutagen convention. Per the prompt's bonus-catch precedent, folded into Phase 4 rather than punted.

- **No other PatchEngine.cs changes.** `OperatorModsKeys`, `ApplyModifications`, the keyword switch, the spell/items wire-ups — all preserved verbatim from Phase 3. Tier D contract intact.

### Python schema reconciliation (`mo2_mcp/tools_patching.py`)

Source of truth: `dev/plans/v2.7.1_race_patching/AUDIT.md`. Each operator description rewritten to match AUDIT's post-P3 supported-types reality.

- **Top-level `mo2_create_patch` description.** Added the v2.7.1 error-shape note: *"Per-record errors include an 'unmatched_operators' field listing any operators not supported on the target record type — silent drops were eliminated in v2.7.1."*
- **`set_fields`.** Aliases line expanded with the new RACE aliases (`BaseHealth/BaseMagicka/BaseStamina/HealthRegen/MagickaRegen/StaminaRegen (RACE)`). Tier C bracket-indexer + JSON-object syntax docs appended verbatim from PLAN.md Phase 4 step 3: *"Dict-typed fields support bracket syntax: 'Starting[Health]: 100' on RACE; works for any Mutagen IDictionary<,> property. Whole-dict assignment via JSON object: 'Starting: {Health: 100, Magicka: 200}' (merge semantics — only specified keys touched)."*
- **`add_keywords` / `remove_keywords`.** Supported-records list expanded from generic to the explicit 16-type list per AUDIT: *Armor, Weapon, NPC, Ingestible (ALCH), Ammunition, Book, Flora, Ingredient, MiscItem, Scroll, Race, Furniture, Activator, Location, Spell, MagicEffect.* `remove_keywords` references `add_keywords` rather than re-listing.
- **`add_spells` / `remove_spells`.** `(NPC)` → `(NPC, RACE)`.
- **`add_items`.** `"Entries to add to leveled lists"` → `"Entries to add to leveled lists (LVLI, LVLN, LVSP)."`
- **`add_conditions` / `remove_conditions`.** **Phase 4 latent-issue catch:** the existing description text claimed `"perks, spells, packages, magic effects, etc."`, but per AUDIT (and the existing KNOWN_ISSUES "Spell conditions apply at effect level" entry) Spells are NOT supported — they take per-effect conditions on their MGEFs. Description rewritten to the AUDIT-confirmed three: *MagicEffect, Perk, Package*, with explicit mentions of why Spell and Quest are out of scope (per-effect / DialogConditions+EventConditions disambiguation). Per the prompt's bonus-catch precedent, surfaced and fixed in Phase 4 rather than punted.
- **`attach_scripts`.** Description expanded with the AUDIT-listed broad set (NPC, Quest, Armor, Weapon, Outfit, Container, Door, Activator, Furniture, Light, MagicEffect, Spell, etc.) plus a one-line PERK/QUST adapter-subclass caveat pointing at KNOWN_ISSUES.
- **NPC-only operators (`add_perks`/`remove_perks`, `add_packages`/`remove_packages`, `add_factions`/`remove_factions`)** — descriptions left unchanged; the existing `(NPC)` annotation is correct.
- **Outfit/FormList/inventory operators** — descriptions unchanged; existing annotations match AUDIT.
- **`set_enchantment` / `clear_enchantment`** — unchanged. Existing `"on ARMO/WEAP"` matches AUDIT (AMMO confirmed out of scope).

No code-path changes in `tools_patching.py` — descriptions only. The Python module is the user-facing schema documentation for `mo2_create_patch`; the bridge does the dispatch.

### KNOWN_ISSUES.md finalization

- **Banner bumped:** `Current as of v2.7.0` → `v2.7.1`.
- **v2.7.1 placeholder section replaced** with the finalized content. Three subsections:
  1. **What's new** — RACE patching fully wired; keyword writes expanded to FURN/ACTI/LCTN/SPEL/MGEF + RACE; `add_items` expanded to LVLN/LVSP; bracket-indexer + JSON-object dict syntax; silent drops eliminated.
  2. **Carried-over limitations (v2.8 candidates)** — replace-semantics whole-dict (cribbed from Phase 2 single-path-merge framing); chained dict access; Quest condition disambiguation; per-effect spell conditions; PERK/QUST adapter-subclass; AMMO enchantment.
  3. **v2.8 = verification release** framing — no new capabilities, real-world exercise of v2.7.1's wire-ups, fix what surfaces.
- **Existing "Spell conditions apply at effect level"** subsection (under Design-trade-off limitations) left unchanged. The user-facing wording is still accurate post-v2.7.1; Tier D just makes the error response more structured. The new v2.7.1 section cross-references it for the per-effect carry-over.
- **No other KNOWN_ISSUES content modified** — User-provided prerequisites, Design-trade-off limitations (other than the cross-reference above), Environmental quirks, Upstream issues, Resolved-bugs history all unchanged.

### CHANGELOG.md finalization

`## v2.7.1 — TBD` placeholder rewritten per PLAN.md Phase 4 step 5 template. Date stays TBD — Phase 5 inserts the actual ship date.

- **Headline paragraph** — RACE-report-driven; Tier D/C/A/B as the four work tiers; v2.8 = verification release framing.
- **Fixed — bridge** (8 entries):
  - RACE keywords (silent-drop elimination)
  - RACE actor effects (silent-drop elimination)
  - RACE per-stat starting + regen (`set_fields` via Tier C)
  - Silent-failure bug class (Tier D, generic across all record types)
  - Keyword writes on FURN/ACTI/LCTN/SPEL/MGEF
  - `add_items` on LVLN/LVSP
  - `set_enchantment` / `clear_enchantment` honesty (Phase 1 enchantment-inverse fix)
  - `remove_conditions` silent-no-op alignment (Phase 1 throw-alignment fix)
- **Added — bridge** (4 entries):
  - Tier C bracket-indexer path syntax
  - Tier C JSON-object form (uniform merge semantics)
  - Tier B RACE field aliases
  - Tier D `unmatched_operators` response field
- **Out of scope (v2.8 candidates)** — replace semantics, chained dict access, Quest condition disambiguation, per-effect spell conditions, PERK/QUST adapter-subclass, AMMO enchantment. Closes with the v2.8 = verification release line.

Per Phase 3 architect's call: SPEL keyword-remove asymmetry **not** mentioned in CHANGELOG (it's a non-issue; calling it out invites questions about a vanilla-Skyrim data gap, not a bridge bug). RecordTypeCode FURN/ACTI/LCTN cosmetic fix also not in CHANGELOG (response-field consistency only; doesn't affect dispatch correctness or any user-visible behavior).

### Production bridge rebuild

```
$ cd tools/mutagen-bridge
$ dotnet publish -c Release -r win-x64 --self-contained false -o ../../build-output/mutagen-bridge/
mutagen-bridge -> .../build-output/mutagen-bridge/
```

- **Path:** `build-output/mutagen-bridge/mutagen-bridge.exe`
- **Size:** 151,552 bytes
- **SHA256:** `a0f1d983be7dc50e8efb12a5965b6716e8fd0f27553a7e5858a0ecccd1253e68`
- **Publish output:** 40 files in `build-output/mutagen-bridge/` — `.exe`, `.dll`, `.deps.json`, `.runtimeconfig.json`, plus Mutagen + Noggog + Loqui + System.* dependencies. Runtime requires the full set.

**Build artifacts are NOT included in the work commit.** The repo's `.gitignore` excludes `build-output/` ("Build artifacts (rebuilt by build/build-release.ps1)"); `git log` confirms `build-output/mutagen-bridge/` has never been tracked. v2.7.0 PHASE_6_HANDOFF.md captures only the installer SHA256 (the shipped artifact in `build-output/installer/`), with the bridge tree regenerated by `build-release.ps1` and synced via `-SyncLive`. v2.7.1 follows the same precedent — Phase 5 rebuilds + ships. The SHA256 above is the audit anchor for what Phase 5 should be reproducing on its rebuild; Phase 5 should verify the rebuilt SHA matches before shipping (or document a rebuild-source-of-difference if it doesn't).

This was confirmed mid-phase with the architect — Option 3 ("flip gitignore for build-output/") is a workflow-policy change that warrants its own scoped discussion (with the v2.7.0 backfill question + the "commit on every C# change" question) rather than landing implicitly through v2.7.1. Carried as a possible v2.8 / parallel-workstream candidate.

## Verification performed

### 1. Bridge builds clean

```
$ cd tools/mutagen-bridge && dotnet build -c Release
mutagen-bridge -> .../bin/Release/net8.0/mutagen-bridge.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.90
```

Zero warnings, zero errors. The new `IFurnitureGetter` / `IActivatorGetter` / `ILocationGetter` cases need no `using` alias (the `SkActivator` alias is for the concrete `Activator` class, not the interface — interfaces are unambiguous because there's no `System.IActivatorGetter`).

### 2. coverage-smoke: ALL 22 PASS

```
$ dotnet run -c Release --project tools/coverage-smoke
... 22 PASS lines, no FAIL ...
=== smoke complete: ALL PASS ===
```

Counted 22 `^  PASS$` lines in the output, zero FAIL. Every Tier D + Tier C + Tier A test from Phases 1–3 still passes after the FieldAliases + RecordTypeCode changes — neither change alters dispatch correctness for any of the 22 tested cases. The new RACE alias resolution chain (`BaseHealth` → `Starting[Health]` → Tier C bracket dispatch) isn't directly smoked — see "Known issues / open questions" below.

### 3. race-probe regression check still passes

```
$ dotnet run -c Release --project tools/race-probe
... (all P0 audit blocks pass, round-trip ESP at %TEMP%\AuditProbe.esp = 906 bytes) ...
=== probe complete ===
```

Zero `FAIL` or `***` markers in the output. Phase 0's audit-verification probe remains green through Phase 4's changes.

### 4. Production bridge runs

`build-output/mutagen-bridge/mutagen-bridge.exe` produced cleanly by `dotnet publish` (above). No runtime smoke against the published exe in this phase — Phase 5's pre-ship comprehensive smoke matrix exercises the published binary end-to-end.

## Deviations from plan

### 1. RACE alias chain not directly smoke-tested

PHASE_3_HANDOFF.md "Files of interest for next phase" suggested: *"Phase 4 should add a Test 23 covering the Tier B aliases (e.g., `set_fields(BaseHealth=300)` → resolves to `Starting[Health]=300` via the alias path)."*

The Phase 4 prompt explicitly excluded `coverage-smoke` source modifications: *"Don't touch: coverage-smoke / race-probe source — regression checks; running them is part of acceptance, modifying them isn't in scope."*

Honored the prompt's scope-lock. The alias chain is mechanical: `ApplySetFields`'s alias resolution (already smoke-tested via NPC_/ARMO/WEAP/ALCH paths in pre-v2.7.1 production use) + Tier C bracket-indexer dispatch (smoke-tested in Tests 4–6 directly) compose without any new dispatch path. The risk of the chain failing while both halves work is near-zero.

PLAN.md Phase 5 step 1 already includes the alias-end-to-end check in the comprehensive pre-ship smoke matrix: *"For Tier B: an alias write (`BaseHealth: 250`) — confirm it resolves to `Starting[Health]`."* Phase 5 will exercise the chain end-to-end against live modlist data.

### 2. RecordTypeCode 3-case fix folded into Phase 4

PLAN.md Phase 4 step 2 only anticipated verifying RACE returns the right code (a no-op since `Race.ClassType.Name == "RACE"` coincidentally works). PHASE_3_HANDOFF.md surfaced 3 additional types falling to default and producing wrong codes — Furniture/Activator/Location. Per the prompt's bonus-catch precedent, folded into Phase 4 alongside the RACE verification rather than punted to v2.8.

Cosmetic only — `record_type` in the response string. No effect on operator dispatch (the switch arms in `ApplyModifications` use interface-pattern matching, not the response code), no effect on Tier D's unmatched-check semantics (which compares mods-key presence per operator, not record_type). The 22 coverage-smoke tests don't assert on `record_type` precisely to avoid coupling to this — readback via Mutagen verifies actual record mutation, which is what matters for correctness.

### 3. add_conditions / remove_conditions schema description corrected (Phase 4 latent-issue catch)

The pre-Phase-4 description text for `add_conditions` claimed `"perks, spells, packages, magic effects, etc."`. Per AUDIT.md and the existing KNOWN_ISSUES "Spell conditions apply at effect level" entry, **Spells are not supported** — conditions live nested inside each Effect, not on the SPEL record itself. Quest also not supported (DialogConditions/EventConditions disambiguation pending v2.8). The description claimed a record type the bridge doesn't actually support — exactly the latent-issue case the prompt's bonus-catch precedent authorizes Phase 4 to fix.

Description rewritten to AUDIT's three: *MagicEffect, Perk, Package*, with explicit mentions of the Spell + Quest carve-outs and a pointer to KNOWN_ISSUES.

### 4. build-output/ not committed

Architect's mid-phase decision (Option 1 of three options surfaced when the gotcha note's mental model conflicted with the gitignore reality). Honored existing repo conventions; SHA256 in this handoff is the audit anchor.

### 5. No other deviations

Tier B alias block is verbatim PLAN.md Phase 4 step 1. CHANGELOG entry follows PLAN.md step 5 template. KNOWN_ISSUES carry-overs match AUDIT.md § "Carry-overs explicitly noted." Production rebuild command is verbatim PLAN.md step 6.

## Known issues / open questions

### 1. RACE alias chain has no direct smoke

See Deviation 1 above. Phase 5's pre-ship comprehensive smoke matrix is the safety net.

### 2. Comprehensive write surface is now exposed by user-facing description but not yet verified in real workflows

v2.7.1 substantially expands the operator × record-type matrix. The smoke harness exercises one representative record per pair (16 pairs across 9 record types); v2.8 = verification release is planned to surface bugs that the synthetic smoke misses on real modlist records. KNOWN_ISSUES + CHANGELOG both frame v2.8 this way.

### 3. AUDIT.md unchanged

No row reclassified in Phase 4. The audit's pre-Phase-3 reality + the 16 wire-ups Phase 3 landed all carry forward unchanged.

### 4. Bridge artifact not in the work commit

Per Option 1 (architect-confirmed). Phase 5's `build-release.ps1 -SyncLive` rebuilds + ships. SHA256 in this handoff anchors the v2.7.1 build for reproducibility checks.

### 5. v2.8 carry-overs unchanged

All carry-overs documented in AUDIT.md § "Carry-overs explicitly noted" remain v2.8 candidates — none surfaced or shifted in Phase 4:
1. Quest condition lists (DialogConditions / EventConditions disambiguation)
2. Per-effect spell conditions
3. Adapter-subclass attach_scripts (PerkAdapter / QuestAdapter)
4. AMMO enchantment
5. Replace-semantics whole-dict assignment
6. Chained dict access (`Foo[Key].Sub`)

Plus carried forward from Phase 3:
7. SPEL keyword-remove smoke gap (vanilla Skyrim.esm has no SPEL with populated Keywords; v2.8 verification could pre-prep an ESP via Mutagen).

Surfaced in Phase 4 (not v2.8 — already-fixed in this phase, listed for completeness):
- `add_conditions`/`remove_conditions` schema description claimed Spell support — corrected to AUDIT's three (MagicEffect, Perk, Package). Was a Phase 4 latent-issue catch under the bonus-catch precedent.

## Preconditions for Phase 5

| Precondition (per PLAN.md) | Status |
|---|---|
| Phase 4 complete (Tier B + Python schema + docs roll-up + bridge rebuild) | ✓ Met |
| Bridge builds clean from cumulative P0+P1+P2+P3+P4 changes | ✓ Met (zero warnings, zero errors) |
| coverage-smoke green (22/22 PASS regression) | ✓ Met |
| race-probe still green (P0 audit-verification probe) | ✓ Met |
| AUDIT.md still authoritative (no rows reclassified by P4) | ✓ Met |
| `build-output/mutagen-bridge/mutagen-bridge.exe` rebuilt (Phase 5 picks up from this output OR rebuilds from source) | ✓ Met (SHA256 captured for reproducibility verification) |
| KNOWN_ISSUES + CHANGELOG finalized for v2.7.1 (banner + carry-overs + write-surface table) | ✓ Met |
| Python schema descriptions match AUDIT.md's post-P3 reality | ✓ Met |
| RACE field aliases in place for Phase 5's smoke matrix to exercise | ✓ Met (6 aliases, all routing to Tier C bracket paths) |

## Files of interest for next phase

Phase 5 is the only sync of v2.7.1.

- **`Claude_MO2/build-output/mutagen-bridge/mutagen-bridge.exe`** (current SHA `a0f1d983be7dc50e8efb12a5965b6716e8fd0f27553a7e5858a0ecccd1253e68`) — rebuild from source if desired and verify the SHA matches; ship via installer.
- **`Claude_MO2/build/build-release.ps1`** — orchestrates installer rebuild + live sync. v2.7.0's PHASE_6_HANDOFF.md documents the `-SyncLive -MO2PluginDir <path>` invocation.
- **`Claude_MO2/installer/claude-mo2-installer.iss`** — Inno Setup config (already version-bumped to v2.7.1 in Phase 0). Run ISCC.exe against this to produce `build-output/installer/claude-mo2-setup-v2.7.1.exe`.
- **`Claude_MO2/dev/plans/v2.7.1_race_patching/PLAN.md` § Phase 5 (lines 664+)** — exact step-by-step recipe:
  1. Pre-ship comprehensive smoke matrix (every wire-up landed in Phases 1–4 against live modlist data; Tier B alias chain end-to-end; throwaway output ESP at `<workspace>/scratch/v2.7.1-final-smoke.esp`).
  2. Build installer; capture installer SHA256.
  3. Live sync via `build-release.ps1 -SyncLive`; verify `mo2_ping` returns v2.7.1.
  4. Tag `v2.7.1`; `gh release create v2.7.1` with installer asset.
  5. Insert ship date in CHANGELOG (replaces `## v2.7.1 — TBD`); commit.
  6. Write PHASE_5_HANDOFF.md.
- **`Claude_MO2/mo2_mcp/CHANGELOG.md`** — Phase 5 inserts the actual ship date. Today is 2026-04-25; if Phase 5 runs same-day, the date stays `2026-04-25`.
- **`Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_4_HANDOFF.md`** (this file) — reference for what landed.

### Phase 5 should verify

- **RACE alias chain end-to-end** — `set_fields: {BaseHealth: 300}` against a real RACE record; confirm `record_type: "RACE"` in response, `Starting[Health] == 300` on readback. Catches any breakage in the FieldAliases-to-Tier-C composition.
- **One representative pair per AUDIT row** — already covered by coverage-smoke for synthetic input; Phase 5 uses live modlist records to surface real-world divergence.
- **Tier D unmatched-operator path** — one deliberately-unsupported request (e.g. `add_perks` on a CONT) — confirm `unmatched_operators` field in response.
- **Installer payload** — bundled README/KNOWN_ISSUES/CLAUDE.md/skills are the v2.7.1-finalized versions (per v2.7.0 P6 sequencing rule: docs first, build after).
- **`mo2_ping` post-sync** — `version: "2.7.0"` → `"2.7.1"`. Confirms the live install picked up the fresh Python + bridge.
