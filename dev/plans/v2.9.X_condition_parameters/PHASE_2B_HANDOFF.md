# Phase 2B Handoff — Enum-shape Condition dispatch (41 functions wired) + 2A SKIPs lifted

**Phase:** 2B
**Status:** Complete — all 11 deliverables landed; coverage-smoke green; drift-detection clean; ready for 2C kickoff
**Date:** 2026-04-26
**Session length:** ~3h
**Commits made:** work commit + hash-record commit (this batch)
**Live install synced:** No (Phase 5 owns live sync)

## What was done

- **Enum branch added to `RouteParameterSlot`** (`tools/mutagen-bridge/PatchEngine.cs:1968–1996`, ~17 LOC drop-in between the `IFormLink<T>` branch and the catch-all throw). Pattern: `if (propType.IsEnum) { ValueKind != String guard → Enum.Parse(propType, value, ignoreCase: true) → SetValue → return }`. Wraps `Enum.Parse`'s `ArgumentException` to add function/slot context for caller DX. Doc-comment header at the helper now folds 2A + 2B branches into a unified narrative; catch-all error message refreshed to name covered branches as "IFormLinkOrIndex<T> + IFormLink<T> + System.Enum".

- **`KnownParameterizedFunctions` += 41 Enum function names** (`PatchEngine.cs:1879–1923`) sourced verbatim from `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1136–1219. Comment header anchors the 18 distinct enum types they span (ActorValue / Axis / CastSource / Alignment / FormType / MaleFemaleGender / MiscStatEnum / AdvanceAction + 5 nested types: `GetVATSValueActionConditionData+Action`, `Projectile+TypeEnum`, `CastType`, `TargetType`, `WeaponAnimationType` + `FurnitureAnimType` / `FurnitureEntryType` / `CriticalStage` / `PlayerAction` / `WardState`). Set total: 119 P2A → **160** (113 FLI + 6 sub-A IFormLink + 41 Enum).

- **Bridge build clean** (0 warnings, 0 errors). Stage 1 SHA carried through Stages 2–3 unchanged per kickoff guidance — no rebuild during coverage-smoke / race-probe / docs work since those don't touch bridge code:
  - 2A baseline: `5541734d2c4086a38830547a59cb8751405109c56bfa3b452b48d9c8e3338d23`
  - **2B (this build): `69f699e93200aa85b368f8aa347348830a2ac955aee75cb009a675e99dd3c1d4`**
  - mutagen-bridge.dll: `e79ebfaa6cef4d053ae2f5f665714d6d2d1c2a14696dbc64012a9d98632343b1`

- **Test 287 [2.05] lifted from SKIP to PASS** — back-compat coexistence: record A `actor_value: "Stamina"` (v2.8 path) + record B `parameters: {ActorValue: "Stamina"}` (v2.9 dispatcher) both succeed in a single bridge invocation; both readbacks resolve to ActorValue=Stamina. Proves the v2.8 back-compat sugar path coexists with the v2.9 generic dispatcher across different records flowing through the same `BuildCondition` factory.

- **Test 290 [4.dsl.03] lifted from SKIP to PASS** — the inline canary cell. `parameters: {ActorValue: "Health"}` alone (no `actor_value` field) routes through `RouteParameterSlot`'s Enum branch; readback `GetActorValueConditionData.ActorValue=Health` proves the dispatcher path is distinct from v2.8's back-compat handler. This was the halt-and-report #1 cell.

- **41 Layer 1.P.Enum positive cells added** (Tests 295–335 in `tools/coverage-smoke/Program.cs`):
  - Test 295 — explicit canary cell `[1.P.GetActorValue.MGEF]`, target `Magicka` (deliberately not Health, to stay distinct from Test 290; valid Mutagen 0.53.1 ActorValue member).
  - Tests 296–335 — 40 bulk cells via new `RunEnumDispatcherCell` helper (defined end-of-file). Driven by a `(Function, Slot)` tuple list. Helper picks `Enum.GetValues(propType).Cast<object>().Last()` per cell as the deterministic non-default test value; logs chosen member name + underlying bits per cell so post-Mutagen-upgrade debugging surfaces the specific member instantly without re-running.

- **Layer 1.D / 4.enum cells added** (Tests 336–338):
  - Test 336 [1.D.03 / 4.enum.01] — bad enum name (`"BogusStatThatDoesntExist"`) → record-level Enum.Parse error wrapped with function/slot context. **One cell covers both MATRIX cell IDs** since they specify the same operation; mirrors v2.8.0's 4.slot.02 ↔ 1.D.51 dedup precedent.
  - Test 337 [4.enum.02] — lowercase `"health"` resolves to `ActorValue.Health` (case-insensitive Enum.Parse).
  - Test 338 [4.enum.03] — numeric input (`24`) → JSON-type error per the documented string-only contract.

- **Race-probe v2.9 P2B Enum section added** (`tools/race-probe/Program.cs:1995ff`) — 3 in-process Mutagen-direct probes spanning enum-size variation: large (GetActorValue/ActorValue, 156 members, target Magicka), small (GetIsSex/MaleFemaleGender, 2 members, target Female), tiny (GetAngle/Axis, 3 members, target Z). Each probe simulates the Enum branch inline (Enum.Parse + reflection setter + readback) and asserts round-trip. Total race-probe surface: 7 P2A + 3 P2B = 10 PASS.

- **`tools_patching.py` schema description updated** — appended Enum coverage detail to the `parameters` key: 41-function in-scope set, case-insensitive parse, string-only / numeric-rejected contract, MiscStatEnum + nested-type coverage. Updated the in-scope count from 119 to 160.

- **`KNOWN_ISSUES.md` updated** — `## Condition-parameter coverage (v2.9.0 P2A)` section renamed to `## Condition-parameter coverage (v2.9.0 P2A + P2B)`; lifted "41 enum-typed Condition functions" from gap-list to covered-for with full per-enum-family detail. Section header confirms gap-list now reduced to 2C (MultiSlot 28) / 2D (PrimitiveOnly 11) / sub-B (String 6) / NoParam (219, in-scope-no-op).

