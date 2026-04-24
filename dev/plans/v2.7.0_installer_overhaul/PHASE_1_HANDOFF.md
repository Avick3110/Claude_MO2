# Phase 1 Handoff — Installer policy fixes + version bump to v2.7.0 (MsgBox hard-block, path-persistence off)

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~1h
**Commits made:** 1 — see `git log --oneline main | grep "[v2.7 P1]"`
**Live install synced:** No (Phase 1 does not touch the live install per PLAN.md § Conventions)

## What was done

Three installer-side changes and one Python-side version bump, exactly as PLAN.md § Phase 1 specifies. No out-of-phase file changes. PLAN.md's Phase 5 test-matrix row T12 was updated to reflect P1's deferred Test B.

- `installer/claude-mo2-installer.iss:21` — `#define AppVersion "2.6.1"` → `"2.7.0"`.
- `installer/claude-mo2-installer.iss:39` — `UsePreviousAppDir=yes` → `no`. Installer now always shows `DefaultDirName={autopf}\Mod Organizer 2` as the directory-select default, regardless of previous install path.
- `installer/claude-mo2-installer.iss:175-201` — `InitializeSetup()` rewritten per Phase 0 Q1's locked Pascal shape. Replaces the v2.6.x `MB_YESNOCANCEL` dialog whose `IDNO` fall-through silently allowed install without .NET 8, with a `mbCriticalError` + `MB_OK` single-button dialog followed by an unconditional `ShellExec` to the Microsoft download page and `Result := False` to abort. No continue-anyway branch exists.
- `mo2_mcp/config.py:9` — `PLUGIN_VERSION = (2, 6, 1)` → `(2, 7, 0)`. `mo2_ping` will report `"version": "2.7.0"` once the live install syncs (deferred to Phase 6 per PLAN).
- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` T12 row — updated to mark Phase 5 T12 as the first-time verification for the .NET hard-block change, deferred from P1 per the dev-machine constraint (see "Known issues / open questions" below).

Files explicitly NOT touched (per PLAN.md § "Files NOT to touch"): README, CHANGELOG, KNOWN_ISSUES, any Python module beyond `config.py`. All deferred to Phase 2+ / Phase 6.

## Verification performed

- **Source review of `InitializeSetup()` rewrite:** read `installer/claude-mo2-installer.iss:175-201` after edit. Confirmed the `IDNO` fall-through is gone. No `response := MsgBox(..., MB_YESNOCANCEL)` remains; the dialog is `MB_OK` single-button. `Result := False` is unconditional inside the `if not IsDotNet8Installed()` block. No branch lets install continue without .NET 8.
- **Source review of `UsePreviousAppDir`:** `installer/claude-mo2-installer.iss:39` reads `UsePreviousAppDir=no` post-edit. Single-line verified.
- **Source review of version strings:** `installer/claude-mo2-installer.iss:21` reads `#define AppVersion "2.7.0"`; `mo2_mcp/config.py:9` reads `PLUGIN_VERSION = (2, 7, 0)`. Both confirmed via `Read` post-edit.
- **Installer build:** `powershell -File build/build-release.ps1 -BuildInstaller` — ISCC compiled clean in 9.140 sec. Output `build-output/installer/claude-mo2-setup-v2.7.0.exe` exists at 10.08 MB. No prior v2.7.0 artifact overwritten (checked: previous output dir contained v2.5.2, v2.5.3, v2.5.5, v2.6.0, v2.6.1 only — v2.7.0 is a first build, no version-lock violation).
- **Sandbox Test A — path-prompt reset on reinstall:** PASS.
  - Created throwaway `dev/P1_sandbox/test-mo2-A/ModOrganizer.exe` stub.
  - Install 1: ran installer with `/VERYSILENT /DIR=<abs>/test-mo2-A /LOG=install1.log` — exit 0, plugin files confirmed at `test-mo2-A/plugins/mo2_mcp/` via log grep of `Dest filename`.
  - Install 2: ran installer again without `/DIR`. Installer's directory-select page defaulted to `C:\Program Files (x86)\Mod Organizer 2` (DefaultDirName), **not** `test-mo2-A` (previous install location). Verified visually via the installer's `NextButtonClick` MsgBox — "ModOrganizer.exe was not found in: C:\Program Files (x86)\Mod Organizer 2" — which fires after the dir-select default is populated. Install 2 was aborted from this dialog without writing files.
  - Install 1 uninstalled silently via `unins000.exe /VERYSILENT`; `dev/P1_sandbox/` deleted.
- **Installer runs on dev machine (positive-path exercise of `IsDotNet8Installed() == True` branch):** PASS. Install 1 executed `InitializeSetup()` against the dev machine's .NET 8 Runtime, passed the check, proceeded through file copy.
- **Installer build output stability:** v2.6.0 and v2.6.1 installer .exe files in `build-output/installer/` untouched by this build. Per PLAN.md § Conventions ("Don't touch out-of-phase files" + `feedback_build_artifact_versioning`), released versioned artifacts stay locked.

## Deviations from plan

