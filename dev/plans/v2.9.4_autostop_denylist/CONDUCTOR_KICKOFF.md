# Conductor Kick-off Prompt — v2.9.4 Auto-stop deny-list (xEdit-clarity capability)

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **execution conductor** for the v2.9.4 Claude_MO2 release (auto-stop deny-list — xEdit-clarity capability — the fourth v2.9.x point release after v2.9.1 + v2.9.2 + v2.9.3). v2.9.4 is **structurally unusual** for the v2.9.x series: the implementation is already complete and live-validated. Your job is the ship sequence — you are simultaneously the Phase 1 executor AND the cross-halt coordinator with Aaron, because Q4 was locked to compress this release into a single Phase 1 (no separate orchestration-conductor session).

Scoping was completed before you spawned. The plan archive is at `dev/plans/v2.9.4_autostop_denylist/`. **You do not re-scope.** You execute Phase 1 end-to-end: pre-ship verification + version bump + doc audit + build chain + live sync + sanity at SHIP SHA + pre-tag mandatory + tag + push + release + memory update.

## What this release is

v2.9.4 lands xEdit-clarity capability for read-mode-during-active-xEdit by narrowing MO2's auto-stop-on-launch to a deny-list that exempts the xEdit family. Today (v2.9.3 and earlier) the auto-stop fires on every executable launch including xEdit, killing the MCP server for the duration of any xEdit session. The shipped change — `_AUTOSTOP_EXEMPT_PATTERN` regex matching 14 game-edition variants × version-suffix tolerance + the user-renamed `xEdit.exe` wildcard — exempts xEdit so the server stays alive during xEdit's lifetime. Game launches and Synthesis still trigger the auto-stop unchanged (Synthesis intentionally NOT exempted — batch-patcher, no concurrent-read use case, similar Mutagen overlay shape to game launches).

**Real consumer signal:** Aaron's xEdit-clarity vision (auto-memory: `project_xedit_clarity_vision.md`) — "Claude should see as clearly as a user can in xEdit." Today that's blocked because the MCP server stops the moment xEdit launches. v2.9.4 ships the read half of the vision cheaply; the v3.0 daemon ships the latency-amortization half (per-call ~10 s → sub-microsecond) later.

**Empirical validation already complete.** During the 2026-04-29 evening daemon-architecture viability research, the deny-list code change was applied and tested live with Aaron in the loop: 13/13 MCP queries succeeded during a 6-minute xEdit session including a 9.9 s record-index lazy build during xEdit's USVFS-setup window. MO2 log line 588 of `<modlist>/logs/mo_interface.log` shows the `keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` qInfo firing as designed. Source change is live in `<repo>/mo2_mcp/__init__.py` (currently uncommitted on `main`, on top of v2.9.3 ship `5cae6a7`). Live install was synced 2026-04-29 12:26.

What v2.9.4 ships is the **logistics tail**: version bump, doc audit, coverage-smoke at the deny-list SHA, build chain, live re-sync at the v2.9.4 SHA, post-restart sanity, pre-tag, tag/push/release, memory update.

The full mandate, architecture decisions, scope locks (Q1–Q7 per Aaron 2026-04-29), out-of-scope, halt structure, and acceptance criteria are in `dev/plans/v2.9.4_autostop_denylist/PLAN.md`. Read it.

## Session-start ritual (do these in order)

1. **Confirm role.** State back to Aaron in your first message that you're the v2.9.4 execution conductor (single-phase ship). If the user pasted this prompt expecting a multi-phase orchestrator session, redirect — v2.9.4 is compressed Phase 1 only.

2. **Read these files in full** (in order):
   - `dev/plans/v2.9.4_autostop_denylist/PLAN.md` — full plan; this is your authoritative reference. Specifically § Phase 1 (your halt structure) and § ✅ Locked decisions (Q1–Q7).
   - `dev/plans/v2.9.4_autostop_denylist/PHASE_0_HANDOFF.md` — bundled into the Phase 0 commit. Records what was scoped and the Q1-Q7 framing.
   - `mo2_mcp/CHANGELOG.md` top entry (v2.9.3) — recent context.
   - `KNOWN_ISSUES.md` § Environmental quirks (around line 160) — current auto-stop-on-launch entry. Phase 1 Halt 1c updates this.

