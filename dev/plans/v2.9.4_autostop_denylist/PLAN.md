# v2.9.4 — Auto-stop deny-list (xEdit-clarity capability)

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-29 evening, post-v2.9.3 ship + same-day daemon-architecture viability research.
**Baseline:** v2.9.3 (shipped 2026-04-29 morning — PERK.Effects writability).
**Target version:** v2.9.4 (slug locked by Aaron 2026-04-29: `v2.9.4_autostop_denylist`).
**Sessions estimated:** 2 phase sessions — Phase 0 = this scoping session (bundles PLAN + CONDUCTOR_KICKOFF + PHASE_0_HANDOFF in a single commit); Phase 1 = compressed ship sequence (single executor session running the full pre-ship verification + build + live sync + sanity + tag/push/release). No orchestration-conductor session contemplated — the release is short enough that a single execution-conductor session covers it end-to-end.

**Mandate.** Land xEdit-clarity capability — Claude can issue MCP read queries (record_detail, query_records, conflict_chain, etc.) concurrent with an active xEdit session — by narrowing the v1.0.3 auto-stop-on-launch behavior to exempt the xEdit family. Today (v2.9.3 and earlier) MO2's auto-stop fires on every executable launch, including xEdit, killing the MCP server for the duration of any xEdit session. The shipped change adds a regex deny-list (`_AUTOSTOP_EXEMPT_PATTERN`) matching xEdit-family executables (14 game-edition variants × version-suffix tolerance) so the server stays alive during xEdit's lifetime. Game launches and Synthesis still trigger the auto-stop unchanged.

**Structural note: this release is unusual for the v2.9.x series.** The implementation is **already done and live-validated**. During the 2026-04-29 evening daemon-architecture viability research (`<research>/SUMMARY.md` § "v3.0 viability empirical validation"), the deny-list code change was applied to `<repo>/mo2_mcp/__init__.py` and synced to `<live>/__init__.py`. An end-to-end concurrent-MCP-during-xEdit test passed cleanly: 13/13 MCP queries succeeded across a 6-minute live xEdit session, including a 9.9 s record-index lazy build during xEdit's USVFS-setup window (the highest-risk concurrent-load scenario). MO2 log line 588 shows `keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` qInfo firing exactly as designed. Zero errors or qWarnings throughout.

What v2.9.4 ships is the **logistics tail** of a one-line capability landing: version bump, doc audit, coverage-smoke verification at the deny-list SHA, build chain (dotnet publish + ISCC), live install re-sync, MO2 full-restart, post-restart sanity, pre-tag mandatory, tag + push + `gh release create`, memory update. Phase structure compressed to a single Phase 1 per Aaron's lock 2026-04-29 (Q4 = compress to Phase 1).

