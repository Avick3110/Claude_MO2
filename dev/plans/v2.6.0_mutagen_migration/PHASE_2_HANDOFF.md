# Phase 2 Handoff — Path-resolution root cause + Mutagen migration

**Phase:** 2
**Status:** Complete
**Date:** 2026-04-22
**Session length:** ~5h (includes diagnostic excursion that reframed the bug)
**Commits made:** see "Commits" at the bottom (single `[v2.6 P2]` commit, hash filled in post-commit).
**Live install synced:** Yes — `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`. Smoke-tested end-to-end after the final bridge + Python sync.

---

## Discovery — what Phase 2 actually fixed, and why the plan's framing needed updating

Phase 2 started out believing the 2026-04-21 broken-FormLink bug was a Mutagen write-path correctness problem. Phase 0 had already established that Mutagen 0.53.1's raw `CreateFromBinaryOverlay` returns correctly-compacted `FormKey.ID` values for ESL plugins. The plan's Phase 2 scope was "add `LoadOrderContext`, route writes through `BeginWrite.WithLoadOrder` so FormLinks to ESL-flagged masters encode correctly".

That Mutagen work landed first and produced… exactly the same broken-FormLink symptom the bug report described. Tracks in the output ESP rendered in xEdit as `[FE000E55]..[FE000E5A] <Error: Could not be resolved>` — indistinguishable from v2.5.x output.

The root cause turned out to be an unrelated Python bug, which the Mutagen plumbing was faithfully amplifying:

**`PluginResolver._build` in `esp_index.py` walked `<modlist>/mods/` in lexical `iterdir` order, letting later folders overwrite earlier ones in its name→path map.**

For `NyghtfallMM.esp`, the modlist has two mods that both contain that filename:

- `mods/Nyghtfall - ESPFE (Replacer)/NyghtfallMM.esp` — the ESL-flagged variant MO2's VFS actually exposes to the game and xEdit.
- `mods/Replacer - Nyghtfall - Music/NyghtfallMM.esp` — a non-ESL legacy variant, lower priority in MO2's left pane but alphabetically later.

Because 'R' sorts after 'N', the Music variant clobbered the ESPFE variant in `PluginResolver`'s map. Every Python tool that looked up `NyghtfallMM.esp` got pointed at the wrong file. The index recorded `providing_mod: "Nyghtfall - ESPFE (Replacer)"` (reading that from MO2's real API) while simultaneously recording the disk path from the wrong folder. Those two fields had disagreed for months without being noticed.

Mutagen's write path then did exactly what it was told: open the disk file at the recorded path, read its Tracks (which are stored at raw `0x002E55..0x002E5A` in the non-ESL Music variant), copy them as override, write them out. xEdit read the output against the live modlist — which runs the ESPFE variant with NYReveal records at `0x000884..0x000889` — and correctly reported that the patch's FormLinks don't resolve against anything actually loaded.

**This was not a Mutagen bug.** It was a parallel-implementation bug: `PluginResolver` re-invented MO2's VFS priority resolution using an alphabetical walk, and the two disagreed on which plugin wins for duplicated filenames. MO2's `organizer.resolvePath(name)` does the right thing natively.

### Architectural lesson for Phase 3 (worth reading before starting)

Every place `esp_reader.py` / `esp_index.py` re-implements something MO2 or Mutagen already owns is a candidate for the same class of silent-drift bug:

- Raw `<I` FormID byte reads → re-implement Mutagen's `FormKey` parsing.
- Hand-rolled `IMPLICIT_MASTERS` set + `read_ccc_plugins` → re-implement MO2's implicit-plugin classification.
- `read_active_plugins` from `plugins.txt` → re-implement `plugin_list.state(name)`.
- `PluginResolver` walk of `mods/` → re-implement MO2's VFS priority (the one that just bit us).

Phase 2 fixes `PluginResolver` specifically as a defence-in-depth stop-gap — an `organizer.resolvePath`-backed `resolve_fn` injection into `LoadOrderIndex`. Phase 3's framing (already updated in `PLAN.md`) is to delete the parallel implementations entirely, not add more.

