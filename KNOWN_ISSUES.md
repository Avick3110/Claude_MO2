# Known Issues & Limitations

Current as of v2.6.1. These are known limitations, not bugs — all reported bugs have been resolved. For the full version history see `mo2_mcp/CHANGELOG.md`.

---

## User-provided prerequisites

These are by design — we don't bundle proprietary or license-undecidable tools. Missing any of these disables only the capabilities that depend on them; everything else continues to work.

### Papyrus compilation requires Creation Kit

`mo2_compile_script` uses Bethesda's `PapyrusCompiler.exe` and needs the base-Skyrim script sources (`Scripts.zip` ships inside the Creation Kit install). Without them, the compiler fails with "unknown type" errors on SKSE's and base-Skyrim's `.psc` files whenever your script extends `Actor` / `Quest` / etc. or calls anything like `Debug.Notification`.

**Workaround:** Install Creation Kit. Extract `Scripts.zip` into a mod that MO2 sees, so the VFS includes the base headers.

**Impact:** Affects `mo2_compile_script` only. All other MCP tools work without the Creation Kit.

### BSA tools require BSArch.exe

`mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, and `mo2_validate_bsa` shell out through Spooky's CLI to `BSArch.exe`, which we do not redistribute.

**Where to get it:** BSArch ships inside [xEdit](https://github.com/TES5Edit/TES5Edit)'s release archive. Extract it and place `bsarch.exe` at `<plugin>/tools/spooky-cli/tools/bsarch/bsarch.exe`.

**Impact:** Those four archive tools fail clearly until BSArch is installed. Everything else works.

### NIF extras require nif-tool.exe

`mo2_nif_list_textures` and `mo2_nif_shader_info` invoke `nif-tool.exe`, a Rust binary created for Spooky's toolkit. Its license is currently undetermined, so we don't redistribute it.

**Where to get it:** Shipped in Spooky's v1.11.1 release 7z. Place at `<plugin>/tools/spooky-cli/tools/nif-tool/nif-tool.exe`.

**Impact:** Those two tools fail with guidance if the binary is missing. `mo2_nif_info` works without it (library-native via Spooky).

---

## Design-trade-off limitations

### Leveled list merges require user judgment

`mo2_create_patch` can merge LVLI / LVLN / LVSP entries across conflicting plugins, but the **base plugin** (whose records are used as-is) must be chosen by the caller. For an overhaul conflict with a content mod, using the vanilla master as base would revert the overhaul's intentional restructuring (deleveling, reweighting) — you want the overhaul as the base and the content mod's unique entries merged in.

See the `leveled-list-patching` skill (`.claude/skills/leveled-list-patching/SKILL.md`) for the reasoning framework.

### Spell conditions apply at effect level, not record level

The bridge refuses `add_conditions` on SPEL records with "Record type Spell does not support conditions." This matches Mutagen's model — Skyrim spells carry conditions per magic effect, not on the spell record itself. To condition a spell, attach the condition to its MGEF (magic effect) record instead.

### RecordReader depth limit

`mo2_record_detail` walks Mutagen object graphs with a depth limit of 6 via reflection. Most records fit easily; extremely deep QUST/PACK/CELL structures could truncate as `"...[max depth reached]"`. If you encounter this, the depth is tunable in the bridge source (`ReadRequest.MaxDepth`).

### `mo2_record_detail` FormID resolution is opt-in

By default, FormIDs in the output are rendered as `Plugin:HexID`. Pass `resolve_links: true` to annotate each with its EditorID via the record index (`"Skyrim.esm:000019"` → `"Skyrim.esm:000019 (NordRace)"`). Opt-in because the extra lookup takes time on large records and most callers don't need it.

### Record queries default to enabled plugins only

Since v2.5.6, the five query tools (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) filter out plugins whose right-pane checkbox is unticked. Rationale: "winning plugin" claims and conflict chains should reflect what the game actually loads at runtime, not every plugin that ever touched the record.

Pass `include_disabled: true` for diagnostic queries ("was this record ever overridden, even by disabled mods?", "what would change if I enabled this plugin?"). When a record only exists in disabled plugins, the error distinguishes "not found" from "found but disabled" and tells the caller how to recover.

Implicit-load plugins (Skyrim.esm, DLC ESMs, Creation Club masters listed in `<game_root>/Skyrim.ccc`) are classified as enabled regardless of `plugins.txt` state — the engine auto-loads them. This was corrected in v2.5.7 after v2.5.6 initially missed the implicit-load case and reported `total_conflicts` off by ~2× on typical modlists.

---

## Environmental quirks (not code bugs, but worth knowing)

- **Claude Code v2.1.73+ required for skills auto-discovery.** The plugin ships procedures and tool-category references as skills under `.claude/skills/`. Claude Code auto-discovers these when the working directory contains the `.claude/` folder. Versions older than v2.1.73 may not support auto-discovery — the plugin still installs and the MCP tools still work, but task-specific skills (crash diagnostics, mod dissection, category-specific tool reference, etc.) won't fire automatically.
- **Claude Code caches the MCP tool list at session start.** If you start the server in MO2 mid-session, Claude Code doesn't see the new tools until you restart Claude Code.
- **MO2 doesn't reload Python modules on server stop/start.** After editing any `.py` inside the plugin, delete `__pycache__/` AND fully restart MO2 (not just the Tools > Start/Stop Claude Server toggle).
- **Claude Code reconnects to the MCP server automatically** after MO2's auto-stop-on-launch cycle (Skyrim / xEdit / etc.) or after a full MO2 restart, as long as the server comes back on the same HTTP URL. No CC restart needed for reconnection — this is an HTTP transport property; the old stdio-era requirement no longer applies. Only restart CC if you've added new MCP tools (server version change → cached tool list is stale) or changed the server port.
- **External filesystem changes require a manual MO2 refresh.** MO2 does not auto-detect `rm`/`cp`/`mv` of plugin files made outside its API. After any external change to plugin files (via Bash, another tool, or manual intervention), press F5 in MO2 (or use the Refresh button) before calling `mo2_create_patch`, `mo2_build_record_index`, or any read-back against the affected plugin. Skipping this leaves orphans in `loadorder.txt` and new plugins may be missing from the index entirely — symptoms include read-back returning empty even with `include_disabled: true`. Prefer `mo2_write_file` (routes through MO2's output mod, detected immediately) over Bash for plugin-adjacent writes.
- **Large modlists can exceed Claude Code's default MCP timeout on cold force-rebuild.** Claude Code's default MCP tool-call timeout is 60 s; `mo2_build_record_index(force_rebuild=true)` on ~3000+ plugin modlists takes roughly 76 s on reference hardware. The server-side build completes regardless — a follow-up `mo2_record_index_status` call will show `state: "done"` — but the client call appears to time out. **Set `MCP_TIMEOUT=120000` in your environment before launching Claude Code** to avoid the timeout entirely. Normal queries and cache-hit rebuilds stay well under the default.
- **Some plugins are rejected by Mutagen's strict parser.** The record index builds by handing every plugin to Mutagen for enumeration. Mutagen is stricter than xEdit about format conformance — plugins with malformed records (e.g. `DATA` subrecord length mismatches) can scan clean in xEdit but fail in Mutagen. Those plugins are absent from the record index; `mo2_record_index_status` lists them in the `errors` array. If a plugin you care about doesn't appear in query results, run xEdit's **Check for Errors** on it to confirm the state, then have the mod author fix it (or auto-clean via xEdit if feasible). Two plugins in the reference test modlist (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) are known to hit this — ~0.06% scan loss on a 3,384-plugin load order.

---

## Upstream (Spooky) issues we work around

These are reported or reportable to Spooky upstream; our wrappers already work around them:

- **`archive extract --filter` is ignored upstream.** Our `mo2_extract_bsa` full-extracts to a temp dir then filters on our side. Disk-usage trade-off for correctness; cleanup is automatic.
- **`audio info` rejects valid FUZ files.** Our bridge includes a local FUZ parser (`AudioCommands.cs`) so `mo2_audio_info` and `mo2_extract_fuz` don't depend on Spooky's broken path. XWM/WAV still go through Spooky's CLI.
- **`tools/` resolution is 5-up from the CLI exe.** Spooky's CLI looks for external tools at an unusual relative path. Our direct `PapyrusCompiler.exe` invocation sidesteps this, but if Spooky CLI is ever used directly, the user should be aware.

---

## Not yet implemented

**Papyrus save-file reading.** Can't yet read `.ess` save files to inspect script state at runtime — which scripts are loaded, variable values on suspended stacks, orphan script instances. Planned for Phase G of the roadmap. Static `.psc`/`.pex` analysis (via `mo2_compile_script` + Creation Kit) works today; only in-save runtime state is unavailable.

---

## Resolved bugs (history)

| Bug | Fixed in |
|-----|----------|
| `mo2_query_records` int/str type error | v1.0.1 |
| `mo2_list_files` VFS leak | v1.0.1 / v1.0.6 |
| `mo2_find_conflicts` parameter confusion | v1.0.1 |
| Editor ID filter timeouts | v1.0.1 |
| Record index build encoding error | v1.0.1 |
| `mo2_list_files` root path empty | v1.0.6 |
| `mo2_read_file` no pagination | v1.0.6 |
| ESP writer master-limit (258 masters → invalid ESP) | v2.0.0 (Mutagen bridge) |
| `add_inventory` broken on Container records | v2.0.0 |
| CopyAsOverride missing ~25 record types | v2.0.0 |
| Localized strings returned as `[lstring ID]` placeholders | v2.0.0 (Mutagen reads STRINGS files) |
| VMAD fragments rendered as raw hex | v2.0.0 |
| `mo2_record_detail` lost nested enumerable-of-byte blobs as int arrays | v2.4.0 |
| `ConditionGlobal` produced NULL reference in xEdit | v2.4.1 (reflection fix) |
| BSA extract ignored filter, storming subprocesses | v2.4.1 (full-extract-then-filter) |
| FUZ parser rejected valid files | v2.4.1 (local bridge parser) |
| Champollion tool-path resolution broken on live install | v2.4.1 (5-up placement) |
| Claude Code auto-registration wrote to `~/.claude/.mcp.json` (wrong path; CC reads `~/.claude.json`) | v2.5.1 |
| Plugin import error blocked MCP server startup (deleted `_find_spooky_cli`/`_invoke_cli` were still imported by three other modules) | v2.5.2 |
| `set_fields` silently failed on `ExtendedList<T>` fields — MUSC `Tracks`, FLST `Items`, OTFT `Items` — with "Cannot convert JSON Array" | v2.5.6 |
| `mo2_create_patch` reported `success: true` and inflated `records_written` when every per-record op failed | v2.5.6 |
| Bridge subprocess calls flashed black CMD windows, stealing focus during bulk operations | v2.5.6 |
| Implicit-load plugins (base-game ESMs + Creation Club masters in `Skyrim.ccc`) misclassified as disabled, silently hiding their records from default queries (`total_conflicts` off by ~2×) | v2.5.7 |
| `mo2_record_index_status` stripped the `errors` list from its response, only surfacing `error_count` | v2.5.7 |
| `mo2_build_record_index(force_rebuild=true)` was a silent no-op because on-disk cache reloaded immediately after the in-memory clear | v2.5.7 |
| ESL FormID compaction end-to-end: patches overriding records that reference ESL-flagged masters wrote unresolved FormLinks; `mo2_record_detail` returned non-compacted IDs that disagreed with xEdit | v2.6.0 (Mutagen-backed bridge + `PluginResolver` path-resolution fix) |
| Bridge routed reads/writes at the wrong file for plugin filenames that existed in more than one mod folder (alphabetical `mods/` walk vs MO2's priority-ordered VFS) | v2.6.0 (`organizer.resolvePath` replaces the walk) |
| Freshly-written patches couldn't be read back until the user ticked the MO2 checkbox — `mo2_record_detail` returned "Record not found" even with `include_disabled: true` | v2.6.0 (`mo2_create_patch` waits on MO2's `onRefreshed` signal before returning) |
| `mo2_build_record_index` fire-and-poll protocol required every caller to implement a polling loop that was easy to misuse | v2.6.0 (now blocking; returns full status dict) |
| Event-driven index invalidation (`onPluginStateChanged` full-rebuild fallback, debounced `onRefreshed` rebuild, `trigger_refresh_and_wait_for_index`) accumulated edge cases and silent stalls | v2.6.0 (replaced with lazy build + per-query mtime freshness check) |
| `_find_papyrus_compiler()` didn't check `<plugin>/tools/spooky-cli/tools/papyrus-compiler/` — the path the installer's README stub directs users to populate — so `mo2_compile_script` returned "PapyrusCompiler.exe not found" for users who followed the documented placement | v2.6.1 (in-plugin paths added as highest-priority search entries) |