Real consumer signal: the xEdit-clarity vision (auto-memory: `project_xedit_clarity_vision.md`) calls for Claude to "see as clearly as a user can in xEdit." The natural workflow is Claude reading records while the user has xEdit open viewing the same data. Today that workflow is impossible because the MCP server stops the moment xEdit launches. v2.9.4 is the first concrete shipped instance of the read-surface-equally-with-write pillar of the xEdit-clarity vision — the daemon (v3.0) amortizes the per-call subprocess cost (10-second order today, sub-microsecond after daemon mode), but the basic read-mode-during-xEdit capability ships now.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |
| `<plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.4_autostop_denylist\` |
| `<research>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\research\` (sibling of `<repo>`; daemon viability research artifacts from 2026-04-29) |
| `<v2.9.3-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.3_perk_effects\` (shipped 2026-04-29; reference only — closed) |
| `<v2.9.2-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.2_read_side_efficiency\` (shipped 2026-04-28; reference only — closed) |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. The conductor's kickoff prompt named your phase. **Before touching anything**, do this in order:

1. **Confirm your phase.** The kickoff prompt named your phase. v2.9.4 has only Phase 0 (scoping — already done by the time you're reading this) and Phase 1 (ship). If the kickoff said Phase 1, that's you. If it said anything else, halt and confirm with Aaron.

2. **Read the previous handoff** in full. For Phase 1, that's `<plan>/PHASE_0_HANDOFF.md` — bundled into the scoping session's single commit alongside this PLAN.md and CONDUCTOR_KICKOFF.md.

3. **Read your phase section in this file** below (§ Phase N). It tells you the goal, files to touch, halt structure, conductor decisions relevant to your phase, and what to write in your own handoff.

4. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `<repo>/README.md`
   - `<repo>/mo2_mcp/CHANGELOG.md` top entry (v2.9.3)
   - `<repo>/KNOWN_ISSUES.md` § Environmental quirks (auto-stop-on-launch entry around line 160 — Phase 1's doc audit updates this)
   - **Skim** `<v2.9.3-plan>/PHASE_5_HANDOFF.md` — your structural template for Phase 1's halt cadence + ship sequence.
   - **Skim** `<research>/SUMMARY.md` § "v3.0 viability empirical validation" — full empirical record for the deny-list change. Specifically the timeline, the smoking-gun MO2 log entry, and the strategic-implication discussion of v2.9.4 vs v3.0 sequencing. Tells you what's already been validated at the dev-build SHA so you know what's left to verify at the post-build SHA.
   - **Skim** `<research>/followups/mcp_autostop_investigation.md` § 3 (original v1.0.3 crash rationale) + § 4 (minimum-change path) — the source-side analysis that motivated the deny-list. Useful background for the CHANGELOG entry copy.

5. **Live state check.** Before any code/build steps, confirm:
   - Current branch is `main`.
   - `<repo>/mo2_mcp/__init__.py` is uncommitted on main (the deny-list code change is already in the working tree but not yet committed — Phase 1's work commit captures it). If it's already committed, confirm the SHA matches `_AUTOSTOP_EXEMPT_PATTERN` lines 96-103 + modified `_on_about_to_run` at lines 238-260; otherwise something has drifted, halt and ask Aaron.
   - `<live>/__init__.py` was synced 2026-04-29 12:26 — should already match the deny-list shape. Aaron's MO2 may or may not be running; you'll do a full restart in Halt 2 anyway.

6. **Confirm phase identity + work plan with Aaron before any code/build/sync changes.** Wait for go-ahead.

7. **At the end of your phase**, write `PHASE_1_HANDOFF.md` in `<plan>/` using the template at the bottom of this file. Force-add (per project convention — `dev/` is gitignored).

---

## 📋 Background — why this plan exists

### v1.0.3 carryover-by-analogy

The auto-stop-on-launch was added in v1.0.3 (2026-04-12) to prevent an MO2 hang caused by the HTTP server thread conflicting with MO2's VFS setup during executable launches. The original failure mode was empirically observed on **game launch** (Skyrim and similar). xEdit / Synthesis / BodySlide / etc. were added to the implicit scope by analogy — the v1.0.3 CHANGELOG entry lists them as covered cases, but no evidence indicates they were independently reproduced. From `<research>/followups/mcp_autostop_investigation.md` § 3:

> "The author observed the hang when launching Skyrim (the most common MO2 executable launch), immediately added the generic hook, and listed xEdit and BodySlide as the obvious other cases it would cover — not as distinct failure modes that were empirically verified."

The v2.9.4 change confirms Aaron's longstanding hypothesis (carried into the v3.0 daemon-research kickoff) that the hang is **specific to game-engine launches**, not to xEdit's USVFS interaction shape.

### Empirical viability test (2026-04-29 evening)

Full record at `<research>/SUMMARY.md` § "v3.0 viability empirical validation". Headline:

- **Code applied:** `<repo>/mo2_mcp/__init__.py` got `_AUTOSTOP_EXEMPT_PATTERN` (regex) + modified `_on_about_to_run()` early-return on xEdit-family matches. Synthesis intentionally NOT exempted (batch patcher, no concurrent-read use case, similar Mutagen overlay shape to game launches).
- **Live install:** Deployed to `<live>/__init__.py` 2026-04-29 12:26.
- **Test:** Aaron launched `SSEEdit64.exe` from MO2's executables list, created a test patch, saved, and exited. During xEdit's lifetime (~6 min), conductor ran 13 MCP read queries across 2 batches (mo2_ping, mo2_record_index_status, mo2_query_records, mo2_record_detail, mo2_conflict_chain). All queries succeeded.
- **MO2 log evidence:** `<modlist>/logs/mo_interface.log:588` shows `MO2 MCP Server: keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` — qInfo from the new code path fired as designed. No `stopping server` line anywhere in the log. Zero errors / qWarnings.
- **Critical sub-test:** record-index auto-build fired during xEdit's USVFS-setup window (9.9 s build, 2.9M records, 427k conflicts), the highest-risk concurrent-load scenario. Completed cleanly with no deadlock.
- **Post-xEdit state:** `ensure_fresh` detected Aaron's `test 3.esp` automatically after MO2's directory refresh — the xEdit-authored patch was visible to MCP queries immediately. xEdit-clarity workflow loop closes empirically.

This is the first time Aaron's hypothesis was tested in the original failure-mode shape (concurrent USVFS load during xEdit's setup window). Result: clean. The deny-list change is empirically safe at the dev-build SHA.

### Real-consumer signal

Per Aaron's xEdit-clarity vision (auto-memory `project_xedit_clarity_vision.md`):

> "The big picture goal is not just for Claude to have the ability to make and edit anything accurately but also to see everything as clearly as a user can in xEdit."

Today (pre-v2.9.4): Claude can *make + edit* via the MCP write surface but *cannot see while xEdit is open*. v2.9.4 closes that gap for read-mode-during-xEdit.

The v3.0 daemon retains its other motivations (~10× token reduction, persistent state, L3 Roslyn surface, OnlyIdentifiers reverse-link traversal — see `<research>/SUMMARY.md`) but is no longer the *sole* path to xEdit-clarity. v2.9.4 ships the read half cheaply; v3.0 ships the latency-amortization half later.

### The visibility-lag failure mode (deferred to v3.0 — Q2 lock)

Once xEdit and the MCP server can run concurrently, a new UX failure mode surfaces: xEdit reads load order at startup, so MCP-driven plugin writes mid-xEdit-session are invisible to xEdit until reload. `<research>/followups/xedit_coexistence_detection.md` describes a 316-line spec for daemon-side detection (running-xEdit polling at write-time) + warning emission. Per Aaron's Q2 lock 2026-04-29: **this is deferred to v3.0** where the daemon's persistent state makes detection cleaner architecturally. v2.9.4 ships the capability; the UX rough edge stays known + documented in KNOWN_ISSUES.md (Phase 1's doc audit).

---

## 🏗️ Architecture — auto-stop deny-list mechanism

### A. The regex deny-list

Live source at `<repo>/mo2_mcp/__init__.py` lines 96-103:

```python
_AUTOSTOP_EXEMPT_PATTERN = re.compile(
    r'^('
    r'sseedit|tes5edit|tes5vredit|enderalseedit|enderaledit|'
    r'fo4edit|fo4vredit|fo76edit|fnvedit|fo3edit|'
    r'tes4edit|tes4redit|tes3edit|sf1edit|xedit'
    r')[\w \-]*\.exe$',
    re.IGNORECASE,
)
```

15 game-edition prefixes (xEdit family across SSE/Enderal/FO4/FO76/FNV/FO3/TES4/TES5/TES3/SF1 + the user-renamed `xEdit.exe` wildcard). Tail `[\w \-]*\.exe$` tolerates version/build suffixes (`SSEEdit64.exe`, `xEdit64.exe`, `TES5Edit32.exe`, etc.). Match is case-insensitive against `os.path.basename(app_path)`.

### B. Modified `_on_about_to_run`

Live source at `<repo>/mo2_mcp/__init__.py` lines 238-260:

```python
def _on_about_to_run(self, app_path: str) -> bool:
    exe_name = os.path.basename(app_path)
    if _AUTOSTOP_EXEMPT_PATTERN.match(exe_name):
        qInfo(f"{PLUGIN_NAME}: keeping server alive across launch of {app_path} (exempt)")
        self._was_running_before_launch = False
        return True

    if self._server and self._server.is_running():
        qInfo(f"{PLUGIN_NAME}: stopping server before launch of {app_path}")
        self._server.stop()
        self._server = None
        self._was_running_before_launch = True
    else:
        self._was_running_before_launch = False
    return True
