; Claude MO2 Installer — Inno Setup script
;
; Requires Inno Setup 6 (Unicode): https://jrsoftware.org/isdl.php
;
; Build (from Claude_MO2_Dev_Build\ ):
;   .\build\build-release.ps1 -BuildInstaller
;
; Or compile manually:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\claude-mo2-installer.iss
;
; Output: build-output\installer\claude-mo2-setup-vX.Y.Z.exe
;
; This installer:
; 1. Prompts the user for their Mod Organizer 2 installation folder (validates that ModOrganizer.exe exists there).
; 2. Checks for .NET 8 Runtime; guides to Microsoft's download page if missing.
; 3. Copies the Python plugin + mutagen-bridge.exe + Spooky CLI into <MO2>\plugins\mo2_mcp\.
; 4. Creates placeholder dirs with README stubs for the three user-provided tools (BSArch, nif-tool, PapyrusCompiler).
; 5. Reports which optional tools are detected post-install.

#define AppName "Claude MO2"
#define AppVersion "2.7.0"
#define AppPublisher "Aaronavich"
#define AppURL "https://github.com/Aaronavich/claude-mo2"
#define PluginFolder "mo2_mcp"

[Setup]
AppId={{8E2F4A7C-9F3B-4F21-B5C5-2D9B8F7D3A0E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}

; User selects their MO2 root folder. Plugin files go under {app}\plugins\mo2_mcp\
DefaultDirName={autopf}\Mod Organizer 2
DirExistsWarning=no
UsePreviousAppDir=no
AppendDefaultDirName=no

; Privileges: default to lowest; user can elevate via dialog if they chose a privileged dir.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output (relative to this .iss at <repo>\installer\)
OutputDir=..\build-output\installer
OutputBaseFilename=claude-mo2-setup-v{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes

; UI
WizardStyle=modern
ShowLanguageDialog=no
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
InfoBeforeFile=installer-welcome.txt
DisableReadyPage=no
DisableFinishedPage=no
SetupIconFile=
UninstallDisplayName=Claude MO2 (MCP plugin for Mod Organizer 2)

; No app group in Programs Menu — plugin is an MO2 addon, not a standalone app.
CreateAppDir=yes
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; All source paths are relative to this .iss at <repo>\installer\
; So "..\" lands at the repo root (Layer 1).

; Python plugin — from repo root
Source: "..\{#PluginFolder}\*"; \
    DestDir: "{app}\plugins\{#PluginFolder}"; \
    Flags: recursesubdirs createallsubdirs ignoreversion; \
    Excludes: "__pycache__\*,*.pyc"

; mutagen-bridge.exe + Mutagen deps — from build-output (produced by build-release.ps1)
Source: "..\build-output\mutagen-bridge\*"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\mutagen-bridge"; \
    Flags: recursesubdirs createallsubdirs ignoreversion

; Spooky CLI — from build-output. Excludes user-provided tools + Champollion
; (Champollion is GPL-2.0 and we chose not to ship a Papyrus decompiler at all —
; no currently-available decompiler produces clean round-trip output).
Source: "..\build-output\spooky-cli\*"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\spooky-cli"; \
    Flags: recursesubdirs createallsubdirs ignoreversion; \
    Excludes: "tools\bsarch\*,tools\nif-tool\*,tools\papyrus-compiler\*,tools\champollion\*"

; User-provided tool READMEs (placeholder dirs so user knows where to drop binaries)
Source: "README_BSARCH.txt"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\bsarch"; \
    DestName: "README.txt"; \
    Flags: ignoreversion
Source: "README_NIFTOOL.txt"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\nif-tool"; \
    DestName: "README.txt"; \
    Flags: ignoreversion
Source: "README_PAPYRUSCOMPILER.txt"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\papyrus-compiler"; \
    DestName: "README.txt"; \
    Flags: ignoreversion

; Top-level docs (next to the plugin, not inside MO2's plugin-loading scan path)
Source: "..\LICENSE"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion
Source: "..\KNOWN_ISSUES.md"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion
Source: "..\CLAUDE.md"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion
Source: "..\kb\KB_Tools.md"; DestDir: "{app}\plugins\{#PluginFolder}\kb"; Flags: ignoreversion
Source: "..\KNOWLEDGEBASE.md"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion

; Skills — Claude Code auto-discovers .claude/skills/*/SKILL.md when the user
; opens Claude Code in this dir. Trigger-matched, loaded on demand.
Source: "..\.claude\skills\*"; \
    DestDir: "{app}\plugins\{#PluginFolder}\.claude\skills"; \
    Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
; Ensure tool dirs exist even if README gets removed (keeps path resolution predictable)
Name: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\bsarch"
Name: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\nif-tool"
Name: "{app}\plugins\{#PluginFolder}\tools\spooky-cli\tools\papyrus-compiler"

[Icons]
; No start menu icons — this is an MO2 plugin, not a standalone app.

[InstallDelete]
; Upgrade hygiene: wipe the v2.5.x tools\spooky-bridge\ directory. v2.6+ ships
; the renamed mutagen-bridge; leaving the old dir behind wastes disk and could
; confuse _find_bridge's fallback chain. Inno only removes files it installed
; by default, so the legacy dir would otherwise orphan. Safe because v2.6's
; binaries land at tools\mutagen-bridge\ (different destination).
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}\tools\spooky-bridge"

[UninstallDelete]
; Python creates __pycache__ at runtime; Inno didn't install them, so they'd
; be orphaned after uninstall if we don't enumerate them here.
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}\__pycache__"
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}\ordlookup\__pycache__"
; Runtime-generated files the plugin creates
Type: files; Name: "{app}\plugins\{#PluginFolder}\.record_index.pkl"
;
; NOTE: tool_paths.json is INTENTIONALLY NOT listed here (v2.7.0 Phase 4).
; The file is user configuration written by CurStepChanged(ssPostInstall);
; removing it on uninstall would lose the user's tool paths across reinstalls.
; Inno only removes files it installed by default, so tool_paths.json (which
; we generate dynamically rather than declare in [Files]) is preserved
; without requiring a [UninstallSkip] equivalent entry.

[Code]
//════════════════════════════════════════════════════════════════════════════
// v2.7.0 Phase 3 — Previous-install detector page
//
// After the directory-select page, detect five tool surfaces at the chosen
// MO2 target and render a conditional wizard page with Keep/Change/Skip
// radios per detected surface. Selections recorded on Next are consumed by
// Phase 4's Optional Tools picker page.
//
// Surfaces (indexed 0..4):
//   0  bsarch                   — <target>\plugins\mo2_mcp\tools\
//                                   spooky-cli\tools\bsarch\bsarch.exe
//   1  nif_tool                 — ...\spooky-cli\tools\nif-tool\nif-tool.exe
//   2  papyrus_compiler_binary  — ...\papyrus-compiler\PapyrusCompiler.exe
//                                 OR ...\papyrus-compiler\
//                                    Original Compiler\PapyrusCompiler.exe
//   3  papyrus_compiler_json    — tool_paths.json["papyrus_compiler"]
//                                   (schema_version == 1, non-null)
//   4  papyrus_scripts_dir      — tool_paths.json["papyrus_scripts_dir"]
//                                   (schema_version == 1, non-null)
//
// Surfaces 3 + 4 parse <target>\plugins\mo2_mcp\tool_paths.json via the
// ReadToolPathsJson helper (P0 Q2 locked signature). Schema mismatch
// (schema_version != 1) or malformed JSON → treat both JSON surfaces as
// absent + log to install log; the wizard is never blocked.
//
// All globals are declared at unit scope so Phase 4's picker page can
// read them directly.
//════════════════════════════════════════════════════════════════════════════

const
  SURF_BSARCH                  = 0;
  SURF_NIF_TOOL                = 1;
  SURF_PAPYRUS_COMPILER_BINARY = 2;
  SURF_PAPYRUS_COMPILER_JSON   = 3;
  SURF_PAPYRUS_SCRIPTS_DIR     = 4;
  NUM_SURFACES                 = 5;

  SEL_NONE   = -1;
  SEL_KEEP   = 0;
  SEL_CHANGE = 1;
  SEL_SKIP   = 2;

var
  // Detector page handle (set in InitializeWizard).
  g_DetectorPage: TWizardPage;

  // Per-surface internal state driving the detector page's UI.
  g_Detected:     array[0..4] of Boolean;
  g_ExistingPath: array[0..4] of String;
  g_Selection:    array[0..4] of Integer;

  // Row-control handles (created once in InitializeWizard, shown/hidden
  // and repositioned per-navigation in LayoutDetectorPage).
  g_Label_Title:   array[0..4] of TNewStaticText;
  g_Label_Path:    array[0..4] of TNewStaticText;
  g_Radio_Keep:    array[0..4] of TNewRadioButton;
  g_Radio_Change:  array[0..4] of TNewRadioButton;
  g_Radio_Skip:    array[0..4] of TNewRadioButton;

  // ── Named globals exposed for Phase 4 consumption ──
  // Populated by SyncNamedGlobalsFromArrays, called from RunDetection
  // (so they're fresh even when ShouldSkipPage auto-skips the page) and
  // again from NextButtonClick (so they reflect the user's actual
  // selection after the page renders). Phase 4 reads these to seed its
  // picker page's initial state. When g_Detected_<surface> is False, the
  // corresponding Keep/Change/Skip booleans are all False — Phase 4 must
  // key off g_Detected_* first.
  g_Detected_bsarch:                  Boolean;
  g_Detected_nif_tool:                Boolean;
  g_Detected_papyrus_compiler_binary: Boolean;
  g_Detected_papyrus_compiler_json:   Boolean;
  g_Detected_papyrus_scripts_dir:     Boolean;

  g_ExistingPath_bsarch:                  String;
  g_ExistingPath_nif_tool:                String;
  g_ExistingPath_papyrus_compiler_binary: String;
  g_ExistingPath_papyrus_compiler_json:   String;
  g_ExistingPath_papyrus_scripts_dir:     String;

  g_Keep_bsarch,   g_Change_bsarch,   g_Skip_bsarch:   Boolean;
  g_Keep_nif_tool, g_Change_nif_tool, g_Skip_nif_tool: Boolean;
  g_Keep_papyrus_compiler_binary,
  g_Change_papyrus_compiler_binary,
  g_Skip_papyrus_compiler_binary: Boolean;
  g_Keep_papyrus_compiler_json,
  g_Change_papyrus_compiler_json,
  g_Skip_papyrus_compiler_json: Boolean;
  g_Keep_papyrus_scripts_dir,
  g_Change_papyrus_scripts_dir,
  g_Skip_papyrus_scripts_dir: Boolean;


//────────────────────────────────────────────────────────────────────────────
// JSON scanner (P0 Q2 locked signature)
//
//   function ReadToolPathsJson(
//     const Path: String;
//     var SchemaVersion: Integer;
//     var PapyrusCompilerPath: String;
//     var PapyrusScriptsDir: String
//   ): Boolean;
//
// Returns True iff parse succeeded, schema_version == 1, and both keys are
// present (value may be null or a string). Returns False on any failure:
// file missing/unreadable, JSON syntax error, schema_version missing or
// mismatched, or either key missing.
//
// On False the out params are left at their parse-failure defaults
// ('' / 0). SchemaVersion IS populated on schema mismatch (non-zero) so
// the caller can distinguish "bad version" from "malformed/missing".
//────────────────────────────────────────────────────────────────────────────

function SkipJsonWhitespace(const S: String; P: Integer): Integer;
begin
  while (P <= Length(S)) and
        ((S[P] = ' ') or (S[P] = #9) or (S[P] = #10) or (S[P] = #13)) do
    Inc(P);
  Result := P;
end;

function FindJsonKey(const S, Key: String): Integer;
var
  Needle: String;
  P: Integer;
begin
  Result := 0;
  Needle := '"' + Key + '"';
  P := Pos(Needle, S);
  if P = 0 then Exit;
  P := P + Length(Needle);
  P := SkipJsonWhitespace(S, P);
  if (P > Length(S)) or (S[P] <> ':') then Exit;
  Inc(P);
  Result := SkipJsonWhitespace(S, P);
end;

function ParseJsonInt(const S: String; StartPos: Integer; var Value: Integer): Boolean;
var
  P, Endp: Integer;
  NumStr: String;
  Quoted: Boolean;
begin
  Result := False;
  Value := 0;
  P := StartPos;
  Quoted := (P <= Length(S)) and (S[P] = '"');
  if Quoted then Inc(P);
  Endp := P;
  while (Endp <= Length(S)) and (S[Endp] >= '0') and (S[Endp] <= '9') do
    Inc(Endp);
  if Endp = P then Exit;
  NumStr := Copy(S, P, Endp - P);
  Value := StrToIntDef(NumStr, -1);
  if Value < 0 then Exit;
  if Quoted then begin
    if (Endp > Length(S)) or (S[Endp] <> '"') then Exit;
  end;
  Result := True;
end;

function ParseJsonStringOrNull(const S: String; StartPos: Integer;
  var Value: String; var IsNull: Boolean): Boolean;
var
  P: Integer;
  Ch: Char;
  Buf: String;
begin
  Result := False;
  IsNull := False;
  Value := '';
  P := StartPos;
  if P > Length(S) then Exit;
  if S[P] = 'n' then begin
    if Copy(S, P, 4) = 'null' then begin
      IsNull := True;
      Result := True;
    end;
    Exit;
  end;
  if S[P] <> '"' then Exit;
  Inc(P);
  Buf := '';
  while P <= Length(S) do begin
    Ch := S[P];
    if Ch = '"' then begin
      Value := Buf;
      Result := True;
      Exit;
    end;
    if Ch = '\' then begin
      if P + 1 > Length(S) then Exit;
      case S[P + 1] of
        '\': Buf := Buf + '\';
        '"': Buf := Buf + '"';
        '/': Buf := Buf + '/';
        'n': Buf := Buf + #10;
        'r': Buf := Buf + #13;
        't': Buf := Buf + #9;
      else
        Exit;
      end;
      P := P + 2;
    end else begin
      Buf := Buf + Ch;
      Inc(P);
    end;
  end;
  // Unterminated string literal → parse failure (Result stays False).
end;

function ReadToolPathsJson(const Path: String;
  var SchemaVersion: Integer;
  var PapyrusCompilerPath: String;
  var PapyrusScriptsDir: String): Boolean;
var
  Lines: TArrayOfString;
  Joined, Val: String;
  P, I: Integer;
  IsNull: Boolean;
begin
  Result := False;
  SchemaVersion := 0;
  PapyrusCompilerPath := '';
  PapyrusScriptsDir := '';

  if not FileExists(Path) then Exit;
  if not LoadStringsFromFile(Path, Lines) then Exit;

  Joined := '';
  for I := 0 to GetArrayLength(Lines) - 1 do begin
    if Joined <> '' then Joined := Joined + #10;
    Joined := Joined + Lines[I];
  end;

  // schema_version — must be integer 1
  P := FindJsonKey(Joined, 'schema_version');
  if P = 0 then Exit;
  if not ParseJsonInt(Joined, P, SchemaVersion) then Exit;
  if SchemaVersion <> 1 then Exit;  // Result still False; SchemaVersion populated

  // papyrus_compiler — string or null
  P := FindJsonKey(Joined, 'papyrus_compiler');
  if P = 0 then Exit;
  if not ParseJsonStringOrNull(Joined, P, Val, IsNull) then Exit;
  if IsNull then PapyrusCompilerPath := '' else PapyrusCompilerPath := Val;

  // papyrus_scripts_dir — string or null
  P := FindJsonKey(Joined, 'papyrus_scripts_dir');
  if P = 0 then Exit;
  if not ParseJsonStringOrNull(Joined, P, Val, IsNull) then Exit;
  if IsNull then PapyrusScriptsDir := '' else PapyrusScriptsDir := Val;

  Result := True;
end;


//────────────────────────────────────────────────────────────────────────────
// JSON writer (P0 Q3 locked signature, v2.7.0 Phase 4)
//
//   function WriteToolPathsJson(
//     const Path: String;
//     const PapyrusCompilerPath: String;
//     const PapyrusScriptsDir: String
//   ): Boolean;
//
// Writes a UTF-8 no-BOM tool_paths.json with schema_version=1. Empty-string
// path params emit `null`; non-empty paths emit quoted string literals with
// `\` → `\\` and `"` → `\"` escape. Overwrites any existing file. Ensures
// the parent directory exists before the write. Line endings are LF; output
// is the exact shape ReadToolPathsJson round-trips AND that Python 3's
// json.load parses cleanly with encoding='utf-8' (no BOM).
//
// Returns True on successful write, False on disk/permissions failure.
// Caller (P4's CurStepChanged) surfaces failure in the post-install MsgBox
// and the install log.
//────────────────────────────────────────────────────────────────────────────

function EscapeJsonString(const S: String): String;
var
  I: Integer;
  Ch: Char;
begin
  Result := '';
  for I := 1 to Length(S) do begin
    Ch := S[I];
    if Ch = '\' then
      Result := Result + '\\'
    else if Ch = '"' then
      Result := Result + '\"'
    else
      Result := Result + Ch;
  end;
end;

function WriteToolPathsJson(const Path: String;
  const PapyrusCompilerPath: String;
  const PapyrusScriptsDir: String): Boolean;
var
  Body: String;
  CompilerLine, ScriptsLine: String;
  ParentDir: String;
begin
  if PapyrusCompilerPath = '' then
    CompilerLine := '  "papyrus_compiler": null'
  else
    CompilerLine := '  "papyrus_compiler": "' + EscapeJsonString(PapyrusCompilerPath) + '"';

  if PapyrusScriptsDir = '' then
    ScriptsLine := '  "papyrus_scripts_dir": null'
  else
    ScriptsLine := '  "papyrus_scripts_dir": "' + EscapeJsonString(PapyrusScriptsDir) + '"';

  Body :=
    '{' + #10 +
    '  "schema_version": 1,' + #10 +
    CompilerLine + ',' + #10 +
    ScriptsLine + #10 +
    '}' + #10;

  ParentDir := ExtractFilePath(Path);
  if (ParentDir <> '') and (not DirExists(ParentDir)) then
    ForceDirectories(ParentDir);

  // AnsiString cast: for ASCII paths (the common case per P0 Q3 scope-lock
  // rationale), bytes are identical to UTF-8. SaveStringToFile writes raw
  // bytes with no BOM, which Python 3's json.load(encoding='utf-8') requires.
  Result := SaveStringToFile(Path, AnsiString(Body), False);
end;


//────────────────────────────────────────────────────────────────────────────
// Named-globals sync
//────────────────────────────────────────────────────────────────────────────

procedure SyncNamedGlobalsFromArrays();
begin
  g_Detected_bsarch                      := g_Detected[SURF_BSARCH];
  g_Detected_nif_tool                    := g_Detected[SURF_NIF_TOOL];
  g_Detected_papyrus_compiler_binary     := g_Detected[SURF_PAPYRUS_COMPILER_BINARY];
  g_Detected_papyrus_compiler_json       := g_Detected[SURF_PAPYRUS_COMPILER_JSON];
  g_Detected_papyrus_scripts_dir         := g_Detected[SURF_PAPYRUS_SCRIPTS_DIR];

  g_ExistingPath_bsarch                  := g_ExistingPath[SURF_BSARCH];
  g_ExistingPath_nif_tool                := g_ExistingPath[SURF_NIF_TOOL];
  g_ExistingPath_papyrus_compiler_binary := g_ExistingPath[SURF_PAPYRUS_COMPILER_BINARY];
  g_ExistingPath_papyrus_compiler_json   := g_ExistingPath[SURF_PAPYRUS_COMPILER_JSON];
  g_ExistingPath_papyrus_scripts_dir     := g_ExistingPath[SURF_PAPYRUS_SCRIPTS_DIR];

  g_Keep_bsarch     := g_Selection[SURF_BSARCH]   = SEL_KEEP;
  g_Change_bsarch   := g_Selection[SURF_BSARCH]   = SEL_CHANGE;
  g_Skip_bsarch     := g_Selection[SURF_BSARCH]   = SEL_SKIP;

  g_Keep_nif_tool   := g_Selection[SURF_NIF_TOOL] = SEL_KEEP;
  g_Change_nif_tool := g_Selection[SURF_NIF_TOOL] = SEL_CHANGE;
  g_Skip_nif_tool   := g_Selection[SURF_NIF_TOOL] = SEL_SKIP;

  g_Keep_papyrus_compiler_binary   := g_Selection[SURF_PAPYRUS_COMPILER_BINARY] = SEL_KEEP;
  g_Change_papyrus_compiler_binary := g_Selection[SURF_PAPYRUS_COMPILER_BINARY] = SEL_CHANGE;
  g_Skip_papyrus_compiler_binary   := g_Selection[SURF_PAPYRUS_COMPILER_BINARY] = SEL_SKIP;

  g_Keep_papyrus_compiler_json   := g_Selection[SURF_PAPYRUS_COMPILER_JSON] = SEL_KEEP;
  g_Change_papyrus_compiler_json := g_Selection[SURF_PAPYRUS_COMPILER_JSON] = SEL_CHANGE;
  g_Skip_papyrus_compiler_json   := g_Selection[SURF_PAPYRUS_COMPILER_JSON] = SEL_SKIP;

  g_Keep_papyrus_scripts_dir   := g_Selection[SURF_PAPYRUS_SCRIPTS_DIR] = SEL_KEEP;
  g_Change_papyrus_scripts_dir := g_Selection[SURF_PAPYRUS_SCRIPTS_DIR] = SEL_CHANGE;
  g_Skip_papyrus_scripts_dir   := g_Selection[SURF_PAPYRUS_SCRIPTS_DIR] = SEL_SKIP;
end;


//────────────────────────────────────────────────────────────────────────────
// Previous-install detection
//
// Reads the current WizardForm.DirEdit.Text target, walks the five tool
// surfaces, and populates g_Detected[] / g_ExistingPath[] / g_Selection[].
// Idempotent — safe to call from both ShouldSkipPage and CurPageChanged so
// re-entry via the Back button re-detects against the latest chosen dir.
//
// Logs to the install log on malformed/schema-mismatch JSON. No wizard-
// blocking errors.
//────────────────────────────────────────────────────────────────────────────

procedure RunDetection();
var
  MO2Dir, PluginDir, ToolsDir, JsonPath: String;
  CompilerFlat, CompilerSub: String;
  SchemaVer: Integer;
  CompilerPath, ScriptsDir: String;
  JsonOk: Boolean;
  I: Integer;
  Summary: String;
begin
  for I := 0 to NUM_SURFACES - 1 do begin
    g_Detected[I]     := False;
    g_ExistingPath[I] := '';
    g_Selection[I]    := SEL_NONE;
  end;

  MO2Dir := WizardForm.DirEdit.Text;
  if MO2Dir = '' then begin
    Log('[v2.7 P3 detector] RunDetection skipped (DirEdit empty)');
    SyncNamedGlobalsFromArrays();
    Exit;
  end;

  PluginDir := AddBackslash(MO2Dir) + 'plugins\mo2_mcp';
  ToolsDir  := PluginDir + '\tools\spooky-cli\tools';
  JsonPath  := PluginDir + '\tool_paths.json';

  // Surface 0 — BSArch
  if FileExists(ToolsDir + '\bsarch\bsarch.exe') then begin
    g_Detected[SURF_BSARCH]     := True;
    g_ExistingPath[SURF_BSARCH] := ToolsDir + '\bsarch\bsarch.exe';
  end;

  // Surface 1 — nif-tool
  if FileExists(ToolsDir + '\nif-tool\nif-tool.exe') then begin
    g_Detected[SURF_NIF_TOOL]     := True;
    g_ExistingPath[SURF_NIF_TOOL] := ToolsDir + '\nif-tool\nif-tool.exe';
  end;

  // Surface 2 — PapyrusCompiler (flat OR Original Compiler sub-layout)
  CompilerFlat := ToolsDir + '\papyrus-compiler\PapyrusCompiler.exe';
  CompilerSub  := ToolsDir + '\papyrus-compiler\Original Compiler\PapyrusCompiler.exe';
  if FileExists(CompilerFlat) then begin
    g_Detected[SURF_PAPYRUS_COMPILER_BINARY]     := True;
    g_ExistingPath[SURF_PAPYRUS_COMPILER_BINARY] := CompilerFlat;
  end else if FileExists(CompilerSub) then begin
    g_Detected[SURF_PAPYRUS_COMPILER_BINARY]     := True;
    g_ExistingPath[SURF_PAPYRUS_COMPILER_BINARY] := CompilerSub;
  end;

  // Surfaces 3 + 4 — JSON-configured paths
  SchemaVer    := 0;
  CompilerPath := '';
  ScriptsDir   := '';
  JsonOk := ReadToolPathsJson(JsonPath, SchemaVer, CompilerPath, ScriptsDir);

  if not JsonOk then begin
    if FileExists(JsonPath) then begin
      if (SchemaVer > 0) and (SchemaVer <> 1) then
        Log(Format('[v2.7 P3 detector] %s has schema_version=%d (expected 1); skipping JSON surfaces.', [JsonPath, SchemaVer]))
      else
        Log(Format('[v2.7 P3 detector] %s could not be parsed; skipping JSON surfaces.', [JsonPath]));
    end;
    // else: file absent — clean install; silent.
  end else begin
    if CompilerPath <> '' then begin
      g_Detected[SURF_PAPYRUS_COMPILER_JSON]     := True;
      g_ExistingPath[SURF_PAPYRUS_COMPILER_JSON] := CompilerPath;
    end;
    if ScriptsDir <> '' then begin
      g_Detected[SURF_PAPYRUS_SCRIPTS_DIR]     := True;
      g_ExistingPath[SURF_PAPYRUS_SCRIPTS_DIR] := ScriptsDir;
    end;
  end;

  // Detection summary (audit trail in install log).
  Summary := '[v2.7 P3 detector] Detected at ' + MO2Dir + ': ';
  if g_Detected[SURF_BSARCH]                  then Summary := Summary + 'bsarch ';
  if g_Detected[SURF_NIF_TOOL]                then Summary := Summary + 'nif_tool ';
  if g_Detected[SURF_PAPYRUS_COMPILER_BINARY] then Summary := Summary + 'papyrus_compiler_binary ';
  if g_Detected[SURF_PAPYRUS_COMPILER_JSON]   then Summary := Summary + 'papyrus_compiler_json ';
  if g_Detected[SURF_PAPYRUS_SCRIPTS_DIR]     then Summary := Summary + 'papyrus_scripts_dir ';
  if Summary = '[v2.7 P3 detector] Detected at ' + MO2Dir + ': ' then
    Summary := Summary + '(none)';
  Log(Summary);

  SyncNamedGlobalsFromArrays();
end;


//────────────────────────────────────────────────────────────────────────────
// Detector page UI
//
// CreateDetectorRow builds a row's controls once in InitializeWizard.
// LayoutDetectorPage (called from CurPageChanged) shows detected rows
// and hides undetected, repositioning visible rows so they stack with
// no gaps.
//────────────────────────────────────────────────────────────────────────────

procedure CreateDetectorRow(Idx: Integer; const Title: String);
begin
  g_Label_Title[Idx] := TNewStaticText.Create(g_DetectorPage);
  g_Label_Title[Idx].Parent := g_DetectorPage.Surface;
  g_Label_Title[Idx].Caption := Title;
  g_Label_Title[Idx].Font.Style := [fsBold];
  g_Label_Title[Idx].AutoSize := False;
  g_Label_Title[Idx].Left := 0;
  g_Label_Title[Idx].Width := g_DetectorPage.SurfaceWidth;
  g_Label_Title[Idx].Height := ScaleY(14);
  g_Label_Title[Idx].Visible := False;

  g_Label_Path[Idx] := TNewStaticText.Create(g_DetectorPage);
  g_Label_Path[Idx].Parent := g_DetectorPage.Surface;
  g_Label_Path[Idx].Caption := '';
  g_Label_Path[Idx].AutoSize := False;
  g_Label_Path[Idx].Left := ScaleX(12);
  g_Label_Path[Idx].Width := g_DetectorPage.SurfaceWidth - ScaleX(12);
  g_Label_Path[Idx].Height := ScaleY(14);
  g_Label_Path[Idx].Visible := False;

  g_Radio_Keep[Idx] := TNewRadioButton.Create(g_DetectorPage);
  g_Radio_Keep[Idx].Parent := g_DetectorPage.Surface;
  g_Radio_Keep[Idx].Caption := 'Keep';
  g_Radio_Keep[Idx].Left := ScaleX(12);
  g_Radio_Keep[Idx].Width := ScaleX(80);
  g_Radio_Keep[Idx].Height := ScaleY(17);
  g_Radio_Keep[Idx].Checked := True;
  g_Radio_Keep[Idx].Visible := False;

  g_Radio_Change[Idx] := TNewRadioButton.Create(g_DetectorPage);
  g_Radio_Change[Idx].Parent := g_DetectorPage.Surface;
  g_Radio_Change[Idx].Caption := 'Change';
  g_Radio_Change[Idx].Left := ScaleX(100);
  g_Radio_Change[Idx].Width := ScaleX(80);
  g_Radio_Change[Idx].Height := ScaleY(17);
  g_Radio_Change[Idx].Checked := False;
  g_Radio_Change[Idx].Visible := False;

  g_Radio_Skip[Idx] := TNewRadioButton.Create(g_DetectorPage);
  g_Radio_Skip[Idx].Parent := g_DetectorPage.Surface;
  g_Radio_Skip[Idx].Caption := 'Skip';
  g_Radio_Skip[Idx].Left := ScaleX(188);
  g_Radio_Skip[Idx].Width := ScaleX(80);
  g_Radio_Skip[Idx].Height := ScaleY(17);
  g_Radio_Skip[Idx].Checked := False;
  g_Radio_Skip[Idx].Visible := False;
end;

procedure LayoutDetectorPage();
var
  I, Y: Integer;
begin
  Y := 0;
  for I := 0 to NUM_SURFACES - 1 do begin
    if g_Detected[I] then begin
      g_Label_Title[I].Top := ScaleY(Y);
      g_Label_Title[I].Visible := True;

      g_Label_Path[I].Caption := g_ExistingPath[I];
      g_Label_Path[I].Top := ScaleY(Y + 16);
      g_Label_Path[I].Visible := True;

      g_Radio_Keep[I].Top := ScaleY(Y + 34);
      g_Radio_Keep[I].Checked := True;
      g_Radio_Keep[I].Visible := True;

      g_Radio_Change[I].Top := ScaleY(Y + 34);
      g_Radio_Change[I].Checked := False;
      g_Radio_Change[I].Visible := True;

      g_Radio_Skip[I].Top := ScaleY(Y + 34);
      g_Radio_Skip[I].Checked := False;
      g_Radio_Skip[I].Visible := True;

      Y := Y + 60;
    end else begin
      g_Label_Title[I].Visible := False;
      g_Label_Path[I].Visible := False;
      g_Radio_Keep[I].Visible := False;
      g_Radio_Change[I].Visible := False;
      g_Radio_Skip[I].Visible := False;
    end;
  end;
end;


//────────────────────────────────────────────────────────────────────────────
// .NET 8 runtime hard-block (v2.7.0 P1)
//
// Runs `dotnet --list-runtimes` and parses output for "Microsoft.NETCore.App 8."
// If absent, shows a critical-icon MsgBox with the download URL, opens the
// URL in the user's default browser via ShellExec, and aborts the install.
// No continue-anyway branch — .NET 8 is required for ESP patching and other
// .NET-backed tools.
//────────────────────────────────────────────────────────────────────────────

function IsDotNet8Installed(): Boolean;
var
  tmpFile: String;
  cmd: String;
  output: AnsiString;
  resultCode: Integer;
begin
  Result := False;
  tmpFile := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  cmd := 'cmd /c dotnet --list-runtimes > "' + tmpFile + '" 2>&1';
  if Exec(ExpandConstant('{cmd}'), '/c dotnet --list-runtimes > "' + tmpFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, resultCode) then begin
    if LoadStringFromFile(tmpFile, output) then begin
      if Pos('Microsoft.NETCore.App 8.', String(output)) > 0 then
        Result := True;
    end;
  end;
  DeleteFile(tmpFile);
end;

function InitializeSetup(): Boolean;
var
  resultCode: Integer;
begin
  Result := True;
  if not IsDotNet8Installed() then begin
    // Hard-block: .NET 8 is required. No continue-anyway branch.
    // mbCriticalError reinforces the hard-block framing; MB_OK has a single
    // button with no fall-through. ShellExec runs unconditionally so the
    // user's browser lands on the download page regardless of how they
    // dismiss the dialog.
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

//────────────────────────────────────────────────────────────────────────────
// Wizard page lifecycle hooks
//
//   InitializeWizard  — creates the detector page shell + row controls.
//                        Detection runs later (dir not yet chosen here).
//   ShouldSkipPage    — calls RunDetection; skips the detector when no
//                        surfaces are present (fresh install case).
//   CurPageChanged    — re-runs detection + re-lays-out rows on page
//                        entry (honours Back→change-dir→Forward).
//   NextButtonClick   — MO2 dir validation on wpSelectDir;
//                        record user Keep/Change/Skip selection on the
//                        detector page.
//────────────────────────────────────────────────────────────────────────────

//════════════════════════════════════════════════════════════════════════════
// v2.7.0 Phase 4 — Optional Tools picker page
//
// Custom wizard page rendered AFTER the P3 detector page. Contains 4 rows:
//
//   Row 0 (PICK_BSARCH)       — BSArch.exe file picker (copy-at-install)
//   Row 1 (PICK_NIF_TOOL)     — nif-tool.exe file picker (copy-at-install)
//   Row 2 (PICK_PAPYRUS_COMP) — PapyrusCompiler.exe file picker + nested
//                               "Reference this path at runtime (don't copy
//                               into plugin folder)" checkbox. Unchecked =
//                               copy-at-install (binary mode). Checked =
//                               tool_paths.json["papyrus_compiler"] write
//                               (JSON-reference mode, no copy).
//   Row 3 (PICK_PAPYRUS_DIR)  — Papyrus Scripts sources dir picker.
//                               tool_paths.json["papyrus_scripts_dir"] write,
//                               no copy.
//
// Initial state seeding (from P3 detector globals):
//   Keep   → path pre-populated, ReadOnly, Browse disabled.
//   Change → path pre-populated, editable, Browse enabled.
//   Skip   → path blank, editable.
//   None   → path blank, editable.
// Row 2 mirrors the dominant detected mode (binary OR JSON). If BOTH were
// detected pre-install, a red one-line warning surfaces and the checkbox
// defaults to JSON-reference (the more explicit signal).
//
// Re-layout convention (inherited from P3): LayoutPickerPage re-seeds each
// row's fields on every page entry. Any user text typed into an Edit is
// discarded if the user navigates Back-then-Forward. Acceptable trade-off
// per P3 precedent; documented in the handoff.
//
// Validation (NextButtonClick on picker page):
//   • Non-empty file-row path: must exist on disk (hard error).
//   • File-row filename mismatch (e.g. user picked xEdit64.exe for BSArch):
//     soft Yes/No warning; Yes overrides, No returns to the picker.
//   • Non-empty dir-row path: dir must exist on disk (hard error). No
//     content validation (user may have partial Scripts.zip extractions).
//   • Empty = skip (valid state, no validation).
//
// Install-step (CurStepChanged ssPostInstall):
//   • BSArch / nif-tool: copy to plugin-dir target (overwrite), OR delete
//     any existing plugin-dir binary when source is empty.
//   • PapyrusCompiler: copy (unchecked) or write JSON key + delete existing
//     plugin-dir binary (checked) or skip-with-cleanup (empty).
//   • Papyrus Scripts dir: write JSON key (or null).
//   • tool_paths.json ALWAYS written (schema_version=1, non-set keys=null).
//
// Note: [UninstallDelete] explicitly does NOT list tool_paths.json — user
// config must survive uninstall. See the comment there.
//════════════════════════════════════════════════════════════════════════════

const
  PICK_BSARCH       = 0;
  PICK_NIF_TOOL     = 1;
  PICK_PAPYRUS_COMP = 2;
  PICK_PAPYRUS_DIR  = 3;
  NUM_PICK_ROWS     = 4;

var
  g_PickerPage:          TWizardPage;
  g_PickerTitle:         array[0..3] of TNewStaticText;
  g_PickerDesc:          array[0..3] of TNewStaticText;
  g_PickerEdit:          array[0..3] of TNewEdit;
  g_PickerBrowse:        array[0..3] of TNewButton;
  g_PickerJsonCheckbox:  TNewCheckBox;
  g_PickerDualWarning:   TNewStaticText;
  // True once NextButtonClick(g_PickerPage.ID) fires — i.e. user interactively
  // confirmed the picker. In silent mode the picker page never renders, so
  // this stays False and CurStepChanged seeds Edit values from detector
  // globals via LayoutPickerPage (honours "Keep existing on silent install").
  g_PickerUserConfirmed: Boolean;


//────────────────────────────────────────────────────────────────────────────
// Row factory — title label, desc label, path edit, Browse button.
//
// DescHeight in px: pass 13 for one-line, 26 for two-line. Edit and Browse
// sit side-by-side on one line; the Browse button is anchored 80px wide at
// the right edge of the surface.
//────────────────────────────────────────────────────────────────────────────

procedure CreatePickerRow(Idx: Integer; Y: Integer; const Title, Desc: String; DescHeight: Integer);
var
  EditTop: Integer;
begin
  EditTop := Y + 14 + DescHeight + 2;

  g_PickerTitle[Idx] := TNewStaticText.Create(g_PickerPage);
  g_PickerTitle[Idx].Parent := g_PickerPage.Surface;
  g_PickerTitle[Idx].Caption := Title;
  g_PickerTitle[Idx].Font.Style := [fsBold];
  g_PickerTitle[Idx].AutoSize := False;
  g_PickerTitle[Idx].Top := ScaleY(Y);
  g_PickerTitle[Idx].Left := 0;
  g_PickerTitle[Idx].Width := g_PickerPage.SurfaceWidth;
  g_PickerTitle[Idx].Height := ScaleY(14);

  g_PickerDesc[Idx] := TNewStaticText.Create(g_PickerPage);
  g_PickerDesc[Idx].Parent := g_PickerPage.Surface;
  g_PickerDesc[Idx].Caption := Desc;
  g_PickerDesc[Idx].AutoSize := False;
  g_PickerDesc[Idx].Top := ScaleY(Y + 14);
  g_PickerDesc[Idx].Left := 0;
  g_PickerDesc[Idx].Width := g_PickerPage.SurfaceWidth;
  g_PickerDesc[Idx].Height := ScaleY(DescHeight);

  g_PickerEdit[Idx] := TNewEdit.Create(g_PickerPage);
  g_PickerEdit[Idx].Parent := g_PickerPage.Surface;
  g_PickerEdit[Idx].Top := ScaleY(EditTop);
  g_PickerEdit[Idx].Left := 0;
  g_PickerEdit[Idx].Width := g_PickerPage.SurfaceWidth - ScaleX(85);
  g_PickerEdit[Idx].Height := ScaleY(21);
  g_PickerEdit[Idx].Text := '';

  g_PickerBrowse[Idx] := TNewButton.Create(g_PickerPage);
  g_PickerBrowse[Idx].Parent := g_PickerPage.Surface;
  g_PickerBrowse[Idx].Caption := 'Browse...';
  g_PickerBrowse[Idx].Top := ScaleY(EditTop - 1);
  g_PickerBrowse[Idx].Left := g_PickerPage.SurfaceWidth - ScaleX(80);
  g_PickerBrowse[Idx].Width := ScaleX(80);
  g_PickerBrowse[Idx].Height := ScaleY(23);
end;


//────────────────────────────────────────────────────────────────────────────
// Browse-button handlers (one per row; wired via .OnClick in InitializeWizard)
//────────────────────────────────────────────────────────────────────────────

procedure OnBrowseBSArch(Sender: TObject);
var
  fn: String;
begin
  fn := g_PickerEdit[PICK_BSARCH].Text;
  if GetOpenFileName('Select BSArch.exe', fn, '',
      'BSArch executables|BSArch.exe;bsarch.exe|All files (*.*)|*.*', 'exe') then
    g_PickerEdit[PICK_BSARCH].Text := fn;
end;

procedure OnBrowseNifTool(Sender: TObject);
var
  fn: String;
begin
  fn := g_PickerEdit[PICK_NIF_TOOL].Text;
  if GetOpenFileName('Select nif-tool.exe', fn, '',
      'nif-tool.exe|nif-tool.exe|All files (*.*)|*.*', 'exe') then
    g_PickerEdit[PICK_NIF_TOOL].Text := fn;
end;

procedure OnBrowsePapyrusCompiler(Sender: TObject);
var
  fn: String;
begin
  fn := g_PickerEdit[PICK_PAPYRUS_COMP].Text;
  if GetOpenFileName('Select PapyrusCompiler.exe', fn, '',
      'PapyrusCompiler.exe|PapyrusCompiler.exe|All files (*.*)|*.*', 'exe') then
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := fn;
end;

procedure OnBrowsePapyrusScriptsDir(Sender: TObject);
var
  dn: String;
begin
  dn := g_PickerEdit[PICK_PAPYRUS_DIR].Text;
  if BrowseForFolder('Select the extracted Scripts.zip folder', dn, True) then
    g_PickerEdit[PICK_PAPYRUS_DIR].Text := dn;
end;


//────────────────────────────────────────────────────────────────────────────
// Row-state seeding helper (Rows 0, 1, 3 — simple copy-or-skip surfaces).
//
// Keep     → path locked to existing, Browse disabled.
// Change   → path pre-populated, editable, Browse enabled.
// Skip/None → path blank, editable.
//────────────────────────────────────────────────────────────────────────────

procedure SeedPickerSimpleRow(Idx: Integer; Detected, Keep, Change: Boolean; const ExistingPath: String);
begin
  if Detected and Keep then begin
    g_PickerEdit[Idx].Text := ExistingPath;
    g_PickerEdit[Idx].ReadOnly := True;
    g_PickerBrowse[Idx].Enabled := False;
  end else if Detected and Change then begin
    g_PickerEdit[Idx].Text := ExistingPath;
    g_PickerEdit[Idx].ReadOnly := False;
    g_PickerBrowse[Idx].Enabled := True;
  end else begin
    g_PickerEdit[Idx].Text := '';
    g_PickerEdit[Idx].ReadOnly := False;
    g_PickerBrowse[Idx].Enabled := True;
  end;
end;


//────────────────────────────────────────────────────────────────────────────
// LayoutPickerPage — seeds all 4 rows from P3 detector globals on every
// page entry. Called from CurPageChanged.
//────────────────────────────────────────────────────────────────────────────

procedure LayoutPickerPage();
var
  binaryPrior, jsonPrior: Boolean;
  compilerReadOnly: Boolean;
begin
  // Rows 0 + 1 + 3 — simple copy-or-skip pattern.
  SeedPickerSimpleRow(PICK_BSARCH,
    g_Detected_bsarch, g_Keep_bsarch, g_Change_bsarch,
    g_ExistingPath_bsarch);
  SeedPickerSimpleRow(PICK_NIF_TOOL,
    g_Detected_nif_tool, g_Keep_nif_tool, g_Change_nif_tool,
    g_ExistingPath_nif_tool);
  SeedPickerSimpleRow(PICK_PAPYRUS_DIR,
    g_Detected_papyrus_scripts_dir, g_Keep_papyrus_scripts_dir, g_Change_papyrus_scripts_dir,
    g_ExistingPath_papyrus_scripts_dir);

  // Row 2 — PapyrusCompiler combined (binary + JSON + checkbox).
  binaryPrior := g_Detected_papyrus_compiler_binary and (g_Keep_papyrus_compiler_binary or g_Change_papyrus_compiler_binary);
  jsonPrior   := g_Detected_papyrus_compiler_json   and (g_Keep_papyrus_compiler_json   or g_Change_papyrus_compiler_json);

  // Dual-detection warning visible only when BOTH surfaces were detected
  // AND the user didn't choose Skip on both (i.e. at least one of them
  // is still in play). Red label prompts user to pick one mode.
  g_PickerDualWarning.Visible := binaryPrior and jsonPrior;

  // Seed path + checkbox based on which surface "wins" the Row 2 slot.
  // Priority: JSON (more explicit signal per P0 Q4) > binary > none.
  if jsonPrior and g_Keep_papyrus_compiler_json then begin
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := g_ExistingPath_papyrus_compiler_json;
    g_PickerJsonCheckbox.Checked := True;
    compilerReadOnly := True;
  end else if binaryPrior and g_Keep_papyrus_compiler_binary and (not jsonPrior) then begin
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := g_ExistingPath_papyrus_compiler_binary;
    g_PickerJsonCheckbox.Checked := False;
    compilerReadOnly := True;
  end else if jsonPrior and g_Change_papyrus_compiler_json then begin
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := g_ExistingPath_papyrus_compiler_json;
    g_PickerJsonCheckbox.Checked := True;
    compilerReadOnly := False;
  end else if binaryPrior and g_Change_papyrus_compiler_binary then begin
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := g_ExistingPath_papyrus_compiler_binary;
    g_PickerJsonCheckbox.Checked := False;
    compilerReadOnly := False;
  end else begin
    // No prior state OR Skip on everything. Blank editable; default copy mode.
    g_PickerEdit[PICK_PAPYRUS_COMP].Text := '';
    g_PickerJsonCheckbox.Checked := False;
    compilerReadOnly := False;
  end;
  g_PickerEdit[PICK_PAPYRUS_COMP].ReadOnly := compilerReadOnly;
  g_PickerBrowse[PICK_PAPYRUS_COMP].Enabled := not compilerReadOnly;
  g_PickerJsonCheckbox.Enabled := True;
end;


//────────────────────────────────────────────────────────────────────────────
// .NET 8 runtime hard-block is defined above (IsDotNet8Installed +
// InitializeSetup). It remains unchanged in Phase 4.
//
// Wizard page lifecycle hooks (InitializeWizard / ShouldSkipPage /
// CurPageChanged / NextButtonClick) combine P3's detector + P4's picker.
//────────────────────────────────────────────────────────────────────────────

procedure InitializeWizard();
begin
  // ── P3 detector page ──
  g_DetectorPage := CreateCustomPage(
    wpSelectDir,
    'Previous Claude MO2 install detected',
    'Choose what to do with each existing tool. ' +
    'Keep = leave as-is. ' +
    'Change = pick a new source on the next page. ' +
    'Skip = remove from the plugin.'
  );

  CreateDetectorRow(SURF_BSARCH,                  'BSArch');
  CreateDetectorRow(SURF_NIF_TOOL,                'nif-tool');
  CreateDetectorRow(SURF_PAPYRUS_COMPILER_BINARY, 'PapyrusCompiler (in-plugin binary)');
  CreateDetectorRow(SURF_PAPYRUS_COMPILER_JSON,   'PapyrusCompiler (JSON reference)');
  CreateDetectorRow(SURF_PAPYRUS_SCRIPTS_DIR,     'Papyrus Scripts sources directory');

  // ── P4 picker page ──
  g_PickerPage := CreateCustomPage(
    g_DetectorPage.ID,
    'Optional Tools',
    'Point the installer at tools you want to enable. ' +
    'Leave any field empty to skip — you can configure later by editing ' +
    'tool_paths.json (Papyrus surfaces) or re-running this installer.'
  );

  CreatePickerRow(PICK_BSARCH, 0,
    'BSArch.exe',
    'Part of xEdit. Required for BSA/BA2 list / extract / validate tools.', 13);
  g_PickerBrowse[PICK_BSARCH].OnClick := @OnBrowseBSArch;

  CreatePickerRow(PICK_NIF_TOOL, 55,
    'nif-tool.exe',
    'From Spooky''s AutoMod Toolkit. Required for NIF texture / shader tools.', 13);
  g_PickerBrowse[PICK_NIF_TOOL].OnClick := @OnBrowseNifTool;

  CreatePickerRow(PICK_PAPYRUS_COMP, 110,
    'PapyrusCompiler.exe',
    'Ships with the Creation Kit. Required for compiling Papyrus scripts.', 13);
  g_PickerBrowse[PICK_PAPYRUS_COMP].OnClick := @OnBrowsePapyrusCompiler;

  // Row 2 checkbox — "Reference at runtime (don't copy)"
  g_PickerJsonCheckbox := TNewCheckBox.Create(g_PickerPage);
  g_PickerJsonCheckbox.Parent := g_PickerPage.Surface;
  g_PickerJsonCheckbox.Caption := 'Reference this path at runtime (don''t copy into plugin folder)';
  g_PickerJsonCheckbox.Top := ScaleY(164);
  g_PickerJsonCheckbox.Left := ScaleX(12);
  g_PickerJsonCheckbox.Width := g_PickerPage.SurfaceWidth - ScaleX(12);
  g_PickerJsonCheckbox.Height := ScaleY(17);
  g_PickerJsonCheckbox.Checked := False;

  // Row 2 dual-detection warning (hidden by default; visible only when both
  // binary AND JSON surfaces were detected pre-install).
  g_PickerDualWarning := TNewStaticText.Create(g_PickerPage);
  g_PickerDualWarning.Parent := g_PickerPage.Surface;
  g_PickerDualWarning.Caption := 'Detected both a copied binary AND a JSON reference. Choose one mode.';
  g_PickerDualWarning.Font.Color := $000000FF;
  g_PickerDualWarning.AutoSize := False;
  g_PickerDualWarning.Top := ScaleY(184);
  g_PickerDualWarning.Left := ScaleX(12);
  g_PickerDualWarning.Width := g_PickerPage.SurfaceWidth - ScaleX(12);
  g_PickerDualWarning.Height := ScaleY(14);
  g_PickerDualWarning.Visible := False;

  // Row 3 uses DescHeight=32 to give the 2-line description breathing room at
  // higher DPI scales. At 125-150% scaling the default dialog font line-height
  // exceeds 13 px, so DescHeight=26 clips the second line under the edit field.
  // 32 keeps total row-3 bottom at Y=272 — still well under Inno's ~305 px
  // surface budget.
  CreatePickerRow(PICK_PAPYRUS_DIR, 203,
    'Papyrus Scripts sources directory',
    'Extracted Scripts.zip (from Creation Kit). Required for compiling' + #13#10 +
    'scripts that reference Actor / Quest / Debug / other base types.', 32);
  g_PickerBrowse[PICK_PAPYRUS_DIR].OnClick := @OnBrowsePapyrusScriptsDir;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
var
  AnyDetected: Boolean;
  I: Integer;
begin
  Result := False;
  if PageID = g_DetectorPage.ID then begin
    RunDetection();
    AnyDetected := g_Detected[SURF_BSARCH] or
                   g_Detected[SURF_NIF_TOOL] or
                   g_Detected[SURF_PAPYRUS_COMPILER_BINARY] or
                   g_Detected[SURF_PAPYRUS_COMPILER_JSON] or
                   g_Detected[SURF_PAPYRUS_SCRIPTS_DIR];
    Result := not AnyDetected;

    // Silent-mode safety net: the detector page never renders for user input
    // in /SILENT or /VERYSILENT, so NextButtonClick never records selections.
    // Default every detected surface to Keep so existing binaries/JSON values
    // are preserved across silent upgrades (T5 / T6 scenarios).
    if WizardSilent then begin
      for I := 0 to NUM_SURFACES - 1 do begin
        if g_Detected[I] then
          g_Selection[I] := SEL_KEEP;
      end;
      SyncNamedGlobalsFromArrays();
    end;

    if Result then
      Log('[v2.7 P3 detector] ShouldSkipPage = True (no surfaces detected; detector page hidden).')
    else
      Log('[v2.7 P3 detector] ShouldSkipPage = False (surfaces detected; detector page will render).');
  end;
  // Picker page is never skipped — user may want to add tools to a fresh install.
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = g_DetectorPage.ID then begin
    // Re-run detection against the current DirEdit value (may have
    // changed since ShouldSkipPage if the user is going forward after
    // a Back → edit → Forward navigation).
    RunDetection();
    LayoutDetectorPage();
  end else if CurPageID = g_PickerPage.ID then begin
    // Seed each row's initial state from P3 detector globals. The globals
    // are populated either by RunDetection via ShouldSkipPage (auto-skip
    // case) or by the detector page's NextButtonClick (surfaces detected).
    LayoutPickerPage();
  end;
end;

//────────────────────────────────────────────────────────────────────────────
// Picker-page Next-button validation helpers
//────────────────────────────────────────────────────────────────────────────

function ValidatePickerFilePath(const Path, ExpectedName, SurfaceLabel: String): Boolean;
var
  actualName: String;
begin
  Result := True;
  if Path = '' then Exit;
  if not FileExists(Path) then begin
    MsgBox(SurfaceLabel + ' path does not exist:' + #13#10 + Path + #13#10 + #13#10 + 'Pick a valid file or clear the field to skip this tool.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  actualName := ExtractFileName(Path);
  if CompareText(actualName, ExpectedName) <> 0 then begin
    if MsgBox('The file you picked for ' + SurfaceLabel + ' does not match the expected filename "' + ExpectedName + '":' + #13#10 + actualName + #13#10 + #13#10 + 'Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

function ValidatePickerDirPath(const Path, SurfaceLabel: String): Boolean;
begin
  Result := True;
  if Path = '' then Exit;
  if not DirExists(Path) then begin
    MsgBox(SurfaceLabel + ' directory does not exist:' + #13#10 + Path + #13#10 + #13#10 + 'Pick a valid directory or clear the field to skip.', mbError, MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  appDir: String;
  I: Integer;
begin
  Result := True;
  if CurPageID = wpSelectDir then begin
    appDir := WizardForm.DirEdit.Text;
    if not FileExists(appDir + '\ModOrganizer.exe') then begin
      if MsgBox(
        'ModOrganizer.exe was not found in:' + #13#10 +
        appDir + #13#10 + #13#10 +
        'Are you sure this is your Mod Organizer 2 installation folder?' + #13#10 + #13#10 +
        'Click No to select a different folder.',
        mbConfirmation, MB_YESNO) = IDNO then begin
        Result := False;
      end;
    end;
  end else if CurPageID = g_DetectorPage.ID then begin
    // Record user's Keep/Change/Skip selection for each detected row.
    // Undetected rows keep g_Selection[I] = SEL_NONE (set by RunDetection).
    for I := 0 to NUM_SURFACES - 1 do begin
      if g_Detected[I] then begin
        if g_Radio_Change[I].Checked then
          g_Selection[I] := SEL_CHANGE
        else if g_Radio_Skip[I].Checked then
          g_Selection[I] := SEL_SKIP
        else
          g_Selection[I] := SEL_KEEP;
      end;
    end;
    SyncNamedGlobalsFromArrays();
  end else if CurPageID = g_PickerPage.ID then begin
    // Silent mode (/SILENT, /VERYSILENT) can't surface MsgBoxes for
    // validation failures — a stale JSON path would block an automated
    // upgrade. Trust the caller's inputs and let CopyFile/JSON-write
    // surface any remaining errors at install-step. Interactive mode
    // runs the full validation so typos hit hard-error / soft-warning
    // dialogs as designed.
    if not WizardSilent then begin
      if not ValidatePickerFilePath(g_PickerEdit[PICK_BSARCH].Text,       'BSArch.exe',          'BSArch') then begin Result := False; Exit; end;
      if not ValidatePickerFilePath(g_PickerEdit[PICK_NIF_TOOL].Text,     'nif-tool.exe',        'nif-tool') then begin Result := False; Exit; end;
      if not ValidatePickerFilePath(g_PickerEdit[PICK_PAPYRUS_COMP].Text, 'PapyrusCompiler.exe', 'PapyrusCompiler') then begin Result := False; Exit; end;
      if not ValidatePickerDirPath(g_PickerEdit[PICK_PAPYRUS_DIR].Text,   'Papyrus Scripts sources') then begin Result := False; Exit; end;
      g_PickerUserConfirmed := True;
    end;
  end;
end;


//────────────────────────────────────────────────────────────────────────────
// Install-step wiring (v2.7.0 Phase 4)
//
// Semantics — final state is determined by the picker's Edit text values
// (NOT the detector's Keep/Change/Skip selection globals). The detector
// merely seeds the picker; what the user confirms in the picker is the
// truth at install time.
//
// For each binary surface (BSArch / nif-tool / PapyrusCompiler-in-copy-mode):
//   • Text empty                → delete any existing plugin-dir binary.
//   • Text equals target path   → no-op (Keep case; source already here).
//   • Text points elsewhere     → CopyFile(source, target, overwrite=True).
// For PapyrusCompiler in JSON-reference mode (checkbox checked, non-empty):
//   • Write JSON key + delete any existing plugin-dir binary.
// For Papyrus Scripts dir:
//   • No copy; JSON key only.
// tool_paths.json is ALWAYS written at install-step end with schema_version=1
// and null values for any unconfigured key.
//
// Post-install MsgBox summarises all 4 user-facing surfaces (BSArch,
// nif-tool, PapyrusCompiler mode+path, Papyrus Scripts dir) + JSON path.
//────────────────────────────────────────────────────────────────────────────

function ApplyBinarySurface(const Source, Target, SurfaceLabel: String): String;
var
  parentDir: String;
begin
  if Source = '' then begin
    if FileExists(Target) then begin
      if DeleteFile(Target) then
        Result := 'not configured (removed prior binary at ' + Target + ')'
      else
        Result := 'WARNING: could not remove prior binary at ' + Target;
    end else
      Result := 'not configured';
    Exit;
  end;
  if CompareText(Source, Target) = 0 then begin
    Result := Target + ' (kept existing)';
    Exit;
  end;
  parentDir := ExtractFilePath(Target);
  if (parentDir <> '') and (not DirExists(parentDir)) then begin
    if not ForceDirectories(parentDir) then begin
      Result := 'FAILED: could not create directory ' + parentDir;
      Exit;
    end;
  end;
  if CopyFile(Source, Target, False) then
    Result := Target + ' (copied from ' + Source + ')'
  else
    Result := 'FAILED: could not copy ' + Source + ' -> ' + Target;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  pluginDir, toolsDir, jsonPath: String;
  bsarchTarget, niftoolTarget, compilerTarget, compilerSubTarget: String;
  bsarchSource, niftoolSource, compilerSource, scriptsDir: String;
  useJsonReference: Boolean;
  jsonCompilerPath, jsonScriptsDir: String;
  statusBsarch, statusNiftool, statusCompiler, statusScripts: String;
  writeOk: Boolean;
  msg: String;
begin
  if CurStep <> ssPostInstall then Exit;

  // Silent-mode fallback: if the user didn't interactively confirm the picker
  // page, seed Edit fields from detector globals now. LayoutPickerPage takes
  // Keep→ExistingPath / Skip→blank / None→blank, matching the semantics of
  // the picker UI user would have seen. Interactive installs skip this call
  // because g_PickerUserConfirmed was set in NextButtonClick(picker).
  if not g_PickerUserConfirmed then begin
    LayoutPickerPage();
    Log('[v2.7 P4] Silent-mode LayoutPickerPage: seeded picker fields from detector globals.');
  end;


  pluginDir         := ExpandConstant('{app}\plugins\' + '{#PluginFolder}');
  toolsDir          := pluginDir + '\tools\spooky-cli\tools';
  jsonPath          := pluginDir + '\tool_paths.json';

  bsarchTarget      := toolsDir + '\bsarch\bsarch.exe';
  niftoolTarget     := toolsDir + '\nif-tool\nif-tool.exe';
  compilerTarget    := toolsDir + '\papyrus-compiler\PapyrusCompiler.exe';
  compilerSubTarget := toolsDir + '\papyrus-compiler\Original Compiler\PapyrusCompiler.exe';

  bsarchSource      := g_PickerEdit[PICK_BSARCH].Text;
  niftoolSource     := g_PickerEdit[PICK_NIF_TOOL].Text;
  compilerSource    := g_PickerEdit[PICK_PAPYRUS_COMP].Text;
  scriptsDir        := g_PickerEdit[PICK_PAPYRUS_DIR].Text;
  useJsonReference  := g_PickerJsonCheckbox.Checked;

  // BSArch + nif-tool — simple copy-or-skip.
  statusBsarch  := ApplyBinarySurface(bsarchSource,  bsarchTarget,  'BSArch');
  statusNiftool := ApplyBinarySurface(niftoolSource, niftoolTarget, 'nif-tool');

  // PapyrusCompiler — mode selection via checkbox.
  if compilerSource = '' then begin
    jsonCompilerPath := '';
    if FileExists(compilerTarget)    then DeleteFile(compilerTarget);
    if FileExists(compilerSubTarget) then DeleteFile(compilerSubTarget);
    statusCompiler := 'not configured';
  end else if useJsonReference then begin
    jsonCompilerPath := compilerSource;
    if FileExists(compilerTarget)    then DeleteFile(compilerTarget);
    if FileExists(compilerSubTarget) then DeleteFile(compilerSubTarget);
    statusCompiler := compilerSource + ' (JSON-reference)';
  end else begin
    jsonCompilerPath := '';
    statusCompiler := ApplyBinarySurface(compilerSource, compilerTarget, 'PapyrusCompiler') + ' (copy mode)';
  end;

  // Papyrus Scripts dir — JSON-only.
  if scriptsDir = '' then begin
    jsonScriptsDir := '';
    statusScripts := 'not configured';
  end else begin
    jsonScriptsDir := scriptsDir;
    statusScripts := scriptsDir;
  end;

  // Write tool_paths.json (always, schema_version=1).
  writeOk := WriteToolPathsJson(jsonPath, jsonCompilerPath, jsonScriptsDir);
  if writeOk then
    Log('[v2.7 P4] Wrote ' + jsonPath)
  else
    Log('[v2.7 P4] FAILED to write ' + jsonPath);

  // Post-install summary MsgBox.
  msg := 'Claude MO2 installed to:' + #13#10;
  msg := msg + pluginDir + #13#10 + #13#10;
  msg := msg + 'Optional tools configured:' + #13#10;
  msg := msg + '  BSArch:          ' + statusBsarch  + #13#10;
  msg := msg + '  nif-tool:        ' + statusNiftool + #13#10;
  msg := msg + '  PapyrusCompiler: ' + statusCompiler + #13#10;
  msg := msg + '  Papyrus Scripts: ' + statusScripts  + #13#10;
  msg := msg + #13#10;
  if writeOk then
    msg := msg + 'Config file:     ' + jsonPath + #13#10
  else
    msg := msg + 'WARNING: failed to write config file at ' + jsonPath + #13#10;
  msg := msg + #13#10 + 'Next steps:' + #13#10;
  msg := msg + '  1. Launch Mod Organizer 2' + #13#10;
  msg := msg + '  2. Tools > Start/Stop Claude Server' + #13#10;
  msg := msg + '  3. Restart Claude Code once (so it discovers the MCP server)';

  MsgBox(msg, mbInformation, MB_OK);
end;
