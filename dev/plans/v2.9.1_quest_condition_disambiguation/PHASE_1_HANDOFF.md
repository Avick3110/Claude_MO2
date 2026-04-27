# Phase 1 Handoff — Schema probe + generality lock (QUST-only confirmed)

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-27
**Session length:** ~1.5h
**Commits made:** `<work-hash>` (work) + this hash-record commit
**Live install synced:** No (Phase 1 is probe-only; live remains at v2.9.0 per `mo2_ping`)

## Working version slug

**`v2.9.1`** — locked at PLAN review (per Phase 0 handoff); Phase 1 doesn't bump constants. Phase 2's first commit bumps version (`config.py` / `.iss` / `README.md`).

## Conductor decisions inherited (Phase 0 → Phase 1, unchanged)

All 5 design questions locked at Phase 0 defaults via conductor relay:

| # | Decision | Lock |
|---|---|---|
| Q1 | `condition_target` placement | **Operator-level** (sibling on `ScopeOps`) |
| Q2 | Parameter naming | **`condition_target`** |
| Q3 | Default on QUST when target omitted | **Error explicitly** |
| Q4 | Non-QUST records receiving `condition_target` | **Reject** (explicit error) |
| Q5 | Case sensitivity for target value | **Case-insensitive** |

No re-litigation in Phase 1. Phase 2 transcribes to `Models.cs` + `PatchEngine.cs`.

## What was done

- **`tools/race-probe/Program.cs`** — extended with v2.9.1 P1 multi-condition record schema sweep section appended after the v2.9.0 P4-INFO regression block (between line 2412 `=== v2.9 P4-INFO probes: ALL PASS ===` and the totalFailures rollup). Section is ~190 LOC + 25 LOC header comment. Sub-sections:
  1. **General sweep** — iterates concrete major-record classes in Mutagen.Bethesda.Skyrim namespace where `SkyrimMajorRecord.IsAssignableFrom(t)`; enumerates public-instance properties whose name `EndsWith("Conditions", OrdinalIgnoreCase)` with `GetIndexParameters().Length == 0`. Prints `[MULTI ]` / `[single]` marker + 4-char record type code (via `<Class>.StaticRegistration.RecordType.Type` reflection — see § Known issues for cosmetic miss) + class name + `I<Class>Getter` interface name + per-property `Name` / `PropertyType.ToString()`.
  2. **QUST negative confirmation** — reflects on `IQuestGetter` directly; enumerates properties matching `*Conditions*` via case-insensitive `IndexOf` (broader than `EndsWith` — catches anomalies like hypothetical `ConditionsExtra`). Asserts exactly 2 properties found, that they're exactly `DialogConditions` + `EventConditions`. Failure increments `p1MultiCondFailures`.
  3. **Nested-condition surfaces** — reflects on `IQuestAliasGetter`, `IQuestStageGetter`, `IQuestObjectiveGetter`, `IQuestLogEntryGetter`, `ISceneActionGetter`, `IPackageProcedureGetter`. Flags any `EndsWith("Conditions")` property as nested-and-out-of-scope-for-v2.9.1. Documented as v2.9.x candidate.
  4. **QUST anchor selection** — loads `Skyrim.esm` from `SkyrimEsmForBatch7` constant; tries MQ101 candidate at `Skyrim.esm:000242`; sweeps all quests for first 10 with both `DialogConditions.Count > 0 && EventConditions.Count > 0`; prints first qualifying anchor's per-list `Data.GetType().Name` distribution + dialog-only / event-only function name sets (forward-look for Phase 2's byfunc cells).
  - Failure counter `p1MultiCondFailures` triggers on (a) QUST not bivariate, (b) zero qualifying quests, (c) Skyrim.esm load throws.
  - Total failures rollup updated (line 2415 + line 2418 breakdown clause) to include `p1MultiCondFailures`.
