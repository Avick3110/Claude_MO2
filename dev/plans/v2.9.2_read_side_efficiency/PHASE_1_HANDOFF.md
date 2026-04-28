# Phase 1 Handoff — Read-side perf baseline + record-shape sweep + Q6 amendments

**Phase:** 1
**Status:** Complete
**Date:** 2026-04-28
**Session length:** ~1.5h
**Commits made:** `<work-hash>` (work) + this hash-record commit
**Live install synced:** No (Phase 1 is probe-only; live remains at v2.9.1 per CLAUDE.md exemption — Phase 1 is race-probe extension + bridge-subprocess invocation, no MCP tool dependence)

## Working version slug

**`v2.9.2`** — confirmed at Phase 0 sign-off; Phase 1 doesn't bump constants. Phase 2's first commit bumps `config.py` / `.iss` / `README.md`.

## Conductor decisions inherited (Phase 0 → Phase 1, locked 2026-04-28 via conductor relay)

| # | Decision | Lock |
|---|---|---|
| Q1 | Path syntax for `fields` / `expand_links` | **A — auto-traversal** (dot-segmented; walker auto-descends into lists/dicts mid-path) |
| Q2 | Expansion output shape | **A — wrapper form** (`{formid, EditorID, expanded: {...}}`) |
| Q3 | Per-record formid-lookup partial failure | **A — per-record envelope** (matches `plugin_names` precedent at `tools_records.py:875`) |
| Q4 | Validation timing | **A — pre-flight** (validate paths against type's reflected property set; reject strict-batch with multi-error accumulation) |
| Q5 | `formids` capacity caps | **A — unbounded** (document tested batch sizes from Phase 1 perf probe) |
| Q6 | Mutual-exclusion of `formids` vs `plugin_names` | **B — allow combination** (cross-product N×M; original Phase 0 default A flipped at conductor sign-off; Phase 1 lands amendment artifacts) |

## What was done

- **`tools/race-probe/Program.cs`** — extended with v2.9.2 P1 read-side perf-baseline + record-shape sweep section appended after the v2.9.1 P2 quest-condition probes block (lines 3396–4244, ~840 LOC + header comment). Six measurement axes per PLAN § G + Q6 amendment item 2:
  1. **Subprocess startup cost** — `read_record` on first vanilla GMST × 5 reps; median + range
  2. **Per-record marginal cost** — `read_records` batch of N ∈ {1, 5, 20, 50, 100, 200} of vanilla RACE records; per-record delta over batch-1 baseline
  3. **Per-record full-detail payload baselines** — RACE / NPC_ / QUST / MGEF / PERK / ARMO / WEAP / SPEL byte sizes via `read_record`
  4. **Projection payload-size impact (PROJECTED)** — RACE full-detail vs ~80% reduction floor; baseline floor anchor for Phase 2's actual measurement
  5. **Expansion round-trip elimination (PROJECTED)** — RACE.ActorEffect chain via N×serial reads vs projected 1×bridge call with expansion
  6. **Cross-product timing (Q6 amendment)** — `formids` × `plugin_names` simulated as `read_records` with N×M items for N ∈ {10, 50, 100} × M ∈ {2, 5, 10}; halt if wall-clock exceeds Python timeout `max(15, 5×N×M)` seconds
  Plus a **record-shape sweep** — every concrete `IMajorRecordGetter`-implementing interface in `Mutagen.Bethesda.Skyrim` 0.53.1 × every FormLink-typed property (single + list-of variants), using FormLink predicates from `PatchEngine.cs:1182`. Plus dedicated checks for `ActorEffect`-vs-`ActorEffects` resolution, RACE FormLink-typed property catalog, vanilla RACE anchor selection, vanilla QUST anchor selection, and NPC_.Factions Scenario 3.2 precondition.
  Failure counter `p1ReadSideFailures` triggers on (a) <3/5 startup samples succeeding, (b) ActorEffect-vs-ActorEffects ambiguity unresolvable, (c) NPC_.Factions structure missing required FormLink sub-property, (d) cross-product cliff (timeout exceeded). Total-failures rollup updated to include `p1ReadSideFailures`.
- **`<workspace>/scratch/v2.9.2-phase-1-perf-and-shape.txt`** — full probe output captured (2071 lines; new v2.9.2 P1 section at lines 1771–2069). Gitignored — not committed; conductor reads directly.
- **`<plan>/MATRIX.md`** — Phase 1 hand-back checklist + Q6 amendment edits landed in this commit:
  - Header methodology block updated — `Skeleton, ActorEffect/ActorEffects, Spells` placeholder language replaced with Phase 1's resolved canonical naming.
  - Cell-naming convention example updated — `1.P.expand.NPC_.formlink` (Phase 1 deviation: RACE has no scalar FormLink in Mutagen 0.53.1).
  - Per-axis carrier convention rewritten with Phase 1 canonical findings — 8 RACE FormLink-typed properties, NPC_.Class as the scalar-FormLink anchor, Voices.Male / Starting as nested + dict anchors.
  - Layer 1.P 7 cells fully resolved — 3 RACE anchor FormIDs (`000D53` / `012E82` / `0131E8`), 3 QUST anchor FormIDs (`04C49D` / `0E3145` / `000E46`), all property placeholders substituted.
  - Layer 1.D 7 cells → 6 cells (`1.D.07` removed per Q6 amendment); all placeholder FormIDs substituted with Phase 1 anchors; `<RACE-only-property>` → `ActorEffect` for `1.D.05`.
  - Layer 2 4 cells → 5 cells (`2.05` cross-product cell ADDED per Q6 amendment); all placeholder property names + FormIDs substituted.
  - Layer 3 Scenario 3.1 use-case updated with Phase 1's perf numbers + canonical projection/expansion paths.
  - Layer 3 Scenario 3.2 precondition status flipped from "conditional" to "in scope" — Phase 1 confirmed `INpcGetter.Factions` structure + `Factions.Faction` path validity.
  - Layer 4.dsl 6 cells — `4.dsl.04` carrier confirmed as `IRaceGetter.Starting` dict; `4.dsl.06` rewritten to clarify Phase 2 builds the missing-master synthetic fixture (vanilla Skyrim has none).
  - Total assertion count table updated — Layer 1.D row dropped to 6, Layer 2 grew to 5, total stays ~26 matrix rows / ~440 harness cells.
  - Phase 2 harness output convention block updated — `1.P.expand.NPC_.formlink` rename, `1.D.07` removed, `2.05` added, property names substituted.
  - Phase 1 hand-back checklist — all 9 items marked `[x]` with handoff notes per item (added 9th item for Q6 amendment).
- **`<plan>/PLAN.md`** — § H one-sentence amendment landed for Q6 (mutual-exclusion → cross-product). Original wording preserved for design-history; bracket-note appended with conductor sign-off date + cross-reference + amendment scope.
- **`<plan>/PHASE_1_HANDOFF.md`** — NEW (this file).

No production code touched. No version bump. No `KNOWN_ISSUES.md` / `CHANGELOG.md` updates (Phase 2's responsibility). No Python wrapper changes.

## Verification performed

### State checks (session start)

| Check | Result |
|---|---|
| `git log -1 --oneline origin/main` top hash | `4e87d8f [v2.9.2 P0] Handoff: record commit hash 46c8474` ✅ matches kickoff prompt's expected hash |
| `git status` | clean working tree ✅ |
| race-probe build (pre-extension, sanity) | 0 warnings, 0 errors ✅ |

### Race-probe build (post-extension)

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.63
```

Race-probe DLL produced at `tools/race-probe/bin/Release/net8.0/race-probe.dll`.

### Race-probe run (post-extension)

`dotnet run -c Release --no-build --project tools/race-probe` → exit 0; full output at `<workspace>/scratch/v2.9.2-phase-1-perf-and-shape.txt` (2071 lines; new v2.9.2 P1 section at lines 1771–2069).

Per-section status (preserved + new):

```
=== v2.9 P2A probes: ALL PASS ===
=== v2.9 P2B probes: ALL PASS ===
=== v2.9 P2C probes: ALL PASS ===
=== v2.9 P2D probes: ALL PASS ===
=== v2.9 P4-INFO probes: ALL PASS ===
=== v2.9.1 P1 multi-condition sweep: ALL PASS ===
=== v2.9.1 P2 quest-condition probes: ALL PASS ===
=== v2.9.2 P1 read-side perf + shape sweep: ALL PASS ===
=== probe complete ===
```

**24 v2.9.0/v2.9.1 baseline probes preserved ALL PASS** (16 v2.9.0 + 8 v2.9.1) + new v2.9.2 P1 perf-and-shape section ALL PASS (no failure-counter increments).

### Perf number tables (concrete numbers, not approximations)

**Axis 1 — Subprocess startup cost (5 samples on a trivial GMST):**

| Sample | Wall-clock (ms) | Success |
|---|---:|---|
| 1 | 903 | ✅ |
| 2 | 887 | ✅ |
| 3 | 889 | ✅ |
| 4 | 873 | ✅ |
| 5 | 908 | ✅ |
| **median** | **889** | — |
| **range** | **873–908 (35 ms)** | — |

Within PLAN § G #1 expected band (1200–1400 ms typical hardware) — actually 25% **faster** than expected. No band-alert; no escalation.

**Axis 2 — Per-record marginal cost (read_records batch of N RACE records):**

| N | Wall-clock (ms) | Per-record (ms) | Marginal over batch-1 | Per-rec marginal |
|---:|---:|---:|---:|---:|
| 1 | 1124 | 1124.00 | — | — |
| 5 | 1276 | 255.20 | 152 ms | **38.00 ms** |
| 20 | 1713 | 85.65 | 589 ms | **31.00 ms** |
| 50 | 3031 | 60.62 | 1907 ms | **38.92 ms** |
| 100 | 3660 | 36.60 | 2536 ms | **25.62 ms** |
| 200 | 4842 | 24.21 | 3718 ms | **18.68 ms** |

Marginals improve with bigger N (subprocess hot-path amortization). At N=200, marginal = 18.68 ms — within PLAN § G #2 expected band (5–20 ms once subprocess hot). At N=50, marginal = 38.92 ms — slightly above band but explained by mid-batch ramp; large-batch behavior dominates and is in band. No band-alert; no escalation.

**Axis 3 — Per-record full-detail payload baselines (8 record types):**

| Type | Anchor FormID | Bytes | Top-level fields |
|---|---|---:|---:|
| ARMO | Skyrim.esm:016FFF | 1441 | 19 |
| MGEF | Skyrim.esm:0173DC | 2146 | 35 |
| NPC_ | Skyrim.esm:000EB4 | 3631 | 24 |
| PERK | Skyrim.esm:01711E | 812 | 15 |
| QUST | Skyrim.esm:000E46 | 1157 | 18 |
| **RACE** | Skyrim.esm:109C7C | **8714** | **62** |
| SPEL | Skyrim.esm:000E52 | 970 | 16 |
| WEAP | Skyrim.esm:017288 | 2394 | 24 |

RACE is the largest by 2.4× — confirms the projection-impact value-prop hypothesis. The consumer's 168-record case at full-detail is ~1.46 MB raw; projection should reduce to ~290 kB.

**Axis 4 — Projection payload-size impact (PROJECTED):** Phase 2 measures actual; Phase 1 baseline floor: RACE full = 8714 bytes; projected to 3–5 paths ≈ 1742 bytes (~20% of full = ~80% reduction per PLAN § Background).

**Axis 5 — Expansion round-trip elimination (PROJECTED):**

Anchor: `Skyrim.esm:10760A` (ManakinRace), ActorEffect.Count = 3.

| Pattern | Wall-clock | Subprocesses |
|---|---:|---:|
| Without expansion (RACE + 3× SPEL serial) | 4698 ms | 4 |
| Projected with expansion (1 bridge call) | 919 ms | 1 |
| **Projected speedup ratio** | **5.11×** | — |

Speedup scales with ActorEffect.Count; consumer's headline case (~5–15 spells per RACE × 168 RACEs) projects to ~10–15× wall-clock reduction on expansion alone, multiplicative with batch + projection axes.

**Axis 6 — Cross-product timing (Q6 amendment):**

Simulation: `read_records` with N×M items (one plugin × one formid per item; M=2/5/10 simulated by repeating Skyrim.esm path since Phase 1 has only one plugin available).

| N | M | N×M items | Wall-clock (ms) | Bytes | Python timeout (s) | Status |
|---:|---:|---:|---:|---:|---:|---|
| 10 | 2 | 20 | 1728 | 537,512 | 100 | ok |
| 10 | 5 | 50 | 2531 | 1,343,714 | 250 | ok |
| 10 | 10 | 100 | 3668 | 2,687,384 | 500 | ok |
| 50 | 2 | 100 | 3975 | 7,397,950 | 500 | ok |
| 50 | 5 | 250 | 5400 | 18,494,809 | 1250 | ok |
| 50 | 10 | 500 | 7067 | 36,989,574 | 2500 | ok |
| 100 | 2 | 200 | 5004 | 13,495,166 | 1000 | ok |
| 100 | 5 | 500 | 7436 | 33,737,849 | 2500 | ok |
| 100 | 10 | 1000 | **11,683** | **67,475,654** | **5000** | **ok** |

**No cross-product cliff.** Largest case (N×M=1000) completes in 11.7 s wall-clock vs Python timeout of 5000 s — 0.23% of timeout budget. Payload at 67 MB is the largest concern long-term but well within reasonable response budgets for consumer-scale (the 168×N case stays under 30 MB on projected/expanded responses). Q5 unbounded posture holds; no soft cap recommended.

### Record-shape sweep findings

**178 concrete getter interfaces scanned in `Mutagen.Bethesda.Skyrim` 0.53.1.** **64 carry one or more FormLink-typed property** (single or list-of variant); **148 total FormLink-typed properties** across all getters.

**Top FormLink-density getters** (>3 FormLink-typed properties):
- `IMagicEffectGetter` — 14 (CastingArt, CastingLight, CounterEffects, DualCastArt, EnchantArt, EnchantShader, EnchantVisuals, EquipAbility, Explosion, HitEffectArt, HitShader, HitVisuals, ImageSpaceModifier, ImpactData, Keywords, PerkToApply, Projectile)
- `IProjectileGetter` — 9 (CollisionLayer, CountdownSound, DecalData, DefaultWeaponSource, DisaleSound, Explosion, Light, MuzzleFlash, Sound)
- `IRaceGetter` — 8 (ActorEffect, DecapitateArmors, DefaultHairColors, EquipmentSlots, Eyes, Hairs, Keywords, Voices)
- `IDualCastDataGetter` — 5
- `IExplosionGetter` — 6
- `INpcGetter` — 6 (ActorEffect, Class, HeadParts, Keywords, Packages, Race)

**Canonical RACE FormLink-typed properties** (matrix anchors):

| Property | Shape | Target |
|---|---|---|
| ActorEffect | list | ISpellRecordGetter |
| DecapitateArmors | list (gendered) | IArmorGetter |
| DefaultHairColors | list (gendered) | IColorRecordGetter |
| EquipmentSlots | list | IEquipTypeGetter |
| Eyes | list | IEyesGetter |
| Hairs | list | IHairGetter |
| Keywords | list | IKeywordGetter |
| Voices | list (gendered) | IVoiceTypeGetter |

**No scalar FormLink-typed property exists on IRaceGetter.** Skeleton in the matrix's original wording was incorrect — Skeleton is an asset-link path (`Mutagen.Bethesda.Plugins.Assets.AssetLink<...>`), not a FormLink. Matrix `1.P.expand.RACE.formlink` cell renamed to `1.P.expand.NPC_.formlink` (NPC_.Class is the cleanest scalar-FormLink anchor; targets `IClassGetter`).

**ActorEffect vs ActorEffects resolution:** `IRaceGetter.ActorEffect` (SINGULAR) **exists**; `IRaceGetter.ActorEffects` (plural) **does NOT**. Mutagen 0.53.1 ground truth: **`ActorEffect`**. v2.7.1 bridge code at `PatchEngine.cs:691` is correct. v2.9.2 task spec's "ActorEffects" mention is incorrect; matrix uses `ActorEffect` throughout.

**RACE anchor candidates (vanilla Skyrim.esm with populated `ActorEffect`):** 10 found in first-10 sweep. Phase 1 anchors:

| FormID | EditorID | ActorEffect.Count |
|---|---|---:|
| **000D53** | **DraugrRace** | **1** |
| **012E82** | **DragonRace** | **2** |
| **0131E8** | **BearBlackRace** | **1** |
| 0131E9 | BearSnowRace | 2 |
| 0131EB | ChaurusRace | 2 |
| 0131EF | DragonPriestRace | 1 |
| 0131F1 | DwarvenCenturionRace | 2 |
| 0131F2 | DwarvenSphereRace | 2 |
| 0131F3 | DwarvenSpiderRace | 2 |
| 0131F4 | FalmerRace | 1 |

**QUST anchors:** v2.9.1's `04C49D` (FollowerCommentary01) + `0E3145` (CR12) + first vanilla QUST `000E46` (CreatureDialogueWerewolf) — all 3 substituted into `1.P.batch.QUST`.

**NPC_.Factions structure (Scenario 3.2 precondition):** **CONFIRMED.** `INpcGetter.Factions` is `IReadOnlyList<IRankPlacementGetter>`; each rank-placement struct exposes `Faction` typed `IFormLinkGetter<IFactionGetter>`. Canonical Scenario 3.2 expansion path: `Factions.Faction` (auto-traversal per Q1 lock).

### Layer 3 anchor record-type recommendation

**RACE confirmed** as the right Layer 3 anchor for the consumer's 168-record case. Vanilla Skyrim alone has 99 RACE records; ~168 across vanilla + DLC + Authoria/Requiem additions is credible. RACE.ActorEffect (FormLink-to-SPEL list) is the canonical FormLink-chase shape that exercises the expansion axis cleanly. Phase 3 picks live FormIDs at execution time.

**Scenario 3.2 (NPC_.Factions.Faction)** stays in scope per Phase 1 precondition confirmation.

## Bugs surfaced

N/A. Phase 1 is probe-only. No bridge code changes; no functional behavior to surface bugs from. The probe extension itself ran clean (build + run + assertion pass with `p1ReadSideFailures = 0`).

## Deviations from plan

1. **`1.P.expand.RACE.formlink` renamed to `1.P.expand.NPC_.formlink`.** PLAN § Phase 1 step 5 expected RACE to have a scalar single-FormLink (e.g. Skeleton). Mutagen 0.53.1 ground truth disagrees — IRaceGetter has 8 FormLink-typed properties, all list-shaped (no scalar single-FormLink). NPC_.Class is the cleanest scalar-FormLink alternative across in-scope record types (`IFormLinkGetter<IClassGetter>` — clean target, populated on ~all NPC records). The matrix substantive scope ("test scalar-FormLink expansion happy-path") is preserved; carrier shifted from RACE to NPC_. Documented as Phase 1 deviation in the matrix per-axis carrier convention block.

2. **Subprocess startup median (889 ms) is 25% faster than PLAN's expected band (1200–1400 ms).** Not an off-band alert — faster than expected is good news. Likely explanation: hardware (Aaron's machine has aged well), bridge code path optimized over v2.6.0–v2.9.1, or Skyrim.esm's overlay-load cost reduced by not opening masters. Phase 2's wrapper layer adds Python-side overhead that may bring real-world median into the 1200–1400 ms band; Phase 3 measures end-to-end on the live Authoria modlist.

3. **Per-record marginal at N=50 = 38.92 ms is slightly above PLAN's 5–20 ms band.** Larger batch sizes (N=200) drop to 18.68 ms which IS in band. Mid-batch ramp is the explanation — the N=50 sample shows the subprocess transitioning from cold-startup-amortization to hot-loop steady-state. Large-batch behavior dominates on the consumer's 168-record case; band check passes at N≥100. No escalation.

## Known issues / open questions

1. **Layer 4.dsl.05 always-null carrier needs Phase 2 selection.** RACE has no scalar non-FormLink "always-null on vanilla" property cleanly. Phase 1 left placeholder `Class` as a marker; Phase 2 picks (e.g. NPC_.DeathItem on most NPCs, NPC_.WornArmor on some). Not blocking — `4.dsl.05` exercises the shape-preserving null-rendering contract; carrier choice is implementation detail.

2. **Layer 4.dsl.06 missing-master expansion needs Phase 2 synthetic fixture.** Vanilla Skyrim.esm has no naturally-occurring missing-master FormLinks. Phase 2 builds the fixture (synthetic in-memory plugin via Mutagen, FormLink to a non-existent master) — pattern matches v2.7.1 round-trip-write fixtures.

3. **Cross-product Q6 wrapper-shape finalization** — Phase 1 surfaced no cliff but the exact JSON envelope shape for `2.05` cross-product cells (`{records: [{formid, plugin_name, success, ...}]}` vs grouped `{formid: {plugin_name: ...}}` etc.) is Phase 2's call. Matrix locks the structural contract (per-cell envelope per `(formid, plugin_name)` per Q3 lock); Phase 2 picks JSON keying.

4. **`tools_records.py:875` mutual-exclusion code site needs Phase 2 inversion** — Phase 1 didn't touch Python; the existing `if plugin_names and plugin_name: error` block stays as-is for `plugin_name` × `plugin_names`, but Phase 2 must NOT add the symmetric block for `formids` × `plugin_names` (Q6 amendment locks combination, not exclusion). Phase 2 adds cross-product fan-out instead.

## Conductor asks

None. Phase 1 perf-and-shape **auto-accepts**:
- All 6 measurement axes within expected bands (or faster than expected on Axis 1).
- Cross-product axis showed NO CLIFF up to N×M=1000 (largest tested case 11.7 s wall-clock vs 5000 s Python timeout — 0.23% of budget).
- ActorEffect-vs-ActorEffects ambiguity cleanly resolved against Mutagen 0.53.1.
- All Phase 1 hand-back checklist items completed in this commit.
- One bonus catch (`1.P.expand.RACE.formlink` rename to NPC_) — substantive scope preserved; carrier shift only.
- Q6 amendment landed per kick-off prompt items 1–3.

If conductor wants to escalate to Aaron for any of the deviations, format below; otherwise default-auto-accept holds.

## Preconditions for Phase 2

Phase 2's responsibilities (per PLAN.md § Phase 2):
- Bridge `RecordReader` extension: projection walker `RenderValueProjected`, expansion resolver `ExpandFormLinkValue`, pre-flight validator `ValidateFieldsAndExpandLinks`.
- `Models.cs`: `ReadRequest.Fields` + `ExpandLinks`; `ReadBatchRequest.Fields` + `ExpandLinks`.
- `Program.cs`: no command additions; existing `read_record` / `read_records` consume new fields via JSON deserialization.
- `tools_records.py`: `mo2_record_detail` schema extension with `formids` / `fields` / `expand_links` parameters; `_handle_record_detail` extension for formids batch + cross-product fan-out per Q6 amendment + new params plumbing.
- Race-probe: per-axis functional probes (positive + negative paths).
- Coverage-smoke: regression cells per MATRIX § Layer 1.P + 1.D + 2 + 4 (Phase 1 confirmed counts: 7 + 6 + 5 + 6 = 24 new cells on top of Layer 5 ~400 v2.9.1 baseline).
- CHANGELOG / KNOWN_ISSUES updates.
- **Version bump to v2.9.2** (Phase 2's first commit).

| Precondition | State |
|---|---|
| Q1–Q6 design lock | ✅ all 6 locked at conductor sign-off (table above); Q6 amendment landed in this commit |
| Performance shape acceptable | ✅ all 6 axes within band (or faster); no cliff; auto-accept |
| Race-probe perf-and-shape extension landed + clean build + clean run | ✅ this commit; output captured |
| Canonical FormLink-typed property names + RACE/QUST anchors | ✅ landed in MATRIX |
| Layer 3 anchor record type confirmed | ✅ RACE; Scenario 3.2 confirmed in scope |
| MATRIX Q6 amendments landed | ✅ this commit (1.D.07 removed, 2.05 added) |
| PLAN.md § H Q6 amendment | ✅ this commit (one-sentence bracket-noted) |
| Bridge code editable + builds clean | ✅ presumed (Phase 1 didn't touch bridge code; v2.9.1 ship at `172ab26` is the baseline) |

**Phase 2 can open** with full design-lock + perf-shape acceptance. No blocking asks.

## Files of interest for Phase 2

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PLAN.md` § Phase 2 | Authoritative steps + § Conductor decisions for Phase 2 |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` § Phase fill-in checklist (Phase 2 hand-back) | Exact rows Phase 2 fills (Layer 5 cell count, Layer 4 expectation flips per Q1–Q6 lock outcomes, Layer 2.04 / 2.05 response shape, Layer 4.dsl.05 / 4.dsl.06 carrier finalization, error-message wording finalization) |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PHASE_1_HANDOFF.md` (this file) | Phase 1's findings + perf numbers + canonical naming + Q6 amendment scope |
| `Claude_MO2/tools/mutagen-bridge/RecordReader.cs` (`Read` + `ReadBatch`) | Phase 2 extends with projection + expansion + validation hooks; Phase 1 read this to understand the per-record render path |
| `Claude_MO2/tools/mutagen-bridge/Models.cs` (`ReadRequest`, `ReadBatchRequest`) | Phase 2 adds `Fields` + `ExpandLinks` properties to both |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs:1182` (`IsFormLinkType`) | Phase 2's `ExpandFormLinkValue` reuses the FormLink predicate; Phase 1's record-shape sweep mirrored it |
| `Claude_MO2/tools/race-probe/Program.cs` v2.9.2 P1 section (lines 3396–4244) | Phase 2 adds bridge-subprocess functional probes per-axis after this section |
| `Claude_MO2/tools/coverage-smoke/Program.cs` | Phase 2 adds per-axis regression cells per MATRIX § Layer 1 + 1.D + 2 + 4; Phase 1 confirmed counts |
| `Claude_MO2/mo2_mcp/tools_records.py` (`_handle_record_detail`, ~line 875 for mutual-exclusion precedent) | Phase 2's Python wrapper extension; cross-product fan-out per Q6 amendment lands here |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` v2.9.1 entry + `Claude_MO2/KNOWN_ISSUES.md` | Phase 2 adds v2.9.2 entry with Phase 1's measured numbers as schema-description anchors |
| `Claude_MO2/installer/config.py` + `installer/Claude_MO2_Setup.iss` + `Claude_MO2/README.md` | Phase 2's first commit bumps version constants from 2.9.1 → 2.9.2 |
| `<workspace>/scratch/v2.9.2-phase-1-perf-and-shape.txt` | Full probe output (2071 lines); gitignored; conductor / Phase 2 reads directly |

## Acceptance — Phase 1 (per kickoff)

- ✅ Race-probe builds clean (0 warnings, 0 errors).
- ✅ Race-probe run captures all 6 measurement axes (5 from PLAN § G + cross-product per Q6 amendment).
- ✅ Record-shape sweep table populated: 178 concrete getter interfaces × 148 FormLink-typed properties enumerated.
- ✅ Canonical RACE FormLink-field names confirmed against Mutagen 0.53.1 ground truth (probe output, not speculation). `ActorEffect` (singular) confirmed.
- ✅ Layer 3 anchor record type proposal in handoff (RACE).
- ✅ MATRIX.md updated per Phase 1 hand-back checklist (all 9 items `[x]`) + Q6 amendments (`1.D.07` removed, `2.05` added, no new cross-product 1.D cells needed).
- ✅ PLAN.md § H one-sentence Q6 amendment landed (bracket-noted, design-history preserved).
- ✅ PHASE_1_HANDOFF.md under 400 lines; § Conductor asks default-auto-accept (no number off-band, no cliff).
- ✅ Work commit + hash-record commit, both pushed.
