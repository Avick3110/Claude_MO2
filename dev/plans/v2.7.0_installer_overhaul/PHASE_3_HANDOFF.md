# Phase 3 Handoff — Previous-install detector wizard page (5 tool surfaces; Keep/Change/Skip radios)

**Phase:** 3
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~2h
**Commits made:** 1 — see `git log --oneline main | grep "\[v2\.7 P3\]"`
**Live install synced:** No (Phase 3 is installer-only; PLAN.md § Conventions defers live sync until Phase 6)

## What was done

One `.iss` file modified in one commit. All logic lives in the installer's `[Code]` section; no Python changes, no new asset files.

- **`installer/claude-mo2-installer.iss:147-832`** — new `[Code]` content inserted in four cohesive blocks (before the existing `.NET` check and again between `InitializeSetup` and the existing `NextButtonClick`):

  1. **Constants + unit-scope globals** (lines 177–237). Five surface indices (`SURF_BSARCH`=0 … `SURF_PAPYRUS_SCRIPTS_DIR`=4), three selection sentinels (`SEL_NONE=-1`, `SEL_KEEP=0`, `SEL_CHANGE=1`, `SEL_SKIP=2`), all arrays and named globals declared at unit scope so Phase 4 can read them. See "Globals exposed for Phase 4" below for the full list with types.

  2. **JSON scanner** (lines 240–401). Implements Phase 0 Q2's locked signature:
     ```pascal
     function ReadToolPathsJson(const Path: String;
       var SchemaVersion: Integer;
       var PapyrusCompilerPath: String;
       var PapyrusScriptsDir: String): Boolean;
     ```
     Uses `LoadStringsFromFile` + a hand-rolled flat-key scanner (`FindJsonKey` → `ParseJsonInt` + `ParseJsonStringOrNull` helpers). Handles: whitespace around colons, quoted or unquoted integer `schema_version`, escaped `\\` / `\"` / `\/` / `\n\r\t` in string values, trailing commas (implicitly — scanner only consumes the value it needs). Returns `True` iff file exists, JSON parses, `schema_version == 1`, AND both string-or-null keys are present. On False, `SchemaVersion` is still populated with the parsed value when available, so the caller can distinguish schema mismatch (`SchemaVer=99`) from malformed (`SchemaVer=0` or other parse failure).

  3. **`SyncNamedGlobalsFromArrays` + `RunDetection`** (lines 404–547). `RunDetection` reads `WizardForm.DirEdit.Text`, walks all 5 surfaces (3 `FileExists` checks for the binaries, `ReadToolPathsJson` for the JSON surfaces), writes internal arrays, and ends with a call to `SyncNamedGlobalsFromArrays` so Phase 4's picker page always sees fresh named globals regardless of whether the detector page renders. Logs a one-line audit summary `[v2.7 P3 detector] Detected at <path>: <space-separated surface names or "(none)">` every invocation + a separate line for malformed/schema-mismatch JSON.

  4. **`CreateDetectorRow`, `LayoutDetectorPage`, wizard hooks** (lines 550–782). Each row is `TNewStaticText` (bold title) + `TNewStaticText` (path display) + three `TNewRadioButton`s (Keep, Change, Skip, arranged horizontally at `Left = 12 / 100 / 188`, width 80 each). Controls are created once in `InitializeWizard`, hidden by default. `LayoutDetectorPage` (called from `CurPageChanged`) shows only detected rows, stacked with no gaps, 60px row height. The hooks below wire everything together:

     - **`InitializeWizard()`** (line 704) — creates the page via `CreateCustomPage(wpSelectDir, 'Previous Claude MO2 install detected', '…')` and calls `CreateDetectorRow` 5 times (one per surface, in stable order).
     - **`ShouldSkipPage(PageID)`** (line 722) — canonical Inno auto-skip pattern per architect mid-phase guidance. Calls `RunDetection` then returns `True` iff no surface is detected. Logs the decision.
     - **`CurPageChanged(CurPageID)`** (line 738) — re-runs detection (against the user's current `DirEdit.Text`, which may have changed via Back) + re-lays-out rows.
     - **`NextButtonClick(CurPageID)`** (line 749) — existing wpSelectDir validation preserved; added an `else if CurPageID = g_DetectorPage.ID` branch that reads each detected row's radio state, records `g_Selection[I]`, and calls `SyncNamedGlobalsFromArrays`.

- **`installer/claude-mo2-installer.iss:634-641`** — fixed stale banner comment above `IsDotNet8Installed`. The v2.6.1 banner said "User can opt to continue (ESP patching and other .NET-backed tools will fail at runtime until they install .NET 8)", which contradicted P1's hard-block rewrite. New banner accurately describes the hard-block behaviour. Conductor permission obtained mid-phase to fold this tidy into the P3 commit.

- **No other files touched.** Python side unchanged; README / CHANGELOG / KNOWN_ISSUES unchanged (deferred to Phase 6 per PLAN.md § Conventions).

## Verification performed

### ISCC clean compile

`"C:\Utilities\Inno Setup 6\ISCC.exe" installer/claude-mo2-installer.iss` → `Successful compile (9.734 sec)`. Output `build-output/installer/claude-mo2-setup-v2.7.0.exe` — 10.08 MB, overwriting the P1 artifact (version-lock OK: v2.7.0 is still unreleased, P1's local-only build is being iteratively updated within the phase sequence). Two compile errors surfaced and were fixed during dev:
1. A line starting with `[JsonPath, SchemaVer]` after a line continuation was parsed by Inno as an invalid `[Section]` tag. Collapsed the multi-line `Log(Format(...))` calls to single lines.
2. `Inc(P, 2)` — PascalScript does not support the two-argument form of `Inc`. Replaced with `P := P + 2`.

### Sandbox matrix — all five pass

All five sandbox scenarios executed via `powershell Start-Process ... /VERYSILENT /NOCANCEL /SP- /LOG=... /DIR=...` into user-writable `%LOCALAPPDATA%\Temp\mo2_p3_sandbox\<A–E>` targets (avoids UAC per the P1 handoff's advisory). Results extracted from each scenario's install log:

| # | Scenario | Expected | Observed | Pass? |
|---|---|---|---|---|
| A | Fresh install (only stub `ModOrganizer.exe`; no `plugins/mo2_mcp/`) | Detector auto-skips; wizard proceeds invisibly | `[v2.7 P3 detector] Detected at <A>: (none)` + `ShouldSkipPage = True (no surfaces detected; detector page hidden).` + installer reaches file-copy phase + exit 0 | ✅ |
| B | Upgrade-from-v2.6.1: 3 stub binaries present, no JSON | Detector shows 3 rows (bsarch, nif-tool, PapyrusCompiler binary); no JSON rows | `Detected at <B>: bsarch nif_tool papyrus_compiler_binary` + `ShouldSkipPage = False (surfaces detected; detector page will render).` | ✅ |
| C | Simulated v2.7+: all 3 binaries + valid `tool_paths.json` (schema_version=1, both keys populated with escaped Windows paths) | Detector shows all 5 rows | `Detected at <C>: bsarch nif_tool papyrus_compiler_binary papyrus_compiler_json papyrus_scripts_dir` | ✅ |
| D | Malformed JSON (truncated after `"oops`) | Detector shows 3 binary rows; install log warns "could not be parsed" | `<D>/plugins/mo2_mcp/tool_paths.json could not be parsed; skipping JSON surfaces.` + `Detected at <D>: bsarch nif_tool papyrus_compiler_binary` | ✅ |
| E | Schema mismatch (`schema_version: 99`) | Detector shows 3 binary rows; install log warns "schema_version=99" | `<E>/plugins/mo2_mcp/tool_paths.json has schema_version=99 (expected 1); skipping JSON surfaces.` + `Detected at <E>: bsarch nif_tool papyrus_compiler_binary` | ✅ |

Each scenario's detector summary log fires multiple times per invocation (ShouldSkipPage is called on every navigation + CurPageChanged calls RunDetection again to defend against dir-changed-via-Back). Detection is idempotent so repeated calls are benign; the repeated log lines are harmless and provide extra audit trail.

### Interactive verification for Scenario A — PASS

Aaron ran `build-output/installer/claude-mo2-setup-v2.7.0.exe` interactively against the Scenario A sandbox (`C:\Users\compl\AppData\Local\Temp\mo2_p3_sandbox\A` — stub `ModOrganizer.exe` only) and confirmed the wizard transitions from Dir-Select → Ready directly, with **no flicker, no empty page**. `ShouldSkipPage` returning True produces a truly invisible skip end-to-end; the canonical Inno pattern holds. Combined with the silent-mode install-log evidence for B–E, all five sandbox scenarios are green.

### Log-message correctness fix mid-phase

During the initial D scenario run, the log message incorrectly read `"has schema_version=1 (expected 1); skipping JSON surfaces."` because my `ReadToolPathsJson` populates `SchemaVersion` to 1 before subsequently failing on a later field. The log-branch condition `if SchemaVer > 0 then ... else ...` treated both "non-zero schema" and "schema mismatch" identically. Fixed to `if (SchemaVer > 0) and (SchemaVer <> 1) then ... else ...`: Scenario D's JSON now correctly logs `"could not be parsed"`, Scenario E still logs `"has schema_version=99"`. Rebuild and re-run of D confirmed the fix; A/B/C/E behaviour unchanged (the fix only touched the logging branch, never the detection logic).

## Deviations from plan

**None material.** Implementation follows PLAN.md § Phase 3 + PHASE_0_HANDOFF.md Q2/Q4 byte-for-byte. Two in-scope refinements worth calling out:

1. **Stale banner fix folded into P3 commit** (`IsDotNet8Installed`'s preamble comment, ~line 634) — the "User can opt to continue" text was obsolete after P1's hard-block. Conductor pre-approved this mid-phase; noted explicitly here per that approval.

2. **`Log()` summary at the end of every `RunDetection` invocation** (line 534) plus a one-line result in `ShouldSkipPage` (line 736). Not in PLAN's literal spec, but covered by the spirit of "Log to the install log so the outcome is auditable" from the architect's mid-phase guidance. Useful for Phase 5's sandbox matrix + post-ship upgrade-failure forensics. Intentional for v2.7.0 ship, not debug-only.

## Known issues / open questions

1. **Repeated detection logs per page-navigation pass (harmless).** In /VERYSILENT mode the install logs show the `Detected at …` summary two or three times per scenario. Inno's page-lifecycle calls ShouldSkipPage once on Back→Forward navigation toward the page + once on the forward edge, and CurPageChanged re-runs RunDetection defensively. Each call is <1ms (5 × `FileExists` + optional JSON read); no user-visible impact. Retained as-is — keeps the audit trail + defends against Back-edit-Forward dir changes cheaply.

2. **Post-install CurStepChanged MsgBox is still MB_OK-only.** Phase 3 did NOT touch `CurStepChanged`'s post-install summary MsgBox. Its three tool-status lines (BSArch / nif-tool / PapyrusCompiler) still key only off the binary-presence check at the fixed plugin-dir paths. Phase 4 will almost certainly rewrite this MsgBox to reflect the final post-install state including JSON-configured paths — out of scope for P3.

3. **P1 `NextButtonClick(wpSelectDir)` "ModOrganizer.exe not found" prompt surfaced during ad-hoc GUI installer testing.** Not a P3-introduced bug. Aaron observed during a GUI double-click of the installer: the default `{autopf}\Mod Organizer 2` path triggered my P1-inherited validation MsgBox (`"ModOrganizer.exe was not found in: …"`). Clicking No returned `Result := False` — per Inno docs this should stay on the dir-select page, but Aaron reports it closed the installer. This is either (a) an Inno quirk when the wizard is immediately-after-Welcome, (b) a UAC/elevation cross-effect, or (c) expected because the user pressed Cancel afterward. **Non-blocking for P3** — P3's detector runs AFTER wpSelectDir is passed, so the detector never saw a path this code-path produced. If behavioural investigation is desired, it's a P5 advisory note.

4. **Phase 4 must handle the "no prior selection" state correctly.** When the detector page auto-skips (Scenario A), `g_Selection[*] = SEL_NONE` (-1) and all `g_Keep_<surface>` / `g_Change_<surface>` / `g_Skip_<surface>` named globals are `False`. Phase 4 MUST key off `g_Detected_<surface>` before interpreting the Keep/Change/Skip booleans — when Detected is False, no selection is meaningful and the picker page should render a blank row. Documented in the comment block in `.iss` at line 207-214.

## Globals exposed for Phase 4 — complete list

All at unit scope in the `[Code]` section's `var` block starting at `installer/claude-mo2-installer.iss:190`. Phase 4 consumers read these to seed picker-page initial state.

### Internal arrays (Phase 4 may read directly using the named `SURF_*` constants)

```pascal
const
  SURF_BSARCH                  = 0;
  SURF_NIF_TOOL                = 1;
  SURF_PAPYRUS_COMPILER_BINARY = 2;
  SURF_PAPYRUS_COMPILER_JSON   = 3;
  SURF_PAPYRUS_SCRIPTS_DIR     = 4;
  NUM_SURFACES                 = 5;

  SEL_NONE   = -1;  // surface was undetected; no meaningful selection
  SEL_KEEP   = 0;
  SEL_CHANGE = 1;
  SEL_SKIP   = 2;

var
  g_DetectorPage: TWizardPage;
  g_Detected:     array[0..4] of Boolean;
  g_ExistingPath: array[0..4] of String;
  g_Selection:    array[0..4] of Integer;
  // Control handles (not usually needed by Phase 4 but exposed for completeness):
  g_Label_Title:   array[0..4] of TNewStaticText;
  g_Label_Path:    array[0..4] of TNewStaticText;
  g_Radio_Keep:    array[0..4] of TNewRadioButton;
  g_Radio_Change:  array[0..4] of TNewRadioButton;
  g_Radio_Skip:    array[0..4] of TNewRadioButton;
```

### Named globals (preferred API for Phase 4 — surface-specific, unambiguous)

```pascal
var
  // Detection flags — check these FIRST. When False, the other fields have no meaning.
  g_Detected_bsarch:                      Boolean;
  g_Detected_nif_tool:                    Boolean;
  g_Detected_papyrus_compiler_binary:     Boolean;
  g_Detected_papyrus_compiler_json:       Boolean;
  g_Detected_papyrus_scripts_dir:         Boolean;

  // Existing paths — the detected surface path. Empty string when Detected is False.
  g_ExistingPath_bsarch:                  String;
  g_ExistingPath_nif_tool:                String;
  g_ExistingPath_papyrus_compiler_binary: String;
  g_ExistingPath_papyrus_compiler_json:   String;
  g_ExistingPath_papyrus_scripts_dir:     String;

  // User selections (mutually exclusive, all False when Detected is False or
  // the detector page was auto-skipped).
  g_Keep_bsarch, g_Change_bsarch, g_Skip_bsarch: Boolean;
  g_Keep_nif_tool, g_Change_nif_tool, g_Skip_nif_tool: Boolean;
  g_Keep_papyrus_compiler_binary, g_Change_papyrus_compiler_binary,
    g_Skip_papyrus_compiler_binary: Boolean;
  g_Keep_papyrus_compiler_json, g_Change_papyrus_compiler_json,
    g_Skip_papyrus_compiler_json: Boolean;
  g_Keep_papyrus_scripts_dir, g_Change_papyrus_scripts_dir,
    g_Skip_papyrus_scripts_dir: Boolean;
```

Named globals are synced from the internal arrays by `SyncNamedGlobalsFromArrays` (line 408), called from both `RunDetection` (so the globals are always fresh even when the page auto-skips) and `NextButtonClick` (so the globals reflect the user's final selection after the page renders).

## JSON reader — exact implementation

`ReadToolPathsJson` and its three helpers live at `installer/claude-mo2-installer.iss:260-401`. Summary for Phase 4 (which will implement the matching `WriteToolPathsJson` per P0 Q3):

- **`SkipJsonWhitespace(S, P)` → Integer.** Advances P past space / tab / CR / LF characters.
- **`FindJsonKey(S, Key)` → Integer.** Finds the position immediately after the `:` that follows `"<Key>"`, with whitespace skipping. Returns 0 if key not found or malformed.
- **`ParseJsonInt(S, StartPos, var Value)` → Boolean.** Parses a digit sequence (optionally quoted, for tolerance). Returns False on non-numeric or negative.
- **`ParseJsonStringOrNull(S, StartPos, var Value, var IsNull)` → Boolean.** Returns True for literal `null` (sets IsNull=True, Value='') or a string literal (sets IsNull=False, Value unescaped). Handles `\\` / `\"` / `\/` / `\n\r\t` escapes. Returns False on any other form (unterminated, invalid escape, not string/not null).
- **`ReadToolPathsJson(const Path, var SchemaVersion, var PapyrusCompilerPath, var PapyrusScriptsDir)` → Boolean.** Load file via `LoadStringsFromFile`, join lines with `#10`, then call `FindJsonKey` + one of the parsers for each of the three keys in order. Returns True iff all three parsed AND SchemaVersion == 1. Null string values → empty-string out param; non-null strings → unescaped out param.

## UI compromises / conventions Phase 4 inherits

- **Row layout: title label (bold, full width) + path label (12px left indent, rest of width, 14px high) + three radios in a horizontal strip at `Top = title.Top + 34`, widths 80 each, `Left = 12 / 100 / 188`.** 60px per row; with 5 rows visible that's 300px — comfortably inside Inno's ~310px surface budget. If Phase 4's picker page uses a similar row pattern, these paddings work fine.
- **Row controls are created once in `InitializeWizard` and hidden by default.** `LayoutDetectorPage` shows only detected rows, repositioning them to stack with no gaps. Any phase-4 code that pulls the Radio handles out of the arrays should note that handles exist for ALL 5 surfaces regardless of whether the corresponding row was rendered.
- **Default selection on re-layout is always `Keep`.** `LayoutDetectorPage` explicitly sets `g_Radio_Keep[I].Checked := True` + `g_Radio_Change[I].Checked := False` + `g_Radio_Skip[I].Checked := False` every time it shows a row. This means Back-edit-Forward discards a previous Change/Skip pick — acceptable trade-off because it's simpler than preserving state across re-detection and the detector page is expected to be a one-shot decision point.
- **Page title: "Previous Claude MO2 install detected". Page description: "Choose what to do with each existing tool. Keep = leave as-is. Change = pick a new source on the next page. Skip = remove from the plugin."** Phase 4's picker page should follow a similar phrasing pattern.

## Preconditions for Phase 4

Phase 4 implements the Optional Tools picker page + install-step wiring (`tool_paths.json` write + binary copy/delete). It reads P3's globals to seed its 4-row picker (BSArch, nif-tool, PapyrusCompiler combined, Papyrus Scripts dir).

- ✅ **Phase 0 Q3 JSON-write signature still pending Phase 4 implementation.** No change from P0 handoff.
- ✅ **Phase 3 globals populated and named-global API documented above.**
- ✅ **Detector page + hooks working across all 5 matrix scenarios (A–E).**
- ✅ **Stale banner at `IsDotNet8Installed` fixed in this commit.** P4 inherits a clean baseline.
- ✅ **`[Files]` section unchanged.** P4 owns any additions here (e.g. dynamically copied binaries are handled in `CurStepChanged(ssPostInstall)` rather than `[Files]`, per PLAN.md § Phase 4).
- ✅ **Post-install MsgBox in `CurStepChanged` untouched.** P4 will rewrite it to reflect JSON-configured paths.
- ✅ **No Live Reported Bugs.** `<workspace>/Live Reported Bugs/` contains only the archive/ folder.

All Phase 4 preconditions met.

## Files of interest for next phase

- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 4 — Optional Tools picker page + install-step wiring" — full step-by-step.
- `dev/plans/v2.7.0_installer_overhaul/PHASE_0_HANDOFF.md` § Q3 (JSON-write signature) + § Q4 (mixed-widget custom page; combined-vs-split PapyrusCompiler row criterion).
- `installer/claude-mo2-installer.iss:177-237` — all constants + globals P4 reads.
- `installer/claude-mo2-installer.iss:408-441` — `SyncNamedGlobalsFromArrays` (do NOT modify — its contract is load-bearing for P4's consumers).
- `installer/claude-mo2-installer.iss:456-547` — `RunDetection`. P4 does not re-detect; it inherits P3's globals verbatim. If P4 needs to re-detect (e.g. after deleting a binary at install step), prefer a P4-specific helper to avoid entangling with P3.
- `installer/claude-mo2-installer.iss:704-720` — `InitializeWizard`. P4 adds its picker page via `CreateCustomPage(g_DetectorPage.ID, …)` immediately after P3's detector page.
- `installer/claude-mo2-installer.iss:788-832` — `CurStepChanged`. P4 rewrites this for post-install copy/delete/JSON-write + updated status MsgBox.
- `mo2_mcp/tool_paths.py` — Python-side consumer contract. P4's JSON writer must emit exactly the shape `tool_paths.py` accepts (schema_version: 1 integer, `papyrus_compiler` and `papyrus_scripts_dir` string-or-null keys, UTF-8 no-BOM).