---

## What was done

**Bridge (`tools/mutagen-bridge/`):**

- `Models.cs` — added `LoadOrderContext` + `LoadOrderListingEntry` types and embedded nullable `LoadOrderContext? LoadOrder` on `PatchRequest`, `ReadRequest`, `ReadBatchRequest`. Patch rejects the request when absent; reads fall through to the existing `CreateFromBinaryOverlay`-per-plugin path (Phase 3 forward-compat).
- `LoadOrderContextResolver.cs` (new) — `BuildMasterStyledListings(ctx)` walks `ctx.Listings`, calls `KeyedMasterStyle.FromPath(new ModPath(modKey, path), release)` per listing, and returns the `IModMasterStyledGetter[]` array. Chose this lightweight path over the full `GameEnvironment.Typical.Builder` because (a) Phase 0 established raw overlay reads return already-compacted FormKeys, (b) MO2 plugins live in separate per-mod folders with no unified data directory for `Builder.WithTargetDataFolder` to enumerate, and (c) `KeyedMasterStyle.FromPath` reads just the plugin header — cheap enough to run per-plugin across a 3000+ load order.
- `PatchEngine.cs` rewrite of the write path:
  - `Process(PatchRequest)` rejects the request when `LoadOrder` is absent with an explicit error message. No silent fallback — patch writes genuinely need the context for ESL correctness even when the immediate records don't reference ESL masters.
  - `patchMod.ModHeader.Flags |= (SkyrimModHeader.HeaderFlag)0x200` replaced with the named constant `SkyrimModHeader.HeaderFlag.Small`.
  - `patchMod.WriteToBinary(path)` replaced with `patchMod.BeginWrite.ToPath(path).WithLoadOrder(masterStyled).Write()`.
  - `AddMasterIfMissing` call sites in `ProcessOverride`, `ProcessMergeLeveledList`, and the three `MergeLeveled*` methods — deleted. `BeginWrite.WithLoadOrder` recomputes master references at write time from actual referenced FormKeys, making the pre-seeding redundant.
  - The `AddMasterIfMissing` method itself — deleted.
  - Masters list in the response is still read back via a second `CreateFromBinaryOverlay` of the written file (not from `patchMod.ModHeader.MasterReferences` — `BeginWrite` mutates its internal write-pipeline state but does NOT propagate the computed masters back to the in-memory mod header, so reading from `patchMod` returns empty). This preserved v2.5.x response shape.

**Python (`mo2_mcp/`):**

- `esp_index.py` — `LoadOrderIndex.__init__` gained an optional `resolve_fn` parameter (`Callable[[str], str | Path | None]`). When supplied, `build()` uses it in place of the legacy `PluginResolver.resolve` walk. The v2.5.x `PluginResolver` class stays as a fallback for callers without an organizer (tests, organizer-less diagnostics).
- `tools_records.py`:
  - `register_record_tools` stashes `organizer` at module scope (`_organizer`) so event-driven rebuild callbacks that run from timer threads without a captured closure can still use MO2's VFS resolver.
  - `_handle_build_index` takes an optional `organizer` parameter, falls back to the module-level stash, and builds a `_resolve_via_mo2` closure around `organizer.resolvePath(name)` to pass into `LoadOrderIndex`.
  - New helper `build_bridge_load_order_context(organizer, idx, game_release='SkyrimSE')` — iterates `idx._load_order`, looks up each plugin's disk path from the (now-correct) `PluginInfo.path`, and emits the JSON context shape the bridge expects. Populates optional `data_folder` / `ccc_path` fields for Phase 3 forward-compat; Phase 2's bridge doesn't consume them.
- `tools_patching.py`:
  - Imports `build_bridge_load_order_context` from `tools_records`.
  - `_handle_create_patch` injects `load_order` into the bridge request dict before `subprocess.run`.

