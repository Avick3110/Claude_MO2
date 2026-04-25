# Phase 1 Handoff — Tier D silent-failure detection

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1.5h
**Commits made:** TBD (single commit at the end of this phase, hash will be added when pushed)
**Live install synced:** No (Phase 1 doesn't sync — Phase 5 does the only live sync of v2.7.1).

## What was done

### Bridge changes (`tools/mutagen-bridge/PatchEngine.cs`)

- **Added `OperatorModsKeys` static dict** — canonical operator → mods-key mapping (25 entries; `set_flags` and `clear_flags` share `flags_changed`). Source-of-truth mirror of the AUDIT.md table at the top of `dev/plans/v2.7.1_race_patching/AUDIT.md`. Placed in a new "Tier D — Silent-Failure Detection" section just before `ApplyModifications`.
- **Added `RequestedOperatorsOf(RecordOperation op)` helper** — returns `Dictionary<operatorName, modsKey>` for every populated operator field on the request. Operators with shared mods-keys (set_flags / clear_flags → `flags_changed`) appear as separate entries.
- **Added `UnsupportedOperatorException` (private nested type)** — carries `RecordType` and `UnmatchedOperators`, message format: `"Record type {RecordType} does not support: {opNames}"`.
- **Wrapped `ApplyModifications`:**
  - Top: `var requested = RequestedOperatorsOf(op);` captures the requested operators before any handler runs.
  - Bottom (before the existing `mods.Count == 0` cleanup): compute `unmatched = requested.Where(kv => !mods.ContainsKey(kv.Value)).Select(kv => kv.Key).ToList();`. If non-empty, throw `UnsupportedOperatorException`.
- **Caught the exception in `ProcessOverride`** (line ~191): on `UnsupportedOperatorException`, call `TryRemoveOverride(...)`, null out `detail.Modifications`, populate `detail.UnmatchedOperators` and `detail.Error`, then return the `detail` without re-throwing. This lets the outer `Process` loop count it as a failed record but continue with remaining records in the patch.
- **Scrubbed conditional `if (added > 0) mods["X"] = added;` writes to unconditional** at all 15 sites inside the per-record-type handler arms (NPC perks/packages/factions/inventory; container inventory; LVLI items; outfit items; FormList entries — both add and remove). The Tier D contract requires that the mods-key be present iff the handler ran for this record type — "ran with 0 changes" is success, not failure.
- **Refactored `&& xxx != null` short-circuits** in 7 remove-paths (spells, perks, factions, inventory NPC, inventory Container, outfit items, form list entries) so the mods-key write happens unconditionally inside the right record-type arm; the null-check moved INSIDE the `if (op.RemoveX?.Count > 0)` block where it controls only the iteration, not the mods-key write.
- **Fixed the enchantment inverse pattern** at the `set_enchantment` / `clear_enchantment` blocks: pre-v2.7.1, the mods-key was written unconditionally inside `if (op.SetEnchantment != null)`, but the actual mutation only fires for Armor/Weapon — so on a Container, the bridge falsely reported `enchantment_set` as success without any record change. Now uses a local `applied` flag inside each block; the mods-key is only written when one of the Armor/Weapon arms actually fired. Tier D's check then catches the unsupported case via the absent mods-key.
- **Aligned `ApplyRemoveConditions`** with `ApplyAddConditions`: pre-v2.7.1, line 1040 silently `return 0` on missing `Conditions` property — Tier D would have classified that as "handler matched, removed 0 = success." Now throws `InvalidOperationException("Record type {Name} does not support conditions")`. The `condList == null` case (property exists but list is null/empty) still returns 0 — that's the legitimate "supported but empty" state.

### Bridge changes (`tools/mutagen-bridge/Models.cs`)

- **Added `UnmatchedOperators` field to `RecordDetail`** (`[JsonPropertyName("unmatched_operators")] List<string>?`). Additive; serializer's `WhenWritingNull` setting ensures the field is omitted on the success path. No changes to request shape; no client-breaking changes.

### New regression-check project (`tools/coverage-smoke/`)

Sibling of `tools/race-probe/`. Builds against Mutagen 0.53.1, opens Skyrim.esm, picks the first Container and first Armor record, and exercises the bridge over stdin/stdout via `Process.Start`. Three test cases:

1. **Tier D failing** — `add_perks` on Container. Asserts: `success=false`, `unmatched_operators: ["add_perks"]`, `record_type: "CONT"`, no output ESP written.
2. **Tier D passing** — `set_fields(Weight=99.5)` on Container. Asserts: `success=true`, `modifications.fields_set: 1`, output ESP written, no `unmatched_operators` field.
3. **Aligned-throw** — `remove_conditions` on Armor (Armor has no `Conditions` property). Asserts: error message contains `"does not support conditions"` (the v2.7.1 alignment), no output ESP written.

Stays in the repo as a regression check for Tier D semantics (mods-key present iff handler-matched-and-ran). Same intent as race-probe — diagnostic, non-shipped, runs via `dotnet run -c Release --project tools/coverage-smoke`.

## Verification performed

### 1. Bridge builds clean

```
$ dotnet build -c Release
mutagen-bridge -> .../bin/Release/net8.0/mutagen-bridge.dll
Build succeeded. 0 Warning(s) 0 Error(s)
```

### 2. Coverage-smoke: ALL PASS

Ran `dotnet run -c Release --project tools/coverage-smoke`:

```
CONT: 10FDE6:Skyrim.esm (MerchantWhiterunEorlundChest)
ARMO: 016FFF:Skyrim.esm (DremoraBoots)

── Test 1: add_perks on CONT (expected: Tier D unmatched-operator error) ──
  exit code: 1
  response:
    {
      "success": false,
      "records_written": 0,
      "successful_count": 0,
      "failed_count": 1,
      "esl_flagged": false,
      "masters": [],
      "details": [
        {
          "formid": "Skyrim.esm:10FDE6",
          "record_type": "CONT",
          "op": "override",
          "source": "Skyrim.esm",
          "error": "Record type CONT does not support: add_perks",
          "unmatched_operators": ["add_perks"]
        }
      ],
      "error": "No records were successfully added to the patch."
    }
  PASS

── Test 2: set_fields(Weight) on CONT (expected: success) ──
  exit code: 0
  response:
    {
      "success": true,
      "output_path": "...\\test2-tier-d-passing.esp",
      "records_written": 1,
      "successful_count": 1,
      "failed_count": 0,
      "esl_flagged": false,
      "masters": ["Skyrim.esm"],
      "details": [
        {
          "formid": "Skyrim.esm:10FDE6",
          "record_type": "CONT",
          "op": "override",
          "source": "Skyrim.esm",
          "modifications": { "fields_set": 1 }
        }
      ]
    }
  PASS

── Test 3: remove_conditions on ARMO (expected: aligned-throw error) ──
  exit code: 1
  response:
    {
      "success": false,
      ...
      "details": [
        {
          "formid": "Skyrim.esm:016FFF",
          "op": "override",
          "error": "Record type Armor does not support conditions"
        }
      ],
      "error": "No records were successfully added to the patch."
    }
  PASS

=== smoke complete: ALL PASS ===
```

### 3. race-probe regression check still passes

```
$ dotnet run -c Release --project tools/race-probe
... (all P0 audit blocks pass, round-trip ESP at %TEMP%\AuditProbe.esp = 906 bytes) ...
=== probe complete ===
```

No regression in the audit-verification probe. Phase 0's contract still holds.

### 4. The `OperatorModsKeys` dict matches existing handler outputs

Cross-checked every value in `OperatorModsKeys` against what each handler in `ApplyModifications` writes to `mods`. All 25 entries align:

| Operator | mods key (in dict) | Handler output (in code) |
|---|---|---|
| add_keywords | keywords_added | mods["keywords_added"] = ... |
| remove_keywords | keywords_removed | mods["keywords_removed"] = ... |
| add_spells | spells_added | mods["spells_added"] = ... |
| remove_spells | spells_removed | mods["spells_removed"] = ... |
| add_perks | perks_added | mods["perks_added"] = ... |
| remove_perks | perks_removed | mods["perks_removed"] = ... |
| add_packages | packages_added | mods["packages_added"] = ... |
| remove_packages | packages_removed | mods["packages_removed"] = ... |
| add_factions | factions_added | mods["factions_added"] = ... |
| remove_factions | factions_removed | mods["factions_removed"] = ... |
| add_inventory | inventory_added | mods["inventory_added"] = ... |
| remove_inventory | inventory_removed | mods["inventory_removed"] = ... |
| add_outfit_items | outfit_items_added | mods["outfit_items_added"] = ... |
| remove_outfit_items | outfit_items_removed | mods["outfit_items_removed"] = ... |
| add_form_list_entries | form_list_added | mods["form_list_added"] = ... |
| remove_form_list_entries | form_list_removed | mods["form_list_removed"] = ... |
| add_items | items_added | mods["items_added"] = ... |
| add_conditions | conditions_added | mods["conditions_added"] = ... |
| remove_conditions | conditions_removed | mods["conditions_removed"] = ... |
| attach_scripts | scripts_attached | mods["scripts_attached"] = ... |
| set_enchantment | enchantment_set | mods["enchantment_set"] = ... (now applied-gated) |
| clear_enchantment | enchantment_cleared | mods["enchantment_cleared"] = ... (now applied-gated) |
| set_fields | fields_set | mods["fields_set"] = ... |
| set_flags | flags_changed | mods["flags_changed"] = ... |
| clear_flags | flags_changed | (shared with set_flags) |

## Deviations from plan

- **PLAN.md placed `OperatorModsKeys` "near the top of the class, alongside `FieldAliases`."** I placed it just above `ApplyModifications` (the only consumer) inside a new "Tier D — Silent-Failure Detection" section. This is "alongside" in the file-section sense rather than "adjacent to FieldAliases at line 692." Rationale: the dict is the input to `RequestedOperatorsOf`, which is the input to the unmatched-check inside `ApplyModifications` — co-locating them keeps the Tier D code in one logical section. `FieldAliases` is the input to `ApplySetFields` and lives near `set_fields` for the same reason.
- **PLAN.md's exception name was tentative (`UnsupportedOperatorException` "or equivalent").** I used that exact name. No deviation, just calling out the exact name landed.
- **Caught the exception in `ProcessOverride` rather than the outer `Process` loop.** PLAN.md step 4 said "Find where `ApplyModifications(...)` is called ... Wrap in try/catch." I did so at the existing `ProcessOverride` try/catch (which already does the rollback). The catch returns `detail` instead of re-throwing — this lets the outer loop count the record as a failure (per `details.Count(d => d.Error == null)`) without losing the structured fields. PLAN.md's "propagate an error response" wording is satisfied because the structured fields land on the per-record `detail`, which surfaces in `PatchResponse.Details`.
- **Found and fixed an audit blind spot during implementation:** AUDIT.md (and PLAN.md by extension) stated that `ApplyAddConditions` AND `ApplyRemoveConditions` both throw on missing Conditions property. In fact only `ApplyAddConditions` did — `ApplyRemoveConditions` silently returned 0. Fixed in this phase to align both. Without this fix, Tier D would have classified `remove_conditions` on a record without `Conditions` as success (mods-key present, removed=0) — silently masking the unsupported operator. The fix path is consistent with the existing `ApplyAddConditions` throw, surfaces through `ProcessOverride`'s outer catch (not Tier D's `UnsupportedOperatorException` path), and Test 3 in coverage-smoke verifies the new behavior end-to-end.
- **Scope expansion within Phase 1:** PLAN.md anticipated the unconditional-mods-write scrub via the Phase 0 handoff's flag (15 conditional-write sites). I additionally found and fixed:
  - 7 `&& xxx != null` short-circuits on remove-paths (would have falsely classified "supported but empty list" as unmatched).
  - The enchantment inverse pattern (would have falsely classified `set_enchantment` on a Container as success).
  - The `ApplyRemoveConditions` silent-no-op (above).
  Each of these is a Tier D correctness requirement; without them, the v2.7.0-baseline behavior would have leaked through to v2.7.1 as silent successes the audit-driven Phase 3 wire-ups would not catch. The user (architect) flagged the enchantment one explicitly during plan review; the others surfaced from a careful re-read of every handler arm. All fixes are minimal — they preserve existing behavior on the success path and only change the silent-no-op cases.