3. **Read these files briefly (skim, don't memorize):**
   - `dev/plans/v2.9.3_perk_effects/PHASE_5_HANDOFF.md` — your structural template for the halt cadence + ship sequence. v2.9.4 P1 mirrors v2.9.3 P5 closely (4 halts, post-Halt-4 mechanical sequence, hash-record commit).
   - `<workspace>/research/SUMMARY.md` § "v3.0 viability empirical validation" — full empirical record for the deny-list change. Tells you what's already validated at the dev-build SHA so you know what's left to verify at the post-build SHA.
   - `<workspace>/research/followups/mcp_autostop_investigation.md` § 1 (auto-stop location) + § 3 (original v1.0.3 crash rationale) + § 4 (minimum-change path). Useful background for the CHANGELOG copy and for understanding why Synthesis is intentionally NOT exempted.
   - `dev/plans/v2.9.2_read_side_efficiency/PHASE_5_HANDOFF.md` — alternate ship-sequence template if you want a second comparison point.

4. **Live state check.** Before any code/build steps, run:
   - `git -C <repo> status --short` → expect `M mo2_mcp/__init__.py` (deny-list code change, uncommitted) AND nothing else outside the v2.9.4 plan archive (the Phase 0 commit will have left a clean working tree minus the deny-list change). If you see other unexpected modifications, halt and ask Aaron.
   - `git -C <repo> log --oneline -5` → expect `[v2.9.4 P0]` Phase 0 archive commit at HEAD, with v2.9.3's commits below.
   - `git -C <repo> branch --show-current` → expect `main`.
   - **Halt-mandatory if** any of the above don't match — something has drifted between scoping and execution.

5. **`mo2_ping` baseline.** Confirm via Claude Code's MCP tools that `mo2_ping` currently returns `version: "2.9.3"` (the live install was synced at the dev-build SHA yesterday — Python plugin reports version from `config.py` at module-load time, and a fresh MO2 hasn't re-loaded it yet). If `mo2_ping` returns `2.9.4`, something has been bumped pre-emptively — halt and ask Aaron.

6. **Confirm phase identity + work plan with Aaron before any code/build/sync changes.** Wait for go-ahead.

## Phase 1 halt structure (recap from PLAN.md)

You run all four halts. There's no separate executor session — you ARE the executor.

### Halt 1 — Pre-ship verification + version bump + doc audit + build chain (internal halt)

- **1a.** Coverage-smoke at the deny-list-applied SHA. Expected: 449/455 (449 PASS + 6 documented SKIPs from v2.9.3 baseline). Halt-mandatory if any cell flips PASS → FAIL.
- **1b.** Version bump in 4 files: `mo2_mcp/config.py` `PLUGIN_VERSION` tuple, `installer/claude-mo2-installer.iss` `AppVersion`, `README.md` (installer download link line 7 + Manual Install reference line 59), `mo2_mcp/CHANGELOG.md` new v2.9.4 entry.
- **1c.** Doc audit: KNOWN_ISSUES.md auto-stop entry update (xEdit no longer triggers + new capability paragraph + visibility-lag caveat noting v3.0 daemon territory). README.md line 133 update (drop xEdit from auto-stop example list, add v2.9.4 parenthetical). Skim the `.claude/skills/` SKILL.md files. **NO PERK PEPMA Float-flag entry** per Q3 lock — Aaron: "this plugin has an error, nothing to fix in my view."
- **1d.** `dotnet publish -c Release tools/mutagen-bridge/mutagen-bridge.csproj` (Q7 lock — re-publish for SHA-chain hygiene). Stage to `build-output/mutagen-bridge/`. Capture SHIP SHAs.
- **1e.** ISCC compile: `C:/Utilities/Inno Setup 6/ISCC.exe installer/claude-mo2-installer.iss`. Capture installer SHA.

### Halt 2 — Live sync + Aaron MO2 full-restart (Aaron-action halt)

