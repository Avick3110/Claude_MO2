# Phase 4 Handoff — Bridge fixes + matrix corrections + docs hygiene

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-26
**Session length:** ~6h
**Commits made:** TBD — pending Aaron's sign-off (Phase 4 double-commit cadence: work + hash-record).
**Live install synced:** No (Phase 4 changes the bridge; Phase 5 syncs to live as part of the ship sequence).

## What was done

All 11 deliverable items per `PLAN.md` § Phase 4, in plan order. Single session, single handoff, single commit pair per the conductor's amended scope (no v2.9 punts for v2.8.0-uncovered findings).

### Item 1 — OTFT/SPEL VMAD disambiguation probe ⇒ Case (A) confirmed

`tools/race-probe/Program.cs` extended with two new sections (~150 lines):
1. `v2.8 P4 — VMAD deeper disambiguation: full property dump (Outfit / Spell)` — case-sensitive PascalCase whole-name match against `Vmad`/`VMad`/`VMAD`/`Adapter` plus exact `VM`/`Scripts`, plus type-assignability check to `VirtualMachineAdapter`. (One first-pass false-positive fixed mid-probe — `lower.Contains("script")` matched `description` because PascalCase substring hits across word boundaries.)
2. `v2.8 P4 — VMAD round-trip preservation test (Mutagen-direct, no bridge)` — best-effort full-mod read+write through Mutagen with `BinaryWriteParameters` discovered via reflection (set every `*Option` enum to `NoCheck` plus picked the most-permissive `ALowerRangeDisallowedHandlerOption` concrete). Round-trip write didn't complete; input-side byte scan ran unconditionally.

**Verdict (three-stream evidence — all converge on Case A):**

| Stream | Outfit | Spell |
|---|---|---|
| Phase 2 disambiguator (typeof reflection on concrete + interfaces + base chain) | null on all | null on all |
| Phase 4 full property dump (all public-instance properties) | 0 VMAD-shaped of 11 | 0 VMAD-shaped of 26 |
| Phase 4 byte-scan of vanilla Skyrim.esm GRUP body | 0 VMAD signatures in 174,425 bytes (SPEL) / 33,889 bytes (OTFT) | (same SPEL row applies) |

The Mutagen 0.53.1 schema has no API surface to access VMAD on Outfit or Spell, AND Bethesda's vanilla data has no precedent for VMAD subrecords on these record types. Resolving requires an upstream Mutagen schema change.

**Round-trip archaeology note** (for future sessions probing similar Mutagen-direct round-trips on master ESMs): `SkyrimMod.CreateFromBinary` reads Skyrim.esm fine in ~8s, but `WriteToBinary` rejects whole-mod write-back with `LowerFormKeyRangeDisallowedException: Lower FormKey range was violated by: 00005B:Skyrim.esm`. None of `BinaryWriteParameters.LowerRangeDisallowedHandler`'s three concrete options bypass this — `ThrowIfLowerRangeDisallowed` throws (default), `NoCheckIfLowerRangeDisallowed` still throws, `AddPlaceholderMasterIfLowerRangeDisallowed` still throws. There appear to be additional master-flag invariants enforced outside the option suite. Don't waste time rediscovering this; if a real consumer needs Mutagen-direct round-trip of a master ESM, set up a smaller test mod or a non-master ESP equivalent.

### Item 2 — Cross-check VMAD probe on every attach_scripts type ⇒ 0 overstatements

`tools/race-probe/Program.cs` extended with `v2.8 P4 — Cross-check VMAD reflection probe on every attach_scripts supported type`. Probed all 11 types post-Outfit/Spell removal. Results:

| Type | Reflected VirtualMachineAdapter declared type |
|---|---|
| Npc | `VirtualMachineAdapter` (base concrete) |
| **Quest** | **`QuestAdapter`** (subclass) |
| Armor | `VirtualMachineAdapter` |
| Weapon | `VirtualMachineAdapter` |
| Container | `VirtualMachineAdapter` |
| Door | `VirtualMachineAdapter` |
| Activator | `VirtualMachineAdapter` |
| Furniture | `VirtualMachineAdapter` |
| Light | `VirtualMachineAdapter` |
| MagicEffect | `VirtualMachineAdapter` |
| **Perk** | **`PerkAdapter`** (subclass) |

