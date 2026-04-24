# mo2_mcp — Plugin Changelog

All plugin changes are made in the Dev Build copy first. Once tested and stable, they get promoted to the product folder (`Claude_MO2/`) and the live MO2 install (`<MO2>/plugins/mo2_mcp/`).

---

## v2.6.1 — 2026-04-24

Hotfix for a silent failure in `mo2_compile_script` where `PapyrusCompiler.exe` wasn't found at the installer-shipped path the `README_PAPYRUSCOMPILER.txt` stub directs users to populate.

### Fixed

- **`_find_papyrus_compiler()` now checks `<plugin>/tools/spooky-cli/tools/papyrus-compiler/`** before falling back to Spooky's `%USERPROFILE%/Documents/tools/papyrus-compiler/` auto-download paths. Both the flat `PapyrusCompiler.exe` layout (what the README stub directs users to produce when they copy CK's `Papyrus Compiler` folder contents into the placeholder dir) and the `Original Compiler/PapyrusCompiler.exe` sub-layout (what users get if they copy Spooky's auto-downloaded tree into the plugin dir) resolve. Prior to v2.6.1 the discovery function only checked the `%USERPROFILE%` variants — so users who followed the installer's README and placed the CK-extracted compiler into the plugin dir got `"PapyrusCompiler.exe not found"` errors at runtime even though the binary was sitting at the documented path.

### Not changed

- MCP tool count or interface — 29 tools, identical surface.
- Record index, bridge, or any non-Papyrus code path.
- Installer layout — the `<plugin>/tools/spooky-cli/tools/papyrus-compiler/` placeholder dir and `README_PAPYRUSCOMPILER.txt` stub are already shipped in v2.6.0; only the Python-side discovery changes in v2.6.1.

### Migration

No action required. The fix is Python-only; v2.6.1's installer is byte-identical to v2.6.0's in payload, apart from the version string and the one-line Python change. Users who encountered `"PapyrusCompiler.exe not found"` in v2.6.0 despite placing the compiler at the documented path will see compile work correctly in v2.6.1. Users who had the compiler at one of the `%USERPROFILE%/Documents/tools/papyrus-compiler/` fallback paths see no behavior change.

---

## v2.6.0 — 2026-04-23

Upgrade path: v2.5.5 (last public) → v2.6.0. v2.5.6 and v2.5.7 were local-only builds whose content rolls into this release; their individual entries are preserved below as historical record.

**Headline:** ESL FormID handling is correct end-to-end. Patches involving ESL-flagged masters (NyghtfallMM and other ESPFE plugins) now resolve FormLinks cleanly in xEdit and at runtime. Record queries against ESL plugins return xEdit-matching compacted FormIDs. Both sides of the bug are fixed — a Python path-resolution defect that routed the bridge at the wrong file on disk for plugins whose filename appeared in more than one mod folder, and Mutagen 0.52.0's lack of write-time master-flag recomputation.

### Changed — architecture

- **Mutagen-backed bridge with direct NuGet dependency.** The v2.5.x `spooky-bridge.exe` is renamed to `mutagen-bridge.exe` and now references `Mutagen.Bethesda.Skyrim 0.53.1` directly via NuGet PackageReference. The bridge no longer inherits Mutagen transitively through the Spooky toolkit submodule — the build graph is cleaner, version pinning is explicit, and ESL FormID correctness rides on Mutagen's own `BinaryWriteBuilder.WithLoadOrder` path rather than raw binary writes. The `mutagen-bridge-path` plugin setting replaces `spooky-bridge-path`; the old key is honored for one release as a backward-compat shim. Spooky's CLI (`spookys-automod.exe`) is unchanged and still ships for Papyrus / BSA / NIF / non-FUZ audio.
- **Write path routes through `BeginWrite.WithLoadOrder`.** `mo2_create_patch` now calls `KeyedMasterStyle.FromPath` per loaded plugin to build a load-order context, then writes via `patchMod.BeginWrite.ToPath(path).WithLoadOrder(masterStyled).Write()`. Mutagen recomputes master references at write time, so the v2.5.x `AddMasterIfMissing` pre-seeding is gone.
- **Bridge gains a `scan` command** used by the record-index rebuild path. `ScanRequest` / `ScanResponse` types in `Models.cs`, `IndexScanner.cs` enumerates major records via `SkyrimMod.CreateFromBinaryOverlay` + `mod.EnumerateMajorRecords()`, and FormIDs are emitted via `FormIdHelper.Format(record.FormKey)` — origin-resolved and ESL-compacted by Mutagen, matching xEdit by construction.

### Changed — record index is now a thin cache over the bridge

Every hand-rolled parallel implementation of MO2 or Mutagen domain logic has been deleted. The Python index stores what the bridge returns; it does not make its own FormID or plugin-state decisions.

- **Deleted entirely:** `esp_reader.py` (hand-rolled binary ESP/ESM parser), `esp_index.PluginResolver` (alphabetical `mods/` walk that caused the original path-resolution bug), `esp_index.resolve_formid` (manual master-table FormID resolution), `esp_index.read_active_plugins` (parsed `plugins.txt` for `*` prefixes), `esp_index.read_implicit_plugins` / `read_ccc_plugins` / `IMPLICIT_MASTERS` (hand-rolled implicit-load classification), `_FMT_RECORD` / `_FMT_GRUP` / `_FMT_SUBREC` struct formats, and `test_esp_reader.py`.
- **Replaced by:** direct use of MO2's `organizer.resolvePath(name)` for VFS-correct plugin paths, `organizer.pluginList().state(name) == ACTIVE` for plugin enable status (which correctly handles implicit-load base ESMs and Creation Club masters without our own parsing), and Mutagen's `FormKey` for FormID semantics.
- **Result:** every FormID the index surfaces now matches xEdit's view. On a representative 3,384-plugin modlist, `mo2_conflict_summary(include_disabled=true).total_conflicts` matches Phase 2's post-fix baseline at 427,180 (the v2.5.7 number of 428,260 was inflated by the implicit-load classifier crediting Skyrim.ccc entries whose plugins were not actually installed).

### Changed — index lifecycle

Event-driven invalidation is retired. The index builds lazily on first query and checks per-query freshness via file mtimes.

- **Lazy build on first query.** If the index isn't built when a read query arrives, it builds synchronously (auto-build-on-first-query). No more "you must call `mo2_build_record_index` first" friction for read tools.
- **Freshness check.** Every read handler calls `LoadOrderIndex.ensure_fresh()` up front. Three paths: no drift (~50–100 ms stat walk, common case), enable-only drift (in-place bulk bit flip), structural drift (bridge-rescan only the changed plugins and rebuild the merged index). The freshness check also reconciles MO2's in-memory `pluginList()` against on-disk `loadorder.txt` — freshly-written plugins present only in pluginList are included, which `loadorder.txt`'s deferred flush would otherwise miss.
- **`mo2_build_record_index` is now blocking.** Call returns only when the build completes and carries the full status dict. No more `{"status": "building"}` → poll protocol. Force-rebuild on ~3,000+ plugin modlists takes roughly 76 s on the reference machine and can exceed Claude Code's default 60 s MCP tool-call timeout (see Migration notes).
- **`onPluginStateChanged` is now a pure in-place flip.** The unknown-plugin fallback rebuild is gone — `ensure_fresh` covers it on the next query.
- **Retired:** `trigger_refresh_and_wait_for_index`, `_build_complete` event, `_rebuild_timer` + debounced rebuild machinery, `onRefreshed` and `onModMoved` rebuild hooks. `onRefreshed` is reinstated narrowly as a signal-only hook for the write-tool refresh wait described below.

### Changed — patch write workflow

- **Pre-enable read-back works.** After `mo2_create_patch` writes a plugin, the tool fires `organizer.refresh(save_changes=True)` and waits for MO2's `onRefreshed` signal (up to 30 s) before returning. The next `mo2_record_detail` / `mo2_query_records` call against records in the newly-written plugin resolves immediately — no need to tick the plugin's checkbox in MO2's right pane first. (Ticking the checkbox is still required to load the plugin in-game; it's just no longer a precondition for read-back.) Response carries `refresh_status: "complete" | "timeout"` and `refresh_elapsed_ms`. On timeout the call still succeeds; `next_step` tells the user to press F5 in MO2 to force the refresh.
- **Response shape changes.** The `mo2_refresh` field is gone from every write tool's response. `next_step` wording is simplified — no more "do NOT chain read-back calls in the same turn" caveat, since chaining is now safe.
- **`set_fields` on `ExtendedList<T>` collection fields works.** MUSC `Tracks`, FLST `Items`, OTFT `Items`, and every other `ExtendedList<IFormLinkGetter<T>>` property previously failed with "Cannot convert JSON Array to ExtendedList\`1" for every record, and the output ESP silently contained no overrides. A `JsonConverter<ExtendedList<T>>` in the bridge's Newtonsoft settings resolves FormID strings through the existing `FormLinkResolver` path and appends them into a freshly-constructed `ExtendedList<T>`. Array-of-FormID fields now round-trip correctly.
- **Honest `success` and per-record counts.** Before: `success: true` and `records_written: N` came back even when every per-record operation failed. Now: top-level `success` is `false` whenever any `details[].error` is non-null, and `successful_count` / `failed_count` / `records_written` reflect what actually landed in the output ESP. Failed records are rolled back from the Mutagen mod before write so the ESP contains only successful overrides.
- **Subprocess console windows are suppressed.** Every bridge and Spooky-CLI `subprocess.run` site uses `creationflags=subprocess.CREATE_NO_WINDOW`. Bulk operations (e.g. 40+ parallel `mo2_record_detail` calls) no longer strobe black CMD windows across the screen or steal focus.
- **Post-write auto-enable machinery is gone.** Four attempts across v2.5.6 beta builds at programmatically ticking the MO2 plugin checkbox each surfaced a different MO2-integration quirk; the retry stack grew to ~7 refresh cycles per patch for no reliable gain. The replacement flow is deterministic: write, refresh, wait for MO2, return. The user ticks the checkbox when they're ready to load the patch in-game.

