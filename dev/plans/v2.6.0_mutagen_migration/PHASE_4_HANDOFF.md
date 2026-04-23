# Phase 4 Handoff — Lazy build + freshness check; event-driven invalidation retired; pre-enable read-back works

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-23
**Session length:** ~5h (includes the first-gate failure + pluginList-reconciliation diagnosis)
**Commits made:**
- `ef64d25` — `[v2.6 P4a] Lazy build + mtime freshness check; retire event-driven invalidation`
- `003d17b` — `[v2.6 P4b] Make mo2_build_record_index blocking; remove polling protocol`
- `aea9580` — `[v2.6 P4c] Route esp_index operational warnings through qWarning`
- `1c9960a` — `[v2.6 P4d] Pre-enable read-back: wait for MO2 refresh in mo2_create_patch`

**Live install synced:** Yes — `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`. All four commits' changes present; full gate (4d) run end-to-end against the synced install.

---

## What was done

### P4a — lazy build + freshness check + event-driven machinery retired

**Added:**

- `LoadOrderIndex.ensure_fresh(scan_fn) -> list[str]` (esp_index.py). Three fast/slow paths:
  - **No drift** (common case, ~50–100ms stat walk on 3384 plugins): returns `[]`.
  - **Enable-only drift** (pluginList state diverges from cached `PluginInfo.enabled`): in-place bulk bit flip, no merged-index rebuild. Returns `[]`.
  - **Structural drift** (file mtime, plugin added/removed, load order shifted): bridge-rescan the affected plugins, rebuild merged index, persist updated pickle. Returns list of rescanned plugin names.
- `_build_index_sync(force: bool) -> dict` helper (tools_records.py) factored out of the previous threaded `do_build` closure. Shared by the explicit `mo2_build_record_index` path (P4b makes it blocking) and the auto-build-on-first-query path.
- `_ensure_index_ready() -> tuple[LoadOrderIndex | None, str | None]` helper (tools_records.py). Every read handler's first line. Auto-builds synchronously on first query after server start; calls `ensure_fresh` on every subsequent query. Returns `(idx, err)` envelope for the handler to JSON-encode on failure.

**Retired from `tools_records.py`:**

- `_build_complete` event + `_get_index_fresh` poll path
- Timeout constants `_BUILD_WAIT_TIMEOUT_S` / `_WRITE_REFRESH_TIMEOUT_S` / `_STATE_FLUSH_DELAY_S`
- `_rebuild_timer` + `_rebuild_timer_lock`
- `_rebuild_pending_during_build` flag
- `_on_mo2_event` (covered onRefreshed + onModMoved paths)
- `_schedule_debounced_rebuild` / `_fire_debounced_rebuild`
- `_register_event_hooks` (renamed, see below)
- `trigger_refresh_and_wait_for_index` (helper for write tools)

**`_on_plugin_state_changed` simplified** to pure in-place flip. The unknown-plugin fallback rebuild is gone — `ensure_fresh` picks up new plugins on the next query via its pluginList reconciliation. `_register_event_hooks` renamed to `_register_plugin_state_hook` (only onPluginStateChanged registered; onRefreshed and onModMoved retired). P4d re-registers onRefreshed for a narrow signal-only purpose and re-renames to `_register_hooks`.

**Write-tool `mo2_refresh` field + `trigger_refresh_and_wait_for_index` import removed** from `tools_patching.py`, `tools_write.py`, `tools_archive.py`, `tools_audio.py`, `tools_papyrus.py`. Replaced with inline `organizer.refresh(save_changes=True)` fire-and-forget for the asset-write tools. (`mo2_create_patch` gets a different refresh pattern in P4d.) `next_step` wording updated to reference `ensure_fresh` in place of the v2.5.x "plugin-state-change event auto-triggers rebuild" language; "tick checkbox + wait for user before read-back" instruction stayed in P4a until 4d dropped it.

### P4a scope addition — pluginList reconciliation

**Discovered during gate verification.** Initial gate attempt (build index → write patch → `mo2_record_detail` with `include_disabled=true`) failed. MO2's log timeline:

