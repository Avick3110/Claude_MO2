# build-release.ps1 - Build Spooky CLI + mutagen-bridge, optionally produce installer or sync to a live MO2 install.
#
# Usage:
#   .\build\build-release.ps1                                # Build only, outputs to build-output\
#   .\build\build-release.ps1 -BuildInstaller                # Build + compile the Inno Setup installer .exe
#   .\build\build-release.ps1 -SyncLive -MO2PluginDir PATH   # Build + copy binaries into an existing MO2 plugin dir
#   .\build\build-release.ps1 -Configuration Debug           # Debug build (default: Release)
#   .\build\build-release.ps1 -SkipCli                       # Skip Spooky CLI build (faster iteration on bridge)
#
# Outputs (relative to repo root):
#   build-output\spooky-cli\     - Spooky's CLI (Papyrus, BSA, NIF, Audio)
#   build-output\mutagen-bridge\ - Our thin bridge exe (ESP patching, record-reading)
#   build-output\installer\      - claude-mo2-setup-vX.Y.Z.exe (when -BuildInstaller)
#
# Prerequisites:
#   - spooky-toolkit\ submodule cloned (run: git submodule update --init --recursive)
#     (spooky-toolkit is still needed for the Spooky CLI build targeting
#     Papyrus / BSA / NIF / Audio non-FUZ workflows. The bridge itself
#     no longer references the toolkit -- it uses Mutagen direct via NuGet.)
#   - .NET 8+ SDK on PATH
#   - Inno Setup 6 (only for -BuildInstaller): https://jrsoftware.org/isdl.php

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SyncLive,
    [switch]$SyncPython,
    [string]$MO2PluginDir = "",
    [switch]$SkipCli,
    [switch]$BuildInstaller
)

# -SyncLive requires -MO2PluginDir.
if ($SyncLive -and [string]::IsNullOrWhiteSpace($MO2PluginDir)) {
    throw "-SyncLive requires -MO2PluginDir pointing at your MO2 plugin folder, e.g. -MO2PluginDir 'C:\ModOrganizer2\plugins\mo2_mcp'"
}

# -SyncLive implies -SyncPython (full deploy). Opt out by passing -SyncPython:$false.
if ($SyncLive -and -not $PSBoundParameters.ContainsKey('SyncPython')) {
    $SyncPython = $true
}

$ErrorActionPreference = "Stop"

# Paths

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SpookyToolkit = Join-Path $RepoRoot "spooky-toolkit"
$BridgeProj = Join-Path $RepoRoot "tools\mutagen-bridge\mutagen-bridge.csproj"
$BuildOutput = Join-Path $RepoRoot "build-output"
$CliOutDir = Join-Path $BuildOutput "spooky-cli"
$BridgeOutDir = Join-Path $BuildOutput "mutagen-bridge"

# Preflight

if (-not (Test-Path $SpookyToolkit)) {
    throw "spooky-toolkit\ not found. Run: git clone --branch v1.11.1 --depth 1 https://github.com/SpookyPirate/spookys-automod-toolkit.git $SpookyToolkit"
}

if (-not (Test-Path $BridgeProj)) {
    throw "mutagen-bridge csproj not found at $BridgeProj"
}

$dotnetVer = & dotnet --version
Write-Host "dotnet SDK: $dotnetVer" -ForegroundColor DarkGray

# Build Spooky CLI (for Phases B-D)

if (-not $SkipCli) {
    Write-Host "`n[1/2] Building Spooky CLI ($Configuration)..." -ForegroundColor Cyan
    $CliProj = Join-Path $SpookyToolkit "src\SpookysAutomod.Cli\SpookysAutomod.Cli.csproj"
    if (-not (Test-Path $CliProj)) {
        throw "Spooky CLI csproj not found at $CliProj (check clone integrity)"
    }
    dotnet publish $CliProj -c $Configuration -o $CliOutDir --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Spooky CLI build failed (exit $LASTEXITCODE)" }
    Write-Host "  -> $CliOutDir" -ForegroundColor DarkGray
} else {
    Write-Host "[1/2] Skipping Spooky CLI build (-SkipCli)" -ForegroundColor DarkYellow
}

# Build mutagen-bridge

Write-Host "`n[2/2] Building mutagen-bridge ($Configuration)..." -ForegroundColor Cyan
dotnet publish $BridgeProj -c $Configuration -o $BridgeOutDir --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "mutagen-bridge build failed (exit $LASTEXITCODE)" }
Write-Host "  -> $BridgeOutDir" -ForegroundColor DarkGray