### Changed — record queries default to enabled plugins only

The five query tools (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) filter out plugins whose right-pane checkbox is unticked. "Winning plugin" claims and conflict chains reflect what the game actually loads at runtime, not every plugin that ever touched the record. Pass `include_disabled: true` for diagnostic queries. When a record only exists in disabled plugins, the error distinguishes "not found" from "found but disabled" and tells the caller how to recover. Implicit-load plugins (Skyrim.esm, DLC ESMs, Creation Club masters listed in `<game_root>/Skyrim.ccc`) are classified as enabled regardless of `plugins.txt` state — the engine auto-loads them. Classification is now answered by MO2's `pluginList()` directly, which correctly excludes Skyrim.ccc entries whose plugins are not installed.

### Changed — smaller fixes (from v2.5.7)

- **`mo2_record_index_status` surfaces the `errors` list alongside `error_count`.** Previously the `**{k: v for k, v in result.items() if k != 'errors'}` comprehension stripped the field; replaced with an unconditional `**result` spread. Users seeing `error_count: 1` can now see which plugin(s) failed to scan.
- **`mo2_build_record_index(force_rebuild=true)` actually forces a re-scan.** `LoadOrderIndex.rebuild()` previously cleared only the in-memory cache; `build()`'s subsequent `_load_cache()` reloaded the on-disk pickle and every plugin came back as `cached_hits`. The pickle is now unlinked in `rebuild()` before the build runs.

