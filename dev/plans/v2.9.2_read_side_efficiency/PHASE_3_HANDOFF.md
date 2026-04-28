# Phase 3 Handoff — Live Authoria workflow scenarios — cross-master expansion bug confirmed

**Phase:** 3
**Status:** Blocked (mandatory halt — cross-master expansion bug confirmed live)
**Date:** 2026-04-28
**Session length:** ~1h
**Commits made:** none (HALT trigger)
**Live install synced:** Yes (`mo2_ping` returns v2.9.2; Phase 2 wrapper sanity confirmed)

## What was done

- **Session-start ritual:** verified `git log -1 --oneline origin/main` = `39dbd07` (Phase 2 hash-record), clean working tree, `mo2_ping` returns v2.9.2 (profile "AL Custom", MO2 2.5.2.0, Skyrim Special Edition).
- **Phase 2 wrapper sanity check passed:** `mo2_record_detail(formids: ["Skyrim.esm:000019"])` returned `{success: true, records: [{formid, success: true, fields: {...}, ...}]}` — per-record envelope shape correct.
- **Live record index status:** built (3341 enabled plugins, 2.9M records, 8.55s warm-cache build, no missing-masters, 2 scan errors on `TasteOfDeath_Addon_Dialogue.esp` and `ksws03_quest.esp`).
- **Live RACE count:** 853 RACE records across the load order (vs Phase 1's 99 in vanilla Skyrim.esm). Phase 1's 168-record consumer figure is well within scope; the live modlist superset is much larger.
- **Live NPC_ count:** queried but unbounded (>>1000); spot-probed Whiterun guards + Alvor for non-empty Factions.
- **Scenario 3.1 micro-probe (5 RACE records, projection + expansion + resolve_links):** ran a 5-element batch with `fields=[EditorID, ActorEffect, Voices, Hairs]`, `expand_links=[ActorEffect]`, `resolve_links=true`. Per-axis assertions:
  - **(a) every formid resolved + per-record envelope:** PASS — top-level `success: true`; all 5 records `success: true`; envelope shape matches Q3 lock.
  - **(b) projection contains exactly requested fields:** PASS — output limited to `EditorID`, `ActorEffect`, `Voices`. (`Hairs` absent because the records have empty Hairs lists; projection is shape-preserving when a path resolves to empty/absent — matches Q4 lock guidance.)
  - **(c) expansion wrapper shape `{formid, EditorID, expanded}`:** PASS for in-master targets; **FAIL for cross-master targets** (see Bug B5).
  - **(d) resolve_links annotates throughout:** PASS — all FormID strings annotated with EditorID, including the cross-master-failed entries (their FormIDs annotate even though their `expanded` payload is null).
- **Scenario 3.2 micro-probe (5 NPC_ records — Alvor + 4 Whiterun guards, projection + expansion on `Factions.Faction` + resolve_links):** All 5 NPC_ records returned `success: true`; total of 32 Faction expansions across 5 NPCs; **all 32 expansions succeeded**. Per-axis assertions:
  - **(a) auto-traversal walks `Factions.Faction` into list-of-structs** per Q1 lock: PASS — `Factions` rendered as a list of `{Faction: {wrapper}}` objects.
  - **(b) wrapper shape on each Faction expansion** per Q2 lock: PASS — every Faction is `{formid, EditorID, expanded: {<full FCTN detail>}}`.
  - **(c) per-record envelope** per Q3 lock: PASS.
  - Cross-master expansion **succeeded** here because one of the batch records (`GuardWhiterunSonsBarracks`) wins in `Skyrim.esm`, which loads Skyrim.esm into the bridge's `modCache`; the FCTN targets (which originate in Skyrim.esm) then resolve. Confirms the bug B5 mechanism — see below.
- **Cross-product Q6 micro-probe** (1 RACE × 2 plugins, `formids=["Skyrim.esm:000D53"] + plugin_names=["Skyrim.esm", "Requiem.esp"]`, projection + expansion): PASS. 2 cells returned, each with its own `plugin_name` + `success: true` + own per-plugin view of the record. Cross-master expansion in the cross-product cell **succeeds** (both Skyrim.esm and Requiem.esp are loaded into modCache, so their FormLink targets resolve). Q6 wiring confirmed working.
- **Stress test:** NOT RUN — halt fired before reaching this step.
- **Full Scenario 3.1 168-record case:** NOT RUN — halt fired during shape verification.
- **Perf comparison vs Phase 1:** NOT RUN — halt fired before timing measurements were taken.

## Verification performed

| Axis | Cell | Status | Evidence |
|---|---|---|---|
| Phase 2 wrapper passthrough | 1-element formids batch on `Skyrim.esm:000019` | PASS | per-record envelope shape |
| Q1 auto-traversal | `Factions.Faction` on NPC_ batch | PASS | walker descends list-of-struct |
| Q1 auto-traversal | `Voices` on RACE batch | PASS | gendered list flattened to FormID list (matches Phase 2 B4 finding) |
| Q2 wrapper shape | `expand_links=[ActorEffect]` on RACE | PARTIAL | wrapper correct for in-master targets; cross-master targets surface uniform `{formid, EditorID:null, expanded:null, error:"FormID target not in load order"}` |
| Q2 wrapper shape | `expand_links=[Factions.Faction]` on NPC_ | PASS | every wrapper has full inline FCTN detail |
| Q3 per-record envelope | 5 RACE batch, 5 NPC_ batch | PASS | each record carries own `success` |
| Q4 pre-flight | not exercised in Phase 3 (covered Phase 2) | n/a | — |
| Q5 unbounded | not exercised at scale (halt fired) | n/a | — |
| Q6 cross-product | N×M=2 cells | PASS | per-cell envelope shape correct |
| Projection | RACE + NPC_ batches | PASS | only requested paths in response |
| resolve_links composition | all axes | PASS | FormIDs annotated throughout including in error wrappers |

## Bugs surfaced

### B5 — Cross-master FormLink expansion fails when the originating master plugin isn't loaded into the bridge's modCache

**Phase 2 known issue #2 surfacing live on Authoria.** Direct continuation of Phase 2's recorded "cross-master FormLink expansion limited by single-plugin reads" — Phase 3's job was to confirm this on the live modlist; confirmation: yes, severe.

**Surface.** `mo2_record_detail` with `formids=[<RACE FormIDs>] + expand_links=["ActorEffect"]` against any RACE whose winning plugin is not Skyrim.esm AND whose `ActorEffect` references a SPEL whose originating master is Skyrim.esm. On the live Authoria load order, this hits a large fraction of RACE records — most RACEs win in `Authoria - Requiem Master Patch.esp` or `Requiem.esp`, and those plugins reference vanilla SPELs (Skyrim.esm-injected, sometimes overridden in Requiem.esp).

**Reproduction.** Single MCP call:
```
mo2_record_detail(
  formids=["Skyrim.esm:000D53"],  // DraugrRace, winning plugin = Requiem.esp
  fields=["ActorEffect"],
  expand_links=["ActorEffect"],
  resolve_links=True
)
```
Response shows the ActorEffect entry `Skyrim.esm:02431D` (REQ_Trait_FX_Draugr — confirmed in load order via `mo2_query_records`, winning in Requiem.esp) wrapped as:
```
{
  "formid": "Skyrim.esm:02431D (REQ_Trait_FX_Draugr)",  // resolve_links annotates correctly
  "EditorID": null,
  "expanded": null,
  "error": "FormID target not in load order"
}
```
The FormID resolves correctly via the load-order index (proven by `resolve_links: true` showing the EditorID), but the bridge's `ExpandFormLinkValue` walker can't find it.

**Root cause.** `Claude_MO2/tools/mutagen-bridge/RecordReader.cs:1032`:
```csharp
foreach (var mod in modCache.Values)
{
    if (!string.Equals(mod.ModKey.FileName.String, formKey.ModKey.FileName.String, StringComparison.OrdinalIgnoreCase))
        continue;
    ...
}
```
The walker loops `modCache.Values` looking for a mod whose ModKey filename matches the linked FormID's originating master. The `modCache` only contains plugins explicitly loaded for the bridge invocation — typically just the parent record's winning plugin. When DraugrRace's winning plugin is `Requiem.esp`, the bridge loads `Requiem.esp` only; it never loads `Skyrim.esm`, so cross-master FormLink expansions targeting Skyrim.esm-originated records fail with `error: "FormID target not in load order"`.

**Why Scenario 3.2 didn't trigger.** The 5-record NPC_ batch happened to include `GuardWhiterunSonsBarracks` whose winning plugin IS `Skyrim.esm`; this loaded `Skyrim.esm` into modCache for the batch as a side effect. The 32 Faction targets (which all originated in Skyrim.esm) then resolved. Without that incidental Skyrim.esm load, the bug would surface for NPC_.Factions.Faction too.

**Why cross-product Q6 doesn't trigger.** When `plugin_names=["Skyrim.esm", "Requiem.esp"]` is supplied, both plugins go into `modCache`, and FormLinks targeting either originator-master resolve.

**Workaround.** Caller can pass `plugin_names=[<every plugin in the master chain>]` alongside `formids` to force-load masters into modCache. But this:
1. Multiplies the response by N×M cells (one per FormID-plugin pairing) — wrong shape for the consumer's "single-plugin-winner read" use case.
2. Requires the caller to know the master chain — defeats the v2.9.2 ergonomics goal.
3. Still doesn't auto-resolve overrides (e.g. if the linked SPEL's winning version is in plugin C, the workaround needs plugin C explicitly listed even if C isn't a master of the parent record's plugin).