- **`mo2_mcp/CHANGELOG.md` updated** — appended 2B content to existing `## v2.9.0 — TBD` entry; no new top-level entry per kickoff directive (single bridge SHA per release; sub-phases bundle into one ship per Phase 5). Top brief, Added — bridge, Architecture, Tests, Documentation sections all extended with 2B narrative; Out of scope dropped the 41 Enum bullet (now covered).

## Verification performed

- **Bridge build:** clean (0/0). Stage 1 SHA `69f699e9…99dd3c1d4` captured and unchanged through Stages 2–3.

- **Race-probe v2.9 P2B section: 3/3 PASS:**
  ```
  === v2.9 P2B — Enum dispatcher functional probes (in-process Mutagen-direct) ===
    [GetActorValue                 ] PASS  Enum ActorValue<ActorValue> (156 members) round-trip → Magicka ✓
    [GetIsSex                      ] PASS  Enum MaleFemaleGender<MaleFemaleGender> (2 members) round-trip → Female ✓
    [GetAngle                      ] PASS  Enum Axis<Axis> (3 members) round-trip → Z ✓
  === v2.9 P2B probes: ALL PASS ===
  ```
  All 7 P2A probes still PASS; total 10/10.

- **Inline canary smoke (Test 290 [4.dsl.03]) — halt-and-report #1:**
  ```
  ── Test 290 [4.dsl.03]: GetActorValue + parameters: {ActorValue: 'Health'} alone (v2.9 P2B Enum dispatcher canary) ──
    source: Skyrim.esm:0173DC (BanishDmgHealthFFTargetActor)
    target: parameters.ActorValue = "Health" (no actor_value field — pure dispatcher path)
    exit: 0
    readback: GetActorValueConditionData.ActorValue=Health ✓ (v2.9 P2B Enum dispatcher canary verified — slot resolved through RouteParameterSlot's Enum branch with ignoreCase: true Enum.Parse, NOT via v2.8's back-compat actor_value handler)
    PASS
  ```

- **Coverage-smoke end-to-end — halt-and-report #2:** **339 cells, 0 FAIL, 4 SKIPs** (all v2.8 baseline carryovers: 1.r.40 OTFT, 1.r.47 SPEL, 1.D.04 CELL, 4.esl.01 ESL). Final run output: `=== smoke complete: ALL PASS ===`.
  - 160 v2.8.0 baseline (156 PASS + 4 carryover SKIPs unchanged).
  - 134 v2.9 P2A (132 PASS + 2 SKIPs lifted to PASS in 2B → 134 PASS).
  - 45 v2.9 P2B new: 1 explicit Enum canary [Test 295] + 40 bulk Layer 1.P.Enum [Tests 296–335] + 3 Layer 1.D / 4.enum [Tests 336–338] + 2 prior SKIPs lifted (Tests 287 + 290).

- **Drift-detection diff:** `git diff 4dab24b -- tools/mutagen-bridge/` shows ONLY `PatchEngine.cs` modified, with changes scoped to:
  - `KnownParameterizedFunctions` HashSet (+41 Enum names + comment header).
  - `RouteParameterSlot` (+Enum branch ~17 LOC + doc-comment update + catch-all error message refresh).
  No other bridge files touched. Other-shape branches (FLI, IFormLink, footgun-guard, DSL-ambiguity check, BuildCondition integration) byte-identical to 2A baseline.

