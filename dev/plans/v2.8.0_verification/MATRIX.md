# v2.8.0 Verification Matrix

**Authoritative test specification for v2.8.0's verification pass.** Mirrors AUDIT.md's role for v2.7.1.

**Methodology.** Every cell is one bridge invocation (or one Mutagen direct call for the PerkAdapter/QuestAdapter probe), with the listed operation against the listed source record, and a documented expected result. Layers 1, 2, 4 run via `tools/coverage-smoke/` against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_create_patch` against the live Authoria modlist, output to `<modlist>/mods/Claude Output/v2.8-scenario-N.esp`, deleted post-verification.

**Record selection.** Layer 1 / 2 / 4 use `coverage-smoke`'s existing `FirstOrDefault` predicate selection where possible. Where multiple records of a type are needed for depth, use the second record matching the predicate. Specific FormIDs are filled in by Phase 2 as the harness extends; this matrix locks the *what* and *how*, not the precise FormID.

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in Phase 2's handoff, including the actual response payload.

**Phase 1 deliverable feeds this matrix.** Layer 1.E (Effects-list cells) tests the new capability Phase 1 builds. Phase 1 lays down the regression tests in `coverage-smoke`; Phase 2 runs them as part of the broader matrix.

---

## 📝 Phase 3 corrections (2026-04-26 plan-amend)

Per Aaron 2026-04-26, MATRIX is updated in place to reflect Phase 3 findings rather than preserved as a strict planning snapshot. Original-vs-corrected delta history lives in `PHASE_3_HANDOFF.md` § Findings. Four cells/sections corrected below:

| Cell | Original | Corrected | Source finding |
|---|---|---|---|
| **1.A.01 / 1.A.03** | `Skyrim.esm:08F95E` labelled VendorItemFood | Use a real KYWD via `coverage-smoke`'s `FreshKwFor` predicate. The original FormID resolves to `AMBWindloopMountainsHills01LP` (audio category record), not a KYWD. Bridge accepts opaque FormLinks for add_keywords without record-type validation, so the test passes mechanically — but the label was misleading. Phase 4 swaps in the harness. | Phase 3 finding #1 |
| **Scenario 3.3** | `mods.merged_count > 0` | Bridge response actually uses `details[].entries_merged` (top-level on per-record detail, not nested under `mods`). Use `entries_merged ≥ 0`. **LVSP topology constraint:** Authoria modlist's LVSP records carry uniform SPEL References across overrides (Requiem just rebalances levels), so merge correctly dedups to `entries_merged: 0` for any tested LVSP. Bridge mechanism is correct; the assertion shape needs to accept zero as valid for this modlist's data shape. | Phase 3 findings #2 + #4 |
| **Scenario 3.5** | PERK reflection target `Configuration.PerkType` | Mutagen's PERK schema has no `Configuration` sub-object. Writable PERK scalars are at the top level: `Level`, `NumRanks`, `Trait`, `Playable`, `Hidden`, plus `NextPerk` as a single-field FormLink (which exercises Phase 1's IFormLinkNullable bonus-catch on `IFormLinkNullable<IPerkGetter>`). Use these instead. | Phase 3 finding #3 |

Phase 3 finding #5 (`add_conditions GetActorValue parameterless default`) is being absorbed in Phase 4 as a bridge enhancement (`actor_value` parameter) rather than a matrix correction — handled in PLAN.md § Phase 4 step 5. After Phase 4, the matrix's add_conditions test cells (1.r.33 et al.) should be re-anchored to use the new `actor_value` parameter for GetActorValue cases.

---

## Layer 1 — Coverage matrix (~58 assertions)

### 1.A — Tier A wire-ups (positive cases — 16 pairs × ≥1 record each)

Re-runs Phase 5's smoke matrix for regression-anchor PLUS adds a depth row per pair where vanilla Skyrim.esm has multiple candidates.

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.A.01 | add_keywords | RACE | first RACE w/ Keywords populated | add a real KYWD via `FreshKwFor` predicate (corrected 2026-04-26 — see § Phase 3 corrections) | mods.keywords_added=1; readback Keywords contains target |
| 1.A.02 | remove_keywords | RACE | same | remove ActorTypeNPC | mods.keywords_removed=1; readback Keywords lacks target |
| 1.A.03 | add_keywords | RACE | second RACE w/ Keywords populated | add a real KYWD via `FreshKwFor` predicate (corrected 2026-04-26) | mods.keywords_added=1 |
| 1.A.04 | remove_keywords | RACE | same | remove a known-present keyword | mods.keywords_removed=1 |
| 1.A.05 | add_spells | RACE | first RACE w/ ActorEffect populated | add Flames (Skyrim.esm:0002B956) | mods.spells_added=1; readback ActorEffect contains target |
| 1.A.06 | remove_spells | RACE | same | remove first existing actor effect | mods.spells_removed=1 |
| 1.A.07 | add_keywords | FURN | first FURN | add a known FURN-applicable keyword | mods.keywords_added=1; record_type=FURN |
| 1.A.08 | remove_keywords | FURN | same | remove pre-added | mods.keywords_removed=1 |
| 1.A.09 | add_keywords | FURN | second FURN | add | mods.keywords_added=1 |
| 1.A.10 | add_keywords | ACTI | first ACTI | add | mods.keywords_added=1; record_type=ACTI |
| 1.A.11 | remove_keywords | ACTI | same | remove pre-added | mods.keywords_removed=1 |
| 1.A.12 | add_keywords | ACTI | second ACTI | add | mods.keywords_added=1 |
| 1.A.13 | add_keywords | LCTN | first LCTN | add | mods.keywords_added=1; record_type=LCTN |
| 1.A.14 | remove_keywords | LCTN | same | remove | mods.keywords_removed=1 |
| 1.A.15 | add_keywords | LCTN | second LCTN | add | mods.keywords_added=1 |
| 1.A.16 | add_keywords | SPEL | first SPEL | add 2 keywords | mods.keywords_added=2 |
| 1.A.17 | remove_keywords | SPEL | same | remove 1 of the added | mods.keywords_removed=1; readback retains other |
| 1.A.18 | add_keywords | SPEL | second SPEL | add | mods.keywords_added=1 |
| 1.A.19 | add_keywords | MGEF | first MGEF | add | mods.keywords_added=1 |
| 1.A.20 | remove_keywords | MGEF | same | remove | mods.keywords_removed=1 |
| 1.A.21 | add_keywords | MGEF | second MGEF | add | mods.keywords_added=1 |
| 1.A.22 | add_items | LVLN | first LVLN | add 1 entry (level=1, ref=NordRace) | mods.items_added=1; readback Entries+1 |
| 1.A.23 | add_items | LVLN | second LVLN | add | mods.items_added=1 |
| 1.A.24 | add_items | LVSP | first LVSP | add 1 entry | mods.items_added=1; readback Entries+1 |
| 1.A.25 | add_items | LVSP | second LVSP | add | mods.items_added=1 |

### 1.B — Tier B alias resolution (RACE)

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.B.01 | set_fields | RACE | first RACE w/ Starting populated | BaseHealth=250 | mods.fields_set=1; readback Starting[Health]=250; sibling stats preserved |
| 1.B.02 | set_fields | RACE | same | BaseMagicka=300 | Starting[Magicka]=300; siblings preserved |
| 1.B.03 | set_fields | RACE | same | BaseStamina=400 | Starting[Stamina]=400; siblings preserved |
| 1.B.04 | set_fields | RACE | same | HealthRegen=2.0 | Regen[Health]=2.0; siblings preserved |
| 1.B.05 | set_fields | RACE | same | MagickaRegen=4.0 | Regen[Magicka]=4.0; siblings preserved |
| 1.B.06 | set_fields | RACE | same | StaminaRegen=6.0 | Regen[Stamina]=6.0; siblings preserved |
| 1.B.07 | set_fields | RACE | same | BaseHealth=250 + HealthRegen=1.5 in same call | mods.fields_set=2; both write |

### 1.C — Tier C bracket-indexer + JSON-object dict syntax

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.C.01 | set_fields | RACE | first RACE | Starting[Health]=200 | bracket write; siblings preserved |
| 1.C.02 | set_fields | RACE | same | Starting[Magicka]=300 + Starting[Stamina]=400 | both bracket writes; mods.fields_set=2 |
| 1.C.03 | set_fields | RACE | same | Starting={Health:100, Magicka:200} | merge; readback Stamina preserved (not in JSON) |
| 1.C.04 | set_fields | RACE | same | Regen={Health:2, Magicka:4} | merge; Stamina preserved |
| 1.C.05 | set_fields | RACE | same | Regen[Health]=3.0 + Regen={Magicka:5} (alias-then-merge) | both apply; sibling preserved |
| 1.C.06 | set_fields | RACE | second RACE | BipedObjectNames[Body]="TestSlot" | indexer write to BipedObjectNames dict (Tier C freebie) |

### 1.E — Effects-list write capability (NEW; Phase 1 deliverable)

Tests the JSON Array → `ExtendedList<Effect>` mechanism Phase 1 implements. All five carrier records exercised. Replace-semantics confirmed (source Effects cleared, JSON-supplied entries land).

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.E.01 | set_fields | SPEL | first SPEL w/ ≥2 Effects | Effects=[{BaseEffect:KW1, Data:{Magnitude:50, Area:0, Duration:0}}] | mods.fields_set=1; readback Effects.Count=1; Effects[0].BaseEffect resolves to KW1; Magnitude=50; source effects gone (replace) |
| 1.E.02 | set_fields | SPEL | first SPEL | Effects=[{BaseEffect:KW1, Data:{Magnitude:50}, Conditions:[{function:GetActorValue,operator:>=,value:50}]}] | nested Conditions land; readback Effects[0].Conditions.Count=1 with right values |
| 1.E.03 | set_fields | ALCH | first ALCH w/ Effects | Effects=[{BaseEffect:MGEF1, Data:{Magnitude:10, Duration:30}}] | mods.fields_set=1; readback Effects.Count=1; replace confirmed |
| 1.E.04 | set_fields | ENCH | first ENCH | Effects=[{BaseEffect:MGEF1, Data:{Magnitude:5}}] | replace; readback confirmed (or skip-with-reason if Phase 1 EFFECTS_AUDIT.md excluded ENCH) |
| 1.E.05 | set_fields | SCRL | first SCRL | Effects=[{BaseEffect:MGEF1}] | replace; readback confirmed (or skip if excluded) |
| 1.E.06 | set_fields | INGR | first INGR | Effects=[{BaseEffect:MGEF1, Data:{Magnitude:1}}] | replace; readback confirmed (or skip if excluded) |
| 1.E.07 | set_fields | SPEL | first SPEL | Effects=[] (empty array) | mods.fields_set=1; readback Effects.Count=0 (whole-list clear) |
| 1.E.08 | set_fields | SPEL | first SPEL | Effects=[{BaseEffect:"Skyrim.esm:DOESNOTEXIST"}] (bad FormLink) | record-level error; rollback; clean message identifying the bad FormID |

### 1.regression — Pre-existing handler regression band (24 pairs sampled to ~32 cells)

The 24 (operator, record-type) pairs Phase 1 of v2.7.1 refactored into "write mods key unconditionally inside matched arm" pattern. Verify no regression introduced.

#### Keywords on the 10 pre-existing types

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.r.01 | add_keywords | ARMO | first ARMO | add a known-applicable keyword | mods.keywords_added=1 |
| 1.r.02 | remove_keywords | ARMO | first ARMO w/ Keywords populated | remove a known-present keyword | mods.keywords_removed=1 |
| 1.r.03 | add_keywords | WEAP | first WEAP | add | mods.keywords_added=1 |
| 1.r.04 | remove_keywords | WEAP | first WEAP w/ Keywords populated | remove | mods.keywords_removed=1 |
| 1.r.05 | add_keywords | NPC_ | first NPC_ | add | mods.keywords_added=1 |
| 1.r.06 | remove_keywords | NPC_ | first NPC_ w/ Keywords populated | remove | mods.keywords_removed=1 |
| 1.r.07 | add_keywords | ALCH | first ALCH | add | mods.keywords_added=1 |
| 1.r.08 | remove_keywords | ALCH | first ALCH w/ Keywords populated | remove | mods.keywords_removed=1 |
| 1.r.09 | add_keywords | AMMO | first AMMO | add | mods.keywords_added=1 |
| 1.r.10 | remove_keywords | AMMO | first AMMO w/ Keywords populated | remove | mods.keywords_removed=1 |
| 1.r.11 | add_keywords | BOOK | first BOOK | add | mods.keywords_added=1 |
| 1.r.12 | add_keywords | FLOR | first FLOR | add | mods.keywords_added=1 |
| 1.r.13 | add_keywords | INGR | first INGR | add | mods.keywords_added=1 |
| 1.r.14 | add_keywords | MISC | first MISC | add | mods.keywords_added=1 |
| 1.r.15 | add_keywords | SCRL | first SCRL | add | mods.keywords_added=1 |

#### Other operators — at least one positive case per pair

| # | Op | Type | Source | Operation | Expected |
|---|----|------|--------|-----------|----------|
| 1.r.16 | add_spells | NPC_ | first NPC_ | add Flames | mods.spells_added=1 |
| 1.r.17 | remove_spells | NPC_ | first NPC_ w/ ActorEffect populated | remove first | mods.spells_removed=1 |
| 1.r.18 | add_perks | NPC_ | first NPC_ | add a vanilla perk | mods.perks_added=1 |
| 1.r.19 | remove_perks | NPC_ | first NPC_ w/ Perks populated | remove first | mods.perks_removed=1 |
| 1.r.20 | add_packages | NPC_ | first NPC_ | add a package | mods.packages_added=1 |
| 1.r.21 | remove_packages | NPC_ | first NPC_ w/ Packages populated | remove first | mods.packages_removed=1 |
| 1.r.22 | add_factions | NPC_ | first NPC_ | add {faction, rank:0} | mods.factions_added=1 |
| 1.r.23 | remove_factions | NPC_ | first NPC_ w/ Factions populated | remove first | mods.factions_removed=1 |
| 1.r.24 | add_inventory | NPC_ | first NPC_ | add {item, count:1} | mods.inventory_added=1 |
| 1.r.25 | remove_inventory | NPC_ | first NPC_ w/ Items populated | remove first | mods.inventory_removed=1 |
| 1.r.26 | add_inventory | CONT | first CONT | add | mods.inventory_added=1 |
| 1.r.27 | remove_inventory | CONT | first CONT w/ Items populated | remove first | mods.inventory_removed=1 |
| 1.r.28 | add_outfit_items | OTFT | first OTFT | add | mods.outfit_items_added=1 |
| 1.r.29 | remove_outfit_items | OTFT | first OTFT w/ Items populated | remove first | mods.outfit_items_removed=1 |
| 1.r.30 | add_form_list_entries | FLST | first FLST | add | mods.form_list_added=1 |
| 1.r.31 | remove_form_list_entries | FLST | first FLST w/ Items populated | remove first | mods.form_list_removed=1 |
| 1.r.32 | add_items | LVLI | first LVLI | add {ref, level:1, count:1} | mods.items_added=1 |
| 1.r.33 | add_conditions | MGEF | first MGEF | add ConditionFloat (function:GetActorValue) | mods.conditions_added=1 |
| 1.r.34 | remove_conditions | MGEF | first MGEF w/ Conditions populated | remove by index 0 | mods.conditions_removed=1 |
| 1.r.35 | add_conditions | PERK | first PERK | add ConditionFloat | mods.conditions_added=1 |
| 1.r.36 | add_conditions | PACK | first PACK | add ConditionFloat | mods.conditions_added=1 |
| 1.r.37 | attach_scripts | NPC_ | first NPC_ w/o VMAD | attach test script | mods.scripts_attached=1 |
| 1.r.38 | attach_scripts | ARMO | first ARMO w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.39 | attach_scripts | WEAP | first WEAP w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.40 | attach_scripts | OTFT | first OTFT w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.41 | attach_scripts | CONT | first CONT w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.42 | attach_scripts | DOOR | first DOOR w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.43 | attach_scripts | ACTI | first ACTI w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.44 | attach_scripts | FURN | first FURN w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.45 | attach_scripts | LIGH | first LIGH w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.46 | attach_scripts | MGEF | first MGEF w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.47 | attach_scripts | SPEL | first SPEL w/o VMAD | attach | mods.scripts_attached=1 |
| 1.r.48 | set_enchantment | ARMO | first ARMO | set to a vanilla ench | mods.enchantment_set=1 |
| 1.r.49 | clear_enchantment | ARMO | first ARMO w/ enchantment | clear | mods.enchantment_cleared=1 |
| 1.r.50 | set_enchantment | WEAP | first WEAP | set | mods.enchantment_set=1 |
| 1.r.51 | clear_enchantment | WEAP | first WEAP w/ enchantment | clear | mods.enchantment_cleared=1 |
| 1.r.52 | set_fields | NPC_ | first NPC_ | Health=200 (alias) | fields_set=1; readback NPC.Configuration.Health=200 |
| 1.r.53 | set_fields | ARMO | first ARMO | Value=1000 | fields_set=1 |
| 1.r.54 | set_fields | WEAP | first WEAP | Damage=20 | fields_set=1 |
| 1.r.55 | set_fields | RACE | first RACE | UnarmedDamage=5.0 (plain-float canonical) | fields_set=1 |
| 1.r.56 | set_flags | NPC_ | first NPC_ | set Essential | flags_changed=1 |
| 1.r.57 | clear_flags | NPC_ | first NPC_ | clear Essential | flags_changed=1 |

### 1.D — Tier D negatives (~12 cells)

Confirms Tier D's coverage check fires correctly across diverse unsupported (operator, record-type) combos.

| # | Op | Type | Source | Expected |
|---|----|------|--------|----------|
| 1.D.01 | add_perks | CONT | first CONT | unmatched_operators=["add_perks"]; success=false; no ESP write of this record |
| 1.D.02 | add_keywords | DOOR | first DOOR | unmatched_operators=["add_keywords"] (DOOR has no Keywords per AUDIT) |
| 1.D.03 | add_keywords | LIGH | first LIGH | unmatched_operators=["add_keywords"] |
| 1.D.04 | add_keywords | CELL | first CELL | unmatched_operators=["add_keywords"] |
| 1.D.05 | add_keywords | QUST | first QUST | unmatched_operators=["add_keywords"] |
| 1.D.06 | add_keywords | PERK | first PERK | unmatched_operators=["add_keywords"] |
| 1.D.07 | add_spells | ARMO | first ARMO | unmatched_operators=["add_spells"] |
| 1.D.08 | add_inventory | ARMO | first ARMO | unmatched_operators=["add_inventory"] |
| 1.D.09 | add_factions | RACE | first RACE | unmatched_operators=["add_factions"] |
| 1.D.10 | add_outfit_items | NPC_ | first NPC_ | unmatched_operators=["add_outfit_items"] |
| 1.D.11 | add_items | CONT | first CONT | unmatched_operators=["add_items"] |
| 1.D.12 | set_enchantment | AMMO | first AMMO | unmatched_operators=["set_enchantment"] (carry-over confirmation) |

---

## Layer 2 — Combinatorial probes (~13 assertions)

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| 2.01 | Multi-op-per-record on RACE | add_keywords + add_spells + set_fields(BaseHealth=250) + set_fields(Starting[Magicka]=200) + set_flags in one record | mods has 5 keys, all values correct; readback shows all 5 mutations |
| 2.02 | Tier A + Tier B + Tier C in one record | RACE: add_keywords + BaseHealth (alias) + Starting[Magicka] (bracket) + Regen={H,M} (whole-dict merge) | all four resolve in single mods dict; readback shows all four |
| 2.03 | Multi-record patch with 4 success / 1 fail | 5 records: 4 valid RACE patches + 1 RACE w/ add_perks (Tier D fail) | response.successful_count=4, failed_count=1; output ESP contains the 4 successful records, NOT the failed one |
| 2.04 | Multi-record patch with 1 success / 4 fail | 1 valid RACE + 4 records each w/ different unmatched op | successful_count=1, failed_count=4; output ESP contains only the success |
| 2.05 | Multi-record same-type same-op | 3 ARMO records all with add_keywords (different keywords each) | all three succeed; per-record mods.keywords_added=1 each |
| 2.06 | Multiple unmatched operators on one record | RACE with add_perks + add_packages + add_inventory | unmatched_operators=["add_perks","add_packages","add_inventory"] (all three reported, not just first) |
| 2.07 | Mixed valid + invalid ops on same record | RACE with add_keywords (valid) + add_perks (invalid Tier D) | record fails; rolled back; mods.keywords_added NOT present in response (because record failed entirely); unmatched=["add_perks"] |
| 2.08 | Cross-tier on ARMO | set_fields(Value=1000) + set_enchantment + attach_scripts + add_keywords | all four mods keys present; all four mutations land |
| 2.09 | Tier C on multiple dicts in one record | RACE: Starting={Health:100,Magicka:200} + Regen={Health:1,Magicka:2} | both dicts merge correctly; Stamina preserved on both |
| 2.10 | Tier D rollback isolation | Patch where record A succeeds, record B fails Tier D, record C succeeds | A and C land in output ESP unchanged; B is fully rolled back; readback verifies A+C present, B absent |
| 2.11 | All-keys-overlap whole-dict | RACE: Starting={Health:100, Magicka:200, Stamina:300} (every key) | merge effectively replaces all 3; mods.fields_set=1 (one whole-dict op) |
| 2.12 | Empty op set | RACE record with op:"override" but no modification fields at all | success=true with no mods key (override-only is valid; Tier D doesn't fire because no operators were requested) |
| 2.13 | **Effects + keywords combo on SPEL** (NEW) | SPEL with set_fields(Effects=[...]) + add_keywords in one call | mods has both fields_set + keywords_added; readback shows new Effects AND added keyword |

---

## Layer 3 — Workflow scenarios (~5 scenarios, ~30 assertions)

Run via `mo2_create_patch` against the live Authoria modlist. Output filenames `v2.8-scenario-N.esp`. Test patches deleted post-verification.

### Scenario 3.1 — Reqtify a custom race + ability spells (~10 assertions, **the consumer-driven scenario**)

This is the workflow that surfaced Phase 1's Effects-list gap. Verifies the v2.7.1 → v2.8 transition end-to-end on real modlist data.

**Target:**
- 1 RACE override (non-vanilla, e.g. an Authoria custom race) — pick at Phase 3
- 2–3 SPEL ability records (the racial passives the RACE.ActorEffect points at)

**Operations:**
- **Step 1 (RACE patch):** add_keywords (creature type + immunities), add_spells (ActorEffect additions), set_fields (BaseHealth + BaseMagicka + BaseStamina + HealthRegen)
- **Step 2 (SPEL patches, one per ability spell):** set_fields(Effects=[{BaseEffect, Data, Conditions}, ...]) — **the new Phase 1 capability**

**Assertions:**
- RACE patch: mods.keywords_added matches; mods.spells_added matches; mods.fields_set ≥4; readback Keywords + ActorEffect + Starting + Regen all reflect changes
- SPEL patches: mods.fields_set=1 per record; readback Effects.Count matches request; per-effect BaseEffect resolves; Magnitude/Area/Duration match; per-effect Conditions land (Phase 1's nested Conditions path)
- Cross-record: all changes ship in a single output ESP

### Scenario 3.2 — Creature mod keyword + script attachment (~5 assertions)

**Target:** 2–3 NPC_ records from a creature/AI mod. Source = current winner.

**Operations:** add_keywords (1–2 keywords) + attach_scripts (one script with typed properties: Object FormLink, Int, Float).

**Assertions:** per-record mods.keywords_added=N + mods.scripts_attached=1; bridge accepts patch; readback Keywords contains added; VMAD contains script with property values matching.

### Scenario 3.3 — Leveled spell list merge (~4 assertions)

**Target:** A LVSP record overridden by both an Authoria patch and a content mod. Use `op: merge_leveled_list`.

**Operations:** merge_leveled_list with base_plugin = overhaul winner, override_plugins = [content mod].

**Assertions** (corrected 2026-04-26 — see § Phase 3 corrections): bridge accepts (`success=true`); per-record `details[].entries_merged ≥ 0` (NOT `mods.merged_count` — bridge response uses top-level `entries_merged` on per-record detail). For Authoria modlist's LVSP records specifically, expect `entries_merged: 0` (LVSP topology constraint — vanilla/USSEP/Requiem carry uniform SPEL References across overrides; merge correctly dedups). Output ESP contains the LVSP override; readback Entries matches base plugin (Requiem); ChanceNone preserved.

### Scenario 3.4 — NPC bundle (~8 assertions)

**Target:** A single key NPC from the modlist. Source = current winner.

**Operations:** add_keywords (2) + add_spells (1) + add_perks (1) + add_factions (1 with rank) + add_inventory (1) + attach_scripts (1) + set_fields (Health=300 alias).

**Assertions:** 7 mods keys present, all positive; readback confirms each mutation: Keywords, ActorEffect, Perks, Factions, Items, VirtualMachineAdapter, Configuration.Health.

### Scenario 3.5 — MGEF condition + PERK reflection set_fields (~5 assertions)

**Target:** An MGEF + a PERK both winnable in the modlist (pick at Phase 3).

**Operations** (corrected 2026-04-26 — see § Phase 3 corrections): MGEF: `add_conditions` (one ConditionFloat targeting GetActorValue; **post-Phase-4 should also pass `actor_value: "<AV name>"`** to exercise the new Phase 4 parameter — see PLAN.md § Phase 4 step 5). PERK: `set_fields` against actually-writable PERK scalars — `Level`, `NumRanks`, `Trait`, `Playable`, `Hidden`, plus `NextPerk` (single-field FormLink — exercises Phase 1's IFormLinkNullable bonus-catch on `IFormLinkNullable<IPerkGetter>`). The original `Configuration.PerkType` path doesn't exist on Mutagen's PERK schema; Phase 3 substituted `{Level, NumRanks, NextPerk}` and confirmed all three writable.

**Assertions:** MGEF readback Conditions contains new entry with correct function/operator/value (and ActorValue resolved correctly post-Phase-4); PERK readback target fields have new values + source fields preserved (Effects, Name, Description, Trait, Playable, Hidden, Conditions); both records ship in output ESP.

---

## Layer 4 — Edges + carry-over probes (~26 assertions)

### 4.malformed — Bracket syntax error handling

| # | Path | Expected |
|---|------|----------|
| 4.m.01 | `Starting[` (no close bracket) | record error: "malformed bracket"; clean response |
| 4.m.02 | `Starting[]` (empty brackets) | record error: "empty bracket key" |
| 4.m.03 | `Starting]` (close without open) | record error: "malformed bracket" |
| 4.m.04 | `[Health]` (no property name) | record error: "missing property name" |
| 4.m.05 | `Starting[Health` (no close, has key) | record error: "unterminated bracket" |
| 4.m.06 | `Starting[Bogus]` (unparseable enum) | record error: "Enum.Parse failure on BasicStat" |

### 4.idempotency — Double-add and remove-not-present

| # | Op | Setup | Expected |
|---|----|-------|----------|
| 4.i.01 | add_keywords (same kw twice in one call) | RACE record | document actual behavior: dedup'd to 1, double-added to 2, OR error (regression baseline) |
| 4.i.02 | add_keywords (kw already on source) | RACE w/ ActorTypeNPC, add ActorTypeNPC | document: skip-add or double-add |
| 4.i.03 | remove_keywords (kw not present) | RACE, remove a kw not in Keywords | document: silent no-op or error |
| 4.i.04 | remove_spells (spell not present) | NPC, remove a spell not in ActorEffect | document |

### 4.chained — Chained dict access rejection

| # | Path | Expected |
|---|------|----------|
| 4.c.01 | `Starting[Health].Foo` (terminal-bracket then property) | record error: "chained access not supported" or similar |
| 4.c.02 | `Foo[K1].Bar[K2]` (multi-level chained brackets) | record error |
| 4.c.03 | `Foo.Bar[Key]` (nested property then terminal bracket — SHOULD be supported per AUDIT) | works correctly OR document if it fails |

### 4.replace — Replace-vs-merge confirmation (Tier C dicts)

| # | Setup | Expected |
|---|-------|----------|
| 4.r.01 | RACE with Regen at source = {H:1, M:2, S:3}; set_fields Regen={H:5} | Regen[Health]=5, Regen[Magicka]=2 (preserved), Regen[Stamina]=3 (preserved) |
| 4.r.02 | RACE: set_fields Starting={H:100}; followed in SAME call by set_fields Regen={H:1} | both dicts: Starting[H]=100/M=preserved/S=preserved; Regen[H]=1/M=preserved/S=preserved (cross-dict isolation) |
| 4.r.03 | RACE: set_fields Starting={H:100, M:200, S:300} (full coverage of every key) | all 3 keys land; effective replace via merge; conceptually still merge |

### 4.array — Array replace-semantics (NEW; distinct from Tier C dict-merge)

| # | Setup | Expected |
|---|-------|----------|
| 4.arr.01 | SPEL with 3 source Effects; set_fields Effects=[{single new entry}] | output Effects.Count=1; source effects gone (replace, not merge) |
| 4.arr.02 | SPEL with 0 source Effects; set_fields Effects=[{entry1}, {entry2}] | output Effects.Count=2 |
| 4.arr.03 | SPEL with 3 source Effects; set_fields Effects=[] | output Effects.Count=0 (whole-list clear) |

### 4.biped — BipedObjectNames probe (Tier C freebie)

| # | Op | Source | Operation | Expected |
|---|----|--------|-----------|----------|
| 4.b.01 | set_fields | RACE | BipedObjectNames[Body]="TestSlotBody" | indexer write succeeds; readback BipedObjectNames[Body]="TestSlotBody" |
| 4.b.02 | set_fields | RACE | BipedObjectNames={Body:"X", Hands:"Y"} | merge; both keys land |

### 4.esl — ESL master interaction

| # | Setup | Expected |
|---|-------|----------|
| 4.esl.01 | add_keywords on a record from an ESPFE plugin in the live modlist | output FormLinks compact correctly per Mutagen's WithLoadOrder writer; xEdit reads ESL FormID without unresolved warning. (Run via Phase 3 live workflow.) |

### 4.carry — Carry-over candidate probes

Five remaining (one absorbed into Phase 1 + dropped from this layer).

| # | Carry-over | Setup | Expected |
|---|------------|-------|----------|
| 4.c.01 | Quest condition disambiguation | QUST: add_conditions | Tier D error: unmatched_operators=["add_conditions"] |
| 4.c.02 | ~~Per-effect spell conditions~~ | **ABSORBED into Phase 1; tested by 1.E.02 (positive case).** | n/a |
| 4.c.03 | AMMO enchantment | AMMO: set_enchantment | Tier D error |
| 4.c.04 | Replace-semantics (Tier C dict) | covered in 4.r above | merge-only confirmed for dicts; array-replace handled separately in 4.array |
| 4.c.05 | Chained dict access | covered in 4.chained above | rejection confirmed |
| 4.c.06 | **PerkAdapter functional probe** | PERK record w/o existing VMAD: attach_scripts via bridge; THEN read output ESP back into a fresh `SkyrimMod` via Mutagen direct (NOT via bridge); inspect `output.Perks[0].VirtualMachineAdapter.GetType()` | If type is `PerkAdapter`: bug doesn't reproduce. If type is base `VirtualMachineAdapter`: BUG CONFIRMED — Phase 4 fix required. Run in `tools/race-probe/`, not in `coverage-smoke` |
| 4.c.07 | **QuestAdapter functional probe** | QUST record w/o existing VMAD: attach_scripts via bridge; readback via Mutagen direct; inspect `output.Quests[0].VirtualMachineAdapter.GetType()` | Same as 4.c.06 but for Quest → expect `QuestAdapter` |

---

## Total assertion count

| Layer | Count |
|---|---:|
| 1.A — Tier A wire-ups | 25 |
| 1.B — Tier B aliases | 7 |
| 1.C — Tier C bracket/merge | 6 |
| **1.E — Effects-list write (NEW)** | **8** |
| 1.regression — Pre-existing handlers | 57 |
| 1.D — Tier D negatives | 12 |
| 2 — Combinatorial probes | 13 |
| 3 — Workflow scenarios | ~30 (split across 5 scenarios) |
| 4.malformed | 6 |
| 4.idempotency | 4 |
| 4.chained | 3 |
| 4.replace (dict) | 3 |
| **4.array (NEW)** | **3** |
| 4.biped | 2 |
| 4.esl | 1 |
| 4.carry (was 7, now 6 with one absorbed) | 6 |
| **Total** | **~186** |

Phase 2 may dedupe / merge rows where the same code path is exercised twice.

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion:

```
[1.A.01] add_keywords RACE NordRace                         PASS
[1.E.01] set_fields(Effects) SPEL FirstSpel                 PASS (3 source effects → 1 new effect, replace confirmed)
[1.E.02] set_fields(Effects+Conditions) SPEL FirstSpel      PASS
[1.D.01] add_perks CONT REQ_VendorChest_Blacksmith_Skyforge PASS (unmatched=add_perks rolled back)
[2.06] multi_unmatched_operators RACE NordRace              FAIL: expected unmatched=["add_perks","add_packages","add_inventory"], got ["add_perks"]
```

Failures embed enough context for handoff to lift into the bug list directly.

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a record with the right shape (e.g. an OTFT with empty Items, or every PERK already has a VMAD), the harness prints:

```
[1.r.29] remove_outfit_items OTFT <none-with-populated-items> SKIP: no vanilla OTFT has populated Items
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture or accept the gap.

Phase 1 may also skip Layer 1.E rows for ENCH/SCRL/INGR if the EFFECTS_AUDIT.md probe found their Effect type incompatible with the generic mechanism. Skips per record type are documented in `EFFECTS_AUDIT.md` and reflected here.