## Known issues / open questions

- **`attach_scripts` adapter-subclass issue stays as v2.8.** AUDIT.md flagged this: `PerkAdapter` / `QuestAdapter` are subclasses of `VirtualMachineAdapter`. The bridge's `vmadProp.GetValue(record) as VirtualMachineAdapter` cast succeeds for the base, so Tier D will NOT catch this — it'll report `scripts_attached: N` but the adapter shape is wrong. Not addressable in v2.7.1 — needs a per-type adapter factory. v2.8 candidate.
- **Quest `DialogConditions` / `EventConditions` stays as v2.8.** Quest doesn't have a property literally named `Conditions`, so `ApplyAddConditions` / `ApplyRemoveConditions` throw "does not support conditions" on Quest. With Tier D in place, this surfaces cleanly as an error rather than silently — but the right v2.8 fix is an operator parameter (`condition_target: "dialog" | "event"`) to disambiguate which list is targeted. v2.8 candidate (no new operators allowed in v2.7.1 per plan scope-lock).
- **Spell per-effect conditions stays as v2.8.** `Spell.Effects[i].Conditions` requires nested-LoquiObject mutation deeper than Tier C's terminal-bracket scope. AUDIT.md already flags this; Tier D will surface it cleanly as `Record type Spell does not support conditions` since SPEL has no top-level `Conditions` property.
- **Phase 2 (Tier C) will run on top of Tier D.** Tier C extends `ConvertJsonValue` and `SetPropertyByPath` to handle dict-typed properties via bracket-indexer syntax. The Tier D unmatched check fires AFTER the handlers run, so Tier C's `ApplySetFields` enhancements remain inside the existing `if (op.SetFields?.Count > 0)` arm and write `mods["fields_set"]` as today — no Tier D semantics change required for Phase 2.

