# Phase 0 Handoff — Scoping + plan archive — v2.9.4 Auto-stop deny-list

**Phase:** 0 (scoping, bundled)
**Status:** Complete
**Date:** 2026-04-29 evening
**Session length:** ~30 min scoping conductor wall-clock
**Commits made:** Single `[v2.9.4 P0]` commit bundling PLAN + CONDUCTOR_KICKOFF + this handoff + the pre-existing SCOPING_HANDOFF (force-add per `dev/` gitignore convention)
**Live install state at Phase 0 close:** `<live>/__init__.py` already carries the deny-list change (synced 2026-04-29 12:26 during the daemon viability research session). Version still reports `2.9.3` from `<live>/config.py` since the version bump is a Phase 1 deliverable.

## Scoping context inherited

The prior session's conductor authored `<plan>/SCOPING_HANDOFF.md` after the live empirical validation passed (2026-04-29 evening). That document:

- Recorded the empirical viability test outcome (13/13 MCP queries during a 6-min xEdit session, MO2 log smoking gun, record-index lazy build during USVFS-setup window — all clean).
- Pre-staged the deny-list code change to live (`<live>/__init__.py` deployed 2026-04-29 12:26).
- Identified the v2.9.4 ship-side work as the logistics tail (version bump, doc audit, coverage-smoke, build chain, live re-sync, sanity, pre-tag, tag/push/release, memory).
- Posed Q1–Q6 (slug, scope expansion, PERK PEPMA absorption, phase structure, coverage-smoke gate, pre-tag re-test) for Aaron to lock before this scoping session writes the plan archive.
- Suggested defaults for each.

This scoping session received that handoff, read the authoritative files (research SUMMARY, autostop investigation, PERK PEPMA diagnosis, CHANGELOG, KNOWN_ISSUES, v2.9.3 PLAN/KICKOFF/PHASE_5 templates), and surfaced Q1–Q7 to Aaron in a single message (Q7 added by this session — bridge re-publish hygiene per the SCOPING_HANDOFF anti-patterns footnote was Aaron-call shape, not auto-default shape).

## Q1–Q7 locks (Aaron 2026-04-29)

| # | Topic | Lock | Rationale |
|---|---|---|---|
| Q1 | Slug | `v2.9.4_autostop_denylist` | Aaron picked the mechanism-named option over the capability-named (`v2.9.4_xedit_clarity`) and surface-named (`v2.9.4_mcp_xedit_coexistence`) alternatives. |
| Q2 | Write-time detection-and-warn | DEFER to v3.0 daemon | v2.9.4 stays a pure deny-list ship at minimum risk; daemon's persistent state makes detection cleaner architecturally. |
| Q3 | PERK PEPMA Float-flag entry | NO absorption | Aaron: "this plugin has an error, nothing to fix in my view." Existing KNOWN_ISSUES § Environmental quirks neighbors (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) already cover the malformed-plugin category; no new doc surface needed. |
| Q4 | Phase structure | Compress to single Phase 1 (no separate doc-audit / version-bump / coverage-smoke / build / ship phases) | Release is mostly logistics; doc-audit-mandatory cadence still preserved as Halt 1c within Phase 1. |
| Q5 | Coverage-smoke gate | RUN full coverage-smoke at v2.9.4 SHA | Cheap insurance + doc-audit-mandatory memory implies full pre-ship verification anyway. |
| Q6 | Aaron-side empirical re-test timing | PRE-TAG re-test at post-build live SHA | Defensive against the released-archive-carries-stale-validation failure mode `feedback_conductor_doc_audit.md` warns against. ~5 min added to Halt 3 sanity sequence. |
| Q7 | Bridge `dotnet publish` re-spin | A — re-publish at same source for SHA-chain hygiene | Per `feedback_build_artifact_versioning.md` spirit (rebuild ≠ overwrite when version bumps). v2.9.4 has zero bridge code changes; SHIP SHAs may differ from v2.9.3's only by .NET build determinism factors. SHA chain recorded fresh for v2.9.4 either way. |

## Why phase compression (Q4 = single Phase 1) is sound

The release ships **one** logical capability that has already been validated empirically. The v2.9.3 5-phase shape (Phase 0 scoping → Phase 1 audit → Phase 2 implementation → Phase 3 verification → Phase 4 fixes-or-skip → Phase 5 ship) was justified there by a non-trivial implementation effort (factory pattern, abstract-subclass discriminator, 12-leaf inventory). v2.9.4 has none of that — implementation is a 28-line regex addition + 5-line `_on_about_to_run` modification, already in the working tree, already empirically tested.

