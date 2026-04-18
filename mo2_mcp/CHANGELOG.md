# mo2_mcp — Plugin Changelog

All plugin changes are made in the Dev Build copy first. Once tested and stable, they get promoted to the product folder (`Claude_MO2/`) and the live MO2 install (`<MO2>/plugins/mo2_mcp/`).

---

## v2.5.0 — 2026-04-17

Public-release prep: first shippable build. Audit cleanup, documentation rewrite, Inno Setup installer, repo layout for GitHub.

### Added

- **Windows installer (`claude-mo2-setup-v2.5.0.exe`).** Inno Setup script at `installer/claude-mo2-installer.iss`. Detects .NET 8 Runtime (guides to Microsoft if missing), prompts for MO2 folder (validates `ModOrganizer.exe` exists), copies the plugin + `spooky-bridge.exe` + Spooky CLI into `<MO2>\plugins\mo2_mcp\`. Post-install status screen reports which optional tools (BSArch, nif-tool, PapyrusCompiler) are detected, with download URLs for the ones missing. Uninstaller cleans `__pycache__` (plugin + ordlookup) and the `.record_index.pkl` cache.
- **`build/build-release.ps1 -BuildInstaller`** switch to compile the installer via ISCC. Requires Inno Setup 6 on PATH or at a common install path.
- **MIT license headers** on every Python tool module (5 files) and every C# bridge source file (6 files). Convention: 3-line `# ...` for Python, `// ...` for C#.
- **User-provided tool README stubs** bundled into the installer so users know where BSArch/nif-tool/PapyrusCompiler go, with download URLs for each.
- **Layer 1 repo layout** ready for GitHub: `.gitignore` excludes build artifacts and caches; `.gitmodules` references Spooky at `v1.11.1`; `README.md` has "Quick Install", "Manual Install", and "Building from Source" sections; `tools/spooky-bridge/` C# source + `installer/` script + `build/` pipeline all present at repo root.

### Fixed

- **`mo2_mcp/CHANGELOG.md` personal path leak.** Line 3 previously exposed the full path to the primary-author's live MO2 install; generic placeholder now.
- **Tool count accuracy in docs.** `README.md`, `CLAUDE.md`, and `KB_Tools.md` claimed "18 tools" (the v1.x count). Corrected to 29 (after the decompile-tool removal — see Removed section below).
- **`KNOWN_ISSUES.md` v1.x artifacts.** v1.x limitations that v4.0 resolved (localized-string placeholders, VMAD raw-hex, "read-only no patch creation", "no BSA extraction") removed or reframed as resolved. Current v2.x limitations listed: CK prereq for Papyrus compile, user-provided tools (BSArch / nif-tool / PapyrusCompiler), SPEL condition model, RecordReader depth cap.
- **`THIRD_PARTY_NOTICES.md` legal gaps.** Added Mutagen.Bethesda.Skyrim (MIT) and Spooky's AutoMod Toolkit (MIT, both CLI + `SpookysAutomod.Esp.dll`). Removed retired `esp.json` schema entry.
- **`KB_LeveledListPatching.md` stale tool name.** The v3.0 `mo2_mutagen_patch` reference (retired tool) corrected to `mo2_create_patch`.
- **README feature list** rewritten for v4.0 reality (ESP patching via Spooky + Mutagen, Papyrus, BSA, NIF, Audio) — previously described v1.x capability only.

### Changed

- **`PLUGIN_VERSION`** bumped to `(2, 5, 0)`.
- **`PLUGIN_DESCRIPTION`** updated to reflect v4.0 capabilities (previously only mentioned modlist / VFS / plugin metadata).
- **`build-release.ps1`** path references changed from layered (`Claude_MO2\tools\spooky-bridge\...`) to flat (`tools\spooky-bridge\...`) so the script runs from Layer 1 as a standalone repo root. `MO2PluginDir` default removed — now required when `-SyncLive` is set.
- **`claude-mo2-installer.iss`** source paths changed from `..\..\Claude_MO2\...` to `..\...` to match Layer 1 root layout.

### Removed

