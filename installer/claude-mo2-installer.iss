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
; 3. Copies the Python plugin + spooky-bridge.exe + Spooky CLI into <MO2>\plugins\mo2_mcp\.
; 4. Creates placeholder dirs with README stubs for the three user-provided tools (BSArch, nif-tool, PapyrusCompiler).
; 5. Reports which optional tools are detected post-install.

#define AppName "Claude MO2"
#define AppVersion "2.5.5"
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
UsePreviousAppDir=yes
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

; spooky-bridge.exe + Mutagen deps — from build-output (produced by build-release.ps1)
Source: "..\build-output\spooky-bridge\*"; \
    DestDir: "{app}\plugins\{#PluginFolder}\tools\spooky-bridge"; \
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

[UninstallDelete]
; Python creates __pycache__ at runtime; Inno didn't install them, so they'd
; be orphaned after uninstall if we don't enumerate them here.
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}\__pycache__"
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}\ordlookup\__pycache__"
; Runtime-generated files the plugin creates
Type: files; Name: "{app}\plugins\{#PluginFolder}\.record_index.pkl"

[Code]
//────────────────────────────────────────────────────────────────────────────
// .NET 8 runtime detection
//
// Runs `dotnet --list-runtimes` and parses output for "Microsoft.NETCore.App 8."
// If absent, prompts user to install. User can opt to continue (ESP patching
// and other .NET-backed tools will fail at runtime until they install .NET 8).
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
  response: Integer;
  resultCode: Integer;
begin
  Result := True;
  if not IsDotNet8Installed() then begin
    response := MsgBox(
      'The .NET 8 Runtime is required by Claude MO2''s ESP patching and other .NET-backed tools.' + #13#10 + #13#10 +
      'It was not detected on your system. Would you like to open the Microsoft download page now?' + #13#10 + #13#10 +
      '(You can continue without it, but those tools will not work until you install .NET 8.)',
      mbConfirmation, MB_YESNOCANCEL);
    if response = IDYES then begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, resultCode);
      Result := False;  // Abort install — user should restart installer after installing .NET 8
    end else if response = IDCANCEL then begin
      Result := False;
    end;
    // IDNO falls through — continue install with warning accepted
  end;
end;

//────────────────────────────────────────────────────────────────────────────
// MO2 path validation
//
// User selects their MO2 installation folder (the one containing ModOrganizer.exe).
// We validate on the directory-selection page, before copying anything.
//────────────────────────────────────────────────────────────────────────────

function NextButtonClick(CurPageID: Integer): Boolean;
var
  appDir: String;
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
