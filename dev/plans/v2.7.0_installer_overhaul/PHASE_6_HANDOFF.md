# Phase 6 Handoff — v2.7.0 release prep + ship

**Phase:** 6
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~2h
**Commits made:** This handoff commit + the `[v2.7 P6]` release-prep commit
**Live install synced:** Yes — `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp` (v2.7.0 verified via `mo2_ping`)

## What was done

P6 ships v2.7.0 publicly. Doc updates landed first (CHANGELOG, README, KNOWN_ISSUES, three skills) so the installer payload bundles the v2.7.0 versions. Final installer built; sandbox smoke verified JSON correctness + addon preservation; live sync verified `mo2_ping`/bridge/Papyrus end-to-end at v2.7.0.

### Files touched

- `mo2_mcp/CHANGELOG.md` — new top entry `## v2.7.0 — 2026-04-24`. Front-loaded "Mid-plan architectural simplification" paragraph documents the detector-page removal + Edit-text Keep/Change/Skip semantics. Subsections: Changed — installer (path persistence, .NET hard-block, Optional Tools picker page, filename-mismatch warning, JSON survives uninstall); Changed — Python config layer (`tool_paths.py`, `_find_papyrus_compiler` priority 0, `_collect_header_dirs` additive); Not changed; Migration.
- `README.md` — installer URL bumped `claude-mo2-setup-v2.6.1.exe` → `claude-mo2-setup-v2.7.0.exe` (lines 7, 59).
- `KNOWN_ISSUES.md` — header bumped v2.6.1 → v2.7.0; new "User-provided tools — how they're configured" section with Aaron's Option C docs table + JSON-reference + additive-VFS explanations; two new environmental-quirk entries (Inno registry hygiene on multi-instance installs; Back-nav re-detection); two resolved-bugs rows (path-persistence v2.7.0; .NET soft-block v2.7.0).
- `.claude/skills/bsa-archives/SKILL.md` — prereq line updated to mention the v2.7.0 installer's Optional Tools page as the primary placement path.
- `.claude/skills/nif-meshes/SKILL.md` — same pattern for nif-tool.exe.
- `.claude/skills/papyrus-compilation/SKILL.md` — Prerequisites section restructured into PapyrusCompiler discovery priority order (JSON override → in-plugin binary → `%USERPROFILE%` legacy fallbacks) + base-Skyrim script headers (VFS-aggregated + `tool_paths.json["papyrus_scripts_dir"]` additive).
- `dev/plans/v2.7.0_installer_overhaul/PHASE_6_HANDOFF.md` (this file).

### Build output

Final installer rebuilt **after** doc updates so the bundled README/KNOWN_ISSUES/CLAUDE.md/skill files are the v2.7.0 versions:

- Path: `build-output/installer/claude-mo2-setup-v2.7.0.exe`
- Size: 10,632,420 bytes (10.14 MB)
- SHA256: `FC2D077633733DD8C4FDC86C89A7C16ED423CACDDE872C480DE3574438B76272`
- ISCC compile: `Successful compile (15.172 sec)`
- Installer payload bundles all 11 skills' SKILL.md files plus the four top-level docs (README, KNOWN_ISSUES, CLAUDE.md, KNOWLEDGEBASE.md) via the `[Files]` recursive copy at `installer/claude-mo2-installer.iss:108-120`.

## Verification performed

### Sandbox smoke (one-shot, fresh install)

Per architect guidance: not a re-run of P5's 15-test matrix; one fresh install + addon-preservation check.

