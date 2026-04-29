# v2.9.5 — Phase 1 Handoff

**Status:** SHIPPED 2026-04-29 evening. Single-session conductor ship; description-and-docs-only; no multi-phase plan archive.

## What landed

Per `PLAN.md` in this directory. Full narrative in `mo2_mcp/CHANGELOG.md` § v2.9.5 entry and `build-output/RELEASE_NOTES_v2.9.5.md`.

Source-state changes:
- `mo2_mcp/tools_records.py` — `mo2_record_detail` tool-level + `formids`/`fields`/`expand_links` property descriptions rewritten to lead with action; `mo2_plugin_conflicts` gained CELL/WRLD warning.
- `.claude/skills/session-strategy/SKILL.md` — `description:` frontmatter rewritten for trigger reliability.
- `CLAUDE.md` — "Knowledge base" section replaced with "Tool documentation".
- `mo2_mcp/CHANGELOG.md` — v2.9.5 entry.
- `mo2_mcp/config.py` — `PLUGIN_VERSION` (2,9,4) → (2,9,5).
- `installer/claude-mo2-installer.iss` — `AppVersion` 2.9.4 → 2.9.5; two `Source:` lines for retired files removed.
- `README.md` — install link bumped, tool-reference and addon-system sections updated.
- `kb/KB_Tools.md` — DELETED (160 lines).
- `KNOWLEDGEBASE.md` — DELETED.

## SHIP SHAs

- `mutagen-bridge.dll`: `8acd969abff44f8275549c1b383105f1a3e3fbd941c6688f75b3facd061aaaaf`
- `mutagen-bridge.exe`: `80e980c058927a88320187b134bca8fdbf615e2a8497043fa1f8da7ed228f8a4`
- `claude-mo2-setup-v2.9.5.exe` (10,632,936 bytes): `27e0e8c50dbc36b4e51b7412e5265c00480e5bdd897e1923905472eefeedef37`

3-way SHA chain (publish == build-output == live install) verified for bridge. Bridge re-published per Q7 lock for SHA-chain hygiene; v2.9.5 has zero bridge code changes at the source level.

## Acceptance

### Build chain

- `dotnet publish` for Spooky CLI: SUCCESS (rebuild from clean was idempotent — uses cache when source unchanged; this run was a re-publish from current .NET artifacts).
- `dotnet publish` for mutagen-bridge: SUCCESS.
- ISCC compilation: SUCCESS (16.547 sec). Setup .exe at `build-output/installer/claude-mo2-setup-v2.9.5.exe`, 10.14 MB.

### Live sync