## Bugs surfaced

### Mutagen MiscStatEnum sign-extension / name-resolution anomaly

**NOT a bridge bug** — Mutagen 0.53.1 schema-shape gotcha with downstream implications for any test harness using `.ToString()` or `Convert.ToInt64` round-trip equality on enum slots.

**Repro:** Test 310 [1.P.GetPCMiscStat.MGEF]. The dispatcher writes `MiscStatEnum.AnimalsKilled` (Bethesda CRC32-of-name hash, UInt32 `0xFCDD5011` = 4242362385 unsigned). Mutagen's CTDA binary reader reads the same 4 bytes back as signed Int32, returning `-52604911`. The bit pattern is identical (`0xFCDD5011`); only sign-extension differs.

**Failure mode in test harness:**
- `.ToString()` comparison fails — Mutagen's reader's enum-name lookup misses (sign-extended Int64 doesn't match the enum's positive UInt32 member value), so readback renders as a raw int `-52604911` rather than `"AnimalsKilled"`.
- `Convert.ToInt64()` comparison also fails — `chosenInt = 4242362385` (positive), `readbackInt = -52604911` (sign-extended).

**Fix in 2B:** `RunEnumDispatcherCell` helper compares **lower 32 bits via `unchecked((uint)int64Value)` cast** — handles both signed and unsigned representations uniformly. Trace logs both name and underlying bits when name lookup misses; e.g. Test 310 PASS line:
```
[310] [1.P.GetPCMiscStat.MGEF] PASS  MiscStatEnum.MiscStat=AnimalsKilled(0xFCDD5011) → readback -52604911 via Last() (bit round-trip ✓; Mutagen name lookup misses — see helper note)
```

**Forward-carry guidance for 2C / 2D:**
- 2C MultiSlot's helper for **GetEventData** (3 slots: Function nested System.Enum + Member nested System.Enum + Record IFormLink) — the two enum slots may exhibit the same anomaly if either uses a hash-style or signedness-mismatched underlying type. Use the same lower-32-bit comparison pattern for the enum-shape slots; the Record IFormLink slot uses RunFLIDispatcherCell's two-step `.Link.FormKey` walk.
- 2D PrimitiveOnly's helper — Int32 with negative test values would survive a direct integer comparison; **Single may need bit-reinterpret comparison** (`BitConverter.SingleToInt32Bits`) for NaN / sub-normal precision edge cases (uncommon for Skyrim conditions but worth flagging).
- The Mutagen schema's UInt32-with-Int32-binary-reader pattern surfaces in MiscStatEnum but may surface in other future enum slots if Mutagen 0.54+ defines new enum types with hash-style values. Bit-level comparison is the safe default.

**Bridge dispatcher correctness:** unaffected — the Enum branch's reflection write IS bit-stable. The anomaly is read-side only and harness-only. No bridge code change needed.

## Deviations from plan

- **Test count delta vs kickoff estimate.** Kickoff projected ~47 P2B new cells (~341 total); actual is **45 new cells (339 total)**. 2-cell delta is rational dedup: Test 336 covers both MATRIX cell IDs `1.D.03` and `4.enum.01` (same operation per MATRIX spec — bad ActorValue enum name → record-level error). Mirrors v2.8.0's 4.slot.02 ↔ 1.D.51 dedup precedent. Kickoff-projected counts assumed separate cells for each MATRIX ID.

- **Canary cell sequencing.** Kickoff default sequence positioned canary smoke BEFORE SKIP lifts. I lifted Test 290 [4.dsl.03] AS the canary — the test specification IS the canary (`parameters: {ActorValue: "Health"}` alone via dispatcher), so writing a transient throwaway probe would have been wasteful. Test 287 [2.05] stayed SKIP through halt-1, lifted in Stage 2. Outcome equivalent to kickoff intent (one canary verified before bulk wiring); structure folded the SKIP lift into the canary cell rather than writing a separate probe.

- **Inline canary "Persuasion" → "Magicka" fix.** Initial Test 295 canary cell + race-probe `ProbeEnum` used hand-picked `Persuasion` as the ActorValue target. Mutagen 0.53.1's ActorValue enum has 156 members but doesn't include `Persuasion` (likely a Bethesda data-renaming historical drift — Speech / Speechcraft / Persuasion variants across game eras). Both spots updated to `Magicka` (well-known Skyrim ActorValue, valid in Mutagen 0.53.1). This is exactly the kind of hand-curation pitfall the bulk loop's `Enum.GetValues.Last()` strategy avoids; logged as a cautionary tale for 2C/2D's harness work.