```

`_on_finished_run` is **unchanged** — its existing `if self._was_running_before_launch:` guard (which the early-return path zeros) means no restart action for exempt executables.

`import re` was added at the top of the file (line 3). Total addition: ~28 lines (regex constant + comment block + modified handler + import).

### C. Synthesis intentionally NOT exempted

Aaron's call during the 2026-04-29 evening test design. Rationale:
- **No concurrent-read use case.** Synthesis is a batch patcher invoked end-to-end by the user, not an interactive viewer where the user has it open and switches between it and Claude.
- **Mutagen overlay shape similar to game launches.** Synthesis loads the entire modlist via Mutagen at startup — the resource-contention shape is closer to a game launch than to xEdit's incremental open-records-on-demand pattern.
- **Easy to revisit.** If a real consumer signal surfaces ("Claude querying records while Synthesis is patching"), adding `synthesis|synthesis-patcher` to the regex is a one-line extension in a future release.

### D. Q2 lock — write-time detection-and-warn deferred to v3.0

xEdit-side write-durability race (visibility lag): once the MCP server can write plugins while xEdit is open, xEdit doesn't see the new patch.esp until reload. `<research>/followups/xedit_coexistence_detection.md` specs daemon-side detection + warn-on-write (~20 ms `Process.GetProcessesByName` cost, parent-PID coupling for same-modlist confirmation). Aaron 2026-04-29 = **defer to v3.0** — daemon's persistent state makes detection architecturally cleaner; v2.9.4 stays a pure deny-list ship at minimum risk.

KNOWN_ISSUES.md (updated by Phase 1's doc audit) carries this as a known UX caveat alongside the new capability description.

---

## ✅ Locked decisions (Q1-Q7, Aaron 2026-04-29)

| # | Topic | Lock |
|---|---|---|
| Q1 | Slug | `v2.9.4_autostop_denylist` |
| Q2 | Scope expansion (write-time detection-and-warn) | DEFER to v3.0 daemon |
| Q3 | PERK PEPMA Float-flag KNOWN_ISSUES.md absorption | NO — Aaron: "this plugin has an error, nothing to fix in my view." Existing § Environmental quirks neighbors (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) already cover the malformed-plugin category; no new entry needed. |
| Q4 | Phase structure | Compress to a single Phase 1 (no separate doc-audit / version-bump / coverage-smoke / ship phases) |
| Q5 | Coverage-smoke gate | RUN full coverage-smoke at v2.9.4 SHA (449/455 baseline expected to hold; deny-list change touches no patching/reading paths) |
| Q6 | Aaron-side empirical re-test timing | PRE-TAG quick xEdit-launch + MCP query test at the post-build live SHA (added to Halt 3 sanity sequence) |
| Q7 | Bridge `dotnet publish` re-spin | A — re-publish at same source for SHA-chain hygiene per `feedback_build_artifact_versioning.md` spirit |

---

## 📤 Out of scope (locked at PLAN write-time)

- **xEdit write-time detection-and-warn.** Q2 lock — v3.0 daemon territory.
- **Synthesis exemption.** § C lock — easy to revisit if consumer signal surfaces.
- **PERK PEPMA Float-flag KNOWN_ISSUES.md entry.** Q3 lock — Aaron's call: existing neighbors cover the category, no new doc surface needed.
- **Bridge code changes.** v2.9.4 is Python-only; `<repo>/tools/mutagen-bridge/` has zero changes. Q7's re-publish reuses the v2.9.3 source-level bridge with a fresh build (SHA-chain hygiene only, no behavioral diff).
- **v3.0 daemon mode.** Separate workstream; v2.9.4 ships in the per-call subprocess architecture. Daemon's per-call latency amortization (the 9-13 s subprocess cost during the empirical test → sub-microsecond after daemon mode) is the next-tier value-prop story, NOT a v2.9.4 deliverable.
- **L0/L1/L2/L3 daemon work.** Out of scope — see `<research>/SUMMARY.md` § "Recommended Next Steps" for v3.0 work plan.
- **Read-surface candidates** (reverse-link search, override-aware FormLink expansion, MaxDepth MCP-configurable, cross-call result caching). v2.9.x candidates per `<repo>/KNOWN_ISSUES.md` § Read-surface candidates; not v2.9.4 scope.
- **PERK PEPMA Float-flag handling in code.** The malformation is plugin-side (`Requiem - Special Feats.esp`); Mutagen 0.53.1's strict parser correctly rejects it with `MalformedDataException`. The bridge's per-record error envelope already surfaces this cleanly. No code-side action item per Aaron's Q3 lock.

---

## 📋 Phase 0 — Scoping + plan archive (THIS SESSION — bundled)

**Goal.** Produce the v2.9.4 plan archive in a single commit. Lock scope via Q1-Q7 with Aaron, write the deliverables, hand off to Phase 1.

**Deliverables (single `[v2.9.4 P0]` commit):**

1. ✅ **`<plan>/PLAN.md`** (this file) — full plan, mandatory.
2. ✅ **`<plan>/CONDUCTOR_KICKOFF.md`** — Phase 1 entry-point paste-text, mandatory.
3. ✅ **`<plan>/PHASE_0_HANDOFF.md`** — bundled per v2.9.3 pattern. Records what was scoped, the Q1-Q7 locks, and Phase 0 → Phase 1 handoff context.
4. ❌ **`<plan>/MATRIX.md`** — SKIPPED. The deny-list change is so narrow that explicit matrix testing is overkill; Phase 1's Halt 3 sanity check (3-path) acts as the single-cell smoke. If Phase 1 surfaces a need for matrix-style verification mid-ship, escalate to Aaron — don't expand silently.

**Pre-existing in `<plan>/`:**

- `<plan>/SCOPING_HANDOFF.md` — written by the prior session conductor before this scoping session opened. Stays in the archive as historical context (the source authority for Q1-Q7 framing). Force-added in the same commit.

**Acceptance:** the four files above exist in `<plan>/`, force-added in a single `[v2.9.4 P0]` commit on `main`. SCOPING_HANDOFF.md is included in the same commit (it's currently untracked).

---

## 📋 Phase 1 — Compressed ship sequence

**Goal.** Ship v2.9.4 end-to-end in a single executor session. No mid-phase orchestration; the Phase 1 executor talks to Aaron directly through halt points.

**Halt structure** (mirrors v2.9.3 P5; condensed for the single-mechanism release):

### Halt 1 — Pre-ship verification + version bump + doc audit + build chain

Internal halt (Phase 1 executor reports outcome to Aaron, then proceeds).

**1a. Coverage-smoke at the deny-list-applied SHA (Q5 lock).**

- Run the full coverage-smoke suite at the current state (deny-list working tree, version not yet bumped).
- **Expected:** 449/455 PASS + 6 documented SKIPs (all pre-v2.9.4 carry-overs from v2.9.3 final state — `1.r.40` OTFT, `1.r.47` SPEL, `1.D.04` CellBinaryOverlay, `4.esl.01` ESL master live-modlist, `1.P.Unknown.MGEF` Mutagen reclassification, `1.P.GetVATSValueUnknown.MGEF` Mutagen 0.53.1 schema gap).
- **Halt-mandatory if:** any cell flips to FAIL relative to v2.9.3 baseline. The deny-list change touches no patching/reading paths; any drift is a regression. Halt and surface to Aaron.

**1b. Version bump.**

Files touched:
- `<repo>/mo2_mcp/config.py` → `PLUGIN_VERSION` tuple `(2, 9, 3)` → `(2, 9, 4)`.
- `<repo>/installer/claude-mo2-installer.iss` → `AppVersion=2.9.3` → `AppVersion=2.9.4`.
- `<repo>/README.md` → installer download link `claude-mo2-setup-v2.9.3.exe` → `claude-mo2-setup-v2.9.4.exe` (line 7) + Manual Install reference at line 59.
- `<repo>/mo2_mcp/CHANGELOG.md` → new `## v2.9.4 — 2026-04-29` (or actual ship date) entry above the v2.9.3 block.

