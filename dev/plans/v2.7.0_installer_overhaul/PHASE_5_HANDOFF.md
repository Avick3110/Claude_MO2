# Phase 5 Handoff — Sandbox install matrix (15/15 PASS; mid-phase architectural fixup landed)

**Phase:** 5
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~6h (includes architectural fixup + full re-run)
**Commits made:** `efae419` ([v2.7 P3+P4 fixup]) + this handoff commit
**Live install synced:** No (P5 is sandbox + non-destructive live smokes only; P6 owns live sync)

## What was done

P5 ran the T1–T15 sandbox matrix per PLAN.md § Phase 5. Interactive testing of the upgrade scenarios (T7 onward) surfaced two structural bugs in the P3 detector page. Rather than patch them, the architect (Aaron) called for removing the detector page entirely and collapsing detection + Keep/Change/Skip into the picker page. That fixup landed as a single combined `[v2.7 P3+P4 fixup]` commit during P5. The full matrix was then re-run against the refactored installer. All 15 tests pass.

### Matrix execution path

1. **Initial silent-subset run** (pre-fixup installer at `62b122b`): T1, T5, T6, T10, T15 PASS.
2. **T11 inspection** (installer/claude-mo2-installer.iss `InitializeSetup` lines 752-778): PASS — MB_OK single-button MsgBox, unconditional ShellExec to download URL, unconditional `Result := False`. No continue-path.
3. **T13 live**: JSON written to live install (`E:\Skyrim Modding\...\mo2_mcp\tool_paths.json`), MO2 restarted, `mo2_compile_script` called with `TestV27T13 extends ObjectReference + Debug.Notification`. Response `headers_used` listed ~150 VFS-aggregated `scripts/Source` dirs from Authoria's mods + the configured `E:/SteamLibrary/.../Data/Source/Scripts` appended as the last entry. Compile succeeded. JSON deleted; MO2 restarted again to clear the `tool_paths.py` process-lifetime cache. PASS.
4. **T14 live** (non-destructive): `mo2_record_detail` on `Skyrim.esm:000A2C94` returned fully-interpreted Mutagen fields — confirms `mutagen-bridge-path` MO2-plugin-setting bridge discovery path is unchanged and not interfered with by v2.7.0's `tool_paths.py` JSON layer. Also verified by grep of `tools_patching.py:341` `_find_bridge` priority chain (v2.6.1-equivalent). PASS.
5. **Interactive batch attempted (T2-T4, T7+)**: T2, T3, T4 PASS on first attempt (fresh installs — detector page auto-skipped). T7 first attempt exposed two bugs:
   - **Radio-group bug.** All 9 `TNewRadioButton`s (3 per row × 3 detected surfaces) shared a single Windows radio group because every button was parented to `g_DetectorPage.Surface`. Only one radio across the entire page could be selected at a time. `LayoutDetectorPage`'s loop set `g_Radio_Keep[I].Checked := True` three times in sequence, but each iteration unchecked the previous — final visual state was only Keep for the last detected surface.
   - **Selection reset on re-entry.** `CurPageChanged(g_DetectorPage.ID)` unconditionally called `RunDetection()`, which reset `g_Selection[I] := SEL_NONE` for all surfaces (line 550). Back-nav from the picker to the detector wiped the user's prior selections every time.
6. **Architectural fixup** — Aaron called for removing the detector page entirely (picker handles everything via Edit-field text). Landed as commit `efae419 [v2.7 P3+P4 fixup]`. See commit body for scope + rationale. ~263 lines of `.iss` deleted; file shrinks 1414 → 1151 LOC.
7. **Interactive cheatsheet re-written** for single-page UX at `C:\Users\compl\AppData\Local\Temp\mo2_p5_matrix\INTERACTIVE_CHEATSHEET.md`.
8. **Full matrix re-ran post-fixup**: silent T1/T5/T6/T10/T15 + interactive T2/T3/T4/T7/T8/T9/T12 — all PASS.

### Files touched (total session)