**Plan document (`dev/plans/v2.6.0_mutagen_migration/PLAN.md`):** four revisions landed in this commit — see "Plan revisions" section below.

## Verification performed

### Smoke tests (passed)

**Smoke 1 — non-ESL regression:** `mo2_create_patch` override on `Skyrim.esm:012E49` (ArmorIronCuirass / REQ_Heavy_Iron_Body) with explicit `source_plugin: "Authoria - Armor Reqtificated.esp"`, adding keyword `Skyrim.esm:06BBE6`. Response: `success: true`, `records_written: 1`, `masters: ["Skyrim.esm", "Requiem.esp"]`, `keywords_added: 1`. xEdit opens the output cleanly, no FormLink warnings, keyword visible in KWDA.

**Smoke 4 — ESL MUSReveal (the headline bug-fix proof):** `mo2_create_patch` override on `Skyrim.esm:05221E` (MUSReveal) with explicit `source_plugin: "NyghtfallMM.esp"`, no modifications. Response: `success: true`, `source: "NyghtfallMM.esp"`, `masters: ["Skyrim.esm", "NyghtfallMM.esp"]`. xEdit verification (screenshot archived in session transcript):

| Tracks[i] | Rendered | Resolves to |
|---|---|---|
| 0 | `NYReveal01 [MUST:FE000884]` | ✓ NyghtfallMM's NYReveal01 |
| 1 | `NYReveal02 [MUST:FE000885]` | ✓ NyghtfallMM's NYReveal02 |
| 2 | `NYReveal03 [MUST:FE000886]` | ✓ NyghtfallMM's NYReveal03 |
| 3 | `NYReveal04 [MUST:FE000887]` | ✓ NyghtfallMM's NYReveal04 |
| 4 | `NYReveal05 [MUST:FE000888]` | ✓ NyghtfallMM's NYReveal05 |
| 5 | `NYReveal06 [MUST:FE000889]` | ✓ NyghtfallMM's NYReveal06 |

Rendered identically to NyghtfallMM's own MUSReveal column — the xEdit-compatible compacted slot IDs the 2026-04-21 bug report named as the correct values. **Zero `<Error: Could not be resolved>` entries.**

The pre-fix smokes 2 and 3 (same test record, bridge before the `PluginResolver` fix) showed all 6 Tracks as `<Error: Could not be resolved>` at `[FE000E55..FE000E5A]`. That output is the bug-repro baseline; smoke 4's clean output is the proof of fix.

Index rebuild (`force_rebuild=true`) after the Python fix: 28.2s, `scanned=3384 / cached_hits=0`. Stats shifted slightly vs v2.5.7 baseline — `unique_records` 2,918,559 → 2,917,695 (-864) and `conflicts` 428,260 → 427,232 (-1,028). That delta is the signature of correctly-resolved plugin paths reading different file contents than the old alphabetical walk produced.

### Control test — `WithLoadOrder` ablation

To answer "is the Mutagen migration load-bearing for the v2.6.0 bug, or defensive?" — with `PluginResolver` fix in place, I replaced `BeginWrite.ToPath(...).WithLoadOrder(masterStyled).Write()` with `BeginWrite.ToPath(...).WithNoLoadOrder().Write()`, synced the bridge only (Python unchanged), and re-ran the same smoke-4 test as `ClaudeMO2_v2.6_P2_control_noloadorder.esp`.

Result: xEdit shows all 6 Tracks resolving cleanly to `NYReveal01..06 [MUST:FE000884..FE000889]` — identical to the `WithLoadOrder` output.

**Interpretation:** `WithLoadOrder` is defensive for the specific MUSReveal bug, not load-bearing. With `PluginResolver` pointing at the correct ESPFE variant, Mutagen's in-memory FormKey for each Track is already at `0x000884` (the compacted slot). `CopyAsOverride` preserves those FormKeys, and even `WithNoLoadOrder`'s write serializer faithfully emits them — the on-disk bytes match the in-memory key regardless of master-style-lookup state.