## Preconditions for Phase 2

| Precondition (per PLAN.md) | Status |
|---|---|
| Phase 1 complete (Tier D in place) | ✓ Met |
| Bridge builds clean from `e77afcd` baseline + Phase 0 + Phase 1 changes | ✓ Met (zero warnings, zero errors) |
| `RecordDetail.UnmatchedOperators` field exists for Phase 2's potential per-record errors | ✓ Met (Phase 2 likely won't add new unmatched cases; Tier C is generic and works inside `set_fields`) |
| race-probe still green | ✓ Met |
| coverage-smoke green (regression check for Tier D semantics) | ✓ Met |
| AUDIT.md still authoritative for Phase 3 wire-up scope | ✓ Met (no rows reclassified by Phase 1) |

## Files of interest for next phase

- **`tools/mutagen-bridge/PatchEngine.cs`** — Phase 2 modifies:
  - `SetPropertyByPath` (~line 745 in v2.7.0 baseline; line numbers shifted by Phase 1's insertions): final-segment branch needs to recognize `PropertyName[Key]` syntax and dispatch to dict indexer when target is `IDictionary<,>`.
  - `ConvertJsonValue` (~line 777): add a branch for `JsonValueKind.Object` against dict-typed `targetType` (build a fresh `Dictionary<TKey, TValue>` and merge in entries).
  - The whole-dict path for setter-less dict properties (the RACE.Starting case): when final segment is no-bracket, value is JSON Object, target is a dict property without a setter — iterate JSON object members and call indexer per entry.
- **`tools/coverage-smoke/Program.cs`** — Phase 2 extends with Tier C verification: bracket-indexer write to RACE.Starting (`Starting[Health]: 250`), whole-dict form (`Starting: {Health: 100, Magicka: 200}`), and an unparseable-enum error case. The existing harness shape (build request → pipe to bridge → assert response) transfers verbatim.
- **`tools/race-probe/Program.cs`** — Phase 0 already verified RACE.Starting/Regen indexer-write API contracts. Phase 2 doesn't need to extend race-probe.
- **`dev/plans/v2.7.1_race_patching/AUDIT.md` § "Operator: set_fields / set_flags / clear_flags"** — documents what RACE properties Tier C unlocks (Starting, Regen, BipedObjectNames). Phase 2 honors that scope.
- **`dev/plans/v2.7.1_race_patching/PLAN.md` § Phase 2** (lines 423–486) — exact step-by-step recipe for Tier C, including the bracket-parser semantics and merge-vs-replace contract.
