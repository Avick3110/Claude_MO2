# v2.9.1 Verification Matrix

**Authoritative test specification for v2.9.1's verification pass.** Mirrors v2.9.0's MATRIX.md role for v2.8.0 → v2.9.0; this matrix serves the v2.9.0 → v2.9.1 transition. Anchored on **Quest condition disambiguation** (the v2.9.1 capability) — extending the bridge's `add_conditions` / `remove_conditions` reflection lookup at [PatchEngine.cs:1576](../../../tools/mutagen-bridge/PatchEngine.cs#L1576) + [:2264](../../../tools/mutagen-bridge/PatchEngine.cs#L2264) from a hardcoded `Conditions` slot to a per-list-target dispatch via the new `condition_target` operator parameter, per PLAN.md § A.

**Methodology.** Every cell is one bridge invocation (or one Mutagen direct call for race-probe functional probes), with the listed operation against the listed source record, and a documented expected result. Layers 1, 2, 4, 5 run via `tools/coverage-smoke/` against vanilla Skyrim.esm at `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. Layer 3 runs via `mo2_create_patch` against the live Authoria modlist, output to `<modlist>/mods/Claude Output/v2.9.1-scenario-N.esp`, deleted post-verification.

**Record selection.** Layer 1 / 2 / 4 use `coverage-smoke`'s existing `FirstOrDefault` predicate selection where possible; the QUST anchor for the per-list-target cells needs a vanilla Skyrim.esm quest with both `DialogConditions` and `EventConditions` populated for round-trip-distinguishability. **Phase 1 selected `Skyrim.esm:04C49D` (FollowerCommentary01)** — Dialog=1 (`GetInFaction`) + Event=1 (`GetEventData`), disjoint function distribution ideal for byfunc round-trip-distinguishability. PLAN.md § Phase 1 step 2's `MQ101` `Skyrim.esm:000242` candidate does NOT exist in vanilla Skyrim.esm (the MQ101 INFO at `Skyrim.esm:000E3D` from v2.9.0 P4-INFO is a distinct record); see PHASE_1_HANDOFF.md § Deviations from plan. Secondary anchor `Skyrim.esm:0E3145` (CR12) — Dialog=3, Event=3 — available if Phase 2 needs higher pre-state variance.

**Pass/fail contract.** Every row's "Expected" column is the assertion the harness checks. PASS = response matches Expected exactly. FAIL = surface as a bug entry in the appropriate phase's handoff, including the actual response payload.

**Phase fill-in cadence.** Phase 0 (commit `144f021`) laid down the layer scaffold + cell-naming convention + Layer 3 scenario use-case descriptions. **Phase 1 (this commit)** anchored Layer 1.P on `Skyrim.esm:04C49D` (FollowerCommentary01), populated the byfunc cells with `GetInFaction` (dialog) + `GetEventData` (event), and locked generality scope to QUST-only (probe found 1 multi-condition record — Quest only — in Mutagen.Bethesda.Skyrim 0.53.1; halt threshold 5+ trivially satisfied at 0). **Phase 2** runs the harness end-to-end after wiring `condition_target` in `Models.cs` + `PatchEngine.cs`. **Phase 3** picks live FormIDs for Layer 3 scenarios.

---

## 🧭 Cell-naming convention

| Prefix | Layer | Pattern | Example |
|---|---|---|---|
| `1.P.<op>.<target>.<RecordType>` | 1 — per-list-target positives | operator + list target + carrier record type | `1.P.add.dialog.QUST`, `1.P.remove.event.byfunc.QUST` |
| `1.D.<NN>` | 1.D — negatives + new explicit error paths | sequential within layer | `1.D.01`, `1.D.05` |
| `2.<NN>` | 2 — combinatorial | sequential | `2.01`, `2.03` |
| `3.<N>` | 3 — workflow scenarios | scenario number | `3.1`, `3.2` |
| `4.<sub>.<NN>` | 4 — edges | sub-grouping + sequential | `4.dsl.01`, `4.dsl.03` |
| `5.<NN>` | 5 — regression | range row, mapped to v2.9.0's 382 cells (see § Layer 5) | `5.range` |

The `1.P.<op>.<target>.<RecordType>` form anchors on **list target** (the v2.9.1 unit of work) — not function name (v2.9.0's anchor) or operator-tier (v2.8.0's). v2.9.1's mechanism dispatches on which `*Conditions` list to read/write; per-function coverage is v2.9.0's already-shipped surface and rolls into Layer 5 regression. `1.D.<NN>` carries v2.9.0's negative-band convention forward. Layer 4's only sub-grouping is `dsl` (parameter-value-form edges) — v2.9.0's `slot` / `formid` / `enum` / `compat` / `carry` sub-groups don't apply because v2.9.1 doesn't change the per-Condition build pipeline.

**Per-list-target coverage is QUST-only (Phase 1 confirmed).** Phase 1's schema probe (output at `<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt`) found **1 multi-condition record type** in `Mutagen.Bethesda.Skyrim` 0.53.1: Quest. The other 15 condition-carrying records (PERK / PACK / IDLE / MGEF / DialogResponses / Faction / Scene / MusicTrack / SoundDescriptor / IdleAnimation / LoadScreen / CameraPath / ConstructibleObject / StoryManagerBranchNode / StoryManagerEventNode / StoryManagerQuestNode) are all single-`Conditions`. Halt threshold (5+ types per CONDUCTOR_KICKOFF.md line 38) trivially satisfied at 0. Generality scope locked to QUST-only via conductor relay; Phase 0's extensibility scaffold preserved structurally but Layer 1.P remains 6 cells.

---

## Layer 1 — Per-list-target coverage (positives)

**v2.9.1 in-scope:** QUST records, two list targets (`dialog` → `DialogConditions`, `event` → `EventConditions`). Six cells exercise the matrix of {add, remove-by-index, remove-by-function} × {dialog, event}.

Each row's expected result follows the shape:

> bridge response `mods.conditions_added=N` (for add) or `mods.conditions_removed=N` (for remove); readback via Mutagen-direct against the output ESP shows the condition lands in / leaves the targeted list (`DialogConditions` or `EventConditions`); the **non-targeted list is unchanged** (round-trip-distinguishability).

**Carrier convention.** QUST is the canonical and only carrier for v2.9.1 Layer 1.P. Phase 1's probe selected **`Skyrim.esm:04C49D` (FollowerCommentary01)** as the anchor — Dialog=1 (`GetInFaction`) + Event=1 (`GetEventData`), disjoint function distribution ideal for byfunc round-trip-distinguishability (the function name in each list does not appear in the other, so byfunc removal against the targeted list has zero ambiguity vs the non-targeted list). Secondary anchor **`Skyrim.esm:0E3145` (CR12)** — Dialog=3, Event=3 — available if Phase 2 needs higher pre-state variance.

**Source-of-truth for property names:** Phase 1's `tools/race-probe/Program.cs` schema sweep against `IQuestGetter` in Mutagen 0.53.1. The matrix names the friendly target names (`dialog`, `event`) per PLAN.md § B baseline; the property literal mapping (`DialogConditions`, `EventConditions`) is Phase 1-confirmed.

### 1.P.add — add_conditions to a targeted list

| # | Op | Target | Carrier | Operation | Expected |
|---|----|--------|---------|-----------|----------|
| `1.P.add.dialog.QUST` | add_conditions | `dialog` (→ `DialogConditions`) | `Skyrim.esm:04C49D` | add 1 condition with `function: "GetIsID"`, `parameters: {Object: "<NPC-FormID>"}`, `condition_target: "dialog"` | mods.conditions_added=1; readback `DialogConditions.Count == anchor.DialogConditions.Count + 1`; new condition's `Data.Object.FormKey` resolves; `EventConditions.Count` unchanged (round-trip-distinguishability) |
| `1.P.add.event.QUST` | add_conditions | `event` (→ `EventConditions`) | `Skyrim.esm:04C49D` | as above with `condition_target: "event"` and a representative event-list condition (e.g. `function: "HasPerk"`, `parameters: {Perk: "<PERK-FormID>"}`) | mods.conditions_added=1; readback `EventConditions.Count == anchor.EventConditions.Count + 1`; new condition's `Data.Perk.FormKey` resolves; `DialogConditions.Count` unchanged |

### 1.P.remove — remove_conditions by index from a targeted list

| # | Op | Target | Carrier | Operation | Expected |
|---|----|--------|---------|-----------|----------|
| `1.P.remove.dialog.QUST` | remove_conditions | `dialog` | `Skyrim.esm:04C49D` | remove condition at `index: 0` from DialogConditions with `condition_target: "dialog"` | mods.conditions_removed=1; readback `DialogConditions.Count == anchor.DialogConditions.Count - 1`; the previous index-0 entry is gone (function name + slot values match the pre-state index-0); `EventConditions` unchanged |
| `1.P.remove.event.QUST` | remove_conditions | `event` | `Skyrim.esm:04C49D` | remove condition at `index: 0` from EventConditions with `condition_target: "event"` | mods.conditions_removed=1; readback `EventConditions.Count == anchor.EventConditions.Count - 1`; previous index-0 entry gone; `DialogConditions` unchanged |

### 1.P.remove.byfunc — remove_conditions by function name from a targeted list

Phase 1's probe pre-confirms which condition functions appear in the anchor QUST's DialogConditions / EventConditions; Phase 2 picks a function present in the targeted list (and absent or differently-positioned in the other list, for round-trip-distinguishability).

| # | Op | Target | Carrier | Operation | Expected |
|---|----|--------|---------|-----------|----------|
| `1.P.remove.dialog.byfunc.QUST` | remove_conditions | `dialog` | `Skyrim.esm:04C49D` | remove all conditions with `function: "GetInFaction"` from DialogConditions with `condition_target: "dialog"` | mods.conditions_removed=N (where N = pre-state count of `GetInFaction` in DialogConditions); readback DialogConditions has zero conditions with that function name; `EventConditions` matching-function entries (if any) are NOT removed |
| `1.P.remove.event.byfunc.QUST` | remove_conditions | `event` | `Skyrim.esm:04C49D` | remove all conditions with `function: "GetEventData"` from EventConditions with `condition_target: "event"` | mods.conditions_removed=N; readback EventConditions has zero conditions with that function name; `DialogConditions` matching-function entries (if any) are NOT removed |

---

## Layer 1.D — Negatives + new explicit error paths

Five cells exercising the new error surface from PLAN.md § C plus the existing Tier D path on records with no `*Conditions` property at all. Wording for the new error messages is finalized in Phase 2 implementation; this matrix locks the shape and the rollback contract.

| # | Op | Setup | Expected |
|---|----|-------|----------|
| `1.D.01` | add_conditions | QUST `add_conditions` request **without** `condition_target` field; one or more entries supplied | new explicit per-record error per PLAN.md § C #2: `"Record type Quest requires a condition_target parameter on add_conditions. Available targets: 'dialog' (DialogConditions) | 'event' (EventConditions). Quest records carry two condition lists rather than a single Conditions list — see KNOWN_ISSUES.md § Patching write surface."`; mods.conditions_added field absent from response (record skipped); other ops on the same record (if any) proceed; output ESP omits the failed condition write |
| `1.D.02` | remove_conditions | QUST `remove_conditions` request **without** `condition_target` field; symmetric to 1.D.01 | new explicit per-record error per § C #2 with `remove_conditions` named in the message; rollback contract identical to 1.D.01 |
| `1.D.03` | add_conditions | QUST `add_conditions` with `condition_target: "story"` (bad enum value — not in the locked target set) | new explicit per-record error per § C #3: `"Unknown condition_target: 'story'. Valid values: 'dialog' | 'event'."` (case sensitivity per Q5 lock — message recites the valid set in the locked canonical case) |
| `1.D.04` | add_conditions | PERK record (single `Conditions` list) with `condition_target: "dialog"` supplied | per Phase 0 default (Q4 = reject): new explicit per-record error: `"Record type Perk does not support condition_target. PERK uses a single Conditions list — omit condition_target."` (or equivalent — Phase 2 finalizes wording). If Q4 lock flips to "ignore" via conductor relay, expectation flips: the call succeeds and the condition lands in PERK.Conditions (no-op on the unsupported parameter) |
| `1.D.05` | add_conditions | ARMO record (no condition list at all) with `condition_target: "dialog"` supplied | existing Tier D path fires uniformly: `unmatched_operators: ["add_conditions"]`. The `condProp == null → return null` path matches v2.9.0 behavior bit-identically — `condition_target` only affects the slot name looked up, not the failure shape when the targeted slot is absent. (Note: if Q4 locks to "reject", 1.D.04 fires earlier than the property lookup; 1.D.05 still surfaces Tier D because there's no targeted property even after the strict reject check — both error paths coexist and ARMO has no condition list of any kind to even attempt the rejection-vs-Tier-D distinction.) |

Phase 2 may add Layer 1.D rows programmatically if test patterns surface (e.g. a per-target negative for each Layer 1.P positive). The matrix locks the structural error-path coverage above; bulk-pattern derivatives are implementation choice for the harness.

---

## Layer 2 — Combinatorial probes

Multi-condition single op, multi-op single record, and v2.9.0 × v2.9.1 surface composition (the new `condition_target` parameter routing, the v2.9.0 `parameters` map dispatching).

| # | Scenario | Setup | Expected |
|---|----------|-------|----------|
| `2.01` | Multi-condition single op (one list target) | `add_conditions` on QUST with `condition_target: "dialog"`: 3 conditions in one list, mixed in-scope functions (e.g. `GetIsID` + `HasPerk` + `GetStageDone`), each with its own `parameters` | mods.conditions_added=3; readback `DialogConditions.Count == anchor.DialogConditions.Count + 3`; all three conditions appended in submitted order with their slot values resolved (verifies the foreach inside `ApplyAddConditions` iterates correctly within one list-target call) |
| `2.02` | Two ops, one record, opposing list targets | Same QUST record receives two ops in one `mo2_create_patch` call: op A with `condition_target: "dialog"` adds 2 conditions; op B with `condition_target: "event"` adds 1 condition. Note: structural limitation per PLAN.md § B — `add_conditions` is one field per op, so the two-ops form requires two op blocks in the request, not one op with split entries. Phase 2 confirms request shape support. | both ops succeed; mods aggregates `conditions_added=3` (or per-op = 2 + 1 depending on response shape — Phase 2 confirms); readback `DialogConditions.Count + 2`, `EventConditions.Count + 1`; cross-list isolation preserved (the dialog op did not touch event, vice versa) |
| `2.03` | v2.9.1 × v2.9.0 surface composition | `add_conditions` on QUST with `condition_target: "dialog"`: one condition `function: "GetIsID"` with `parameters: {Object: "<NPC-FormID>"}` (v2.9.0 generic dispatch path) routed via v2.9.1 list-target dispatch (`condition_target: "dialog"`) | mods.conditions_added=1; readback DialogConditions's new entry has `Data.GetType() == GetIsIDConditionData`; `Data.Object.FormKey` resolves to the supplied NPC FormKey (NOT FormID 0 — proves v2.9.0 dispatcher composes underneath untouched); `EventConditions` unchanged |

---

## Layer 3 — Workflow scenarios on live install

Run via `mo2_create_patch` against the live Authoria modlist. Output filenames `v2.9.1-scenario-N.esp`. Test patches deleted post-verification.

**Phase 0 pre-specs use cases + assertions; Phase 3 picks live FormIDs at execution time.** Aaron may swap the named records during Phase 3 if better targets exist in the live modlist.

### Scenario 3.1 — Quest DialogConditions: perk-gated quest visibility

**Use case.** Real-world dialog patcher: a QUST record in the live modlist gates its dialog visibility on the player (or a follower) having a prerequisite perk. The patcher needs to add a `HasPerk` Condition to the quest's `DialogConditions` list — the canonical motivating example for v2.9.1's mechanism per PLAN.md § Background bullet 1 ("Add a `GetIsID` condition gating a quest to a specific actor (DialogConditions — quest visibility)" — the perk-gating variant exercises the same DialogConditions write surface). Today the bridge accepts the `add_conditions` op against QUST but the reflection lookup `record.GetType().GetProperty("Conditions")` returns null (QUST exposes `DialogConditions` + `EventConditions`, not `Conditions`), so the call falls through uniformly to Tier D with `unmatched_operators: ["add_conditions"]`. v2.9.1 routes the write via `condition_target: "dialog"` to `DialogConditions` directly.

**Target (Phase 3 picks):**
- 1 QUST record from the live modlist with an existing `DialogConditions` list (or empty — bridge's `add_conditions` works either way per the v2.9.0 `add_conditions` semantics; an existing-non-empty target is preferable for round-trip-distinguishability against the "preserves existing entries" assertion).
- 1 prerequisite PERK FormID for the `HasPerk` `Perk` slot. Authoria modlist exposes the standard Skyrim perk tree plus Requiem-style additions; Phase 3 picks a perk relevant to the chosen QUST's lore-fit (e.g. a Speech-tree perk gating a merchant-related quest).

**Operations:**
- `add_conditions` on the QUST record with `condition_target: "dialog"`: one ConditionFloat with `function: "HasPerk"`, `operator: "==", "value": 1`, `parameters: {Perk: "<plugin>:<localID-of-prereq-perk>"}`.

**Assertions:**
- `mods.conditions_added=1`.
- Readback via `mo2_record_detail`: new entry in `DialogConditions` with `Data.GetType() == HasPerkConditionData`.
- Readback `condition.Data.Perk.FormKey` matches the supplied prereq-perk FormKey (NOT FormID 0 — proves v2.9.0's generic dispatcher composes underneath untouched).
- Readback `EventConditions` is unchanged from the source record (round-trip-distinguishability — confirms the write didn't accidentally land in the wrong list).
- Existing `DialogConditions` entries are preserved (add, not replace — matches v2.9.0 `add_conditions` semantics on single-Conditions carriers).
- Output ESP contains the QUST override; xEdit reads cleanly with no unresolved-FormID warning on the new condition.
- Pre-flight regression: same `mo2_create_patch` call shape against the same QUST **without** `condition_target` field surfaces the new explicit error per `1.D.01` (verifies Phase 2's missing-target check is wired live).

### Scenario 3.2 — Quest EventConditions: Story Manager perk-eligibility gating

**Use case.** Real-world Story Manager patcher: a QUST record in the live modlist gates its eligibility for a Story Manager event payload on the player having a prerequisite perk. The patcher needs to add a `HasPerk` Condition to the quest's `EventConditions` list — the canonical motivating example for v2.9.1's mechanism per PLAN.md § Background bullet 2 ("Add a `HasPerk` / `GetStageDone` precondition for a quest's eligibility for a given Story Manager event (EventConditions)"). Symmetric to 3.1 with the dispatch routing to the other list.

**Target (Phase 3 picks):**
- 1 QUST record from the live modlist with an existing `EventConditions` list (a Story-Manager-eligible quest — Authoria/Requiem modlists carry many; Phase 3 picks one with both DialogConditions and EventConditions populated for cross-scenario coverage if a single anchor can serve both 3.1 and 3.2). Reusing the same QUST for both scenarios maximizes round-trip-distinguishability — the dialog-target write must NOT touch EventConditions and vice versa.
- 1 prerequisite PERK FormID for the `HasPerk` `Perk` slot. May be the same perk as 3.1 or different per Phase 3's choice.

**Operations:**
- `add_conditions` on the QUST record with `condition_target: "event"`: one ConditionFloat with `function: "HasPerk"`, `operator: "==", "value": 1`, `parameters: {Perk: "<plugin>:<localID-of-prereq-perk>"}`.

**Assertions:**
- `mods.conditions_added=1`.
- Readback via `mo2_record_detail`: new entry in `EventConditions` with `Data.GetType() == HasPerkConditionData`.
- Readback `condition.Data.Perk.FormKey` matches the supplied prereq-perk FormKey.
- Readback `DialogConditions` is unchanged from the source record (round-trip-distinguishability — symmetric to 3.1's `EventConditions` unchanged check).
- Existing `EventConditions` entries are preserved.
- Output ESP contains the QUST override; xEdit clean.
- **If 3.1 and 3.2 target the same QUST in sequence** (within one `mo2_create_patch` call as two ops, or across two patches deleted-and-rebuilt between scenarios — Phase 3 picks the cleaner pattern), confirm cross-scenario isolation: the 3.1 write to DialogConditions is preserved through 3.2's write to EventConditions, and the 3.2 write to EventConditions is preserved through 3.1's prior write. This is the live-install equivalent of Layer 2.02 — operator-shape independence verified end-to-end.

---

## Layer 4 — Edges

DSL-form edges of the new `condition_target` operator parameter. v2.9.1's mechanism doesn't change v2.9.0's per-Condition build pipeline — slot dispatch / FormID resolution / enum case-insensitivity / back-compat / carry-over surfaces all stay identical to v2.9.0 Layer 4 sub-groups and are exercised via Layer 5 regression. v2.9.1's new edges are parameter-value-form only.

### 4.dsl — `condition_target` value form edges

| # | Setup | Expected |
|---|-------|----------|
| `4.dsl.01` | QUST `add_conditions` with `condition_target: ""` (empty string) | bad-value error per `1.D.03` shape: `"Unknown condition_target: ''. Valid values: 'dialog' | 'event'."` (Phase 2 confirms whether empty string and missing-field surface the same error or distinct errors — current matrix expectation: distinct, since `""` is a supplied value that fails the lookup, not an absent field) |
| `4.dsl.02` | QUST `add_conditions` with `condition_target: null` (JSON null literal) | treated as field-absent (JSON null is the C# `string? = null` deserialization for an optional field); fires the missing-target error per `1.D.01`. Distinct from `4.dsl.01` (empty string) — null = "field not supplied", empty string = "field supplied with empty value" |
| `4.dsl.03` | QUST `add_conditions` with `condition_target: "Dialog"` (uppercase initial — case-variant of `"dialog"`) | per Phase 0 default (Q5 = case-insensitive): success; same outcome as `1.P.add.dialog.QUST`; readback condition lands in DialogConditions. If Q5 lock flips to case-sensitive via conductor relay, expectation flips to bad-value error per `1.D.03`. (Phase 0 baseline rationale: matches v2.9.0 enum-parse posture `Enum.Parse(... ignoreCase: true)`.) |
| `4.dsl.04` | QUST request op with both `condition_target: "dialog"` AND an unrelated operator on the same record op (e.g. `add_keywords`) | `condition_target` is an operand for the conditions sub-operators only (`add_conditions` / `remove_conditions`) — NOT a global flag affecting the whole op. The unrelated operator (`add_keywords`) ignores `condition_target` and proceeds normally; the conditions sub-operator consumes `condition_target` and routes accordingly. mods aggregates both `keywords_added` and `conditions_added` independently. (If Phase 2's implementation surfaces a different read — e.g. `condition_target` is rejected at the op level when no conditions sub-operator is present — Phase 2 documents and the matrix expectation flips.) |

---

## Layer 5 — Regression band

All v2.9.0 coverage-smoke cells run unchanged. v2.9.1 must not regress any v2.9.0 behavior — the new `condition_target` parameter is additive, defaulting to the v2.9.0 hardcoded `"Conditions"` lookup when absent.

| Cell range | Source | Expected |
|---|---|---|
| `5.range` | `dev/plans/v2.9.X_condition_parameters/MATRIX.md` Layer 1.P + 1.D + 2 + 4 + 5 (382 v2.9.0 cells: 22 pre-v2.8.0 + 138 v2.8.0 + 134 P2A + 45 P2B + 32 P2C + 11 P2D, per the v2.9.0 P2 final tally) | each cell PASS as it did in v2.9.0 P5 (and v2.9.0's own Layer 5 = 160 v2.8.0 cells, transitively green). The 382 figure assumes coverage-smoke's v2.9.0-shipped cell count; Phase 2 confirms the actual baseline against the harness output before adding v2.9.1's new Layer 1 / 1.D / 2 / 4 cells. |

Specifically: every v2.9.0 `add_conditions` cell against a non-QUST carrier (MGEF / PERK / PACK / IDLE / INFO via response-level conditions) stays green **without** `condition_target` — the bridge's reflection lookup defaults to `"Conditions"` when `condition_target` is absent, preserving v2.9.0 behavior bit-identically. This is the core back-compat assertion of v2.9.1.

---

## Total assertion count (Phase 0 baseline)

**v2.9.1 capability surface is single — list-target dispatch on add/remove.** No Pareto pull, no function inventory, no slot-shape branches. Cell counts below are final at Phase 0 (scope-locked at PLAN write-time per § D); Phase 1 only adjusts if the schema probe surfaces additional in-scope multi-condition record types and Aaron locks them in.

| Layer | Matrix rows | Harness cells | Source |
|---|---:|---:|---|
| 1.P (per-list-target positives) | 6 | 6 | this doc |
| 1.D (negatives + new explicit error paths) | 5 | 5 | this doc |
| 2 (combinatorial) | 3 | 3 | this doc |
| 3 (workflow scenarios) | 2 scenarios (3.1 dialog + 3.2 event) | ~12–14 assertions | this doc; Phase 3 picks live FormIDs |
| 4.dsl | 4 | 4 | this doc |
| 5 (regression) | 1 (range row) | 382 | v2.9.0 baseline |
| **Total** | **~20 matrix rows** | **~400 harness cells** | — |

Phase 2 may dedupe or merge cells where the same code path is exercised twice. v2.9.0's MATRIX.md is the source of truth for the Layer 5 regression count; Phase 2 reads from `coverage-smoke/Program.cs`'s actual cell enumeration rather than from this matrix doc when running the full regression band.

**Extensibility note.** If Phase 1's schema probe surfaces additional multi-condition record types (PACK / SCEN / SMQN / SMEN / etc.) and Aaron locks them in v2.9.1 scope via the conductor relay, Layer 1.P extends with one cell per `(op, target, RecordType)` triple — for example, a hypothetical PACK with `OnBegin` / `OnEnd` / `OnChange` script-block conditions would add cells like `1.P.add.onbegin.PACK`, `1.P.add.onend.PACK`, etc. Layer 1.D's structural error rows generalize unchanged (the missing-target and bad-value error paths apply to any multi-list carrier). Phase 1's handoff documents the probe finding; Phase 2's first commit (post-conductor-lock) extends the matrix if scope generalized.

---

## Phase 2 harness output convention

`coverage-smoke/Program.cs` should print one line per assertion, mirroring v2.9.0:

```
[1.P.add.dialog.QUST]            add_conditions QUST  dialog  <anchor>      PASS (DialogConditions+1, EventConditions unchanged)
[1.P.add.event.QUST]             add_conditions QUST  event   <anchor>      PASS (EventConditions+1, DialogConditions unchanged)
[1.P.remove.dialog.QUST]         remove_conditions QUST  dialog  <anchor>   PASS (index-0 removed; EventConditions unchanged)
[1.P.remove.event.byfunc.QUST]   remove_conditions QUST  event   <anchor>   PASS (3 conditions matching 'HasPerk' removed; DialogConditions unchanged)
[1.D.01]                         add_conditions QUST  (no condition_target) PASS (missing-target error; rolled back)
[1.D.04]                         add_conditions PERK  condition_target=dialog PASS (reject error per Q4 lock; rolled back)
[2.03]                           add_conditions QUST  dialog  GetIsID+params PASS (v2.9.0 dispatcher composes; Object slot resolved)
[3.1]                            live: dialog HasPerk gating                 PASS (DialogConditions on live QUST; readback OK)
[4.dsl.03]                       add_conditions QUST  condition_target=Dialog PASS (case-insensitive parse per Q5)
[5.range]                        v2.9.0 regression band                      382/382 PASS
```

Failures embed enough context for handoff to lift into the bug list directly. Per-cell PASS/FAIL is the harness contract; round-trip-distinguishability assertions (the "non-targeted list unchanged" check) are inlined in each Layer 1 cell's PASS string.

---

## Skip-with-reason convention

Where vanilla Skyrim.esm doesn't have a QUST record meeting the test fixture requirements (e.g. anchor needs both DialogConditions and EventConditions populated for round-trip-distinguishability and the picked anchor lacks one), the harness prints:

```
[1.P.<op>.<target>.QUST]  <op> QUST <target> <none-meeting-fixture>  SKIP: anchor QUST lacks <target> populated
```

Skips are not failures, but listed in PHASE_2_HANDOFF.md so Aaron can decide whether to manufacture a test fixture (build a synthetic QUST in-memory via Mutagen) or accept the gap. PLAN.md § Phase 1 step 2 expects the probe to identify a vanilla QUST with both lists populated; if no such vanilla QUST exists, Phase 1's handoff names the gap and Phase 2 falls back to a synthetic-QUST fixture pattern.

---

## Phase fill-in checklist (Phase 1 hand-back)

Phase 1 closed (this commit; see PHASE_1_HANDOFF.md) with these MATRIX edits landed:

- [x] **Anchor QUST FormID** — anchored Layer 1.P on `Skyrim.esm:04C49D` (FollowerCommentary01); secondary anchor `Skyrim.esm:0E3145` (CR12) flagged for higher-variance Phase 2 needs. PLAN.md candidate `Skyrim.esm:000242` (MQ101) does NOT exist in vanilla Skyrim.esm; documented in PHASE_1_HANDOFF.md § Deviations from plan.
- [x] **Anchor QUST condition function names** — `1.P.remove.dialog.byfunc.QUST` uses `GetInFaction` (only function in FollowerCommentary01.DialogConditions); `1.P.remove.event.byfunc.QUST` uses `GetEventData` (only function in FollowerCommentary01.EventConditions). Disjoint function distribution → zero ambiguity for byfunc round-trip-distinguishability.
- [x] **Generality scope confirmation** — schema probe found **1 multi-condition record type** in Mutagen.Bethesda.Skyrim 0.53.1: Quest. The other 15 condition-carriers are all single-`Conditions`. Halt threshold (5+) trivially satisfied at 0; QUST-only locked via conductor relay. Layer 1.P stays at 6 cells.
- [x] **Property name mapping table** — confirmed: `IQuestGetter` exposes exactly `DialogConditions: IReadOnlyList<IConditionGetter>` + `EventConditions: IReadOnlyList<IConditionGetter>` (writer side `Quest` exposes `Noggog.ExtendedList<Condition>` for both). Friendly mapping `dialog → DialogConditions` / `event → EventConditions` matches Mutagen 0.53.1 literally; no adjustment needed.

---

## Phase fill-in checklist (Phase 2 hand-back)

Phase 2 closes with these MATRIX edits:

- [ ] **Layer 5 cell count** — confirm the 382 figure against the actual coverage-smoke baseline at start-of-Phase-2 (the 382 is a Phase 0 estimate from v2.9.0 ship; the actual count depends on coverage-smoke's run-time enumeration). Update `5.range` row's count if different.
- [ ] **Layer 4 expectation flips** if Q4 (non-QUST posture) or Q5 (case sensitivity) locks differ from Phase 0 defaults — update `1.D.04` and `4.dsl.03` expected outcomes per the actual lock.
- [ ] **Layer 2.02 response shape** — confirm whether per-op `mods.conditions_added` aggregates (3 total) or surfaces separately (2 + 1) per op. Phase 2 reads the actual response and locks the matrix expectation.
- [ ] **Layer 4.dsl.04 read** — confirm whether `condition_target` supplied alongside an unrelated operator is silently ignored at the op level or rejected. Phase 2 implementation determines; matrix updates accordingly.
- [ ] **Error message wording finalization** — Layer 1.D and Layer 4.dsl rows currently reference the Phase 2-finalized wording placeholders. Replace with the actual strings from PatchEngine.cs once landed.

---

## Phase fill-in checklist (Phase 3 hand-back)

Phase 3 closes with:

- [ ] **Live FormIDs** — replace placeholder FormIDs in Layer 3 scenarios with the FormIDs picked from the live Authoria modlist at execution time.
- [ ] **Per-scenario PASS/FAIL** — annotate each scenario row with the readback evidence + result.