**Cross-check verdict: schema list is accurate post-Outfit/Spell removal. No further docs corrections needed inline.** Quest+Perk both expose `VirtualMachineAdapter` with subclass declared types — confirms item 4's fix angle.

A bonus inheritance-chain probe ran during item 4 troubleshooting (see § Deviations) — found that `VirtualMachineAdapter`, `PerkAdapter`, and `QuestAdapter` are sibling concrete classes under the abstract base `Mutagen.Bethesda.Skyrim.AVirtualMachineAdapter`, not a base→subclass hierarchy. This drove item 4's local-variable typing decision.

### Item 3 — OTFT/SPEL outcome handling: docs-only Case (A) path

- `mo2_mcp/tools_patching.py:104` — Outfit and Spell removed from the attach_scripts supported-records list. Note added that PERK/QUST use subclassed adapters auto-created correctly post-item-4. Stale "Auto-creating an adapter on PERK/QUST records with no existing scripts uses the wrong adapter subclass" sentence removed.
- `KNOWN_ISSUES.md` — new "Outfit/Spell `attach_scripts`" entry under v2.8.0 carry-over limitations, capturing the three-stream Case (A) evidence and framing as "Mutagen 0.53.1 schema absence + Bethesda data has no precedent" per Aaron's KNOWN_ISSUES wording guidance.
- `tools/coverage-smoke/Program.cs` — `AttachScriptTest`'s "does not support scripts" SKIP path strengthened with the three-stream Case (A) reason. Both the console message and the `skipReasons` traceback now include the full evidence summary so future sessions reading the SKIP reason don't re-investigate without a Mutagen schema upgrade.

Coverage-smoke 1.r.40 (OTFT) and 1.r.47 (SPEL) retain SKIP with the strengthened reason.

### Item 4 — `perk_quest_adapter_subclass` fix

`tools/mutagen-bridge/PatchEngine.cs:1727+` (`ApplyAttachScripts`):