**1c. Doc audit (per `feedback_conductor_doc_audit.md` — mandatory pre-installer-build).**

Files touched:
- `<repo>/KNOWN_ISSUES.md` § Environmental quirks (around line 160) — UPDATE the "Claude Code reconnects to the MCP server automatically" entry to reflect that **xEdit no longer triggers auto-stop**. Suggested wording (final wording is the executor's call with Aaron):
  > "Claude Code reconnects to the MCP server automatically after MO2's auto-stop-on-launch cycle (Skyrim / SKSE loaders / Synthesis / etc.) or after a full MO2 restart, as long as the server comes back on the same HTTP URL. As of v2.9.4, **xEdit launches NO LONGER trigger the auto-stop** — the server stays alive during xEdit sessions, enabling concurrent record queries while the user has xEdit open. (Game executables, Synthesis, and other tools whose Mutagen / VFS load profile matches the original v1.0.3 hang race continue to trigger the auto-stop as before.) Note: xEdit reads load order at startup, so MCP-driven plugin writes mid-xEdit-session are invisible to xEdit until it reloads — write-time detection-and-warn is a v3.0 daemon candidate."

- `<repo>/README.md` line 133 — UPDATE the bullet currently reading `"Auto-stop on launch — the server stops when MO2 launches any executable (Skyrim, xEdit, etc.) and restarts after it exits, preventing conflicts with MO2's VFS setup"`. New wording: drop the `xEdit` from the example list and add a parenthetical noting xEdit-coexistence as of v2.9.4.

- ❌ **PERK PEPMA Float-flag entry — NOT added** per Q3 lock. Aaron: "this plugin has an error, nothing to fix in my view." Existing § Environmental quirks neighbors (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) already cover the malformed-plugin category.

- Other `.claude/skills/` SKILL.md files — skim, decide. The change is plugin-lifecycle-only; no skill behavior surface changes. Likely zero edits, but the skim is mandatory per `feedback_conductor_doc_audit.md`.

**1d. Bridge re-publish (Q7 lock).**

- `dotnet publish -c Release tools/mutagen-bridge/mutagen-bridge.csproj` — produces `<repo>/tools/mutagen-bridge/bin/Release/net8.0/publish/`.
- Stage to `<repo>/build-output/mutagen-bridge/` (mirrors v2.9.3 P5 structure).
- Capture SHIP SHAs:
  - `mutagen-bridge.dll` SHIP SHA
  - `mutagen-bridge.exe` SHIP SHA
- **Note:** v2.9.4 has zero bridge code changes; SHIP SHAs may differ from v2.9.3's only by .NET build determinism factors (build timestamps, etc.). If SHIP SHAs match v2.9.3 byte-identically, that's expected and not an error — it just means determinism held. Either way the SHA chain is recorded fresh for v2.9.4 hygiene.

**1e. ISCC compile.**

- `C:/Utilities/Inno Setup 6/ISCC.exe installer/claude-mo2-installer.iss` — produces `<repo>/build-output/installer/claude-mo2-setup-v2.9.4.exe`.
- ISCC log confirms audited `KNOWN_ISSUES.md` + `README.md` + `mo2_mcp/CHANGELOG.md` bundled (release-archive freeze per `feedback_conductor_doc_audit.md`).
- Capture installer SHA.

**Halt 1 acceptance:**
- Coverage-smoke 449/455 (or documented drift, surfaced to Aaron).
- Version bumped in 4 files.
- Doc audit landed (KNOWN_ISSUES + README + CHANGELOG entries).
- Bridge published + ISCC built.
- All SHAs captured.

### Halt 2 — Live sync + Aaron MO2 full-restart

Aaron-action-required halt (executor stages, Aaron restarts MO2 + reports `mo2_ping`).

**2a. Pre-sync live SHA capture.** Record the current `<live>/__init__.py` mtime + SHA so the post-sync SHA chain is auditable.

**2b. Sync to live.**

- Copy fresh-built bridge: `<repo>/tools/mutagen-bridge/bin/Release/net8.0/publish/*` → `<live>/tools/mutagen-bridge/` (preserve the `runtimes/` subdir).
- Copy version-bumped Python: `<repo>/mo2_mcp/__init__.py`, `<repo>/mo2_mcp/config.py` → `<live>/`.
- Copy audited docs: `<repo>/CHANGELOG.md` (no — there's no top-level CHANGELOG; the dev-facing one is `<repo>/mo2_mcp/CHANGELOG.md`), `<repo>/KNOWN_ISSUES.md`, `<repo>/README.md`, `<repo>/mo2_mcp/CHANGELOG.md` → `<live>/` (mirroring the install layout, where `KNOWN_ISSUES.md` and `README.md` live at `<live>/`'s parent).
- Audit cross-check: `<live>/tools/mutagen-bridge/mutagen-bridge.dll` SHA == publish output SHA byte-identical via `sha256sum`.

**2c. Aaron full-restarts MO2.**

- Aaron closes MO2 fully, restarts.
- After Tools > Start/Stop Claude Server (start), Aaron runs `mo2_ping` from Claude Code.
- **Expected:** `version: "2.9.4"` in the response.
- **Halt-mandatory if:** `mo2_ping` returns `2.9.3` — sync didn't take effect; investigate before proceeding.

**Halt 2 acceptance:** `mo2_ping` returns `version: "2.9.4"` post-restart + bridge SHA chain matches publish → build-output → live install.

### Halt 3 — Live sanity check at SHIP SHA (3-path)

Aaron-cooperative halt (executor drives MCP queries, Aaron drives xEdit launch).

**3a. Path (a) — Deny-list verification (Q6 lock).**

This is the v2.9.4-specific re-test at the post-build live SHA. Per Q6: pre-tag mandatory.

- Aaron launches `SSEEdit64.exe` (or another xEdit-family exe of his choice) from MO2's executables list.
- **Expected MO2 log line** (in `<modlist>/logs/mo_interface.log`): `MO2 MCP Server: keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` — the qInfo from the deny-list path firing on the v2.9.4 build.
- **Expected MO2 log absence:** no `stopping server before launch of` line for the xEdit launch.
- During xEdit's lifetime: executor runs `mo2_ping`, `mo2_record_index_status`, and one `mo2_query_records` call. **Expected:** all return successfully.
- Aaron exits xEdit. Executor confirms `mo2_ping` still works (server not restarted by `_on_finished_run` because `_was_running_before_launch` was `False`).

**3b. Path (b) — Game-launch auto-stop unchanged.**

This is regression-baseline coverage — confirm the original auto-stop still fires for non-exempt executables.

- Either Aaron launches a game executable (Skyrim Special Edition / SKSE loader) OR (faster) the executor verifies via grep on the v2.9.4 source that the non-exempt path is structurally unchanged + relies on the empirical evidence from prior v2.9.x ships.
- **Phase 1 executor's call.** If Aaron has a game launch handy, do it (most defensive). If not, source-level verification is acceptable since the v2.9.3 baseline shipped with the same non-exempt path live and clean.

**3c. Path (c) — v2.9.3 regression baseline.**

Smoke that v2.9.3-shipped capabilities (PERK.Effects, v2.9.2 read-side efficiency) still work.

- Single `mo2_record_detail` read on a known-good FormID (e.g. `Skyrim.esm:000019`).
- Optionally one `mo2_record_detail` with `formids: [...]` + `expand_links: [...]` to exercise v2.9.2 surface.
- **Expected:** unchanged from v2.9.3 P5 sanity check.

**Halt 3 acceptance:** all 3 paths PASS. MO2 log carries the exempt qInfo line for the v2.9.4 build (NOT just the dev-build SHA from yesterday). v2.9.3 regression baseline holds.

### Halt 4 — Pre-tag mandatory (Aaron approval)

Hard halt — no autonomous tag/push/release.

**4a. Executor surfaces to Aaron:**
- Release-notes draft (consumer-facing — narrative anchor: "Claude can now read while you're in xEdit").
- Tag/push/`gh release create` command sequence ready to execute.
- SHA-chain summary (publish == build-output == live install).
- Doc-audit confirmation (KNOWN_ISSUES + README + CHANGELOG entries reviewed).

**4b. Aaron approves with explicit "go" or surfaces blockers.**

**Halt 4 acceptance:** Aaron's explicit "go" sign-off recorded in handoff. **No tag without it.**

### Post-Halt-4 ship sequence

Executor proceeds autonomously after Halt 4 sign-off:

- **Stage:** `<repo>/mo2_mcp/__init__.py` (deny-list code change — currently uncommitted), `<repo>/mo2_mcp/config.py` (version bump), `<repo>/mo2_mcp/CHANGELOG.md` (new v2.9.4 entry), `<repo>/installer/claude-mo2-installer.iss` (AppVersion bump), `<repo>/README.md` (audit + version-link bump), `<repo>/KNOWN_ISSUES.md` (audit), `<plan>/PHASE_1_HANDOFF.md` (NEW, force-add), `<repo>/build-output/RELEASE_NOTES_v2.9.4.md` (NEW, force-add).
- **Work commit:** `[v2.9.4 P1] Ship v2.9.4 — auto-stop deny-list (xEdit-clarity capability)`.
- **Tag:** `git tag v2.9.4` on the work commit.
- **Push:** `git push origin main` + `git push origin v2.9.4`.
- **`gh release create v2.9.4`** with `--notes-file build-output/RELEASE_NOTES_v2.9.4.md` + installer `.exe` attached.
- **Memory:** `project_capability_roadmap.md` updated with v2.9.4 entry (xEdit-clarity capability shipped 2026-04-29; first concrete instance of read-surface-equally-with-write pillar; v3.0 daemon retains its other motivations but is no longer sole path to xEdit-clarity).
- **Hash-record commit:** `[v2.9.4 P1] Handoff: record commit hash <work-hash>` (fills the placeholder in PHASE_1_HANDOFF.md).

### Phase 1 acceptance

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.4` resolves with installer `.exe` attached.
- `mo2_ping` returns `version: "2.9.4"` from `<live>/`.
- KNOWN_ISSUES.md auto-stop entry reflects xEdit no longer triggers auto-stop.
- `mo2_mcp/CHANGELOG.md` has v2.9.4 entry framing the xEdit-clarity capability.
- README.md link points to v2.9.4 installer.
- SHA chain: publish == build-output == live install (3-way byte-identical).
- Memory `project_capability_roadmap.md` reflects v2.9.4 ship.
- Phase 1 handoff committed.

### Mandatory halt-and-report triggers (any of these → halt immediately)

- **Coverage-smoke regression** — any cell flips PASS → FAIL relative to v2.9.3 baseline.
- **`mo2_ping` returns wrong version** post-restart.
- **MO2 log missing the exempt qInfo line** during Halt 3 xEdit launch (sync didn't take effect, or regex doesn't match the specific exe Aaron used).
- **MO2 hangs** during xEdit launch (the original v1.0.3 failure mode reappearing — unlikely per yesterday's empirical evidence but a hard halt if it happens).
- **Bridge SHA chain mismatch** (publish ≠ build-output, OR live ≠ build-output post-sync).
- **Doc-audit ambiguity** — if the executor finds an additional skill / KB / doc surface that may need updating, halt and ask Aaron rather than autonomously expanding the audit.
- **Pre-tag mandatory not approved** — no tag without explicit Aaron "go".

---

## 🔁 Communicating with Aaron during Phase 1

There is **no orchestration-conductor session** between phases. Phase 1 executor talks to Aaron directly across all four halts. If the executor hits a question that would normally surface to a conductor (scope expansion, design-lock adjustment, write-surface bonus-catch absorption), surface to Aaron with the same `FROM CONDUCTOR — escalation` token-efficient summary format used in v2.9.3:

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

**Bonus-catch policy carries forward** (per `feedback_write_surface_bonus_catch.md` + Aaron 2026-04-28):
- Latent-bug fixes in code Phase 1 is touching (e.g. typos in the audit'd doc files): conductor's call if <1 h, escalate if borderline.
- **Write-surface bonus catches**: ALWAYS escalate, regardless of cost. v2.9.4 is plugin-lifecycle-only territory so this should not fire, but if Phase 1 surfaces an opportunistic fix in the write surface (e.g. a tools_patching.py issue noticed during the doc audit), do NOT auto-absorb — surface to Aaron.

**Doc-audit absorptions** (per `feedback_conductor_doc_audit.md`): the four files named in Halt 1c are the locked audit scope. Additional audit candidates surfacing mid-phase escalate to Aaron — don't expand silently.

---

## 📝 Phase 1 handoff template (PHASE_1_HANDOFF.md)

```markdown
# Phase 1 Handoff — Ship v2.9.4 — Auto-stop deny-list

**Phase:** 1
**Status:** Complete | In-progress | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** `<work-hash>` + `<hash-record-hash>`
**Live install synced:** Yes — re-synced from `tools/mutagen-bridge/bin/Release/net8.0/publish/` to `<live>/tools/mutagen-bridge/`; SHIP SHA present + verified via `mo2_ping` post-restart

## Locks (final, inherited from Phase 0)

All seven Q1–Q7 locked per Aaron 2026-04-29:
- **Q1** = `v2.9.4_autostop_denylist`.
- **Q2** = defer write-time detection-and-warn to v3.0 daemon.
- **Q3** = NO PEPMA absorption.
- **Q4** = compress to single Phase 1.
- **Q5** = run full coverage-smoke at v2.9.4 SHA.
- **Q6** = pre-tag empirical re-test.
- **Q7** = re-publish bridge for SHA-chain hygiene.

## What was done

### Halt 1 — Pre-ship verification + version bump + doc audit + build chain
[per-step results]

### Halt 2 — Live sync + Aaron MO2 full-restart
[pre-sync SHA, sync results, post-restart mo2_ping]

### Halt 3 — Live sanity check at SHIP SHA
[Path (a) deny-list, Path (b) regression, Path (c) v2.9.3 baseline]

### Halt 4 — Pre-tag mandatory
[Aaron approval recorded]

### Post-Halt-4 ship sequence
[stage / commit / tag / push / release / memory / hash-record]

## Verification performed

| Check | Status | Evidence |
|---|---|---|
| Coverage-smoke at SHIP SHA | ✅ 449/455 (or documented drift) | <evidence path> |
| `dotnet publish` build clean | ✅ 0 warnings, 0 errors | <evidence> |
| ISCC compile clean | ✅ <duration>, audit'd files bundled | <evidence> |
| SHA chain integrity | ✅ publish == build-output == live install | sha256sum 3-way |
| `mo2_ping` post-restart | ✅ `version: "2.9.4"` | <evidence> |
| Path (a) deny-list at SHIP SHA | ✅ exempt qInfo + MCP queries during xEdit | MO2 log line + MCP responses |
| Path (b) game-launch regression | ✅ auto-stop unchanged | <evidence> |
| Path (c) v2.9.3 regression baseline | ✅ PERK + read-side efficiency unchanged | <evidence> |

## Bugs surfaced

[None / list with disposition]

## Deviations from plan

[None / list]

## Known issues / open questions

[None for v2.9.4 / list]

## Conductor asks

**NONE.** v2.9.4 shipped.

## Files of interest

| Path | Why |
|---|---|
| `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.4` | Public release URL |
| `<repo>/build-output/installer/claude-mo2-setup-v2.9.4.exe` | Shipped installer artifact |
| `<repo>/build-output/RELEASE_NOTES_v2.9.4.md` | Consumer-facing release notes |
| `<repo>/mo2_mcp/CHANGELOG.md` § v2.9.4 | Dev-facing technical change log |
| `<plan>/` | Plan archive (PLAN + CONDUCTOR_KICKOFF + PHASE_0_HANDOFF + PHASE_1_HANDOFF) |
| `<repo>/KNOWN_ISSUES.md` § Environmental quirks | Updated auto-stop-on-launch entry |
| `<memory>/project_capability_roadmap.md` | Memory entry reflecting v2.9.4 shipped |
```