$BridgeExe = Join-Path $BridgeOutDir "mutagen-bridge.exe"
if (-not (Test-Path $BridgeExe)) {
    throw "mutagen-bridge.exe not produced at $BridgeExe (check build output)"
}

# Bundle external tools into spooky-cli/tools/ so they ride the sync
# (Spooky's CLI resolves them 5 parents up from the exe, which doesn't land in the
# live install. Copying them adjacent to the CLI makes that resolution moot — the
# tools live right next to the exe.)

if (-not $SkipCli -and (Test-Path $CliOutDir)) {
    $CliToolsDir = Join-Path $CliOutDir "tools"
    New-Item -ItemType Directory -Path $CliToolsDir -Force | Out-Null

    # Where to look for external tools, in preference order:
    $ExternalToolSources = @(
        (Join-Path $env:USERPROFILE "Documents\tools"),  # Spooky's auto-download dir
        $CliToolsDir                                     # Already here from a prior run
    )

    # Note: Champollion deliberately omitted — Claude MO2 does not ship a
    # Papyrus decompiler (see installer README_PAPYRUSCOMPILER.txt). If
    # Spooky CLI's `papyrus decompile` is ever needed locally, it will
    # auto-download Champollion on first use.
    $ExpectedTools = @(
        @{ Name = "papyrus-compiler"; Exe = $null; Required = $false },  # contains subtree
        @{ Name = "bsarch"; Exe = "bsarch.exe"; Required = $false },
        @{ Name = "nif-tool"; Exe = "nif-tool.exe"; Required = $false }
    )

    foreach ($tool in $ExpectedTools) {
        $dest = Join-Path $CliToolsDir $tool.Name
        if (Test-Path $dest) { continue }  # already present

        foreach ($src in $ExternalToolSources) {
            $candidate = Join-Path $src $tool.Name
            if (Test-Path $candidate) {
                Write-Host "[tools] Bundling $($tool.Name) from $candidate" -ForegroundColor DarkCyan
                Copy-Item -Recurse -Force $candidate $dest
                break
            }
        }

        if (-not (Test-Path $dest)) {
            Write-Host "[tools] WARNING: $($tool.Name) not found in any known location. Phases B/C/D that need it will fail at runtime." -ForegroundColor Yellow
        }
    }
}

# Sync bridge to live MO2 install (opt-in)

if ($SyncLive) {
    if (-not (Test-Path $MO2PluginDir)) {
        throw "MO2 plugin dir not found: $MO2PluginDir"
    }

    # Bridge
    $LiveBridgeDir = Join-Path $MO2PluginDir "tools\mutagen-bridge"

    # One-release cleanup: wipe any legacy tools/spooky-bridge/ the installer
    # previously dropped. Keeps the live install from holding two bridge
    # binaries side by side after an in-place -SyncLive upgrade.
    $LegacyBridgeDir = Join-Path $MO2PluginDir "tools\spooky-bridge"
    if (Test-Path $LegacyBridgeDir) {
        Write-Host "[sync] Removing legacy $LegacyBridgeDir" -ForegroundColor DarkYellow
        Remove-Item -Recurse -Force $LegacyBridgeDir
    }
    Write-Host "`n[sync] Copying bridge to $LiveBridgeDir..." -ForegroundColor Cyan
    if (Test-Path $LiveBridgeDir) {
        Remove-Item -Recurse -Force $LiveBridgeDir
    }
    New-Item -ItemType Directory -Path $LiveBridgeDir -Force | Out-Null
    Copy-Item -Recurse -Force "$BridgeOutDir\*" $LiveBridgeDir
    $bridgeCount = (Get-ChildItem $LiveBridgeDir -Recurse | Measure-Object).Count
    Write-Host "  -> bridge: synced $bridgeCount files" -ForegroundColor DarkGray

    # CLI (only if it was built)
    if (-not $SkipCli -and (Test-Path $CliOutDir)) {
        $LiveCliDir = Join-Path $MO2PluginDir "tools\spooky-cli"
        Write-Host "[sync] Copying CLI to $LiveCliDir..." -ForegroundColor Cyan
        if (Test-Path $LiveCliDir) {
            Remove-Item -Recurse -Force $LiveCliDir
        }
        New-Item -ItemType Directory -Path $LiveCliDir -Force | Out-Null
        Copy-Item -Recurse -Force "$CliOutDir\*" $LiveCliDir
        $cliCount = (Get-ChildItem $LiveCliDir -Recurse | Measure-Object).Count
        Write-Host "  -> cli: synced $cliCount files" -ForegroundColor DarkGray
    }

    Write-Host "  NOTE: Python changes require MO2 restart. Exe-only changes do not." -ForegroundColor Yellow
} else {
    Write-Host "`n[sync] -SyncLive not set. Bridge built at $BridgeExe" -ForegroundColor DarkGray
}

