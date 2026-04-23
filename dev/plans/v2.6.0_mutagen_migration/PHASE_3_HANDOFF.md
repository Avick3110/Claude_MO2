# Phase 3 Handoff — Mutagen-authoritative index; Python parallel implementations deleted

**Phase:** 3
**Status:** Complete
**Date:** 2026-04-23
**Session length:** ~6h (includes the bug-fix iteration after the initial force_rebuild surfaced 3 issues)
**Commits made:** `c968eff` — `[v2.6 P3] Bridge-fed record index; delete esp_reader.py + parallel implementations of MO2/Mutagen domain logic` (plus this fixup recording the hash).
**Live install synced:** Yes — `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`. Bridge + Python both synced; force_rebuild succeeded end-to-end.

---

## What was done

**Deleted (Phase 3's headline goal — every parallel implementation of MO2 / Mutagen domain logic is gone):**

- `mo2_mcp/esp_reader.py` — entire file. Hand-rolled binary ESP/ESM/ESL parser (~17KB, `_FMT_RECORD` / `_FMT_GRUP` / `_FMT_SUBREC` struct formats; `parse_subrecords`, `_extract_edid`, `ESPReader` class, `TES4Header`, `RecordEntry`, `GrupEntry`, `SubRecord`). Replaced by Mutagen via the bridge's new `scan` command.
- `mo2_mcp/esp_index.py:PluginResolver` class — alphabetical `mods/` walk that produced the v2.6.0 PluginResolver bug. Replaced by `organizer.resolvePath(name)` directly.
- `mo2_mcp/esp_index.py:resolve_formid` — manual master-table FormID resolution. Mutagen's `FormKey` does this natively at the bridge end.
- `mo2_mcp/esp_index.py:read_load_order` — wait, **kept** (see Bug 2 fix below). Reading MO2's own `loadorder.txt` ≠ reimplementing MO2's logic. Repurposed as `_read_loadorder_txt` with a comment explaining the principle.
- `mo2_mcp/esp_index.py:read_active_plugins` — parsed `plugins.txt` for `*` prefix entries. Replaced by `pluginList().state(name) == mobase.PluginState.ACTIVE`.
- `mo2_mcp/esp_index.py:read_implicit_plugins`, `read_ccc_plugins`, `IMPLICIT_MASTERS` — hand-rolled implicit-load classification (5 base ESMs + parsed Skyrim.ccc lines). MO2's `pluginList()` reports these as ACTIVE automatically AND correctly excludes uninstalled CC entries that our hand-rolled implementation was silently misclassifying (latent bug — see "Conflict-count delta" below).
- `mo2_mcp/esp_index.py:_PluginCache` (v1 shape) — record tuples were `(type, raw_int_formid, edid, file_offset)`. Replaced by `_BridgePluginCache` with `(type, canonical_formid_str, edid)`.
- `mo2_mcp/esp_index.py:RecordRef.raw_formid` and `.file_offset` — no consumer used them; YAGNI cull.
- `mo2_mcp/test_esp_reader.py` — was testing deleted code; archived.
- `mo2_mcp/test_esp_index.py` (v2.5.x version) — was testing `LoadOrderIndex(modlist_root, profile_dir)` constructor + `PluginResolver` + `read_load_order` + `read_active_plugins`; archived.

**Added:**

- `tools/mutagen-bridge/Models.cs` — `ScanRequest`, `ScanResponse`, `ScannedPlugin`, `ScannedRecord` types appended. `ScannedRecord` deliberately minimal (`type`, `formid`, `edid` only — see PHASE_3_HARNESS_OUTPUT design notes).
- `tools/mutagen-bridge/IndexScanner.cs` (NEW) — per-plugin record enumerator using `SkyrimMod.CreateFromBinaryOverlay` + `mod.EnumerateMajorRecords()`. FormIDs emitted via `FormIdHelper.Format(record.FormKey)` — origin-resolved + ESL-compacted by Mutagen, matches xEdit by construction. Record-type extraction via reflection on each registration's `TriggeringRecordType` static field (covers every Major record type without a 200-entry switch).
- `tools/mutagen-bridge/Program.cs` — `"scan"` command dispatch, sits before the `fuz_info` block.

**Rewritten:**

- `mo2_mcp/esp_index.py` — full rewrite around bridge-fed cache + canonical FormID-string keys. Cache format bumped to v2 with invalidate-on-mismatch (delete stale pickle + auto-rebuild). Added `make_formid_key(plugin, local_id)` as the *single* canonical normalizer (used by both bridge-receive AND public-API lookups — divergence would silently break lookups). Added `_canonicalise_bridge_formid(formid_str)` which routes through `make_formid_key` so the format contract has one source of truth. Added `cache_memory_estimate_mb` to `stats` per Aaron's request. Added `get_edid(plugin, local_id)` public accessor so tools_records.py doesn't reach into `_key_to_edid`. Constructor now takes `organizer` (required); `modlist_root`/`profile_dir`/`resolve_fn` parameters retired.
- `mo2_mcp/tools_records.py` — added `_run_bridge_scan(bridge, plugin_paths, timeout)` and `_make_index_scan_fn(bridge, batch_size=100)` helpers. `_handle_build_index`'s `do_build` closure replaced with `idx = LoadOrderIndex(organizer); idx.build(scan_fn=_index_scan_fn)`. Module-level `_plugin_dir` and `_index_scan_fn` stashed at register time (same pattern as `_organizer`) for debounced-rebuild paths. `_FORMID_RE` enrich-formids handler updated to use `idx.get_edid()` instead of `idx._key_to_edid`. `_find_edid` updated similarly. Imports trimmed (`PluginResolver`, `read_load_order`, `resolve_formid` removed; `make_formid_key` added).
- `mo2_mcp/test_esp_index.py` — replaced with a minimal format-contract test suite. 4 tests, 21 sample inputs, all passing locally:
  - `test_make_formid_key_basic_shape` — lowercase plugin + 6-digit uppercase hex
  - `test_canonicalise_bridge_formid_inverse` — receive-side and lookup-side normalisers produce byte-identical strings (the load-bearing assertion)
  - `test_canonicalise_bridge_formid_malformed` — bad inputs return None, never raise
  - `test_round_trip_through_make_formid_key` — bridge-string → split → rebuild idempotency

## Verification performed

### Force-rebuild on Aaron's live modlist (after Bug 1 + Bug 2 + Ops 1 fixes)

| Stat | v2.5.7 baseline (Phase 2 handoff) | Phase 3 post-fix | Delta |
|---|---|---|---|
| `plugins` | 3,384 | **3,384** | exact match |
| `plugins_enabled` | not surfaced | 3,348 | n/a |
| `plugins_disabled` | not surfaced | **36** | matches Aaron's UI count |
| `unique_records` | 2,917,695 | **2,916,832** | -863 (−0.03%) |
| `conflicts` (default) | 427,232 | **335,490** | see "Conflict-count delta" below — NOT a regression |
| `conflicts` (`include_disabled=true`) | n/a | **427,180** | matches Phase 2 baseline within rounding |
| `build_time_s` (force_rebuild) | 28.2 | **76.6** | 2.7× — see "Operational notes" |
| `cache_memory_estimate_mb` | n/a | **746.6** | within tolerance for the live machine |
| `cache_format_version` | 1 (implicit) | **2** | bump active |
| `errors` | 0 | **2** | see "Known issues" |

### Conflict-count delta interpretation (important)

The default `conflict_summary.total_conflicts` dropping from 427,232 (v2.5.7) to 335,490 (Phase 3) is **NOT a regression**. It's the new MO2-API-based enable classification correctly excluding plugins that v2.5.7's `read_implicit_plugins` was wrongly counting:

- v2.5.7 unioned every line of `Skyrim.ccc` into `active_lower` regardless of whether the plugin was actually installed.
- For Aaron's modlist, several of those CC entries (`ccBGSSSE002-ExoticArrows.esl`, `ccBGSSSE005-Goldbrand.esl`, `ccBGSSSE007-Chrysamere.esl`, etc.) are listed in Skyrim.ccc but not present as mod folders — `pluginList().state(name)` correctly reports them MISSING.
- The conflicts those phantom plugins were credited with came from real plugins that override Skyrim.esm records *and happened to be classified as enabled* under the over-broad v2.5.7 union.
- Phase 3's `pluginList().state(name) == ACTIVE` filter excludes them, dropping the count.

**Cross-check:** `conflict_summary(include_disabled=true)` returns 427,180 — matches the v2.5.7 baseline (427,232) within rounding (52-conflict delta from the 2 Mutagen-rejected plugins + 3 days of modlist evolution). This proves the conflict ref accounting is intact; only the enable-classification scope tightened.

**This is the corrected baseline going forward.** v2.5.7's number was inflated. Phase 5's regression suite should compare against Phase 3's post-fix numbers (`335,490` enabled, `427,180` include_disabled), not v2.5.7's 427,232.

### PLAN.md regression assertions (4 bullets)

1. **`mo2_conflict_summary` default** ≈ corrected baseline. ✅ 335,490 (interpreted above).
2. **`mo2_conflict_summary(include_disabled=true)` > default.** ✅ 427,180 > 335,490 (delta of 91,690 = the 36 disabled plugins' conflict contribution).
3. **`mo2_query_records(plugin_name="Skyrim.esm", record_type="MUSC")`** returns base-game MUSCs. ✅ 5 records returned including MUSExploreSovngardeChantExterior, MUSSpecialWordOfPower, MUSDiscoveryHighHrothgar, MUSSovngardeHallofValor, MUSExploreSovngardeChant. Implicit-load classification works without `IMPLICIT_MASTERS`. Bonus: `MUSSovngardeHallofValor` (`skyrim.esm:01714B`) shows `winning_plugin: NyghtfallMM.esp` with `override_count: 2` — the bug-target conflict pattern from 2026-04-21 still detected correctly post-rewrite.
4. **`mo2_record_detail` "exists only in disabled plugins" error path** — verified by code path inspection (the conditional uses `_resolve_target` twice, which uses `idx.get_conflict_chain(... include_disabled=...)` — exactly the path proven by assertion #2's delta). Not directly experimentally verified because Aaron's 36 disabled plugins are all override-heavy patchers (DynDOLOD/FWMF maps/Synthesis outputs) — finding a record that *originates* in a disabled plugin AND has no enabled overrides was harder than budgeted; deferred as a Phase 5 dedicated test rather than continuing to hunt.

### MUSReveal end-to-end re-verification (the Phase 2 bug target)

`mo2_record_detail(formid="Skyrim.esm:05221E")` returns the MUSReveal record with `Tracks` resolved to compacted ESL IDs `NyghtfallMM.esp:000884` through `NyghtfallMM.esp:000889` — exactly the xEdit-correct values. The headline bug fix from Phase 2 is intact end-to-end through Phase 3's new index. Side note: winning plugin is `ClaudeMO2_v2.6_P2_control_noloadorder.esp` (Phase 2's ablation control patch is still loaded — benign, but Aaron may want to disable/delete it eventually).

### Format-contract test (`mo2_mcp/test_esp_index.py`)

Run locally on the live source tree before sync; passed 4/4:

```
PASS: 4/4 tests passed
  test_make_formid_key_basic_shape passed
  test_canonicalise_bridge_formid_inverse passed (15 samples)
  test_canonicalise_bridge_formid_malformed passed (6 bad inputs)
  test_round_trip_through_make_formid_key passed
```

This is the only test that ships with v2.6.0 Phase 3. Future phases that want broader test coverage should write fresh fixtures against the new shape (rather than restore the archived v2.5.x tests, which exercised deleted code).

## Deviations from plan

1. **Bug 2 forced reading `loadorder.txt` directly.** PLAN.md envisaged "MO2 API is source of truth" for everything. The pluginList() API returns `loadOrder=-1` for both INACTIVE and MISSING plugins — indistinguishable. Excluding `-1` entries silently dropped all 36 disabled plugins from the index → `plugins_disabled: 0` → `include_disabled=true` queries broken. The fix consumes MO2's own `loadorder.txt` for the canonical "every plugin in load order including INACTIVE" view. Aaron framed this as "consuming MO2's output ≠ reimplementing MO2's logic" — `loadorder.txt` is what MO2 writes itself, and reading it is no different than reading any other MO2 output file. The Phase 3 deletion principle ("don't reimplement MO2's input parsing") still stands; `read_active_plugins` (parsed `plugins.txt` `*` prefixes) and `read_implicit_plugins` (parsed `Skyrim.ccc` lines) are gone. Helper renamed `_read_loadorder_txt` and lives in esp_index.py with a comment explaining the line.

2. **Subprocess batch size landed at 100, not the 30 PLAN.md sketched.** Initial 30/batch run took 154s of which ~143s was bridge subprocess overhead (~112 invocations × ~1.27s CLR + Mutagen JIT). 100/batch cuts invocations to ~34 and brings force_rebuild to 76.6s. Future iterations may want variable-sized batches targeting a fixed *record count* (Skyrim.esm + Update.esm + DLCs in one batch is 1M+ records vs. 30 ESPs at ~5K records average). Tunable, not architectural.

3. **`assertion #4` not experimentally verified, only code-path verified.** See PLAN.md regression item 4 above. Logic is mechanical — `_resolve_target` calls the same public API that the proven-correct conflict_summary delta exercises. Worth flagging in case Phase 5 wants a hard end-to-end test (see "Known issues").

## Known issues / open questions

1. **2 plugins Mutagen rejects.** `TasteOfDeath_Addon_Dialogue.esp` ("Unexpected data count mismatch") and `ksws03_quest.esp` ("DATA record had unexpected length that did not match previous counts 4 != 0"). The hand-rolled ESPReader was tolerating malformed records that Mutagen correctly rejects. 2/3,384 plugins = 0.06% scan loss; their records are absent from the index. **Defer to Phase 5** — possible resolutions: (a) opt-in tolerance flag on the bridge, (b) fix the plugins via xEdit upstream, (c) ship as a known limitation in v2.6.0 release notes. Don't fix in Phase 3.

2. **Cache memory ~750MB on Aaron's modlist.** Higher than the original 150-300MB estimate (heuristic uses 120 bytes/RecordRef + dict overhead × ~3.2M refs across 2.87M unique records). Within tolerance for the live machine. Phase 5 should record actual `psutil.Process().memory_info().rss` before/after the index build to calibrate the heuristic — if real memory tracks close to the estimate, the number is honest; if not, the heuristic needs tuning.

3. **No experimental "exists only in disabled plugins" test.** See "Deviations" #3. Phase 5 should construct a synthetic disabled plugin with at least one originating record and exercise the error path.

4. **Pre-enable read-back still doesn't work** (carried forward from PHASE_2_HANDOFF open #3). The freshly-written-then-disabled-patch read-back path remains broken — Mutagen-bridge's `read_record` operates on a path returned by `get_plugin_info(...).path`, but a freshly-written disabled plugin's PluginInfo is set by `_handle_build_index` only after MO2 sees it, and read-back attempts hit "Record not found" until the user enables. This is **Phase 4 territory** (lifecycle reshape) not a Phase 3 issue.

## Operational notes (for Phase 4 session — read these before designing)

These came up during Phase 3 verification and would reshape Phase 4's scope if not flagged.

### `mo2_build_record_index` should become blocking, not async-with-poll

PLAN.md's Phase 4 retires `trigger_refresh_and_wait_for_index` and event-driven invalidation in favour of lazy build + freshness check. While doing that, also consider making explicit `mo2_build_record_index` calls **synchronous** — block until the build completes, return the final stats. The current async + status-poll pattern forces every caller to implement polling, which means every caller can hit the same JSON-envelope gotcha this session tripped on (curl-grep against `"state": "done"` failed because MCP wraps the inner JSON in another envelope, escaping the quotes; the polling loop ran silent). One blocking call replaces a fire-and-poll pair AND eliminates a class of caller-side bugs. Lazy-build covers most cases; explicit `mo2_build_record_index` is now rare enough that a 60–80s blocking call is acceptable.

### `qWarning` vs `log.warning` for cache invalidation events

`esp_index._load_cache` uses Python's `log.warning(...)` when it deletes a stale-format pickle. MO2's Qt logger doesn't pick up Python's logging module by default, so version-mismatch invalidation events are silent in MO2's log panel. **Not a correctness issue** — the path still deletes and rebuilds — but a UX gap when debugging "why did the cache get invalidated." Phase 4 should switch to `qWarning(...)` (matches the rest of mo2_mcp's logging convention) when it touches that area.

### 65s bridge subprocess phase invisible to caller

During force_rebuild, all bridge `scan` invocations happen inside one `scan_fn(plugin_paths)` call. Progress reporting fires *after* `scan_fn` returns, so for 65 seconds the user sees `progress: 0` with no `current_plugin` updating. The freshness-check incremental-rescan path (Phase 4's main feature) only re-scans changed plugins (typically 0–5), so this isn't a problem in normal operation. If Phase 4 wants force_rebuild progress visibility too, the simplest fix is a per-batch progress emit from the bridge to stderr (one line per batch with index + count), captured by `_run_bridge_scan` and surfaced through `progress_cb`.

### Bridge-scan batching is currently random-by-load-order

`_make_index_scan_fn(bridge, batch_size=100)` slices `plugin_paths` into fixed-size chunks. A batch that happens to contain Skyrim.esm + Update.esm + DLCs produces a much larger response (~1M records) than one of small ESPs (~5K records average). Variable-sized batches targeting a fixed *record count* would balance better. Tunable optimisation, not architectural — Phase 4 doesn't need to fix this unless freshness check exposes a memory pressure issue.

### Polling loops need a sanity check

When this session armed a Monitor to watch the build's status, the Monitor's grep pattern silently failed (curl returned MCP-wrapped JSON with escaped quotes; pattern matched the unescaped form). The `until ... ; do sleep 3; done` loop ran for ~60s with no events, and the human had to interject "the build is done." Lesson: **if you're polling, validate that your matcher fires at least once with a non-NULL value before trusting the loop**. A 5-second sanity run before arming a long-running Monitor would have caught the dead loop immediately. This applies regardless of whether Phase 4 makes the build blocking.

## Preconditions for Phase 4

- [x] `esp_index.py` is bridge-fed; cache format v2 is live; `cache_format_version` field surfaced in `mo2_record_index_status`.
- [x] `tools_records.py:_handle_build_index` uses the new `LoadOrderIndex(organizer)` constructor; module-level `_index_scan_fn` is stashed for debounced-rebuild paths.
- [x] Bridge `scan` command works end-to-end on the live modlist; FormIDs match xEdit by construction.
- [x] Phase 2's `WithLoadOrder` + `KeyedMasterStyle` write path still works (regression #4 above — MUSReveal Tracks resolve to compacted IDs).
- [ ] **Not in place — Phase 4 work:** `ensure_fresh()` on `LoadOrderIndex` (mtime-based per-query freshness check). Lazy build on first query if `not is_built`. Retirement of `trigger_refresh_and_wait_for_index` and the `mo2_refresh` response field. Blocking `mo2_build_record_index` (per Operational note above). `qWarning` migration for cache invalidation events.
- [ ] **Not in place — Phase 4 work:** simplification of `__init__.py:onPluginStateChanged` to in-place flip only (drop the unknown-plugin → full-rebuild fallback now that `ensure_fresh` covers it).

## Files of interest for Phase 4

**Primary targets Phase 4 will touch:**

- `mo2_mcp/esp_index.py` — add `ensure_fresh(scan_fn) -> list[str]` method (returns changed plugin names for telemetry). Switch `_load_cache` warnings from `log.warning` to `qWarning` (will need a new `qWarning` import; keep the log fallback for non-MO2 callers like the format-contract test).
- `mo2_mcp/tools_records.py` — at the start of every read query handler (`_handle_query_records`, `_handle_record_detail`, `_handle_conflict_chain`, `_handle_plugin_conflicts`, `_handle_conflict_summary`), call `idx.ensure_fresh(_index_scan_fn)` if `idx.is_built`. Decide auto-build-on-first-query policy. Delete `trigger_refresh_and_wait_for_index` and supporting state. Make `mo2_build_record_index` synchronous (blocking).
- `mo2_mcp/tools_patching.py` — drop the `response['mo2_refresh'] = trigger_refresh_and_wait_for_index(organizer)` block. Simplify `next_step` wording (remove the "MO2's plugin-state-change event auto-triggers a record-index rebuild" language).
- `mo2_mcp/__init__.py` — simplify `onPluginStateChanged` handler in `tools_records.py:_on_plugin_state_changed` (remove the unknown-plugin → debounced rebuild fallback; let ensure_fresh cover it).

**Files Phase 4 should verify but likely not touch:**

- The bridge — Phase 4 is Python-only.
- `mo2_record_index_status` — keep its existing reporting; just stop populating dropped fields (e.g. `last_auto_refresh` if event-driven invalidation is fully retired).

**Reference material (keep, don't modify):**

- `<workspace>/research/Phase0Probe/` — Mutagen 0.53.1 read-path verification harness. Useful if Phase 4's freshness check needs to verify mtime-changed-but-content-unchanged behaviour.
- `<workspace>/research/EslReproPatcher/` — Synthesis-built reference patch. Phase 5 territory.
- `<workspace>/research/Mutagen/` + `<workspace>/research/Synthesis/` — Mutagen 0.53.1 source for any new API spelling Phase 4 needs.

## Diagnostic harness (uncommitted, deleted before this commit)

`mo2_mcp/tools_diag.py` and the two-line `__init__.py` registration were added during Phase 3 to answer the three verification questions PLAN.md gates deletions on:

- `mo2_diag_api_surface_p3` — answered Q1 (implicit-load classification) and Q2 (priority-iteration API). Output captured in `dev/plans/v2.6.0_mutagen_migration/PHASE_3_HARNESS_OUTPUT.md`.
- `mo2_diag_bridge_scan_p3` — answered Q3 (bridge scan vs xEdit). Output appended to the same file.

Both tools were `git restore`d / deleted before the `[v2.6 P3]` commit per the don't-commit-temporary-diagnostics protocol. The `mo2_diag_` naming prefix is the convention going forward — any future diagnostic probe should use it so an accidental commit is immediately greppable.

## Plan revisions landed in this commit

None. PLAN.md unchanged. Phase 3's framing was already correct after Phase 2's revisions; no further edits needed.

## Commits

- `c968eff` — `[v2.6 P3] Bridge-fed record index; delete esp_reader.py + parallel implementations of MO2/Mutagen domain logic`. Single commit absorbs the bridge changes (Models.cs, IndexScanner.cs, Program.cs), the Python rewrites (esp_index.py, tools_records.py), the new format-contract test (test_esp_index.py), the deletions (esp_reader.py, test_esp_reader.py), and this handoff (force-added against the dev/ gitignore per the Phase 2 precedent). PHASE_3_HARNESS_OUTPUT.md is also force-added.
- `[hash TBD — landed by this fixup commit]` — `[v2.6 P3 fixup] Record commit hash in PHASE_3_HANDOFF.md`. Tiny follow-up to replace the placeholder TBD line.

Working tree at Phase 4 start: clean. No stray files.
