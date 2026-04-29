# v2.9.4 Scoping Handoff — xEdit-clarity capability

**Author of this handoff:** prior session conductor, 2026-04-29 evening, after live empirical viability test passed.
**Audience:** the scoping session (a fresh Claude Code session Aaron opens to scope v2.9.4 and produce the plan archive).
**Suggested slug** (Aaron-locks): `v2.9.4_xedit_clarity`. Alternatives if you prefer different framing: `v2.9.4_mcp_xedit_coexistence`, `v2.9.4_autostop_denylist`. Pick one with Aaron in the first message.

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **scoping conductor** for v2.9.4 — the next Claude_MO2 point release after v2.9.3 (PERK.Effects writability, shipped 2026-04-29 morning).

Your job is to **produce the v2.9.4 plan archive** (`PLAN.md`, `CONDUCTOR_KICKOFF.md`, optional `MATRIX.md` and `PHASE_0_HANDOFF.md` if you bundle Phase 0). You are NOT the execution conductor for v2.9.4 — that role activates after Aaron opens a separate session with your `CONDUCTOR_KICKOFF.md`.

This release is **structurally unusual** for the v2.9.x series — read § "What makes this release unusual" carefully before scoping the phase structure. Most of the technical work is already done.

## What v2.9.4 is shipping

**xEdit-clarity capability for read-mode-during-active-xEdit.** When the user has xEdit open, Claude can issue MCP read queries (record_detail, query_records, conflict_chain, etc.) and get correct responses concurrently with xEdit's session. Today (v2.9.3 and earlier), MO2's auto-stop-on-launch behavior — added in v1.0.3 of the plugin to prevent a Skyrim-engine launch hang — also fires for xEdit launches as a carryover-by-analogy, killing the MCP server for the duration of any xEdit session.

The shipped change narrows the auto-stop scope: an `_AUTOSTOP_EXEMPT_PATTERN` regex matches the xEdit family (14 game-edition variants + version-suffix tolerance: `SSEEdit.exe`, `SSEEdit64.exe`, `xEdit.exe`, `xEdit64.exe`, `TES5Edit.exe`, etc.) so the server stays alive during xEdit's lifetime. Game launches and Synthesis still trigger the auto-stop unchanged (Synthesis was deliberately NOT exempted — it's a batch patcher with no concurrent-read use case and similar Mutagen-overlay shape to game launches).

**Real consumer signal:** the xEdit-clarity vision (auto-memory: `project_xedit_clarity_vision.md`) calls for Claude to "see as clearly as a user can in xEdit." The natural workflow is Claude reading records while the user has xEdit open looking at the same data. Today that workflow is impossible because the MCP server stops the moment xEdit launches.

## What makes this release unusual

**The implementation is done and live-validated already.** During v3.0 daemon-architecture viability research (2026-04-29 evening), the deny-list code change was applied to the live `Claude_MO2/mo2_mcp/__init__.py` and an end-to-end concurrent-MCP-during-xEdit test ran with Aaron in the loop. The test passed cleanly. See `<repo>/research/SUMMARY.md` § "v3.0 viability empirical validation" for the full record.

**What's already complete:**
- Source code change: `Claude_MO2/mo2_mcp/__init__.py` carries `_AUTOSTOP_EXEMPT_PATTERN` (regex) + modified `_on_about_to_run()` that early-returns on xEdit-family matches.
- Live install sync: `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\__init__.py` deployed 2026-04-29 12:26 (you can verify mtime).
- Empirical validation: 13/13 MCP queries succeeded during a live xEdit session including a 9.9 s record-index lazy build during xEdit's USVFS setup (the highest-risk concurrent-load scenario). MO2 log line 588 shows `keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` qInfo firing as designed. Zero errors or qWarnings throughout.
- Aaron's test patch (`test 3.esp`) was created in xEdit during the test and detected by `ensure_fresh` after MO2's directory refresh — full xEdit-clarity loop closes empirically.