`WithLoadOrder` stays in shipped code. Revert landed before commit. Rationale: (a) it's still correct for MasterFlagsLookup-dependent encoding edge cases (medium masters, non-compacted-on-read plugin variants that Mutagen might grow support for), (b) it's the forward-compatible call shape Mutagen's 0.53+ write API wants, (c) removing it to save 15 lines would trade a legible "we tell Mutagen our load order" pattern for "we rely on FormKeys being already-correct in memory", which is a weaker invariant to defend.

### Feeds into Phase 3's decision

The control test makes the Phase 3 question "do we need `GameEnvironment` in the bridge for reads, or only for writes?" easier to reason about. Evidence:

- Raw `CreateFromBinaryOverlay` on a correctly-resolved plugin path returns FormKeys that match xEdit's compacted-slot view. No LinkCache required for correctness on the ESL cases we hit.
- `BeginWrite.WithLoadOrder(KeyedMasterStyle[])` alone is correct for the v2.6.0 write scope — no full `IGameEnvironment<ISkyrimMod, ISkyrimModGetter>` plumbing required.

Phase 3 should probably NOT introduce `GameEnvironment` to the bridge unless a new scan-command edge case requires it. Keep the bridge's Mutagen surface narrow: `SkyrimMod.CreateFromBinaryOverlay` for reads, `patchMod.BeginWrite.WithLoadOrder(...)` for writes, `KeyedMasterStyle.FromPath` for load-order context. That's the full API used in Phase 2's working build.

## Deviations from plan

1. **Scope expanded to absorb the `PluginResolver` fix.** PLAN.md's original Phase 2 section limits files-to-touch to the bridge plus `tools_patching.py` / `tools_records.py`'s path-resolution. `esp_index.py` was explicitly out-of-scope for Phase 2 (gated to Phase 3). The `PluginResolver` root-cause discovery forced the scope expansion to make the headline bug fix actually ship in v2.6.0. PLAN.md's "Mid-Phase-2 update" paragraph in the Background section documents this. The `PluginResolver` fix is a surgical `resolve_fn` injection — the class and its legacy walk remain in place as a fallback for organizer-less callers and tests. Phase 3 deletes both.

2. **Chose `KeyedMasterStyle` over full `GameEnvironment.Typical.Builder`.** Plan text sketched building an `IGameEnvironment<ISkyrimMod, ISkyrimModGetter>` via `GameEnvironment.Typical.Builder<...>(GameRelease.SkyrimSE).WithTargetDataFolder(...).WithLoadOrder(...).Build()` and routing reads + writes through it. I took the leaner path because Phase 0's scope-narrowing conclusion (reads are correct, write-path master-flag lookup is the correctness need) held up under investigation, and `KeyedMasterStyle.FromPath` supplies exactly the master-style info `WithLoadOrder` needs. This also sidesteps the "MO2 plugins live in separate mod folders, `Builder` expects a unified data directory" mismatch — `KeyedMasterStyle.FromPath(new ModPath(modKey, disk_path), release)` takes an absolute path per plugin, so no unified directory is required.

3. **Probe extension (not a deviation per se, but worth logging).** Phase 0's decisive probe dumped first-10 MUSTs from NyghtfallMM — which happened to be the `NYExploreEvening01..04` + `NYExploreMorning01..04` records whose raw stored IDs happen to fall in the ESL 12-bit range (0x800-0x808). The probe recorded correct-looking output and Phase 0 concluded "MINIMAL scope — reads are fine". The bug-target records `NYReveal01..06` weren't in the first-10 MUST sample. Mid-Phase-2 I extended `research/Phase0Probe/Program.cs` with an EDID-filtered NYReveal dump plus a `LinkCache.TryResolve` comparison, which settled that Mutagen 0.53.1's raw overlay IS applying correct compaction for NyghtfallMM across the board — the issue wasn't read-side after all. That extension is worth keeping in the probe for future sessions rerunning the diagnostic against other plugins.

