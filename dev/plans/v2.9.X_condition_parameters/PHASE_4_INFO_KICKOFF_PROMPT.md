# Phase 4-INFO Sub-Session Kick-off — INFO override fix (parent-topic resolution + child-response find)

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 4-INFO sub-session executor** for the v2.9.0 Claude_MO2 release. Your job is to land the INFO override fix that Phase 4 deferred after pre-flight reflection refuted Phase 3's recommended fix shape (`patchMod.DialogResponses.GetOrAddAsOverride(r)` is structurally not viable — Mutagen 0.53.1's `SkyrimMod` has no `DialogResponses` property; INFO is nested under `DialogTopic.Responses` with no override-add helper). **The architectural work is already done** — Phase 4's handoff captured the Mutagen surface diagnosis, the 5-step implementation outline, the new failure modes, and the Option α/β/γ rollback design with Phase 4's recommended Option α. Your job is the implementation: build the parent-topic resolution + child-response find-by-FormKey path through `CopyAsOverride` + `TryRemoveOverride`, lift Phase 4's race-probe archaeology to a proper FAIL→PASS regression, add the `1.P.GetIsID.INFO` coverage-smoke cell, and ship docs.

## Context (read this once, don't search for history)

v2.8.0 shipped `419a719`. v2.9.0 in flight: Phases 0/1 + 2A/2B/2C/2D + 3 + 4 complete (origin/main HEAD `8bea314`). Phase 2 landed the dispatcher feature-complete (199 wired functions across 5 of 6 PLAN-named branches). Phase 3 surfaced `info_override_missing_in_copyasoverride`. Phase 4 landed Item 2 (line-180 error-message DX bonus-catch — the leaky `DialogResponsesBinaryOverlay` overlay-class-name leak is fixed; user-facing errors now name the 4-char record type code via `RecordTypeCode(sourceRecord)`). Phase 4's bridge SHA `a69179b30217746e29ab727ac8484a242c72aba29f7ee38f3846b025653972a7` is your drift-detection baseline. Coverage-smoke at 382 cells (376 PASS + 6 SKIP + 0 FAIL).

The architectural diagnosis Phase 4 surfaced (load-bearing, do not re-derive): `SkyrimMod.DialogResponses` doesn't exist; `SkyrimMod.DialogTopics: SkyrimGroup<DialogTopic>` is the parent group; `DialogTopic.Responses: Noggog.ExtendedList<DialogResponses>` is a plain extended list with `Add`/`Insert` but no `GetOrAddAsOverride`; `DialogResponsesMixIn` static helpers offer `Duplicate`/`DeepCopy` but no override-add. **INFO override requires parent-topic `GetOrAddAsOverride` + finding the matching child response by FormKey inside the override topic's `Responses` list.**

## Path conventions

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<plan>` | `<repo>\dev\plans\v2.9.X_condition_parameters\` |

Quote paths in shell commands.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `8bea314…` (Phase 4 hash-record commit).
   - Working tree clean.
   - Repo bridge SHA `a69179b30217746e29ab727ac8484a242c72aba29f7ee38f3846b025653972a7`. Confirm via `sha256sum <repo>/tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`. If SHA differs, halt — dirty inheritance.
   - Note: mo2 MCP server may be disconnected (Phase 4 closed without live sync). This sub-session is local-only (race-probe + coverage-smoke + bridge build + docs); zero mo2_* tool calls needed. If mo2_* tools aren't in your tool list, that's expected — don't halt for it.
2. **Read these files in full, in order:**
   - `<plan>/PHASE_4_HANDOFF.md` — **§ Phase 4-INFO sub-session preconditions is your spec.** Architectural diagnosis (§1), 5-step implementation outline (§2), new failure modes + Option α/β/γ rollback design (§3), race-probe archaeology pointer (§4), recommended deliverables (§5).
   - `<plan>/PLAN.md` § Session-start ritual + § Architecture A/B/C + § Handoff template + § Communicating with the conductor + § Conventions (probe-first discipline).
   - `<plan>/PHASE_3_HANDOFF.md` § Bugs surfaced (info_override_missing_in_copyasoverride entry) + § Scenario 3.1 assertion checklist (Phase 5 will re-verify against this; your `1.P.GetIsID.INFO` coverage-smoke cell mirrors the assertion shape).
3. **Skim, don't memorize:**
   - `<repo>/tools/race-probe/Program.cs` — search for `v2.9 P4 — INFO override architectural archaeology`. The full surface dump + likely sub-session implementation shape lives here. Run the probe (`dotnet run -c Release --no-build --project tools/race-probe`) to see its current output before extending — the diagnostic is deterministic against Mutagen 0.53.1.
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:172` — `FindRecord(sourceMod, targetFormKey)` returns `IMajorRecordGetter`. Source of `sourceMod` for threading.
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:177–180` — the `CopyAsOverride` call site with the line-180 DX bonus-catch already landed.
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:2508–2571` — `CopyAsOverride` switch (where the `IDialogResponsesGetter` branch lands; **NOT** as a one-liner — separate code path with parent resolution).
   - `<repo>/tools/mutagen-bridge/PatchEngine.cs:2581+` — `TryRemoveOverride` switch (symmetric; per its doc comment, "when CopyAsOverride learns a new record type, this switch must too").

