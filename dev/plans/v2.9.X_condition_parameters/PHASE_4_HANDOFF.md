# Phase 4 Handoff — Line-180 error-message DX bonus-catch landed; INFO override deferred to Phase 4-INFO sub-session (architectural mismatch)

**Phase:** 4
**Status:** Partial — Item 2 (line-180 DX bonus-catch) landed clean; Item 1 (INFO override fix) deferred to a Phase 4-INFO sub-session after pre-flight reflection refuted Phase 3's recommended fix shape mid-session. Aaron's call (2026-04-27, mid-Phase-4): land Item 2 in this session, spawn a separate sub-session for INFO override implementation.
**Date:** 2026-04-27
**Session length:** ~2h
**Commits made:** TBD — pending push (Phase 4 double-commit cadence: work + hash-record).
**Live install synced:** No — Phase 4 is repo-side only per kickoff conductor decision #6. Live install stays at P2D bridge SHA `2e3a1094…f8293975e` until Phase 5 owns the canonical sync.

## What was done

### Item 2 — `override` failure error message names the 4-char record type code (DX bonus-catch)

`tools/mutagen-bridge/PatchEngine.cs` — two narrowly-scoped hunks:

1. **Line 180 throw site.** Replaced `sourceRecord.GetType().Name` (which renders runtime-overlay type names like `DialogResponsesBinaryOverlay` — leaking Mutagen's internal class naming through a user-facing error) with `RecordTypeCode(sourceRecord)` (the same helper the success path already uses for `detail.RecordType` at line 185). User-facing error now reads `Could not create override for INFO` (or `ARMO`, `RACE`, etc.) for any record type missing from the `CopyAsOverride` switch.
2. **Line 2479 `RecordTypeCode` switch.** Added `IDialogResponsesGetter => "INFO"` case for symmetry with `RecordReader.cs:365` (which already had this mapping). Without the explicit case, the fallback `record.Registration.ClassType.Name.ToUpperInvariant()` would yield `"DIALOGRESPONSES"` — better than the leaky overlay name but still off-spec. The dedicated case makes Phase 5's eventual ship error messages match Bethesda's 4-char convention exactly.

### Item 1a — INFO override race-probe (probe-first; restructured to architectural archaeology)

`tools/race-probe/Program.cs` — new `v2.9 P4 — INFO override architectural archaeology (deferred)` section (~95 lines, between P2D close and the totalFailures rollup line). Originally written as a probe-first FAIL→PASS lift per kickoff. The pre-flight reflection check refuted Phase 3's recommended fix shape immediately (HALT 1 trigger #5: assumption mismatch on `SkyrimMod.DialogResponses`), so the section was restructured to:

- Print the architectural finding cleanly (no failure recorded — DEFERRED status, not FAIL).
- Dump alternative override surfaces (SkyrimMod Dialog/Info/Response properties; DialogTopic.Responses methods; static `*MixIn` Duplicate/DeepCopy helpers) for the Phase 4-INFO sub-session executor to evaluate.
- Print the likely sub-session implementation shape (parent-topic resolution + override + child-response find-by-FormKey).
- Exit clean (race-probe total exit code 0, all phases stay green).

The architectural archaeology persists across sessions; the Phase 4-INFO sub-session reads its diagnostic output as the bootstrap for picking the actual fix shape.

### Item 1b/1c/1d/1e — INFO override switch extensions + regression

**Not landed in this session.** Deferred to Phase 4-INFO sub-session per Aaron's Option C call. See § Phase 4-INFO sub-session preconditions below.

### Out-of-scope items (not absorbed)

- v2.7.1 / v2.8.0 carry-overs (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access) — explicitly deferred per kickoff conductor decision #4.
- Boolean dispatcher branch — deferred to v2.9.x first-consumer trigger.
- Sub-B 6 String-slot Condition functions — deferred to v2.9.x.
- Live install sync — Phase 5 owns the canonical sync.
- Layer 3 scenarios re-run — Phase 5 territory.

### Docs hygiene

- **`mo2_mcp/CHANGELOG.md`** — `## v2.9.0 — TBD` entry gained two updates:
  - New `### Changed — bridge` section under v2.9.0 with the line-180 DX bonus-catch entry. Documents the swap from `sourceRecord.GetType().Name` to `RecordTypeCode(sourceRecord)` + the `IDialogResponsesGetter => "INFO"` case addition + the per-record-type-agnostic posture (any record type missing from the switch surfaces a clean diagnostic going forward).
  - New first bullet under `### Out of scope (v2.9.x candidates within release line)`: INFO override deferral with the full architectural finding (SkyrimMod has no DialogResponses property; INFO nested under DialogTopic.Responses; Mutagen 0.53.1 has no override-add helper) + sub-session pointer + line-180 DX context.
- **`KNOWN_ISSUES.md`** — `## Patching write surface — current limitations` section gained a new INFO-override-not-yet-wired bullet alongside the existing Outfit/Spell attach_scripts gap. Explains the Mutagen 0.53.1 architectural finding, the deferred Phase 4-INFO sub-session, the line-180 DX clean-error result, and that read-path INFO operations (`mo2_record_detail` etc.) are unaffected.

## Verification performed

### Bridge build

`cd tools/mutagen-bridge && dotnet build -c Release` — 0 warnings, 0 errors.

**New bridge SHA:** `a69179b30217746e29ab727ac8484a242c72aba29f7ee38f3846b025653972a7`
(at `tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`). Differs from P2D's `2e3a1094…f8293975e` byte-for-byte ✓ — expected, since `PatchEngine.cs` changed at lines 180 + 2479.

### Drift-detection diff (`git diff def8fa84 -- tools/mutagen-bridge/PatchEngine.cs`)

```
@@ -177,7 +177,7 @@ public class PatchEngine
         var overrideRecord = CopyAsOverride(patchMod, sourceRecord);
         if (overrideRecord == null)
             throw new InvalidOperationException(
-                $"Could not create override for {sourceRecord.GetType().Name}");
+                $"Could not create override for {RecordTypeCode(sourceRecord)}");

@@ -2479,6 +2479,11 @@ public class PatchEngine
         IFormListGetter => "FLST", IMagicEffectGetter => "MGEF",
         IContainerGetter => "CONT", IPackageGetter => "PACK",
         IFurnitureGetter => "FURN", IActivatorGetter => "ACTI", ILocationGetter => "LCTN",
+        // INFO records — the Mutagen class name (DialogResponses) doesn't match
+        // Bethesda's 4-char code. Without this case the fallback yields
+        // "DIALOGRESPONSES" — symmetry with RecordReader.cs:365 where the same
+        // mapping already lives.
+        IDialogResponsesGetter => "INFO",
         _ => record.Registration.ClassType.Name.ToUpperInvariant(),
     };
```

Two hunks only. No drift outside the named lines. Mirrors v2.8.0 P4's narrow-scope discipline.

### Race-probe

`cd tools/race-probe && dotnet build -c Release && dotnet run -c Release --no-build --project tools/race-probe` — exit **0**, `=== probe complete ===`. All prior-phase scoreboards stay green (0 audit failures across P0/v2.7.1, v2.8 P1, v2.9 P1/P2A/P2B/P2C/P2D); P4 archaeology section reports `DEFERRED — INFO override deferred to Phase 4-INFO sub-session; Item 2 (line-180 DX) lands this session`. Diagnostic dump captured in the probe output (full transcript at `<workspace>/scratch/v2.9-phase-4-probe.txt` if archived; else reproducible by re-running the probe — the architectural reflection is deterministic).

### Coverage-smoke

`dotnet run -c Release --no-build --project tools/coverage-smoke` — exit **0**, `=== smoke complete: ALL PASS ===`. Total cell count and SKIP set unchanged from Phase 2/3 baseline:

| Bucket | Count | Notes |
|---|---:|---|
| Total cells | 382 | No new cells in Phase 4 (Item 2 has no positive cell — error-message DX is exercised via the existing CELL/INFO Tier-D-edge paths; no record type currently fails CopyAsOverride besides INFO + CellBinaryOverlay, and both already have SKIP-with-reason cells documenting the limitation). |
| 0 FAIL | ✓ | All 376 PASS cells stay green; no regression. |
| 6 SKIP unchanged | ✓ | Same set as Phase 2 close: 1.r.40 (OTFT), 1.r.47 (SPEL), 1.D.04 (CELL), 4.esl.01 (Layer 4 ESL), 1.P.Unknown.MGEF, 1.P.GetVATSValueUnknown.MGEF. None added, none lifted. |

### Pre-existing warnings

Coverage-smoke build emits 2 CS8602 warnings at `Program.cs:3932` — pre-existing per v2.8.0 P4 handoff § Known issues; not introduced by Phase 4; not absorbed (kickoff scope-locked at items 1+2; pre-existing warnings stay deferred per the same discipline that v2.8.0 P4 used).

## Bugs surfaced

**Zero new bridge bugs.** Phase 4's only mid-session diagnostic loop was the architectural finding around Phase 3's recommended fix shape — handled cleanly via probe-first discipline (HALT 1 → Aaron's Option C call → restructure the probe to archaeology → land Item 2). The architectural finding itself is a Mutagen 0.53.1 schema observation, not a bridge bug.

## Findings

### Mutagen 0.53.1 has no top-level INFO override surface (load-bearing)

`typeof(SkyrimMod).GetProperty("DialogResponses")` returns null. Mutagen 0.53.1 exposes:

- `SkyrimMod.DialogTopics: SkyrimGroup<DialogTopic>` — the parent-record group.
- `DialogTopic.Responses: Noggog.ExtendedList<DialogResponses>` — a plain extended list with `Add(DialogResponses)` + `Insert(int, DialogResponses)` — no `GetOrAddAsOverride` or any override-add helper.
- `DialogResponsesMixIn` static class — provides `Duplicate`, `DeepCopy`, `Print`, `Equals`, `Clear`, `CopyInFromBinary` for `IDialogResponsesGetter` / `DialogResponses`. **No** override-add helper.

**Implication.** INFO override in Mutagen requires parent-topic `GetOrAddAsOverride(parent)` + mutating the matching `DialogResponses` instance inside the override topic's `Responses` list. The bridge's existing 1:1 record→group `CopyAsOverride` switch model doesn't generalize cleanly — INFO needs a separate code path.

This finding is the load-bearing reason Phase 4 split into "Item 2 lands now" + "Item 1 deferred to sub-session." It also documents why v2.7.1 + v2.8.0 carried the same gap silently — no prior phase exercised INFO override, so the architectural mismatch surfaced fresh in v2.9 P3.

## Deviations from plan

1. **Item 1 deferred mid-session via HALT 1 trigger.** Kickoff named items 1+2 as Phase 4's scope. Pre-flight reflection check on the recommended fix shape refuted Phase 3's bug entry's assumption about `SkyrimMod.DialogResponses` existing as a top-level group. Per kickoff mandatory halt trigger #5 ("An assumption Phase 3's bug entry made about Mutagen's `SkyrimMod.DialogResponses` interface turns out wrong"), I halted, presented three options to Aaron (defer / implement-now / spawn-sub-session), and Aaron chose Option C (spawn sub-session). Item 1's race-probe was restructured from probe-first FAIL→PASS to architectural archaeology (DEFERRED, no failure recorded). Items 1b/1c/1d/1e/1e are not landed in this session.
2. **Item 2 landed standalone.** Originally intended as a same-work-commit absorption alongside Item 1. With Item 1 deferred, Item 2 is now Phase 4's sole production-code deliverable. Still ~10 LOC across 2 hunks; still well within the kickoff's "small fix-and-regress" framing.
3. **No coverage-smoke cell added.** Item 1e's `1.P.GetIsID.INFO` cell was tied to Item 1's fix; deferred together. Item 2 doesn't warrant a new cell — its effect (clean record-type code in the error string) is exercised any time `CopyAsOverride` returns null, but no currently-supported record type exercises that path during a green coverage-smoke run. Adding a cell that deliberately attempts INFO override to read back the new error wording would be a valid Phase 4-INFO sub-session deliverable; not landed here to avoid pre-empting that session's scope.

## Known issues / open questions

- **INFO override not yet wired.** Captured in `KNOWN_ISSUES.md § Patching write surface` + `CHANGELOG § Out of scope (v2.9.x candidates)`. Phase 4-INFO sub-session owns the implementation. v2.9.0 ships without INFO override unless the sub-session lands first; if v2.9.0 ships first, INFO override lands as v2.9.1 or later.
- **Pre-existing CS8602 warnings in coverage-smoke `AttachScriptTest`** at line 3932 (carry-forward from v2.8.0 P4 § Known issues). Not a Phase 4 driveby.

## Conductor asks

```
CONDUCTOR ASK
Phase: 4
Topic: Phase 4-INFO sub-session spawn-vs-defer
Context:
- Phase 4 surfaced an architectural mismatch on Phase 3's recommended fix shape (SkyrimMod has no DialogResponses property; INFO is nested under DialogTopic.Responses with no override-add helper)
- Real INFO override implementation is multi-hour scope (CopyAsOverride signature change + parent-topic resolution + child-response find-by-FormKey + new rollback semantics)
- Phase 4 (this session) landed Item 2 (line-180 error-message DX bonus-catch) but deferred Item 1 (INFO override) per Aaron's Option C call mid-session
- Phase 5's ship sequence can proceed without INFO override — v2.9.0 ships with INFO documented as a known limitation (clean per-record error after Item 2's DX fix)
Question: Spawn the Phase 4-INFO sub-session before Phase 5 ships, or punt to v2.9.x point release?
Suggested options:
  A. SPAWN before v2.9.0 ships — sub-session lands INFO override, Phase 5 includes Scenario 3.1 lift-from-BLOCKED→PASS in the ship verification. Costs: extra sub-session (3-6h estimated) before ship.
  B. PUNT to v2.9.1 — v2.9.0 ships with INFO override deferred (current state via Item 2's clean error). Sub-session spawns post-ship as a v2.9.1 fix-only release.
  C. PUNT to a later v2.9.x point release — same as B but with no immediate sub-session commitment; INFO lands when a real consumer surfaces it as blocking (matches v2.7.1 → v2.8.0 → v2.9.0 cadence of "real-consumer-driven" feature picks).
Default if no response: B (punt to v2.9.1, since Phase 5 can ship v2.9.0 cleanly with the gap documented and the line-180 DX fix already softens the user-facing experience).
```

## Phase 4-INFO sub-session preconditions

When the conductor spawns the Phase 4-INFO sub-session, its kickoff inherits the analysis below. The sub-session executor should NOT re-derive any of this — it's already done.

### 1. Architectural diagnosis (load-bearing)

**Mutagen 0.53.1's INFO surface.**

| Surface | What it is | Override-add helper |
|---|---|---|
| `SkyrimMod.DialogResponses` | **Does not exist.** Refuted by reflection. | n/a |
| `SkyrimMod.DialogTopics` | `SkyrimGroup<DialogTopic>` — top-level group of dialog topics. | `GetOrAddAsOverride(IDialogTopicGetter)` ✓ |
| `DialogTopic.Responses` | `Noggog.ExtendedList<DialogResponses>` — plain extended list | `Add(DialogResponses)`, `Insert(int, DialogResponses)`. **No** `GetOrAddAsOverride`. |
| `DialogResponsesMixIn` | Static helper class | `Duplicate(IDialogResponsesGetter, FormKey, TranslationMask)` — produces a fresh `DialogResponses` instance with a new FormKey. `DeepCopy` variants — produce an instance preserving FormKey. **No** override-add. |

**Phase 3's bug entry proposed fix `IDialogResponsesGetter r => patchMod.DialogResponses.GetOrAddAsOverride(r)` is structurally not viable.** The sub-session must use a different shape.

### 2. Likely 5-step implementation outline

Phase 4 sketched the implementation shape during the architectural finding analysis. The sub-session executor should treat this as a strawman, not a spec — verify each step with a probe before committing.

**Step 1: Thread `sourceMod` context through `CopyAsOverride`.**
- Current signature: `private static IMajorRecord? CopyAsOverride(SkyrimMod patchMod, IMajorRecordGetter sourceRecord)`.
- New signature candidate: `private static IMajorRecord? CopyAsOverride(SkyrimMod patchMod, ISkyrimModGetter sourceMod, IMajorRecordGetter sourceRecord)`.
- Ripple: every existing call site (PatchEngine.cs:177 is the only one) needs to pass sourceMod. The 40+ existing switch arms ignore the new param.
- Alternative: keep the existing signature and store a `currentSourceMod` ambient via an `[ThreadStatic]` field set/cleared around the call. Less clean but avoids signature ripple. Sub-session executor's call.

**Step 2: Resolve the parent DialogTopic from the source mod.**
- Approach A: Linear scan `sourceMod.DialogTopics` for the topic whose `Responses` contains a record with the target INFO's FormKey. O(N) per override; cache if hot.
- Approach B: Mutagen's link cache (`ILinkCache.TryResolveContext<IDialogTopic, IDialogTopicGetter>(...)` followed by walking responses). Requires constructing a link cache, which has setup cost.
- Approach C: The bridge already has access to `sourceRecord` from `FindRecord(sourceMod, targetFormKey)` (PatchEngine.cs:172). Mutagen records may expose a `ParentTopic` getter on `IDialogResponsesGetter` — sub-session executor should reflect to verify before committing to Approach A/B.

**Step 3: Override the parent topic.**
- `var parentOverride = patchMod.DialogTopics.GetOrAddAsOverride(parentTopic)` — this is the established pattern; should work cleanly.

**Step 4: Find/return the matching DialogResponses inside the override topic.**
- After GetOrAddAsOverride, the override topic's `Responses` list should mirror the source topic's responses (deep-copy semantics). Iterate `parentOverride.Responses`, find the entry matching the target INFO's FormKey, return it as `IMajorRecord`.
- Edge case: if the source topic carries the INFO at FormKey X but the override list (post-GetOrAddAsOverride) no longer carries it (race condition with prior overrides; unlikely), return null with a clean error.

**Step 5: Mirror in `TryRemoveOverride`.**
- Symmetric path: find the parent topic in `patchMod.DialogTopics`, find the matching response in its `Responses`, remove the response from the list.
- Rollback granularity decision (see § 3 below).

### 3. New failure modes + design decisions for the sub-session

The sub-session executor needs explicit calls on these — they didn't exist in the original 1:1 switch model.

**Failure mode 1: parent topic missing from source mod.** Should never happen for well-formed plugins (the FindRecord call would have failed earlier), but defensive error: `"INFO {FormKey} has no parent DialogTopic in {sourceMod} — record may be orphaned."`

**Failure mode 2: response not found in override topic post-GetOrAddAsOverride.** Likely a Mutagen quirk if hit; defensive error: `"DialogResponses {FormKey} present in source topic but missing from override — Mutagen GetOrAddAsOverride did not deep-copy responses as expected."`

**Failure mode 3: link cache unavailable.** Only relevant if the sub-session picks Approach B. Skippable if Approach A or C suffices.

**Design decision — TryRemoveOverride rollback granularity.** When `ApplyModifications` fails partway through and the caller invokes `TryRemoveOverride`, what should happen for an INFO override?

- **Option α (response-only).** Remove just the matching `DialogResponses` from the override topic's `Responses` list. Leaves the parent topic override in place (with all other responses intact). Risk: the parent override is now "no-op-shaped" (no real changes), masquerading as a legitimate change. Mirrors the existing TryRemoveOverride doc-comment posture: "the no-op override is strictly less misleading than silently swallowing the failure."
- **Option β (full topic rollback).** Remove the entire parent topic override from `patchMod.DialogTopics`. Risk: data loss if the parent topic override was carrying *other* changes (e.g. another response was added in the same `mo2_create_patch` call before the failure). Mirrors the existing TryRemoveOverride semantic for non-nested records.
- **Option γ (response-only + cleanup if-empty).** Remove the response, then check if the parent topic override has any remaining changes (by diff against the source topic). If empty, remove the parent override too. Most semantically clean but most complex.

**Phase 4's recommendation: Option α.** Cheapest to implement; consistent with the existing "no-op override is less misleading than silent swallow" posture; the rollback isn't load-bearing for correctness (the outer `ApplyModifications` exception is what surfaces to the caller). Sub-session can revisit if real consumers surface the no-op-override-masquerade concern.

### 4. Race-probe diagnostic as archaeology

Phase 4's restructured race-probe section (`tools/race-probe/Program.cs`, search for `v2.9 P4 — INFO override architectural archaeology`) carries:

- The reflection check confirming `SkyrimMod.DialogResponses` doesn't exist.
- The full surface dump (SkyrimMod Dialog/Info/Response properties; DialogTopic.Responses methods; DialogResponsesMixIn Duplicate/DeepCopy candidates).
- The prose description of the likely sub-session implementation shape.

**Sub-session executor:** read the probe section's source comments + run the probe (`dotnet run -c Release --no-build --project tools/race-probe`) to see the latest output. The diagnostic is deterministic against Mutagen 0.53.1 so its content won't drift unless the package version bumps. **Once the sub-session implements INFO override, the archaeology section can be replaced** with a probe-first FAIL→PASS lift mirroring the original Phase 4 kickoff intent — this time against the correct fix shape.

### 5. Recommended sub-session deliverables

| # | Item | Files |
|---|---|---|
| 1a | INFO override race-probe regression — proper FAIL→PASS lift against the actual fix shape (parent-topic-resolution path). Replaces the archaeology section. | `tools/race-probe/Program.cs` |
| 1b | `CopyAsOverride` — `IDialogResponsesGetter` branch with parent-topic resolution + child-response find-by-FormKey | `tools/mutagen-bridge/PatchEngine.cs` |
| 1c | `TryRemoveOverride` — symmetric removal path; design decision per § 3 above | same |
| 1d | Re-run race-probe → INFO probe lifts to PASS | same |
| 1e | `1.P.GetIsID.INFO` coverage-smoke positive cell (Scenario 3.1 shape: vanilla INFO + add_conditions GetIsID via parameters.Object → readback proves slot resolved) | `tools/coverage-smoke/Program.cs` |
| 2 | Bridge build clean; new SHA captured; coverage-smoke end-to-end ALL PASS with cell count = 382 + 1 = 383 | bridge artifacts |
| 3 | CHANGELOG: add `### Fixed — bridge` bullet under existing `## v2.9.0 — TBD` (or under a new `## v2.9.1 — TBD` if v2.9.0 has shipped); REMOVE the INFO override deferral bullet from `### Out of scope (v2.9.x candidates)` (the gap is closed). KNOWN_ISSUES: REMOVE the INFO override entry from `## Patching write surface`. | `mo2_mcp/CHANGELOG.md` + `KNOWN_ISSUES.md` |
| 4 | `PHASE_4_INFO_HANDOFF.md` per PLAN.md § Handoff template | `dev/plans/v2.9.X_condition_parameters/` |

Sub-session is single-item-scope (just INFO override; no other absorbents) — mirrors the v2.7.1 / v2.8.0 P4 sub-session model where applied.

## Preconditions for Phase 5

| Precondition | State |
|---|---|
| Bridge SHA `a69179b3…2a7` is the Phase-4-tested baseline | ✓ — captured above; race-probe + coverage-smoke ran clean against this SHA. |
| `info_override_missing_in_copyasoverride` repro is documented | ✓ — Phase 3 handoff § Bugs surfaced + Phase 4 race-probe archaeology + KNOWN_ISSUES + CHANGELOG. |
| Item 2 (line-180 DX) verified | ✓ — drift-detection diff scoped; coverage-smoke green; race-probe clean. |
| Phase 5 scope adjusted for Phase 4 split | ⚠️ — **Decision needed (see § Conductor asks).** If Phase 4-INFO sub-session is spawned before Phase 5: Phase 5 re-runs Scenario 3.1 + 3.2 against the post-sub-session bridge. If sub-session is punted to v2.9.x: Phase 5 ships v2.9.0 with Scenario 3.1 deliberately stays-BLOCKED in the ship verification; CHANGELOG + KNOWN_ISSUES already document the gap and the line-180 DX softens the user-facing experience; Scenario 3.2's 12/12 PASS still satisfies the "Layer 3 re-run required if Phase 4 ran" condition for non-INFO record types. |
| Live install state for Phase 5 | unchanged from Phase 3 (P2D bridge SHA `2e3a1094…f8293975e`); Phase 5 publishes post-Phase-4 build → ship SHA. |
| Coverage-smoke baseline | 382 cells (376 PASS + 6 SKIP + 0 FAIL) at Phase 4 close — same as Phase 2/3 close. No new cells in Phase 4 (Item 2 has no new positive cell; Item 1's `1.P.GetIsID.INFO` cell deferred to sub-session). |

## Files of interest for next session(s)

### Phase 4-INFO sub-session (when spawned)

| Path | Why |
|---|---|
| `dev/plans/v2.9.X_condition_parameters/PHASE_4_HANDOFF.md` (this file) § Phase 4-INFO sub-session preconditions | The architectural diagnosis + 5-step implementation outline + new failure modes / rollback design + recommended deliverables. Inherits as the kickoff context. |
| `tools/race-probe/Program.cs` | Search for `v2.9 P4 — INFO override architectural archaeology` — the archaeological dump. Sub-session replaces this section with the proper FAIL→PASS probe. |
| `tools/mutagen-bridge/PatchEngine.cs:172,177–180` | `FindRecord` + the override-creation call site (sub-session may need to thread `sourceMod` through here). |
| `tools/mutagen-bridge/PatchEngine.cs:2508–2571` | `CopyAsOverride` switch — sub-session adds the IDialogResponsesGetter branch (separate code path, not a one-liner). |
| `tools/mutagen-bridge/PatchEngine.cs:2581+` | `TryRemoveOverride` symmetric path. |
| `dev/plans/v2.9.X_condition_parameters/PHASE_3_HANDOFF.md` § Scenario 3.1 assertion checklist | Phase 5 (post-sub-session) re-runs Scenario 3.1 against this checklist. |

### Phase 5 ship sequence

| Path | Why |
|---|---|
| `dev/plans/v2.8.0_verification/PHASE_5_HANDOFF.md` | Canonical 12-step ship sequence with halt cadence. |
| `dev/plans/v2.9.X_condition_parameters/PHASE_3_HANDOFF.md` § Preconditions for Phase 5 | What carries forward from Phase 3 (live state, Scenario 3.2 PASS, Scenario 3.1 carrier reservation). |
| `dev/plans/v2.9.X_condition_parameters/PHASE_4_HANDOFF.md` (this file) | Phase 4's post-fix bridge SHA + coverage-smoke counts + INFO deferral context. |
| `mo2_mcp/CHANGELOG.md` | Phase 5 inserts ship date in `## v2.9.0 — TBD` header. |

## Acceptance — Phase 4 (under Option C scope)

- ✅ Item 2 (line-180 error-message DX bonus-catch) landed cleanly; drift-detection diff scoped to two hunks (line 180 + line 2479+).
- ✅ Item 1 (INFO override fix) deferred to Phase 4-INFO sub-session per Aaron's call after pre-flight reflection refuted Phase 3's recommended fix shape (HALT 1 trigger #5).
- ✅ Race-probe restructured to architectural archaeology — exit 0, all phases stay green, diagnostic dump preserved as sub-session bootstrap.
- ✅ Bridge builds clean (0 warnings, 0 errors). New SHA `a69179b3…2a7` differs from P2D's `2e3a1094…f8293975e`.
- ✅ Coverage-smoke runs to 382 cells, ALL PASS (376 PASS + 6 SKIP + 0 FAIL). Same SKIP set as Phase 2/3 close — no regression.
- ✅ CHANGELOG `### Changed — bridge` bullet for line-180 DX added under `## v2.9.0 — TBD`. `### Out of scope` gained INFO override deferral with full architectural context.
- ✅ KNOWN_ISSUES `## Patching write surface` gained INFO override entry alongside Outfit/Spell attach_scripts gap.
- ✅ Phase 4-INFO sub-session preconditions section comprehensive (architectural diagnosis + 5-step implementation outline + new failure modes / rollback design decision + race-probe archaeology pointer).
- ⏸️ Two commits pending push (work + hash-record).

## Out of scope for Phase 4 (carried forward unchanged)

- v2.7.1 / v2.8.0 carry-overs (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access).
- Boolean dispatcher branch.
- Sub-B 6 String-slot Condition functions.
- Live install sync (Phase 5 owns).
- Layer 3 scenarios re-run (Phase 5 owns; Scenario 3.1 lift contingent on sub-session).