- **`RunEnumDispatcherCell` comparison strategy.** Kickoff §3 said "readback is `prop.GetValue(condData)?.ToString()`". The helper started there but Test 310 [1.P.GetPCMiscStat.MGEF] failed the `.ToString()` comparison due to the Mutagen MiscStatEnum anomaly (see § Bugs surfaced). Final helper compares lower 32 bits via `unchecked uint` cast; trace logs both name and underlying bits for debuggability. The dispatcher's correctness is unchanged; this is harness-side robustness.

## Known issues / open questions

- **Mutagen MiscStatEnum anomaly** (see § Bugs surfaced) — forward-carry guidance for 2C / 2D harness designs documented above.
- **No new architectural surprises.** Enum branch fits the "5–10 LOC drop-in" envelope kickoff predicted (final ~17 LOC including ArgumentException wrapping for DX — modest growth justified by clearer error messages). KnownParameterizedFunctions extension was pure mechanical transcription from scratch.
- **Bonus-catch decisions:** none surfaced; 2B work scoped exactly as kickoff predicted.

## Conductor asks

None — all halt-and-report points landed cleanly:
- Halt-1 (canary green, drift baseline holds, Test 287 deferred to Stage 2): confirmed.
- Halt-2 (coverage-smoke green, drift-detection clean, SHA stability): confirmed.

## Preconditions for Phase 2C