**What's NOT done (the actual v2.9.4 ship work):**
1. Version bump: `mo2_mcp/config.py` `PLUGIN_VERSION` tuple, `installer/claude-mo2-installer.iss` AppVersion, `README.md` if it mentions a version, `mo2_mcp/CHANGELOG.md` new entry.
2. Doc audit + KNOWN_ISSUES.md update — see § "Required doc-audit absorptions" below.
3. Coverage-smoke run at the new SHA (verify the 449/455 baseline holds — the deny-list change touches NO patching paths, NO reading paths, only a plugin-lifecycle hook, so this should be a clean 449/455 PASS).
4. `dotnet publish` build chain → ISCC compile → SHA-chain audit.
5. Live install re-sync with the version-bumped binaries + docs.
6. Aaron MO2 full-restart + post-restart `mo2_ping` verifying `version: "2.9.4"`.
7. Live sanity check: confirm the deny-list still works after the version-bumped reinstall (basically Test C lite).
8. Pre-tag mandatory (Aaron approval) → tag + push + `gh release create`.
9. Memory updates: `project_capability_roadmap.md` gets a v2.9.4 entry.

## Required doc-audit absorptions (per `feedback_conductor_doc_audit.md`)

The conductor doc-audit (mandatory pre-installer-build per memory) MUST land these:

**1. KNOWN_ISSUES.md auto-stop-on-launch entry — UPDATE.**
Current text (KNOWN_ISSUES.md § Environmental quirks, around line 160):
> "Claude Code reconnects to the MCP server automatically after MO2's auto-stop-on-launch cycle (Skyrim / xEdit / etc.) or after a full MO2 restart..."

After v2.9.4: this needs to reflect that **xEdit no longer triggers auto-stop**. Suggested replacement (final wording is your call with Aaron):
> "Claude Code reconnects to the MCP server automatically after MO2's auto-stop-on-launch cycle (Skyrim / SKSE loaders / Synthesis / etc.) or after a full MO2 restart, as long as the server comes back on the same HTTP URL. As of v2.9.4, **xEdit launches NO LONGER trigger the auto-stop** — the server stays alive during xEdit sessions, enabling concurrent record queries while the user has xEdit open. (Game executables, Synthesis, and other tools whose Mutagen / VFS load profile matches the original v1.0.3 hang race continue to trigger the auto-stop as before.)"

Also worth a fresh § entry under Environmental quirks (or a dedicated § for new capabilities) summarizing the xEdit-clarity behavior, including the visibility-lag caveat (xEdit reads load order at startup; MCP-driven plugin writes mid-xEdit-session are invisible to xEdit until reload).

**2. KNOWN_ISSUES.md PERK PEPMA Float-flag entry — NEW.**
Bonus catch from the v3.0 daemon viability research (the OnlyIdentifiers measurement spike caught it; the perk_pepma_diagnosis spike isolated it). Single failing record: `Feat_Perk_Skill_HeavyArmor_DevastatingTackle` (FormKey `00080E:Requiem - Special Feats.esp`). Plugin-side malformation, NOT a Mutagen schema gap. Aaron approved bundling this into v2.9.4's pre-ship doc audit (per "defer to v2.9.4 ship cadence" decision in the prior session).

Draft entry text in `<repo>/research/followups/perk_pepma_float_flag_diagnosis.md` § "Recommendation for KNOWN_ISSUES.md". The executor's draft uses an `### Header + paragraph + bulleted impact` shape, but KNOWN_ISSUES.md § Environmental quirks uses inline bulleted paragraphs (matching the existing `TasteOfDeath_Addon_Dialogue.esp` / `ksws03_quest.esp` neighbors). Reformat to a single bullet starting `- **Requiem - Special Feats.esp PEPMA Float-flag malformation.**` — see § "Some plugins are rejected by Mutagen's strict parser" for the formatting pattern.

**3. CHANGELOG.md (`mo2_mcp/CHANGELOG.md`) — NEW v2.9.4 entry.**
Write a fresh entry above the v2.9.3 block. Headline: "xEdit-clarity capability — MCP server stays alive while xEdit is running." Mention the empirical validation, the regex deny-list, and the explicit choice to keep auto-stop firing for game launches and Synthesis. Reference the v3.0 daemon work as the next-tier amortization story (per-call 9-13 s subprocess cost during xEdit becomes sub-microsecond with daemon mode).

