# Phase 0 Handoff — Scope lock + audit (locked Inno API shapes, Python re-verified, 4 surfaces confirmed)

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~1h
**Commits made:** none (handoff-only; commit with phase-close)
**Live install synced:** No (Phase 0 is audit-only)

## What was done

Audit against `PLAN.md` § Phase 0. No source changes. Re-verified Python tool-lookup sites against current `main` (v2.6.1); researched Inno Setup 6 Pascal APIs for each of the four installer-side questions (TaskDialog, JSON read, JSON write, custom-page widgets); confirmed the remaining scope-lock questions (JSON schema wording, README-stub verdict).

- Re-read `mo2_mcp/tools_papyrus.py` — `_find_papyrus_compiler` candidate list and `_collect_header_dirs` aggregation confirmed as PLAN describes.
- Re-read `mo2_mcp/tools_archive.py` and `mo2_mcp/tools_nif.py` — Python never invokes BSArch or nif-tool directly; only Spooky CLI does (Option C prerequisite holds).
- Re-read `mo2_mcp/__init__.py` settings — `mutagen-bridge-path` and `spooky-cli-path` both registered; untouched by v2.7.0.
- Grepped `mo2_mcp/*.py` for `.exe` / tool-discovery sites — no 6th user-provided surface exists.
- Researched four Inno Setup 6 Pascal APIs via official jrsoftware.org docs and locked function signatures for every Phase 1/3/4 implementer touchpoint.

## Verification performed

- `mo2_mcp/tools_papyrus.py:67-77` — `_find_papyrus_compiler()` candidate list is exactly the 5 entries PLAN expects (2 in-plugin + 3 `%USERPROFILE%`). No change since v2.6.1.
- `mo2_mcp/tools_papyrus.py:267-288` — `_collect_header_dirs(vfs_dir)` uses `organizer.findFiles` + `resolvePath` fallback, collects unique parent dirs of `.psc` via the existing `seen` set. Matches PLAN's description of where Phase 2 will append.
- `mo2_mcp/tools_archive.py` — every Spooky CLI invocation goes through `_invoke_cli(cli, ["archive", ...])`. No direct `BSArch.exe` subprocess anywhere.
- `mo2_mcp/tools_nif.py` — every call routes through `_nif_cli_passthrough` to Spooky CLI. No direct `nif-tool.exe` subprocess anywhere.
- `mo2_mcp/__init__.py:106-133` — `settings()` registers `port`, `output-mod`, `auto-start`, `mutagen-bridge-path`, `spooky-cli-path`. No existing setting touches Papyrus surfaces; JSON config is purely additive.
- Grep across `mo2_mcp/*.py` for `PapyrusCompiler|papyrus-compiler|Scripts\.zip` confirms `tools_papyrus.py` is the sole Python-side invocation site.
- Grep across `mo2_mcp/*.py` for `\.exe` — only surfaces are bundled binaries (`mutagen-bridge.exe`, `spookys-automod.exe`) plus the already-catalogued user-provided trio. No 6th surface.

## Deviations from plan

None. Every question's answer matches the plan's expected shape; one required the escalation-triggering fallback per the user's mid-phase guidance (Q1 — see below).

## Decisions locked for implementers

### Q1 — Inno 6 TaskDialog: MsgBox+ShellExec fallback locked (TaskDialogMsgBox fallback)

**Locked: MsgBox+ShellExec fallback** (user's mid-phase guidance triggered after confirming Inno's built-in `TaskDialogMsgBox` has no hyperlink callback surface).

**What was ruled out:** Inno Setup 6's built-in `TaskDialogMsgBox` ([jrsoftware.org docs](https://jrsoftware.org/ishelp/topic_isxfunc_taskdialogmsgbox.htm)) has signature:

```pascal
function TaskDialogMsgBox(
  const Instruction, Text: String;
  const Typ: TMsgBoxType;
  const Buttons: Cardinal;
  const ButtonLabels: TArrayOfString;
  const ShieldButton: Integer
): Integer;
```