| Precondition | State |
|---|---|
| `RouteParameterSlot` ready for 2C MultiSlot composition (no new branches needed — multi-slot is the dispatcher foreach iterating per-slot through already-landed FLI / IFormLink / Enum branches; 2D PrimitiveOnly will add Int32 / Single / Boolean branches) | ✓ — at `PatchEngine.cs:1937ff`. 2D will insert primitive branches between the Enum branch and the catch-all throw. |
| 2A + 2B branches stable for 2C composition | ✓ — Test 286 [2.03] (SPEL Effects-list × HasPerk via parameters) already proves multi-slot composition works through shared `BuildCondition` factory. GetEventData (3 slots: 2 nested System.Enum + 1 IFormLink) will exercise per-slot routing fully via the 2B Enum branch + 2A IFormLink<T> branch. |
| `KnownParameterizedFunctions` extension pattern reaffirmed | ✓ — 160 names in HashSet. 2C adds 28 names (27 native MultiSlot + GetEventData absorbed); doc-comment at the top of the field anchors the convention. Pattern: append after the P2B 41-name block with a `// ── 28 MultiSlot functions (P2C; scratch lines 1448–1531) ──` header. |
| 2 SKIP-with-reason cells (2A's 287 + 290) — both lifted to PASS in 2B | ✓ — Test 287 [2.05] back-compat coexistence, Test 290 [4.dsl.03] dispatcher canary; both PASS post-2B. |
| Bridge SHA snapshot for 2C drift detection | ✓ — `69f699e93200aa85b368f8aa347348830a2ac955aee75cb009a675e99dd3c1d4` (v2.9.0 P2B). 2C's build SHA must change (28 MultiSlot names added); other-shape behavior (FLI, IFormLink, Enum, footgun-guard) MUST stay byte-identical. |
| `RunEnumDispatcherCell` + `RunFLIDispatcherCell` as templates for `RunMultiSlotDispatcherCell` | ✓ — both helpers in `tools/coverage-smoke/Program.cs` end-of-file. RunEnumDispatcherCell's lower-32-bit comparison pattern is the recommended template for any 2C / 2D slot-readback assertion that crosses the binary serialization boundary. |
| Coverage-smoke Layer 1.P.MultiSlot scaffold ready | ✓ — MATRIX.md § Layer 1.P.MultiSlot rows: 1 GetStageDone explicit + 1 GetEventData explicit + 1 bulk-range covering 26 native. |

## Files of interest for Phase 2C

| Path | Why |
|---|---|
| `tools/mutagen-bridge/PatchEngine.cs:1739–1923` (`KnownParameterizedFunctions` HashSet) | Append 28 MultiSlot names after the P2B 41-name block. Use a `// ── 28 MultiSlot functions (P2C; scratch lines 1448–1531; incl. GetEventData absorbed under sub-A's IFormLink<T> branch) ──` header. Sourced verbatim from scratch lines 1448–1531 + GetEventData detail. |
| `tools/mutagen-bridge/PatchEngine.cs:1937–2080` (`RouteParameterSlot`) | **No new branches needed** — MultiSlot composition routes through the foreach in `BuildCondition` (lines 1681–1685), which calls `RouteParameterSlot` once per slot. Already-landed Enum + IFormLink<T> + IFormLinkOrIndex<T> branches handle GetEventData's 3 slots transparently. 2D will insert Int32 / Single / Boolean branches between the Enum branch and the catch-all throw. |
| `tools/coverage-smoke/Program.cs` end of file (v2.9 P2A + P2B helper definitions) | Add `RunMultiSlotDispatcherCell` helper. Template: combine `RunFLIDispatcherCell`'s two-step `.Link.FormKey` walk + `RunEnumDispatcherCell`'s lower-32-bit comparison; per-slot tuple list of `(Function, Slot, ShapeBranch)`. Add Tests 339–366 [1.P.<Function>.MGEF] for 27 native MultiSlot + 1 explicit GetStageDone + 1 explicit GetEventData. Add Layer 2.06 multi-condition mixed-function cell. |
| `tools/race-probe/Program.cs` end of v2.9 P2B section | Add 2C MultiSlot probe section. Pattern: `ProbeMultiSlot` constructs the full *ConditionData → calls each per-slot inline write (FormLinkOrIndex / FormLink / Enum.Parse depending on slot type) → reads each slot back → asserts per-slot value. GetStageDone (Quest + Stage) is the canonical probe; GetEventData (Function nested-enum + Member nested-enum + Record IFormLink) is the most architecturally interesting (3-slot mixed-shape composition). |
| `dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md` § GetEventData re-triage + § MultiSlot full slot detail | Source-of-truth for 2C's 28-function list. GetEventData's nested System.Enum types route through P2B's Enum branch; Record IFormLink routes through P2A's sub-A branch. |
| `<workspace>/scratch/v2.9-phase-1-inventory.txt` lines 1448–1531 + 1162–1207 | Per-MultiSlot-function full slot detail (27 native) + GetEventData's 3-slot breakdown. 2C's KnownParameterizedFunctions extension reads from here. |
| `mo2_mcp/CHANGELOG.md` v2.9.0 entry | 2C appends to existing `## v2.9.0 — TBD` entry rather than creating a new top-level entry. Pattern: extend Added / Architecture / Tests / Documentation sections with 2C bullets; drop 28 MultiSlot bullet from Out of scope (now covered). |

## Acceptance — Phase 2B (per kickoff)

- ✓ Enum branch in `RouteParameterSlot` lands as ~17 LOC drop-in; uniform reflection write via `Enum.Parse(..., ignoreCase: true)`; numeric-vs-string posture documented (error if not String per kickoff §3).
- ✓ `KnownParameterizedFunctions` += 41 Enum names sourced verbatim from scratch lines 1136–1219.
- ✓ Bridge builds 0 warnings / 0 errors; new SHA `69f699e9…99dd3c1d4` differs from 2A's `5541734d…3338d23`.
- ✓ Inline canary (Test 290 [4.dsl.03]): `parameters: {ActorValue: "Health"}` on a GetActorValue MGEF resolves through new dispatcher; readback `GetActorValueConditionData.ActorValue=Health` proves resolution NOT via v2.8 back-compat path.
- ✓ Coverage-smoke total: 339 cells (160 v2.8 baseline + 134 v2.9 P2A + 45 v2.9 P2B). PASS counts: 156 v2.8 + 134 P2A (with 2 SKIPs lifted to PASS) + 45 P2B = 335 PASS. SKIPs: 4 v2.8 baseline carryovers (1.r.40, 1.r.47, 1.D.04, 4.esl.01). 0 FAIL. (See § Deviations for the 339 vs ~341 estimate dedup rationale.)
- ✓ All 294 v2.9 P2A / v2.8 baseline cells stay green.
- ✓ Race-probe 3 Enum probes PASS (large/small/tiny enum-size variation).
- ✓ Schema description + KNOWN_ISSUES + CHANGELOG updated; CHANGELOG appends to existing v2.9.0 entry.
- ✓ Drift-detection diff confirms changes scoped to `RouteParameterSlot` Enum branch + `KnownParameterizedFunctions` HashSet only; no other bridge files touched.
- ✓ Handoff under 400 lines (this file).

## End-of-phase ritual

Per kickoff:
1. ✓ Final state matches acceptance criteria.
2. ✓ Handoff written per template (this file).
3. ✓ Did NOT write Phase 2C's kickoff prompt — conductor owns that after this handoff lands.
4. ⏳ Force-add new file (next): `PHASE_2B_HANDOFF.md`.
5. ⏳ Push the double-commit chain (next): work commit + hash-record commit.