### Changed — operational warnings routed through `qWarning`

Six `log.warning` call sites in `esp_index.py` (cache corruption, format-version mismatch, unexpected top-level type, disk I/O errors, freshness-check scan-fn failures) are now routed through a `_warn()` shim that uses `qWarning` when PyQt6 is importable and falls back to stdlib `logging` for organizer-less consumers like the test suite. Operational events are visible in MO2's log panel for forensic investigation.

### Changed — skill and doc updates

- The `esp-patching` skill's post-write workflow section is rewritten to reflect pre-enable read-back, the new `refresh_status` / `refresh_elapsed_ms` response fields, and the simplified `next_step` wording.
- `CLAUDE.md` drops the "you must call `mo2_build_record_index` first" instruction from First Session Setup — lazy auto-build covers it. The standing rule about external filesystem changes requiring a manual MO2 refresh is kept; MO2 still doesn't detect outside-API file changes on its own.
- `KNOWN_ISSUES.md` adds entries for the MCP timeout on force-rebuild, the two plugins Mutagen rejects that xEdit's lenient parser accepts, and the ESL FormID fix in the resolved-bugs table.
- `THIRD_PARTY_NOTICES.md` attributes Mutagen as a direct NuGet reference rather than a transitive dependency of the Spooky toolkit.

### Not changed

- MCP tool count, surface, or interface — 29 tools, same shape (the one response-field removal aside).
- The Spooky CLI (`spookys-automod.exe`) — still used for Papyrus, BSA, NIF, and non-FUZ audio. Papyrus compile requires Creation Kit; BSA tools require user-provided BSArch.exe; NIF extras require nif-tool.exe. All three gated tools behave the same as v2.5.5.

### Migration

- **Cache format v1 → v2.** The on-disk record-index pickle bumps to v2. Old caches are auto-invalidated and rebuilt on first use — no manual cleanup. Expect one ~75 s force-rebuild-equivalent on first session after upgrade; subsequent sessions hit the cache in ~8 s.
- **Plugin setting rename.** `spooky-bridge-path` → `mutagen-bridge-path`. The old key is honored for one release as a backward-compat shim. If you set `spooky-bridge-path` in MO2's plugin settings, rename it to `mutagen-bridge-path` at your convenience; it'll keep working until v2.7.
- **Write-tool response shape.** `mo2_refresh` is gone from every write tool's response. `next_step` wording is simplified. Callers that branched on `mo2_refresh` should switch to `refresh_status` (on `mo2_create_patch` specifically) or drop the branch — the new flow doesn't need it.
- **Record query default.** Queries default to enabled-only since v2.5.6. If you're upgrading from v2.5.5, analyses that relied on seeing the full "ever-touched" history now need `include_disabled: true`.
- **MCP timeout on force-rebuild.** Claude Code's default MCP tool-call timeout is 60 s. `mo2_build_record_index(force_rebuild=true)` on large modlists (~3,000+ plugins) takes roughly 76 s and will hit the timeout client-side. The server-side build completes regardless; a follow-up `mo2_record_index_status` call confirms `state: "done"`. **To avoid the timeout entirely, set `MCP_TIMEOUT=120000` in your environment before launching Claude Code.** Normal queries and cache-hit rebuilds stay under the limit.
- **Known plugin scan failures.** Two plugins in the reference test modlist (`TasteOfDeath_Addon_Dialogue.esp`, `ksws03_quest.esp`) are rejected by Mutagen's strict parser that xEdit's lenient parser accepts. Their records are absent from the index; everything else scans normally. If a plugin you care about doesn't appear in query results, run xEdit's "Check for Errors" on it — genuine corruption is on the mod author to fix, and `mo2_record_index_status`'s `errors` list names the affected plugins.