- Bridge: 44 files synced to `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\tools\mutagen-bridge\`.
- CLI: 110 files synced to `tools\spooky-cli\`.
- Python: 28 .py files synced to plugin root (`__pycache__/` wiped).

### Q6 pre-tag empirical re-test

**STATUS: BLOCKED on MO2 full restart.** Pre-restart `mo2_ping` returns v2.9.4 (cached Python). Aaron must fully close MO2 (not just stop the server) and reopen, then start Claude Server, before mo2_ping returns v2.9.5. Once that returns v2.9.5, this section gets updated with the validated post-restart query results.

Expected post-restart sanity:
1. `mo2_ping` → version "2.9.5"
2. Spot-check via `tools/list` JSON-RPC that `mo2_record_detail`'s description leads with "Get full interpreted field data for one or more records." (no "v2.9.X batch read mode" anywhere in the lead).
3. Spot-check that `mo2_plugin_conflicts`'s description includes the CELL/WRLD warning.
4. `session-strategy/SKILL.md` skill listing surfaces the new `description:` text in Claude Code's `available_skills`.

For v2.9.5 specifically, since there are zero Python behavior changes, the pre-tag sanity is operational only — confirm the version string and confirm the new descriptions are visible. Functional regression is impossible without code changes.

### Empirical skill description validation

**STATUS: ATTEMPTED, blocked by Windows env.** `anthropic-skills:skill-creator`'s `scripts/run_loop.py` was invoked (`max-iterations 5`, model `claude-opus-4-7`) against the eval set at `eval_set.json` (10 should-trigger + 10 should-not-trigger queries). Run blocked by two Windows-specific issues:

1. **(Fixed in-session)** `subprocess.run(["claude", ...])` failed with `WinError 2: file not found` because `claude` resolves to `.cmd` on Windows and Python's CreateProcess does not auto-resolve `.cmd` extensions. Patched both `improve_description.py` (line 26) and `run_eval.py` (line 71) to use `claude.cmd` explicitly. **These edits are session-temp and do NOT persist to the upstream skill-creator package** — the path includes a session-UUID so the patched files vaporize on session end.

2. **(Unfixed)** `select()` cannot poll subprocess pipes on Windows (only sockets). `run_eval.py:11` imports `select` and uses it for streaming output detection. Every query failed with `WinError 10038: An operation was attempted on something that is not a socket`. The script logged every query as 0/3 trigger rate, producing meaningless results across all 5 iterations. Train: 18/36 correct (precision=100% recall=0% accuracy=50%); identical numbers each iteration because the eval engine was returning constant "no trigger" for every query regardless of description.

**Implication:** The empirical validation rule codified in `feedback_descriptions_are_documentation.md` cannot run on Aaron's Windows machine without `run_eval.py` being rewritten to use a Windows-compatible streaming approach (e.g., `threading + queue` or `subprocess.communicate()` with a wall-clock timeout). Linux/macOS environments should work as-is. **Documented as a known limitation** in this handoff and in the feedback memory's How-to-apply section.

**Manual validation reasoning (substituting for the empirical loop):** The new `session-strategy` description was engineered against the established pattern from the other 10 working skills (concrete user-recognizable phrasings + action-led `Use when...` framing + pushy `even if` reinforcement). Pattern-matching against the eval set:

- Should-trigger (10): 7 queries contain explicit trigger keywords from the description ("modlist", "mod", "plugins", "NPC", "leveled list", "conflicts"); 3 queries are marginal (require Claude to infer the connection from context like "deer give 0 gold" mapping to record-investigation).
- Should-not-trigger (10): 5-6 queries are clear non-triggers (no overlap with trigger keywords); 3-4 queries are near-misses where the description's keywords appear in conceptual/historical contexts ("differences between leveled lists conceptually", "what does merging plugins mean"). The pushy framing means SOME over-triggering on these is expected and acceptable per skill-creator guidance.

Predicted real-world behavior: high recall (close to consumer Claude reaching for the skill on legitimate MCP work), moderate precision (some over-triggering on conceptual queries that share modding keywords). This is the deliberate trade-off — accept false positives to reduce false negatives, since the failure mode that triggered v2.9.5 was a false negative (consumer didn't load session-strategy and burned 3,500 calls).

**Follow-up action item (deferred):** When validation infrastructure is fixed (`run_eval.py` rewritten to be Windows-compatible, or run on a Linux/macOS dev box), retro-validate the v2.9.5 `session-strategy` description against this eval set. If empirical results suggest improvements, ship as v2.9.6.

## What's still pending after this commit

- **MO2 full restart** (Aaron): close MO2 entirely, reopen, start Claude Server.
- **Post-restart `mo2_ping` confirms v2.9.5** (this handoff updated with confirmation).
- **Hash-record commit** referencing this ship commit's SHA.
- **Tag v2.9.5** at the ship commit.
- **`git push origin main` + `git push --tags`**.
- **GitHub release** with `claude-mo2-setup-v2.9.5.exe` attached and the `RELEASE_NOTES_v2.9.5.md` body.

## Memory state

- `feedback_descriptions_are_documentation.md` — NEW. Captures the architectural rule + Windows env limitation + how-to-apply.
- `project_capability_roadmap.md` — UPDATED. v2.9.5 is now Current public release; v2.9.4 demoted to Previous; v2.9.3 demoted to Earlier.
- `project_v3_release_strategy.md` — UPDATED. v2.9.3, v2.9.4, v2.9.5 added to cadence list.
- `MEMORY.md` — UPDATED. v2.9.4 hook replaced with v2.9.5 hook; new feedback memory hook added.

## No plan-archive multi-phase

Prior v2.9.x releases (v2.9.0 → v2.9.4) had multi-phase ships with PLAN.md / MATRIX.md / per-Phase-handoff archives, race-probes, coverage-smoke matrix expansions, and pre-Phase-0 empirical validations. v2.9.5 is a single-session description-and-docs ship — no race-probe, no schema reconciliation, no coverage-smoke matrix expansion. The CHANGELOG.md v2.9.5 entry is the canonical narrative; this PHASE_1_HANDOFF.md is the sole handoff document; PLAN.md captures the design rationale.

This is the first single-session ship in the v2.9.x line. Pattern reinforcement candidate for `project_capability_roadmap.md`: when a release is description-and-docs-only (no code changes, no race-probe needed, no coverage-smoke expansion), single-session conductor ship is appropriate; the multi-phase plan archive structure is overhead.