4. **Debug logging added and removed inline.** Added stderr logging in `LoadOrderContextResolver.BuildMasterStyledListings` (per-listing included/skipped/style) and `PatchEngine.Process` (pre-write / post-write MUSC Tracks FormKeys) plus a `_bridge_stderr` passthrough in `tools_patching._handle_create_patch` to diagnose the root cause via one `mo2_create_patch` call. Those revealed `NyghtfallMM.esp added at index 362, style=Full, path=.../Replacer - Nyghtfall - Music/NyghtfallMM.esp` — the smoking gun. All debug logging has been removed from the final commit.

## Known issues / open questions

1. **PRE-WRITE `patchMod.MusicTypes` iteration in Phase0Probe Stage 2 returned FormKeys at `0x000884`, but my standalone write-with-4-masters output round-tripped clean at `0x000884` — the full-load-order bridge write (before `PluginResolver` fix) produced `0x002E55`.** This confirmed the root cause is at the read step (wrong file), not the write step. Noting for Phase 3 in case a scan command ever needs to replicate this style of cross-module debug harness.

2. **`organizer.resolvePath(name)` vs `organizer.resolvePath(Path(name))`.** Python's `organizer.resolvePath` returns a `str`, and I coerce it via `Path(...)` in `LoadOrderIndex._scan_plugin`'s caller. Edge case I didn't test: plugins whose VFS resolution returns a path with non-ASCII characters. Aaron's modlist doesn't have any, so this didn't surface. Phase 3 should confirm the Unicode handling when it refactors the whole resolution path through `organizer.modList()`.

3. **Pre-enable read-back.** Still doesn't work — the Phase 1 smoke test note (`mo2_record_detail` returns "Record not found" for just-written disabled plugins) remains true in Phase 2. That's known v2.5.6 behaviour; Phase 4 is where the workflow gets simplified. Out of scope for Phase 2.

## Preconditions for Phase 3

- [x] Bridge has `LoadOrderContext` + `LoadOrderListingEntry` JSON types; `PatchRequest` / `ReadRequest` / `ReadBatchRequest` accept them nullably. Phase 3 scan command can hang a new `ScanRequest` alongside on the same contract.
- [x] `KeyedMasterStyle`-backed write path produces xEdit-correct patches when fed a correct plugin load order. Phase 3's scan command doesn't need to reinvent master-style resolution.
- [x] `PluginResolver` stop-gap fix routes plugin-path lookups through MO2's VFS — Phase 3 can replace it with `organizer.modList()` usage without worrying that path correctness regresses during the transition.
- [ ] **Not yet in place:** bridge `scan` command (`ScanRequest` / `ScanResponse` / `IndexScanner.cs` / `Program.cs` dispatch). Phase 3 implements.
- [ ] **Not yet in place:** Python harness exploration of MO2's API surface for implicit-load classification, enabled/disabled state, priority-ordered mod iteration. Phase 3 must do this FIRST, before committing to deletions. See "Open question" below.
- [ ] **Not yet in place:** cache-format bump. Current `.record_index.pkl` still stores `_PluginCache` entries with `(type, raw_formid_int, edid, file_offset)` tuples. Phase 3 changes this to bridge-sourced `(record_type, formid_str, edid)` and needs to invalidate old caches.

## Open question for Phase 3

The v2.5.6 and v2.5.7 CHANGELOG entries describe several behaviours that the index currently implements in Python and that Phase 3's framing wants to delete:

- Implicit-load classification (base-game ESMs + CC masters from `Skyrim.ccc` counted as enabled even without `plugins.txt` stars).
- `include_disabled` filtering across `mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`.
- "Record exists only in disabled plugins" error distinguishing from "not found".

Phase 3 needs to re-implement these on top of MO2's `pluginList()` API rather than Python parsers. Phase 3's FIRST act is a harness script that answers:

1. Does `organizer.pluginList().state(name)` distinguish `ACTIVE` / inactive / missing for implicit-load plugins? (`ACTIVE` already comes back for base ESMs without explicit work in v2.5.7's classifier? Needs verification.)
2. Does `organizer.pluginList().pluginNames()` include implicit-load plugins without `plugins.txt` stars?
3. Does MO2 have a priority-iteration API like `mod_list.allModsByProfilePriority()` or similar? Verified the existence of `mod_list.priority(name)`; need to confirm the iteration form.

If the answer to any is NO, Phase 3's framing of "delete the parallel implementations" needs to weaken to "delete where possible, keep thin shims where MO2's API surface is incomplete — but never duplicate Mutagen's FormID work". The PLAN.md update anticipates this fallback.

## Files of interest for Phase 3

**Primary targets Phase 3 will modify or delete:**

- `mo2_mcp/esp_reader.py` — delete entirely. Archive to `dev/archive/v2.6_retired/` first.
- `mo2_mcp/esp_index.py` — delete `PluginResolver`, `resolve_formid`, `read_active_plugins`, `read_implicit_plugins`, `read_ccc_plugins`, `IMPLICIT_MASTERS`; replace `_scan_plugin` with a bridge-call path; bump cache format.
- `mo2_mcp/test_esp_index.py`, `test_esp_reader.py` — rewrite / archive.
- `tools/mutagen-bridge/Models.cs` — append `ScanRequest` / `ScanResponse` / `ScannedPlugin` / `ScannedRecord` types.
- `tools/mutagen-bridge/IndexScanner.cs` (new).
- `tools/mutagen-bridge/Program.cs` — dispatch `"scan"` command.

**Files Phase 3 should verify but likely not touch:**

- `mo2_mcp/tools_records.py` — `_parse_formid_str`, `build_bridge_load_order_context`, `_resolve_via_mo2` closure, `_handle_build_index` — confirm they keep working when the index rebuild starts calling the bridge. The module-level `_organizer` stash pattern is there, so debounced rebuilds from timer threads continue to work.
- `mo2_mcp/tools_patching.py` — load-order injection path stays the same; bridge's `LoadOrderContext` schema is forward-compatible.

**Reference material (keep, don't modify):**

- `<workspace>/research/Phase0Probe/` — extended in Phase 2 with NYReveal-specific dumps + write-repro stages. Useful for Phase 3 if the scan command's FormID output needs diagnostic comparison.
- `<workspace>/research/EslReproPatcher/` — still the canonical "Synthesis produces correct output on this modlist" reference. Phase 3 doesn't need to run it, but keep until Phase 5 completes.
- `<workspace>/research/Mutagen/` + `<workspace>/research/Synthesis/` — Mutagen 0.53.1 source for fluent-API spelling lookups (e.g., `BinaryWriteBuilder.WithLoadOrder` overloads, `KeyedMasterStyle.FromPath`).

## Plan revisions landed in this commit

Four changes to `Claude_MO2/dev/plans/v2.6.0_mutagen_migration/PLAN.md`:

1. **Background section** — appended a "Mid-Phase-2 update (2026-04-22)" paragraph documenting the path-resolution discovery and the scope-expansion rationale.
2. **Phase 3** — rewritten entirely from "Retire `esp_reader.py`, rewrite `esp_index.py` over the bridge" to "Mutagen-authoritative; Python is a thin cache". New framing emphasises deletion of parallel implementations, not just retiring `esp_reader.py`. Adds a required open-question verification step as Phase 3's first act.
3. **Phase 4** — appended a sentence under the goal noting that the freshness check is trivial post-Phase-3 because the cache has nothing to be stale about except plugin mtimes.
4. **Phase 5 — T6** — appended a note that some v2.5.6/v2.5.7 regression scenarios (implicit-load classification, enabled-filter behaviour) now exercise MO2 API integration rather than our parser. Same surface outcomes, different implementation underneath.

## Commits

- `[v2.6 P2] Fix PluginResolver path resolution; add Mutagen load-order write path; PLAN.md revisions` — single commit absorbs the bridge/Python changes and the plan revisions. Hash recorded post-commit.

Working tree at Phase 3 start: clean. No stray files.