---

## v2.5.7 — 2026-04-21

Correctness fixes for v2.5.6's enabled/disabled filtering. Surfaced by live verification on 2026-04-21 (see `Live Reported Bugs/mo2_v256_enabled_disabled_filtering_RETEST_CLEAN_LOADORDER_2026-04-21.md`): the filter was classifying implicit-load plugins (base-game ESMs + Creation Club masters) as disabled, which silently hid their records from default conflict analysis. A large modlist saw default `mo2_conflict_summary.total_conflicts` reporting 229,609 vs. the true 428,260 — off by nearly 2× because every conflict involving `Skyrim.esm` / `Update.esm` / any CC master as origin was dropped from the chain.

### Fixed

- **Implicit-load plugins (Skyrim.esm, DLC ESMs, Creation Club masters) now correctly classified as enabled** (esp_index.py). These plugins load at runtime regardless of `plugins.txt` state — Skyrim auto-loads its base masters, and `<game_root>/Skyrim.ccc` lists the Creation Club content that loads implicitly. New module-level `IMPLICIT_MASTERS` frozenset covers the 5 hardcoded base masters (Skyrim, Update, Dawnguard, HearthFires, Dragonborn); new `read_ccc_plugins()` parses `Skyrim.ccc` from the game root; `read_implicit_plugins()` combines both. At index build time, the implicit set is unioned into `active_lower` before per-plugin classification, so these plugins are flagged `enabled=True` alongside starred `plugins.txt` entries. Graceful fallback when `Skyrim.ccc` is missing — base masters only.
- **`mo2_record_index_status` now surfaces the `errors` list alongside `error_count`** (tools_records.py). Previously `errors` (capped at 20 entries) was explicitly stripped when building `_build_status`, so users seeing `error_count: 1` had no way to see which plugin(s) failed to scan. Root cause: the `**{k: v for k, v in result.items() if k != 'errors'}` comprehension excluded the field; replaced with an unconditional `**result` spread.
- **`mo2_build_record_index(force_rebuild=true)` now actually forces a re-scan** (esp_index.py). `LoadOrderIndex.rebuild()` only cleared the in-memory `_plugin_cache`; `build()`'s subsequent `_load_cache()` reloaded the on-disk `.record_index.pkl` and every plugin came back as `cached_hits`. Fixed by also unlinking the pickle in `rebuild()` before calling `build()`. Post-fix a forced rebuild reports `scanned == total, cached_hits == 0`.

### Changed

- **`esp-patching` skill post-write workflow section rewritten** (`.claude/skills/esp-patching/SKILL.md`). Points #2 and #5 had stale/incorrect claims:
  - Old #2 asserted pre-enable read-back always returned empty. Correct behavior: when `loadorder.txt` is clean at `mo2_create_patch` time, the new plugin lands in the index as `enabled: false` and is visible via `include_disabled: true` before the user ticks the checkbox. Only when orphans disrupt MO2's refresh (from external `rm`/`cp`/`mv` without a manual MO2 refresh) does pre-enable read-back fail.
  - Old #5 claimed "the record index does not filter by plugin enable state" as a known separate bug — this shipped in v2.5.6 and was never updated in the skill. Corrected: filtering works; fresh patches correctly don't appear as conflict winners until the user enables them.
  - Finding I (external filesystem hygiene) folded into #2 and surfaced as a new always-loaded standing rule in `CLAUDE.md`: after any external `rm`/`cp`/`mv` on plugin files, ask the user to manually refresh MO2 (F5) before `mo2_create_patch`, `mo2_build_record_index`, or any read-back.
- **`PLUGIN_VERSION`** bumped to `(2, 5, 7)`.

### Not changed

- MCP tool count, behavior, or core interface — 29 tools, same surface.
- Cache format (`.record_index.pkl`) — per-plugin scan data only; enabled state is re-derived on every build.
- Bridge binary path or installer layout.

### Migration