- Local typed as `AVirtualMachineAdapter` (the abstract base), not `VirtualMachineAdapter` (one of the three sibling concretes). Pre-Phase-4 typing made the `as VirtualMachineAdapter` cast return null on PERK/QUST values (PerkAdapter/QuestAdapter aren't VirtualMachineAdapter subclasses), which sent the auto-create path into the wrong concrete.
- Auto-create path now uses `(AVirtualMachineAdapter)Activator.CreateInstance(vmadProp.PropertyType)!` — constructs PerkAdapter for PERK, QuestAdapter for QUST, base `VirtualMachineAdapter` for everyone else (whose property declares the base concrete; the call reduces to the prior path).
- `vmad.Scripts.Add(script)` at the existing append loop compiles clean against `AVirtualMachineAdapter` — the abstract base declares `Scripts`.

**Verification:** Race-probe Batch 7 PERK + QUST adapter probes run post-fix. Both report:

```
output.PERK.VirtualMachineAdapter runtime type: PerkAdapter
✓ PerkAdapter constructed correctly — bug DOES NOT reproduce on this code path
output.QUST.VirtualMachineAdapter runtime type: QuestAdapter
✓ QuestAdapter constructed correctly — bug DOES NOT reproduce on this code path
```

### Item 5 — `add_conditions` `actor_value` parameter

`tools/mutagen-bridge/Models.cs` — `ConditionEntry` gained `[JsonPropertyName("actor_value")] public string? ActorValue`. Optional — null preserves pre-v2.8.0 behavior (ActorValue defaults to enum index 0 = Aggression).

`tools/mutagen-bridge/PatchEngine.cs` `BuildCondition` — between RunOnType setter and ConditionFloat/ConditionGlobal branch, when `ce.ActorValue != null`:
- Look up `condDataType.GetProperty("ActorValue")` (the Mutagen enum slot on `GetActorValueConditionData`, `GetBaseActorValueConditionData`, `GetActorValuePercentConditionData`, etc.).
- Throw `ArgumentException` if the function's ConditionData has no ActorValue slot (consumer passed actor_value to the wrong function).
- Parse via `Enum.TryParse(avProp.PropertyType, ce.ActorValue, ignoreCase: true, ...)`.
- Set on the constructed condData.

`mo2_mcp/tools_patching.py` — schema description for `add_conditions` items gained the `actor_value` field with the supported-functions note.

**Implementation stayed minimal** — single `Enum.TryParse` plus property reflection. Generic Mutagen ConditionData parameter-slot mechanism (per-function dispatch + per-parameter-type routing for FormLink slots on `GetIsID` / `GetInFaction` / `GetInCell`) would have been substantially more code; per Aaron's scope-lock (and confirmed during implementation), other ConditionData parameters stay v2.9 candidates.

### Item 6 — Helper-throw → Tier D unification on `add_conditions` / `remove_conditions`

`tools/mutagen-bridge/PatchEngine.cs`:

- `ApplyAddConditions(record, conditions)`: return type `int` → `int?`. Returns `null` instead of throw when `Conditions` property is missing.
- `ApplyRemoveConditions(record, removals)`: same shape change.
- Call sites at `:875` and `:880` updated to skip writing `mods["conditions_added"]` / `mods["conditions_removed"]` when the helper returns null. Tier D's coverage check at `:929-938` then surfaces uniform `unmatched_operators: ["add_conditions"]` / `["remove_conditions"]` shape — same as every other unsupported (operator, record-type) combo.

`tools/coverage-smoke/Program.cs` test-3 + test-157 assertion shapes updated:
- Test 3 (`remove_conditions on ARMO`): pre-v2.8.0 asserted helper-throw text `"does not support conditions"`; post-v2.8.0 asserts `unmatched_operators: ["remove_conditions"]`.
- Test 157 (`4.c.01-carry`, `add_conditions on QUST`): pre-Phase-4 accepted EITHER shape (Tier D OR helper-throw); post-Phase-4 tightened to Tier D-only (`unmatched_operators: ["add_conditions"]`).

These assertion changes documented v2.7.1 behavior; v2.8.0 changed the documented behavior, so the assertions had to update. Per Aaron's classification: in-scope assertion updates, not regressions.

### Item 7 — Coverage-smoke 1.A.01 cell label alignment

Phase 3 finding #1 had noted MATRIX.md's pre-correction 1.A.01 cell labeled `Skyrim.esm:08F95E` as VendorItemFood (actually `AMBWindloopMountainsHills01LP` audio category record). Investigation found the harness was always correct — tests 7-12 / 13-18 used `FreshKwFor(record.Keywords)` which selects a real KYWD by predicate, never the literal. Only the matrix label was wrong, and MATRIX.md's Phase 3 corrections section had already fixed it.

Phase 4 alignment: backfilled MATRIX cell IDs into the type-label parameters for coverage-smoke tests 7-18 (e.g. `"RACE"` → `"RACE [1.A.01]"`) so printed output and skipReasons traceback match the matrix numbering Phase 2 introduced for tests 31+. Comment added explaining the historical context.

### Item 8 — Regression tests for items 4/5/6

Three new positive-case cells added to coverage-smoke (tests 158-160):

- **Test 158 (`4.c.06-rgr`)** — PERK + attach_scripts. Lifted from race-probe Batch 7. Picks first PERK without VMAD; readback asserts the constructed `VirtualMachineAdapter` runtime type equals `"PerkAdapter"`. PASS verifies item 4 fix.
- **Test 159 (`4.c.07-rgr`)** — QUST + attach_scripts. Same pattern, expects `QuestAdapter`. PASS verifies item 4 fix.
- **Test 160 (`4.regr.av`)** — MGEF + add_conditions with `actor_value: "Health"`. Picks first MGEF with a Conditions container; reads back the Conditions list, finds the appended `GetActorValueConditionData` entry by type (the source MGEF may carry pre-existing conditions, so the new entry isn't necessarily at index 0), reflects `Data.ActorValue`, asserts `== ActorValue.Health`. PASS verifies item 5 wire-up.

Item 6's regression coverage is the test-3 + test-157 assertion-shape updates from item 6 itself (no separate cell needed — those tests already exercise the helper-throw → Tier D unification path).

### Item 9 — Bridge build clean

`cd tools/mutagen-bridge && dotnet build -c Release` — 0 warnings, 0 errors.

**Bridge SHA after Phase 4:** `74df93131fa953222bb185106374b89af51a372964d5bb80d17c69eb388332c1`
(at `tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`)

Differs from Phase 1+3's `f998c4e0…c8bb04` — expected, since `PatchEngine.cs` and `Models.cs` both changed.

`build-output/mutagen-bridge/mutagen-bridge.exe` still holds Phase 3's interim publish SHA `fb723cd3…48926fa`. Phase 5's ship sequence will produce a fresh publish from the post-Phase-4 build.

Bridge build environment: dotnet SDK 9.0.311 building net8.0 target — 0 framework-mismatch or downgrade warnings (verified per Aaron's flagged concern).

### Item 10 — Coverage-smoke end-to-end

`dotnet run -c Release --no-build --project tools/coverage-smoke` — exit 0, **`=== smoke complete: ALL PASS ===`**.

| Layer | Tests | Strict PASS | PASS (documented) | FAIL | SKIP |
|---|---:|---:|---:|---:|---:|
| Pre-existing v2.7.1 (1–22) | 22 | 22 | 0 | 0 | 0 |
| Phase 1 Layer 1.E (23–30) | 8 | 8 | 0 | 0 | 0 |
| Phase 2 Batch 1: 1.A depth + 1.B + 1.C (31–52) | 22 | 22 | 0 | 0 | 0 |
| Phase 2 Batch 2: 1.regression keywords (53–67) | 15 | 15 | 0 | 0 | 0 |
| Phase 2 Batch 3: 1.regression non-keyword (68–109) | 42 | 40 | 0 | 0 | 2 |
| Phase 2 Batch 4: 1.D Tier D negatives (110–121) | 12 | 11 | 0 | 0 | 1 |
| Phase 2 Batch 5: Layer 2 combinatorial (122–134) | 13 | 13 | 0 | 0 | 0 |
| Phase 2 Batch 6: Layer 4 edges (135–157) | 23 | 17 | 5 | 0 | 1 |
| **Phase 4 regression cells (158–160)** | **3** | **3** | **0** | **0** | **0** |
| **Total** | **160** | **151** | **5** | **0** | **4** |

The 4 SKIPs all carry forward from Phase 2's documented set, with 1.r.40 + 1.r.47 now using the strengthened three-stream Case (A) reason:

| Cell | Reason |
|---|---|
| 1.r.40 (OTFT) | Case (A) confirmed Phase 4 via 3 evidence streams (typeof reflection null on concrete + interfaces + base chain; full property dump finds zero VMAD-shaped properties; vanilla Skyrim.esm SPEL/OTFT GRUP byte-scan shows zero VMAD subrecords) |
| 1.r.47 (SPEL) | Same |
| 1.D.04 (CELL) | Mutagen 0.53.1 CellBinaryOverlay can't be overridden via GetOrAddAsOverride; bridge errors before Tier D dispatch (carry-forward from Phase 2) |
| 4.esl.01 | Layer 4 ESL master interaction requires live modlist; deferred to Phase 3 (Phase 3's verification ran the equivalent against Authoria modlist) |

Output captured at `<workspace>/scratch/v2.8-phase-4-coverage.txt` (gitignored).

### Item 11 — Schema + CHANGELOG + KNOWN_ISSUES updates

- **`mo2_mcp/tools_patching.py`** — `attach_scripts` list updated (Outfit/Spell removed; PERK/QUST subclassed adapter note added; stale subclass-bug note removed). `add_conditions` items gained `actor_value` field with supported-functions documentation.
- **`mo2_mcp/CHANGELOG.md`** — v2.8.0 entry expanded:
  - Existing `### Added — bridge` (Phase 1 work) preserved verbatim.
  - New `### Added — bridge` bullet for `add_conditions actor_value` parameter.
  - New `### Fixed — bridge` section with `perk_quest_adapter_subclass` fix detail.
  - New `### Changed — bridge` section with helper-throw → Tier D unification.
  - New `### Changed — schema description` section with three-stream Case (A) evidence for Outfit/Spell removal.
  - New `### Documentation` section noting the matrix-cell-ID backfill for tests 7-18.
  - `### Out of scope (v2.9 candidates)` updated: removed the "Adapter-subclass attach_scripts on PERK/QUST" line (now fixed), added "Other Condition-function parameter slots" line capturing the v2.8.0-vs-v2.9 split.
- **`KNOWN_ISSUES.md`** — v2.8.0 section updated:
  - Old "Adapter-subclass `attach_scripts` on PERK/QUST" entry removed (fixed in item 4).
  - New "Outfit/Spell `attach_scripts`" entry with three-stream Case (A) evidence.
  - New "Schema observations (Phase 3 verification, useful when authoring patches)" subsection covering PERK no-`Configuration` clarification + LVSP topology constraint on Authoria-style modlists.

## Verification performed

### Bridge build

`cd tools/mutagen-bridge && dotnet build -c Release` — 0 warnings, 0 errors. SHA `74df9313…332c1`.

### Race-probe (item 1 + item 2 + item 4 verification)

`dotnet run -c Release --no-build --project tools/race-probe` — exit 0, `=== probe complete ===`. Output captured at `<workspace>/scratch/v2.8-phase-4-probe.txt`.

Includes:
- Original Phase 1 Effects API contract probe (passes).
- Original Phase 2 Batch 7 disambiguator (Case A confirmed for Outfit + Spell).
- New Phase 4 full property dump (0 VMAD-shaped on Outfit/Spell).
- New Phase 4 round-trip preservation test (write failed; input byte-scan reports 0 VMAD in vanilla SPEL/OTFT GRUP).
- New Phase 4 cross-check probe (11 types, 0 overstatements; QuestAdapter + PerkAdapter declared types observed).
- Bonus inheritance-chain dump (VirtualMachineAdapter / PerkAdapter / QuestAdapter all derive from `AVirtualMachineAdapter`).
- Updated PerkAdapter / QuestAdapter functional probes — both now ✓ post-fix.

### Coverage-smoke (item 4/5/6 regression + items 7-10 verification)

`dotnet run -c Release --no-build --project tools/coverage-smoke` — exit 0, `=== smoke complete: ALL PASS ===`. 160 cells; 151 strict PASS, 5 PASS (documented), 0 FAIL, 4 SKIP. See § Item 10 for the per-batch breakdown.

## Bugs surfaced

**Zero new bridge bugs** — Phase 4's deliverables landed cleanly. The only mid-session diagnostic loop was item 4's first attempt (typed local as `VirtualMachineAdapter`) producing a different cast error than the original bug; the bonus inheritance-chain probe found the abstract base `AVirtualMachineAdapter` and the second attempt landed clean.

## Findings

**Zero new findings beyond what Phase 4 was already absorbing.** Phase 1's findings (Effects-list capability, IFormLinkNullable bonus-catch) shipped. Phase 2's findings (perk_quest_adapter_subclass, OTFT/SPEL Case A provisional, helper-throw → Tier D divergence) all addressed. Phase 3's findings (1.A.01 FormID label, MATRIX merge response field name, Configuration.PerkType, LVSP topology, GetActorValue parameterless default) all addressed.

## Deviations from plan

1. **Item 4's first attempt cast to `VirtualMachineAdapter` failed differently than expected.** The plan's proposed fix angle was `vmad = (VirtualMachineAdapter)Activator.CreateInstance(vmadProp.PropertyType)!`, on the assumption that PerkAdapter and QuestAdapter inherit from VirtualMachineAdapter (matching Mutagen's "X / XAdapter" naming pattern). At runtime the assumption proved wrong — they're sibling concretes under abstract `AVirtualMachineAdapter`. Diagnosis: ~10min loop including a one-shot inheritance-chain probe extension. Resolution: changed the cast target to `AVirtualMachineAdapter`. The fix is structurally what the plan intended (subclass-aware adapter construction); only the local-variable type changed. Captured as a probe + handoff note for future archaeology.
2. **Item 1 round-trip didn't complete due to Mutagen master-flag invariants.** Three `BinaryWriteParameters` enum guards plus the `LowerRangeDisallowedHandler` concrete subclass were probed; none bypass the throw on writing whole Skyrim.esm back. Property dump + byte-scan streams were sufficient for Case (A); round-trip half is documented as a non-blocker per Aaron's review. Archaeology note in § Item 1 above so future sessions don't waste time.
3. **Item 7 was effectively a no-op for the harness.** Phase 3 finding #1 framed the swap as "harness uses literal 08F95E"; investigation found the harness was always correct (uses `FreshKwFor` predicate) and only MATRIX.md's label was wrong (already fixed in MATRIX's Phase 3 corrections section). Phase 4 backfilled cell IDs into the test labels for output traceability.
4. **No bonus-catch absorbed.** No latent bridge issue surfaced during item 4/5/6 work. PatchEngine.cs touches were narrow (one local-var type change + Activator-create-by-prop-type for item 4; one BuildCondition extension for item 5; two helper return-type changes + two call-site updates for item 6).

## Known issues / open questions

1. **Mutagen 0.53.1 master ESM round-trip is gated by undocumented invariants.** None of `BinaryWriteParameters`'s exposed surface bypasses the `LowerFormKeyRangeDisallowedException` on writing Skyrim.esm back through Mutagen — the three `LowerRangeDisallowedHandler` concretes (`Throw`, `NoCheck`, `AddPlaceholder`) all throw against the actual binary. There must be additional master-flag invariants enforced outside the option suite. Not blocking v2.8.0 — captured here for future probe sessions that may need round-trip-of-master support.
2. **Pre-existing CS8602 warnings in coverage-smoke `AttachScriptTest`** at line 3932 (Phase 2's harness extension). Not introduced by Phase 4; not worth a Phase 4 driveby clean.

## Preconditions for next session (Phase 5 — re-run + ship v2.8.0)

- ✅ `origin/main` ready to advance to Phase 4's commit pair (TBD — pending sign-off).
- ✅ Bridge builds clean — `74df9313…332c1` at `tools/mutagen-bridge/bin/Release/net8.0/`.
- ✅ Coverage-smoke runs to completion: 160 cells, all PASS (151 strict + 5 documented), 4 SKIP, 0 FAIL.
- ✅ Race-probe runs to completion; PerkAdapter/QuestAdapter probes flip from ✗ pre-Phase-4 to ✓ post-Phase-4. Phase 4 VMAD probes confirm Case A for Outfit + Spell; cross-check confirms 0 overstatements among the remaining 11 types.
- ✅ Schema + CHANGELOG + KNOWN_ISSUES updated per items 1+2+3+4+5+6+7+11.
- ⏸️ Live install at v2.8.0 (Phase 3 interim sync's bridge SHA `fb723cd3…48926fa`); Phase 5 will publish post-Phase-4 bridge → `build-output/` → live, generating a new SHA. The Phase 5 re-run (Scenarios 3.1, 3.4, 3.5 against the post-Phase-4 bridge) is a required step per the conductor's amended plan.
- ⏸️ Two commits pending Aaron's sign-off (per v2.7.1 double-commit cadence): work commit `[v2.8 P4] Bridge fixes + matrix corrections + docs hygiene` + handoff hash-record commit `[v2.8 P4] Handoff: record commit hash <work-hash>`.

## Files of interest for next session (Phase 5)

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.8.0_verification/PLAN.md` § Phase 5 | Authoritative steps for the ship sequence (re-run + publish + installer + tag + release). |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_4_HANDOFF.md` (this file) | Phase 4 deliverable summary + bridge SHA + coverage-smoke results. |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` | Bridge code post-fix; Phase 5 re-runs Scenarios 3.1, 3.4, 3.5 against this. |
| `Claude_MO2/build-output/mutagen-bridge/` | Phase 3's interim publish; Phase 5 publishes post-Phase-4 build over this. |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` | v2.8.0 entry — Phase 5 inserts ship date `## v2.8.0 — 2026-MM-DD`. |
| `Claude_MO2/installer/claude-mo2-installer.iss` | AppVersion 2.8.0 already set in Phase 1; Phase 5 invokes ISCC against it. |

## Acceptance — Phase 4

- ✅ OTFT/SPEL VMAD disambiguation verdict captured (Case A) with three-stream evidence in handoff.
- ✅ Cross-check probe captures per-type verdict for every attach_scripts supported type (11 types, 0 overstatements).
- ✅ `perk_quest_adapter_subclass` bug fix verified by lifted race-probe Batch 7 → coverage-smoke regression cells (158/159).
- ✅ `add_conditions actor_value` parameter accepted by bridge; readback confirms `ActorValue.Health` enum routes correctly (test 160).
- ✅ Helper-throw → Tier D unification verified by coverage-smoke regression cells (test 3 + test 157 assertion shape updates).
- ✅ Coverage-smoke 1.A.01 cell uses real KYWD via `FreshKwFor` (always did; matrix-label backfill landed for tests 7-18).
- ✅ Bridge builds clean (0 warnings, 0 errors). SHA `74df9313…332c1`.
- ✅ Coverage-smoke runs to 160 cells, ALL PASS (151 strict + 5 documented), 4 SKIPs (1.r.40 + 1.r.47 with strengthened Case (A) reason; 1.D.04 + 4.esl.01 carry-forward).
- ✅ Schema description, CHANGELOG, KNOWN_ISSUES updated per item 11.
- ⏸️ Two commits pending Aaron's sign-off.