- `installer/claude-mo2-installer.iss` — refactored in `[v2.7 P3+P4 fixup]` commit.
- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` — inline plan-revision annotations at top of Phase 3 + Phase 4 sections; log entry in § Plan revisions. Original text preserved for audit trail.
- `dev/plans/v2.7.0_installer_overhaul/PHASE_5_HANDOFF.md` (this file).
- `dev/plans/v2.7.0_installer_overhaul/scratch/` — unmodified.
- No Python side changes. No changes to v2.6.1 installed live plugin. No release-prep docs changes (README, CHANGELOG, KNOWN_ISSUES — Phase 6 owns).

### Matrix — full results table

| # | Scenario | Mode | Expected | Observed | Pass |
|---|---|---|---|---|---|
| T1 | Fresh install, skip all 4 rows | Silent | JSON schema=1 both null; no plugin-dir binaries | JSON `{schema_version:1, papyrus_compiler:null, papyrus_scripts_dir:null}`, 85 bytes, no BOM; tool dirs hold only README.txt | ✅ |
| T2 | Fresh install, all 4 populated, PapyrusCompiler copy mode | Interactive | JSON compiler null + scripts populated; 3 binaries at plugin paths SHA-matching source | JSON matches; all 3 binaries SHA-identical to sources | ✅ |
| T3 | Fresh install, PapyrusCompiler JSON-reference mode | Interactive | JSON both keys populated; BSArch + nif at plugin; papyrus-compiler dir holds only README.txt | JSON shape matches; BSArch + nif SHA-match source; papyrus-compiler dir has only README.txt | ✅ |
| T4 | Filename mismatch (BSArch row → xEdit64.exe) + Yes override | Interactive | BSArch target filename stays `bsarch.exe` but bytes are xEdit64's | dst SHA `de12c968…` matches xEdit64 source (not BSArch); filename at destination is `bsarch.exe` (fixed); warning Yes/No dialog surfaced + acknowledged | ✅ |
| T5 | v2.6.1 upgrade (3 binaries, no JSON), Keep all | Silent | Binaries preserved SHA-identical; JSON first-time-written with both nulls | All 3 binary SHAs identical pre/post; JSON `{schema:1, nulls}`; no BOM | ✅ |
| T6 | v2.7 upgrade (BSArch+nif+JSON, no in-plugin compiler), Keep all | Silent | BSArch + nif SHA-preserved; JSON byte-identical; papyrus-compiler dir holds only README.txt | All 3 SHAs match pre-install; JSON byte-identical SHA; papyrus-compiler dir has README.txt only | ✅ |
| T7 | v2.7 upgrade, Change PapyrusCompiler copy→JSON | Interactive | BSArch + nif preserved; plugin-dir PapyrusCompiler deleted; JSON compiler = external path | BSArch + nif SHAs match pre; papyrus-compiler dir has only README.txt; JSON compiler = `C:\...\sources\creation-kit\PapyrusCompiler.exe` | ✅ |
| T8 | v2.7 upgrade, Change PapyrusCompiler JSON→copy | Interactive | BSArch + nif preserved; plugin-dir PapyrusCompiler now present (copied from external); JSON compiler null | BSArch + nif SHAs match pre; plugin-dir PapyrusCompiler SHA matches source; JSON both null | ✅ |
| T9 | v2.7 upgrade (all 4 surfaces), Skip all (clear every field) | Interactive | All plugin-dir binaries deleted; JSON both keys null | All 3 tool dirs hold only README.txt; JSON `{schema:1, nulls}` | ✅ |
| T10 | Future schema_version:99 JSON | Silent | Detector logs schema mismatch + treats JSON as absent; install-step writes schema:1 nulls; future keys purged | Install log: `schema_version=99 (expected 1); skipping JSON surfaces`; JSON rewritten to `{schema:1, nulls}`; `new_key_from_v2_8` gone | ✅ |
| T11 | .NET 8 hard-block (code review only) | Inspection | InitializeSetup: single MB_OK MsgBox, unconditional ShellExec, unconditional `Result := False`; no continue-path | Verified at lines 752-778: `MsgBox(..., mbCriticalError, MB_OK)` → `ShellExec('open', url, ...)` unconditional → `Result := False`. No IDNO/Cancel fall-through | ✅ (deferred live verify, inspection-only per architect decision) |
| T12 | Path-persistence regression (install A → run again, observe default Dir) | Interactive | Run #2 Dir page defaults to `{autopf}\Mod Organizer 2`, not T12a | Aaron observed `C:\Program Files\Mod Organizer 2` (generic default) on run #2; T12b install succeeded | ✅ |
| T13 | Live Papyrus additive-VFS (JSON-configured scripts dir appends to VFS) | Live | `mo2_compile_script` response `headers_used` contains VFS-aggregated paths AND the configured dir | `success: true`; `headers_used` lists ~150 VFS dirs (Fancy Fishing, Campfire, Frostfall, SKSE64, PapyrusUtil, …) + `E:/SteamLibrary/steamapps/common/Skyrim Special Edition/Data/Source/Scripts` appended LAST. Additive (not replacing). `TestV27T13.pex` landed in Claude Output | ✅ |
| T14 | MO2-settings-precedence (mutagen-bridge-path non-interference) | Live | Existing bridge discovery still works post-v2.7.0 | `mo2_record_detail Skyrim.esm:000A2C94` returned fully-interpreted Mutagen fields (Base, LinkedReferences, PersistentLocation, Placement, flags); confirms bridge resolved via `_find_bridge` priority chain unchanged from v2.6.1 | ✅ |
| T15 | User addon preservation across install + uninstall | Silent | `CLAUDE_TestAddon.md`, `kb/KB_TestAddon.md`, `.claude/skills/test-addon/SKILL.md` SHA-identical across install + uninstall | All 3 SHAs identical at pre-install, post-install, post-uninstall | ✅ |

### UTF-8 no-BOM confirmation

All scenarios producing `tool_paths.json` were verified via Python:
```python
with open(p, 'rb') as f: raw = f.read()
print(f'BOM={raw[:3] == b"\xef\xbb\xbf"}')  # always False
```

T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T12a, T12b, T13 (live), T15 — every JSON confirmed UTF-8 no-BOM, first bytes `{\n`, Python `json.load(encoding='utf-8')` parses cleanly.

## Verification performed

### Architectural fixup motivation

P5's T7 interactive test was the trigger. Two bugs surfaced:

1. **Radio-group bug** (introduced P3, never caught before P5):
   - All `TNewRadioButton`s for all rows shared `g_DetectorPage.Surface` as Parent → one Windows radio group → single-select across the whole page.
   - Missed by P3's own matrix because silent-mode auto-Keep bypasses radio UI entirely.
   - Missed by P4's matrix for the same reason (silent-mode matrix).
   - Missed by Aaron's P3/P4 interactive smokes because those focused on row-height DPI scaling, not radio behaviour.

2. **Selection reset on re-entry** (introduced P3):
   - `CurPageChanged(detector)` unconditionally calls `RunDetection()`, which resets `g_Selection[I] := SEL_NONE` (line 550).
   - Back-nav from picker re-enters the detector → selections wiped.
   - Documented in P4's handoff § Known issues #2 as the re-layout convention, but not understood as a bug until Aaron hit it interactively in P5.

### Fixup scope (commit `efae419`)

- Deleted: `CreateDetectorRow`, `LayoutDetectorPage`, `SyncNamedGlobalsFromArrays`, `g_DetectorPage`, `g_Selection[]`, `g_Label_Title[]`, `g_Label_Path[]`, `g_Radio_Keep/Change/Skip[]`, all `g_Detected_*` / `g_ExistingPath_*` / `g_Keep_*` / `g_Change_*` / `g_Skip_*` named globals, `SEL_NONE`/`SEL_KEEP`/`SEL_CHANGE`/`SEL_SKIP` constants, `SeedPickerSimpleRow`, `g_PickerDualWarning`, `g_PickerUserConfirmed`.
- Added: `g_PickerInitialized: Boolean` — first-entry-only seeding guard.
- Modified: `RunDetection` (dropped selection resets + sync calls); `LayoutPickerPage` (reads `g_Detected[]`/`g_ExistingPath[]` directly); `InitializeWizard` (picker parents off `wpSelectDir`); `CurPageChanged(picker)` (first-entry seeding with initialized guard); `CurStepChanged` (defense-in-depth seeding if initialized guard is still False at ssPostInstall).
- PLAN.md revised: inline `[Superseded 2026-04-24]` at Phase 3, `[Revised 2026-04-24]` at Phase 4, top-level ✏️ Plan revisions log entry.

Keep/Change/Skip semantics now derive from picker Edit-field text at install-step (behaviour `ApplyBinarySurface` was already implementing in original P4):

- Leave path as-is = Keep (install-step no-op; source equals target)
- Edit path = Change (install-step: copy new source → plugin-dir target)
- Clear field = Skip (install-step: delete existing plugin-dir binary)

### ISCC compile — clean

Post-fixup compile: `Successful compile (10.703 sec)` from `"C:\Utilities\Inno Setup 6\ISCC.exe" installer/claude-mo2-installer.iss`. Output at `build-output/installer/claude-mo2-setup-v2.7.0.exe`, SHA256 `4d98217…` (18:59 build was `2068f33…`; the timestamp + refactor changed bytes).

### Install registry state hygiene

Discovered mid-phase: Inno's `CreateUninstallRegKey=yes` + static `AppId={8E2F4A7C-9F3B-4F21-B5C5-2D9B8F7D3A0E}` means each silent install writes the AppId's registry entry to HKCU. Subsequent silent installs to *different* directories saw the stale entry and silently skipped the install (exit 0 but no actual file-copy or post-install step run). Mitigation during P5: clear `HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E2F4A7C-9F3B-4F21-B5C5-2D9B8F7D3A0E}_is1` between every silent-test run. Noted in the § Inno quirks section below for P6 awareness.

Also discovered: `/NOCANCEL` + `/VERYSILENT` + `/SUPPRESSMSGBOXES` combination caused exit=1 on upgrade scenarios (pre-existing plugin dir); replaced with `/FORCECLOSEAPPLICATIONS` + same silent flags for clean exit 0 on every scenario.

## Deviations from plan

1. **Detector page removed during P5 via `[v2.7 P3+P4 fixup]` commit.** Not merely a defect fix — architectural collapse of two pages into one. Motivation: interactive T7 exposed two bugs in the P3 detector that weren't worth patching individually given the picker page already had all the information to handle Keep/Change/Skip via its Edit-field text. Aaron made the scope call during P5 and adjudicated commit tagging as a single `[v2.7 P3+P4 fixup]`. PLAN.md inline-annotated with supersede markers preserving original intent.

2. **T11 run as code-review inspection only, not live.** Per architect decision (Aaron's adjudication): the P1 `InitializeSetup` rewrite is ~30 lines, isolated, and code-review surface is sufficient. Live verification deferred to any future .NET-absent environment (VM or equivalent) — not a v2.7.0 ship gate. Documented as Option (c) per PLAN.md § Phase 5 T11 coordination note.

3. **Silent mode `/NOCANCEL` flag dropped.** Original silent invocation used `/NOCANCEL /VERYSILENT /SUPPRESSMSGBOXES`; caused install to fail with exit=1 when the target dir had pre-existing content. Replaced with `/FORCECLOSEAPPLICATIONS` + same silent flags. All P5 silent-test runs used the new flag set. Unchanged in `.iss` source — pure command-line flag change.

4. **No T16 proposed.** The plan's 15 scenarios covered the surface area. T13 showed additive-VFS works; T14 showed non-interference; T15 showed addon preservation; T7 + T8 round-tripped the JSON/copy mode transition. No gap identified that a 16th scenario would fill without redundancy.

## Known issues / open questions

### Shipping behavioural notes worth documenting in P6

1. **Uninstall-registry hygiene.** Installing twice to different dirs with the same AppId creates a stale HKCU uninstall key pointing at the first install. Inno's default behaviour is conservative: subsequent installs honour `/DIR=` but leave both registry entries + both uninstall stubs side-by-side. For the test matrix this was a test-harness problem (registry cleared between runs). For real users, this is unlikely to matter — they rarely install the plugin twice to different MO2 instances. If they do, each instance gets its own full install + uninstall, and the registry key reflects whichever was installed most recently. Consider documenting in KNOWN_ISSUES.md or README.md Manual Install section. Or: set `AppId` dynamically per install path (v2.8 candidate; out of scope for v2.7.0).

2. **Back-nav from Ready → Picker preserves user edits.** The `g_PickerInitialized` guard ensures this. But Back-nav from Dir → Picker is also preserved (same guard). If the user changes the target MO2 dir on Back, the picker's Edit values do NOT re-detect against the new dir — they keep the original first-entry detection. For ship, this is the right trade-off (user edits survive Back); users who want fresh detection can Cancel and restart the installer. Worth a one-line mention in KNOWN_ISSUES.md.

3. **Picker page header text mentions "clear to uninstall".** Replaces the original "Leave any field empty to skip" phrasing. Re-verify visual rendering during P6's final smoke.

4. **PapyrusCompiler Row 2 dual-detection visual warning removed.** Originally P4 had a red "Detected both a copied binary AND a JSON reference. Choose one mode." label. Fixup removed it — the single-page UX has the checkbox state reflecting the detected mode directly (checked if JSON, unchecked if binary-or-none; JSON wins if both detected). The user changing the checkbox is the explicit mode switch. If anyone raises "but the UI used to warn me about dual-state" post-ship, the answer is: you can see the picker's pre-filled path + checkbox state at the top of the page; the install-step outcome is deterministic from those two controls.

### Non-issues

- **T13 live Papyrus test artifacts** (`TestV27T13.pex`) left in `Claude Output/Scripts/` alongside Aaron's existing `TestV261Compile.pex` + `ClaudeMo2P2Smoke.pex`. Aaron noted he'd batch-clean these.
- **All sandbox artifacts** under `C:\Users\compl\AppData\Local\Temp\mo2_p5_matrix\` are safe to delete.

## Preconditions for Phase 6

Phase 6 ships v2.7.0 publicly: updates CHANGELOG, README installer URL, KNOWN_ISSUES docs table, skills prerequisites, builds the final installer, live-syncs Aaron's install, tags, `gh release create`. Inherits:

- ✅ **All code in.** `[v2.7 P3+P4 fixup]` at `efae419`; P5 handoff this file. Clean working tree (after this handoff commits).
- ✅ **Installer builds clean.** `build-output/installer/claude-mo2-setup-v2.7.0.exe` at SHA `4d98217…` (post-fixup build); ready for release asset upload.
- ✅ **All 15 matrix tests pass** on the final installer. Binary landing correctness, JSON schema round-trip correctness, addon preservation, bridge non-interference, additive-VFS behaviour, path-persistence fix, .NET hard-block all verified.
- ✅ **UTF-8 no-BOM across every JSON-producing scenario.** `tool_paths.json` always starts with `{\n`, Python `json.load(encoding='utf-8')` parses every produced file.
- ✅ **Tool_paths.json survives uninstall.** T10 silent uninstall verified: JSON file persists post-uninstall (per `[UninstallDelete]` explicit non-entry + comment).
- ✅ **User addon files survive install AND uninstall.** T15 verified: CLAUDE_*.md, kb/KB_*.md, .claude/skills/** all byte-identical across both lifecycle events.
- ✅ **P1 .NET 8 hard-block verified via code review.** No ship blocker; live-environment verification deferred as documented.
- ✅ **Python side unchanged.** v2.7.0 Python (P2 `tool_paths.py` + `tools_papyrus.py` edits) already committed + live-synced; T13 + T14 live smokes confirm the Python JSON-config layer works end-to-end against the bridge and VFS.

### What P6 must do

1. Write `CHANGELOG.md` entry for v2.7.0 per PLAN.md § Phase 6 spec. Include headline items: path-persistence fix, .NET 8 hard-block, configurable tool paths via installer picker + `tool_paths.json`, **and note the mid-plan architectural fixup** (detector page removed → single picker page handles Keep/Change/Skip via Edit semantics).
2. Update README installer URL (2 occurrences of `claude-mo2-setup-v2.6.1.exe` → `-v2.7.0.exe`).
3. Update KNOWN_ISSUES.md header + add Option C docs table + add resolved-bugs rows for the path-persistence fix and .NET hard-block.
4. Update skill files' prerequisite language to reflect the installer picker (BSArch / nif-tool / papyrus-compilation skills).
5. Build final installer + live-sync + verify `mo2_ping` reports version 2.7.0 + one bridge smoke + one Papyrus smoke.
6. Tag + push + `gh release create`.

### Optional follow-ups (post-ship, not P6 scope)

- Consider dynamic `AppId` generation per install path (v2.8) — would obviate the registry-hygiene issue and allow side-by-side installs to different MO2 instances.
- Consider adding an MCP tool surface for `tool_paths.json` inspection + editing (`mo2_get_tool_paths` / `mo2_set_tool_path`) — out of scope for v2.7.0; candidate for v2.8 as noted in PLAN.md § Cleanup.
- Consider exposing the `mutagen-bridge-path` + `spooky-cli-path` MO2 plugin settings via the installer picker too — would unify config surfaces. Out of scope for v2.7.0; v2.8 candidate.

## Inno quirks encountered (for P6 awareness)

1. **Static AppId + `CreateUninstallRegKey=yes` → registry accumulates across installs.** See Known issues #1 above. Ship-relevant if P6 decides to document in KNOWN_ISSUES.md.
2. **Silent flag combination `/NOCANCEL` + pre-existing target dir → exit=1.** Replace with `/FORCECLOSEAPPLICATIONS /VERYSILENT /SUPPRESSMSGBOXES` for any silent-install test harness or CI. Not user-facing; just a test-harness note.
3. **`TNewRadioButton`s parented to the same `TWizardPage.Surface` form ONE radio group.** Per-row radio groups require per-row container parents (e.g. `TPanel` per row). This bit P3; it's not a future risk for v2.7.0 (no more detector page) but worth knowing if any future phase adds a multi-row radio UI.
4. **`UsePreviousAppDir=no` behaves as expected across repeated silent installs.** T12 verified it interactively; silent installs via `/DIR=` explicitly override regardless of `UsePreviousAppDir`.
5. **`/LOG=<path>` writes the install log even in `/VERYSILENT` mode.** Useful for post-hoc debugging. Log includes all Custom `Log()` calls, file-copy activity, and MsgBox content (even suppressed).
6. **`AnsiString(stringVar)` cast in Inno Pascal.** Works when `stringVar` is a `String` local; fails on `AnsiString('literal')`. The production `WriteToolPathsJson` uses the variable-cast form and compiles clean.
7. **`FileCopy` deprecated in Inno 6.7.1.** Use `CopyFile(src, dst, FailIfExists)`. Production code uses `CopyFile`.

## Files of interest for Phase 6

- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 6 — Ship v2.7.0" — full release-prep spec.
- `mo2_mcp/CHANGELOG.md` top entry (currently v2.6.1) — P6 adds v2.7.0 above this.
- `README.md` lines 7, 59 — installer URL references to bump.
- `KNOWN_ISSUES.md` lines 3-4, ~80-90 — version header + resolved-bugs table + the new Option C docs table to add.
- `.claude/skills/bsa-archives/SKILL.md`, `.claude/skills/nif-meshes/SKILL.md`, `.claude/skills/papyrus-compilation/SKILL.md` — prerequisites language updates.
- `installer/claude-mo2-installer.iss` — NOT to touch in P6 unless a last-minute regression surfaces; the refactored file is shipping.
- `build-output/installer/claude-mo2-setup-v2.7.0.exe` at SHA `4d98217…` — final matrix-passed build; P6 either re-builds (timestamp changes only; behaviour identical) or uses as-is for the GitHub release asset.

## Final Go/No-Go for Phase 6

**GO.** All 15 matrix tests pass on the refactored installer. No known ship-blocking bugs. Architectural fixup was caught + fixed + re-verified within P5's scope. PLAN.md audit trail intact. Working tree clean after this commit. Ready for Phase 6 release prep + live sync + public ship.