It supports custom button labels (good) but **does NOT expose the underlying Windows `TaskDialogIndirect` `TDF_ENABLE_HYPERLINKS` flag or callback mechanism**. The docs make no mention of HTML/hyperlink support. Implementing clickable-hyperlink TaskDialog would require bypassing Inno's wrapper and calling raw `TaskDialogIndirect` via `LoadLibrary` + `CreateCallback` + hand-rolled `TASKDIALOGCONFIG` Pascal struct — fragile, well past the "don't spend >30 min fighting the API" threshold per the user's mid-phase guidance.

**Locked Pascal shape for Phase 1:**

```pascal
function InitializeSetup(): Boolean;
var
  resultCode: Integer;
begin
  Result := True;
  if not IsDotNet8Installed() then begin
    MsgBox(
      '.NET 8 Runtime is required.' + #13#10 + #13#10 +
      'Claude MO2 needs the .NET 8 Runtime for ESP patching and other .NET-backed tools.' + #13#10 +
      'It was not detected on your system.' + #13#10 + #13#10 +
      'Click OK to open the Microsoft download page in your browser, then re-run this installer after installing .NET 8.' + #13#10 + #13#10 +
      'Download URL:' + #13#10 +
      'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
      mbCriticalError,
      MB_OK
    );
    ShellExec('open',
      'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
      '', '', SW_SHOW, ewNoWait, resultCode);
    Result := False;
  end;
end;
```

Key properties:
- `mbCriticalError` icon — reinforces hard-block framing.
- `MB_OK` — single button. No YES/NO/CANCEL branches → no fall-through to "install without .NET 8."
- Unconditional `ShellExec` after MsgBox returns — user's browser lands on the download page regardless of how they dismiss.
- `Result := False` unconditionally → install aborts.
- No per-version TaskDialog/MsgBox conditional; every supported Inno 6 version renders this identically.

**Aaron-adjudication note:** Per the user's mid-phase guidance, this is the locked fallback. If adjudication opts for TaskDialog anyway, the implementation cost is ~2-3h additional Phase 1 work to set up `TaskDialogIndirect` via DLL import — not recommended unless clickable-hyperlink UX is load-bearing.

### Q2 — JSON read in Inno Pascal: LoadStringsFromFile + manual scanner

**Locked: `LoadStringsFromFile` (UTF-8-aware) + manual flat-key scanner.** PowerShell shellout rejected as heavier-than-needed for a 3-key flat schema we control.

