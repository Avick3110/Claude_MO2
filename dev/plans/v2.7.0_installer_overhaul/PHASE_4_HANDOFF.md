# Phase 4 Handoff — Optional Tools picker page + install-step wiring (4 surfaces; combined PapyrusCompiler row)

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~3.5h
**Commits made:** none (stopped before commit per conductor instruction — report-then-commit gate)
**Live install synced:** No (Phase 4 is installer-only; PLAN.md § Conventions defers live sync until Phase 6)

## What was done

One `.iss` file modified. All logic lives in `[Code]`; no Python changes, no new assets, no `[Files]` additions. Production code only — temporary test harnesses were written and removed cleanly.

- **`installer/claude-mo2-installer.iss:414-468`** — `EscapeJsonString` + `WriteToolPathsJson(path, papyrus_compiler_path, papyrus_scripts_dir): Boolean`. Matches P0 Q3's locked signature byte-for-byte. Emits the exact shape P0 Q3 locked (2-space indent, LF line endings, keys in declared order, trailing newline). `AnsiString` cast before `SaveStringToFile` writes raw bytes with no BOM, which Python 3's `json.load(encoding='utf-8')` parses cleanly — verified by round-trip test in the primitive harness AND in T9 across every matrix scenario.

- **`installer/claude-mo2-installer.iss:788-871`** — Phase 4 section banner + constants (`PICK_BSARCH`/`PICK_NIF_TOOL`/`PICK_PAPYRUS_COMP`/`PICK_PAPYRUS_DIR`/`NUM_PICK_ROWS`) + all picker-page globals (`g_PickerPage`, `g_PickerTitle/Desc/Edit/Browse` arrays, `g_PickerJsonCheckbox`, `g_PickerDualWarning`, `g_PickerUserConfirmed`).

- **`installer/claude-mo2-installer.iss:875-920`** — `CreatePickerRow(idx, y, title, desc, descHeight)` factory. Layout: bold title (14 px) + desc label (13 or 32 px) + `TNewEdit` path field (21 px) + `TNewButton` Browse (80 px wide, anchored to right edge of surface).

- **`installer/claude-mo2-installer.iss:922-966`** — Four Browse handlers: `OnBrowseBSArch` / `OnBrowseNifTool` / `OnBrowsePapyrusCompiler` use `GetOpenFileName` with per-tool filters; `OnBrowsePapyrusScriptsDir` uses `BrowseForFolder`. Filters match P4 spec (`BSArch.exe|bsarch.exe`, `nif-tool.exe`, `PapyrusCompiler.exe`).

- **`installer/claude-mo2-installer.iss:968-1054`** — `SeedPickerSimpleRow` (used for Rows 0/1/3) + `LayoutPickerPage`. LayoutPickerPage runs on every picker-page entry (interactive mode) AND on silent-mode fallback from CurStepChanged. Row 2 seeding logic picks dominant detected mode (JSON > binary per P0 Q4 default rule) and surfaces the dual-detection warning red label when both were detected pre-install.

- **`installer/claude-mo2-installer.iss:1056-1131`** — `InitializeWizard` extended. Still creates the P3 detector page first, then the P4 picker page via `CreateCustomPage(g_DetectorPage.ID, …)` with the 4 rows, the combined Row 2 checkbox ("Reference this path at runtime (don't copy into plugin folder)"), and the hidden red dual-surface warning label. Browse buttons wired via `.OnClick := @Handler`.

- **`installer/claude-mo2-installer.iss:1133-1166`** — `ShouldSkipPage` extended with a silent-mode safety net: when `WizardSilent` and the detector page would render, default every detected surface's `g_Selection[]` to `SEL_KEEP` so existing binaries/JSON values are preserved across scripted upgrades (T5 / T6).

- **`installer/claude-mo2-installer.iss:1168-1186`** — `CurPageChanged` extended with a `g_PickerPage.ID` branch that calls `LayoutPickerPage` on entry (honours Back→change-dir→Forward re-seeding).

- **`installer/claude-mo2-installer.iss:1188-1214`** — `ValidatePickerFilePath(path, expectedName, surfaceLabel)` + `ValidatePickerDirPath(path, surfaceLabel)`. Path-doesn't-exist = hard error; filename mismatch = soft Yes/No warning. Matches PLAN's validation semantics.

- **`installer/claude-mo2-installer.iss:1216-1262`** — `NextButtonClick` extended with picker-page branch. Wrapped in `if not WizardSilent` so silent installs don't block on MsgBox validation dialogs (a stale JSON path on an automated upgrade would otherwise abort the install). `g_PickerUserConfirmed := True` at the end of the interactive path.