- **`<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt`** — full probe output captured (1745 lines; new section at lines 1660–1743). Gitignored — not committed. Conductor reads directly.
- **`<plan>/MATRIX.md`** — Phase 1 hand-back checklist edits landed in this commit per the conductor's in-session lock decision (cadence flexibility per PLAN § Phase 1 step 7):
  - Line 7 **Record selection** — replaced MQ101 candidate parenthetical with `Skyrim.esm:04C49D` (FollowerCommentary01) selection + secondary anchor `Skyrim.esm:0E3145` (CR12) flag.
  - Line 11 **Phase fill-in cadence** — Phase 1 status updated to "this commit" with summary of Phase 1's anchor + byfunc + generality-scope decisions.
  - Line 28 **Per-list-target coverage** — flipped from "QUST-anchored" (with extensibility-if-found language) to "QUST-only (Phase 1 confirmed)" with the 15 single-Conditions carriers enumerated and halt threshold trivially satisfied at 0.
  - Line 40 **Carrier convention** — replaced MQ101 candidate language with `Skyrim.esm:04C49D` selection + secondary anchor flag.
  - Layer 1.P 6 cell rows (lines 48–65) — `<QUST-anchor>` placeholder replaced with `Skyrim.esm:04C49D` across all 6 rows; `(Phase 1 picks)` annotation stripped from row 48; `<F-in-dialog>` placeholder replaced with `GetInFaction` (line 64, 2 occurrences); `<F-in-event>` placeholder replaced with `GetEventData` (line 65, 2 occurrences).
  - Phase 1 hand-back checklist (lines 225–232) — all 4 items marked `[x]` with handoff notes per item.
- **`<plan>/PHASE_1_HANDOFF.md`** — NEW (this file).