- **2a.** Pre-sync live SHA capture.
- **2b.** Sync to live — bridge publish output + version-bumped Python + audit'd docs.
- **2c.** Aaron full-restarts MO2; you call `mo2_ping` from Claude Code. Expected: `version: "2.9.4"`.
- Halt-mandatory if `mo2_ping` still shows `2.9.3` post-restart.

### Halt 3 — Live sanity check at SHIP SHA (3-path)

- **Path (a) — Deny-list verification (Q6 lock).** Aaron launches `SSEEdit64.exe` (or other xEdit-family). You verify MO2 log carries the `keeping server alive ... (exempt)` qInfo line for the v2.9.4 build. During xEdit's lifetime, you run `mo2_ping`, `mo2_record_index_status`, one `mo2_query_records`. Aaron exits xEdit. You confirm `mo2_ping` still works post-exit.
- **Path (b) — Game-launch regression baseline.** Either Aaron launches a game OR you do source-level structural verification of the non-exempt code path (executor's call based on Aaron's availability).
- **Path (c) — v2.9.3 regression baseline.** `mo2_record_detail` on a known-good FormID + optionally one `mo2_record_detail` with `formids` + `expand_links` to exercise v2.9.2 surface.

### Halt 4 — Pre-tag mandatory (hard halt — Aaron approval)

- Surface release-notes draft, tag/push/release command sequence, SHA-chain summary, doc-audit confirmation. **No tag without Aaron's explicit "go".**

### Post-Halt-4 mechanical ship

- Stage all v2.9.4 changes (incl. uncommitted deny-list change in `mo2_mcp/__init__.py`).
- Work commit: `[v2.9.4 P1] Ship v2.9.4 — auto-stop deny-list (xEdit-clarity capability)`. Heredoc multi-line message.
- Tag, push, `gh release create v2.9.4 --notes-file build-output/RELEASE_NOTES_v2.9.4.md` + installer attached.
- Memory update: `project_capability_roadmap.md` v2.9.4 entry.
- Hash-record commit fills the placeholder in `PHASE_1_HANDOFF.md`.

## Conductor decisions (locked from Phase 0)

These are NOT up for re-litigation:

- **Q1** = `v2.9.4_autostop_denylist` slug (Aaron 2026-04-29).
- **Q2** = DEFER write-time detection-and-warn to v3.0 daemon. v2.9.4 ships the deny-list ONLY; the visibility-lag UX rough edge stays known + documented in KNOWN_ISSUES.md.
- **Q3** = NO PERK PEPMA Float-flag entry in KNOWN_ISSUES.md. Aaron: existing § Environmental quirks neighbors (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) already cover the malformed-plugin category.
- **Q4** = single Phase 1 (compressed). No orchestration-conductor session; you cover the full ship end-to-end.
- **Q5** = run full coverage-smoke at v2.9.4 SHA (449/455 baseline expected to hold; deny-list change touches no patching/reading paths).
- **Q6** = PRE-TAG empirical re-test at the post-build live SHA (added to Halt 3 Path (a)).
- **Q7** = re-publish the bridge for SHA-chain hygiene. v2.9.4 has zero bridge code changes; SHIP SHAs may differ from v2.9.3's only by .NET build determinism factors. Either way the SHA chain is recorded fresh for v2.9.4.

## Decisions you own vs escalate to Aaron

| Decision | You own | Escalate to Aaron |
|---|---|---|
| Halt sequencing within Phase 1 | ✅ | — |
| Doc-audit wording (within scope of files Halt 1c names) | ✅ | If significant rephrasing needed |
| Halt 3 Path (b) source-verify vs game-launch | ✅ | If unsure |
| `dotnet publish` / ISCC build chain mechanics | ✅ | Build failures escalate |
| Live sync exact file list | ✅ | If unexpected files diverge between repo + live |
| Coverage-smoke result interpretation | ✅ if 449/455 baseline holds | Any PASS → FAIL flip escalates immediately |
| Doc-audit absorption beyond the locked file list | — | ✅ always — never expand audit silently |
| **Write-surface bonus-catch absorption** (any new write function or write-surface mechanism, even trivial) | — | ✅ always — per `feedback_write_surface_bonus_catch.md` |
| Latent-bug bonus-catch in code Phase 1 is touching | ✅ if <1 h + load-bearing | ✅ if >1 h or borderline |
| Q1–Q7 lock adjustments | — | ✅ Aaron locked at Phase 0 |
| Synthesis exemption | — | ✅ Aaron's call (default no per § C lock) |
| Pre-tag mandatory ship sign-off | — | ✅ Halt 4 hard halt |
| Release-notes copy + framing | ✅ first draft, surface to Aaron at Halt 4 | — |