```
10:29:57.760  bridge wrote ClaudeMO2_v2.6_P4a_gate.esp
10:29:59.288  MO2 refreshing structure     (organizer.refresh() ran)
10:30:05.238  refresher saw 713592 files   (up from 713591)
10:30:13.320  TESData::PluginList::refresh() 8074 ms
10:30:15.568  onDirectoryRefreshed() 10322 ms  (refresh complete — in-memory)
10:30:21.600  ensure_fresh rescanned 1 plugin(s): ['Northern Roads...']
              (NOT the gate patch)
```

MO2 had the patch in pluginList in-memory by `10:30:15`, but `loadorder.txt` on disk hadn't been flushed (MO2 only writes `loadorder.txt` on specific events — profile switch, shutdown). My `ensure_fresh` intersected the stale `loadorder.txt` with `pluginList().pluginNames()`, and the new patch — present only in pluginList — got dropped by the intersection filter.

**Fix (folded into the P4a commit):** in both `ensure_fresh` and `build()`, after reading loadorder.txt, append any plugins present in pluginList but absent from the on-disk loadorder.txt to the end of `current_load_order`. Matches MO2's "new plugins append" convention for load-order position. Both paths share identical reconciliation logic.

After the fix, `mo2_build_record_index` bumped `plugins` from 3384 → 3385 and `plugins_disabled` from 36 → 37 (picked up the gate patch via pluginList reconciliation at `scanned=1, cached_hits=3384`). Gate then passed — see Verification below.

### P4b — blocking `mo2_build_record_index`

`_handle_build_index` now calls `_build_index_sync()` directly on the handler thread and returns the populated `_build_status` dict when done. Dropped the `threading.Thread` wrapper and the `threading` import (P4d re-imports it for `_refresh_event`).

Tool description updated (user-visible): call is blocking; force_rebuild can exceed Claude Code's 60s MCP tool timeout (default per https://code.claude.com/docs/en/mcp); server-side build completes regardless and `mo2_record_index_status` will confirm; set `MCP_TIMEOUT=120000` before launching Claude Code for routine force_rebuild use.

### P4c — `qWarning` shim for `esp_index.py` operational warnings

Six `log.warning` call sites — five in `_load_cache` (cache corruption / format-version mismatch / unexpected top-level type) and one in `save_cache` (disk I/O error), plus one added in P4a's `ensure_fresh` (scan_fn failure) — were all invisible in MO2's Qt log panel because MO2 doesn't capture Python's `logging` module. Now routed through a module-level `_warn()` shim that uses `qWarning` when PyQt6 is importable and falls back to stdlib `logging` for `test_esp_index.py` and any other organizer-less consumer.

Grep confirmed no other `log.warning` / `log.error` / `log.critical` call sites anywhere else in `mo2_mcp/`; every other module already uses `qInfo` / `qWarning` directly. Future modules that add stdlib logging should follow the same shim pattern.

### P4d — pre-enable read-back via onRefreshed signal wait