**Supporting facts:**
- `function LoadStringsFromFile(const FileName: String; var S: TArrayOfString): Boolean;` ([jrsoftware.org](https://jrsoftware.org/ishelp/topic_isxfunc_loadstringsfromfile.htm)) reads UTF-8 with or without BOM — the right tool for our write side.
- Our JSON schema is flat (3 keys: `schema_version`, `papyrus_compiler`, `papyrus_scripts_dir`), small (~200 bytes max), and we control the writer so formatting is predictable.
- A scanner using `Pos()`, `Copy()`, `Trim()` handles the shape robustly in ~50 lines.

**Locked function signature for Phase 3:**

```pascal
function ReadToolPathsJson(
  const Path: String;
  var SchemaVersion: Integer;
  var PapyrusCompilerPath: String;
  var PapyrusScriptsDir: String
): Boolean;
```

**Contract:**
- Returns `True` on successful parse of a `schema_version: 1` JSON with both string keys present (value may be `null` or a string).
- Returns `False` on any failure path: file missing, file unreadable, JSON syntax error, `schema_version` missing/mismatch, either key missing. Output var params are left empty-string / zero on False.
- `papyrus_compiler: null` and `papyrus_compiler: "<path>"` both return `True`; caller distinguishes by checking `PapyrusCompilerPath = ''`.
- Scanner must handle: double-backslash-escaped Windows paths (`C:\\Users\\...`), whitespace around colons, trailing commas, quoted vs unquoted integer for `schema_version`.
- Caller (Phase 3 detector) treats `False` as "JSON surfaces absent; only inspect binary surfaces."

Implementation note: After `LoadStringsFromFile` returns the lines array, join into a single `String` with `#13#10` separators, then single-pass scan for each key. For `papyrus_compiler` string value: find `"papyrus_compiler"`, skip whitespace + `:`, check for literal `null` or opening `"`, capture until matching unescaped `"`, unescape `\\` → `\` and `\"` → `"`.

### Q3 — JSON write in Inno Pascal: manual assembler + SaveStringToFile (UTF-8-safe)

**Locked: manual string assembler + `SaveStringToFile`** (not `SaveStringsToUTF8FileWithoutBOM` — see rationale below).

**Supporting facts:**
- `function SaveStringToFile(const FileName: String; const S: AnsiString; const Append: Boolean): Boolean;` ([jrsoftware.org](https://jrsoftware.org/ishelp/topic_isxfunc_savestringtofile.htm)).
- Rationale for AnsiString over UTF-8: JSON values contain only pre-escaped ASCII after `\` → `\\` normalization. Windows paths in our target use case are ASCII (plugin dir under `C:\Program Files\Mod Organizer 2\...`); in the edge case a user's Papyrus Scripts dir contains non-ASCII chars (Cyrillic username, Japanese install path), the installer Pascal side writes via AnsiString (active code page). Python reads via `json.load` with `encoding='utf-8'` which will raise on a non-UTF-8 byte. **This is acceptable for v2.7.0**: Python surfaces the "JSON unreadable" state to Claude as "papyrus_scripts_dir not configured," which is the same graceful degradation as no-JSON. A future-v2.8 hardening could switch to `SaveStringsToUTF8FileWithoutBOM` and tokenize Pascal-side writes — out of scope for v2.7.0.
- Why not UTF-8-with-BOM: Python's `json.load` with default `encoding='utf-8'` **fails on a UTF-8 BOM** (reads `\ufeff` as content). `SaveStringsToUTF8File` writes with BOM; `SaveStringToFile` writes raw bytes with no BOM. So AnsiString is simultaneously simpler AND more Python-compatible for the ASCII-path common case.

**Locked function signature for Phase 4:**

```pascal
function WriteToolPathsJson(
  const Path: String;
  const PapyrusCompilerPath: String;
  const PapyrusScriptsDir: String
): Boolean;
```

**Contract:**
- Always writes with `schema_version: 1`.
- Empty-string path value → JSON `null`; non-empty → JSON string literal with `\` → `\\` escape and `"` → `\"` escape (the latter is paranoia; Windows paths don't contain `"`).
- Overwrites existing file (Append=False).
- Returns `True` on success, `False` on disk/permissions failure.
- Caller (Phase 4 install-step wiring) fails the install with a clear error if the write fails — we can't recover from "can't write JSON" by silently falling back.

**Locked output format** (Phase 4 must emit exactly this shape so Phase 3 detector's scanner doesn't need to handle unexpected whitespace variants):

```
{
  "schema_version": 1,
  "papyrus_compiler": null,
  "papyrus_scripts_dir": "C:\\path\\to\\extracted-scripts"
}
```

- Two-space indent, LF line endings (Python's `json.load` handles either CRLF or LF), trailing newline after the closing brace.
- Keys in the order shown.

### Q4 — Inno custom wizard page with mixed widgets: combined row feasible

**Locked: combined PapyrusCompiler row (file picker + JSON-reference checkbox).** Fallback-split-row criterion defined but not expected to trigger.

**Supporting facts:**
- `function CreateCustomPage(AfterID: Integer; const ACaption, ADescription: String): TWizardPage;` ([jrsoftware.org](https://jrsoftware.org/ishelp/topic_isxfunc_createcustompage.htm)).
- `TNewEdit`, `TNewButton`, `TNewCheckBox`, `TNewStaticText` all create via `Create(Page)` + `.Parent := Page.Surface`. Positional properties (`.Top`, `.Left`, `.Width`, `.Height` via `ScaleX`/`ScaleY`), `.OnClick := @HandlerFn`, `.Text`/`.Caption`/`.Checked` accessors — identical pattern across all control types. Heliumproject's `Examples/CodeClasses.iss` confirms mixing TNewEdit + TNewButton + TNewCheckBox on one surface works.
- `function GetOpenFileName(const Prompt: String; var FileName: String; const InitialDirectory, Filter, DefaultExtension: String): Boolean;` ([jrsoftware.org](https://jrsoftware.org/ishelp/topic_isxfunc_getopenfilename.htm)) — filter syntax `'BSArch.exe|bsarch.exe|All files (*.*)|*.*'`.
- `function BrowseForFolder(const Prompt: String; var Directory: String; const NewFolderButton: Boolean): Boolean;` ([jrsoftware.org](https://jrsoftware.org/ishelp/topic_isxfunc_browseforfolder.htm)).
- `NextButtonClick(CurPageID: Integer): Boolean` and `ShouldSkipPage(PageID: Integer): Boolean` work identically for custom pages — compare `CurPageID` / `PageID` against the `TWizardPage.ID` returned by `CreateCustomPage`.

**Locked approach for Phases 3 + 4:**
- Phase 3 detector page: `CreateCustomPage(wpSelectDir, 'Previous install detected', '...')` → page body contains one row per detected surface (TNewStaticText label + TRadioButton group "Keep/Change/Skip").
- Phase 4 picker page: `CreateCustomPage(DetectorPage.ID, 'Optional Tools', '...')` → 4 rows (3 file pickers + 1 dir picker). Each file picker row = TNewStaticText label + TNewEdit path + TNewButton "Browse...". PapyrusCompiler row adds an additional indented TNewCheckBox row beneath it for "Reference this path at runtime (don't copy into plugin folder)".
- Row height: ~50 px per row for file/dir pickers, ~28 px for the PapyrusCompiler sub-checkbox. Total page height budget ~300 px — well under Inno's default surface.
- Validation in `NextButtonClick`: Phase 4's page validates each non-empty path exists on disk. File picker filename-mismatch (e.g. user picked `xEdit64.exe` as BSArch) → warning `MsgBox` with `MB_YESNO`, soft override allowed per PLAN.

**Fallback-split trigger criterion (carried over from PLAN):** If Phase 4's implementer sees any of the following after ~1h of UI iteration:
- The combined row's indented-checkbox visual hierarchy is not obvious to a user seeing the page for the first time, OR
- State transitions (Change-on-JSON-surface pre-checks the checkbox; Change-on-binary-surface leaves it unchecked) create surprising UX on re-open,

then split Row 3 into two separate rows: "Row 3a: PapyrusCompiler.exe (copy into plugin)" + "Row 3b: PapyrusCompiler.exe path (JSON reference)". Page becomes 5 rows. Phase 4 handoff records which path was taken. Cost of split-fallback: +1-2h. No Phase 0 reason to expect this trigger.

### Q5 — Python tool-lookup site re-verification: PLAN's description matches current main

Confirmed against `mo2_mcp/tools_papyrus.py:43-92` and `mo2_mcp/tools_papyrus.py:245-446` at v2.6.1:

- **`_find_papyrus_compiler()` candidate list (lines 67-77):** exactly 5 entries, in priority order:
  1. `<plugin>/tools/spooky-cli/tools/papyrus-compiler/PapyrusCompiler.exe` (flat CK-extraction layout)
  2. `<plugin>/tools/spooky-cli/tools/papyrus-compiler/Original Compiler/PapyrusCompiler.exe` (Spooky download layout)
  3. `%USERPROFILE%/Documents/tools/papyrus-compiler/papyrus-compiler/Original Compiler/PapyrusCompiler.exe`
  4. `%USERPROFILE%/Documents/tools/papyrus-compiler/Original Compiler/PapyrusCompiler.exe`
  5. `%USERPROFILE%/Documents/tools/papyrus-compiler/PapyrusCompiler.exe`

  Phase 2 prepends **a new priority 0 entry** that calls `tool_paths.get("papyrus_compiler")` and returns that path if present+valid. Entries 1-5 remain as the fallback chain in order.

- **`_collect_header_dirs(vfs_dir)` algorithm (lines 267-288):** normalizes the VFS path separator, calls `organizer.findFiles(vfs_norm, "*.psc")` to enumerate every .psc across the VFS merge, walks the result collecting `os.path.dirname` of each into a `list[str]` with a `seen` set (lowercased) to dedupe. Fallback: if `findFiles` returns empty, tries `organizer.resolvePath(vfs_norm)` and appends if that resolves to an existing dir. Returns the list (may be empty).

  Phase 2 extends this by checking `tool_paths.get("papyrus_scripts_dir")` at the end. If set AND not already in `seen`, appends to `dirs`. The existing `seen` set provides the dedupe guarantee. Phase 2 must normalize the configured path with `os.path.normpath` before the `seen.lower()` check.

- **No other tool-lookup sites reference PapyrusCompiler or Scripts sources.** Grep across `mo2_mcp/*.py` for `PapyrusCompiler|papyrus-compiler|Scripts\.zip` shows matches only in `tools_papyrus.py` (13 matches) and `__init__.py` (1 comment). `tools_records.py`, `tools_patching.py`, `tools_write.py`, `tools_audio.py`, `tools_archive.py`, `tools_nif.py`, `tools_filesystem.py`, `esp_index.py` — all clean.

- **BSArch and nif-tool invocation topology re-confirmed (Option C prerequisite):** `mo2_mcp/tools_archive.py` routes every archive operation through `_invoke_cli(cli, ["archive", ...])` where `cli` is `spookys-automod.exe`; never invokes `BSArch.exe` directly. `mo2_mcp/tools_nif.py` routes every NIF operation through `_nif_cli_passthrough` → `_invoke_cli(cli, ["nif", ...])`; never invokes `nif-tool.exe` directly. Option C architecture holds.

### Q6 — MO2 plugin settings coexistence confirmed

`mo2_mcp/__init__.py:106-133` registers five settings: `port`, `output-mod`, `auto-start`, `mutagen-bridge-path`, `spooky-cli-path`. Phase 2/3/4 do not touch `settings()`. `mutagen-bridge-path` continues to override `_find_bridge()` discovery in `tools_patching.py` and `tools_records.py`; `spooky-cli-path` continues to override `_find_spooky_cli()` in `tools_papyrus.py`. JSON config is purely additive for the four new Papyrus/BSA/NIF surfaces.

**Implicit lock:** The `_find_papyrus_compiler()` priority 0 JSON override does NOT compete with any MO2 plugin setting — there is no `papyrus-compiler-path` MO2 setting. Users who want to override PapyrusCompiler discovery have one surface: `tool_paths.json["papyrus_compiler"]`.

### Q7 — JSON schema final wording (2 keys locked, no expansion)

Final shape for Phase 1 onwards:

```json
{
  "schema_version": 1,
  "papyrus_compiler": null,
  "papyrus_scripts_dir": null
}
```

Locked:
- Field names: `papyrus_compiler`, `papyrus_scripts_dir` (snake_case, Python-idiomatic).
- Integer `schema_version: 1`. v2.7.0 Python will warn+ignore any JSON whose `schema_version != 1`. A future v2.8 schema change bumps to 2.
- `null` = not configured. Missing key = treat same as `null` (defensive read on Python side so an older JSON still works when a future version adds a key).
- v2.7.0 Python writes `null` for both keys if the user skipped those pickers; does not omit the keys. Consistency helps the Phase 3 detector in future versions.

**Python schema-mismatch semantics:** `tool_paths.get("papyrus_compiler")` returns `None` when `schema_version` mismatches, when the key is null/missing, or when the configured path does not exist on disk. A warning is logged but no exception propagates. Phase 2's module spec (see PLAN) already encodes this.

**No 6th key / no schema expansion.** Per user's mid-phase guidance: only `schema_version`, `papyrus_compiler`, `papyrus_scripts_dir`. No additions for future-proofing.

### Q8 — Legacy README stubs: keep all three (PLAN verdict confirmed)

Read `installer/README_PAPYRUSCOMPILER.txt`; content is still accurate for manual-install fallback guidance (flat-layout path matches `_find_papyrus_compiler()`'s priority 1 entry). Phase 4 leaves `installer/README_BSARCH.txt`, `installer/README_NIFTOOL.txt`, `installer/README_PAPYRUSCOMPILER.txt` and their installer-side destination entries in `[Files]` untouched. Users who skip the installer's picker page OR want to refresh a tool binary outside the installer workflow still have documented path guidance.

## Known issues / open questions

None. Every question produced a definitive lock per Phase 0's goal.

**Mild note for Phase 1:** The chosen MsgBox+ShellExec fallback for the .NET hard-block means users see a single-button Win32-style dialog (not the modern TaskDialog visual). Aaron may want to adjudicate if he prefers the modern look at the cost of ~2-3h of raw `TaskDialogIndirect` DLL plumbing in Phase 1. If Aaron does not speak up, Phase 1 ships the MsgBox pattern.

## Preconditions for Phase 1

- **Q1 TaskDialog decision locked as MsgBox+ShellExec fallback.** Phase 1's `InitializeSetup()` rewrite uses the exact shape in Q1 above.
- **`UsePreviousAppDir=yes` → `UsePreviousAppDir=no` at `installer/claude-mo2-installer.iss:39`.** Single-line change.
- **Version bump v2.6.1 → v2.7.0 at `mo2_mcp/config.py` (`PLUGIN_VERSION`) + `installer/claude-mo2-installer.iss:21` (`#define AppVersion`).** README installer URL + CHANGELOG entry deferred to Phase 6.
- **Sandbox Test A (path prompt reset on reinstall):** runnable on any machine — create throwaway `test-mo2-A` dir with stub `ModOrganizer.exe`, install v2.7.0, reinstall, confirm directory-select page does not pre-populate the prior path.
- **Sandbox Test B (.NET hard-block):** requires a .NET-8-absent environment. If Aaron's dev machine has .NET 8, option is to temporarily rename `%PROGRAMFILES%\dotnet\` (risky — other apps depend on it), use a VM, or skip Test B with handoff note and defer verification to Phase 5's sandbox matrix.
- **Phase 1 does NOT touch Python side.** `tools_papyrus.py`, `tool_paths.py` (doesn't exist yet) deferred to Phase 2.
- **Phase 1 does NOT touch README/CHANGELOG/KNOWN_ISSUES.** Deferred to Phase 6.

## Files of interest for next phase

- `installer/claude-mo2-installer.iss:21` — `#define AppVersion "2.6.1"`.
- `installer/claude-mo2-installer.iss:39` — `UsePreviousAppDir=yes` → `no`.
- `installer/claude-mo2-installer.iss:156-195` — `IsDotNet8Installed()` + `InitializeSetup()` current implementation; rewrite `InitializeSetup` per Q1 locked Pascal shape.
- `mo2_mcp/config.py` — `PLUGIN_VERSION = (2, 6, 1)` → `(2, 7, 0)`.
- `Claude_MO2/dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 1 — Installer policy fixes + version bump to v2.7.0" — step-by-step for Phase 1 implementer.
- This handoff's Q1 Pascal block — implement verbatim.