No action required. Existing caches load fine; the new implicit-load classification is computed on every build from `Skyrim.ccc` + hardcoded base masters. After upgrading, `plugins_enabled` / `plugins_disabled` counts will shift — on a typical modlist, `plugins_disabled` may drop from 60+ to single digits as Creation Club masters reclassify. Default `mo2_conflict_chain` / `mo2_conflict_summary` / `mo2_query_records` output will now include vanilla and CC records that were previously hidden.

---

## v2.5.6 — 2026-04-20

Fixes for `mo2_create_patch` surfaced by a 2026-04-19 MUSC music merge patch that wrote an empty ESP and silently reported success. All three underlying bugs are fixed and verified end-to-end; the associated auto-refresh-and-enable machinery that was built on top proved unsustainable and has been removed in favour of a simpler write-and-refresh flow that asks the user to enable the plugin manually.

### Fixed

- **ESP `set_fields` on `ExtendedList<T>` collection fields** (bridge/PatchEngine.cs). Fields like `MUSICTYPE.Tracks`, `FLST.Items`, `OTFT.Items`, and other `ExtendedList<IFormLinkGetter<T>>` properties previously failed with `"Cannot convert JSON Array to ExtendedList\`1"` for every record and the output ESP contained no overrides. A new `JsonConverter<ExtendedList<T>>` registered into the bridge's Newtonsoft settings resolves FormID strings via the existing `FormLinkResolver` path and appends them into a freshly-constructed `ExtendedList<T>`. MUSC `Tracks` merge patches and all other array-of-FormID fields now round-trip correctly.
- **Honest `success` / per-record counts** (bridge/PatchEngine.cs + Models.cs). Before: `success: true` and `records_written: N` were returned even when every per-record operation failed. Now: top-level `success` is `false` whenever any `details[].error` is non-null, `successful_count` / `failed_count` / `records_written` reflect what actually landed in the output ESP, and failed records are rolled back from the Mutagen mod before write so the ESP contains only the successful overrides.
- **spooky-bridge.exe console windows no longer flash on every call** (all six `subprocess.run` sites in Python). Added `creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0)` to every bridge / CLI invocation. Previously, bulk operations (e.g., a 40+ record read-back using `mo2_record_detail` in parallel) strobed black CMD windows across the screen for minutes and stole focus from other apps. Now invisible.

### Changed