## Conductor decisions inherited (locked — do not re-litigate)

1. **Version slug = `v2.9.0`.** No re-bump. Sub-session lands under existing v2.9.0 entry.
2. **No plan-amend.** Phase 4-INFO is fix-and-regress; the architectural finding is captured in CHANGELOG already from Phase 4. Once the sub-session lands the fix, CHANGELOG's `### Out of scope` deferral bullet REMOVES (gap closed) + new `### Fixed — bridge` bullet ADDS. KNOWN_ISSUES `## Patching write surface` INFO entry REMOVES.
3. **Step 1 sourceMod-context threading: signature change preferred over `[ThreadStatic]` ambient.** Phase 4's two-approach sketch leaves the call open. Conductor recommends **signature change**: cleaner, no hidden state, the 40+ existing switch arms ignore the new param trivially (single-line addition each — the param is simply unused in the existing branches). `[ThreadStatic]` is a code smell that's hard to test and obscures the call graph. If you discover a structural reason signature change is blocked (e.g. a pattern-match breaks, an interface boundary intervenes), escalate via § Conductor asks rather than fall back to ambient quietly.
4. **Step 2 parent-topic resolution approach: reflect first, then pick.** Phase 4 sketched three approaches (linear scan / link cache / parent-topic getter on `IDialogResponsesGetter`). **First action**: reflect on `IDialogResponsesGetter` to see if it exposes a parent-topic getter — that's Approach C and is the cleanest. If it doesn't, fall back to Approach A (linear scan; cache only if hot, which it likely isn't for a single override). Approach B (link cache) is overkill for this use case unless the bridge already builds one for other reasons (it doesn't, per current PatchEngine.cs). Document the call in handoff under § What was done.
5. **Step 5 rollback granularity: Option α (response-only).** Phase 4 recommended Option α as its design call. Conductor confirms — cheapest to implement, consistent with the existing TryRemoveOverride doc-comment posture ("the no-op override is strictly less misleading than silently swallowing the failure"), and the rollback isn't load-bearing for correctness (the outer ApplyModifications exception surfaces to the caller). Sub-session implements Option α; Option β (full topic rollback) is rejected (data-loss risk if parent override carries other changes); Option γ (response-only + cleanup-if-empty) is rejected (complexity not earned for the marginal cleanliness).
6. **Probe-first discipline per PLAN.md § Conventions.** Replace Phase 4's archaeology section with a proper FAIL→PASS lift. The probe attempts the actual fix shape (parent-topic resolution + child-response find), FAILs against the unmodified bridge (since the new code path isn't there yet), then PASSes after the fix lands. Same shape v2.7.1 + v2.8.0 P4 probes used.
7. **Bridge SHA changes; live re-sync gated on Aaron's go-ahead.** This sub-session's bridge build produces a new SHA (different from Phase 4's `a69179b3…2a7`). Live install at `<live>/` stays at P2D `2e3a1094…f8293975e` until Phase 5's canonical sync — Phase 4-INFO doesn't NEED a live re-sync to complete (race-probe + coverage-smoke are local). Phase 5 owns Scenario 3.1 lift-from-BLOCKED→PASS against the live install.
8. **No other carryovers absorbed.** v2.7.1 / v2.8.0 deferrals (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access, Boolean dispatcher branch, sub-B 6 String-slot functions) all stay deferred. This sub-session is single-item-scope: just INFO override.

## Phase 4-INFO deliverables

| # | Item | Files |
|---|---|---|
| 1a | INFO override race-probe regression — proper FAIL→PASS lift against the parent-topic-resolution path. **Replaces** Phase 4's archaeology section in `tools/race-probe/Program.cs`. | `<repo>/tools/race-probe/Program.cs` |
| 1b | `CopyAsOverride` — `IDialogResponsesGetter` branch: thread sourceMod (signature change), reflect on `IDialogResponsesGetter` for parent-topic getter (Approach C); fallback to linear scan (Approach A) if Approach C unavailable; `patchMod.DialogTopics.GetOrAddAsOverride(parentTopic)`; iterate override topic's `Responses` for the matching FormKey; return that DialogResponses as `IMajorRecord`. New failure-mode error messages per Phase 4 handoff § 3. | `<repo>/tools/mutagen-bridge/PatchEngine.cs:2549+` (alongside IDialogTopicGetter — though as a separate code path, not a one-line switch arm) |
| 1c | `TryRemoveOverride` — symmetric INFO removal: locate parent topic in `patchMod.DialogTopics`, find matching response in `Responses` list, remove it. **Option α implementation**: response-only; leave parent override in place. | `<repo>/tools/mutagen-bridge/PatchEngine.cs:2581+` |
| 1d | Re-run race-probe — Item 1a's probe lifts from FAIL to PASS post-fix. Race-probe total exit 0; all prior-phase scoreboards stay green. | `<repo>/tools/race-probe/Program.cs` |
| 1e | `1.P.GetIsID.INFO` coverage-smoke positive cell — covers Scenario 3.1's exact shape: vanilla INFO + `add_conditions: [{function: "GetIsID", parameters: {Object: "<NPC-FormID>"}}]` → readback proves slot resolved (NOT default FormID 0). | `<repo>/tools/coverage-smoke/Program.cs` |
| 2 | Bridge build clean; new SHA captured; coverage-smoke end-to-end ALL PASS with cell count 382 + 1 = **383**. | bridge artifacts |
| 3a | CHANGELOG: under existing `## v2.9.0 — TBD` entry, ADD `### Fixed — bridge` bullet for `info_override_missing_in_copyasoverride`. REMOVE the INFO override deferral bullet from `### Out of scope (v2.9.x candidates within release line)` (the gap is closed). | `<repo>/mo2_mcp/CHANGELOG.md` |
| 3b | KNOWN_ISSUES: REMOVE the INFO override entry from `## Patching write surface — current limitations`. | `<repo>/KNOWN_ISSUES.md` |
| 4 | `PHASE_4_INFO_HANDOFF.md` per PLAN.md § Handoff template. | `<plan>/PHASE_4_INFO_HANDOFF.md` |

## Double-commit cadence (no plan-amend, no version bump)

1. **Work commit:** `[v2.9 P4-INFO] INFO override implementation (parent-topic resolution + child-response find) + Scenario 3.1 unblocks`. Bridge code + race-probe (lifts archaeology to FAIL→PASS) + coverage-smoke + CHANGELOG + KNOWN_ISSUES. Push.
2. **Hash-record commit:** `[v2.9 P4-INFO] Handoff: record commit hash <work-hash>`. PHASE_4_INFO_HANDOFF.md only. Push.

End each subject line with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line bodies.

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 4-INFO sub-session executor" + confirm session-start state (HEAD + repo bridge SHA + tree clean).
2. Recap deliverables in your own words.
3. **Reflect on `IDialogResponsesGetter` to confirm Approach C (parent-topic getter) availability.** If C is available, work plan uses C. If not, work plan uses A (linear scan). Document the call.
4. Propose your work order: **Item 1a probe rewrite (FAIL expected against unmodified bridge) → Step 1 sourceMod-threading signature change → Step 2 parent-topic resolution implementation → Item 1b CopyAsOverride INFO branch → bridge build → re-run race-probe (PASS expected) → Item 1c TryRemoveOverride symmetric path → Item 1e coverage-smoke cell → bridge build → coverage-smoke end-to-end → docs (Item 3a/3b) → handoff.**
5. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **HALT 1 — Reflection result + work-plan lock.** After step 3 above (reflect on `IDialogResponsesGetter`), report Approach C vs A pick to Aaron. The choice cascades through Item 1b's implementation. Surface concern: if neither C nor A is workable (say, `Responses` list construction is non-deterministic across read-back cycles), escalate via § Conductor asks before committing.
- **HALT 2 — Pre-fix probe FAIL confirms repro.** Item 1a's probe attempts the new fix shape against the unmodified bridge; FAILs cleanly. Show Aaron the FAIL trace; confirms the probe targets the right code path.
- **HALT 3 — Post-fix probe PASS + coverage-smoke green.** After Items 1b/1c/1d/1e + bridge builds clean + coverage-smoke end-to-end. Show Aaron: probe PASS trace, new SHA, total cell count (383: 382 baseline + 1 new), 0 FAIL, 6 SKIPs unchanged (the 2 carryovers from v2.8 baseline + 4 P2C/P2D-era — your `1.P.GetIsID.INFO` shouldn't introduce a new SKIP since it tests a now-supported path), drift-detection diff confirming bridge changes scoped to `CopyAsOverride` + `TryRemoveOverride` + the sourceMod-threading signature change.

## Mandatory halt-and-report triggers (any → halt immediately)

- Any of the 376 PASS cells starts failing. Drift-detection: `git diff 8bea314 -- tools/mutagen-bridge/PatchEngine.cs` should show only the named changes (sourceMod signature + CopyAsOverride INFO branch + TryRemoveOverride INFO branch). If unrelated code paths drifted (e.g. another switch arm changed because of the signature ripple in an unexpected way), halt.
- Bridge build fails (warnings or errors).
- Reflection on `IDialogResponsesGetter` produces an unexpected result (e.g. neither parent-topic-getter nor linear-scan-via-sourceMod surfaces a clean parent resolution path).
- The override topic's `Responses` list post-`GetOrAddAsOverride` does NOT carry the original DialogResponses entries (would mean Mutagen's GetOrAddAsOverride doesn't deep-copy nested lists as expected — major Mutagen quirk, halt and ask conductor before working around).
- Bonus-catch surfaces > 1h additional or new operator surface.
- Any pre-existing test (race-probe or coverage-smoke) starts failing post-fix that wasn't related to the INFO override path.

## Acceptance criteria (Phase 4-INFO complete)

- INFO race-probe lifts from FAIL→PASS through the new fix shape; archaeology section replaced with proper regression.
- `CopyAsOverride` + `TryRemoveOverride` both grow `IDialogResponsesGetter` paths (separate code blocks, not one-liners). Bridge builds clean.
- `1.P.GetIsID.INFO` coverage-smoke cell PASSes (vanilla INFO + add_conditions GetIsID via parameters.Object → readback proves slot resolved + INFO override succeeded).
- Coverage-smoke total: **383 cells**; 377 PASS (376 baseline + 1 new) + 6 SKIPs unchanged + 0 FAIL.
- All 376 baseline PASS cells stay green — drift-detection diff confirms scoped bridge changes (sourceMod signature + 2 switch additions).
- New bridge SHA captured (must differ from Phase 4's `a69179b3…2a7`).
- CHANGELOG: `### Fixed — bridge` bullet ADDED for `info_override_missing_in_copyasoverride`; deferral bullet REMOVED from `### Out of scope`.
- KNOWN_ISSUES: INFO override entry REMOVED from `## Patching write surface`.
- Handoff under 400 lines.
- Race-probe `v2.9 P4 — INFO override architectural archaeology` section has been REPLACED with a proper FAIL→PASS regression (or renamed to `v2.9 P4-INFO — INFO override regression`), per the architectural-archaeology-was-temporary intent.

## Out of scope for Phase 4-INFO

- Other v2.7.1 / v2.8.0 carryovers (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access).
- Boolean dispatcher branch (deferred).
- Sub-B 6 String-slot Condition functions (deferred).
- Live install sync (Phase 5 owns).
- Re-running Layer 3 scenarios (Phase 5 owns; Scenario 3.1 lift verified post-sub-session).
- Version bump.
- Plan-amend (none expected).
- Touching CONDITIONS_AUDIT.md / MATRIX.md.
- Modifying any P0/P1/P2A/P2B/P2C/P2D code paths beyond the necessary signature change to `CopyAsOverride` (and its trivial ripple through the 40+ other switch arms ignoring the new param).

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `<plan>/PHASE_4_INFO_HANDOFF.md` per PLAN.md § Handoff template:
   - **What was done** — Step-by-step (Approach C vs A pick + sourceMod-threading + Items 1b/1c/1d/1e + Item 2 + docs).
   - **Verification performed** — pre-fix probe FAIL trace + post-fix probe PASS trace + coverage-smoke counts (382 → 383, 376 → 377 PASS) + drift-detection diff confirmation + new bridge SHA.
   - **Bugs surfaced** — any new bug. Likely none; flag if anything.
   - **Deviations from plan** — anything different from this kickoff (especially if Approach A used instead of C, or if a new failure mode surfaced).
   - **Known issues / open questions** — anything Phase 5 needs to know.
   - **Conductor asks** — only if questions remain.
   - **Preconditions for Phase 5** — bridge built; new SHA captured; live install still at P2D SHA until Phase 5 syncs; Layer 3 re-run mandatory per PHASE_3_HANDOFF.md (Scenario 3.1 lifts from BLOCKED → PASS post-fix; Scenario 3.2 confirms no regression of existing 12/12 PASS).
   - **Files of interest for Phase 5** — bridge SHA + path; coverage-smoke and race-probe entry points; PHASE_3_HANDOFF.md scenarios assertion checklists; PLAN.md § Phase 5 ship sequence.
3. **Do NOT write Phase 5's kickoff prompt.** Conductor owns the ship-sequence kickoff.
4. Force-add new files (`git add -f <plan>/{PHASE_4_INFO_HANDOFF.md,PHASE_4_INFO_KICKOFF_PROMPT.md}`).
5. Push the double-commit chain (work + hash-record).

## What "good" looks like

- A `[v2.9 P4-INFO]` work-commit diff that reads as the inverse of Phase 3's bug surface: the architectural mismatch Phase 3 flagged is now gone; the INFO branch in `CopyAsOverride` has the parent-topic-resolution shape Phase 4 documented; the symmetric `TryRemoveOverride` path matches Option α; the race-probe archaeology section is replaced with a proper regression that future v2.9.x runs can re-verify against.
- A coverage-smoke `1.P.GetIsID.INFO` cell that exercises Scenario 3.1's exact shape — when Phase 5 re-runs Scenario 3.1 against the post-Phase-4-INFO bridge, the live readback should match the in-process readback exactly (live test = scaled-up unit test).
- A handoff that lets Phase 5's executor read in 5 minutes: bridge SHA, what changed, what to verify in scenario re-runs, no surprises.
- An architectural archaeology section in race-probe that's been replaced (not just supplemented) — the deferred-state diagnostic was always temporary; with the fix landed, the archaeology becomes redundant noise. Replacing it with the proper FAIL→PASS regression is the cleaner long-term shape.

---

Confirm you've identified yourself as Phase 4-INFO sub-session executor + state-checks pass + bridge SHA matches Phase 4 baseline `a69179b3…2a7`, then reflect on `IDialogResponsesGetter` for Approach C availability before proposing your detailed work order.
