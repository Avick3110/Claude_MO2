# Phase 4-INFO Handoff — INFO override implementation (parent-topic resolution + child-response DeepCopy) + Scenario 3.1 unblocks

**Phase:** 4 — INFO sub-session
**Status:** Complete
**Date:** 2026-04-27
**Session length:** ~3h
**Commits made:** Work commit `ed869cf5533de387bfc30a44e0dbc8fecb28d308`; hash-record commit follows on push of this handoff.
**Live install synced:** No — sub-session is local-only per kickoff conductor decision #7. Live install stays at P2D bridge SHA `2e3a1094…f8293975e` until Phase 5's canonical sync.

## What was done

### Item 1a — race-probe `v2.9 P4-INFO — INFO override regression` block

`tools/race-probe/Program.cs` — the `v2.9 P4 — INFO override architectural archaeology (deferred)` block (Phase 4 lines 2188–2286, ~95 lines) is REPLACED with a `v2.9 P4-INFO — INFO override regression (bridge subprocess)` block (~190 lines). The new block shells out to `mutagen-bridge.exe` with a JSON `patch` request: override INFO `Skyrim.esm:000E3D` (MQ101 Helgen-escape Stormcloak/Imperial dialog — Phase 3 Scenario 3.1 carrier) + `add_conditions: [{function: "GetIsID", parameters: {Object: "Skyrim.esm:02BF9F"}}]` (Hadvar — distinct from the source INFO's two native GetIsID slots so the appended condition is uniquely identifiable). Asserts: bridge `success=true`, output ESP exists, override INFO carries the new condition, the GetIsID `Object` slot resolves to Hadvar's FormKey (NOT the FormID 0 dispatcher default). Mirrors v2.8 P4 batch 7's bridge-subprocess probe pattern. Failure counter `p4InfoFailures` (renamed from `p4Failures`) feeds the totalFailures rollup at the bottom of the probe.

Pre-fix run (against the unmodified bridge SHA `a69179b3…2a7`) FAILed with the clean Phase-4-Item-2 wording: `success=false (failed_count=1, process exit=1) — first error: Could not create override for INFO`. Post-fix (against the new bridge SHA — see Item 1d) PASSes: `bridge exit: 0`, override INFO present with `Conditions.Count=6` (5 source + 1 appended), `slot resolved to 02BF9F:Skyrim.esm (NOT FormID 0 default)`. Race-probe overall: `=== probe complete ===` (exit 0); all prior phase scoreboards stay green.

### Step 1 — sourceMod-context threading (signature change)

`tools/mutagen-bridge/PatchEngine.cs:177` (call site) and `:2513` (signature) — `CopyAsOverride(SkyrimMod patchMod, IMajorRecordGetter sourceRecord)` becomes `CopyAsOverride(SkyrimMod patchMod, ISkyrimModGetter sourceMod, IMajorRecordGetter sourceRecord)`. The 40+ existing switch arms ignore `sourceMod` trivially (pure expression bodies). Conductor's preferred shape per kickoff §3 — no `[ThreadStatic]` ambient. The `sourceMod` flowing in is already-`ISkyrimModGetter`-shaped by `FindRecord`'s existing signature, so no upstream ripple beyond the call site.

### Item 1b — `CopyAsOverride` IDialogResponsesGetter branch (separate code path)

`tools/mutagen-bridge/PatchEngine.cs:2524–2525` adds an early-return guard for `IDialogResponsesGetter` before the switch expression:

```csharp
if (sourceRecord is IDialogResponsesGetter response)
    return CopyDialogResponseAsOverride(patchMod, sourceMod, response);
```

The new helper `CopyDialogResponseAsOverride(SkyrimMod patchMod, ISkyrimModGetter sourceMod, IDialogResponsesGetter sourceResponse)` (~30 lines) does:

1. **Parent topic resolution (Approach A — linear scan).** `sourceMod.DialogTopics.FirstOrDefault(t => t.Responses.Any(r => r.FormKey == responseFk))`. Approach C (direct parent-topic getter) is unavailable — `IDialogResponsesGetter` has no `.ParentTopic` property; the `.Topic` `IFormLinkNullable<IDialogTopicGetter>` field exists but its semantics for "true parent topic of this response" overlap with `WalkAwayTopic` / `LinkTo` and aren't documented as the parent reference, so we resolve structurally via the GRUP-nesting that the source plugin already encodes. Linear scan is microseconds for a single override (Skyrim.esm has ~thousands of topics × handful of responses each). Defensive `InvalidOperationException` on parent-not-found names the orphaned-record case.
2. **Parent topic override.** `patchMod.DialogTopics.GetOrAddAsOverride(parentTopic)` — establishes the topic GRUP membership in the patch mod.
3. **Idempotency check.** If `parentOverride.Responses` already contains a response with the target FormKey (caller invoked an op against the same INFO twice in one patch session), return that existing override response. Protects callers issuing multiple ops targeting the same INFO in a single `mo2_create_patch` call from getting a duplicated entry.
4. **Explicit child DeepCopy.** `sourceResponse.DeepCopy()` (FormKey preserved via `DialogResponsesMixIn`'s extension method) → append to `parentOverride.Responses`. The bridge then mutates the override response through the standard `ApplyModifications` path. Returns the override response as `IMajorRecord`.

See § Findings below for the architectural correction this shape encodes (load-bearing pattern beyond INFO).

### Item 1c — `TryRemoveOverride` symmetric INFO removal (Option α)

`tools/mutagen-bridge/PatchEngine.cs:2634–2654` adds an `IDialogResponsesGetter` case to the switch:

```csharp
case IDialogResponsesGetter:
    foreach (var topicOverride in patchMod.DialogTopics)
    {
        var match = topicOverride.Responses.FirstOrDefault(r => r.FormKey == fk);
        if (match != null)
        {
            topicOverride.Responses.Remove(match);
            break;
        }
    }
    break;
```

Option α — response-only rollback. Removes the matching DialogResponses from the override topic's `Responses` list; leaves the parent topic override in place even if it now carries no real changes. Consistent with the method's overall posture for unknown record types: "the no-op override is strictly less misleading than silently swallowing the failure" (per the existing doc-comment, applied recursively to the nested case). Conductor decision #5 confirmed Option α; β and γ rejected (β risks data loss if the parent override carries other changes; γ adds complexity not earned for the marginal cleanliness).

### Item 1d — bridge build + race-probe re-run

`cd tools/mutagen-bridge && dotnet build -c Release` — 0 warnings, 0 errors. `cd .. && dotnet run -c Release --no-build --project tools/race-probe` → race-probe exit 0; all phases green; `v2.9 P4-INFO probes: ALL PASS`.

**New bridge SHA:** `1b54e8eb5b975727d07c19940ca238bcd4e2e7afca2e64e77d0638d333f2a3dd`
(at `tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`). Differs from Phase 4's `a69179b3…2a7` byte-for-byte ✓ (signature ripple through 40+ switch arms + new `CopyDialogResponseAsOverride` method + TryRemoveOverride INFO case all visible in the binary).

### Item 1e — `1.P.GetIsID.INFO` coverage-smoke positive cell (Test 382)

`tools/coverage-smoke/Program.cs` — new Test 382 cell inserted between the existing GetVATSValueUnknown SKIP and the SKIP rollup (line 7500ish). Same shape as Phase 3's Scenario 3.1: vanilla INFO `Skyrim.esm:000E3D` + `add_conditions GetIsID parameters.Object=Skyrim.esm:02BF9F (Hadvar)`. Readback enumerates via `outMod.EnumerateMajorRecords<IDialogResponsesGetter>()`, locates the override INFO by FormKey, asserts `Conditions.Count=6` (5 source + 1 appended), then finds the appended GetIsID condition by `Object.Link.FormKey == hadvarFk` (not by ordinal — the source MQ101 INFO has 2 native GetIsID conditions targeting Player + Ulfric, so finding by slot value is the unique identifier). Run: PASS. Numbered 382 (next available after the existing P2D bulk Tests 372-381).

### Item 2 — Coverage-smoke end-to-end

`dotnet run -c Release --no-build --project tools/coverage-smoke` — `=== smoke complete: ALL PASS ===`. Cell counts:

| Bucket | Count | Notes |
|---|---:|---|
| Total cells | 383 | 382 baseline + 1 new (Test 382) |
| 0 FAIL | ✓ | All baseline cells stay green; Test 382 PASSes. |
| 6 SKIP unchanged | ✓ | Same set as Phase 4 close: 1.r.40 (OTFT), 1.r.47 (SPEL), 1.D.04 (CELL), 4.esl.01 (Layer 4 ESL), 1.P.Unknown.MGEF, 1.P.GetVATSValueUnknown.MGEF. None added, none lifted. |

### Items 3a/3b — Docs hygiene

- **`mo2_mcp/CHANGELOG.md`**:
  - Under existing `## v2.9.0 — TBD`: ADDED `### Fixed — bridge` section with the `info_override_missing_in_copyasoverride` entry. Body documents the parent-topic resolution + DeepCopy approach, the architectural correction (load-bearing pattern beyond INFO), idempotency rationale, and pointers to the race-probe + coverage-smoke + Phase 5 Scenario 3.1 triple-anchor regression.
  - REMOVED the INFO override deferral bullet from `### Out of scope (v2.9.x candidates within release line)` (the gap is closed; first bullet was `INFO record op: "override" (Phase 4 architectural finding — sub-session deferral)`).
- **`KNOWN_ISSUES.md`**: REMOVED the INFO entry from `## Patching write surface — current limitations` (section's first INFO-specific bullet, between the Outfit/Spell entry and the AMMO entry).

## Verification performed

### Pre-fix probe FAIL trace (HALT 2)

```
=== v2.9 P4-INFO — INFO override regression (bridge subprocess) ===
  bridge:  …\mutagen-bridge.exe (SHA a69179b3…2a7 — Phase 4 baseline)
  carrier: INFO Skyrim.esm:000E3D (MQ101 Helgen dialog, Scenario 3.1 record)
  append:  GetIsID Object=Skyrim.esm:02BF9F (Hadvar)
  bridge exit: 1
  *** FAIL: bridge reports success=false (failed_count=1, process exit=1)
      first error: Could not create override for INFO
=== v2.9 P4-INFO probes: 1 FAILURE(S) ===

=== probe FAILED: 1 audit failure(s) (0 v2.7.1 + 0 v2.8 P1 + 0 v2.9 P1 + 0 v2.9 P2A + 0 v2.9 P2B + 0 v2.9 P2C + 0 v2.9 P2D + 1 v2.9 P4-INFO) ===
```

Confirms: line-180 DX bonus-catch wording landed cleanly (`INFO`, not `DialogResponsesBinaryOverlay`); all prior-phase scoreboards stay green; FAIL is uniquely attributable to v2.9 P4-INFO.

### Post-fix probe PASS trace (HALT 3)

```
=== v2.9 P4-INFO — INFO override regression (bridge subprocess) ===
  bridge:  …\mutagen-bridge.exe (SHA 1b54e8eb…2a3dd — Phase 4-INFO post-fix)
  carrier: INFO Skyrim.esm:000E3D (MQ101 Helgen dialog, Scenario 3.1 record)
  append:  GetIsID Object=Skyrim.esm:02BF9F (Hadvar)
  bridge exit: 0
  override INFO Skyrim.esm:000E3D present in output ESP (Conditions.Count=6)
  PASS  INFO override + GetIsID(Object=Skyrim.esm:02BF9F) round-trip ✓
        slot resolved to 02BF9F:Skyrim.esm (NOT FormID 0 default)
=== v2.9 P4-INFO probes: ALL PASS ===

=== probe complete ===
```

### Drift-detection diff (`git diff --name-only 8bea3143`)

```
tools/coverage-smoke/Program.cs
tools/mutagen-bridge/PatchEngine.cs
tools/race-probe/Program.cs
```

Three files exactly. No drift outside the named regions.

`git diff --stat 8bea3143 -- tools/...`:

```
tools/coverage-smoke/Program.cs    | +104 lines (Test 382 cell)
tools/mutagen-bridge/PatchEngine.cs | 207 lines  (sig change + INFO branch + helper + TryRemoveOverride INFO case + indentation re-shuffle from expression-bodied → block-bodied switch)
tools/race-probe/Program.cs        | 294 lines  (archaeology block REPLACED with regression block)
```

The PatchEngine.cs line count looks larger than the pure logical change because converting the `=> sourceRecord switch { ... }` expression-bodied method to a block-bodied method requires re-indenting all 40+ switch arms by 4 spaces. The actual NEW logic is: 3 lines at the call site (sig change ripple), 12 lines for the IDialogResponsesGetter early-return guard + comment, ~30 lines for `CopyDialogResponseAsOverride`, ~20 lines for the TryRemoveOverride INFO case.

### Bridge build clean

`cd tools/mutagen-bridge && dotnet build -c Release` → 0 warnings, 0 errors.

### Coverage-smoke end-to-end

383 cells, 377 PASS + 6 SKIP + 0 FAIL. Test 382 PASS. SKIP set unchanged from Phase 4 close.

### Pre-existing CS8602 warnings

Coverage-smoke build still emits 2 CS8602 warnings at Program.cs:3932 — pre-existing per Phase 4 § Known issues; not introduced by Phase 4-INFO; not absorbed (sub-session scope-locked at items 1a–1e + docs + handoff).

## Bugs surfaced

**Zero new bridge bugs.** The HALT trigger that did fire mid-session was an architectural-assumption refutation, not a bug — see § Deviations from plan and § Findings.

## Findings

### Architectural correction — Phase 4 + this kickoff carried an incomplete assumption (load-bearing pattern beyond INFO)

Phase 4's deferred-state architectural archaeology refuted Phase 3's recommended fix shape (`patchMod.DialogResponses.GetOrAddAsOverride(r)`) on the correct ground that `SkyrimMod` doesn't expose a top-level `DialogResponses` group. Phase 4's resulting outline (§ 2 Step 4 in the Phase 4 handoff) and this kickoff's HALT trigger #4 ("override topic's `Responses` list post-`GetOrAddAsOverride` does NOT carry the original DialogResponses entries — would mean Mutagen's GetOrAddAsOverride doesn't deep-copy nested lists as expected — major Mutagen quirk, halt and ask conductor before working around") + Phase 4's Failure mode 2 ("response not found in override topic post-GetOrAddAsOverride — Likely a Mutagen quirk if hit") all forward-carried the **assumption** that `DialogTopic.GetOrAddAsOverride` would deep-copy the topic's nested `Responses` list. **It doesn't, and that's correct Bethesda format behavior, not a Mutagen quirk.**

INFO records are independent major records — their own FormIDs, their own override semantics, their own RGRP membership — nested under DIAL GRUPs purely for organizational reasons. Override-add of a parent topic deep-copies the topic's record-level fields, not its child majors. The fix shape is therefore not "iterate parent override's Responses to find the child" (Phase 4 § 2 Step 4) but rather "explicitly DeepCopy the source response into the parent override's Responses list" via `DialogResponsesMixIn.DeepCopy(sourceResponse)`. This is the standard Mutagen pattern, not a workaround.

**This generalizes beyond INFO.** Any future "child major nested under organizational GRUP parent" gap follows the same reference shape:

1. Override the parent for the GRUP membership (`patchMod.{ParentGroup}.GetOrAddAsOverride(sourceParent)`).
2. Idempotency-check the parent override's child list for an existing entry at the target FormKey; return it if present.
3. Otherwise, explicitly deep-copy the targeted child via `{ChildType}MixIn.DeepCopy(sourceChild)` (FormKey preserved) and append to the parent override's child list.
4. Symmetric removal in `TryRemoveOverride`: scan `patchMod.{ParentGroup}` for the override parent that holds the target child by FormKey; remove the child from the parent override's list.

Future candidates this generalization unblocks (without re-deriving): similar PERK.Effects sub-records (mentioned in v2.8.0 carry-overs as "harder; sub-class polymorphism"), QUST.Aliases / Stages / Objectives (same v2.8.0 carry-over), any future Mutagen-modeled child-major patterns. The handoff captures this as the v2.9.x reference shape — first-consumer-trigger landings of those gaps don't have to re-derive the architectural diagnosis; they just transcribe the Phase 4-INFO `CopyDialogResponseAsOverride` pattern with the appropriate type substitutions.

The idempotency check (returns existing override response if already present from a prior op in the same patch session) is a DX nicety that protects callers issuing multiple ops targeting the same INFO in a single `mo2_create_patch` call from getting a duplicated entry — worth noting for future analogous patterns where a single patch session might touch the same child major from multiple ops.

## Deviations from plan

1. **Architectural correction to Phase 4's outline + this kickoff's HALT trigger #4 (load-bearing).** See § Findings above. The fix shape lands as parent-topic override + explicit child DeepCopy + idempotency check — not parent-topic override + iterate-Responses-for-deep-copied-child as Phase 4 § 2 Step 4 outlined. Aaron approved continuing past the HALT trigger after surfacing the correction; the handoff captures it prominently because the corrected pattern is the v2.9.x reference shape for any future "child major nested under organizational GRUP parent" gap.
2. **Approach A chosen over Approach C.** Kickoff §4 recommended reflecting on `IDialogResponsesGetter` for a direct parent-topic getter (Approach C) before falling back to linear scan (Approach A). Reflection found no `.ParentTopic` direct getter on `IDialogResponsesGetter`; the `.Topic` `IFormLinkNullable<IDialogTopicGetter>` field exists but its semantics for "true parent topic of this response" are ambiguous vs `WalkAwayTopic` / `LinkTo`, so trusting it would risk silent wrong-parent resolution. Approach A is deterministic; for a single override per call (microseconds against ~thousands of topics × handful of responses each in vanilla Skyrim.esm), the perf cost is negligible. Documented the call in `CopyDialogResponseAsOverride`'s doc-comment.
3. **Bonus-finding for `SkyrimGroup<DialogTopic>` indexer.** Reflection during HALT 1 also confirmed `SkyrimGroup<DialogTopic>` exposes `this[FormKey]: DialogTopic` (O(1) FormKey-keyed lookup) + `ContainsKey(FormKey)`. Could have been used in `TryRemoveOverride` to short-circuit the linear scan when `patchMod.DialogTopics` carries a known-to-be-overridden parent. Not used — the foreach-and-break shape is symmetric with the CopyAsOverride parent resolution and reads cleanly; `patchMod.DialogTopics` in practice has at most a handful of override topics in one patch session, so the linear scan is a non-issue. Captured here so a future v2.9.x optimization (if rollback profiling ever surfaces this as hot) has the alternate shape pre-validated.

## Known issues / open questions

- **Pre-existing CS8602 warnings in coverage-smoke at Program.cs:3932** (carry-forward from v2.8.0 P4 + Phase 4 § Known issues). Not introduced by Phase 4-INFO; not absorbed (sub-session scope-locked).
- **Live install state** unchanged from Phase 3 (P2D bridge SHA `2e3a1094…f8293975e`); Phase 5 owns the canonical sync. The pre-Phase-4-INFO `mo2_create_patch` call against an INFO would still hit the line-180 error on the live install until Phase 5 syncs.

## Conductor asks

None. The architectural correction was surfaced and resolved in-session under Aaron's "push through" directive at HALT 2; all sub-session deliverables landed cleanly.

## Preconditions for Phase 5

| Precondition | State |
|---|---|
| Bridge SHA `1b54e8eb…2a3dd` is the Phase-4-INFO-tested baseline | ✓ — captured above; race-probe + coverage-smoke ran clean against this SHA. |
| `info_override_missing_in_copyasoverride` fix landed | ✓ — race-probe FAIL→PASS lift; coverage-smoke Test 382 PASS; clean `success=true` end-to-end through the bridge subprocess. |
| Drift-detection scope verified | ✓ — only 3 files touched (PatchEngine.cs + race-probe Program.cs + coverage-smoke Program.cs); no drift outside named regions. |
| Layer 3 Scenario 3.1 — re-run required | ⏸️ Phase 5 owns. **Lift from BLOCKED → PASS expected.** Same record selection (INFO `Skyrim.esm:000E3D`, GetIsID `Object` slot Lydia `Skyrim.esm:000A2C8E` per Phase 3 carrier reservation) and full 9-assertion checklist (3.1.A–3.1.I per [PHASE_3_HANDOFF.md](PHASE_3_HANDOFF.md) § Scenario 3.1 assertion checklist) re-evaluated end-to-end against the post-Phase-4-INFO live bridge. The race-probe + coverage-smoke regressions in this sub-session use Hadvar `Skyrim.esm:02BF9F` (canary-verified) rather than Lydia, so Phase 5's run against Lydia is independent verification of the same code path. |
| Layer 3 Scenario 3.2 — re-run required | ⏸️ Phase 5 owns. Verified PASS in Phase 3 (12/12 assertions) against P2D bridge SHA `2e3a1094…f8293975e`; Phase 4 + Phase 4-INFO landed bridge changes (line-180 DX, signature, INFO branch, INFO TryRemoveOverride) — re-run against post-fix bridge expected to confirm no regression. Same record selection (PERK `Skyrim.esm:0CB413`) and 12-assertion checklist (3.2.A–3.2.L). |
| Final ship SHA | ⏸️ Phase 5 produces via `dotnet publish` (different from Phase 4-INFO build SHA). |
| Coverage-smoke baseline for Phase 5 | 383 cells (377 PASS + 6 SKIP + 0 FAIL). Phase 5's ship-SHA re-run against the published binary should match. |
| Race-probe baseline for Phase 5 | All phases green; v2.9 P4-INFO probes: ALL PASS. Phase 5's ship-SHA re-run should match. |

## Files of interest for Phase 5

| Path | Why |
|---|---|
| `dev/plans/v2.8.0_verification/PHASE_5_HANDOFF.md` | Canonical 12-step ship sequence with halt cadence (the v2.7.1 / v2.8.0 P5 model). |
| `dev/plans/v2.9.X_condition_parameters/PHASE_3_HANDOFF.md` § Preconditions for Phase 5 + § Scenario 3.1 assertion checklist | Phase 3's carry-forward reservation: Scenario 3.1 record selection, Scenario 3.2 12-assertion checklist for re-run. |
| `dev/plans/v2.9.X_condition_parameters/PHASE_4_HANDOFF.md` | Phase 4's bridge SHA + DX bonus-catch context. |
| `dev/plans/v2.9.X_condition_parameters/PHASE_4_INFO_HANDOFF.md` (this file) | Phase 4-INFO bridge SHA + INFO override mechanism + architectural correction (load-bearing for any future child-major-under-organizational-GRUP-parent gap landings). |
| `tools/mutagen-bridge/PatchEngine.cs:177,2513–2575,2578–2613,2654–2674` | The four landing zones — call site + CopyAsOverride sig + IDialogResponsesGetter early-return + new helper + TryRemoveOverride INFO case. |
| `tools/race-probe/Program.cs:2188–2407` | The new v2.9 P4-INFO regression block (replaces the Phase 4 archaeology). |
| `tools/coverage-smoke/Program.cs` Test 382 | The 1.P.GetIsID.INFO regression cell (right before the SKIP rollup). |
| `mo2_mcp/CHANGELOG.md` | Phase 5 inserts ship date in `## v2.9.0 — TBD` header; the new `### Fixed — bridge` entry for `info_override_missing_in_copyasoverride` documents what shipped. |
