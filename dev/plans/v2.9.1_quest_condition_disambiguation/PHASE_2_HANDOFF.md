# Phase 2 Handoff — Quest condition disambiguation (bridge + tests + docs + version)

**Phase:** 2
**Status:** Complete
**Date:** 2026-04-27
**Session length:** ~3h
**Commits made:** `<work-hash>` (work) + `<this-commit>` (hash-record)
**Live install synced:** No (Phase 2 doesn't touch live; live remains at v2.9.0 per `mo2_ping`)

## Working version slug

**`v2.9.1`** — bumped this phase per PLAN § Phase 2 step 13. Constants: `mo2_mcp/config.py:9`, `installer/claude-mo2-installer.iss:21`, `README.md:7+59`.

## Conductor decisions inherited

All Phase 0 + Phase 1 locks honored verbatim:

| # | Decision | Lock |
|---|---|---|
| Q1 | `condition_target` placement | Operator-level (`RecordOperation.ConditionTarget`) |
| Q2 | Parameter naming | `condition_target` |
| Q3 | QUST without target | Error explicitly (pre-flight throw before lookup) |
| Q4 | Non-QUST + condition_target | Reject (PERK-style with Q4 sentinel) — but only if record has Conditions; ARMO-style (no Conditions) → Tier D fallthrough |
| Q5 | Case sensitivity for target value | Case-insensitive (`StringComparer.OrdinalIgnoreCase`) |
| — | Generality scope | QUST-only (Phase 1 schema probe — sole multi-condition record type in Mutagen 0.53.1) |
| — | QUST anchor | `Skyrim.esm:04C49D` (FollowerCommentary01) |
| — | byfunc anchors | `GetInFaction` (dialog), `GetEventData` (event) |
| — | Property names | `DialogConditions` + `EventConditions` |

The Q4 refinement (PERK reject vs ARMO Tier D fallthrough) was added by conductor mid-Phase A — implemented via defensive `GetProperty("Conditions")` probe on targeted-lookup miss in `ResolveConditionListProperty`.

## What was done

### Bridge — list-target dispatcher (`PatchEngine.cs`, `Models.cs`)

- **`Models.cs`** — added `RecordOperation.ConditionTarget` field (operator-level per Q1):
  ```csharp
  [JsonPropertyName("condition_target")]
  public string? ConditionTarget { get; set; }
  ```
  Inserted after `RemoveConditions` field at `Models.cs:430`. xmldoc cites valid values + KNOWN_ISSUES.md reference.

- **`PatchEngine.cs`** — new private static dispatcher helper + friendly-name table, colocated above `ApplyAddConditions`:
  - **Friendly-name table** as `Dictionary<string, string>` keyed by `OrdinalIgnoreCase`: `["dialog"] = "DialogConditions", ["event"] = "EventConditions"`. Phase 1-confirmed property names transcribed verbatim.
  - **Helper signature**: `private static PropertyInfo? ResolveConditionListProperty(IMajorRecord record, string? conditionTarget, string operatorName)`.
  - **Error paths** (in dispatch order):
    1. **Q3** (QUST + null target): pre-flight throw `ArgumentException` with full `"requires a condition_target parameter on {operatorName}"` message.
    2. Legacy path (null target, non-QUST): return `record.GetType().GetProperty("Conditions")` — back-compat for v2.9.0 single-Conditions carriers.
    3. **§C#3** (bad target value): throw `"Unknown condition_target: '{value}'. Valid values: 'dialog' | 'event'."`
    4. Targeted lookup: return `GetProperty(propName)` if found.
    5. **Q4 distinction**: if targeted lookup misses, probe `GetProperty("Conditions")`:
       - **Has Conditions (PERK/PACK/IDLE/MGEF/INFO)** → throw `"Record type {Name} uses a single Conditions list — omit condition_target. (condition_target='{value}' resolved to {propName}, which this record does not expose.)"`.
       - **No Conditions (ARMO)** → return null → existing Tier D `unmatched_operators` shape fires.

- **Wired into `ApplyAddConditions` (PatchEngine.cs:1573)** + **`ApplyRemoveConditions` (PatchEngine.cs:~2348 post-helper-insert)**: signatures gained `string? conditionTarget` parameter; reflection lookup replaced with `ResolveConditionListProperty(record, conditionTarget, "add_conditions"/"remove_conditions")`. Call sites at PatchEngine.cs:884 + :890 thread `op.ConditionTarget` through.

### Test infrastructure — race-probe (`tools/race-probe/Program.cs`)

8 new probes anchored on QUST `Skyrim.esm:04C49D` (FollowerCommentary01); Hadvar `Skyrim.esm:02BF9F` reused as Object slot from v2.9.0 P4-INFO (vanilla-confirmed). Section banner: `=== v2.9.1 P2 — Quest condition disambiguation (bridge subprocess) ===`. New failure counter `p2QustFailures` threaded into `totalFailures` rollup.

| # | Probe | Verdict | Evidence |
|---|---|---|---|
| 1 | add condition_target=dialog (GetIsID(Object=Hadvar)) | PASS | Dialog=2 (+1), Event=1 unchanged |
| 2 | add condition_target=event (GetIsID(Object=Hadvar)) | PASS | Dialog=1 unchanged, Event=2 (+1) |
| 3 | remove byfunc dialog (GetInFaction) | PASS | Dialog=0 (removed), Event=1 unchanged |
| 4 | remove byfunc event (GetEventData) | PASS | Dialog=1 unchanged, Event=0 (removed) |
| 5 | Q3 error (QUST without target) | PASS | sentinel `requires a condition_target parameter` + `Quest` matched |
| 6 | §C#3 error (bad value `"story"`) | PASS | sentinel `Unknown condition_target: 'story'` matched |
| 7 | Q4 reject (PERK + dialog) | PASS | dynamically-resolved PERK `Skyrim.esm:01711E`; sentinel `uses a single Conditions list` + `omit condition_target` matched |
| 8 | Q5 case-insensitivity (`"Dialog"` TitleCase) | PASS | TitleCase routes to DialogConditions correctly |

Full output: `<workspace>/scratch/v2.9.1-phase-2-smoke-output.txt`.

### Test infrastructure — coverage-smoke (`tools/coverage-smoke/Program.cs`)

18 new cells (Tests 383-400) per MATRIX § Layer 1.P / 1.D / 2 / 4.dsl:

| MATRIX cell | Test # | Verdict |
|---|---|---|
| 1.P.add.dialog.QUST | 383 | PASS |
| 1.P.add.event.QUST | 384 | PASS |
| 1.P.remove.dialog.QUST | 385 | PASS |
| 1.P.remove.event.QUST | 386 | PASS |
| 1.P.remove.dialog.byfunc.QUST | 387 | PASS |
| 1.P.remove.event.byfunc.QUST | 388 | PASS |
| 1.D.01 (Q3 add) | 389 | PASS |
| 1.D.02 (Q3 remove) | 390 | PASS |
| 1.D.03 (bad value) | 391 | PASS |
| 1.D.04 (PERK Q4 reject) | 392 | PASS |
| 1.D.05 (ARMO Tier D) | 393 | PASS |
| 2.01 (multi-condition single op) | 394 | PASS |
| 2.02 (two ops opposing targets) | 395 | PASS |
| 2.03 (v2.9.1 × v2.9.0 composition) | 396 | PASS |
| 4.dsl.01 (empty string) | 397 | PASS |
| 4.dsl.02 (JSON null) | 398 | PASS |
| 4.dsl.03 (case-insensitive) | 399 | PASS |
| 4.dsl.04 (orthogonal w/ add_keywords) | 400 | PASS |

**Final tally: 400 cells (382 v2.9.0 + 1 v2.9.0-flipped Test 157 + 18 v2.9.1) = ALL PASS.** `=== smoke complete: ALL PASS ===` + exit 0. Output captured at `<workspace>/scratch/v2.9.1-phase-2-coverage.txt` (~159 KB, ~4700 lines).

### Schema description (`mo2_mcp/tools_patching.py`)

- Removed v2.9.0 caveat from `add_conditions` description: `"... QUST records use DialogConditions/EventConditions which require a parameter not yet exposed (see KNOWN_ISSUES)."`
- Updated `add_conditions` + `remove_conditions` descriptions to reference QUST coverage via the v2.9.1 `condition_target` parameter.
- Added new operator-level `condition_target` schema entry (sibling to add/remove_conditions) — full description covering valid values, case-insensitivity, error paths (Q3/Q4/Tier D), generality scope (QUST-only), nested-conditions deferral.

### `KNOWN_ISSUES.md`

- **Removed** old gap-list entry: `**Quest condition disambiguation.** QUST records carry DialogConditions and EventConditions ...`
- **Added** new gap-list entry (v2.9.x deferral): `**Multi-condition record types beyond QUST top-level (deferred to v2.9.x).** ...IQuestAliasGetter.Conditions + IQuestLogEntryGetter.Conditions ...requires extension of v2.9.1's condition_target mapping table to address nested-major sub-records, similar to v2.9.0's INFO override pattern...`
- **Added** new subsection `### Covered as of v2.9.1` with single bullet documenting the Quest disambiguation coverage + cross-reference to the deferred nested-conditions entry.

### `CHANGELOG.md`

Inserted new top-level `## v2.9.1 — TBD` entry (~85 lines) between Unreleased and v2.9.0 per PLAN § Phase 2 step 12 template:
- Mandate + composition framing
- Added — bridge: full Q3/Q4/§C#3/Tier D error path enumeration; QUST-only generality lock; nested-conditions deferral; Test 157 contract flip noted
- Changed — schema: condition_target parameter added; v2.9.0 caveat removed
- Test infrastructure: race-probe 8 probes + coverage-smoke 18 cells
- Documentation: tools_patching.py + KNOWN_ISSUES.md changes summarized

Ship date `TBD` — Phase 5 fills.

### Version bump (4 files)

| File | Change |
|---|---|
| `mo2_mcp/config.py:9` | `PLUGIN_VERSION = (2, 9, 0)` → `(2, 9, 1)` |
| `installer/claude-mo2-installer.iss:21` | `#define AppVersion "2.9.0"` → `"2.9.1"` |
| `README.md:7` | `claude-mo2-setup-v2.9.0.exe` ×2 (markdown link) → ...v2.9.1.exe |
| `README.md:59` | `claude-mo2-setup-v2.9.0.exe` → ...v2.9.1.exe |

## Verification performed

### State checks (session start)

| Check | Result |
|---|---|
| `git log -3 --oneline` top hash | `18ad5f4 [v2.9.1 P1] Handoff: record commit hash 8a7fb9a` ✅ |
| `git status` | clean ✅ |
| `mo2_ping` | `version: "2.9.0"` ✅ (live untouched) |
| mutagen-bridge build (Release, baseline) | 0 warnings, 0 errors ✅ |
| race-probe build + run (Release, baseline) | 0/0; all 16 v2.9.0 + new v2.9.1 P1 sweep ALL PASS ✅ |

### Post-Phase-A bridge build

`mutagen-bridge -> bin/Release/net8.0/mutagen-bridge.dll`. 0 warnings, 0 errors. Re-built defensively after each subsequent phase — clean every time.

### Post-Phase-B/C race-probe run

```
=== v2.9 P2A probes: ALL PASS ===
=== v2.9 P2B probes: ALL PASS ===
=== v2.9 P2C probes: ALL PASS ===
=== v2.9 P2D probes: ALL PASS ===
=== v2.9 P4-INFO probes: ALL PASS ===
=== v2.9.1 P1 multi-condition sweep: ALL PASS ===
=== v2.9.1 P2 quest-condition probes: ALL PASS ===
=== probe complete ===
```

`p2QustFailures = 0`. `totalFailures = 0`. Exit 0.

### Post-Phase-D coverage-smoke run

```
=== smoke complete: ALL PASS ===
```

400 cells total (382 v2.9.0 + 18 v2.9.1; Test 157 flip in v2.9.0 baseline preserves count). 0 FAIL. 6 SKIP-with-reason (pre-existing; ESL deferred to live, Mutagen schema gaps documented in KNOWN_ISSUES). Exit 0.

## Bugs surfaced (Phase 2 in-phase fixes)

### Test 157 [4.c.01-carry] — v2.9.0 contract flip

Pre-existing v2.7.1+ cell asserting `QUST + add_conditions` returns Tier D `unmatched_operators=["add_conditions"]`. With v2.9.1, QUST + add_conditions WITHOUT `condition_target` now fires the new Q3 explicit error first (pre-flight check in `ResolveConditionListProperty`).

**Resolution: in-place flip per conductor sign-off (Path A).** Updated assertion to match new Q3 sentinel (`requires a condition_target parameter` + `Quest`). Cell ID `4.c.01-carry` preserved as the historical anchor for the now-covered (QUST, add_conditions) pair, with comment cross-reference to MATRIX 1.D.01 (Test 389) as the canonical v2.9.1 home. This is contract-flipped behavior per PLAN § Phase 2 step 11, not a regression — Test 157's expectation was the codification of "QUST is in the gap-list, fires Tier D"; that posture flipped with v2.9.1's coverage.

### Test 400 [4.dsl.04] — fixture correction

Initial draft used FACT (Faction) as the carrier for `add_keywords + condition_target`. FACT does NOT support `add_keywords` — bridge returned `unmatched_operators: ["add_keywords"]` because FACT's Keywords property doesn't exist; the cell fired Tier D for the wrong reason.

**Resolution: swapped carrier to RACE per conductor sign-off.** RACE supports `add_keywords` (verified via Test 7 [1.A.01]) and has no `Conditions` property at all (Phase 1 sweep confirmed RACE is not in the 16-carrier set). On the corrected cell, `add_keywords` succeeds, and `condition_target='dialog'` is silently ignored at the op level when no add/remove_conditions sub-op is present — exactly the orthogonal-field-ignore semantics MATRIX 4.dsl.04 was meant to verify.

## Deviations from plan

1. **PLAN.md operator-level field placement named `ScopeOps.ConditionTarget`; actual class is `RecordOperation`.** PLAN.md § B (line 139) and PLAN.md § Phase 2 step 3 (line 440-454) both name `ScopeOps` as the placement class. The actual operator-level class in `Models.cs:342` is `RecordOperation` — `ScopeOps` does not exist as a type. Same conceptual layer (sibling to `AddConditions`/`RemoveConditions`/`SetFields`/etc.). Field added on `RecordOperation`. Identified and confirmed at HALT 1; conductor sign-off acknowledged this as a historical artifact ("pre-Phase-0 conceptual naming"). Similar in spirit to the MQ101 PLAN correction Phase 1 surfaced.

2. **Q4 dispatch logic refined mid-Phase-A by conductor.** Initial draft proposed: "non-QUST + `condition_target` → reject" uniformly. Conductor's HALT 1 sign-off refined to distinguish:
   - **Single-Conditions carriers (PERK/PACK/IDLE/MGEF/INFO)** → Q4 reject (HAS Conditions, wrong target).
   - **No-Conditions carriers (ARMO)** → return null → Tier D fallthrough (preserves v2.9.0 behavior bit-identically; the fundamental issue is "ARMO doesn't support add_conditions" not "wrong target").

   Implementation uses defensive `GetProperty("Conditions")` probe on targeted-lookup miss to distinguish the two cases. MATRIX 1.D.04 vs 1.D.05 separation preserved; coverage-smoke Tests 392 + 393 verify both shapes.

3. **Inline smoke folded into race-probe rather than written as a separate one-shot harness.** Phase B's two positive-add probes (Tests 1+2 in the v2.9.1 P2 section) are the inline smoke anchor; Phase C extended with probes 3-8. Conductor approved at HALT 2.

4. **Probe 8 repurposed from "v2.9.0 dispatcher composition" to "Q5 case-insensitivity"** per HALT 3 sign-off. Composition is exercised in probes 1-4 (which all pass `parameters: {Object: hadvarFkStr}` for GetIsID); a dedicated probe 8 for composition was redundant. Repurposed to verify Q5 lock end-to-end (TitleCase `"Dialog"` parsed through `StringComparer.OrdinalIgnoreCase` friendly-name table).

## Known issues / open questions

1. **`StaticRegistration` cosmetic miss** (Phase 1 informational item #3). Phase 1's race-probe `TryRecordTypeCode` helper printed "????" for all 16 multi-condition-sweep entries; Mutagen 0.53.1's static-registration field/property naming differs from the probe's reflection lookup. Not blocking — bridge runtime dispatch uses `record.GetType()`, not the 4-char code. v2.9.x cosmetic follow-up; no Phase 2 code change needed.

2. **Layer 3 (live workflow scenarios)** is Phase 3's deliverable — Phase 2 only laid down the test fixtures (anchor record + byfunc anchors) Phase 3 will exercise.

## Conductor asks

```
CONDUCTOR ASK
Phase: 2
Topic: Live install sync timing
Context:
  - Live install at v2.9.0 (mo2_ping returns 2.9.0).
  - Phase 3 reads via mo2_create_patch against the live install
    (per PLAN.md § Phase 3 step 1: "Verify mo2_ping returns
    version: '2.9.1'. If disconnected or wrong version: halt
    and ask conductor.").
  - Live install needs sync to v2.9.1 BEFORE Phase 3 can begin
    (otherwise Phase 3's pre-flight check will halt).
Question: When does conductor expect Phase 3's pre-Phase-3 live
sync to happen? Options:
  A. Conductor explicitly arranges the sync before writing
     Phase 3 kickoff (PLAN.md § E names live-sync as conductor
     responsibility for Phase 3/4/5).
  B. Phase 3 executor runs the sync as their first step before
     the workflow scenarios.
Default if no response: A — conductor arranges sync, names it
in Phase 3 kickoff.
```

## Preconditions for Phase 3

| Precondition | State |
|---|---|
| Bridge dispatcher landed (`condition_target` operator parameter) | ✅ Phase 2 |
| Coverage-smoke at 400/400 PASS | ✅ Phase 2 |
| Live install at v2.9.1 | ⏳ **NOT MET** — live still at v2.9.0; conductor needs to sync before Phase 3 (see Conductor ask above) |
| MATRIX § Layer 3 scenarios specified | ✅ Phase 0 + 1 (3.1 dialog HasPerk gating, 3.2 event Story Manager gating) |
| QUST anchor + Object slot fixtures confirmed | ✅ Phase 1 (FollowerCommentary01) + Phase 2 race-probe (Hadvar) |
| Phase 2 doesn't touch live | ✅ confirmed (live remains at v2.9.0 baseline) |

## Files of interest for Phase 3

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PLAN.md` § Phase 3 (lines 572-617) | Authoritative Phase 3 step list |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/MATRIX.md` § Layer 3 (lines 97-141) | Scenario specifications (3.1 dialog + 3.2 event); Phase 3 picks live FormIDs at execution time |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_2_HANDOFF.md` (this file) | Phase 2 implementation reference; sentinels for matching live-bridge errors |
| Live install at `<live>/` | Phase 3 executes against the Authoria modlist via `mo2_create_patch`; output to `<modlist>/mods/Claude Output/v2.9.1-scenario-N.esp` (deleted post-verification) |
| `<workspace>/scratch/v2.9.1-phase-2-coverage.txt` | Phase 2 smoke output; reference for expected response shapes |
| `<workspace>/scratch/v2.9.1-phase-2-smoke-output.txt` | Phase 2 race-probe output; reference for bridge subprocess invocation pattern |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs:~1573` (ApplyAddConditions) + `:~2348` (ApplyRemoveConditions) + `ResolveConditionListProperty` helper above | Bridge dispatch source — Phase 3 doesn't modify, but useful for sentinel-text reference |

## Acceptance — Phase 2 (per kickoff)

- ✅ Phase 1-confirmed property names (`DialogConditions` + `EventConditions`) transcribed verbatim into `ResolveConditionListProperty`'s friendly-name table; no speculation.
- ✅ Bridge builds clean (0 warnings, 0 errors).
- ✅ Inline smoke + per-list-target functional probes pass via Mutagen-direct readback (8 probes total).
- ✅ Coverage-smoke runs to total (382 v2.9.0 + 1 v2.9.0-flipped + 18 v2.9.1 = 400 cells), all PASS or documented SKIP. **All 382 v2.9.0 cells stay green** (Test 157 contract-flipped per v2.9.1 contract; not regression).
- ✅ Version bumped in all four version-bearing files (config.py + .iss + README.md × 2 occurrences via 3 lines).
- ✅ Schema description in `tools_patching.py` reflects new `condition_target` parameter; v2.9.0 caveat removed.
- ✅ `KNOWN_ISSUES.md` updated (Quest disambiguation moved to "Covered as of v2.9.1" subsection; new gap entry for nested-conditions surfaces).
- ✅ CHANGELOG entry `## v2.9.1 — TBD` inserted with full mechanism documentation.
- ✅ Handoff under 400 lines (this file).
