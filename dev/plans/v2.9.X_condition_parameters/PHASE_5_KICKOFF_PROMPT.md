# Phase 5 Kick-off — v2.9.0 ship sequence (final verify + publish + live sync + tag + release)

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 5 executor** for the v2.9.0 Claude_MO2 release — the ship sequence. Your job is to (1) re-verify the dispatcher in three independent ways (coverage-smoke + race-probe + live Layer 3 scenarios), (2) produce the canonical v2.9.0 ship bridge SHA via `dotnet publish`, (3) build the installer via direct ISCC invocation that PRESERVES that SHA byte-for-byte, (4) sync live install to v2.9.0 with Aaron's explicit go-ahead, (5) sanity-check live, (6) draft GitHub release notes for Aaron's explicit ship approval, (7) tag + push + create GitHub release, (8) update memory + write handoff. **This is the ceremonial close-out** — public action (tag/release) requires Aaron's explicit "ship" go-ahead before you execute it; the MANDATORY HALT before tag/push is non-negotiable.

## Context (read this once, don't search for history)

v2.9.0 dispatcher is feature-complete after Phase 2 (199 wired functions across 5 of 6 PLAN-named branches; Boolean deferred to v2.9.x). Phase 3 surfaced the INFO override gap; Phase 4 landed the line-180 DX bonus-catch + deferred INFO override; Phase 4-INFO sub-session landed INFO override via the corrected architectural pattern (parent-topic resolution + explicit child-DeepCopy + Option α rollback + idempotency check). All bridge code is in. Origin/main HEAD `53ef08a` (P4-INFO hash-record). Repo bridge SHA `1b54e8eb5b975727d07c19940ca238bcd4e2e7afca2e64e77d0638d333f2a3dd` (P4-INFO `dotnet build`). Coverage-smoke at 383 cells (377 PASS + 6 SKIP + 0 FAIL). Race-probe ALL PASS. Live install at `<live>/` is still at P2D `2e3a1094…f8293975e` (Phase 3's sync; Phase 4 / 4-INFO didn't sync). **Phase 5 produces the canonical ship SHA via `dotnet publish`** — that's a different SHA from P4-INFO's `dotnet build` (publish optimizes differently); the publish SHA is the v2.9.0 ship anchor and must be byte-identical across the final coverage-smoke run, the installer bundle, and the live install.

## Path conventions

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` |
| `<plan>` | `<repo>\dev\plans\v2.9.X_condition_parameters\` |

Quote paths in shell commands.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `53ef08a…` (P4-INFO hash-record commit).
   - Working tree clean.
   - Repo bridge SHA `1b54e8eb5b975727d07c19940ca238bcd4e2e7afca2e64e77d0638d333f2a3dd` (P4-INFO `dotnet build` output). Confirm via `sha256sum <repo>/tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`.
   - **`mo2_ping` MUST return** — if mo2_* tools aren't in your tool list, MO2's Claude Server is offline. Halt and ask Aaron to start it before any further work; Phase 5's live re-runs and live sync verification BOTH require mo2_*. **Expected return**: `version: "2.9.0"` (Phase 3 synced live to P2D's bridge SHA + tools_patching.py; Phases 4 / 4-INFO didn't re-sync — live still reports 2.9.0 from version constants which were bumped in P2A but live's BRIDGE behavior is P2D's, missing INFO override).
   - `git log --oneline -10` to confirm the chain: P0/P1/P2A/P2B/P2C/P2D/P3/P4/P4-INFO all landed cleanly, hash-record commits paired with each work commit.
2. **Read these files in full, in order:**
   - `<plan>/PLAN.md` § Session-start ritual + § Phase 5 + § Conventions (bridge SHA preservation chain) + § Handoff template + § Communicating with the conductor + § E (cross-phase decisions).
   - `<plan>/PHASE_4_INFO_HANDOFF.md` — most-recent state. Captures the Bridge SHA + the architectural correction (CopyDialogResponseAsOverride pattern) for the CHANGELOG narrative + Layer 3 re-run preconditions.
   - `<plan>/PHASE_4_HANDOFF.md` — the line-180 DX bonus-catch context for the CHANGELOG narrative.
   - `<plan>/PHASE_3_HANDOFF.md` § Scenario 3.1 + § Scenario 3.2 assertion checklists — Phase 5 re-runs against these exact assertion sets. § Preconditions for Phase 5 explicitly captures both as "re-run required" per PLAN § Phase 5 conductor decisions.
   - `<plan>/PHASE_2D_HANDOFF.md` § Phase 2 closing summary — the canonical "v2.9.0 dispatcher capability surface" narrative for the GitHub release notes draft.
3. **Skim, don't memorize:**
   - `<repo>/dev/plans/v2.8.0_verification/PHASE_5_HANDOFF.md` — **canonical 12-step ship-sequence exemplar.** Mirrors structure (state checks → coverage-smoke → Layer 3 re-run → publish → installer → live sync → sanity → CHANGELOG date → MANDATORY HALT → tag → release → memory → handoff → commits), not content. Aaron's "ship" go-ahead pattern is in v2.8.0 P5's halt-and-report block.
   - `<repo>/installer/claude-mo2-installer.iss` — the Inno Setup script. v2.9.0 version constant landed in P2A bump; ISCC compiles directly without rebuilding the bridge.
   - `<repo>/build-output/` — where `dotnet publish` and ISCC outputs land. Verify path conventions match v2.8.0.

## Conductor decisions inherited (locked — do not re-litigate)

1. **Version slug = `v2.9.0`.** No re-bump (P2A bumped). Phase 5 inserts ship date in CHANGELOG (`## v2.9.0 — TBD` → `## v2.9.0 — 2026-04-27`).
2. **Bridge SHA preservation chain is non-negotiable.** Phase 5's `dotnet publish` produces the canonical ship SHA (different from P4-INFO's `dotnet build` SHA — publish optimizes differently). That publish SHA must be byte-identical across:
   - Final coverage-smoke run (re-built coverage-smoke against publish-output bridge)
   - Installer bundle (ISCC reads publish output directly)
   - Live install (post-sync `<live>/tools/mutagen-bridge/mutagen-bridge.exe` matches publish SHA)
   **Build installer via direct ISCC invocation** (NOT `build-release.ps1 -BuildInstaller`, which rebuilds the bridge and breaks the chain — see PLAN § Phase 5 conductor decisions). Capture publish SHA before ISCC; verify post-ISCC bundle still matches; verify post-sync live still matches.