# Python plugin sync (independent of -SyncLive when set explicitly)

if ($SyncPython) {
    if (-not (Test-Path $MO2PluginDir)) {
        throw "MO2 plugin dir not found: $MO2PluginDir"
    }

    $PySource = Join-Path $RepoRoot "mo2_mcp"
    $PySource = Resolve-Path $PySource
    Write-Host "`n[sync] Copying Python plugin from $PySource to $MO2PluginDir..." -ForegroundColor Cyan

    # Wipe stale .pyc so MO2 reloads modules cleanly on restart
    $LivePycache = Join-Path $MO2PluginDir "__pycache__"
    if (Test-Path $LivePycache) {
        Remove-Item -Recurse -Force $LivePycache
    }

    # Copy all .py files and the ordlookup/ subdir (if present),
    # but preserve tools/ which -SyncLive manages separately.
    $skipDirs = @("tools", "__pycache__")
    Get-ChildItem -Path $PySource -Force | ForEach-Object {
        if ($_.PSIsContainer -and $skipDirs -contains $_.Name) { return }
        $dest = Join-Path $MO2PluginDir $_.Name
        if ($_.PSIsContainer) {
            if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
            Copy-Item -Recurse -Force $_.FullName $dest
        } else {
            Copy-Item -Force $_.FullName $dest
        }
    }
    $pyCount = (Get-ChildItem -Path $MO2PluginDir -Filter *.py -Recurse | Measure-Object).Count
    Write-Host "  -> python: synced $pyCount .py files (plus any subdirs)" -ForegroundColor DarkGray
    Write-Host "  WARNING: MO2 must be fully restarted for Python module cache to reload." -ForegroundColor Yellow
} elseif ($SyncLive) {
    Write-Host "[sync] -SyncPython explicitly disabled. Live Python plugin is unchanged." -ForegroundColor DarkYellow
}

# Compile Inno Setup installer (opt-in)

if ($BuildInstaller) {
    # Find ISCC.exe: prefer PATH, fall back to common install locations.
    $InnoCompiler = $null
    $onPath = Get-Command -Name ISCC -ErrorAction SilentlyContinue
    if ($onPath) { $InnoCompiler = $onPath.Source }

    if (-not $InnoCompiler) {
        $InnoCandidates = @(
            "C:\Utilities\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
            "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
        )
        $InnoCompiler = $InnoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $InnoCompiler) {
        throw "Inno Setup 6 compiler (ISCC.exe) not found on PATH or common install paths. Install from https://jrsoftware.org/isdl.php and rerun. You can also set `$env:INNO_SETUP_DIR or add ISCC to PATH."
    }

    $IssFile = Join-Path $RepoRoot "installer\claude-mo2-installer.iss"
    if (-not (Test-Path $IssFile)) {
        throw "Installer script not found at $IssFile"
    }

    # Preflight: installer expects build-output/mutagen-bridge and build-output/spooky-cli to be populated.
    if (-not (Test-Path $BridgeOutDir)) {
        throw "Bridge output dir missing: $BridgeOutDir. Run a full build first."
    }
    if (-not $SkipCli -and -not (Test-Path $CliOutDir)) {
        throw "CLI output dir missing: $CliOutDir. Run a full build (without -SkipCli) first."
    }

    # Ensure installer output dir exists (ISCC creates the .exe but not the parent dir)
    $InstallerOutDir = Join-Path $BuildOutput "installer"
    New-Item -ItemType Directory -Path $InstallerOutDir -Force | Out-Null

    Write-Host "`n[installer] Compiling with $InnoCompiler..." -ForegroundColor Cyan
    & $InnoCompiler $IssFile
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed (exit $LASTEXITCODE)" }

    $SetupExe = Get-ChildItem -Path $InstallerOutDir -Filter "claude-mo2-setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($SetupExe) {
        Write-Host "  -> $($SetupExe.FullName)" -ForegroundColor DarkGray
        Write-Host "  size: $([math]::Round($SetupExe.Length / 1MB, 2)) MB" -ForegroundColor DarkGray
    } else {
        Write-Host "  WARNING: installer compile succeeded but no .exe found in $InstallerOutDir" -ForegroundColor Yellow
    }
}

Write-Host "`nBuild complete." -ForegroundColor Green
