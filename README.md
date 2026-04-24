# Claude MO2

An MCP server plugin that connects AI assistants to [Mod Organizer 2](https://www.modorganizer.org/), giving them live read access to your Skyrim SE modlist. Claude (or any MCP-compatible client) can query your load order, read records inside any ESP/ESM/ESL, detect conflicts across your entire plugin list, analyze SKSE DLLs, and help you understand and develop your modlist.

## Quick Install (Recommended)

**Download [claude-mo2-setup-v2.6.1.exe](https://github.com/Avick3110/Claude_MO2/releases/latest/download/claude-mo2-setup-v2.6.1.exe) and run it.**

The installer:
- Detects whether .NET 8 Runtime is installed; guides you to Microsoft's download page if missing
- Prompts for your Mod Organizer 2 folder (the one containing `ModOrganizer.exe`)
- Copies the plugin + `mutagen-bridge.exe` + Spooky CLI into `<MO2>\plugins\mo2_mcp\`
- Reports which optional tools are detected on completion

After install:
1. Launch Mod Organizer 2
2. Tools → Start/Stop Claude Server
3. Restart Claude Code once so it discovers the MCP server

**SmartScreen warning:** the installer is unsigned (free OSS project, no EV cert budget). Click "More info" → "Run anyway" to proceed.

See [Manual Install](#manual-install) below if you prefer to copy files yourself, or [Building from Source](#building-from-source) if you want to build the installer and binaries yourself.

---

## Features

- **Full modlist access** — list mods, query plugins, resolve virtual file paths
- **ESP/ESM/ESL record reading** — parse any record type with field-level detail via Mutagen (localized strings resolve, VMAD scripts and properties render correctly)
- **Conflict detection** — field-by-field comparison across the full override chain
- **ESP patch creation** — overrides with field/flag/keyword/spell/perk/faction/inventory/package/outfit/form-list/leveled-list/condition/script modifications; leveled list merging (LVLI/LVLN/LVSP). Built on [Mutagen](https://github.com/Mutagen-Modding/Mutagen) via a direct NuGet reference. Writes route through `BeginWrite.WithLoadOrder` so ESL-flagged masters (ESPFE plugins like NyghtfallMM) get correctly-compacted FormLinks that resolve cleanly in xEdit and at runtime.
- **Papyrus** — compile `.psc` scripts to `.pex` (requires the Creation Kit for `PapyrusCompiler.exe` + base-Skyrim script sources). Decompile is intentionally not included — no currently-available decompiler produces clean round-trip output.
- **BSA/BA2 archives** — list, extract, and validate (requires BSArch.exe, ships with xEdit)
- **NIF meshes** — read format metadata; list textures and inspect shader properties (requires nif-tool.exe for the last two)
- **Audio/voice** — read FUZ headers, extract FUZ → XWM + LIP
- **SKSE plugin analysis** — PE header parsing, import/export tables, version info, string extraction
- **Virtual file system** — browse and read files through MO2's VFS as the game sees them
- **Write support** — create files in a designated output mod (safe, sandboxed)
- **29 tools** — see [Tool Reference](#tool-reference) below

## Requirements

- Mod Organizer 2 (v2.5.0+) with a Skyrim SE modlist
- Python 3.11+ (bundled with MO2)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (required by the bundled `mutagen-bridge.exe` and Spooky CLI for ESP patching and other capabilities)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) **v2.1.73 or later** — earlier versions install fine but the bundled `.claude/skills/` won't auto-load. Any MCP-compatible client also works for tool access; skills are a Claude Code feature.

### Optional — only needed for specific capabilities

- **BSArch.exe** — ships inside [xEdit](https://github.com/TES5Edit/TES5Edit)'s release archive. Required for `mo2_list_bsa` / `mo2_extract_bsa` / `mo2_validate_bsa`.
- **nif-tool.exe** — required for `mo2_nif_list_textures` and `mo2_nif_shader_info`. `mo2_nif_info` works without it.
- **Creation Kit** — required for `mo2_compile_script`. The Bethesda Creation Kit ships `PapyrusCompiler.exe` and the base-Skyrim script sources (`Scripts.zip`) that user compilation depends on.

## Manual Install

Alternative to the installer above. Use this if you prefer to copy files yourself, or if you're on a platform where the installer doesn't run.

1. Copy the `mo2_mcp/` folder into your MO2 `plugins/` directory
2. Copy `claude-mo2-setup-v2.6.1.exe` internals (specifically, the bundled `tools/mutagen-bridge/` and `tools/spooky-cli/`) into `plugins/mo2_mcp/tools/` — or run the installer once to populate those, then copy the result somewhere else
3. Restart MO2
4. Start the server: **Tools > Start/Stop Claude Server**

The plugin automatically configures Claude Code's MCP connection on first start — it merges the server entry into `~/.claude.json` under `mcpServers.mo2`. No manual config needed.

If you're using a different MCP client, point it to `http://localhost:27015/mcp` (Streamable HTTP transport).

## Quick Start

1. Start the server in MO2
2. Open Claude Code and set this folder as your project directory (or any folder — the server is registered user-scoped in `~/.claude.json`, so it's visible from any directory)
3. Start exploring:
   > "List my active plugins."
   >
   > "Show me the NPC_ record for Lydia in Skyrim.esm."
   >
   > "What conflicts does Dawnguard.esm have with my other plugins?"

The record index builds automatically on the first query that needs it (~75 seconds for a ~3000-plugin modlist from cold, ~8 seconds from cache on subsequent sessions). You can also kick off an explicit build with `mo2_build_record_index` at any time.

**Large modlists:** set `MCP_TIMEOUT=120000` in your environment before launching Claude Code. Cold force-rebuilds on ~3000+ plugin modlists can exceed Claude Code's default 60 s MCP tool timeout; the server-side build completes regardless, but the client-side call will appear to time out.

## Tool Reference

The plugin provides 29 MCP tools. See `kb/KB_Tools.md` for the full reference with parameters, usage patterns, and workflow examples.

| Category | Tools |
|----------|-------|
| **Modlist** | `mo2_ping`, `mo2_list_mods`, `mo2_mod_info`, `mo2_list_plugins`, `mo2_plugin_info`, `mo2_find_conflicts` |
| **File System** | `mo2_resolve_path`, `mo2_list_files`, `mo2_read_file`, `mo2_analyze_dll` |
| **Write** | `mo2_write_file` |
| **Records** | `mo2_record_index_status`, `mo2_build_record_index`, `mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary` |
| **ESP Patching** | `mo2_create_patch` |
| **Papyrus** | `mo2_compile_script` |
| **BSA/BA2** | `mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, `mo2_validate_bsa` |
| **NIF** | `mo2_nif_info`, `mo2_nif_list_textures`, `mo2_nif_shader_info` |
| **Audio** | `mo2_audio_info`, `mo2_extract_fuz` |

## How It Works

The plugin runs a lightweight HTTP server inside MO2's process, exposing MO2's Python API through the [Model Context Protocol](https://modelcontextprotocol.io/). When an AI assistant calls a tool (e.g., `mo2_query_records`), the server executes the query using MO2's live data and returns structured JSON results.

**Record indexing (Python + bridge):** A record index built on first use caches every record location across your load order. The index is a thin cache over the bridge — Python no longer reads ESP binary directly. Build time scales with modlist size (~75 s on a 3000-plugin modlist from cold via the bridge's `scan` command; ~8 s from cache thereafter). Index queries answer from the cache with no file I/O; every query runs a cheap mtime freshness check and re-scans only the plugins that actually changed.

**Field interpretation and ESP patching (Mutagen via mutagen-bridge):** When you need a record's actual field values (`mo2_record_detail`) or want to write an ESP patch (`mo2_create_patch`), the Python handler invokes `mutagen-bridge.exe` — a thin .NET 8 CLI that references [Mutagen](https://github.com/Mutagen-Modding/Mutagen) directly via NuGet for engine-correct parsing and writing.

**Ancillary modules (Spooky CLI subprocess):** Papyrus, BSA, NIF, and Audio tools shell out to Spooky's CLI with JSON output. Each is one subprocess per call.

## Configuration

Edit `mo2_mcp/config.py` to change defaults:

| Setting | Default | Description |
|---------|---------|-------------|
| `DEFAULT_PORT` | `27015` | Server port |
| `DEFAULT_OUTPUT_MOD` | `"Claude Output"` | Target mod folder for `mo2_write_file` |
| `DEFAULT_LOG_LEVEL` | `"info"` | Logging verbosity |

The output mod must exist in your MO2 mod list — create an empty mod if needed.

## Addon System

Claude MO2 supports modlist-specific knowledge files that layer on top of the base instructions:

- **`CLAUDE_[YourList].md`** — Modlist-specific rules and conventions. Claude loads all `CLAUDE_*.md` files on startup.
- **`KB_[Topic].md`** — Topic knowledge files, indexed by `KNOWLEDGEBASE.md`. Claude loads these on demand based on the task.

Claude will offer to create these as it learns about your modlist. They allow Claude to accumulate understanding of your specific setup across sessions.

## Security

- **Localhost only** — the server binds to `127.0.0.1`, never exposed to the network
- **User-controlled** — you manually start and stop the server from MO2's Tools menu
- **Auto-stop on launch** — the server stops when MO2 launches any executable (Skyrim, xEdit, etc.) and restarts after it exits, preventing conflicts with MO2's VFS setup
- **Write-sandboxed** — write access is limited to creating new files in one designated output mod
- **Read-only modlist** — cannot modify your load order, plugin state, or MO2 settings
- **No authentication** — the server has no auth mechanism. This is safe because it only binds to localhost, but be aware that any process on your machine can make requests to it while it's running
- **Logged** — all tool calls are logged to MO2's log panel

## Troubleshooting

**Plugin doesn't appear in Tools menu:** Verify `plugins/mo2_mcp/__init__.py` exists. Restart MO2. Check MO2's log panel for Python errors.

**Claude can't find the tools:** The plugin registers itself in `~/.claude.json` (under `mcpServers.mo2`) on first server start. Confirm that entry is present, then restart Claude Code so it picks up the new config. Claude Code caches the MCP tool list at session start, so adding a server mid-session has no effect until restart.

**Record queries return nothing:** The index builds lazily on the first query, so this is usually a transient "building in progress" state on a cold start. If queries continue to return empty, call `mo2_record_index_status` and look at `error_count` / `errors` — some plugins may be failing to scan. An explicit `mo2_build_record_index(force_rebuild=true)` forces a fresh rebuild from scratch.

**Server disappeared after launching a program:** Normal behavior. The server auto-stops when MO2 launches executables and auto-restarts when they exit. If it doesn't come back, restart it manually.

**Code changes not taking effect:** Delete `__pycache__/` inside `mo2_mcp/` AND restart MO2. Both steps are required — MO2 keeps module objects in memory across server stop/start cycles.

**Port conflict:** Change the port in `mo2_mcp/config.py` and update `.mcp.json` to match.

## Known Limitations

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for the full list. Key limitations:

- **`mo2_compile_script` requires Creation Kit** — Bethesda's base-Skyrim script sources (`Scripts.zip` inside CK) are needed to resolve types like `Actor`, `Quest`, `Debug`, etc. The compile tool works, but any script that references those base types will fail with "unknown type" until the sources are installed.
- **BSA and NIF-extras are gated on user-provided tools** — see Optional Requirements above.
- **Papyrus save-file reading is not yet implemented.**
- **Claude Code caches the MCP tool list at session start** — if you start the server mid-session, restart Claude Code to pick up the tools.
- **MO2 caches Python modules** — after editing any `.py` in the plugin, delete `__pycache__/` AND fully restart MO2.

## Building from Source

You only need this if you want to build the installer or the `mutagen-bridge.exe` binary yourself.

Prerequisites:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (not just the runtime)
- PowerShell (comes with Windows)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (only if you want to compile the installer)

Clone with the Spooky submodule:

```powershell
git clone --recursive https://github.com/<owner>/claude-mo2.git
cd claude-mo2
```

Or if you already cloned without `--recursive`:

```powershell
git submodule update --init --recursive
```

Build:

```powershell
# Binaries only (mutagen-bridge.exe + Spooky CLI into build-output\)
.\build\build-release.ps1

# Also compile the installer (.exe into build-output\installer\)
.\build\build-release.ps1 -BuildInstaller

# Build + sync binaries into an existing MO2 plugin dir
.\build\build-release.ps1 -SyncLive -MO2PluginDir "C:\ModOrganizer2\plugins\mo2_mcp"
```

The repo layout:

| Path | What |
|---|---|
| `mo2_mcp/` | Python plugin source (runs inside MO2) |
| `tools/mutagen-bridge/` | .NET 8 C# source for the Mutagen bridge |
| `installer/` | Inno Setup script + user-provided tool README stubs |
| `build/build-release.ps1` | Build pipeline |
| `spooky-toolkit/` | Git submodule — Spooky's AutoMod Toolkit (still required for the Spooky CLI build: Papyrus, BSA, NIF, non-FUZ audio). The mutagen-bridge itself does not depend on it. |
| `build-output/` | Build artifacts (gitignored; not committed) |
| `KB_*.md`, `CLAUDE.md` | Knowledge-base docs shipped with the plugin |

## License

MIT License. See [LICENSE](LICENSE) for details.

Bundled third-party components (all MIT-licensed):
- [pefile](https://github.com/erocarrera/pefile)
- [Mutagen](https://github.com/Mutagen-Modding/Mutagen) — linked into `mutagen-bridge.exe` via NuGet
- [Spooky's AutoMod Toolkit](https://github.com/SpookyPirate/spookys-automod-toolkit) — CLI (Papyrus, BSA, NIF, non-FUZ audio)

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for full attribution.

## Credits

- Built with [MO2's Python plugin API](https://github.com/ModOrganizer2/modorganizer)
- DLL analysis via [pefile](https://github.com/erocarrera/pefile) by Ero Carrera
- ESP reading and writing via [Mutagen.Bethesda.Skyrim](https://github.com/Mutagen-Modding/Mutagen) by the Mutagen team
- Papyrus / BSA / NIF / non-FUZ audio tools built on [Spooky's AutoMod Toolkit](https://github.com/SpookyPirate/spookys-automod-toolkit) by SpookyPirate
- ESP binary format reference from [UESP](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format)