## Token-efficient escalation format (when escalating to Aaron)

```
TOPIC: <one line>
CONTEXT (3 bullets):
  - <bullet>
  - <bullet>
  - <bullet>
QUESTION: <single specific question>
OPTIONS: A/B/C with one-line rationale each.
RECOMMENDATION: <pick + 1-line why>
DEFAULT IF NO RESPONSE: <what happens absent guidance>
```

## End-of-release ritual

When the post-Halt-4 ship sequence completes:

1. Confirm `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.4` resolves with the installer attached.
2. Confirm `<live>` is at v2.9.4 via `mo2_ping`.
3. Confirm memory updated (`project_capability_roadmap.md` reflects v2.9.4 shipped — first concrete instance of read-surface-equally-with-write pillar; v3.0 daemon retains its other motivations but is no longer sole path to xEdit-clarity).
4. Confirm SHAs captured + bridge SHA chain matches across publish / build-output / live install.
5. Confirm KNOWN_ISSUES.md auto-stop entry updated.
6. Tell Aaron: "v2.9.4 shipped. Conductor session done. Plan archive at `Claude_MO2/dev/plans/v2.9.4_autostop_denylist/`. xEdit-clarity capability live; v3.0 daemon work resumes from `<workspace>/research/` where it left off."
7. Stop. Don't spawn anything else.

## Operating notes

- **Token discipline.** Phase 1 is mechanical — your context budget is mostly halt outputs + commit/tag/push commands. The full halt structure should fit comfortably in a single session.
- **Don't re-litigate locked decisions.** Q1–Q7 are locked. If something during Halt 1c suggests revisiting (e.g. an unrelated KNOWN_ISSUES.md staleness surfaces during the audit skim), escalate per the doc-audit-absorption row above — don't silently expand.
- **Trust the empirical validation.** The 2026-04-29 evening test confirmed the deny-list works at the dev-build SHA. Halt 3 Path (a) re-confirms at the post-build SHA per Q6 lock. Don't re-litigate the deny-list approach itself; ship it cleanly.
- **The deny-list change is plugin-lifecycle territory, NOT write-surface.** Per `feedback_write_surface_bonus_catch.md` framing: this release is NOT a write-surface release. Bonus-catch policy applies to anything that surfaces during the audit / build / sanity sweeps — escalate write-surface candidates always; latent-bug fixes in audit'd code surfaces follow the legacy ">1 h or new operator" bar.
- **Bridge re-publish is Q7 = A.** v2.9.4's bridge is bytewise identical at the source level (zero bridge code changes) — but per Q7 you re-publish + re-sync for SHA-chain hygiene. If the SHIP SHAs come out byte-identical to v2.9.3's via .NET build determinism, that's expected; the chain is still recorded fresh for v2.9.4.
- **Don't confuse `<repo>` and `<workspace>`.** The plan archive uses `<workspace>/research/` (sibling of `<repo>`) for the daemon viability research artifacts. Plan archives live under `<repo>/dev/plans/`. Live install lives at `<live>` on the E: drive.
- **Live-install-side docs.** KNOWN_ISSUES.md and README.md live at `<live>`'s parent directory level, NOT inside `<live>/` itself (`<live>` is `<modlist>/plugins/mo2_mcp/`). Confirm exact layout on Halt 2 sync — the sync target paths must match the install layout, not the repo layout.

Confirm you've identified yourself as the v2.9.4 execution conductor, name your current state (expect: at the start of Halt 1), and propose your first action (typically: run live state check + `mo2_ping` baseline, then propose Halt 1a coverage-smoke command before executing).