**4. README.md** — only if it mentions specific capabilities related to xEdit or auto-stop. Skim, decide.

## Open scoping questions for Aaron (raise these in your first message)

These are the calls Aaron needs to make to lock scope. Don't autonomously decide them.

**Q1: Slug.** Default suggestion: `v2.9.4_xedit_clarity`. Alternatives: `v2.9.4_mcp_xedit_coexistence`, `v2.9.4_autostop_denylist`. Aaron-locks per project convention.

**Q2: Scope expansion — xEdit write-time detection-and-warn?** During the v3.0 research, an executor produced `<repo>/research/followups/xedit_coexistence_detection.md` — a 316-line spec for daemon-side detection of running xEdit + warning emission when MCP-driven plugin writes happen during xEdit's lifetime (the "visibility lag" failure mode). The detection mechanism is a `Process.GetProcessesByName` poll at write-time, ~20 ms cost, parent-PID coupling for same-modlist confirmation.

This is **adjacent work** that would close the visibility-lag loop now that xEdit can be running concurrently. Two paths:
- **Include in v2.9.4 (expand scope):** Pro — closes the loop on the user-confusion failure mode at the same moment we unlock the capability. Con — separate code change in `tools_patching.py` write paths, expands ship risk.
- **Defer to v2.9.5 or v3.0 (keep v2.9.4 minimal):** Pro — v2.9.4 stays a pure deny-list ship, minimum risk, fast turnaround. Con — capability ships with a known UX rough edge.

Aaron's call. The conductor recommends DEFER (keep v2.9.4 surgical, fold detection-and-warn into v3.0 daemon work where it belongs architecturally — daemon's persistent state makes the detection-and-warn cleaner anyway). But Aaron may have UX urgency that argues for inclusion.

**Q3: PERK PEPMA Float-flag KNOWN_ISSUES entry.** Default: INCLUDE in this ship's doc audit (per Aaron's earlier "defer to v2.9.4 ship cadence" decision). Just confirming the default.

**Q4: Phase structure.** Given the unusual situation (implementation done, validation done), this release is mostly logistics. Suggested phase structure:

- **Phase 0** — Scoping + plan (this session bundles it).
- **Phase 1** — Pre-ship doc audit + version bump + coverage-smoke verification at the deny-list-applied SHA.
- **Phase 2** — Ship sequence: `dotnet publish` + ISCC + live sync + Aaron MO2 full-restart + sanity check + pre-tag mandatory + tag + push + release + memory update.

That's a 3-phase release (including Phase 0). Compare to v2.9.3's 5+phase structure (Phase 0 scoping, Phase 1 audit, Phase 2 implementation, Phase 3 verification, Phase 4 fixes-or-skip, Phase 5 ship). Aaron may want to compress further (single Phase 1 = "ship") or expand (split coverage-smoke into its own phase). Ask.

**Q5: Coverage-smoke gate scope.** The deny-list change touches no patching/reading code paths — only a plugin-lifecycle hook. The 449/455 baseline coverage-smoke from v2.9.3 should pass identically. Decision: do we run the full coverage-smoke at the v2.9.4 SHA (defensive, ~minutes of executor time) or skip on the basis that no relevant code path moved (faster)? Conductor recommends RUN — it's cheap insurance and the doc-audit memory implies a full pre-ship verification anyway. But Aaron may have shipped point releases without full smoke before.

**Q6: Should Aaron-side empirical re-test happen before tag, or post-tag?** The 2026-04-29 evening empirical test passed at the dev-build SHA. After Phase 2's ISCC compile + live re-sync, the live install will be at a NEW SHA. Path forward:
- **Pre-tag re-test:** Run a quick xEdit-launch + MCP query test at the post-build live SHA. Adds ~5 min to ship sequence; confirms the regex still works after build pipeline. Recommended by conductor — this is exactly the kind of "released archive carries stale validation" failure mode the doc-audit memory warns against.
- **Trust the source-level test:** Skip re-test, ship on the strength of the source-level validation. Faster but less defensive.

Aaron-call.

## Files to read (in order)