**Proposed Phase 4 fix angles** (conductor + Aaron pick):

A. **Bridge auto-loads master chain.** When `Read` / `ReadBatch` opens a plugin, also open all plugins listed in its `MasterReferences`. Recurse one level. Adds load time per plugin but that's typically modest (~100 ms per master). Bounded by master count (5–20 typical, ~50 max). This makes cross-master expansion work for any FormLink whose originating master is in the parent record's master chain — covers vanilla Skyrim records and most mod-cross-references.

B. **Wrapper passes the full load-order plugin list into the bridge**. The Python wrapper has access to `_index._load_order` (the full enabled-plugin set). Pass it to the bridge as `available_plugins: [...]` and have the bridge load them on-demand into modCache when a FormLink fails the originating-master match. Hot-loads only when expansion would otherwise fail — minimal cold-call overhead. Resolves arbitrary cross-plugin FormLinks regardless of master chain.

C. **Wrapper passes the load-order index's per-FormID winning-plugin map into the bridge.** Bridge's `ExpandFormLinkValue` looks up the winning plugin for the linked FormID + loads ONLY that specific plugin on demand. Most surgical fix; resolves overrides-not-in-master-chain cleanly. Adds wrapper→bridge protocol surface (the index serializes a winner map).

D. **Bridge takes a per-call `master_search_plugins: [...]` parameter; wrapper auto-fills it from the master chain of every batch member.** Hybrid of A and B — caller-driven but wrapper auto-fills. Less invasive than B; same coverage as A.