What remains is mechanical: bump version → audit docs → run smoke → publish bridge → ISCC → sync → restart → sanity → pre-tag → tag/push/release. Every step has clear acceptance criteria + standard tooling. Compression to a single Phase 1 with 4 halts (matching v2.9.3 P5's halt cadence) preserves all the discipline (mandatory pre-installer-build doc audit, pre-tag mandatory, SHA-chain hygiene) without inventing artificial phase boundaries.

## What was scoped (deliverables of Phase 0)

1. ✅ **`<plan>/PLAN.md`** — full plan with path conventions, session-start ritual, background, architecture, locked decisions table, out-of-scope, Phase 0 + Phase 1 sections, communicating-with-Aaron block, handoff template.
2. ✅ **`<plan>/CONDUCTOR_KICKOFF.md`** — execution conductor entry-point paste-text. Confirms role (single-phase ship, executor + cross-halt coordinator merged), names the file reading list, recaps Phase 1 halt structure, locks the Q1-Q7 list, lays out decision-ownership table + escalation format, end-of-release ritual.
3. ✅ **`<plan>/PHASE_0_HANDOFF.md`** (this file) — bundled per v2.9.3 pattern. Records the Q1–Q7 locks and Phase 0 → Phase 1 handoff context.
4. ❌ **`<plan>/MATRIX.md`** — explicitly skipped. The deny-list change is so narrow that explicit matrix testing is overkill; the SCOPING_HANDOFF agreed that "a single-cell smoke test (verify exempt log line + verify post-launch query succeeds) might suffice." Halt 3 Path (a) is that single-cell smoke. If Phase 1 surfaces a need for matrix-style verification mid-ship, Phase 1 escalates to Aaron rather than expanding silently.

## What is NOT scoped (deferred or out)

Per the locks + § Out of scope of PLAN.md:

- xEdit write-time detection-and-warn (Q2 → v3.0 daemon territory).
- Synthesis exemption (§ C lock — easy to revisit via 1-line regex extension if consumer signal surfaces).
- PERK PEPMA Float-flag KNOWN_ISSUES.md entry (Q3 → no doc surface, no code action).
- Bridge code changes (zero — v2.9.4 is Python-only at the source level; Q7 just re-publishes the same source).
- v3.0 daemon mode (separate workstream; v2.9.4 ships in per-call subprocess architecture).
- L0/L1/L2/L3 daemon work (per `<workspace>/research/SUMMARY.md` § Recommended Next Steps; resumes after v2.9.4 ships).
- Read-surface candidates (reverse-link search, override-aware FormLink expansion, MaxDepth MCP-configurable, cross-call result caching — v2.9.x candidates per KNOWN_ISSUES).

## Files touched by Phase 0

| Path | Operation |
|---|---|
| `<plan>/` | Renamed from `v2.9.4_xedit_clarity` (working slug from prior conductor) → `v2.9.4_autostop_denylist` (Aaron's Q1 lock) |
| `<plan>/PLAN.md` | NEW |
| `<plan>/CONDUCTOR_KICKOFF.md` | NEW |
| `<plan>/PHASE_0_HANDOFF.md` | NEW (this file) |
| `<plan>/SCOPING_HANDOFF.md` | UNCHANGED content; included in the Phase 0 commit (force-add) for archival completeness — it was previously untracked. |

The Phase 0 commit subject is `[v2.9.4 P0] Plan archive — auto-stop deny-list (xEdit-clarity capability)`. Force-add (`git add -f`) per the `dev/` gitignore convention used across all v2.9.x plan archives.

**Note:** `<repo>/mo2_mcp/__init__.py` carries the deny-list code change (uncommitted on `main`). Phase 0 does NOT stage or commit that file — it is Phase 1's work-commit deliverable. Phase 0's commit is plan-archive-only.

## Bugs surfaced

None. Phase 0 is documentation-only.

## Deviations from the SCOPING_HANDOFF's recommended defaults

1. **Q1 — Slug.** SCOPING_HANDOFF defaulted `v2.9.4_xedit_clarity`; Aaron locked `v2.9.4_autostop_denylist`. Mechanism-named over capability-named. Surfaced in this session's first scoping-question message; Aaron's preference 1-line response.
2. **Q3 — PERK PEPMA absorption.** SCOPING_HANDOFF defaulted INCLUDE per Aaron's earlier "defer to v2.9.4 ship cadence" disposition; Aaron locked NO absorption with the rationale "this plugin has an error, nothing to fix in my view." This narrows the doc audit scope for Halt 1c — KNOWN_ISSUES.md updates are limited to the auto-stop entry + the new xEdit-clarity capability paragraph.
3. **Q4 — Phase structure.** SCOPING_HANDOFF defaulted 3-phase (Phase 0 + Phase 1 audit/version/smoke + Phase 2 ship); Aaron locked compress to Phase 1 (single phase covering everything). PLAN.md § Phase 1 absorbs the audit + version + smoke + build + sync + sanity + tag/push/release as 4 halts within one phase.
4. **Q7 added by this session.** SCOPING_HANDOFF surfaced bridge re-publish vs reuse only as an anti-patterns footnote ("Aaron-call; conductor leans toward re-publish"). This session promoted it to Q7 because the choice meaningfully affects Halt 1d's command sequence and the build-output SHA chain. Aaron locked A (re-publish).

## Phase 0 → Phase 1 handoff context

**State Phase 1 inherits:**
- Deny-list code change uncommitted on `main` at `<repo>/mo2_mcp/__init__.py` (top of v2.9.3 ship `5cae6a7`).
- Live install at `<live>/` synced 2026-04-29 12:26 carrying the deny-list shape; `<live>/config.py` still reports `2.9.3`.
- Plan archive committed in single `[v2.9.4 P0]` commit on `main`.
- Q1–Q7 locks in PLAN.md § ✅ Locked decisions.
- Empirical validation already passed at the dev-build SHA (per `<workspace>/research/SUMMARY.md` § "v3.0 viability empirical validation"); Q6's pre-tag re-test confirms at the post-build SHA.
- Coverage-smoke baseline at v2.9.3 ship: 449/455 (449 PASS + 6 documented SKIPs, all pre-v2.9.4 carry-overs). Halt 1a expects this baseline to hold.

**State Phase 1 produces:**
- v2.9.4 work commit with deny-list code change + version bump + doc audit + Phase 1 handoff.
- Hash-record commit fills work-hash placeholder in PHASE_1_HANDOFF.md.
- Tag `v2.9.4` on the work commit.
- Push to origin.
- `gh release create v2.9.4` with installer + release notes attached.
- `<live>/` synced to v2.9.4 with `mo2_ping` confirming `version: "2.9.4"`.
- Memory `project_capability_roadmap.md` updated with v2.9.4 entry.

## Conductor asks

**NONE.** Phase 0 complete; Phase 1 has full scope clarity from PLAN.md + CONDUCTOR_KICKOFF.md + this handoff.

## Files of interest

| Path | Why |
|---|---|
| `<plan>/PLAN.md` | Phase 1 reads § Phase 1 + § ✅ Locked decisions in full. |
| `<plan>/CONDUCTOR_KICKOFF.md` | Phase 1 starts here (paste-into-fresh-session paste-text). |
| `<plan>/SCOPING_HANDOFF.md` | Historical context for the framing of Q1–Q7 (pre-this-session). |
| `<workspace>/research/SUMMARY.md` § "v3.0 viability empirical validation" | Empirical record for the deny-list's dev-build-SHA validation. |
| `<workspace>/research/followups/mcp_autostop_investigation.md` § 3 + § 4 | v1.0.3 carryover-by-analogy rationale + minimum-change path; useful for CHANGELOG copy. |
| `<repo>/mo2_mcp/__init__.py` lines 96-103 + 238-260 | The actual deny-list code change Phase 1 commits. |
| `<repo>/KNOWN_ISSUES.md` § Environmental quirks (around line 160) | The auto-stop-on-launch entry Phase 1's Halt 1c updates. |
| `<repo>/README.md` line 7 + line 59 + line 133 | Installer-link bumps + auto-stop bullet update. |
| `<repo>/mo2_mcp/CHANGELOG.md` v2.9.3 entry | Format precedent for Phase 1's new v2.9.4 entry. |
| `<v2.9.3-plan>/PHASE_5_HANDOFF.md` | Halt cadence + ship sequence template for Phase 1. |