- **`mo2_decompile_script` MCP tool.** Champollion (the only production-grade Papyrus decompiler) has well-documented round-trip fidelity issues — lost operator precedence, dropped float casts, CustomEvents missing, events mis-tagged as functions, fragment-comment wrappers stripped. Rather than ship an unreliable tool, we removed the feature entirely. Users who need to decompile a `.pex` should use Champollion standalone and manually review output before any recompile.
- **Champollion bundling.** No longer shipped with the installer. Simplifies licensing (installer now bundles only MIT-licensed components) and reduces install size.
- **Dead code in `tools_papyrus.py`.** Removed `_find_spooky_cli` and `_invoke_cli` helpers (compile path bypasses Spooky's CLI and calls `PapyrusCompiler.exe` directly). Also removed a stale `result.get("result")` reference in the compile response that would have raised `NameError` if reached.

### Not changed

- All remaining MCP tool behavior — no runtime code changes in this release.
- Bridge binary behavior — no new operations or fixes in `spooky-bridge.exe` since v2.4.1.
- KB routing / session strategy — unchanged.

---

## v2.4.0 — 2026-04-17

Pre-validation polish. Three targeted additions that remove specific friction we'd hit during the Phase A-D test pass, each isolated so a regression in one can't affect the others.

### Added

- **Batch read in the bridge** — new command `read_records` with shape `{command: "read_records", records: [{plugin_path, formid}, ...]}`. Plugins load at most once per batch (cache keyed by path), so walking a conflict chain now costs one subprocess call instead of N.
- **`mo2_record_detail` accepts `plugin_names: [list]`** — batch variant. Returns `{records: [...], per_plugin_errors: [...]}` shape. Mutually exclusive with the existing `plugin_name` (singular).
- **`mo2_record_detail` accepts `resolve_links: true`** — post-processes bridge output in Python: any string matching `Plugin.esp:XXXXXX` gets annotated with its EditorID from the record-index reverse map (`_key_to_edid`). Turns `"Race": "Skyrim.esm:000019"` into `"Race": "Skyrim.esm:000019 (NordRace)"`. Unknown FormIDs pass through unchanged.
- **`build-release.ps1 -SyncPython` switch** — copies `Claude_MO2/mo2_mcp/*.py` and subdirs (except `tools/` and `__pycache__/`) to the live plugin dir. Wipes `__pycache__` on the way to force module reload. Implied when `-SyncLive` is set; pass `-SyncPython:$false` to opt out.

### Fixed

- **Byte-enumerable rendering in `RecordReader`.** Mutagen exposes some byte blobs (e.g., `WorldModel.Model.Data` MODT hashes, NPC Faction `Fluff` bytes) as `IReadOnlyList<byte>` wrappers, which my `IEnumerable` branch rendered as decimal integer arrays (`[2, 0, 0, 0, 26, 177, ...]`). Added explicit `IEnumerable<byte>` detection before the generic enumerable branch; blobs now render as hex (`"0200000002000000000000001AB1..."`).

### Not changed

- Single-record `read_record` semantics — existing callers keep working.
- `resolve_links` is opt-in (default false) — no unexpected output changes for existing workflows.

---

## v2.3.0 — 2026-04-17

Phase D — NIF mesh and voice-audio tools via Spooky CLI subprocess.

### Added (NIF)

- **`mo2_nif_info`** — Format metadata for a `.nif` (version, file size, header string). Library-native; no external binary dependency.
- **`mo2_nif_list_textures`** — Every texture path referenced by a NIF. Useful for auditing missing-texture references or pattern-matching texture prefixes across a mod.
- **`mo2_nif_shader_info`** — Shader flags on `BSLightingShaderProperty` blocks. Useful for debugging lighting/material issues.

Both list/shader tools shell out to `nif-tool.exe` (a Rust binary from the Spooky team). Not bundled in our build; when missing, Spooky's error passes through with placement suggestions (`{spooky-cli-dir}/tools/nif-tool/`).

### Added (Audio)

- **`mo2_audio_info`** — Metadata for FUZ/XWM/WAV files.
- **`mo2_extract_fuz`** — Split a `.fuz` voice file into its `.xwm` + `.lip` components. Writes to the output mod under `FuzExtract/<basename>/` by default.

### Smoke-tested

- `nif info` confirmed working on a real Skyrim NIF (v20.2.0.7 Gamebryo).
- `audio info` status: Spooky's v1.11.1 "Not a valid FUZ file" message on real FUZes with correct magic bytes (`FUZE\x01\x00\x00\x00`) looks like an upstream parsing issue — to be filed against Spooky. Our wrapper passes the error through as-is.

### Runtime deps summary after Phase D

| Binary | Required by | Shipping strategy |
|---|---|---|
| `spooky-bridge.exe` | All ESP patching | Built by `build-release.ps1` |
| `spookys-automod.exe` | Papyrus, Archive, NIF, Audio | Built by `build-release.ps1` |
| `PapyrusCompiler.exe` | `mo2_compile_script` | Auto-downloaded via `spookys-automod papyrus download` |
| `Champollion.exe` | `mo2_decompile_script` | Auto-downloaded (same command) |
| `BSArch.exe` | All `mo2_*_bsa` tools | Manual — user extracts from xEdit's release 7z |
| `nif-tool.exe` | `mo2_nif_list_textures`, `mo2_nif_shader_info` | Manual — Spooky's Rust binary |

---

## v2.2.0 — 2026-04-17

Phase C — BSA/BA2 archive tools via Spooky CLI subprocess.

### Added

- **`mo2_list_bsa`** — List files inside a `.bsa`/`.ba2`. VFS-resolves the archive path. Optional glob filter (`*.nif`, `textures/*`). Default limit 500 entries (set 0 for all).
- **`mo2_extract_bsa_file`** — Pull a single file out of an archive. Preferred over bulk extract when Claude only needs one asset (e.g., decompile one script from inside a mod's BSA). Writes to the configured output mod preserving archive path.
- **`mo2_extract_bsa`** — Bulk extract with a **required** filter to prevent accidental 2+ GB dumps. Output goes to `{output_mod}/{archive_basename}/` by default.
- **`mo2_validate_bsa`** — Archive integrity check. Reports format version, corrupt entries, unreadable files.

### Deployment requirement

- **BSArch.exe is required at runtime.** Not bundled — it ships inside xEdit's release 7z. Spooky's status/error responses include install suggestions (links to xEdit releases + Nexus Mods mirror). Place `bsarch.exe` under `{spooky-cli-dir}/tools/bsarch/`. Once installed, `spookys-automod archive status --json` returns `{"success": true, "bsarchPath": "..."}`.

### Supersedes

- The rolled-back v1.0.5-bsa libbsarch ctypes approach documented in `SESSION_HANDOFF_2026-04-12_bsa_testing.md`. Spooky's BSArch subprocess wrapper handles the same workflows without the DLL fragility.

---

## v2.1.0 — 2026-04-17

Phase B — Papyrus compile/decompile via Spooky CLI subprocess.

### Added

- **`mo2_decompile_script`** — Decompile a `.pex` to Papyrus source. VFS-resolves the script path (or accepts an absolute path), invokes `spookys-automod papyrus decompile --json` in a temp directory, returns the decompiled `.psc` text. Uses Champollion under the hood (auto-downloaded via `spookys-automod papyrus download` on first run).
- **`mo2_compile_script`** — Compile Papyrus `.psc` source to `.pex`. Takes source text + filename, writes to a temp `.psc`, resolves headers from VFS (tries `Scripts/Source/` then `Source/Scripts/`, overridable via `headers_path`), runs `spookys-automod papyrus compile --json`, copies the resulting `.pex` into the output mod's `Scripts/` folder.

### Plugin Settings

- New: `spooky-cli-path` — explicit override for `spookys-automod.exe` location. Default search order: `{plugin_dir}/tools/spooky-cli/spookys-automod.exe`, then `{plugin_dir}/tools/spookys-automod.exe`.

### Under the hood

- `tools_papyrus.py` — subprocess wrapper around Spooky CLI. Single-op per call (no batch), which is fine because Papyrus workflows are naturally single-op (one decompile, one compile) and CLI startup cost is negligible against compile time.
- `build/build-release.ps1` default now exercises the Spooky CLI build; `-SyncLive` copies both `spooky-bridge/` and `spooky-cli/` into the live MO2 plugin dir. Use `-SkipCli` for bridge-only iteration.

### Not yet wired

- Compile error messages — Spooky reports them in the CLI JSON response but we don't surface them in a special-cased way yet. For now they pass through as the bridge's raw response under `cli_result`.
- Batch decompile — single-script only. Callers decompile one script at a time; a batch version would be trivial to add but isn't part of the Phase B scope.

---

## v2.0.0 — 2026-04-17

Spooky-backed Mutagen bridge — ESP patching now built on Spooky's AutoMod Toolkit v1.11.1 library for correct master handling and battle-tested Mutagen integration. Custom `mutagen-bridge.exe` retired.

### Architecture Change

- **Bridge rebuilt on Spooky.** The v1.2.0 custom `mutagen-bridge.exe` is retired. Replaced by `spooky-bridge.exe` which references `SpookysAutomod.Esp.dll`. Reasons:
  - v1.2.0 had a critical master-limit bug (plugins with 200+ source-plugin masters produced invalid ESPs exceeding the 255-engine limit). Spooky's `PluginService.CreateOverride` handles masters correctly.
  - Three dispatch gaps in v1.2.0 (container inventory, non-NPC flags, CopyAsOverride record types) — Spooky's battle-tested patterns eliminate the knowledge-gap class of bugs.
  - Mutagen version pinning inherited from Spooky (v0.52.0) — reproducible builds.
- **Unchanged MCP interface.** Tool name (`mo2_create_patch`), JSON request/response shape, and all existing operations work identically. Only internal plumbing changed.
- **`spooky-bridge.exe`** lives at `mo2_mcp/tools/spooky-bridge/spooky-bridge.exe` (self-contained publish). `.NET 8 runtime` dependency unchanged from v1.2.0.
- **Plugin setting renamed:** `mutagen-bridge-path` -> `spooky-bridge-path`.

### Added

- **`add_conditions[*].global`** — Build `ConditionGlobal` against a Global variable FormID instead of `ConditionFloat` against a literal. Common Requiem-ecosystem pattern for MCM-configurable patches (e.g., `if GLOB_SomeToggle == 1`). Mutually exclusive with `value`.
- **VMAD `Alias` property type** — 6th `attach_scripts[*].properties[*].type` option (alongside Object/Int/Float/Bool/String). Alias properties link to a quest's named alias; JSON shape: `{"quest": "Plugin:FormID", "alias_id": int}`. Fills a gap in v1.2.0 which only supported the first 5 types.

### Under the hood

- `SpookysAutomod.Esp.dll` referenced as a library via local clone at `spooky-toolkit/` (tag v1.11.1). Build pipeline: `build/build-release.ps1`.
- PatchEngine.cs ported forward from v1.2.0 with the two schema extensions above. Bug fixes from v1.2.0's vulnerability audit (master limit, container inventory, general flags, expanded `CopyAsOverride` dispatch to 50+ record types) all preserved.
- Spooky CLI available for future Phases B-D (Papyrus / BSA / NIF / Audio) — not wired up in v2.0.0.

### Field interpretation — rebuilt on Mutagen (Phase A+)

- **`mo2_record_detail` now reads via the bridge.** New bridge command `read_record` (JSON: `{"command": "read_record", "plugin_path": "...", "formid": "Plugin:FormID"}`) loads the target plugin via Mutagen's `CreateFromBinaryOverlay` and returns all fields as a JSON dict. Reflection-based walker handles FormLinks ("Plugin:FormID"), enums (name strings), nested Mutagen types (recursive dict), AssetLink/AssetPath (path string), and P2/P3 point types (flat {X,Y,Z}).
- **More accurate than the retired schema walker.** Mutagen correctly handles VMAD fragments, localized strings, union deciders, enum flags — all gaps in v1.x's `esp_fields.py`/`esp_schema.py` approach.
- **Single-plugin read semantics preserved.** Same as v1.x: `mo2_record_detail(plugin_name=X)` returns that plugin's version of the record. FormLinks in the output stay as FormKeys; resolution to EditorIDs across the chain is a future enhancement.

### Retired

- `tools_mutagen.py` -> replaced by `tools_patching.py` (same tool name `mo2_create_patch`, new backing bridge).
- `tools/mutagen-bridge/` C# project -> replaced by `tools/spooky-bridge/`. Source of v1.2.0 bridge preserved in v4.0 `backups/mutagen-bridge-v3/`.
- `esp_fields.py`, `esp_schema.py`, `schema/SSE.json`, `test_esp_schema.py` — **fully retired in this release**. Sources preserved in v4.0 `backups/retired-esp-fields/` for reference. `esp_reader.py` stays but is now used only for record-index building (header + EDID scan), not field interpretation.

---

## v1.2.0 — 2026-04-16

Mutagen bridge — all ESP patching now uses Mutagen via `mutagen-bridge.exe`. Python ESP writer retired.

### Architecture Change
- **All patching routed through Mutagen.** The Python ESP writer (`esp_writer.py`, `tools_patch.py`) is retired. `.NET 8 runtime` is now a dependency. All ESP output is Mutagen-validated — guaranteed correct binary format.
- **Single tool:** `mo2_create_patch` is now powered by the Mutagen bridge (replaces both the old Python `mo2_create_patch` and `mo2_mutagen_patch`).
- **`mutagen-bridge.exe`** — .NET 8 CLI tool using Mutagen.Bethesda.Skyrim v0.52.0. Accepts JSON on stdin, returns JSON on stdout. Lives in `mo2_mcp/tools/`.

### Added — Override Operations
- **`set_fields`** — Set arbitrary typed fields via reflection. Supports friendly aliases (Health, Magicka, Stamina for NPC; ArmorRating/Value/Weight for ARMO; Damage/Speed/Reach for WEAP) and raw Mutagen property paths.
- **`set_flags` / `clear_flags`** — Set or clear named flags on records (Essential, Protected, Female, etc.).
- **`add/remove_keywords`** — Add/remove keywords on any keyworded record (ARMO, WEAP, NPC_, ALCH, AMMO, BOOK, FLOR, INGR, MISC, SCRL).
- **`add/remove_spells`** — Add/remove spells from NPC ActorEffect list.
- **`add/remove_perks`** — Add/remove perks from NPC perk list.
- **`add/remove_packages`** — Add/remove AI packages from NPC package list.
- **`add/remove_factions`** — Add/remove faction memberships with rank on NPCs.
- **`add/remove_inventory`** — Add/remove items from NPC/container inventory.
- **`add/remove_outfit_items`** — Modify outfit record contents (OTFT).
- **`add/remove_form_list_entries`** — Add/remove entries from form lists (FLST).
- **`add_items`** — Add entries to leveled item lists (LVLI) with level and count.
- **`add/remove_conditions`** — Add/remove conditions on perks, spells, packages, magic effects. Supports all Mutagen condition functions via reflection.
- **`attach_scripts`** — Attach Papyrus scripts with typed properties (Object, Int, Float, Bool, String) to any VMAD-supporting record.
- **`set/clear_enchantment`** — Set or remove enchantment on ARMO/WEAP records.

### Added — Merge Operations
- **`merge_leveled_list`** — Merge entries from multiple plugins into a single leveled list. Supports LVLI, LVLN, and LVSP. `base_plugin` parameter allows specifying which plugin version to use as the merge base (critical for overhaul-aware merging — see KB_LeveledListPatching.md).

### Added — Knowledge Base
- **`KB_LeveledListPatching.md`** — Reasoning framework for leveled list conflict resolution. Covers plugin intent classification, base selection, merge strategy, nested lists, weighting, and common mistakes.

### Verified
- Override + keyword addition: xEdit validated (ARMO record from Skyrim.esm)
- set_fields reflection: Value and Weight set correctly via aliases
- Leveled list merge: xEdit validated (LItemBanditBossShield with Vikings + WAR)
- Full MCP round-trip: mo2_create_patch → subprocess → bridge → ESP → xEdit

### Retired
- `esp_writer.py` — Python binary ESP writer (Phase 0 + Phase 1). Backed up to `backups/python_writer_2026-04-16/`.
- `tools_patch.py` — Python MCP tool wrapper. Backed up.

---

## v1.1.0 — 2026-04-16

ESP patch writer with field modification (Phases 0 + 1). **Superseded by v1.2.0 Mutagen bridge.**

### Added
- **`mo2_create_patch` tool (19th tool):** Creates ESP patch plugins by copying records from source plugins with correct FormID remapping. Records are selected by FormID with optional source plugin override. Output is written to the configured output mod directory. Supports ESL-flagging.
- **`esp_writer.py` — Phase 0 (raw bytes override):** `PatchBuilder` class copies complete records from any source plugin into a new patch ESP. Reads raw record data, decompresses if needed, remaps all FormID references (record header + subrecord data) using schema-aware walking of SSE.json field definitions. Merges master lists from multiple source plugins. Writes valid ESP binary with TES4 header, type-0 GRUPs, MAST+DATA pairs.
- **`esp_writer.py` — Phase 1 (subrecord splice):** `PatchBuilder.modify_record()` modifies individual field values within copied records. Subrecord locator finds subrecords by type in raw byte stream (handles XXXX oversize markers, repeated signatures). Field serializer reverses `esp_fields.py` interpretation — converts Python values to raw bytes for primitives (uint8/16/32, int8/16/32, float), FormIDs, and strings. Handles enum label→int, flags list→bitfield, and divide reversal. Splice operation overwrites field bytes in-place (same size) or rebuilds the subrecord stream (different size).
- **`tools_patch.py`:** MCP tool wrapper with `modifications` parameter per record — each modification specifies a subrecord type, optional field name within a struct, and the new value.

### Verified
- Single ARMO record copy: valid ESP, field-for-field identical to source
- Multi-record from multiple plugins: master lists merged correctly
- FormID remapping: header and cross-plugin subrecord references remapped
- Round-trip: create → index → record_detail → compare source: all fields match
- **Phase 1: field modification validated in xEdit** — changed numeric values in DATA subrecord, confirmed correct in xEdit

---

## v1.0.6 — 2026-04-13

Fixed `mo2_list_files` root path bug and VFS path reconstruction. Added line-range reads to `mo2_read_file`.

### Fixed
- **`mo2_list_files` root path (bugs #2 and #6):** Querying `"."` returned 0 results because MO2's `findFiles` doesn't recognize `"."` as root. Now normalizes `"."` / `"./"` to `""`. Also fixed the underlying VFS path reconstruction — previously used `os.path.basename()` which lost subdirectory info and produced wrong paths. New `_vfs_relative_path()` helper reconstructs correct game-relative paths from absolute disk paths using `modsPath()`, `overwritePath()`, and game data directory. Non-recursive filtering now actually works (was declared but never implemented).
- **`truncated` field:** Now correctly reflects whether the result list was capped at the limit, not whether the raw VFS search had more entries.

### Added
- **`mo2_read_file` offset/limit:** Optional `offset` (0-based line number) and `limit` (max lines) parameters for partial reads of large files. Omitting both returns the full file (backward compatible). Response always includes `total_lines`. When offset/limit used, also includes `offset` and `lines_returned` in response.

### Changed
- `tools_filesystem.py` — rewrote `_list_files`, enhanced `_read_file`, added `_vfs_relative_path` helper.

---

## v1.0.5 — 2026-04-13

Auto-setup Claude Code MCP config on server start.

### Added
- **`_ensure_claude_mcp_config(port)`:** On server start, automatically writes or updates `~/.claude/.mcp.json` with the MO2 server entry. Merges with existing config to preserve other MCP servers the user may have. Silently skips if `~/.claude/` directory doesn't exist (Claude Code not installed). Never fails server startup — all errors are caught and logged as warnings.
- Called from both `_start_server()` (manual start) and `_on_finished_run()` (auto-restart after game launch).
- Success dialog updated to inform users the config was handled automatically.

### Why
Claude Code discovers MCP servers via `.mcp.json` files. The project-level `.mcp.json` fails to be discovered when the install path contains spaces (common on Windows). Writing to the user-level `~/.claude/.mcp.json` bypasses this entirely — zero manual setup required.

### Changed
- `__init__.py` only — added `import os` and the `_ensure_claude_mcp_config()` function.

---

## v1.0.5-bsa — ROLLED BACK (2026-04-12)

BSA/BA2 tools via libbsarch.dll. Implemented, tested (all 6 tests pass), but rolled back — libbsarch ctypes interop is fragile and results cannot be validated at scale without extracting every archive. Code preserved in `backups/bsa_tools_2026-04-12/`. See `SESSION_HANDOFF_2026-04-12_bsa_testing.md` for full details and alternative approaches.

---

## v1.0.4 — 2026-04-12

New tool: `mo2_analyze_dll` for analyzing SKSE plugin DLLs.

### Added
- **`mo2_analyze_dll` tool:** Parses PE headers of SKSE plugin DLLs through MO2's VFS. Returns file metadata, compile info (architecture, timestamp), version info from PE resources, imports (grouped by DLL), exports, SKSE API detection (modern vs legacy), companion PDB check, and categorized string extraction (errors, file references, engine strings, versions).
- **Bundled `pefile` library:** MIT-licensed pure-Python PE parser (v2024.8.26) with `ordlookup` dependency. Patched for MO2 compatibility (mmap and ordlookup imports wrapped in try/except for environments that may lack them).
- **New module `tools_dll.py`:** Follows existing tool registration pattern from `tools_filesystem.py`.

### Performance
- Small DLLs (~500KB): ~93ms
- Medium DLLs (~2.5MB): ~1050ms
- Large DLLs (~3.9MB): under 2s
- String extraction adds ~100-200ms on top of PE parsing

---

## v1.0.3 — 2026-04-12

Auto-stop/restart MCP server around executable launches to prevent MO2 hang.

### Added
- **`onAboutToRun` hook:** Server auto-stops before MO2 launches any executable (Skyrim, xEdit, BodySlide, etc.), preventing the hang caused by the HTTP server thread conflicting with MO2's VFS setup.
- **`onFinishedRun` hook:** Server auto-restarts after the launched executable exits, but only if it was running before the launch.
- **`_start_server_core()`:** Silent server start method (no message box) used by auto-restart. Manual start/stop via Tools menu still shows dialogs.

### Changed
- `__init__.py` only — no changes to `mcp_server.py` or tool handlers.

---

## v1.0.2 — 2026-04-12

VMAD (Virtual Machine Adapter) parser fix. Script data in records now parses correctly instead of returning garbled output.

### Fixed
- **Prefix-counted strings:** Added `prefix` handling to `_read_string` for length-prefixed strings (uint16 length + chars, no null terminator). Previously read the length prefix as a character, misaligning all subsequent reads.
- **Prefix-counted arrays:** Added `prefix` handling to `_read_array` for count-prefixed arrays. Previously read until end-of-data, ignoring the element count.
- **Union deciders for VMAD:** Implemented `ScriptPropertyDecider` (selects property value type based on Type field) and `ScriptObjFormatDecider` (selects Object v1/v2 based on VMAD header). Previously always used the first union element (Null), so all property values were None.
- **Context passing:** Added `context` dict threaded through `_interpret` -> `_read_struct` -> `_read_union` -> `_read_array` so union deciders can reference sibling and ancestor field values.

### Affected
- All 21 record types with VMAD: ACHR, ACTI, APPA, ARMO, BOOK, CONT, DOOR, EXPL, FLOR, FURN, INGR, KEYM, LIGH, MGEF, MISC, NPC_, REFR, TACT, TREE, WEAP, and reference records
- Property types now correctly parsed: None, Object, String, Int32, Float, Bool, and all array variants
- QUST fragment data beyond basic scripts may still show as raw hex (separate scope)

---

## v1.0.1 — 2026-04-12

Bug fixes for all five issues found during the v1.0.0 diagnostic session.

### Fixed
- **Editor ID filter timeouts (Bug 4):** Added `_key_to_edid` reverse index, replacing O(n*m) linear scan with O(1) dict lookup in `query_records()` and `_find_edid()`
- **`mo2_query_records` type error (Bug 1):** Coerce `limit`/`offset` to `int` in all handlers across `tools_records.py`, `tools_modlist.py`, and `tools_filesystem.py`; coerce `force_rebuild` string-to-bool in `_handle_build_index`
- **`mo2_list_files` VFS leak (Bug 2):** Added `mod_name` parameter to filter results to files provided by a specific mod
- **`mo2_find_conflicts` parameter confusion (Bug 3):** Updated tool description and `mod_name` parameter docs to clarify it takes mod folder names, not plugin filenames; added helpful error when input looks like a plugin filename
- **Record index build encoding error (Bug 5):** Encode plugin names to ASCII before passing to `qInfo()`; replaced em-dash in completion log with plain hyphen

---

## v1.0.0 — 2026-04-12

Initial release. Renamed from `authoria_mcp` to `mo2_mcp`. Sent to external tester.

- 17 MCP tools (filesystem, modlist, record, write)
- ESP/ESM record parser with SSE.json schema
- Record index (~18–20s build, cached to `.record_index.pkl`)
- Zero personal-modlist references in code

### Known Bugs (from diagnostic session)
1. `mo2_query_records` type error — comparison fails between int and str when combining `record_type` + `plugin_name` filters
2. `mo2_list_files` VFS leak — returns entire VFS instead of filtering to target mod
3. `mo2_find_conflicts` parameter confusion — takes mod folder names, not plugin filenames; docs misleading
4. Editor ID filter timeouts — `editor_id_filter` for common substrings times out; direct FormID lookups work fine
5. Record index build encoding error — em-dash in plugin name causes ascii codec error (cosmetic, index still completes)