**Authoritative:**
1. **This file** (you're reading it) — but stop here, you have the context.
2. `<repo>/research/SUMMARY.md` § "v3.0 viability empirical validation" — full empirical record. Specifically the timeline, the smoking-gun MO2 log entry, and the strategic-implication discussion of v2.9.4 vs v3.0 sequencing.
3. `<repo>/research/followups/mcp_autostop_investigation.md` — source-side analysis that motivated the deny-list. Specifically § 1 (auto-stop location), § 3 (original crash rationale from CHANGELOG v1.0.3), § 4 (minimum-change path).
4. `Claude_MO2/mo2_mcp/__init__.py` — the live source. Specifically:
   - Lines 1-7: imports (note `import re` was added)
   - Lines ~75-105: `_AUTOSTOP_EXEMPT_PATTERN` regex constant + comment block
   - Lines ~244-275: modified `_on_about_to_run()` and unchanged `_on_finished_run()`
5. `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.3) — recent context for changelog formatting.
6. `Claude_MO2/KNOWN_ISSUES.md` § Environmental quirks — current auto-stop-on-launch entry (around line 160) + neighboring malformed-plugin entries (formatting precedent for the PERK PEPMA addition).

**For the bonus PERK PEPMA absorption:**
7. `<repo>/research/followups/perk_pepma_float_flag_diagnosis.md` — full diagnostic report + draft KNOWN_ISSUES.md entry text.

**For Q2 scope-expansion decision (only if Aaron wants to discuss inclusion):**
8. `<repo>/research/followups/xedit_coexistence_detection.md` — the 316-line detection-and-warn spec.

**Plan-archive precedents (read briefly for structure):**
9. `Claude_MO2/dev/plans/v2.9.3_perk_effects/CONDUCTOR_KICKOFF.md` — your structural template for v2.9.4's `CONDUCTOR_KICKOFF.md` deliverable.
10. `Claude_MO2/dev/plans/v2.9.3_perk_effects/PLAN.md` — structural template for v2.9.4's `PLAN.md`. Note: v2.9.4's plan is shorter because most technical work is done.
11. `Claude_MO2/dev/plans/v2.9.3_perk_effects/PHASE_5_HANDOFF.md` — ship sequence template (your Phase 2 will mirror this closely).
12. `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PHASE_5_HANDOFF.md` — alternate ship-sequence template.

## Memory references (auto-loaded, but worth re-reading)

- `feedback_build_artifact_versioning.md` — version bumping rules. Specifically: never overwrite versioned artifacts; bump config.py + .iss + README + CHANGELOG before rebuild; version locks at first build, even local-only.
- `feedback_conductor_doc_audit.md` — pre-installer-build mandatory doc audit. Owns KNOWN_ISSUES staleness during ship cadence. Post-ship cleanup commits are a failure mode.
- `feedback_write_surface_bonus_catch.md` — bonus catches escalate to Aaron. The deny-list change wasn't a write-surface bonus catch (it's a plugin-lifecycle change), but the PERK PEPMA absorption IS a doc-audit-time absorption rather than a write-surface change — closer to the ">1 h or new operator" bar than the bonus-catch bar. Worth checking with Aaron whether explicit approval needed for the absorption.
- `project_v3_release_strategy.md` — v2.9.x is the point-release vehicle. v2.9.4 fits this pattern. v3.0 still gated on threshold of completed work; v2.9.4 may slightly delay v3.0 by absorbing capacity, but the deny-list ship was effectively zero-cost (work was already done in research).
- `project_xedit_clarity_vision.md` — read for narrative framing in CHANGELOG / release notes. v2.9.4 is the first concrete shipped instance of the read-surface-equally-with-write pillar.
- `feedback_conductor_routing.md` — sign off on technical executor work plans directly; translate to Aaron only when business judgment is needed.

## Session-start ritual

1. **Confirm role.** State back to Aaron that you're the v2.9.4 SCOPING conductor. Distinguish from execution conductor (which activates after your `CONDUCTOR_KICKOFF.md` is in place and Aaron opens a fresh session with it).

2. **Lock the slug.** Q1 above. Aaron's call. Then the directory is `Claude_MO2/dev/plans/<locked-slug>/` — note that this `SCOPING_HANDOFF.md` is currently at `Claude_MO2/dev/plans/v2.9.4_xedit_clarity/SCOPING_HANDOFF.md`. If Aaron picks a different slug, rename the directory + this file in the move (standard git mv).

3. **Read the authoritative files** (1-6 above) in order. Read briefly — these give you the substance.

4. **Surface scoping questions Q2-Q6 to Aaron.** Recommend defaults, let him override. Don't write `PLAN.md` until Aaron has answered Q2-Q6 (Q4-Q6 in particular gate phase structure).

5. **Write the deliverables** once scope is locked:
   - `PLAN.md` — full plan (mandatory). Structure mirrors v2.9.3's PLAN.md but is shorter due to the unusual nature of this release.
   - `CONDUCTOR_KICKOFF.md` — execution conductor entry point (mandatory). Slug-locked, scope-locked, phase-structure-locked.
   - `MATRIX.md` — only if Aaron wants explicit matrix testing. The deny-list change is so narrow that a matrix may be overkill; a single-cell smoke test (verify exempt log line + verify post-launch query succeeds) might suffice.
   - `PHASE_0_HANDOFF.md` — only if you bundle Phase 0 into the scoping session per v2.9.3's pattern. Recommended: yes, for cadence consistency.

6. **Commit the plan archive.** Single `[v2.9.4 P0]` commit per project convention.

7. **Hand off to Aaron with the `CONDUCTOR_KICKOFF.md` paste-text.** End your session.

## Anti-patterns to avoid

- **Don't re-litigate the deny-list approach itself.** It's empirically validated. Your job is to ship it cleanly, not re-design.
- **Don't expand scope autonomously.** Q2 (write-time detection-and-warn) and any other surface expansions go to Aaron.
- **Don't skip the doc-audit.** Per memory, post-ship cleanup commits are a documented failure mode. KNOWN_ISSUES + CHANGELOG MUST land in the pre-installer-build commit.
- **Don't assume the empirical test trumps post-build re-test.** The 2026-04-29 evening test was at the dev source SHA. Phase 2's ISCC compile produces a new artifact at a new SHA. Q6 covers this — make Aaron aware.
- **Don't forget the bridge re-build is unnecessary.** The deny-list change is Python-only. `Claude_MO2/tools/mutagen-bridge/` has no changes; no `dotnet publish` re-spin needed for v2.9.4 unless Aaron wants a fresh build for SHA-chain hygiene. (Argument for fresh build: v2.9.4's installer should bundle the v2.9.4 plugin + the v2.9.3 bridge, but the SHA chain is cleaner with a re-publish at the same source. Argument against: bytewise-identical bridge means re-publish is wasted work. Aaron-call; conductor leans toward re-publish for SHA-chain hygiene per `feedback_build_artifact_versioning.md` spirit.)

## Post-ship cleanup checklist (for execution conductor reference)

After v2.9.4 tag + push + release:
- Memory `project_capability_roadmap.md` updated with v2.9.4 entry.
- `<repo>/research/SUMMARY.md` § "v3.0 viability empirical validation" gets a "shipped in v2.9.4" cross-reference at the top.
- `<repo>/research/PROGRESS.md` final entry notes v2.9.4 ship.
- The four follow-up artifacts in `<repo>/research/followups/` (mcp_autostop_investigation.md, xedit_coexistence_detection.md, onlyidentifiers_hybrid.md, perk_pepma_float_flag_diagnosis.md) stay in research — they're v3.0 input artifacts, not v2.9.4 deliverables.
- v3.0 daemon work resumes from where research left off; v2.9.4's deny-list ship doesn't change v3.0 scope (daemon-mode amortizes the per-call subprocess cost, which is a separate axis from xEdit-clarity and remains valuable).

---

**Closing context for the scoping session:** the headline narrative for v2.9.4 release notes is "Claude can now read while you're in xEdit." The follow-on for v3.0 is "...and reads become instant instead of taking 10 seconds each." That's a clean two-step value-prop story for users, and v2.9.4 ships the first half cheaply.

Good scoping. Ping Aaron with Q1-Q6 in your first message and get cracking.
