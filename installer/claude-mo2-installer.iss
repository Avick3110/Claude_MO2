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

procedure InitializeWizard();
begin
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
end;

function ShouldSkipPage(PageID: Integer): Boolean;
var
  AnyDetected: Boolean;
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
    if Result then
      Log('[v2.7 P3 detector] ShouldSkipPage = True (no surfaces detected; detector page hidden).')
    else
      Log('[v2.7 P3 detector] ShouldSkipPage = False (surfaces detected; detector page will render).');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = g_DetectorPage.ID then begin
    // Re-run detection against the current DirEdit value (may have
    // changed since ShouldSkipPage if the user is going forward after
    // a Back → edit → Forward navigation).
    RunDetection();
    LayoutDetectorPage();
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
  end;
end;

//────────────────────────────────────────────────────────────────────────────
// Post-install: report which user-provided tools are detected
//────────────────────────────────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
var
  baseDir: String;
  bsarchOk, niftoolOk, pscOk: Boolean;
  msg: String;
begin
  if CurStep = ssPostInstall then begin
    baseDir := ExpandConstant('{app}\plugins\' + '{#PluginFolder}' + '\tools\spooky-cli\tools');
    bsarchOk := FileExists(baseDir + '\bsarch\bsarch.exe');
    niftoolOk := FileExists(baseDir + '\nif-tool\nif-tool.exe');
    pscOk := FileExists(baseDir + '\papyrus-compiler\PapyrusCompiler.exe');

    msg := 'Claude MO2 installed to:' + #13#10;
    msg := msg + ExpandConstant('{app}\plugins\' + '{#PluginFolder}') + #13#10 + #13#10;
    msg := msg + 'Optional tools status:' + #13#10;
    if bsarchOk then
      msg := msg + '  [OK]  BSArch (BSA/BA2 tools enabled)' + #13#10
    else begin
      msg := msg + '  [--]  BSArch NOT installed' + #13#10;
      msg := msg + '         Get it: https://github.com/TES5Edit/TES5Edit/releases' + #13#10;
      msg := msg + '         (see tools/spooky-cli/tools/bsarch/README.txt)' + #13#10;
    end;
    if niftoolOk then
      msg := msg + '  [OK]  nif-tool (NIF extras enabled)' + #13#10
    else begin
      msg := msg + '  [--]  nif-tool NOT installed' + #13#10;
      msg := msg + '         Get it: https://github.com/SpookyPirate/spookys-automod-toolkit/releases' + #13#10;
      msg := msg + '         (see tools/spooky-cli/tools/nif-tool/README.txt)' + #13#10;
    end;
    if pscOk then
      msg := msg + '  [OK]  PapyrusCompiler (script compile enabled)' + #13#10
    else begin
      msg := msg + '  [--]  PapyrusCompiler NOT installed' + #13#10;
      msg := msg + '         Requires the Creation Kit (free via Steam)' + #13#10;
      msg := msg + '         https://store.steampowered.com/app/1946180/' + #13#10;
    end;

    msg := msg + #13#10 + 'Next steps:' + #13#10;
    msg := msg + '  1. Launch Mod Organizer 2' + #13#10;
    msg := msg + '  2. Tools > Start/Stop Claude Server' + #13#10;
    msg := msg + '  3. Restart Claude Code once (so it discovers the MCP server)';

    MsgBox(msg, mbInformation, MB_OK);
  end;
end;
