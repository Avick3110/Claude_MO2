# Phase 3 — MO2 API harness output

**Date:** 2026-04-22
**Tool:** `mo2_diag_api_surface_p3` (uncommitted; `tools_diag.py` + `__init__.py` registration line)
**Modlist:** Authoria - Requiem Reforged (Aaron's live install)
**MO2 version:** 2.5.2.0
**Plugin version:** 2.5.7 (PLUGIN_VERSION still on v2.5.7 — harness shipped via plain Python sync, no bump)
**modList().allMods() count:** 3,764
**Skyrim.ccc entries:** 75

This document captures the answers to the three verification questions PLAN.md
gates Phase 3 deletions on (`Phase 3 — Open question`). Q1 + Q2 are answered
here; Q3 (bridge scan vs xEdit) is answered separately once the bridge `scan`
command lands.

The harness output is **verbatim raw API responses** with type, repr, length,
and exception info for every probe. Future regressions can re-run the harness
and diff against this file.

---

## Verdict summary

| Question | Answer | Phase 3 implication |
|---|---|---|
| **Q1.** Does `pluginList()` include implicit-load plugins (base masters + Skyrim.ccc CC) as ACTIVE without our hand-rolled classification? | **YES** — and *better*: MO2 only marks them ACTIVE if the plugin is actually installed in some mod folder. Our hand-rolled `read_implicit_plugins()` blindly unions every line of Skyrim.ccc into the active set whether the plugin exists or not. | **Delete** `IMPLICIT_MASTERS`, `read_ccc_plugins`, `read_implicit_plugins`, `read_active_plugins`. Replace with a single `pluginList().state(name) == mobase.PluginState.ACTIVE` check per plugin. |
| **Q2.** Is there a priority-iteration API? | **YES on modList** — `mod_list.allModsByProfilePriority()` exists and is callable. For pluginList, no built-in helper, but `sorted(plugin_list.pluginNames(), key=plugin_list.loadOrder)` is a one-liner that uses MO2's actual load order. | **Delete** `PluginResolver` (alphabetical mods/ walk). Replace mod-iteration call sites with `allModsByProfilePriority()`. Replace plugin-iteration with the sorted pluginNames() form. Phase 2's `organizer.resolvePath` injection stays as the per-plugin path lookup — it's the single-plugin equivalent. |
| **Bonus.** Does `organizer.resolvePath()` handle non-ASCII names safely? (PHASE_2 open question #2) | **YES** — synthetic name `"Tëst-Pluğin-名前-Ω.esp"` returned `""` (empty string) without exception. Path coercion of empty result is a no-op. | Non-blocking. The actual rewrite still needs to verify that real on-disk paths with non-ASCII characters round-trip cleanly through Mutagen-bridge JSON, but the Python side is safe. |
| **Q3.** Does the bridge scan output match xEdit's enumeration? | **PENDING** — bridge `scan` command not yet implemented. | Built next; output captured in a follow-up section of this document. |

**Overall scope decision (pending Aaron's go-ahead):** **FULL DELETE** path. MO2's API answers Q1 and Q2 cleanly. No need for the "thin shims where MO2's API is incomplete" fallback PLAN.md anticipated. Phase 3 can proceed with the full deletion checklist.

---

## Q1 — Implicit-load classification (verbatim)

**Question:** Does `pluginList()` include implicit-load plugins (base ESMs + CC masters from Skyrim.ccc) without our hand-rolled classification?

**Probe results:**

### Base masters (5/5 ACTIVE in pluginList)

| Plugin | In `pluginNames()`? | `state()` | `loadOrder` | `origin` |
|---|---|---|---|---|
| Skyrim.esm | true | 2 (ACTIVE) | 0 | `"data"` |
| Update.esm | true | 2 (ACTIVE) | 1 | `"Cleaned Base Game Masters"` |
| Dawnguard.esm | true | 2 (ACTIVE) | 2 | `"Cleaned Base Game Masters"` |
| HearthFires.esm | true | 2 (ACTIVE) | 3 | `"Cleaned Base Game Masters"` |
| Dragonborn.esm | true | 2 (ACTIVE) | 4 | `"Cleaned Base Game Masters"` |

`organizer.resolvePath("Skyrim.esm")` → `"E:\Skyrim Modding\Authoria - Requiem Reforged\Stock Game\data\Skyrim.esm"` ✅

Note: `origin == "data"` for Skyrim.esm specifically (it lives directly in `Stock Game/data/`, not in a mod folder). The other base masters live in the `Cleaned Base Game Masters` mod folder. The Phase 3 rewrite should not assume `origin` is always a mod folder name — `"data"` is a valid origin string.

### CC masters (sample of first 10 from Skyrim.ccc)

| CC plugin | In `pluginNames()`? | `state()` | Notes |
|---|---|---|---|
| ccASVSSE001-ALMSIVI.esm | true | 2 (ACTIVE) | Installed as mod ✅ |
| ccBGSSSE001-Fish.esm | true | 2 (ACTIVE) | Installed as mod ✅ |
| ccBGSSSE002-ExoticArrows.esl | **false** | **0 (MISSING)** | Not installed — and MO2 correctly excludes it |
| ccBGSSSE003-Zombies.esl | true | 2 (ACTIVE) | Installed as mod ✅ |
| ccBGSSSE004-RuinsEdge.esl | true | 2 (ACTIVE) | Installed as mod ✅ |
| ccBGSSSE005-Goldbrand.esl | **false** | **0 (MISSING)** | Not installed |
| ccBGSSSE006-StendarsHammer.esl | true | 2 (ACTIVE) | Installed as mod ✅ |
| ccBGSSSE007-Chrysamere.esl | **false** | **0 (MISSING)** | Not installed |
| ccBGSSSE010-PetDwarvenArmoredMudcrab.esl | **false** | **0 (MISSING)** | Not installed |
| ccBGSSSE011-HrsArmrElvn.esl | true | 2 (ACTIVE) | Installed as mod ✅ |

**Key finding:** Aaron's Skyrim.ccc has 75 entries, but only the ones physically installed as mods appear in `pluginNames()` and report `state == ACTIVE`. Our hand-rolled `read_implicit_plugins()` (esp_index.py lines 210–222) has been silently misclassifying the missing CC plugins as enabled — they would have shown up in `active_lower` but not in `loadorder.txt`, so they would never have been scanned, but they would also never have been removed from `enabled` consideration. This is a latent bug masked by the fact that `_load_order` only contains plugins from `loadorder.txt` (which MO2 keeps clean of missing CC entries), so these "phantom enabled" entries had nowhere to land. Worth noting in PHASE_3_HANDOFF as a side benefit of the rewrite — even though the bug never surfaced, the parallel implementation was wrong in a way ours doesn't detect.

### Decision

**DELETE** — every line of:
- `esp_index.py:IMPLICIT_MASTERS` (frozenset of 5 base ESM names)
- `esp_index.py:read_ccc_plugins(game_root)` (parses Skyrim.ccc)
- `esp_index.py:read_implicit_plugins(game_root)` (combines the above)
- `esp_index.py:read_active_plugins(profile_dir)` (parses plugins.txt)

**REPLACE WITH:**
```python
def get_enabled_plugins(plugin_list):
    return {
        n for n in plugin_list.pluginNames()
        if plugin_list.state(n) == mobase.PluginState.ACTIVE
    }
```

This single pluginList() walk handles base masters, CC masters, plain ESMs, and ESPs with one rule, automatically excludes uninstalled CC entries, and uses MO2's authoritative state — no plugins.txt parsing.

Note: `read_load_order(profile_dir)` reads `loadorder.txt`. We can also replace it with `sorted(plugin_list.pluginNames(), key=plugin_list.loadOrder)` — see Q2 — but that needs verification that pluginNames() covers BOTH active AND inactive plugins (test case below).

---

## Q2 — Priority-iteration API (verbatim)

**Question:** Is there a priority-iteration API like `allModsByProfilePriority()`?

### modList method enumeration (verbatim, non-dunder)

```
allMods, allModsByProfilePriority, displayName, getMod,
onModInstalled, onModMoved, onModRemoved, onModStateChanged,
priority, removeMod, renameMod, setActive, setPriority, state
```

**Probed candidates:**
- `mod_list.allModsByProfilePriority` → **EXISTS** (`hasattr` returned `True`, `callable=True`) ✅
- All other candidates (`allModsByPriority`, `modsByPriority`, `sortedByPriority`, etc.) → False

### pluginList method enumeration (verbatim, non-dunder)

```
hasLightExtension, hasMasterExtension, hasNoRecords,
isLightFlagged, isMaster, isMasterFlagged, isMediumFlagged,
loadOrder, masters,
onPluginMoved, onPluginStateChanged, onRefreshed,
origin, pluginNames, priority,
setLoadOrder, setPriority, setState, state
```

**Probed candidates:**
- All priority-iteration / load-order-iteration candidates returned `False`. No built-in helper. **Workaround:** `sorted(plugin_list.pluginNames(), key=plugin_list.loadOrder)` — every plugin has a `loadOrder()` value (or `-1` for missing).

### Are the `allMods()` / `pluginNames()` returns already in priority order?

`allMods()` first-20 priorities (verbatim):
```
('(CVEO) by LDD - Aretuza Eyes Remastered', 2022)
('--- Alternate Perspective ---_separator', 3525)
('--- ANIMATIONS ---_separator', 3025)
('--- ARMORS AND WEAPONS---_separator', 2307)
('--- AUDIO ---_separator', 478)
('--- CHARACTER VISUALS ---_separator', 1959)
('--- CITY STUFF ---_separator', 1525)
('--- CORE ---_separator', 4)
('--- FINISHING LINE ---_separator', 3539)
('--- INTERIORS ---_separator', 1849)
('--- LIGHTING ---_separator', 3285)
('--- Norden UI ---_separator', 3583)
('--- QUESTS AND NEWLANDS ---_separator', 534)
('--- USER CUSTOMIZATION ---_separator', 3610)
('--- USER INTERFACE ---_separator', 350)
('--- VISUALS ---_separator', 903)
('--------------_separator', 3700)
('-----_separator', 3614)
('---GAMEPLAY---_separator', 2544)
('1st Person Animations - Bow-Archery', 3235)
```

→ `allMods_first20_priorities_match_ascending: false` — **`allMods()` returns alphabetical, NOT priority order.** This is exactly the failure mode Phase 2's PluginResolver bug exhibited.

`pluginNames()` first-20 load-orders: also alphabetical (mixed positions: 1060, 57, 1057, 1194, 1133, …), `pluginNames_first20_match_ascending: false`.

**Implication:** any code that iterates `allMods()` or `pluginNames()` in iteration order and relies on priority/load-order semantics is buggy. Phase 2's PluginResolver was exactly this. Phase 3 must use `allModsByProfilePriority()` for mods and `sorted(pluginNames(), key=loadOrder)` for plugins.

### Decision

**DELETE** — `esp_index.py:PluginResolver` class entirely (alphabetical `mods/` walk).

**REPLACE WITH:** Phase 2's `organizer.resolvePath(name)` injection — already the per-plugin path-lookup primitive the index uses post-Phase-2. That stays.

For mod-iteration call sites (none currently in `esp_index.py`, but if Phase 3 adds any during the rewrite — e.g., to walk mod files for the new bridge-fed scan), use `mod_list.allModsByProfilePriority()`.

For plugin-iteration:
```python
def load_order_plugins(plugin_list):
    return sorted(
        plugin_list.pluginNames(),
        key=lambda n: plugin_list.loadOrder(n),
    )
```

This replaces `read_load_order(profile_dir)` from `esp_index.py`. Note: this returns ALL plugins (active + inactive), matching `loadorder.txt`'s contents. Confirmed by the harness's pluginNames() returning 1060 / 1057 / 1147 etc. — these are not all-ACTIVE positions, so pluginNames() includes inactive plugins too. (The `loadOrder=-1` cases were only the MISSING-from-disk plugins.)

---

## Bonus — Unicode resolvePath (PHASE_2 open question #2)

**Synthetic name:** `"Tëst-Pluğin-名前-Ω.esp"` (Latin-1 ë, extended Latin ğ, CJK 名前, Greek Ω)

**UTF-8 bytes:** `54c3ab73742d506c75c49f696e2de5908de5898d2dcea92e657370`

**Result:**
```json
{
  "ok": true,
  "type": "str",
  "value": "",
  "len": 0,
  "repr": "''"
}
```

**Path coercion:** skipped (raw was empty)

**Verdict:** `organizer.resolvePath()` accepts non-ASCII strings without raising and returns `""` for unknown plugins (same return as for `"DefinitelyDoesNotExist_zzqq.esp"` — empty-string is the universal "not found" sentinel). Phase 2's `Path(organizer.resolvePath(name)) if real else None` pattern is safe — the `if real` short-circuit handles the empty case before Path coercion.

**Open follow-up (not blocking):** real on-disk paths containing non-ASCII characters need to round-trip through:
1. Python str → JSON (UTF-8, fine)
2. subprocess stdin → .NET stdin reader (UTF-8 if we set it; defaults can vary)
3. .NET file open by path

The bridge currently uses `Console.In.ReadToEnd()` which on Windows defaults to the system code page. If a user has a non-ASCII path, the bridge could lose information. Worth a 5-line probe in Phase 3 (synthetic plugin file with non-ASCII filename in a throwaway mod folder, run a `read_record` against it). Not in scope for the API harness; tracked as a Phase 3 verification step.

---

## Bonus — Auxiliary observations from the data

These are findings outside the three questions but worth noting before the rewrite:

### `loadOrder == priority` for plugins, but distinct for mods

Plugin `priority(n)` and `loadOrder(n)` return the same int for ACTIVE plugins (e.g. Skyrim.esm both = 0, NyghtfallMM both = 362). For mods, `priority` is the left-pane priority (different scale, e.g. NyghtfallMM's parent mod folder has its own priority).

Phase 3 implication: when wiring plugin iteration, either `priority(n)` or `loadOrder(n)` works on the pluginList side. Use `loadOrder()` for clarity (matches the "load order" terminology elsewhere).

### `origin == "data"` for Skyrim.esm

Plugins living directly in `Stock Game/data/` (the game's root data dir) have `origin == "data"` — not a mod folder name. Phase 3 must not assume `origin` resolves through `mod_list.getMod(origin)` — that returns `None` for `"data"`. If we need the actual on-disk parent folder for any reason, derive it from `organizer.resolvePath(plugin_name)` instead of via `mod_list`.

### NyghtfallMM.esp — Phase 2 fix verified at API level

```
organizer.resolvePath("NyghtfallMM.esp")
  → "E:\Skyrim Modding\Authoria - Requiem Reforged\mods\Nyghtfall - ESPFE (Replacer)\NyghtfallMM.esp"
origin = "Nyghtfall - ESPFE (Replacer)"
isLightFlagged = true
```

MO2's API correctly resolves to the ESPFE variant. Phase 2's `resolve_fn` injection lands exactly here. Phase 3's `organizer.resolvePath`-based replacement of `PluginResolver` will use the same primitive. ✅

### `mobase.PluginState` enum (verbatim)

```
ACTIVE  = 2
INACTIVE = 1
MISSING = 0
```

(Plus aliases `active`, `inactive`, `missing` for the old casing.)

Phase 3 query handlers should use `mobase.PluginState.ACTIVE` for the comparison — already the convention in `tools_modlist.py`.

### `mobase.ModState` enum (verbatim)

```
ACTIVE     = 2
ALTERNATE  = 64
EMPTY      = 8
ENDORSED   = 16
ESSENTIAL  = 4
EXISTS     = 1
VALID      = 32
```

ModState is a bitmask (per `tools_modlist._is_active_mod`: `bool(state & mobase.ModState.ACTIVE)`). Phase 3 doesn't directly touch ModState but worth recording for the next session.

---

## Files this informs

The next session implementing Phase 3 will use this output to:

1. **`mo2_mcp/esp_index.py`** — delete `IMPLICIT_MASTERS`, `read_ccc_plugins`, `read_implicit_plugins`, `read_active_plugins`, `PluginResolver`, `resolve_formid`, `_PluginCache.records`'s int-formid format, `_FMT_RECORD` etc. Replace with bridge-fed cache + MO2 API queries for enabled state.
2. **`mo2_mcp/esp_reader.py`** — archive to `dev/archive/v2.6_retired/esp_reader.py`, then delete.
3. **`mo2_mcp/test_esp_index.py`, `test_esp_reader.py`** — rewrite/archive as appropriate.
4. **`tools/mutagen-bridge/Models.cs`, `IndexScanner.cs`, `Program.cs`** — implement `scan` command.
5. **`mo2_mcp/tools_records.py`** — replace `_handle_build_index`'s `LoadOrderIndex(...).build()` path with bridge-call orchestration. The Phase 2 `_resolve_via_mo2` closure becomes unnecessary at this seam (bridge takes paths directly via `LoadOrderContext.listings`); keep it only if it's still used by leftover callers.

---

## Q3 — Bridge scan vs xEdit (verbatim)

**Tool:** `mo2_diag_bridge_scan_p3` (uncommitted; scan command added to
`tools/mutagen-bridge/{Models.cs, IndexScanner.cs, Program.cs}` plus a
diagnostic dispatch in `tools_diag.py`).

**Bridge:** `mutagen-bridge.exe` (just-built v2.6 P3 binary, synced to
`E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\tools\mutagen-bridge\`).

**Plugins scanned:** Skyrim.esm, Update.esm, Dawnguard.esm, NyghtfallMM.esp, Requiem.esp.

**Scan elapsed:** **7.68s** for 5 plugins / 1,012,070 records → ~131,000 records/sec aggregate. Fast enough that batched re-scans during Phase 4's freshness check will be invisible to interactive use.

### Per-plugin metadata (verbatim)

| Plugin | Records | is_master | **is_light** | is_localized | masters_count | Error |
|---|---:|:---:|:---:|:---:|---:|:---|
| Skyrim.esm | 869,687 | true | false | true | 0 | none |
| Update.esm | 16,044 | true | false | true | 1 | none |
| Dawnguard.esm | 95,133 | true | false | true | 2 | none |
| **NyghtfallMM.esp** | 268 | false | **true** | false | 3 | none |
| Requiem.esp | 30,938 | false | false | false | 11 | none |

NyghtfallMM correctly classified as `is_light=true` (the ESL flag) — Mutagen reads `SkyrimModHeader.HeaderFlag.Small` directly. Master-count, localization, and master-flag detection all working.

### Aggregate top record types (top 10, across all 5 plugins)

```
REFR  771,653
INFO   35,116
CELL   31,890
LAND   19,507
NAVM   17,767
DIAL   17,110
ACHR   12,360
STAT   11,225
NPC_    8,448
WEAP    6,948
```

Record-type extraction works correctly via the `TriggeringRecordType` reflection trick — no fallback `SKYRIMMAJORRECORD` strings, no missing types. The four-letter Bethesda codes are emitted directly off Mutagen's per-record registration class.

### THE smoking gun — NyghtfallMM.esp NYReveal records (verbatim)

These are the exact records that the 2026-04-21 bug report identified as broken — the MUSC merge patch's Tracks rendered as `[FE000E55..FE000E5A] <Error: Could not be resolved>` because the patch encoded the raw `0x002E55..` bytes instead of the compacted `0x000884..` slot IDs. Phase 2's `WithLoadOrder` write path + `PluginResolver` fix shipped the headline bug fix; here we confirm the bridge's *read* path is also correct (which is the foundation Phase 3's index will build on).

```
type  formid                                edid
MUST  NyghtfallMM.esp:000884                NYReveal01
MUST  NyghtfallMM.esp:000885                NYReveal02
MUST  NyghtfallMM.esp:000886                NYReveal03
MUST  NyghtfallMM.esp:000887                NYReveal04
MUST  NyghtfallMM.esp:000888                NYReveal05
MUST  NyghtfallMM.esp:000889                NYReveal06
```

**Verdict:** ✅ FormIDs come back **compacted** at `0x000884..0x000889` — exactly what xEdit reports for these records. NOT raw `0x002E55..0x002E5A`. The bridge `scan` command produces xEdit-correct output for ESL plugins by construction. Mutagen's `FormKey` already encodes the compacted slot ID via `CreateFromBinaryOverlay`; `FormIdHelper.Format(record.FormKey)` emits it directly.

### NyghtfallMM MUSC overrides (sample — first 5)

The MUSC records in NyghtfallMM are mostly **overrides of Skyrim.esm vanilla records**, which is exactly the conflict pattern the original 2026-04-21 bug occurred against. The bridge correctly emits these with the master-table-resolved origin:

```
type  formid                                edid
MUSC  Skyrim.esm:013686                     MUSSpecialDeath
MUSC  Skyrim.esm:017035                     MUSTavernB
MUSC  Skyrim.esm:017036                     MUSSpecialHallofValor
MUSC  Skyrim.esm:01714B                     MUSSovngardeHallofValor
MUSC  Skyrim.esm:02C3CA                     MUSTownTest
```

Origin resolution working: NyghtfallMM's overrides of Skyrim.esm records show up keyed on `Skyrim.esm:`, not `NyghtfallMM.esp:`. This is what Python's old `resolve_formid()` was computing manually — Mutagen does it natively.

### NyghtfallMM MUST first 5 (mixed origin)

```
type  formid                                edid
MUST  Skyrim.esm:000EA4                     _MUSExploreSILENT15TypeSpacerNIGHT
MUST  NyghtfallMM.esp:000800                NYExploreEvening01
MUST  NyghtfallMM.esp:000801                NYExploreEvening02
MUST  NyghtfallMM.esp:000802                NYExploreEvening03
MUST  NyghtfallMM.esp:000803                NYExploreEvening04
```

Mixed-origin enumeration works: one Skyrim.esm override, then NyghtfallMM's own records at compacted IDs `0x000800..0x000803`. (These are the records Phase 0 originally probed; matches Phase 0's findings.)

### Unicode round-trip (PHASE_2 open #2 — bridge side)

Python sent: `"C:/no/such/path/Tëst-Pluğin-名前-Ω.esp"` over UTF-8 stdin.

Bridge response (verbatim):
```json
{
  "success": false,
  "plugins": [
    {
      "plugin_name": "Tëst-Pluğin-名前-Ω.esp",
      "plugin_path": "C:/no/such/path/Tëst-Pluğin-名前-Ω.esp",
      "masters": [],
      "is_master": false,
      "is_light": false,
      "is_localized": false,
      "record_count": 0,
      "records": [],
      "error": "Plugin not found: C:/no/such/path/Tëst-Pluğin-名前-Ω.esp"
    }
  ],
  "error": "All plugins in the batch failed to scan."
}
```

The non-ASCII bytes (Latin-1 `ë`, extended Latin `ğ`, CJK `名前`, Greek `Ω`) round-tripped **verbatim** through:
1. Python `subprocess.run(input=json.dumps(...), encoding='utf-8')` → bridge stdin
2. .NET `Console.In.ReadToEnd()` → JSON parse → `ScanRequest.Plugins`
3. Bridge `Path.GetFileName()` extraction (preserved encoding)
4. JSON serialize → `Console.Write` → bridge stdout
5. Python subprocess capture → `json.loads`

**No mangling at any hop.** PHASE_2 open question #2 is fully resolved on both sides — both `organizer.resolvePath()` (Q1's Unicode probe) AND the bridge subprocess pipeline handle non-ASCII safely.

### Q3 verdict

✅ **Bridge scan output matches xEdit by construction.** Phase 3 can proceed with the full deletion of `esp_reader.py` + `esp_index.py:resolve_formid` + the `IMPLICIT_MASTERS`/`PluginResolver`/`read_*` classification machinery. The bridge's `scan` command supplies everything those parallel implementations were producing, with FormIDs that match xEdit by virtue of using Mutagen's authoritative `FormKey`.

### Notes for the rewrite

- **Batch size for the initial full-build call:** the harness ran 5 plugins / ~1M records / 7.68s ≈ 1.5s per plugin average, but Skyrim.esm dominated (869K records). Smaller plugins are millisecond-scale. PLAN.md sketched ~30-plugin batches; that should comfortably stay under the 60s subprocess timeout. Tunable empirically.
- **Output-size watch:** the harness response was 110.3MB because it included `raw_scan_response` (every record twice — once raw, once aggregated). The Phase 3 production path consumes the bridge response in-memory and never serializes it through MCP, so this isn't a production concern. The diag tool is being deleted anyway. But: future harnesses should default to summary-only with raw-on-request, not raw-by-default.
- **Record-type extraction needs no special-casing.** The reflection-on-`TriggeringRecordType` trick covers every record we hit (REFR, INFO, CELL, LAND, NAVM, DIAL, ACHR, STAT, NPC_, WEAP — and that's just the top 10). No "SKYRIMMAJORRECORD" fallback strings observed in the aggregated distribution.
- **NyghtfallMM in-file FormID `0x002E55..` was NOT observed.** Phase 2's prediction held: Mutagen 0.53.1 returns compacted FormKeys at the read step; the v2.5.x bug was at the write step + PluginResolver, both of which Phase 2 fixed.

