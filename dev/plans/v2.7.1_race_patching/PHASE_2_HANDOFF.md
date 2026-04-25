# Phase 2 Handoff — Tier C bracket-indexer dict mutation

**Phase:** 2
**Status:** Complete
**Date:** 2026-04-25
**Session length:** ~1h
**Commits made:** `cb0cc9d` (`[v2.7.1 P2] Tier C — bracket-indexer dict mutation`), pushed to `origin/main` alongside this handoff hash-record commit.
**Live install synced:** No (Phase 2 doesn't sync — Phase 5 does the only live sync of v2.7.1).

## What was done

### Bridge changes (`tools/mutagen-bridge/PatchEngine.cs`)

- **`SetPropertyByPath` rewrite.** The intermediate-segment loop now rejects bracket syntax (`Foo[Bar].Baz`) early with a clear `ArgumentException` — keeps Tier C's "terminal-segment only" scope-lock honest. The final segment is parsed via the new `ParsePathSegment` helper into `(name, key?)`. Two new branches before the existing reflection `SetValue`:
  - `finalKey != null` → `WriteDictEntry` (bracket-indexer write, single dict entry via `set_Item`)
  - `value.ValueKind == JsonValueKind.Object` AND target property is `IDictionary<,>` → `WriteDictMerge` (per-entry merge via the same indexer)
- **`ParsePathSegment(string)`** — splits `"Foo"` → `("Foo", null)` and `"Foo[Bar]"` → `("Foo", "Bar")`. Trims whitespace inside brackets. Throws on `"Foo["`, `"Foo]"`, `"Foo[]"`, `"[Foo]"`, `"Foo[a]b"`. The `"foo[Bar]"` case is allowed (the property name resolves with `IgnoreCase`).
- **`IsClosedDictionary(Type, out keyType, out valueType)`** — detects whether a type IS or IMPLEMENTS a closed `IDictionary<TKey, TValue>`. Catches both the `Dictionary<,>` concrete case (via interface walk) and the `IDictionary<,>` interface-typed case (via direct generic-definition match).
- **`ParseDictKey(string, Type)`** — enum keys via `Enum.Parse(ignoreCase: true)`; string/int/uint/short/ushort/byte/long via invariant culture; everything else → `ArgumentException("Unsupported dict key type ...")`.
- **`GetDictIndexer(object, Type)`** — walks the dict instance's public-instance properties looking for the single-arg indexer whose parameter type equals the expected key type. Strict equality (rather than `IsAssignableFrom`) is correct here — `Dictionary<TKey, TValue>` exposes exactly one such indexer, so there's no ambiguity.
- **`WriteDictEntry(prop, owner, keyText, value)`** — bracket-indexer write. Validates the property is a dict, fetches the dict instance (Mutagen initializes these to empty), parses the key, converts the value via the existing `ConvertJsonValue` against `TValue`, and invokes the indexer.
- **`WriteDictMerge(prop, owner, objectValue)`** — whole-dict merge. Iterates each JSON object member and routes through the same indexer-per-entry path. **Single-path merge regardless of setter presence** — see deviation note below.

All seven new helpers are grouped in their own commented `Tier C — Bracket-indexer dict mutation` section in the source, between `SetPropertyByPath` and `ConvertJsonValue`. Total addition: ~165 lines.

### Smoke harness extension (`tools/coverage-smoke/Program.cs`)

Three new test blocks (Tests 4–6) added after the Phase 1 Tier D tests. Each follows the existing harness shape (build JSON request → pipe to `mutagen-bridge.exe` via stdin → assert response shape). Tests 4 + 5 also do a Mutagen-side readback of the output ESP to confirm what landed.

The harness now picks a Race with populated `Starting + Regen` (the first one — turned out to be FoxRace) and stashes Magicka/Stamina baselines for the preservation assertions. File header + comments updated to reflect Tier C scope.

### No other files touched

- `tools/mutagen-bridge/Models.cs` — unchanged. Tier C is internal-only; no new request/response shape.
- `mo2_mcp/tools_patching.py` — unchanged. Per PLAN.md scope-lock, schema description updates are deferred to Phase 4 (the docs roll-up).
- `tools/race-probe/Program.cs` — unchanged. Phase 0 already verified RACE.Starting/Regen indexer-write API contracts.
- `Claude_MO2/KNOWN_ISSUES.md` — unchanged. The Phase 0 placeholder section already mentions bracket-indexer support coming; Phase 4 finalizes wording.
- `Claude_MO2/mo2_mcp/CHANGELOG.md` — unchanged. Phase 4 finalizes the v2.7.1 entry.

## Verification performed

### 1. Bridge builds clean

```
$ dotnet build -c Release tools/mutagen-bridge
Build succeeded. 0 Warning(s) 0 Error(s)
```

### 2. coverage-smoke: ALL 6 PASS (3 Tier D + 3 Tier C)

Picked race: `109C7C:Skyrim.esm (FoxRace)`.
Source state: `Starting={Health=12, Magicka=0, Stamina=200}`, `Regen={Health=0, Magicka=10, Stamina=5}`.

```
── Test 4: set_fields(Starting[Health]=250) on RACE (expected: success + readback) ──
  exit code: 0
  bridge response: success=true, fields_set=1
  readback: Starting={Health=250, Magicka=0, Stamina=200}
  PASS

── Test 5: set_fields(Regen={Health,Magicka}) on RACE (expected: merge, Stamina preserved) ──
  exit code: 0
  bridge response: success=true, fields_set=1
  readback: Regen={Health=1.5, Magicka=2.5, Stamina=5}
  PASS

── Test 6: set_fields(Starting[Bogus]=100) on RACE (expected: error + rollback) ──
  exit code: 1
  bridge response: success=false, details[0].error="Requested value 'Bogus' was not found.",
                   unmatched_operators ABSENT (set_fields IS a matched handler)
  output ESP not written
  PASS
```

Test 4 confirms bracket-indexer write path: only `Starting[Health]` mutated; `Magicka` and `Stamina` preserved from the source verbatim.

Test 5 confirms whole-dict merge path: `Regen[Health]` and `Regen[Magicka]` updated to the JSON-supplied values; `Regen[Stamina]` preserved at the source value (5). This is the single-path-merge contract — same outcome regardless of whether Mutagen exposes a setter on the property.

Test 6 confirms the error-rollback path goes through `ProcessOverride`'s general catch arm (NOT Tier D's `UnsupportedOperatorException`) — `set_fields` is always a matched handler, so the failure surfaces as a value-conversion error, not an unmatched-operator error. `unmatched_operators` field is correctly absent on the failure response.