- **Dropped the `auto_enable` parameter on `mo2_create_patch` and the whole post-write auto-enable mechanism.** Four attempts across v2.5.6 beta builds at programmatically ticking the MO2 plugin checkbox (via `IPluginList.setState` routed through `organizer.onNextRefresh`, then a two-phase queue drained from `plugin_list.onRefreshed`, then observation-first retry) each surfaced a different MO2-integration quirk: `setState` silently no-ops for newly-written plugins, `organizer.refresh(save_changes=True)` can revert in-memory setState during its plugin-list rescan, and MO2 autonomously enables new plugins (fighting our opt-out path). The retry machinery was stacking up to 7 refresh cycles per patch (~2 minutes on Aaron's modlist) for no reliable gain. The replacement flow: `mo2_create_patch` writes the file, fires one MO2 refresh, waits for the record index to rebuild, and returns with a `next_step` field telling the user to tick the plugin's checkbox in MO2 when they're ready to load the patch in-game. Once the user ticks it, MO2's `onPluginStateChanged` event fires an auto-rebuild of the record index (no manual `mo2_build_record_index` needed) and `mo2_record_detail` / `mo2_query_records` see the new records. Pre-enable read-back returns empty — verified against live v2.5.6 on 2026-04-20 — so callers must wait for user confirmation before chaining read-back calls. Typical write call time drops from ~60–120s back to ~20–30s.
- **`trigger_refresh_and_wait` helper → `trigger_refresh_and_wait_for_index`.** Simpler shape: one refresh, one wait on `_build_complete`, no queue, no drain, no retry, no `plugin_to_enable` / `plugin_to_disable` / `output_mod` params. Response shape is correspondingly simplified to `{refreshed, elapsed_s, error}`; the old `mod_enabled` / `plugin_enabled` / `mod_enable_error` / `plugin_enable_error` fields are removed.
- **`next_step` field added to every write tool's response** (`mo2_create_patch`, `mo2_write_file`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, `mo2_extract_fuz`, `mo2_compile_script`). Each explains what the user needs to do — if anything — for the written content to be visible / loaded. For ESP patches this is "tick the right-pane checkbox when ready"; for VFS assets (files, extracted BSA contents, compiled .pex) there's no user action needed and `next_step` says so.
- **`esp-patching` skill** gains a "Post-write workflow" section describing the no-auto-enable flow and how Claude should phrase results to the user.
- **Record index now tracks plugin enable state and filters queries by it.** `PluginInfo` gains an `enabled` bit populated at build time from `plugins.txt` via the existing `read_active_plugins()` helper; the index still scans every plugin in `loadorder.txt` so toggling a checkbox doesn't invalidate the record cache. The five query tools (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) take a new `include_disabled` boolean that defaults to `false` — meaning "winning plugin" claims and conflict chains reflect what the game actually loads at runtime. Pass `include_disabled=true` for diagnostic queries ("was this record ever overridden, even by disabled mods?", "what would change if I enabled this plugin?"). When a record only exists in disabled plugins, the error now distinguishes "not found" from "found but disabled" and tells the caller how to recover. `mo2_record_index_status` stats now include `plugins_enabled` / `plugins_disabled` counts.
- **`onPluginStateChanged` no longer triggers a full index rebuild.** Toggling a plugin's right-pane checkbox now flips the `enabled` bit on the existing `PluginInfo` in place — queries pick up the new state immediately with no ~10-15s rebuild cost. Multi-select toggles still resolve in one event dispatch. Falls back to a full rebuild only when the event references a plugin the index hasn't seen yet (e.g., first-time enable of a freshly-written patch), so the just-written-and-then-enabled workflow still works end-to-end.
- **`PLUGIN_VERSION`** bumped to `(2, 5, 6)`.

### Not changed

- MCP tool count, behavior, or core interface — 29 tools, same surface.
- Bridge binary path or installer layout.
- Record index build path — still reads every plugin listed in `loadorder.txt` via `read_load_order()`. `plugins.txt` is now read alongside it to populate the `enabled` bit on each `PluginInfo`, but the record-scan workload is unchanged. Cache format (`.record_index.pkl`) stores per-plugin scan data only; enable state is re-derived on every build, so toggling a plugin never needs a re-scan.

### Migration

No action required for existing installs. Cache format (`.record_index.pkl`) is unchanged — the `enabled` bit on `PluginInfo` is derived fresh from `plugins.txt` on every build, so the old cache still loads without migration. Response shape on write tools adds `next_step` (new field, additive) and drops the `mod_enabled` / `plugin_enabled` / `*_enable_error` fields (removed; the `auto_enable` param on `mo2_create_patch` is gone with them). Callers that branched on those fields should now surface `next_step` to the user and wait for the user to tick the plugin's checkbox before attempting read-back queries against the just-written plugin.

**Behavior change for record queries:** the five query tools (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`) now default to enabled-only. Pre-v2.5.6 queries returned results from disabled plugins as if they were live; post-v2.5.6 they don't. Existing analyses that relied on seeing the full "ever-touched" history need to pass `include_disabled=true`. Error messages for "record exists only in disabled plugins" explicitly tell the caller how to recover, so accidental filter-outs are self-diagnosing.

---

## v2.5.5 — 2026-04-19

Full KB → Skills migration. Six task-procedure KBs and most of the tool reference (`kb/KB_Tools.md`) are now Claude Code skills at `.claude/skills/<name>/SKILL.md`. Skills are auto-discovered and trigger-matched by Claude Code based on each skill's `description` frontmatter — no manual routing table required. The always-loaded core (`kb/KB_Tools.md`) shrinks from 369 lines to 149 lines, covering only the tools used in every session (modlist queries, VFS, write, record indexing, record queries, conflict analysis) plus FormID format, field interpretation output types, and a pointer to the category skills. Functional behavior is unchanged; this is a delivery-mechanism upgrade that eliminates eager loading of procedures and tool categories that aren't relevant to the current task.

**Prerequisite: Claude Code v2.1.73+.** Earlier versions install fine but the bundled `.claude/skills/` folder won't auto-load.

### Added

- **6 task-procedure skills** at `.claude/skills/<name>/SKILL.md`:
  - `crash-diagnostics` (was `kb/KB_Diagnostics.md`)
  - `leveled-list-patching` (was `kb/KB_LeveledListPatching.md`)
  - `mod-dissection` (was `kb/KB_ModDissection.md`, now absorbs three procedural sections from `kb/KB_Tools.md`: script health check workflow, efficient conflict analysis workflow, CELL/WRLD ITM handling)
  - `npc-analysis` (was `kb/KB_NPCAnalysis.md`)
  - `npc-outfit-investigation` (was `kb/KB_NPC_Outfits.md`)
  - `session-strategy` (was `kb/KB_SessionStrategy.md`)
- **5 MCP tool category skills** (new — split out of `kb/KB_Tools.md`):
  - `esp-patching` — `mo2_create_patch` (override ops, merge leveled lists, attach scripts) + Mutagen field interpretation narrative
  - `papyrus-compilation` — `mo2_compile_script` and its Creation Kit prerequisite
  - `bsa-archives` — all four BSA/BA2 tools (`mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, `mo2_validate_bsa`)
  - `nif-meshes` — all three NIF tools (`mo2_nif_info`, `mo2_nif_list_textures`, `mo2_nif_shader_info`)
  - `audio-voice` — both audio tools (`mo2_audio_info`, `mo2_extract_fuz`)
- **`.claude/skills/` auto-bundled by the installer** into `<MO2>/plugins/mo2_mcp/.claude/skills/`. Same project-scope behavior as `CLAUDE.md` and `kb/KB_Tools.md` — skills are discovered when Claude Code opens a working directory that contains them.

### Changed

- **`kb/KB_Tools.md` reduced from 369 lines to 149 lines** (60% shrink). Now contains only the core tool reference loaded in every session: startup procedure, the core tool tables (modlist queries, VFS, write, index management, record queries, conflict analysis), FormID format, field interpretation output types table, and a "Category Skills" pointer. Tool-category documentation (ESP patching, Papyrus, BSA, NIF, audio) moved into the new category skills. Procedural content (efficient conflict analysis workflow, CELL/WRLD handling, script health check) moved into `mod-dissection`. ESP binary format quick reference and Nexus research guidance dropped — the former is derivable from UESP, the latter is already covered in `CLAUDE.md` and `mod-dissection`.
- **`CLAUDE.md`** — removed the "Operational KB Routing" block (no longer needed; skills auto-load). Added a Skills section listing all 11 bundled skills grouped into task-procedure skills and MCP tool category skills. "Building Knowledge Through Use" now routes findings into three buckets: modlist rules → `CLAUDE_*.md` addon, reusable procedures or tool categories → new skill, topic reference → `kb/KB_*.md`.
- **`KNOWLEDGEBASE.md`** — reduced to a short index pointing at the shrunken `kb/KB_Tools.md` and cross-referencing the skills in `CLAUDE.md`.
- **Installer** (`installer/claude-mo2-installer.iss`) — removed the six individual `kb/KB_*.md` copy lines for files that became skills; added one recursive copy of `.claude/skills/*`. `kb/KB_Tools.md` still ships as the always-load core tools reference.
- **`README.md`** — Requirements section now specifies Claude Code v2.1.73+ as the minimum supported version (for skills auto-discovery). Added note that any MCP-compatible client works for tool access; skills are a Claude Code feature.
- **`KNOWN_ISSUES.md`** — new "Environmental quirks" entry documenting the CC v2.1.73+ prerequisite for skills auto-discovery.
- **`PLUGIN_VERSION`** bumped to `(2, 5, 5)`.

### Removed

- **6 `kb/KB_*.md` files** from the repo (Diagnostics, LeveledListPatching, ModDissection, NPCAnalysis, NPC_Outfits, SessionStrategy). Content lives in the corresponding `SKILL.md` files.
- Several sections of `kb/KB_Tools.md` — ESP patching reference (→ `esp-patching` skill), Papyrus reference (→ `papyrus-compilation` skill), BSA/NIF/Audio references (→ respective skills), efficient conflict analysis + CELL/WRLD + script health check workflows (→ `mod-dissection` skill), ESP binary format quick reference (derivable from UESP), and Nexus research guidance (already in `CLAUDE.md` and `mod-dissection`).

### Not changed

- MCP tool count, behavior, or interface — 29 tools, identical surface.
- Bridge binary — unchanged since v2.4.1.
- `~/.claude.json` auto-registration — same as v2.5.1+.

### Context budget impact

Across the six task-procedure skills and the KB_Tools shrink, approximately **745 lines of previously eager-loaded content** (525 across the deleted KB files + 220 from the KB_Tools reduction) now load on demand instead of every session. Actual per-session token savings depend on which skills the task triggers — sessions that don't touch ESP patching, BSA, NIF, audio, Papyrus, or the deep diagnostic procedures may save the entire 745 lines.

### Addon authors

Modlist-specific procedures can now live as skills in an addon's `.claude/skills/<name>/SKILL.md` alongside `CLAUDE_*.md` files — Claude Code auto-discovers them alongside the bundled set. Useful for modlist-specific patching workflows, list-specific diagnostic steps, or conventions unique to a particular overhaul.

### Migration

- **Requires Claude Code v2.1.73+.** Users on older CC can install the plugin but skills won't auto-load — task-specific procedures and category-specific tool references will silently fail to fire. Upgrade CC first.
- Upgrading over v2.5.3 leaves the old `kb/KB_Diagnostics.md`, `kb/KB_LeveledListPatching.md`, `kb/KB_ModDissection.md`, `kb/KB_NPCAnalysis.md`, `kb/KB_NPC_Outfits.md`, and `kb/KB_SessionStrategy.md` in `<MO2>/plugins/mo2_mcp/kb/` as orphans. Inno Setup only removes files it installed, not prior-layout stragglers. They're harmless (CLAUDE.md no longer routes to them) but can be manually deleted for tidiness. `kb/KB_Tools.md` is overwritten in place with the shrunken version — no orphan.
- For skills to be discovered, Claude Code's working directory must contain the `.claude/` folder — same scope constraint as CLAUDE.md and the kb/ files. Nothing new to configure.

---

## v2.5.3 — 2026-04-18

Cosmetic reorg: moved the 7 `KB_*.md` knowledge-base files into a `kb/` subdirectory. No functional or tool-behavior changes.

### Changed

- **`KB_*.md` location.** Moved from the repo root (and installer's flat `{app}\plugins\mo2_mcp\`) into a `kb/` subdirectory. Installer now writes KBs to `{app}\plugins\mo2_mcp\kb\`, alongside the existing `tools/` and `ordlookup/` subfolders. Repo root drops from 9 top-level `.md` files to 4 (README, LICENSE, THIRD_PARTY_NOTICES, KNOWN_ISSUES) plus CLAUDE.md + KNOWLEDGEBASE.md, which stay at root because Claude Code auto-loads CLAUDE.md and the routing index pairs naturally with it.
- **Cross-references updated.** `CLAUDE.md`, `KNOWLEDGEBASE.md`, `README.md`, `KNOWN_ISSUES.md`, `KB_Tools.md`, `KB_NPCAnalysis.md`, and `KB_NPC_Outfits.md` now point to `kb/KB_*.md` paths consistently (both routing instructions and cross-KB references).
- **`PLUGIN_VERSION`** bumped to `(2, 5, 3)`.

### Not changed

- MCP tool count, behavior, or interface — 29 tools, identical surface.
- Bridge binary — unchanged since v2.4.1.
- `~/.claude.json` auto-registration — same as v2.5.1+.

### Migration

Upgrading over an earlier install leaves the old flat `KB_*.md` files in `<MO2>\plugins\mo2_mcp\` as orphans — Inno Setup only removes files it installed, not previous-layout stragglers. They're harmless (CLAUDE.md + KNOWLEDGEBASE.md now route through `kb/` so the flat copies are never loaded), but can be manually deleted for tidiness. A clean reinstall (delete the plugin folder first, then run the installer) avoids this entirely.

---

## v2.5.2 — 2026-04-18

Hotfix: the plugin failed to load in Mod Organizer 2 with `ImportError: cannot import name '_find_spooky_cli' from 'mo2_mcp.tools_papyrus'`. No MCP server started, so no `mo2_*` tools appeared regardless of whether Claude Code was configured correctly. Both v2.5.0 and v2.5.1 shipped with this bug; v2.5.2 restores the helpers that three other modules depend on.

### Fixed

- **Restored `_find_spooky_cli` and `_invoke_cli` in `tools_papyrus.py`.** The v2.5.0 changelog claimed these were unused and removed them. In fact, `tools_archive.py`, `tools_nif.py`, and `tools_audio.py` all import them to invoke Spooky's CLI for BSA, NIF, and Audio operations. Their removal broke the cascading imports in `__init__.py`, and MO2 refused to load the plugin on startup. The helpers now live back in `tools_papyrus.py` with full docstrings describing their cross-module use, and the import surface is audited (all other intra-package imports are valid).

### Changed

- **`PLUGIN_VERSION`** bumped to `(2, 5, 2)`.

### Not changed

- All MCP tool behavior — no capability or interface changes.
- Bridge binary — unchanged since v2.4.1.
- `~/.claude.json` auto-registration from v2.5.1 — still in place, still correct.

### Migration

Install this over v2.5.0 or v2.5.1 and restart MO2. The plugin will now load and register the MCP server with Claude Code. If you already tested v2.5.1 and saw no `mo2_*` tools, that was this bug — the v2.5.1 fix for auto-registration was correct, but the plugin never got far enough to call it.

---

## v2.5.1 — 2026-04-18

Hotfix: auto-registration into Claude Code was writing to a path Claude Code does not read. Fresh installs on v2.5.0 appeared to "silently succeed" (server running, log showing config written) but Claude Code never discovered the server unless the user manually added a project-level `.mcp.json`.

### Fixed

- **`_ensure_claude_mcp_config` wrote to the wrong file.** The function was creating `~/.claude/.mcp.json`, which Claude Code does not read. Claude Code's user-scoped MCP servers live under the top-level `mcpServers` key of `~/.claude.json` (a single file containing all user settings). The function now merges the `mo2` server entry into that file, preserving every other key the user has. The write is atomic (temp file + `os.replace`) so a crash mid-write cannot corrupt `~/.claude.json`. If `~/.claude.json` does not exist (Claude Code not installed) the function returns silently, same as before. No-op on unchanged configs avoids pointless rewrites on every server restart.
- **`README.md` / `CLAUDE.md` path references** updated to `~/.claude.json` + `mcpServers.mo2` throughout (Quick Install, Quick Start, Troubleshooting; Connection section).

### Migration

Users who installed v2.5.0 will have a stale `~/.claude/.mcp.json` left behind. It is harmless (Claude Code never read it) and can be deleted. The first v2.5.1 server start will register the server into `~/.claude.json`; a one-time Claude Code restart picks up the new server.

### Changed

- **`PLUGIN_VERSION`** bumped to `(2, 5, 1)`.

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