**Sandbox Test B (.NET hard-block, negative path) deferred to Phase 5 T12**, per Aaron's explicit instruction during this session: dev machine has .NET 8; renaming `%PROGRAMFILES%\dotnet\` would disable `mutagen-bridge.exe` in live MO2 while in use, which is not an acceptable side-effect for a P1 verification test. PLAN.md § Phase 5 T12 row updated inline (in this P1 commit) to record that Phase 5 must provision a .NET-8-absent environment (VM, fresh Windows account, or equivalent) before declaring P1's `InitializeSetup` rewrite fully verified. P1 ships positive-path verification only; negative-path is gated by P5 T12.

No other deviations. Every line edit matches PLAN.md § Phase 1's spec + Phase 0 Q1's locked Pascal shape.

## Known issues / open questions

**UAC re-launch drops silent-mode flags — Phase 5 advisory.** During Sandbox Test A install 2, the installer escaped silent mode despite being launched with `/VERYSILENT /SP-`. Most likely cause: Inno's `PrivilegesRequiredOverridesAllowed=dialog` (`installer/claude-mo2-installer.iss:44`) triggered a UAC elevation request, and the elevated re-launched process did not re-apply the silent-mode flags from the parent command line. The installer proceeded in GUI mode under elevated privileges (landing `DefaultDirName` at `C:\Program Files (x86)\Mod Organizer 2` — the 32-bit program files path under elevation, vs the `%LOCALAPPDATA%\Programs` path it would take unelevated).

This had a happy-accident side effect for Test A (cleaner visual evidence of the DirEdit default) but is a **non-obvious gotcha for Phase 5's 15-test matrix**, which relies heavily on silent-mode install cycles. Workaround options P5 should choose between per-scenario:

1. **Install into a user-writable path** (e.g. `%TEMP%\test-mo2-*`) so Inno elevates to `lowest` and never triggers UAC. Cleanest for automated test cycles.
2. **Launch the installer from an already-elevated shell** (Windows Terminal → "Run as administrator"). UAC is pre-satisfied; `/VERYSILENT` is honored end-to-end.
3. **Accept GUI visual inspection** for scenarios where elevation is intrinsic (e.g. writing to `C:\Program Files`). Log or screenshot the observed behavior.

P5 executor picks per-scenario. The test-matrix rows that simulate upgrade/keep/change/skip states on existing installs (T5–T9) benefit most from option 1 — user-writable paths keep the test fully automatable.

**No other known issues.** No code defects discovered; no open questions for Phase 2.

## Preconditions for Phase 2

Phase 2 creates `mo2_mcp/tool_paths.py` and extends `mo2_mcp/tools_papyrus.py`. Phase 1 did not touch either; v2.7.0 Python side is at Phase 0's re-verified shape modulo the `PLUGIN_VERSION` bump.

- ✅ **Phase 0 JSON schema locked** (Phase 0 handoff Q7): 2 keys + `schema_version: 1`. Phase 2's `tool_paths.py` spec in PLAN.md § Phase 2 encodes this.
- ✅ **Phase 0 re-verified `_find_papyrus_compiler()` + `_collect_header_dirs()`** (Phase 0 handoff Q5): both match PLAN's description at v2.6.1 and remain unchanged in v2.7.0 (Phase 1 did not touch `tools_papyrus.py`).
- ✅ **Version bump in place:** `PLUGIN_VERSION = (2, 7, 0)` in `config.py`. Phase 2's smoke test against the live install will show `mo2_ping` reporting `"version": "2.7.0"` after `-SyncPython`.
- ✅ **Installer policy fixes do not interact with Python side.** Phase 2 graceful-degrades when `tool_paths.json` is absent (PLAN.md § Phase 2 spec), so Phase 1's installer-only changes neither help nor hinder Phase 2's Python work.
- ✅ **Test B deferral documented in PLAN.md T12.** Phase 5 executor will not miss this.

All Phase 2 preconditions met.

## Files of interest for next phase

Phase 2 creates `mo2_mcp/tool_paths.py` and extends `mo2_mcp/tools_papyrus.py`. Files the P2 implementer should read:

- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 2 — Python config layer" — step-by-step spec + embedded `tool_paths.py` module shape.
- `dev/plans/v2.7.0_installer_overhaul/PHASE_0_HANDOFF.md` § Q7 (JSON schema final wording) + § Q5 (tool-lookup site re-verification) — schema contract + existing Python behavior P2 must preserve and extend.
- `mo2_mcp/tools_papyrus.py:67-77` — `_find_papyrus_compiler()` candidate list; P2 prepends a priority-0 JSON-override entry.
- `mo2_mcp/tools_papyrus.py:267-288` — `_collect_header_dirs(vfs_dir)`; P2 appends `papyrus_scripts_dir` from JSON (additive, not replacement) after the existing VFS-aggregation block, using the existing `seen` set for dedupe.
- `mo2_mcp/__init__.py:106-133` — `settings()` registers `port`, `output-mod`, `auto-start`, `mutagen-bridge-path`, `spooky-cli-path`. P2 does NOT touch this; JSON config is additive.
- `build/build-release.ps1` — P2 smoke test uses `-SyncPython -MO2PluginDir "E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp"` (Python-only sync, no `-SyncLive`).