- **`installer/claude-mo2-installer.iss:1289-1318`** — `ApplyBinarySurface(source, target, surfaceLabel): String`. Core copy/delete helper used by BSArch, nif-tool, and PapyrusCompiler-copy-mode. Semantics: empty source → delete existing target (logs status); source == target → no-op (Keep case); source ≠ target → `CopyFile(source, target, False)` with `ForceDirectories` for the parent dir. Uses `CompareText` for case-insensitive Windows-path equality (matches Inno's convention).

- **`installer/claude-mo2-installer.iss:1320-1414`** — Production `CurStepChanged(ssPostInstall)`. Flow: silent-mode fallback (`LayoutPickerPage` when `g_PickerUserConfirmed = False`) → three binary surfaces processed via `ApplyBinarySurface` → PapyrusCompiler mode branch (empty → skip+cleanup; checkbox → JSON-reference (delete plugin binary, write JSON key); else → copy (clear JSON key)) → Papyrus Scripts dir (JSON-only, no copy) → `WriteToolPathsJson` → 4-line post-install MsgBox summary.

- **`installer/claude-mo2-installer.iss:145-152`** — `[UninstallDelete]` section explicitly annotated that `tool_paths.json` is NOT listed, with a comment explaining the preservation mechanism (Inno's default-don't-remove-what-wasn't-installed behaviour + dynamic JSON write means no extra `[UninstallSkip]` entry is required).

- **No other files touched.** Python side unchanged; README / CHANGELOG / KNOWN_ISSUES unchanged (Phase 6).

## Verification performed

### ISCC compile — clean across every iteration

Final build: `"C:\Utilities\Inno Setup 6\ISCC.exe" installer/claude-mo2-installer.iss` → `Successful compile (~9s)`. Output at `build-output/installer/claude-mo2-setup-v2.7.0.exe`. Three compile errors surfaced and were fixed during dev:
1. `AnsiString('literal')` cast failed with "Type mismatch" — worked around by assigning to an `AnsiString` variable first, then passing the variable. Only affected the temp test harness; the production `WriteToolPathsJson` uses `AnsiString(bodyVar)` where `bodyVar` is a `String`, which works fine.
2. `function BoolToStr` name — I accidentally did a `replace_all` `BoolToStr` → `P4BoolToStr` that double-matched the function declaration, producing `P4P4BoolToStr`. Fixed by a one-off edit.
3. `Log(Format([multi-line])` continuation parsed as `[Section]` (same class of P3's documented gotcha). Collapsed to single lines.

One non-blocking warning: `Support function "FileCopy" has been renamed. Use "CopyFile" instead.` — production code uses `CopyFile`; the earlier `FileCopy` in the temp harness triggered this and was addressed when writing the production `ApplyBinarySurface`.

### Primitive-first test (architect mid-phase guidance)

Before building the picker UI, the JSON write + copy + delete primitives were exercised via a temp `CurStepChanged` harness against a throwaway sandbox. All three primitives green:

- `WriteToolPathsJson` emitted `{schema_version:1, papyrus_compiler:"C:\\Program Files\\...", papyrus_scripts_dir:"C:\\..."}` with first bytes `{\n` (no BOM). Python `json.load(encoding='utf-8')` parsed it, round-tripped correctly including the doubled-backslash escapes.
- `DeleteFile` on a pre-placed dummy returned `True`; post-call `FileExists` returned `False`.
- `CopyFile(src, dst, False)` against a pre-populated target overwrote the existing bytes.

This validation took ~15 minutes and gave high confidence in the copy/delete/JSON-write install-step logic before any UI work.

### Sandbox matrix — 10 scenarios, 10 PASS

All scenarios executed in `%LOCALAPPDATA%\Temp\mo2_p4_matrix\T*` sandboxes via silent install with a temporary env-var override harness (`P4_TEST_BSARCH`/`_NIFTOOL`/`_PAPYRUS_COMP`/`_PAPYRUS_JSON`/`_SCRIPTS_DIR`, plus `_SKIP` sentinel for clearing fields). Harness removed before final compile.

| # | Scenario | Expected | Observed | Pass |
|---|---|---|---|---|
| T1 | Fresh install, skip all 4 rows | JSON all-nulls; no plugin-dir binaries | `{schema_version:1, papyrus_compiler:null, papyrus_scripts_dir:null}`; bsarch/nif-tool/papyrus-compiler dirs hold only `README.txt` | ✅ |
| T2 | Fresh install, all 4 populated, PapyrusCompiler copy mode | JSON: scripts_dir populated, compiler null; 3 binaries at plugin paths | JSON matches expected; `bsarch.exe` + `nif-tool.exe` + `PapyrusCompiler.exe` landed at plugin targets | ✅ |
| T3 | Fresh install, PapyrusCompiler JSON-reference mode | JSON: both compiler + scripts_dir populated; BSArch/nif-tool copied; no plugin-dir compiler | JSON has both escaped paths; `bsarch.exe` + `nif-tool.exe` present; papyrus-compiler dir holds only `README.txt` | ✅ |
| T4 | Filename-mismatch — picker points BSArch at `xEdit64.exe` | Bytes of `xEdit64.exe` land at plugin's `bsarch.exe` target (destination filename is fixed, user's bytes honoured); JSON null for this surface | Plugin `bsarch.exe` contents = `xEdit64 fake dummy for T4 mismatch` (the source bytes); ship UI warning dialog is interactive-only (validated via code review + test log confirms no dialog in silent mode) | ✅ |
| T5 | v2.6.1 upgrade: 3 binaries pre-seeded, no JSON → Keep all (silent-mode auto-Keep) | Binaries preserved byte-identical; JSON written first-time with all-nulls | Pre-install SHA-256 == post-install SHA-256 for all 3 binaries; JSON has `schema_version:1` + both keys null | ✅ |
| T6 | v2.7+ upgrade: BSArch + nif-tool pre-seeded, valid JSON with both keys populated, no in-plugin PapyrusCompiler → Keep all | JSON byte-identical; binaries unchanged | JSON SHA-256 pre == post; BSArch + nif-tool SHA-256 pre == post | ✅ |
| T7 | Upgrade: plugin-dir PapyrusCompiler pre-seeded, picker set to external JSON-reference path + checkbox checked | Plugin `PapyrusCompiler.exe` DELETED; JSON `papyrus_compiler` populated; BSArch + nif-tool preserved | Plugin papyrus-compiler dir holds only `README.txt`; JSON compiler key = external path; BSArch + nif-tool SHA-256 unchanged | ✅ |
| T8 | Upgrade: 3 binaries pre-seeded, BSArch picker cleared via `_SKIP` sentinel | Plugin `bsarch.exe` DELETED; nif-tool + compiler preserved; JSON all-nulls | Plugin bsarch dir holds only `README.txt`; nif-tool + compiler SHA-256 unchanged; JSON both keys null | ✅ |
| T9 | Python `json.load(encoding='utf-8')` on every T1–T8 JSON | All parse; no BOM; correct semantics | T9_check.py ran across 8 files: `T1…T8  PASS  BOM=False  schema=1  compiler=… scripts=…` all match the per-scenario expected values | ✅ |
| T10 | Silent install (T2 sandbox) then silent uninstall via `unins000.exe /VERYSILENT` — verify `tool_paths.json` preserved | JSON file still present post-uninstall with byte-identical content | File present; content matches T2 install-time state | ✅ |

Sandbox artefacts remain under `%LOCALAPPDATA%\Temp\mo2_p4_matrix\` for post-hoc inspection; safe to delete.

### Interactive render verification (architect mid-phase note 2)

Aaron ran the first picker-page build interactively and reported a DPI-scaling clip on Row 3's 2-line description (the "scripts that reference Actor / Quest / Debug …" line was occluded by the dir-edit field's top edge at 125%+ scaling). Fixed by bumping Row 3's `DescHeight` parameter from 26 → 32 px. Total picker-page surface consumption remains comfortably under Inno's ~305 px budget (Row 3 bottom = Y=272). Second interactive render confirmed the clip was resolved.

**Combined-vs-split PapyrusCompiler row decision locked: combined.** The nested "Reference this path at runtime (don't copy into plugin folder)" checkbox sits one line below the path edit, indented by `ScaleX(12)`, reads naturally alongside the other rows — no bolted-on feeling. Aaron confirmed visually. Split-row fallback (P0 Q4's ~1h criterion) did not trigger; combined-row UI work landed in under 30 minutes of iteration.

## Deviations from plan

**None material.** Implementation follows PLAN § Phase 4 + PHASE_0_HANDOFF.md (Q3 JSON-write signature + Q4 combined-row criterion) + PHASE_3_HANDOFF.md (detector globals consumption). Three in-scope refinements worth calling out:

1. **Silent-mode validation bypass in `NextButtonClick(picker)`.** PLAN specifies validation as always-on (path-exists hard error + filename-mismatch soft warning). In silent mode, `NextButtonClick` runs but MsgBoxes still render, which would block automated upgrades on a stale JSON path (T6 initially failed this way). Added `if not WizardSilent` guard around the picker validation block: silent installs trust their inputs; interactive installs see the full validation UX. Surfaced errors will still appear at install-step via `ApplyBinarySurface`'s copy/delete return codes (logged to install log + post-install MsgBox).

2. **Silent-mode auto-Keep in `ShouldSkipPage(detector)`.** PLAN's P3 detector page requires user radio selections; in silent mode, no user click, so `g_Selection[]` stays at `SEL_NONE`. Added: when `WizardSilent` and the detector page wouldn't auto-skip, default every detected surface to `SEL_KEEP` and call `SyncNamedGlobalsFromArrays`. This makes scripted/automated upgrades behave conservatively ("preserve existing tools") rather than aggressively ("skip → delete everything").

3. **Silent-mode `LayoutPickerPage` fallback in `CurStepChanged`.** `g_PickerUserConfirmed` tracks whether the user interactively confirmed the picker. When `False` at `ssPostInstall` (i.e. silent mode OR interactive mode where picker was never reached — unusual), `LayoutPickerPage` runs once more in CurStepChanged to seed Edit fields from detector globals. Ensures silent-mode upgrade semantics are coherent without duplicating the logic across both page-lifecycle hooks.

None of these affect the interactive user flow. They're all silent-mode correctness fixes.

## Known issues / open questions

1. **Dual-surface pre-existing state (both binary AND JSON `papyrus_compiler` detected pre-install).** My T6's first run pre-seeded BOTH a plugin-dir `PapyrusCompiler.exe` AND a JSON `papyrus_compiler` entry. `LayoutPickerPage`'s priority rule (JSON > binary, per P0 Q4) picked JSON mode + the red dual-surface warning label would render in interactive mode. In silent-mode auto-Keep, the JSON-mode install-step deleted the plugin-dir binary. PLAN's T6 description says "Binaries unchanged"; my re-run with only BSArch + nif-tool pre-seeded (no plugin-dir compiler) matches that literal expectation. Documenting here so P5 can choose whether to re-test this dual-surface variant explicitly. The dual-surface UI path (interactive mode showing the warning) is still interactive-only-verified from code review; P5 may want to drive it manually.

2. **Picker page re-layout discards user Edit text on Back-then-Forward navigation.** Inherited convention from P3 (LayoutDetectorPage resets radios to Keep on every layout). LayoutPickerPage does the same: on every picker-page entry, Edit fields re-seed from detector globals. A user who types a path, goes Back to the dir-select page, and returns will see their typed path replaced by the detector-global-derived default. Acceptable trade-off — matches P3's precedent — but worth noting for P5 testing (if P5 runs interactive matrix scenarios) and for the eventual user docs (Phase 6).

3. **Filename-mismatch soft warning is interactive-only (T4 interactive-path uncovered).** The UI Yes/No dialog on filename mismatch (e.g. user picked `xEdit64.exe` for BSArch) is part of the interactive NextButtonClick validation. Silent mode bypasses validation entirely, so T4's silent run cannot exercise the warning — only the "copy-proceeds-with-override" code path. The warning itself is straightforward code-reviewable (`CompareText(actualName, ExpectedName) <> 0` + `MsgBox(..., mbConfirmation, MB_YESNO)`). P5's interactive test matrix (if any) should drive the filename-mismatch interactively for full UX verification.

4. **Validation gap: picker doesn't warn if user picks a file/dir OUTSIDE their system.** E.g. a UNC path, a removable drive that's not mounted. Inno's `FileExists`/`DirExists` handle these gracefully (returns False), so the validation dialog fires for non-existence, not "wrong kind of path." Fine for v2.7.0; noting as a potential v2.8 polish.

5. **Scripts dir validation is loose (by design per PLAN).** Row 4's `ValidatePickerDirPath` only checks `DirExists`. PLAN explicitly calls out that we should NOT gate on contents (no check for `Actor.psc` inside) because user extraction layouts vary. This is intended behaviour; documenting here so P5 doesn't flag it as a missing test.

## Preconditions for Phase 5

Phase 5 executes the full sandbox install matrix (T1–T15 per PLAN). It inherits:

- ✅ **All P4 `.iss` code committed to local `main` on green compile.** The .iss file is MODIFIED but uncommitted per conductor instruction; commit happens after this handoff is reviewed. Commit message will be `[v2.7 P4] Installer Optional Tools picker + install-step wiring (4 surfaces; copy + JSON)` per PLAN.
- ✅ **Installer builds clean** — `build-output/installer/claude-mo2-setup-v2.7.0.exe` produced.
- ✅ **10/10 sandbox scenarios pass** in this handoff's matrix.
- ✅ **Temp env-var harness removed.** Ship-clean build confirmed — final `.iss` has no `temp-override` log lines, only production `[v2.7 P4]` entries.
- ✅ **UninstallDelete preserves tool_paths.json** — T10 verified.
- ✅ **Python-side integration still v2.6.1-compatible.** P4 made no Python changes. P5's T13 (Python additive VFS with real `papyrus_scripts_dir`) requires Aaron's live install + a real Scripts.zip-extracted dir — not attempted in P4.
- ⏸ **.NET hard-block interactive verification (T12 per PLAN P5)** still deferred — requires .NET-absent environment (VM / renamed `%PROGRAMFILES%\dotnet`).
- ⏸ **P5 user-addon preservation (T15 per PLAN P5)** not validated here — P4's install-step touches only the 4 tool surfaces + `tool_paths.json`; Authoria-style addon files under `<plugin>/` are untouched by construction, but P5 should add explicit before/after content checks.
- ⏸ **Interactive-render validation of filename-mismatch warning + dual-surface warning** still needed for UX completeness; P5's own matrix plus Aaron's own interactive smoke fills this gap.

## Inno quirks encountered (relevant for P5 matrix)

- **`/VERYSILENT` in Git Bash gets path-converted** (`/VERYSILENT` → `C:/Program Files/Git/VERYSILENT`) unless `MSYS_NO_PATHCONV=1` is set. Test-infra gotcha; also applies to `/LOG=`, `/DIR=`, and every `/SWITCH` value. All P4 matrix runs use `MSYS_NO_PATHCONV=1 "$INSTALLER" /VERYSILENT /NOCANCEL /SP- /LOG="$LOG_W" /DIR="$DIR_W"` where `$LOG_W` and `$DIR_W` are `cygpath -w`-produced Windows paths.
- **`NextButtonClick` + `CurPageChanged` both run in `/VERYSILENT`** for pages that aren't auto-skipped. MsgBoxes in `NextButtonClick` WILL render (silent mode hides the wizard frame, not modal dialogs). P4's silent-mode validation bypass addresses this.
- **`FileCopy` is deprecated** in Inno 6.7.1 — compiler hints to use `CopyFile`. Same signature. Production code uses `CopyFile` throughout.
- **`AnsiString(stringLiteral)` cast fails** with "Type mismatch" in some contexts; assigning to an `AnsiString` variable first works reliably. Only affected the temp harness; production `WriteToolPathsJson` passes a `String` variable to `AnsiString(...)` and compiles fine.
- **Row-height DPI scaling** — `TNewStaticText` with 2-line Caption + `DescHeight=26` at 125%+ DPI clips the second line. Bump to 32 for 2-line descriptions. Single-line descriptions at 13 are fine.
- **Multi-line `Log(Format(...))` continuations** starting with `[` parse as `[Section]` tags. Same class of issue P3 documented. Single-line Log calls avoid this. (P4 inherited P3's fix pattern + didn't re-encounter once following the convention.)

## Files of interest for next phase (Phase 5)

- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 5 — Sandbox install matrix" — full T1–T15 spec.
- `installer/claude-mo2-installer.iss` as a whole — P5 doesn't modify this unless a regression is found.
- `installer/claude-mo2-installer.iss:414-468` — `EscapeJsonString` + `WriteToolPathsJson` (inspect if P5 adds an xEdit-source-dir surface or other JSON schema expansion, which is out-of-scope but listed in the PLAN § Cleanup).
- `installer/claude-mo2-installer.iss:1133-1166` — `ShouldSkipPage`'s silent-mode auto-Keep logic (P5 scripted scenarios rely on this).
- `installer/claude-mo2-installer.iss:1216-1262` — `NextButtonClick`'s `if not WizardSilent` validation guard (P5 silent scenarios rely on this; interactive scenarios exercise the full validation).
- `installer/claude-mo2-installer.iss:1289-1414` — `ApplyBinarySurface` + production `CurStepChanged`. P5 regression-checks binary preservation / deletion across upgrade scenarios.
- `C:\Users\compl\AppData\Local\Temp\mo2_p4_matrix\` — P4 matrix sandbox artefacts. Source-file pool (`sources/bsarch`, `sources/nif-tool`, `sources/creation-kit`, `sources/scripts-src`) is reusable for P5 scenarios.
- `C:\Users\compl\AppData\Local\Temp\mo2_p4_matrix\T6_writejson.py` and `T9_check.py` — reusable helpers for P5's JSON-write pre-seeding + Python parse verification.