No production code touched (zero changes to `mutagen-bridge/` or `mo2_mcp/`). No version bump. No `KNOWN_ISSUES.md` / `CHANGELOG.md` updates (Phase 2's responsibility).

## Verification performed

### State checks (session start)

| Check | Result |
|---|---|
| `git -C . log -3 --oneline` top hash | `7f53ba5 [v2.9.1 P0] Handoff: record commit hash 144f021` ✅ matches kickoff prompt's expected hash |
| `git -C . status` | clean working tree ✅ |
| `mo2_ping` | `version: "2.9.0"` ✅ live install at v2.9.0 baseline (untouched) |
| race-probe build (pre-extension, sanity) | 0 warnings, 0 errors ✅ |
| race-probe run (pre-extension) | `=== probe complete ===`; **16/16 PASS** (7 P2A + 3 P2B + 4 P2C + 1 P2D + 1 P4-INFO) ✅ |

### Race-probe build (post-extension)

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.35
```

Race-probe DLL produced at `tools/race-probe/bin/Release/net8.0/race-probe.dll`.

### Race-probe run (post-extension)

`dotnet run -c Release --no-build --project tools/race-probe` → exit 0; full output at `<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt` (1745 lines).

Per-section status (grep on output file):

```
=== v2.9 P2A probes: ALL PASS ===
=== v2.9 P2B probes: ALL PASS ===
=== v2.9 P2C probes: ALL PASS ===
=== v2.9 P2D probes: ALL PASS ===
=== v2.9 P4-INFO probes: ALL PASS ===
=== v2.9.1 P1 multi-condition sweep: ALL PASS ===
=== probe complete ===
```

**16 v2.9.0 baseline probes preserved ALL PASS** + new v2.9.1 P1 sweep ALL PASS (no failure-counter increments).

### Schema finding — general sweep

133 concrete major-record classes scanned in `Mutagen.Bethesda.Skyrim`. **16 carry one or more `*Conditions` property:**

**Multi-condition (≥2 `*Conditions` properties): 1 record type.**

| Record class | Getter interface | Properties |
|---|---|---|
| Quest | IQuestGetter | DialogConditions, EventConditions (both `Noggog.ExtendedList<Condition>`) |

**Single-condition (1 `*Conditions` property): 15 record types.**

CameraPath, ConstructibleObject, DialogResponses, Faction, IdleAnimation, LoadScreen, MagicEffect, MusicTrack, Package, Perk, Scene, SoundDescriptor, StoryManagerBranchNode, StoryManagerEventNode, StoryManagerQuestNode — all expose `Conditions: Noggog.ExtendedList<Condition>` (the v2.7.1+ supported set; bridge's hardcoded `"Conditions"` lookup at PatchEngine.cs:1576 + :2264 already handles them).

**Halt threshold check (CONDUCTOR_KICKOFF.md line 38: 5+ additional multi-condition record types).** Found 0 additional types beyond Quest. Threshold trivially satisfied at 0. No halt fires.

### Schema finding — QUST negative confirmation

PASS — `IQuestGetter` exposes exactly 2 `*Conditions*` properties:

| Property | Type |
|---|---|
| `DialogConditions` | `IReadOnlyList<IConditionGetter>` |
| `EventConditions` | `IReadOnlyList<IConditionGetter>` |

No third top-level `*Conditions*` property. v2.9.1's bivariate dispatch design is correct.

(Writer side `Quest` exposes `Noggog.ExtendedList<Condition>` for both — same property names, different types per the Mutagen Getter/Setter convention.)

### Schema finding — nested-condition surfaces (out-of-scope, v2.9.x candidates)

Scanned 6 candidate interfaces; 2 carry nested `*Conditions` properties:

| Interface | Property | Type |
|---|---|---|
| `IQuestAliasGetter` | `Conditions` | `IReadOnlyList<IConditionGetter>` |
| `IQuestLogEntryGetter` | `Conditions` | `IReadOnlyList<IConditionGetter>` |

`IQuestStageGetter`, `IQuestObjectiveGetter`, `ISceneActionGetter` carry no nested `*Conditions`. `IPackageProcedureGetter` not found in Mutagen 0.53.1 (likely renamed or absent — not load-bearing for v2.9.1 scope).

These nested surfaces are out-of-scope for v2.9.1 — they're conditions on sub-records (alias-level, log-entry-level), not on top-level major-record condition lists. Handling them would require a new `condition_path` parameter (`alias.<index>.Conditions`, `log_entry.<index>.Conditions`) — distinct mechanism from `condition_target`. Documented as v2.9.x candidates per PLAN.md § D.

### QUST anchor selection — vanilla Skyrim.esm

MQ101 candidate at `Skyrim.esm:000242` **does NOT exist** in vanilla Skyrim.esm (see § Deviations).

Sweep yielded 6 qualifying quests (DialogConditions.Count > 0 AND EventConditions.Count > 0):

| FormID | EditorID | Dialog | Event |
|---|---|---:|---:|
| **04C49D** | **FollowerCommentary01** | **1** | **1** |
| 04C6EB | FollowerCommentary02 | 1 | 1 |
| 04C727 | FollowerCommentary03 | 1 | 1 |
| 050CE3 | WIDragonKilled | 20 | 1 |
| 0E3145 | CR12 | 3 | 3 |
| 0E3156 | CR14 | 3 | 2 |

**Selected primary anchor: `Skyrim.esm:04C49D` (FollowerCommentary01).**

Per-list function-name distribution:
- `DialogConditions`: `GetInFactionConditionData` (count=1)
- `EventConditions`: `GetEventDataConditionData` (count=1)

**Disjoint distribution** — `GetInFaction` is dialog-only; `GetEventData` is event-only. Phase 2's `1.P.remove.dialog.byfunc.QUST` cell uses `GetInFaction`; `1.P.remove.event.byfunc.QUST` cell uses `GetEventData`. Round-trip-distinguishability assertion ("matching-function entries in non-targeted list NOT removed") is automatically zero-ambiguity because the byfunc target doesn't exist in the non-targeted list.

**Secondary anchor flag: `Skyrim.esm:0E3145` (CR12)** — Dialog=3, Event=3 — available if Phase 2 needs higher pre-state variance (e.g. for richer remove-by-index variance or to distinguish multiple instances of the same function in one list).

## Bugs surfaced

N/A. Phase 1 is probe-only. No bridge code changes; no functional behavior to surface bugs from. The probe extension itself ran clean (build + run + assertion pass).

## Deviations from plan

1. **PLAN.md § Phase 1 step 2's MQ101 (`Skyrim.esm:000242`) candidate does NOT exist in vanilla Skyrim.esm.** The probe's `srcMod.Quests.FirstOrDefault(q => q.FormKey == new FormKey(Skyrim.esm, 0x000242))` returned null. The MQ101 *INFO* (dialog response) record at `Skyrim.esm:000E3D` exists (used in v2.9.0 P4-INFO regression probe), but that's a distinct record type — INFO carries dialog responses, not the quest itself. Phase 1 selected `Skyrim.esm:04C49D` (FollowerCommentary01) from the sweep instead. PLAN.md substantive scope ("Phase 1 picks anchor from probe") is preserved; the example FormID was wrong but the procedure is intact. PLAN.md does not need editing — the candidate FormID was advisory ("MQ101 ... or alternative"). MATRIX.md updated to reflect the actual selection.

2. **MATRIX.md edits landed this session** rather than deferred to Phase 2's first step (per PLAN § Phase 1 step 7 cadence flexibility). Conductor's in-session sign-off chose this path because the generality lock was clean (data forecloses alternatives) and there was no benefit to deferring. Layer 1.P anchor + byfunc placeholders + generality-scope language + Phase 1 hand-back checklist all landed in this commit.

## Known issues / open questions

1. **Nested-condition surfaces (2 found)** — `IQuestAliasGetter.Conditions` + `IQuestLogEntryGetter.Conditions`. Out-of-scope for v2.9.1 (different mechanism: `condition_path` not `condition_target`). v2.9.x candidates. **Phase 2 owns the KNOWN_ISSUES.md update** documenting these as known capability gaps post-v2.9.1, with the v2.9.x deferral rationale.

2. **`StaticRegistration` reflection lookup didn't resolve** — all 16 entries in the general sweep printed `????` for the 4-char record type code column. Mutagen 0.53.1's static-registration field/property naming is different from what my probe tried (`StaticRegistration` field/property with `Public | Static | NonPublic` BindingFlags). Cosmetic only — the bridge's runtime dispatch uses `record.GetType()` not the 4-char code; the schema finding (count + property names + interfaces) is unaffected. **Phase 2 follow-up if anyone wants a working code lookup; not blocking.** A working approach likely involves Mutagen's `LinkInterfaceMapping` or `LoquiRegistration.Instance.RecordType` indirection — out of scope for Phase 1.

3. **`IPackageProcedureGetter` not found in Mutagen 0.53.1.** The probe's nested-surface candidate list included `IPackageProcedureGetter` based on Skyrim's PACK record's nested procedure structure. Mutagen 0.53.1 either renames the type or absorbs procedures into a different shape (e.g. `IPackageDataGetter`). Cosmetic — printing "interface not found" is informational only. v2.9.1 scope unaffected.

4. **PLAN.md § B's "open question — non-QUST records receiving `condition_target`"** locked at Q4 = reject (Phase 0). No probe-time data needed for this lock; the bridge implementation in Phase 2 enforces it. Phase 1 has no opinion to surface here beyond the lock confirmation.

## Conductor asks

```
CONDUCTOR ASK
Phase: 1
Topic: Generality scope lock — QUST-only vs expand
Context:
  - Schema sweep found 1 multi-condition record type in
    Mutagen.Bethesda.Skyrim 0.53.1: QUST (DialogConditions +
    EventConditions). Zero other candidates.
  - 15 other condition-carrying records are all single-Conditions
    (PERK, PACK, IDLE, MGEF, INFO/DialogResponses, plus 10 others
    — already supported by v2.7.1's hardcoded "Conditions" lookup).
  - Halt threshold (5+ additional types per CONDUCTOR_KICKOFF.md
    line 38) trivially satisfied at 0.
  - Aaron's pre-stated steer ("if easy win opportunities are
    presented we should make a call") doesn't fire — zero
    opportunities surfaced.
Question: Lock generality scope to QUST-only for v2.9.1?
Suggested options:
  A. QUST-only — Phase 0 default holds. Data forecloses
     alternatives. MapConditionTarget handles {dialog →
     DialogConditions, event → EventConditions} and rejects
     others. Layer 1.P stays 6 cells.
  B. Expand — no candidates exist; option presented for
     completeness only.
Default if no response in 24h: A (QUST-only).
```

This ask is informational confirmation rather than a hard binary — the data foreclosed B before the question was posed. Aaron's lock can be a one-line "confirm A" via the conductor relay.

## Preconditions for Phase 2

Phase 2's responsibilities (per PLAN.md § Phase 2):
- Bridge `ApplyAddConditions` / `ApplyRemoveConditions` extension with list-target dispatch (per `condition_target` operator parameter routing reflection lookup to `DialogConditions` / `EventConditions` instead of hardcoded `"Conditions"`).
- `Models.cs` `ScopeOps.ConditionTarget` field (operator-level placement per Q1 lock).
- Race-probe per-list-target functional probes.
- Coverage-smoke +N regression cells (Layer 1.P 6 cells + Layer 1.D 5 cells + Layer 2 3 cells + Layer 4.dsl 4 cells = 18 new cells; Layer 5 regression band = 382 v2.9.0 baseline).
- `tools_patching.py` schema description.
- CHANGELOG / KNOWN_ISSUES updates.
- **Version bump to v2.9.1 (Phase 2's first commit).**

| Precondition | State |
|---|---|
| Generality scope locked (QUST-only confirmed by data) | ⏳ pending Aaron lock via conductor relay; default-if-no-response = QUST-only |
| QUST anchor FormID selected | ✅ `Skyrim.esm:04C49D` (FollowerCommentary01); secondary `Skyrim.esm:0E3145` (CR12) flagged |
| Property name mapping confirmed | ✅ `dialog → DialogConditions`, `event → EventConditions` matches Mutagen 0.53.1 literal |
| Byfunc cell function names selected | ✅ `GetInFaction` (dialog), `GetEventData` (event) — disjoint distribution |
| MATRIX.md placeholders resolved | ✅ landed in this commit (Phase 1 hand-back checklist 4/4 `[x]`) |
| Bridge code editable + builds clean as-is | ✅ presumed — Phase 1 didn't touch bridge code; v2.9.0 ship at `f7a8e5d` is the bridge baseline; race-probe uses `mutagen-bridge.exe` from v2.9.0 build for P4-INFO regression and that ran clean |
| `tools/race-probe/Program.cs` extended for v2.9.1 P1 schema sweep | ✅ landed in this commit; 16 v2.9.0 baseline probes preserved ALL PASS; new sweep ALL PASS |
| 5 design questions locked (Q1–Q5) | ✅ Phase 0 defaults locked via conductor relay |
| Phase 2 race-probe extension model | ✅ Phase 1's sweep section + the 4-section pattern (general / negative / nested / anchor) is the template; Phase 2 adds bridge-subprocess functional probes per-list-target (mirrors v2.9.0 P4-INFO pattern) |

**Phase 2 cannot open until Aaron locks generality scope** (Conductor ask above). The lock is informational confirmation; default holds at QUST-only. If Aaron is silent for 24h, conductor proceeds with Phase 2 kickoff under the default.

## Files of interest for Phase 2

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PLAN.md` § Phase 2 | Authoritative steps + § Conductor decisions for Phase 2 |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/MATRIX.md` § Phase fill-in checklist (Phase 2 hand-back) | Exact rows Phase 2 fills (Layer 5 cell count, Layer 4 expectation flips per Q4/Q5 lock outcomes, Layer 2.02 response shape, Layer 4.dsl.04 read, error-message wording finalization) |
| `Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_1_HANDOFF.md` (this file) | Phase 1's findings + generality lock + QUST anchor decisions |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs:1573` (`ApplyAddConditions`) + `:2262` (`ApplyRemoveConditions`) | Reflection lookup call sites Phase 2 extends — replace hardcoded `"Conditions"` with target-name → property-name dispatch |
| `Claude_MO2/tools/mutagen-bridge/Models.cs` | Add `ConditionTarget` field on `ScopeOps` (operator-level per Q1 lock); JSON serialization attributes per existing pattern |
| `Claude_MO2/tools/race-probe/Program.cs` | Phase 2 adds bridge-subprocess functional probes per-list-target after the v2.9.1 P1 sweep section. Pattern: mirror v2.9.0 P4-INFO probe shape (bridge subprocess → patch QUST `Skyrim.esm:04C49D` with `condition_target: "dialog"` add, then with `"event"` add — assert per-list readback). |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | Phase 2 adds Layer 1.P 6 cells + Layer 1.D 5 cells + Layer 2 3 cells + Layer 4.dsl 4 cells. Anchor on FollowerCommentary01 (`Skyrim.esm:04C49D`); use `GetInFaction` / `GetEventData` for byfunc cells. |
| `Claude_MO2/dev/plans/v2.9.X_condition_parameters/PHASE_2C_HANDOFF.md` | v2.9.0 reference for "schema-probe + drift-detection + bridge SHA capture" pattern; Phase 2's bridge build SHA capture mirrors P2C's pattern |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` v2.9.0 entry + `Claude_MO2/KNOWN_ISSUES.md` § Patching write surface | Phase 2 lifts the QUST condition disambiguation entry from gap-list to covered; appends new top-level CHANGELOG entry for v2.9.1; bumps `KNOWN_ISSUES.md` to remove the gap |
| `Claude_MO2/installer/config.py` + `installer/Claude_MO2_Setup.iss` + `Claude_MO2/README.md` | Phase 2's first commit bumps version constants from 2.9.0 → 2.9.1 |
| `<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt` | Full probe output for reference; gitignored; conductor reads directly |

## Acceptance — Phase 1 (per kickoff)

- ✅ Schema probe runs to completion; output captured to `<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt` (1745 lines).
- ✅ Probe extension preserves v2.9.0's existing 16 probes ALL PASS (7 P2A + 3 P2B + 4 P2C + 1 P2D + 1 P4-INFO).
- ✅ `PHASE_1_HANDOFF.md` captures: per-record-type `*Conditions` property list (16 carriers, 1 multi-type Quest); QUST anchor confirmation (`Skyrim.esm:04C49D` FollowerCommentary01, Dialog=1 + Event=1); negative confirmation (no third top-level QUST condition list); flag of nested-conditions surfaces (2: alias-level + log-entry-level); generality proposal in § Conductor asks.
- ✅ Race-probe builds clean (0 warnings, 0 errors); race-probe DLL at `tools/race-probe/bin/Release/net8.0/race-probe.dll`.
- ✅ MATRIX.md updated this session per conductor in-session lock decision (Phase 1 hand-back checklist all `[x]`); placeholder substitutions complete.
- ✅ Handoff under 400 lines (this file).
