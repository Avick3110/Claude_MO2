# Phase 3 Handoff — Tier A: comprehensive operator wire-ups

**Phase:** 3
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1.5h
**Commits made:** `cc3bd50` (`[v2.7.1 P3] Tier A — comprehensive operator wire-ups (16 pairs)`), pushed to `origin/main` alongside this handoff hash-record commit.
**Live install synced:** No (Phase 3 doesn't sync — Phase 5 does the only live sync of v2.7.1).

## What was done

### Bridge changes (`tools/mutagen-bridge/PatchEngine.cs`)

- **`using SkActivator = Mutagen.Bethesda.Skyrim.Activator;` alias** added near the top of the file. Mutagen's `Activator` clashes with `System.Activator` under `<ImplicitUsings>enable</ImplicitUsings>`; the alias keeps the new switch arm compiling without changing the project's implicit-usings setting. Same disambiguation Phase 0 made in race-probe.
- **`GetKeywordsList` switch — 6 new arms** appended after the existing 10 (Armor, Weapon, Npc, Ingestible, Ammunition, Book, Flora, Ingredient, MiscItem, Scroll) under a `// v2.7.1 Tier A wire-ups` comment:
  - `Race`, `Furniture`, `SkActivator`, `Location`, `Spell`, `MagicEffect`
  - Each line: `r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>()` — verbatim mirror of the Armor/Weapon idiom.
  - Tier D contract is inherited — keywords flow through the existing `if (keywords != null) { … mods["keywords_added"] = … ; mods["keywords_removed"] = … }` block at PatchEngine.cs:540 (post-Phase-1, unconditional writes inside the matched arm). No new Tier D plumbing required.
- **RACE ActorEffect block** inserted right after the existing NPC block closes (post-NPC, pre-Container at the new line ~688). Mirrors the **post-Phase-1 NPC pattern** — mods key written unconditionally inside the matched arm; the null-check on `race.ActorEffect` controls only the iteration, not the mods-key write:
  ```csharp
  if (record is Race race)
  {
      if (op.AddSpells?.Count > 0)
      {
          race.ActorEffect ??= new ExtendedList<IFormLinkGetter<ISpellRecordGetter>>();
          mods["spells_added"] = AddFormLinks(race.ActorEffect, op.AddSpells);
      }
      if (op.RemoveSpells?.Count > 0)
      {
          mods["spells_removed"] = race.ActorEffect != null
              ? RemoveFormLinks(race.ActorEffect, op.RemoveSpells)
              : 0;
      }
  }
  ```
  PLAN.md's example for this block used the v2.7.0 short-circuit pattern (`if (op.RemoveSpells?.Count > 0 && race.ActorEffect != null)`); the implementation uses the post-Phase-1 fixed pattern instead, preserving Tier D correctness for the "supported but ActorEffect is null" case.
- **LeveledNpc / LeveledSpell `add_items` blocks** inserted immediately after the existing LVLI block. Both mirror the LVLI shape verbatim, with the entry-construction shape lifted from `MergeLeveledNpcs` (line 303) and `MergeLeveledSpells` (line 353):
  - LVLN: `Reference = fk.ToLink<INpcSpawnGetter>()`
  - LVSP: `Reference = fk.ToLink<ISpellRecordGetter>()`
  - Both use the LVLI pattern of `int added = 0; foreach { added++; } mods["items_added"] = added;` — unconditional write inside the matched arm.

All three new sections are documented inline with `// v2.7.1 Tier A wire-up` comments.

### No other PatchEngine.cs changes

- **`OperatorModsKeys` dict unchanged.** Phase 1 mapped every operator including the ones not yet wired. The new Phase 3 arms populate existing keys (`keywords_added`, `spells_added`, `items_added`, etc.); no new keys required.
- **`RecordTypeCode` switch unchanged.** Latent issue surfaced (see Known issues below) but out of Phase 3 scope.
- **No new `ExceptionType` / request shape / response shape.** Pure dispatch additions.

### Smoke harness extension (`tools/coverage-smoke/Program.cs`)

**Decision: monolithic Program.cs.** Kept all 22 tests in one file rather than splitting by tier. Rationale: the existing test pattern (build req → pipe to bridge → assert response → readback via Mutagen) is well-established and shared (`RunBridge`, `FormatFormKey`, source loading). Splitting into multiple projects or partial classes would add ceremony without clarity gain at 22 tests / ~1100 lines total. Tests are grouped under labeled `── Test N: ──` banners and read top-to-bottom.

**Two new helper functions** (non-static local functions that capture outer scope) factor the 12 keyword tests:
- `KwAddTest(testNum, recordTypeLabel, targetFk, freshKwFk, readKwFromOutput)` — one-shot test for a single keyword add. Verifies `keywords_added=1` in the response and that the fresh kw is present in the output ESP via Mutagen readback. Returns 0 on PASS / 1 on FAIL.
- `KwRemoveTest(testNum, recordTypeLabel, targetFk, existingKwFk, readKwFromOutput, expectedRemoved=1)` — one-shot test for a single keyword removal. Default `expectedRemoved=1` is the normal case. `expectedRemoved=0` covers the SPEL fallback (see deviation note below) — verifies the wire-up dispatched (Tier D contract: mods key written) without requiring an actual mutation.

**Tests 19-22 (spells + leveled items) are inlined** — they're unique enough per-record-type that the helper abstraction would add more friction than it removes.

### Per-pair smoke result table (16 new tests)

| # | Operator | Type | Source picked (Skyrim.esm:formid) | mods key | Readback | Result |
|---|---|---|---|---|---|---|
| 7 | add_keywords | RACE | `109C7C` FoxRace (Keywords.Count=3) | `keywords_added=1` | fresh kw present, 4 total | PASS |
| 8 | add_keywords | FURN | `10F636` WindhelmThrone (Keywords.Count=3) | `keywords_added=1` | fresh kw present, 4 total | PASS |
| 9 | add_keywords | ACTI | `10C1C0` DoorDeadBoltDbl01 (Keywords.Count=1) | `keywords_added=1` | fresh kw present, 2 total | PASS |
| 10 | add_keywords | LCTN | `01706E` RiftenMercerHouseInteriorLocation (Keywords.Count=2) | `keywords_added=1` | fresh kw present, 3 total | PASS |
| 11 | add_keywords | SPEL | `000E52` (Keywords.Count=0, fallback) | `keywords_added=1` | fresh kw present, 1 total | PASS |
| 12 | add_keywords | MGEF | `017331` (with kw) | `keywords_added=1` | fresh kw present, N total | PASS |
| 13 | remove_keywords | RACE | `109C7C` FoxRace | `keywords_removed=1` | existing kw gone, 2 remaining | PASS |
| 14 | remove_keywords | FURN | `10F636` WindhelmThrone | `keywords_removed=1` | existing kw gone, 2 remaining | PASS |
| 15 | remove_keywords | ACTI | `10C1C0` DoorDeadBoltDbl01 | `keywords_removed=1` | existing kw gone, 0 remaining | PASS |
| 16 | remove_keywords | LCTN | `01706E` RiftenMercerHouseInteriorLocation | `keywords_removed=1` | existing kw gone, 1 remaining | PASS |
| 17 | remove_keywords | SPEL | `000E52` (no existing kw) | `keywords_removed=0` | trivially absent, 0 remaining | PASS (wire-up only) |
| 18 | remove_keywords | MGEF | `017331` | `keywords_removed=1` | existing kw gone, 0 remaining | PASS |
| 19 | add_spells | RACE | `10760A` (ActorEffect populated) | `spells_added=1` | fresh spell present, 4 total | PASS |
| 20 | remove_spells | RACE | `10760A` | `spells_removed=1` | existing spell gone, 2 remaining | PASS |
| 21 | add_items | LVLN | `10FCE5` | `items_added=1` | fresh NPC ref present, 4 total | PASS |
| 22 | add_items | LVSP | `10FE1C` | `items_added=1` | fresh spell ref present, 6 total | PASS |

Plus the 6 existing tests (Tests 1-6, regression for Tier D and Tier C): all PASS.

**Total: 22/22 PASS.**

## Verification performed

### 1. Bridge builds clean

```
$ dotnet build -c Release tools/mutagen-bridge
Build succeeded. 0 Warning(s) 0 Error(s)
```

### 2. coverage-smoke: ALL 22 PASS

```
$ dotnet run -c Release --project tools/coverage-smoke
... (full per-test output, 22 PASS lines, no FAIL) ...
=== smoke complete: ALL PASS ===
```

Tests 1-6 (Tier D + Tier C regression) all still pass — no regression from Phase 3 wire-ups.

### 3. race-probe regression check still passes

```
$ dotnet run -c Release --project tools/race-probe
... (all P0 audit blocks pass, round-trip ESP at %TEMP%\AuditProbe.esp = 906 bytes) ...
=== probe complete ===
```

Phase 0's audit-verification probe still green.

## Deviations from plan

### 1. RACE ActorEffect block uses post-Phase-1 NPC pattern, not PLAN.md's verbatim example

PLAN.md Phase 3 step 3 example showed:
```csharp
if (op.RemoveSpells?.Count > 0 && race.ActorEffect != null)
    mods["spells_removed"] = RemoveFormLinks(race.ActorEffect, op.RemoveSpells);
```

This is the **v2.7.0 short-circuit pattern that Phase 1 explicitly refactored** (see Phase 1 handoff "Refactored `&& xxx != null` short-circuits"). Implementation uses the post-Phase-1 NPC fix instead — mods key written unconditionally inside the matched arm, with `race.ActorEffect != null` controlling only the iteration:

```csharp
if (op.RemoveSpells?.Count > 0)
{
    mods["spells_removed"] = race.ActorEffect != null
        ? RemoveFormLinks(race.ActorEffect, op.RemoveSpells)
        : 0;
}
```

Carry-forward note in the prompt called this out explicitly: "Mirror Phase 1's pattern, not the v2.7.0 baseline's `if (added > 0)` pattern."

### 2. SPEL keyword test (Test 17) downgraded to wire-up-only check

Vanilla Skyrim.esm has **zero SPEL records with populated `Keywords`** (verified during smoke run via `source.Spells.FirstOrDefault(s => s.Keywords?.Count >= 1)` returning null). Mod overhauls like Requiem add keywords to spells, but the base game leaves the slot empty. Other vanilla ESMs (Update.esm / Dawnguard.esm / Dragonborn.esm) also lack populated SPEL keywords as far as the modlist's record index shows — they override existing Skyrim.esm spells without adding keywords.

Two options were weighed:
- **Option A (chosen):** Fall back to the first SPEL regardless of Keywords. Test 11 (ADD on SPEL) still proves wire-up correctness end-to-end (adds a fresh kw, verifies it landed in the output ESP). Test 17 (REMOVE on SPEL) becomes a Tier D wire-up check — `keywords_removed=0` is a valid Tier D pass per the contract ("handler ran with 0 changes is success"). Documented in the helper signature with `expectedRemoved` parameter.
- **Option B (rejected):** Pre-prep an ESP with a SPEL+kw via Mutagen, then use it as Test 17's source. More rigorous but introduces test-ordering coupling and ~30 lines of prep ceremony for a wire-up that is structurally identical to the other 5 keyword types (it dispatches through the same generic `if (keywords != null)` block). Rejected as over-engineering.

Test 11's ADD on SPEL proves Spell is in `GetKeywordsList`; the `if (keywords != null)` block at PatchEngine.cs:540 handles add and remove identically once the switch arm fires. Test 17 verifies the remove-side dispatch reaches the same block. The combination gives full wire-up coverage even without an end-to-end "remove an existing kw on SPEL" verification.

### 3. Helper parameter type is `ISkyrimModGetter`, not `SkyrimMod`

`SkyrimMod.CreateFromBinaryOverlay` returns `ISkyrimModDisposableGetter`, not the writeable `SkyrimMod`. Initial helper signature used `Func<SkyrimMod, …>` and failed compilation. Changed to `Func<ISkyrimModGetter, …>` (the broader read-only interface that `ISkyrimModDisposableGetter` extends). Functional equivalent — Mutagen's typed groups work the same on the getter side, and the `?.Keywords` access returns `IReadOnlyList<IFormLinkGetter<IKeywordGetter>>?` which is assignment-compatible with the helper's return type.

## Known issues / open questions

### 1. `RecordTypeCode` returns inconsistent codes for some new types — Phase 4 carry-over

Confirmed during the smoke run by inspecting bridge response `record_type` values:
- **Falls to default (`record.Registration.ClassType.Name.ToUpperInvariant()`):**
  - Race → `"RACE"` ✓ (coincidence — class name happens to match)
  - Furniture → `"FURNITURE"` ✗ (canonical Skyrim record code: `"FURN"`)
  - Activator → `"ACTIVATOR"` ✗ (canonical: `"ACTI"`)
  - Location → `"LOCATION"` ✗ (canonical: `"LCTN"`)
- **Already explicit in the switch:**
  - `ISpellGetter => "SPEL"` ✓
  - `IMagicEffectGetter => "MGEF"` ✓
  - `ILeveledNpcGetter => "LVLN"` ✓
  - `ILeveledSpellGetter => "LVSP"` ✓

PLAN.md Phase 4 step 2 anticipates the RACE check ("Confirm `RecordTypeCode` returns `"RACE"` for `IRaceGetter`. … If the default returns `"RACE"` already (which it should), no change needed"). It does. **Phase 4 should add explicit cases for FURN/ACTI/LCTN** (3 new lines) alongside the RACE verification. Phase 3's smoke tests deliberately do not assert `record_type` to avoid coupling to this — readback via Mutagen verifies actual record mutation, which is what matters for wire-up correctness.

This issue is purely cosmetic at the response-shape level (the field returns a longer name); it does not affect operator dispatch or mutation correctness. No record was reclassified in AUDIT.md as a result — the wire-ups themselves all pass.

### 2. SPEL keyword test gap (see Deviation 2)

Test 17 confirms Spell-side wire-up dispatch but cannot verify "remove an existing kw" end-to-end because vanilla Skyrim.esm has no qualifying source. Future v2.8 verification could pre-prep an ESP via Mutagen for a more aggressive smoke. Not a Phase 3 blocker.

### 3. v2.8 carry-overs unchanged

All carry-overs documented in AUDIT.md § "Carry-overs explicitly noted" remain v2.8 candidates — none surfaced in Phase 3:
1. Quest condition lists (DialogConditions / EventConditions disambiguation)
2. Per-effect spell conditions
3. Adapter-subclass attach_scripts (PerkAdapter / QuestAdapter)
4. AMMO enchantment
5. Replace-semantics whole-dict assignment
6. Chained dict access (`Foo[Key].Sub`)

Plus newly added:
7. RecordTypeCode FURN/ACTI/LCTN explicit cases — Phase 4 candidate (cosmetic only).

### 4. AUDIT.md unchanged

No row reclassified by Phase 3. Every "wire up in P3" row produced a smoke `pass` per the table above. The 16 (operator, record-type) pairs all wire correctly.

## Preconditions for Phase 4

| Precondition (per PLAN.md) | Status |
|---|---|
| Phase 3 complete (Tier A wire-ups in place) | ✓ Met |
| Bridge builds clean from cumulative P0+P1+P2+P3 changes | ✓ Met (zero warnings, zero errors) |
| coverage-smoke green (all 22 tests, includes Tier D + Tier C regression + new Tier A coverage) | ✓ Met (22/22 PASS) |
| race-probe still green (P0 audit-verification probe) | ✓ Met |
| AUDIT.md still authoritative for Phase 4 docs scope | ✓ Met (no reclassifications) |
| `set_fields` + ActorEffect + add_items wire-ups verified end-to-end | ✓ Met (Tests 7-22 prove all 16 pairs) |

## Files of interest for next phase

Phase 4 lands the cosmetic + docs + alias polish per PLAN.md Phase 4 (lines 552+).

- **`tools/mutagen-bridge/PatchEngine.cs`** — Phase 4 modifies:
  - `FieldAliases["RACE"]` block at ~line 692 (Tier B aliases — explicit per PLAN.md step 1).
  - `RecordTypeCode` at the renumbered location (was line 1617 in v2.7.0 baseline; Phase 1 + Phase 2 + Phase 3 insertions shifted it down). **Add explicit cases for FURN/ACTI/LCTN** alongside the RACE verification (see Known issue #1 above). Three new lines:
    ```csharp
    IFurnitureGetter => "FURN", IActivatorGetter => "ACTI", ILocationGetter => "LCTN",
    ```
- **`mo2_mcp/tools_patching.py`** — Phase 4 reconciles operator descriptions per PLAN.md step 3:
  - `add_keywords` / `remove_keywords` — list expanded to include Race, Furniture, Activator, Location, Spell, MagicEffect (in addition to the existing 10 item types).
  - `add_spells` / `remove_spells` — list expanded to include Race (in addition to NPC).
  - `add_items` — list expanded to include LVLN and LVSP (in addition to LVLI).
  - `set_fields` — bracket-indexer + JSON-object syntax docs (Tier C from Phase 2; placeholder only after Phase 2).
- **`Claude_MO2/KNOWN_ISSUES.md`** — finalize the v2.7.1 in-flight section (currently a placeholder pointing at PLAN.md). Phase 2 Tier C deviation (single-path merge) and Phase 3 carry-overs (RecordTypeCode FURN/ACTI/LCTN, SPEL keyword test gap) both feed into the wording.
- **`Claude_MO2/mo2_mcp/CHANGELOG.md`** — finalize the `## v2.7.1 — TBD` placeholder. Promote to date `2026-04-25` (or the actual ship date), populate the Fixed/Added/Note sections per Phase 4 plan.
- **`tools/coverage-smoke/Program.cs`** — Phase 4 should add a Test 23 covering the Tier B aliases (e.g., `set_fields(BaseHealth=300)` → resolves to `Starting[Health]=300` via the alias path). Also covers the alias resolution for the new RACE field aliases.
- **`PLAN.md` § Phase 4** (lines 552+) — exact step-by-step recipe.

### Where Phase 5 picks up

- **Production bridge artifact rebuild + live install sync.** Phase 5 (final) copies the cumulative P0+P1+P2+P3+P4 PatchEngine.cs build output to `Claude_MO2/build-output/mutagen-bridge/mutagen-bridge.exe` and to the user's live MO2 install. Phase 5 is the only sync of v2.7.1.
- **Installer rebuild.** v2.7.1 installer regenerated from the populated `build-output/`.