- **Setup:** `C:\Users\compl\Desktop\mo2-v270-ship-smoke\` with stub `ModOrganizer.exe` + pre-seeded addon `plugins/mo2_mcp/CLAUDE_TestSmoke.md` (6 bytes, SHA `8D76EF077804E6A447DF0C830543423673470031678EA8FE740C1EEC4B5F0EB3`).
- **HKCU registry:** `{8E2F4A7C-…}_is1` cleared before run.
- **Install command (corrected silent flags from P5):**
  ```
  installer.exe /FORCECLOSEAPPLICATIONS /VERYSILENT /SUPPRESSMSGBOXES \
                /DIR=<sandbox> /LOG=<sandbox>/install.log /NORESTART /CURRENTUSER
  ```
- **Exit code:** 0.
- **`tool_paths.json`:** 85 bytes, no BOM, first bytes `b'{\n  "schema_vers'`, parses clean as `{"schema_version": 1, "papyrus_compiler": null, "papyrus_scripts_dir": null}`.
- **Tool dirs:** `bsarch/`, `nif-tool/`, `papyrus-compiler/` each contain only README.txt (skip-all install — no binaries supplied).
- **Mutagen bridge + Spooky CLI:** present at expected paths.
- **Addon preservation:** post-install SHA of `CLAUDE_TestSmoke.md` = `8D76EF077804E6A447DF0C830543423673470031678EA8FE740C1EEC4B5F0EB3` — **byte-identical** to pre-install.

### Live sync verification

After `build-release.ps1 -SyncLive -MO2PluginDir "E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp"` and a full MO2 restart:

- **`mo2_ping`:**
  ```json
  {
    "status": "ok",
    "server": "MO2 MCP Server",
    "version": "2.7.0",
    "mo2_version": "2.5.2.0",
    "game": "Skyrim Special Edition",
    "profile": "AL Custom"
  }
  ```
  Version confirms v2.7.0 Python live.

- **`mo2_record_detail Skyrim.esm:000A2C94`:** Returned PLACEDNPC `HousecarlWhiterunRef` (USSEP wins; load_order 62) with fully-interpreted Mutagen fields — `Base`, `LinkedReferences[]`, `PersistentLocation`, `LocationReference`, `Placement` (Position + Rotation), flags. Confirms bridge discovery via `_find_bridge` priority chain unchanged from v2.6.1; `tool_paths.json` JSON layer is non-interfering.

- **`mo2_compile_script TestV270Ship`:** `success: true`. `.pex` written to `E:/Skyrim Modding/Authoria - Requiem Reforged/mods/Claude Output/Scripts/TestV270Ship.pex`. `headers_used` lists ~150 VFS-aggregated `scripts/Source` dirs (no `tool_paths.json` present at live → priority-0 papyrus_compiler resolves to None → falls through to in-plugin binary; additive-VFS path empty since `papyrus_scripts_dir` not configured). Confirms compile-step is regression-free at v2.7.0.

### Pre-sync sanity check

Before the live smokes, verified the sync landed correctly:

- `config.py` shows `PLUGIN_VERSION = (2, 7, 0)`.
- `diff -rq` between repo `mo2_mcp/` and live install: zero `.py` differences (21 files byte-identical).
- `tool_paths.py` present at live (4663 bytes).
- `mutagen-bridge.exe` timestamp Apr 24 23:45 (fresh from this build).
- `spookys-automod.exe` timestamp Apr 18 16:41 (unchanged — no v2.7.0 source changes; `dotnet` reports "All projects are up-to-date").
- No stale `tool_paths.json` at live install (correct v2.6.1-equivalent fallback state).

## Deviations from plan

1. **Build sequencing.** Plan-Phase-6 § Steps had build at step 5 and doc updates earlier; the architect's mid-phase guidance explicitly required doc updates → build → sandbox → live sync, which is what was done. The `.iss` `[Files]` section bundles `README.md`, `KNOWN_ISSUES.md`, `CLAUDE.md`, and the skill files; building before doc edits would ship stale content in the installer.

2. **Sandbox smoke scoped to a single fresh-install scenario.** Per architect guidance: P5 already ran the full 15-test matrix on the same shipping installer (post-fixup). P6 only verifies (a) installer builds clean, (b) JSON + binary + addon preservation work end-to-end on one install, (c) live sync + smoke. Re-running the full matrix would be duplicative.

3. **`KNOWN_ISSUES.md` Resolved-bugs section also includes existing rows above the v2.7.0 additions.** No prior row text was modified — only two new rows appended after the v2.6.1 entry.

4. **CLAUDE.md not modified.** PLAN.md § Phase 6 listed CLAUDE.md as "verify `tool_paths.json` is referenced where modlist-specific config would otherwise be expected (skim)." On read, CLAUDE.md is the user-facing instructions that ship with the plugin; `tool_paths.json` is an installer/runtime config concern documented in CHANGELOG + KNOWN_ISSUES + the papyrus-compilation skill. No change needed in CLAUDE.md.

## Known issues / open questions

None ship-blocking. Documented forward-looking items:

1. **Inno static `AppId` registry hygiene** — KNOWN_ISSUES.md environmental-quirks entry calls this out for multi-instance users. Permanent fix candidate for v2.8: dynamic `AppId` per install path.
2. **Back-nav from Dir → Picker preserves user edits** — KNOWN_ISSUES.md environmental-quirks entry documents the design choice (edit survival > re-detection). Workaround: Cancel + restart for fresh detection.
3. **PapyrusCompiler dual-detection visual warning removed** — operational knowledge from P5 fixup; not a CHANGELOG item. If support surfaces ask about the removed red label, the answer is: pre-filled path + checkbox state are the explicit signal; install-step outcome is deterministic from those two controls.

## Pre-push state for Aaron

Awaiting explicit go before push + release create. State at this gate:

- **Local commit:** `<filled in once committed>` — `[v2.7 P6] v2.7.0 release prep: CHANGELOG, README, KNOWN_ISSUES, skills`
- **Local tag:** `v2.7.0` (annotated, msg `v2.7.0 release`)
- **Installer SHA256:** `FC2D077633733DD8C4FDC86C89A7C16ED423CACDDE872C480DE3574438B76272`
- **Installer size:** 10,632,420 bytes (10.14 MB)
- **Installer path:** `build-output/installer/claude-mo2-setup-v2.7.0.exe`
- **Sandbox smoke:** PASS (JSON, binaries, addon preservation all green)
- **Live `mo2_ping`:** version 2.7.0
- **Live bridge smoke (`mo2_record_detail`):** PASS
- **Live Papyrus smoke (`mo2_compile_script TestV270Ship`):** PASS
- **Push pending:** local stack from P0/P1/P2/misc-doc/P3/P4/P3+P4-fixup/P5/P6 — 9 commits ahead of origin
- **Release pending:** `gh release create v2.7.0` with installer asset

## Post-release cleanup deferred to Aaron

Test artifacts in `Claude Output\Scripts\` accumulated across the v2.7.0 plan's testing:
- `TestV261Compile.pex` (v2.6.1 baseline)
- `ClaudeMo2P2Smoke.pex` (P2 Python sync smoke)
- `TestV27T13.pex` (P5 T13 live additive-VFS test)
- `TestV270Ship.pex` (P6 ship smoke — this session)

Aaron noted in P5 he'd batch-clean these. No automation needed.

## Migration arc summary

The v2.7.0 plan ran 7 phases (P0–P6) over ~2 calendar days. End state vs. the plan's original intent:

- **Path-persistence bug → fixed.** `UsePreviousAppDir=no` (Phase 1).
- **.NET 8 soft-block → hard-block.** Single-button `MB_OK` + unconditional `ShellExec` + unconditional abort (Phase 1).
- **4 user-provided tool surfaces configurable via installer.** BSArch, nif-tool, PapyrusCompiler (copy or JSON-reference), Papyrus Scripts dir (Phases 3+4 → fixup → ship).
- **Python additive-VFS for Papyrus headers.** `tool_paths.json["papyrus_scripts_dir"]` appends to MO2's VFS aggregation rather than replacing it (Phase 2).
- **JSON-config schema locked at v1.** `{schema_version, papyrus_compiler, papyrus_scripts_dir}`; future schema bumps reserve future capability without breaking v2.7.0 readers.
- **Mid-plan architectural simplification.** Two structural Inno bugs (shared radio group; selection reset on re-entry) caused the design to collapse from two pages (detector + picker) to one (picker handles detection + Keep/Change/Skip via Edit text). ~263 lines of installer Pascal deleted; two structural bugs eliminated. Documented in PLAN.md inline annotations + Plan-revisions log + this handoff.
- **No regressions to the 29 MCP tools.** Surface unchanged; bridge non-interference verified live (T14 in P5 + `mo2_record_detail` in P6).

**v2.7.0 plan COMPLETE.**

## Files of interest

- `mo2_mcp/CHANGELOG.md` § v2.7.0 — user-facing release log.
- `KNOWN_ISSUES.md` § "User-provided tools — how they're configured" — Option C reference for users.
- `installer/claude-mo2-installer.iss` — single-page picker design as shipped at commit `efae419`.
- `mo2_mcp/tool_paths.py` — JSON config loader.
- `mo2_mcp/tools_papyrus.py` — `_find_papyrus_compiler` (priority-0 JSON) + `_collect_header_dirs` (additive append).
- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § Plan revisions — architectural-fixup audit trail.
- All seven phase handoffs (`PHASE_0_HANDOFF.md` through `PHASE_6_HANDOFF.md`) — full plan history.