E. **Document the limitation; ship v2.9.2 as-is.** Cross-master expansion only works when the linked record's master is in the same modCache (i.e. when one of the batch records or named plugins happens to be the master). Caller workaround: include `plugin_names: [...]` for any masters they need expanded. Defers fix to v2.9.x. Acceptable if Aaron decides the consumer's primary use case (single-plugin patcher reading its OWN records' FormLinks) doesn't hit this — but the consumer's 168-record framing (vanilla Skyrim RACE FormIDs read from their winning plugins) DOES hit it broadly.

**My recommendation:** Option A or B. Option A is simplest, catches >95% of real-world cases, and doesn't require new protocol surface. Option B is more robust but requires more wiring. Option C is surgical but adds protocol churn. Option E is the v2.9.2-ship-with-caveat path — viable if Aaron decides the consumer use case is well-served by `plugin_names`-driven inclusion, but the 168-record consumer signal that motivated v2.9.2 expects the bridge to handle this.

## Deviations from plan

1. **Halt fired before completing all Phase 3 deliverables.** Per kickoff "Mandatory halt-and-CONDUCTOR-ASK triggers": "Cross-master FormLink expansion fails on Authoria... HALT. This is a real bug requiring Phase 4 fix." Followed: scenario 3.1 full 168-record run skipped; stress test skipped; perf comparison vs Phase 1 not measured (no timing data captured).
2. **Used MCP tool calls directly rather than a separate Python timing harness.** The kickoff outlined a Python timing wrapper pattern (subprocess-based timing); given the halt fired during shape verification, the timing harness was not built. Phase 4's re-run after the cross-master fix should include the full Python timing harness pattern to capture wall-clock + token-count vs Phase 1 baseline.
3. **MATRIX live-FormID hand-back not landed.** Per kickoff "NO MATRIX edits beyond Phase 3 hand-back (placeholder live-FormID substitutions per the checklist at MATRIX bottom)" — but with the halt before commits and Phase 3's run incomplete, MATRIX edits aren't appropriate yet. Phase 4's re-run (post-bridge-fix) lands the MATRIX hand-back at that time.

## Known issues / open questions

1. **Phase 4 needed.** The cross-master expansion bug is the headline finding. Confirmed mechanism + reproduction + root cause + 5 fix angles documented above. Phase 4 should land the chosen fix in `RecordReader.cs:ExpandFormLinkValue` + likely matching changes in `Models.cs` (if a new request-side parameter is added) + `tools_records.py` (if the wrapper drives the master-chain inclusion).
2. **Re-run Phase 3 after Phase 4 fix.** Once the cross-master bug is fixed, re-run Scenarios 3.1 (168-record) + 3.2 (50-NPC_) + stress test (full 853 RACE) + cross-product (N×M wider) + perf measurement vs Phase 1 baseline. Without the cross-master fix, perf comparison is invalid (the failing entries return fast-error wrappers, not real expansion).
3. **Tests for cross-master expansion in coverage-smoke / race-probe.** Phase 2's coverage-smoke ran against vanilla Skyrim.esm (single-plugin); the cross-master code path was structurally untested at scaffold time. Phase 4 should add at least one Layer 1.D-style cross-master positive cell using a synthetic two-plugin fixture (one plugin overrides a record + a FormLink chase across plugins). Phase 1 documented this test-coverage gap as known issue #2; Phase 3's live finding is the consumer-signal-confirmed cost.
4. **Does the QUST batch path (`1.P.batch.QUST`) hit the same bug live?** QUST records on Authoria likely also win in mod plugins with FormLinks pointing into Skyrim.esm-originated records (e.g. `DialogConditions` referencing FormIDs). Not tested in Phase 3 because the halt fired first. Worth confirming during Phase 4 re-run that the cross-master fix covers the QUST anchor as well.
5. **Per-axis full coverage of Q1–Q6 against live Authoria not yet captured.** Phase 3 confirmed Q1, Q2, Q3, Q6 live (positive); Q4 (pre-flight) and Q5 (unbounded) not exercised live in this session. Phase 4 re-run should capture all six against live as part of the rerun.

## Conductor asks

```
CONDUCTOR ASK
Phase: 3
Topic: Cross-master FormLink expansion bug — Phase 4 fix angle selection + ship-or-fix decision
Context:
  - Phase 2's known issue #2 (cross-master expansion) confirmed live on Authoria.
  - Bridge's ExpandFormLinkValue (RecordReader.cs:1032) only resolves FormLinks whose originating master happens to be in modCache; modCache typically only contains the parent record's winning plugin.
  - Affects RACE.ActorEffect significantly (most RACEs win in Authoria/Requiem patches; their SPEL FormLinks originate in Skyrim.esm). Scenario 3.1 unrunnable without fix.
  - Doesn't affect NPC_.Factions.Faction in tested batches because batch incidentally included an NPC winning in Skyrim.esm — but would affect NPC_-only batches if no Skyrim.esm-winner is present.
  - Cross-product workaround (`plugin_names=[...]`) works but reshapes response to N×M and requires caller to know the master chain. Not the v2.9.2 ergonomics promise.
Question: Phase 4 fix angle, or ship v2.9.2 with documented limitation?
Suggested options:
  A — Bridge auto-loads master chain (one-level recursion on parent plugin's MasterReferences).
      Simple; covers >95% of real cases; no protocol churn. ~30–60 min Phase 4 work + rerun.
  B — Wrapper passes load-order plugin list; bridge hot-loads on missing-formid.
      More robust but adds protocol surface; covers arbitrary cross-plugin overrides. ~1.5–2 h.
  C — Wrapper passes per-FormID winner-plugin map; bridge loads precisely the winner.
      Surgical; minimal load overhead; protocol churn. ~1.5 h.
  D — Hybrid of A and B (caller param, wrapper-auto-fills from master chain).
  E — Document limitation, ship v2.9.2 as-is, defer fix to v2.9.x.
      Caller workaround: `plugin_names=[<masters>]`. Reshapes response. Defers consumer signal value.
Default if no response in N/A: this is a hard halt; no proceeding without conductor + Aaron decision.
```

## Preconditions for Phase 4

| Precondition | State |
|---|---|
| Phase 2 wrapper baseline preserved | ✅ confirmed via session-start `formids: ["Skyrim.esm:000019"]` probe |
| Live install at v2.9.2 | ✅ `mo2_ping` returns 2.9.2; record index built with no missing-masters |
| Phase 3 confirms cross-master bug surfaced live | ✅ this handoff |
| Phase 3 confirms Q1, Q2, Q3, Q6 locks hold live | ✅ this handoff |
| Phase 4 fix angle selected | ⏳ conductor + Aaron decide between A/B/C/D/E |
| Cross-master test fixture available for Phase 4 regression | ⏳ Phase 1 known issue #2; Phase 4 builds (or use live Authoria probe directly) |

## Files of interest for next phase

| Path | Why |
|---|---|
| `Claude_MO2/tools/mutagen-bridge/RecordReader.cs:996-1054` (`ExpandFormLinkValue`) | Bug B5 root cause; Phase 4 fix lands here. |
| `Claude_MO2/tools/mutagen-bridge/RecordReader.cs:101-185` (`ReadBatch`) | modCache lifecycle; if Phase 4 expands to auto-load masters, this is where the load loop changes. |
| `Claude_MO2/tools/mutagen-bridge/Models.cs` (`ReadBatchRequest`) | If Phase 4 adds a request-side param (option B/C/D), it lands here. |
| `Claude_MO2/mo2_mcp/tools_records.py` (`_handle_record_detail` + `_handle_formids_batch`) | If Phase 4 has wrapper drive the master-chain inclusion, the wiring lands here. |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PHASE_2_HANDOFF.md` § Known issues #2 | Phase 2's recorded cross-master limitation; Phase 3 promotes to confirmed bug. |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | Phase 4 adds at least one cross-master positive cell. |
| `Claude_MO2/tools/race-probe/Program.cs` | Phase 4 may add a cross-master synthetic fixture probe. |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PLAN.md` § Phase 4 | Phase 4's general step structure (probe → fix → regression test → smoke green). |