Fixes the v2.5.x-carried "freshly-written patch returns empty on read-back until user enables" bug (Phase 2 open #3 / Phase 3 open #4).

**Root cause** (confirmed via the P4a gate log): `organizer.refresh(save_changes=True)` is async. MO2's pluginList rebuild takes ~10–15s after the `refresh()` call returns. The P4a inline fire-and-forget refresh in `mo2_create_patch` didn't wait for that, so the immediate-next read-back query ran `ensure_fresh` against a pluginList that hadn't yet picked up the new plugin.

**Fix:**

- Re-registered MO2's `onRefreshed` hook (retired in P4a from its rebuild-triggering role) as a pure signal setter. `_register_plugin_state_hook` → `_register_hooks`; now registers both `onPluginStateChanged` (plugin-state fast path) and `onRefreshed` (write-path signal).
- Module-level `_refresh_event: threading.Event`, initially `set()` so a stray `wait()` at boot doesn't block.
- `_refresh_and_wait(organizer, timeout_s=30) -> (completed, elapsed_ms)` helper in `tools_records.py`. Strict ordering `_refresh_event.clear()` → `organizer.refresh()` → `wait()` is load-bearing and documented inline (if another refresh completed from a warm cache before the clear, a subsequent wait would return instantly with a stale signal).
- `mo2_create_patch` calls the helper before returning. Response now carries `refresh_status: 'complete' | 'timeout'` and `refresh_elapsed_ms`.
- Timeout semantics are **best-effort** per Aaron's refinement #1: the tool call still succeeds on timeout. Patch is already on disk; MO2 finishes refreshing asynchronously. On timeout, `next_step` tells the user to press F5 to force the refresh.

**Log signal** (Aaron's refinement #3): `qInfo` on success (`"MO2 directory refresh completed in {elapsed_ms}ms"`), `qWarning` on timeout (`"MO2 directory refresh did not signal within {timeout}s"`). Visible in MO2's log panel for forensic "why didn't my read-back work" investigations.

**Concurrent-refresh risk** (user F5 or other refresh racing between our `clear()` and our `wait()` could fire onRefreshed from the other refresh, unblocking our wait early): documented as acceptable per Aaron. Probability low; consequence bounded — the read-back that follows would hit the same "pluginList not yet reflected" state we'd hit on timeout, which `ensure_fresh` handles on the next query anyway.

**`next_step` wording updated on success**: the "wait for user confirmation before chaining read-back" instruction is gone — read-back works immediately. "Tick the checkbox when ready to load in-game" stays. On timeout, the instruction is "press F5 in MO2 to force the refresh."

## Verification performed

### P4a gate (re-run after pluginList-reconciliation fix)

Build on Aaron's live modlist (AL Custom profile) picked up the pre-existing `ClaudeMO2_v2.6_P4a_gate.esp` via pluginList reconciliation:

| Stat | Before gate | After gate |
|---|---|---|
| `plugins` | 3384 | **3385** (+1) |
| `plugins_disabled` | 36 | **37** (+1) |
| `scanned` | 0 | **1** (gate patch) |
| `cached_hits` | 3384 | 3384 |

`mo2_record_detail(formid="Skyrim.esm:012EB7", plugin_name="ClaudeMO2_v2.6_P4a_gate.esp", include_disabled=true)` resolved the record through the new patch at `load_order: 3386` with `BasicStats.Value: 10` intact and all FormLinks resolved (no "not found" results). Mechanism verified via the `build()` reconciliation path.

### P4b smoke test

Single `mo2_build_record_index` call blocked for 8.6s on a cache-hit reload and returned the full `_build_status` dict directly (no `{"status": "building"}` → poll cycle). Response included `state: "done"`, `built: true`, `cached_hits: 3384`, `scanned: 0`, and the complete merged stats. Confirmed blocking semantics work cleanly.

Force-rebuild timing not separately exercised in the gate — Phase 3's 76.6s baseline stands. The 60s MCP timeout is documented; recovery path via `mo2_record_index_status` is known.

### P4c spot-check

Module loads clean on the live server (ping + build succeed post-restart), so the `try: from PyQt6.QtCore import qWarning` branch of `_warn`'s import shim is taking effect — the stdlib fallback path would only trigger for organizer-less callers like `test_esp_index.py`, which passes 4/4 on the dev shell without PyQt6 installed. Full in-situ trigger of a `_warn` call (e.g. cache-version mismatch) is rare and deferred to natural occurrence.

### P4d gate — pre-enable read-back

Full sequence on Aaron's live modlist:

1. `mo2_build_record_index` → `build_time_s: 8.35` (cache-hit blocking).
2. `mo2_create_patch` writing `ClaudeMO2_v2.6_P4d_gate.esp` (Iron Sword, `Value=10`) → `refresh_status: "complete"`, `refresh_elapsed_ms: 15156`, `next_step` drops wait-for-user language. **onRefreshed signal fired and was caught.**
3. `mo2_record_detail(formid="Skyrim.esm:012EB7", plugin_name="ClaudeMO2_v2.6_P4d_gate.esp")` — WITHOUT `include_disabled` — returned `{"error": "Record exists only in disabled plugins...", "winning_disabled_plugin": "ClaudeMO2_v2.6_P4d_gate.esp"}`. Correct error shape — patch IS in the index chain, filtered out by the default disabled filter. **`ensure_fresh` successfully picked up the newly-written plugin.**
4. `mo2_record_detail(..., include_disabled=true)` — returned full field resolution through the patch at `load_order: 3386` with `BasicStats.Value: 10` and all inherited Requiem fields intact.

Pre-enable read-back works end-to-end.

Both gate patches (`ClaudeMO2_v2.6_P4a_gate.esp`, `ClaudeMO2_v2.6_P4d_gate.esp`) cleaned up from disk.

## Deviations from plan

1. **pluginList reconciliation in `ensure_fresh` and `build()`.** Not in PLAN.md's Phase 4 sketch; folded in during P4a gate verification. See "P4a scope addition" above. Both paths share identical reconciliation logic — when `loadorder.txt` on disk is stale relative to MO2's in-memory `pluginList()`, plugins present only in pluginList get appended at end. Without this, freshly-written plugins are silently dropped by both build and freshness-check paths. Landed in the same P4a commit as the rest of the phase-4a work.

2. **PLAN.md Phase 4 described `organizer.refresh()` alone as sufficient for write-path ensure_fresh to pick up the new plugin.** Gate evidence showed it isn't — MO2's in-memory pluginList takes ~10–15s to rebuild after `refresh()` returns. P4d added `_refresh_and_wait` which signals on `onRefreshed` and blocks until MO2's refresh lands. This required re-registering the `onRefreshed` hook that P4a had retired from its rebuild-triggering role — the hook is now narrowly scoped to signal-only, does not trigger any rebuild or invalidation, and is clearly separated from `_on_plugin_state_changed`. The P4d commit explicitly calls out that the "wait" Aaron originally wanted dropped was the record-index rebuild wait (via `_build_complete`), not MO2's own refresh-complete wait, which is a different pipeline stage.

3. **`threading` import removed in P4b, restored in P4d.** P4b dropped the threading import alongside the `threading.Thread` wrapper. P4d re-added it for `threading.Event` (`_refresh_event`). Not a reversion — the two uses are different.

4. **Force-rebuild timing not exercised separately from P4a's cache-hit baseline.** Phase 3's 76.6s force-rebuild baseline stands. Exercising it live would cost Aaron a 76s stall during a session, and the result would be redundant — blocking force_rebuild correctness is the same code path as blocking cache-hit build. The 60s MCP-timeout caveat is documented in the tool description and the commit body; recovery via `mo2_record_index_status` is known. Can be explicitly re-verified in Phase 5 if desired.

## Known issues / open questions

1. **MCP client timeout on force_rebuild.** Claude Code's default MCP tool timeout is 60s; force_rebuild on Aaron's modlist is ~76s. Server-side build completes regardless; client sees a timeout error. Recovery: call `mo2_record_index_status`, read `state == 'done'`. Preempt: set `MCP_TIMEOUT=120000` before launching Claude Code. Documented in the P4b tool description; no code change needed unless GitHub Issue #424 lands per-call timeout configuration. Phase 5 can decide whether to document this more prominently (README?).

2. **Concurrent-refresh race in `_refresh_and_wait`.** A user F5 or other auto-refresh firing between our `_refresh_event.clear()` and our `wait()` could unblock the wait early with a stale signal. Probability low (user F5 during a Claude write is rare), consequence bounded (downstream read-back would hit the same "pluginList not yet reflected" state we'd see on timeout, and `ensure_fresh` handles it on the next query). Documented as acceptable per Aaron; don't solve unless it actually bites.

3. **Save-cache I/O cost on `ensure_fresh` structural changes.** Every structural-change branch in `ensure_fresh` calls `save_cache()`, which rewrites the ~150 MB pickle. Not measured in Phase 4 gates (the P4d gate rewrote it once during the bridge-rescan merge). Expected to be a few seconds; fine for normal drift (post-write), could accumulate if mid-session patch writes are common. If Phase 5 or a later phase sees noticeable query latency after writes, consider conditional/batched save (e.g. save only when N plugins added/removed, or mark dirty and defer to background). Leaving as-is for now — correct behavior, acceptable latency.

4. **2 plugins Mutagen rejects.** `TasteOfDeath_Addon_Dialogue.esp` ("Unexpected data count mismatch") and `ksws03_quest.esp` ("DATA record had unexpected length that did not match previous counts 4 != 0"). Unchanged from Phase 3 baseline; their records are absent from the index. Phase 5 decision: opt-in bridge tolerance flag, fix plugins via xEdit upstream, or ship as a known limitation in v2.6.0 release notes. Don't fix in Phase 4.

5. **`ensure_fresh` enable-only fast path not directly live-exercised in Phase 4 gates.** The P4d gate exercised the full structural-change path (unknown plugin → rescan). Enable-only path (pluginList state diverges from cached `PluginInfo.enabled` without any file-mtime / load-order changes) is algorithmically straightforward and shares `set_plugin_enabled` semantics with the battle-tested `onPluginStateChanged` handler, so confidence is high. If a Phase 5 test shows a divergence between a live checkbox toggle and a subsequent query's enabled-filter result, investigate this path first.

6. **Orphan `tools_diag.py` cleaned from live install, unchanged otherwise.** Phase 3's diagnostic harness was `git restore`d before its commit but left behind on the live install. I deleted it from live during Phase 4 prep per Aaron's approval. No repo change — the file was never committed.

## Preconditions for Phase 5

- [x] P4a: `ensure_fresh()` + `_ensure_index_ready()` + auto-build-on-first-query; event-driven machinery retired except onPluginStateChanged fast path.
- [x] P4a: pluginList reconciliation in `ensure_fresh` + `build()` (freshly-written plugins in pluginList-memory but not in on-disk loadorder.txt are included).
- [x] P4b: `mo2_build_record_index` blocking; returns full `_build_status` dict; polling protocol gone.
- [x] P4c: `_warn` shim routes esp_index operational warnings through `qWarning` when MO2 is present.
- [x] P4d: `_refresh_and_wait` helper + onRefreshed signal hook; `mo2_create_patch` pre-enable read-back works end-to-end; `next_step` wording reflects that.
- [x] All four Phase 4 commits landed on `main` locally (not yet pushed to GitHub — v2.6.0 publishes in P6).
- [x] Live install synced with Phase 4 changes + `__pycache__` cleared; MO2 restarted.
- [x] P4a + P4d gate patches deleted from live install; no test artifacts remain in the modlist.

## Files of interest for Phase 5

**Primary — `ensure_fresh` / build reconciliation behavior, covered by T5 / T6 / T7 in the plan:**

- `mo2_mcp/esp_index.py` — `ensure_fresh`, `build`, the `_warn` shim, pluginList reconciliation in both paths.
- `mo2_mcp/tools_records.py` — `_ensure_index_ready`, `_build_index_sync`, `_handle_build_index` (blocking), `_refresh_and_wait`, `_register_hooks`, `_on_plugin_state_changed` (simplified).
- `mo2_mcp/tools_patching.py` — `mo2_create_patch` response shape (`refresh_status`, `refresh_elapsed_ms`, new `next_step` wording).

**Secondary — simpler write-tool refresh paths (fire-and-forget, unchanged semantics from v2.5.x other than the wait being removed):**

- `mo2_mcp/tools_write.py`, `tools_archive.py`, `tools_audio.py`, `tools_papyrus.py`.

**Reference — Phase 0 / 2 / 3 research artifacts (keep for now, archivable after Phase 5 passes):**

- `<workspace>/research/EslReproPatcher/` — Synthesis-built reference patch, useful for T1 if the original ESL FormID bug ever resurfaces.
- `<workspace>/research/Phase0Probe/` — Mutagen 0.53.1 read-path verification.
- `<workspace>/research/Mutagen/` and `<workspace>/research/Synthesis/` — Mutagen source trees.

---

## Phase 5 hand-off note — test matrix deltas from PLAN.md

PLAN.md's Phase 5 test matrix (T1–T9) stands with two small post-P4 annotations:

- **T6 (lazy build + freshness check)** — can now include a "no explicit `mo2_build_record_index` call" sub-step: start session, call a read query directly, confirm auto-build-on-first-query fires with a `qInfo` line visible in MO2's log panel.
- **T7 (patch workflow with new `next_step`)** — add: confirm `refresh_status: "complete"` and `refresh_elapsed_ms` are present in the `mo2_create_patch` response; confirm the `mo2_refresh` field is absent (removed in P4a). On slow modlists (>30s refresh), confirm `refresh_status: "timeout"` path surfaces the F5 instruction.

No other changes needed to the Phase 5 plan.