3. **Layer 3 Scenario 3.1 + 3.2 BOTH re-run** per PHASE_3_HANDOFF.md § Preconditions for Phase 5 + PLAN § Phase 5 conductor decisions ("Layer 3 workflow re-run is required if Phase 4 ran" — Phase 4 ran, including the sub-session, so re-run is mandatory). Scenario 3.1 lifts from BLOCKED → PASS (verifies INFO override fix end-to-end against live); Scenario 3.2 confirms no regression of existing 12/12 PASS (verifies the dispatcher hasn't subtly drifted).
4. **MANDATORY HALT before tag + push tag + GitHub release** — public, hard-to-undo action. Show Aaron the prepared release-notes draft, the exact `git tag` + `git push` + `gh release create` command sequence, the bridge SHA chain summary (publish/installer/live all matching), the triple-anchor regression results (coverage-smoke 383 cells / race-probe ALL PASS / Layer 3 both scenarios PASS). Wait for explicit "ship" go-ahead. Without that go-ahead, do NOT push the tag or create the release.
5. **Live install sync gated on Aaron's go-ahead at each filesystem-touching step** — same ritual as Phase 3 Step 1. Propose specific cp commands; Aaron approves; you execute; Aaron full-restarts MO2 (NOT just Tools menu Stop/Start — full process restart so Python interpreter reloads + bridge subprocess restarts cleanly); `mo2_ping` returns `version: "2.9.0"` post-sync; live bridge SHA matches publish SHA byte-for-byte.
6. **Full MO2 process restart after live sync.** Per CLAUDE.md "MO2 doesn't reload Python modules on server stop/start." Aaron's restart is mandatory; coverage of P3 sync ritual repeats here.
7. **Memory update is to `project_capability_roadmap.md`** per the conductor instructions' end-of-release ritual. The v2.8.0 entry is the template; v2.9.0 entry adds the dispatcher narrative + plan archive pointer.

## Phase 5 deliverables (12-step ship sequence per PLAN § Phase 5)

| # | Step | Notes |
|---|---|---|
| 1 | Session-start state checks | per Session-start ritual above |
| 2 | Final coverage-smoke run against P4-INFO bridge SHA | Confirm 383 cells, 377 PASS + 6 SKIP + 0 FAIL stays green pre-publish |
| 3 | Layer 3 re-runs — both Scenario 3.1 + 3.2 against post-Phase-4-INFO bridge | Live install IS at P4-INFO bridge yet (Phase 4 / 4-INFO didn't sync; this is a tactical decision — see HALT 1 below) |
| 4 | Build production bridge via `cd tools/mutagen-bridge && dotnet publish -c Release` | Produces canonical v2.9.0 ship SHA. Capture: `sha256sum <publish-output-path>/mutagen-bridge.exe` |
| 5 | Build installer via direct `ISCC.exe <repo>/installer/claude-mo2-installer.iss` (NOT build-release.ps1) | Reads publish output; preserves bridge SHA. Capture installer .exe SHA. Verify bundled bridge SHA inside installer matches publish SHA. |
| 6 | **Live sync** — copy publish-output bridge + Python files to `<live>/`. Aaron full-restarts MO2. | Same Phase 3 ritual; gated on Aaron's go-ahead. Verify post-sync `mo2_ping` returns `2.9.0` AND live bridge SHA matches publish SHA |
| 7 | Live sanity check — 2–3 representative scenarios via `mo2_create_patch` | One in-scope condition (e.g. GetIsID on MGEF) + one out-of-scope-error case (e.g. Sub-B function with parameters → clean error) + one regression (Tier D negative or Effects-list write — proves no v2.8.0 regression) |
| 8 | Insert ship date in CHANGELOG | `## v2.9.0 — TBD` → `## v2.9.0 — 2026-04-27` (or whatever today's date is per the system's currentDate). Adjust top-brief paragraph to past tense ("Phase 2 landed..." → "Phase 2 landed..."; the date is the only required edit but feel free to lightly polish if anything reads as draft-state). |
| 9 | **MANDATORY HALT** — show Aaron: release-notes draft + exact tag/push/release command sequence + bridge SHA chain summary + triple-anchor regression results. Wait for explicit "ship" go-ahead. | Public, hard-to-undo. **Do not proceed without Aaron's word.** |
| 10 | After Aaron's "ship": `git tag v2.9.0 <head-sha>` + `git push origin v2.9.0` + `gh release create v2.9.0 --title "v2.9.0 - <one-line>" --notes-file <release-notes-path> <installer-path>` | Use the prepared release notes from Step 9; attach installer .exe |
| 11 | Update memory `project_capability_roadmap.md` | Add v2.9.0 entry mirroring v2.8.0's structure: ship date + dispatcher narrative (199 functions / 5 branches / Boolean+sub-B deferred) + plan archive pointer (`<plan>` path) |
| 12 | Write `PHASE_5_HANDOFF.md` per template + final work commit + hash-record commit + push | Final commits close v2.9.0's git chain |

## Triple-commit cadence (ship date + tag chain + handoff)

Phase 5 has more commits than other phases due to the public-action sequence:

1. **CHANGELOG ship-date commit (pre-tag):** `[v2.9 P5] Insert ship date 2026-04-27 in CHANGELOG`. Single-file change. Push.
2. **Tag** (after Aaron's "ship" go-ahead): `git tag v2.9.0 <head-sha>` + `git push origin v2.9.0`. NOT a commit — a tag.
3. **GitHub release** (after tag pushes): `gh release create v2.9.0 …`. NOT a commit — uses gh CLI.
4. **Memory + handoff commit:** `[v2.9 P5] Ship v2.9.0 — memory updated + handoff`. Includes `project_capability_roadmap.md` update + PHASE_5_HANDOFF.md. Push.
5. **Hash-record commit:** `[v2.9 P5] Handoff: record commit hash <previous-work-hash>`. Push.

End each subject line with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line bodies.

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 5 executor" + confirm session-start state (HEAD + repo bridge SHA + tree clean + `mo2_ping` returns + live bridge SHA at P2D for now).
2. Recap the 12-step sequence in your own words.
3. Propose the live-sync-timing call: **Default sequence**: re-run Layer 3 scenarios FIRST against the P4-INFO bridge (in-process via mo2_create_patch hitting the live-installed P2D bridge would FAIL Scenario 3.1 since live is still at P2D — so re-run requires sync first). **Therefore reorder Steps 2-7 to**: Step 2 (coverage-smoke pre-publish) → Step 4 (publish) → Step 5 (installer) → Step 6 (live sync of publish SHA — this is Aaron's go-ahead point) → Step 3 (live Layer 3 re-runs against synced publish bridge) → Step 7 (live sanity). The reorder gets the canonical ship SHA into live BEFORE re-running Layer 3, so the re-runs verify against the actual ship binary, not the build-output binary. Any issue with this reorder, escalate; otherwise propose this as the default work plan.
4. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **HALT 1 — Pre-publish coverage-smoke + Layer 3 re-run timing call.** After Step 2 coverage-smoke green confirmed against the P4-INFO bridge, present the reorder rationale (Layer 3 re-runs require live sync to publish SHA; default sequence puts publish + sync before live re-runs). Aaron approves the reordered sequence.
- **HALT 2 — Post-publish + post-installer SHA chain verified.** After Steps 4 + 5: publish SHA captured, installer .exe SHA captured, bundled-bridge SHA inside installer extracted and verified to match publish SHA byte-for-byte. Show Aaron the SHA chain.
- **HALT 3 — Pre-live-sync filesystem touch.** Mirror Phase 3 Step 1 propose-then-execute: specific cp commands, source paths (publish output, not bin/Release/net8.0), destination paths, `rm -rf <live>/__pycache__/`, the explicit ask for Aaron's MO2 full-restart.
- **HALT 4 — Post-live-sync: `mo2_ping` 2.9.0 + live bridge SHA matches publish SHA byte-for-byte.** Show Aaron the verification trace.
- **HALT 5 — Post-Layer-3-re-run.** Both scenarios run; assertion tables captured. Scenario 3.1 PASSes (lifts from BLOCKED); Scenario 3.2 confirms 12/12 PASS unchanged. Show Aaron the cross-scenario rollup.
- **HALT 6 — Post-live-sanity.** 2–3 sanity scenarios green. Modlist clean post-cleanup; Aaron F5'd.
- **HALT 7 — MANDATORY (PUBLIC ACTION GATE)** — Step 9: release-notes draft + tag/push/release command sequence + SHA chain summary + triple-anchor regression results. **Aaron's explicit "ship" go-ahead required.** Without it, do not proceed to Step 10.

## Mandatory halt-and-report triggers (any → halt immediately)

- Any of the 376 PASS coverage-smoke cells starts failing at any point.
- Bridge `dotnet publish` fails or produces a SHA that's the same as P4-INFO's `dotnet build` SHA (publish optimizations should produce a different binary; same SHA = either publish skipped or environment misconfigured).
- ISCC build fails or produces an installer whose bundled bridge SHA doesn't match publish SHA (the SHA preservation chain broke; investigate before retry — do NOT use build-release.ps1 as a workaround).
- Post-live-sync `mo2_ping` returns anything other than `2.9.0` OR live bridge SHA doesn't match publish SHA.
- Layer 3 Scenario 3.1 doesn't lift to PASS post-fix (would mean Phase 4-INFO's fix has a live-vs-in-process discrepancy — major).
- Layer 3 Scenario 3.2 regresses from 12/12 PASS (would mean a subtle drift between P3-tested and P5-tested bridge — major).
- Live sanity check fails on the regression scenario (would mean a v2.8.0 capability silently broke — major).
- Aaron does not give explicit "ship" go-ahead at HALT 7. Do NOT push the tag or create the release. Treat this as a non-failure halt — Phase 5 stays paused; conductor decides what to do.
- Bonus-catch surfaces > 1h additional or new operator surface.
- `gh release create` fails after tag pushes (recoverable: investigate; the tag is independently retrievable).
- Memory `project_capability_roadmap.md` update conflicts with prior memory state (investigate before overwriting).

## Acceptance criteria (Phase 5 complete = v2.9.0 shipped)

- Coverage-smoke against publish-output bridge: 383 cells, 377 PASS + 6 SKIP + 0 FAIL.
- Race-probe against publish-output bridge: ALL PASS.
- Layer 3 Scenario 3.1: PASSes (lifts from Phase 3's BLOCKED) — INFO override end-to-end via live `mo2_create_patch` + `mo2_record_detail` readback.
- Layer 3 Scenario 3.2: 12/12 PASS unchanged from Phase 3 (no regression).
- Live sanity check: 2–3 representative scenarios PASS.
- Bridge SHA chain: publish SHA = installer-bundled SHA = post-sync live SHA (single byte-identical anchor).
- CHANGELOG `## v2.9.0 — TBD` → `## v2.9.0 — 2026-04-27` (or current date).
- `git tag v2.9.0` + `git push origin v2.9.0` + `gh release create v2.9.0 …` all succeed.
- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.0` resolves with installer .exe attached.
- `<live>/` running v2.9.0 (`mo2_ping`).
- Memory `project_capability_roadmap.md` reflects v2.9.0 shipped with dispatcher narrative + plan archive pointer.
- Handoff under 400 lines.

## Out of scope for Phase 5

- Bridge code changes (Phase 4 + Phase 4-INFO closed code work).
- Coverage-smoke or race-probe additions (Phase 4-INFO closed test work).
- Schema changes (Phase 4-INFO closed schema work).
- Plan-amend (none expected).
- Version bump (P2A bumped; P5 only inserts ship date in CHANGELOG).
- New scenarios beyond Layer 3.1/3.2 + 2-3 sanity (re-runs only, no new test surface).
- Other v2.7.1 / v2.8.0 carryovers + Boolean dispatcher branch + sub-B 6 functions (deferred to v2.9.x).
- Any modlist changes beyond test-patch creation/cleanup (do NOT enable/disable mods, do NOT touch loadorder, do NOT modify any non-Claude-Output content).

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `<plan>/PHASE_5_HANDOFF.md` per PLAN.md § Handoff template:
   - **What was done** — final coverage-smoke + race-probe + Layer 3 re-runs + publish + installer + live sync + sanity + CHANGELOG date + tag + release + memory.
   - **Verification performed** — counts, SHAs (publish/installer/live all matching), assertion tables, release URL, mo2_ping post-sync, memory diff.
   - **Bugs surfaced** — any. Likely none if Phase 4-INFO landed cleanly.
   - **Deviations from plan** — anything different from this kickoff (especially the Step 2-7 reorder if applied; document the reorder rationale was conductor-pre-authorized).
   - **Known issues / open questions** — for v2.9.x point releases (Boolean dispatcher branch, sub-B 6 String functions, anything Phase 4 / 4-INFO surfaced as future work).
   - **Conductor asks** — none expected; Phase 5 closes the conductor session.
   - **Final v2.9.0 capability surface summary** — restate the 199-function dispatcher coverage + 5/6 branches + deferrals + total cell counts (382 P2D baseline + 1 P4-INFO new = 383) + race-probe count (15+1 P4-INFO regression = 16) + bridge SHA chain.
3. **Do NOT spawn anything else.** Phase 5 is the last phase. Conductor session reads this handoff and closes.
4. Force-add new files (`git add -f <plan>/{PHASE_5_HANDOFF.md,PHASE_5_KICKOFF_PROMPT.md}`).
5. Push the final commit chain.

## What "good" looks like

- A `dotnet publish` SHA that's the canonical ship anchor — captured prominently in handoff, matched against installer-bundled SHA + post-sync live SHA = single byte-identical chain.
- An installer .exe at `<repo>/build-output/installer/claude-mo2-setup-v2.9.0.exe` whose bundled bridge SHA matches the publish output exactly (use the ISCC log to verify or extract the bundled .exe and sha256sum it).
- A GitHub release at `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.0` with installer attached, release notes summarizing the v2.9.0 capability surface (199 dispatcher-wired Condition functions + INFO override + line-180 DX bonus-catch + Boolean/sub-B/NoParam deferral context).
- Layer 3 re-run output that reads as a "Phase 3 redux but with 3.1 lifted" — Scenario 3.1's exact assertion checklist (3.1.A–3.1.I) all PASS this time; Scenario 3.2's 12/12 PASS holds.
- Memory update that future sessions can read in 30 seconds: "v2.9.0 shipped 2026-04-27 — generic Condition-parameter dispatcher (199 functions across 5 branches: FLI/IFormLink/Enum/Int32/Single; Boolean+sub-B deferred to v2.9.x); INFO override via parent-topic-resolution+child-DeepCopy pattern; line-180 error-message DX bonus-catch. Plan archive at `Claude_MO2/dev/plans/v2.9.X_condition_parameters/`."
- A handoff that's the v2.9.0 release narrative — future v2.9.x readers can pick up the capability surface + carryover list + architectural patterns from this single doc.

## After Phase 5 closes

The conductor session reads PHASE_5_HANDOFF.md and runs the end-of-release ritual:
- Confirm GitHub release tag/v2.9.0 resolves with installer attached.
- Confirm `<live>/` is at v2.9.0 via mo2_ping.
- Confirm memory updated.
- Confirm SHA chain matches.
- Tell Aaron: "v2.9.0 shipped. Conductor session done. Plan archive at `Claude_MO2/dev/plans/v2.9.X_condition_parameters/`."
- Stop.

You don't run those steps; the conductor does after your handoff lands.

---

Confirm you've identified yourself as Phase 5 executor + state-checks pass + repo bridge SHA matches P4-INFO baseline `1b54e8eb…2a3dd` + `mo2_ping` returns `2.9.0`, then propose the reordered work plan (Steps 2 → 4 → 5 → 6 → 3 → 7 → 8 → 9-MANDATORY-HALT → 10 → 11 → 12) for Aaron's approval before any execution.