Tests 1–3 (Tier D from Phase 1) all still PASS — no regressions.

### 3. race-probe regression check still passes

```
$ dotnet run -c Release --project tools/race-probe
... (all P0 audit blocks pass, round-trip ESP at %TEMP%\AuditProbe.esp = 906 bytes) ...
=== probe complete ===
```

No regression in the Phase 0 audit-verification probe.

## Deviations from plan

### Single-path merge collapse (PLAN.md steps 3 + 4 → one path)

**PLAN.md Phase 2 had two separate paths for dict writes:**
- Step 3 (`ConvertJsonValue` extension): for setter-having dict properties, build a fresh `Dictionary<TKey, TValue>` from JSON object members and `SetValue` it onto the property — **replace semantics**.
- Step 4 (whole-dict against setter-less): mutate the existing dict in place via indexer-per-entry — **merge semantics**.

This produced an asymmetric UX: identical JSON shape (`set_fields: { "Starting": {...} }`) would have replace-vs-merge behavior depending on whether Mutagen happened to expose a setter on the property. PLAN.md's design header explicitly says "Merge semantics, not replace" without qualification, so the asymmetry was a latent inconsistency, not an intentional design.

**Phase 2 collapses into a single path: always merge, regardless of setter presence.** All dict writes (bracket form OR whole-dict form) go through the property's indexer. `ConvertJsonValue` was NOT extended with a `JsonObject` branch — there's no caller that would reach it (the merge branch in `SetPropertyByPath` intercepts first, and `ConvertJsonArray`'s element conversion has no dict-as-element case in v2.7.1). The "build fresh + SetValue" path from PLAN.md step 3 simply doesn't exist in the implementation.

**Confirmed safe by AUDIT.md:** the v2.7.1 `set_fields` targets in scope are `RACE.Starting`, `RACE.Regen`, `RACE.BipedObjectNames` — all three setter-less, all three indexer-only. No current Mutagen dict property in scope for v2.7.1 needs replace semantics, so there's no v2.7.1 use case that this collapse breaks.

**v2.8 implication:** if a future use case wants replace semantics ("clear the dict, then set these keys"), a new operator parameter or a sentinel (`null`-valued JSON object?) would be the v2.8 surface. The carry-over note in PLAN.md § "Carry-overs" already lists "replace-semantics whole-dict assignment" as a v2.8 candidate — this deviation makes that lock explicit in code as well as docs.

User flagged this design call mid-session before I started coding; the architecture call ran through the user, decision was "single-path merge."

### No other deviations

- `ParsePathSegment` helper signature matches PLAN.md's intent. Parser is strict on malformed brackets per PLAN.md.
- `WriteDictEntry` / `WriteDictMerge` use the same indexer-lookup helper (`GetDictIndexer`) — no duplication.
- Intermediate-segment bracket rejection happens via a cheap `IndexOf` check rather than running the full parser per segment (functionally equivalent, slightly tighter).
- I co-located all Tier C helpers in their own labeled section between `SetPropertyByPath` and `ConvertJsonValue` rather than scattering them. Same pattern as Phase 1's "Tier D" section.

## Known issues / open questions

- **No production code changes besides PatchEngine.cs.** Per PLAN.md scope, Tier C is generic and lives entirely in the bridge's reflection layer. No Models.cs surface change, no Python schema change. Phase 4 will document the new syntax in `tools_patching.py`'s `set_fields` description.
- **Tier C does NOT auto-init null dict properties.** If a Mutagen dict property is null at write time, `WriteDictEntry` / `WriteDictMerge` throw "no auto-init path." In practice Mutagen initializes RACE.Starting / Regen / BipedObjectNames to empty (probe-confirmed), so the throw never fires for the v2.7.1 in-scope targets. If a future Mutagen schema introduces a nullable dict-typed property in scope, the auto-init path is a small additive change (mirror the existing intermediate-walk auto-init pattern).
- **Replace semantics for whole-dict assignment** stays as a v2.8 candidate (see the deviation note above and PLAN.md § Carry-overs item 2).
- **Chained dict access (`Foo[Key].Sub`)** stays as a v2.8 candidate (PLAN.md § Carry-overs item 6). The intermediate-segment bracket-rejection in `SetPropertyByPath` enforces this scope-lock explicitly — chained attempts fail with a clear error rather than silently producing wrong behavior.
- **Tier D coverage check is unaffected by Phase 2.** `set_fields` always matches a handler (writes `mods["fields_set"]` regardless of value-conversion outcome inside the loop). Tier D's unmatched-operator check operates on the operator level, not the per-field level. A `set_fields` request that fails value conversion goes through `ProcessOverride`'s general catch arm (verified by Test 6), preserving the existing rollback behavior.

## Preconditions for Phase 3

| Precondition (per PLAN.md) | Status |
|---|---|
| Phase 2 complete (Tier C in place) | ✓ Met |
| Bridge builds clean from Phase 0 + Phase 1 + Phase 2 cumulative changes | ✓ Met (zero warnings, zero errors) |
| coverage-smoke green (Tier D regression + Tier C verification) | ✓ Met (6/6 PASS) |
| race-probe still green (no regression in audit verification) | ✓ Met |
| AUDIT.md still authoritative for Phase 3 wire-up scope | ✓ Met (no rows reclassified by Phase 2) |
| `set_fields` against RACE dict properties works end-to-end | ✓ Met (Test 4 + Test 5 prove it) |

## Files of interest for next phase

Phase 3 wires up the audit-identified (operator, record-type) gaps.

- **`dev/plans/v2.7.1_race_patching/AUDIT.md`** — Phase 3's authoritative scope. Treat the "wire up in P3" rows as the deliverable list (16 pairs across 9 record types per the AUDIT.md summary).
- **`tools/mutagen-bridge/PatchEngine.cs`** — Phase 3 modifies:
  - `GetKeywordsList` (~line 1242 in v2.7.0 baseline; line numbers shifted by Phase 1 + Phase 2 insertions): add 6 new switch arms (Race, Furniture, Activator, Location, Spell, MagicEffect).
  - `ApplyModifications` Race ActorEffect block (parallel to the existing Npc block at ~line 419): 1 new arm.
  - `ApplyModifications` LeveledNpc/LeveledSpell `add_items` blocks (parallel to the existing LVLI block at ~line 578): 2 new arms.
  - All 9 new arms must write `mods[<modskey>]` unconditionally even when count=0 (the Phase 1 Tier D contract).
- **`tools/coverage-smoke/Program.cs`** — Phase 3 adds 16 new test blocks (one per newly-wired (operator, record-type) pair). Existing patterns from Tests 1–6 transfer verbatim. For each new pair:
  1. Pick a representative source record from Skyrim.esm.
  2. Build a `bridge_request` exercising the operator.
  3. Assert response includes the `mods` key with the expected count.
  4. Read back the output ESP via Mutagen, confirm the mutation landed.
- **`tools/race-probe/Program.cs`** — Phase 3 reads it for reference (the API contracts each new wire-up depends on are probe-verified there). No changes expected to race-probe in Phase 3.
- **`PLAN.md` § Phase 3 (lines 488–550)** — exact step-by-step recipe.
- **Phase 3 acceptance:** every "wire up in P3" row in AUDIT.md has a smoke `pass` in the handoff. No `Partial` status — if anything fails, AUDIT.md is updated to reflect the new reality and the handoff explains.

### Where Phase 4 picks up

- `mo2_mcp/tools_patching.py` `set_fields` description gets the bracket-indexer + JSON-object syntax docs (per PLAN.md Phase 4 step 3, third sub-bullet). Phase 2 left the schema description untouched — Phase 4 reconciles the full docs roll-up alongside the Tier B aliases.
- `KNOWN_ISSUES.md` Tier C carry-overs (chained dict access; replace semantics) get the final wording in Phase 4.
- The single-path-merge deviation (this handoff's deviation note) is the "merge-only ships in v2.7.1" doc that PLAN.md Phase 4 § KNOWN_ISSUES references. Phase 4 should crib from this section when finalizing the user-facing wording.
