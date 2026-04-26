# v2.9.0 Phase 1 — Condition-function parameter API contract audit

**Probe binary:** `tools/race-probe/bin/Release/net8.0/race-probe.exe`
**Probe source:** `tools/race-probe/Program.cs` (v2.9 P1 inventory section appended at end; lines 1488–1747 in this commit)
**Probe output:** `<workspace>/scratch/v2.9-phase-1-inventory.txt` (1622 lines)
**Mutagen package:** `Mutagen.Bethesda.Skyrim 0.53.1` (matches the bridge's `PackageReference`)
**Probe exit code:** `0` (clean — 0 v2.7.1 + 0 v2.8 P1 + 0 v2.9 P1 audit failures)
**Date:** 2026-04-26

This audit captures the runtime shape of every concrete `Mutagen.Bethesda.Skyrim.*ConditionData` subclass, categorizes by parameter shape, surfaces the architectural surprises Phase 2's plan-amend needs, and frames the Pareto for Aaron's lock. Phase 2's `RouteParameterSlot` implementation transcribes this contract; it does not speculate.

---

## Inventory totals — 424 concrete *ConditionData

Authoritative count for Mutagen 0.53.1. v2.8.0's "Condition Examples" research note estimated ~157 — the probe's 424 is the audit-time number; CONDITIONS_AUDIT.md is the source-of-truth for v2.9.0 + future v2.9.x point releases.

Per-shape distribution (post-filter; see § Architectural surprises for filter discipline):

| Shape | Count | % | Routable | Notes |
|---|---:|---:|---|---|
| **NoParam** | 219 | 51.7% | trivially (no slots to dispatch) | In-scope-no-op; back-compat preserved per PLAN § C |
| **FormLinkOrIndex** | 113 | 26.7% | yes — `IFormLinkOrIndex<T>` Global-handler pattern | Pareto goldmine |
| **Enum** | 41 | 9.7% | yes — `Enum.Parse(prop.PropertyType, value, ignoreCase: true)` | v2.8.0 ActorValue precedent generalizes |
| **MultiSlot** | 27 | 6.4% | yes — per-slot dispatch via foreach | Includes `Unknown` (3-slot generic-fallback) |
| **PrimitiveOnly** | 11 | 2.6% | yes — direct conversion (`GetInt32`/`GetSingle`/`GetBoolean`) | Strict per PLAN: int/float/bool only |
| **Exotic** | 13 | 3.1% | mixed — see § Sub-A / § Sub-B / § GetEventData | Triaged below |

**Sum check:** 219 + 113 + 41 + 27 + 11 + 13 = 424 ✓

Exotic (13) further triaged: **6 absorb under sub-A** (IFormLink<T> branch, ~30 min Phase 2 cost), **1 absorbs under GetEventData re-triage** (per conductor mid-halt ask, both nested types are System.Enum), **6 defer to v2.9.x** (sub-B — String slots requiring new operator surface).

**v2.9.0 in-scope = 199 dispatcher-wired + 219 in-scope-no-op = 418 total functions in scope.** (199 = 113 FLI + 41 Enum + 27 MultiSlot + 11 Prim + 6 sub-A + 1 GetEventData.) Deferred to v2.9.x: 6 (sub-B, all VariableName/GraphVariable String-slot variants).

---

## Architectural surprises (Phase 2 plan-amend needed)

Five discrepancies with PLAN.md and one Mutagen-schema curiosity. Phase 2's first commit (per v2.8.0 precedent at `407c5e3` / `ca62e44`) is a `[v2.9 plan-amend]` commit folding these into PLAN.md + MATRIX.md before any bridge code lands.

### 1. `Reference` is a BASE property; `Object` is GetIsID's actual function-specific slot

**PLAN.md § Architecture B example wrong.** It uses:
```jsonc
{ "function": "GetIsID", "parameters": { "Reference": "Skyrim.esm:0001A696" } }
```
…but `Reference` is declared on the abstract `Mutagen.Bethesda.Skyrim.ConditionData` base class (not on `GetIsIDConditionData`). Every condition inherits it; it's the slot used for the `RunOnType: Reference` mode (when a condition is targeted at a specific PlacedObject — engine-level dispatch, not function-parameter).

GetIsID's actual function-specific parameter slot is **`Object: IFormLinkOrIndex<IReferenceableObjectGetter>`** (declared on `GetIsIDConditionData`).

Probe evidence (scratch line 642):
```
GetIsIDConditionData anchor — every property annotated [base] / [padding] / [function-specific]:
    Object         [function-specific]    IFormLinkOrIndex<IReferenceableObjectGetter>  (declared on GetIsIDConditionData)
    Reference      [base]                 IFormLink<ISkyrimMajorRecordGetter>           (declared on ConditionData)
    ...
```

**Phase 2 plan-amend:**
- PLAN.md § Architecture B example → use `Object` for GetIsID
- MATRIX.md scenario 3.1 → assertion target is `condition.Data.Object.FormKey`, not `.Reference.FormKey`
- Phase 2 dispatcher schema description (`tools_patching.py`) → list `GetIsID: Object (IFormLink to any record)`
- CHANGELOG entry → name the slot correctly
- KNOWN_ISSUES.md → no change (covers limitations, not function names)

### 2. Skip-list discrepancy — dynamic vs PLAN.md static

PLAN.md § Phase 1 step 2 names a static skip list `{RunOnType, Reference, Function, Unknown1, Unknown2, Unknown3}` for filtering base slots. The probe's dynamic detector (walks `DeclaringType` against `typeof(ConditionData)`) found base props `{Reference, RunOnType, RunOnTypeIndex, UseAliases, UsePackageData}` — 5 instead of 6, with overlapping subset {Reference, RunOnType}.

| PLAN names as base | Status per probe |
|---|---|
| `RunOnType` | ✓ confirmed base |
| `Reference` | ✓ confirmed base |
| `Function` | ✗ NOT base — function-specific on `Unknown` ConditionData (`Function: Condition+Function`); also doesn't exist as a base prop |
| `Unknown1`, `Unknown2`, `Unknown3` | ✗ NOT present anywhere — Mutagen 0.53.1 uses `*Unused*Parameter*` naming convention instead |

| PLAN missed | Status per probe |
|---|---|
| `RunOnTypeIndex` | base, `System.Int32` (alias-index for RunOnType when targeting an alias) |
| `UseAliases` | base, `System.Boolean` |
| `UsePackageData` | base, `System.Boolean` |

**Phase 2 plan-amend:** PLAN.md § Phase 1 step 2 → drop the static skip list, point at this audit doc's dynamic-detector definition. The probe's filter is correct as-is.

### 3. CTDA padding pattern (universal-with-exceptions)

Mutagen 0.53.1's *ConditionData classes uniformly mirror CTDA's 4-parameter binary format, with slots a function doesn't actually use named `*Unused*Parameter*` (e.g. `FirstUnusedStringParameter`, `SecondUnusedIntParameter`, `SecondUnusedStringParameter`). These are never set in practice; the dispatcher's reflection lookup (`condDataType.GetProperty(slotName)`) implicitly ignores them because user `parameters` maps never name them.

**1436 padding slots filtered across 424 functions** (avg 3.4 per function).

| Padding count | Functions | Useful count | Functions |
|---:|---:|---:|---:|
| 4 | 210 | 0 | 219 |
| 3 | 168 | 1 | 171 |
| 2 | 46 | 2 | 32 |
| — | — | 3 | 2 |

The 4-property "universal" shape holds for 400/424 functions. **24 non-uniform exceptions:**
- **GetEventData** (5 properties total: 3 useful + 2 padding)
- **`Unknown`** (5 properties: 3 useful + 2 padding) — a generic-fallback ConditionData with `Function: Condition+Function` enum + 2 Int32 parameter slots; likely Mutagen's forward-compat slot for unknown CTDA function codes
- **22 GetVATSValue\*** functions (3 properties each: varying useful + padding)

The 22 GetVATSValue* exceptions are a separate Mutagen schema curiosity — they have only 3 function-specific properties total (not 4). Doesn't affect Phase 2 dispatch logic; noted here so future contributors aren't surprised.

**Filter rule** (encoded in probe + dispatcher): `IsPaddingSlot(p) := p.Name.Contains("Unused")`. Documented as a fact for Phase 2's schema-doc text and any future v2.9.x contributor. Per the "warning-not-error" rationale in PLAN § C, supplying `parameters: {SecondUnusedIntParameter: 42}` to the bridge would be silently ignored by the reflection lookup (slot name doesn't exist on the type as a non-base, non-padding property) — `condDataType.GetProperty("SecondUnusedIntParameter")` would actually find it (it's a real property), and the dispatcher would route it via the int branch. That's technically a footgun: a typo'd intentional slot name could land on a padding slot. Phase 2 should consider explicitly rejecting `*Unused*` slot names in the dispatcher (one-line guard in RouteParameterSlot) for footgun-prevention.

### 4. `GetActorValuePercentage` doesn't exist — `GetActorValuePercent` is canonical

PLAN.md § Phase 1 floor-AV list named `GetActorValuePercentage`. Mutagen 0.53.1 has only `GetActorValuePercent` (single-letter typo in PLAN). Per conductor mid-halt resolution: drop GetActorValuePercentage from floor-AV, keep GetActorValuePercent (Enum-shape, single ActorValue slot — same precedent as GetActorValue / GetBaseActorValue).

**Phase 2 plan-amend:** PLAN.md § Phase 1 conductor-decisions floor-AV list → `GetActorValuePercent` (one entry, not two).

### 5. `IItemOrListGetter` is a Mutagen union interface — IS routable

`GetItemCount` and `GetEquipped` (both stretch candidates per PLAN.md) use `IFormLinkOrIndex<IItemOrListGetter>` for their respective slots. `IItemOrListGetter` is a Mutagen union interface that any ITEM-or-FORMLIST FormLink can resolve to.

The dispatcher's existing Global-handler pattern doesn't care about the inner T's interface shape — it constructs `FormLinkOrIndex<T>` around the FormKey, where T can be any interface (the bridge's existing `Global` handler at PatchEngine.cs:1657–1667 demonstrates this for `IGlobalGetter`). So `IFormLinkOrIndex<IItemOrListGetter>` IS routable through the same code path with no extension needed. Documented here so Phase 2 doesn't re-discover during implementation.

The same applies to other union/composite getters that surface in the inventory (`ISpellOrListGetter`, `INpcOrListGetter`, `IWeaponOrListGetter`, `IPlacedSimpleGetter`, `IReferenceableObjectGetter`, etc.). All routable through the existing pattern.

### 6. 424-vs-157 count discrepancy

v2.8.0's research note estimated ~157 concrete `*ConditionData` types. The probe's authoritative count is **424**. The earlier estimate was likely based on xEdit's CTDA function code dropdown (which lists ~150–160 commonly-used functions); Mutagen's enumeration includes every function code the CK/engine supports, including DLC-introduced and rarely-used ones. The probe is authoritative for v2.9.0 + future v2.9.x point releases.

---

## GetEventData re-triage decision (per conductor mid-halt ask)

**Decision: ABSORB into v2.9.0 as MultiSlot.**

Probe evidence (scratch lines 653–659):
```
GetEventData re-triage anchor — nested EventFunction / EventMember inspection:
    EventFunction    FullName=Mutagen.Bethesda.Skyrim.GetEventDataConditionData+EventFunction
                     IsEnum=True  BaseType=System.Enum
                     Values: GetIsID, IsInList, GetValue, HasKeyword, GetItemValue (5 total)
    EventMember      FullName=Mutagen.Bethesda.Skyrim.GetEventDataConditionData+EventMember
                     IsEnum=True  BaseType=System.Enum
                     Values: None, Form, Keyword, OldLocation, CreatedObject, Value1, NewLocation, Value2 (8 total)
```

Both nested types are standard `System.Enum` subclasses (not custom Loqui sub-objects, not requiring chained-slot DSL). They route through the existing `Enum.Parse(prop.PropertyType, value, ignoreCase: true)` path with no new code.

The remaining slot, `Record: IFormLink<ISkyrimMajorRecordGetter>`, is covered by sub-A's IFormLink<T> branch (see below).

**Result:** GetEventData becomes a 3-slot MultiSlot in v2.9.0 (Function: enum + Member: enum + Record: IFormLink). ~30 min Phase 2 cost, mostly coverage-smoke cells (the dispatcher logic is already in place once sub-A lands). MultiSlot full count: **27 native + 1 GetEventData absorbed = 28**.

---

## Sub-A absorption — 6 GetVATSValue* with `IFormLink<T>` (NOT IFormLinkOrIndex)

**Decision: ABSORB into v2.9.0 as FormLinkOrIndex (after IFormLink<T> branch lands in RouteParameterSlot).**

The 6 functions:

| Function | Slot | Type |
|---|---|---|
| GetVATSValueCriticalEffect | Value | `IFormLink<ISpellGetter>` |
| GetVATSValueCriticalEffectOrList | Value | `IFormLink<ISpellOrListGetter>` |
| GetVATSValueTarget | Value | `IFormLink<INpcGetter>` |
| GetVATSValueTargetOrList | Value | `IFormLink<INpcOrListGetter>` |
| GetVATSValueWeapon | Value | `IFormLink<IWeaponGetter>` |
| GetVATSValueWeaponOrList | Value | `IFormLink<IWeaponOrListGetter>` |

`IFormLink<T>` is `IFormLinkOrIndex<T>` minus the alias-index half. The Phase 2 extension is a single-branch addition to RouteParameterSlot:
```csharp
// existing:  if (prop.PropertyType is IFormLinkOrIndex<T>)
//                construct FormLinkOrIndex<T>(condData, formKey)
// add:       else if (prop.PropertyType is IFormLink<T>)
//                construct FormLink<T>(formKey)  // simpler — no parent ctor
```

No new operator surface, no MCP request/response shape change. **Fits the pre-auth envelope cleanly.** Total Phase 2 cost: ~15–30 min (one branch + 6 coverage-smoke cells).

After absorption, FormLinkOrIndex shape effectively grows to **113 native + 6 sub-A = 119** in the v2.9.0 in-scope set. (Categorically they're still in the Exotic bucket of the inventory because the type is `IFormLink<T>` not `IFormLinkOrIndex<T>`, but for Pareto and Phase 2 dispatch they're the same logical group.)

---

## Sub-B deferral — 6 String/VariableName functions to v2.9.x

**Decision: DEFER to v2.9.x point release.**

The 6 functions:

| Function | Slots |
|---|---|
| GetGraphVariableFloat | `GraphVariable: System.String` |
| GetGraphVariableInt | `GraphVariable: System.String` |
| GetQuestVariable | `Quest: IFormLinkOrIndex<IQuestGetter>` + `VariableName: System.String` |
| GetScriptVariable | `Target: IFormLinkOrIndex<IPlacedSimpleGetter>` + `VariableName: System.String` |
| GetVMQuestVariable | `Quest: IFormLinkOrIndex<IQuestGetter>` + `VariableName: System.String` |
| GetVMScriptVariable | `Target: IFormLinkOrIndex<IPlacedSimpleGetter>` + `VariableName: System.String` |

**Why defer:** the `String`-typed slots are Papyrus / Behavior-Graph runtime-only identifiers. Write-time validation is impossible (no schema for what variables exist on a quest/script — they're declared in user-authored Papyrus). Routing them needs either:
- A new operator surface (accept-any-string contract — sets a precedent for "trust caller" string slots, breaks the bridge's existing schema-validation posture), OR
- A new MCP request/response shape (round-trip through a Papyrus introspection step to validate the variable name pre-write — way out of scope)

Both options exceed the pre-auth envelope (>1h Phase 2 work + new operator OR new MCP shape). Right call is to defer until a real consumer surfaces and we have signal on the validation contract.

**Phase 2 plan-amend:** PLAN.md § Carry-overs → add new bullet "Sub-B Condition functions with String-typed slots: 6 functions covering Graph/Quest/Script/VM variable lookups. Defer until real consumer surfaces; needs accept-any-string operator-surface decision."

---

## Floor + stretch slot signatures (corrected per ARCH NOTES)

Probe evidence (scratch lines 1099–1126). All floor + stretch confirmed routable, single-slot, with `Object` substituted for `Reference` on GetIsID:

| Band | Function | Shape | Slot | Type |
|---|---|---|---|---|
| FLOOR | **GetIsID** | FormLinkOrIndex | `Object` ★ | `IFormLinkOrIndex<IReferenceableObjectGetter>` |
| FLOOR | GetInFaction | FormLinkOrIndex | `Faction` | `IFormLinkOrIndex<IFactionGetter>` |
| FLOOR | GetInCell | FormLinkOrIndex | `Cell` | `IFormLinkOrIndex<ICellGetter>` |
| FLOOR | HasMagicEffect | FormLinkOrIndex | `MagicEffect` | `IFormLinkOrIndex<IMagicEffectGetter>` |
| FLOOR | HasPerk | FormLinkOrIndex | `Perk` | `IFormLinkOrIndex<IPerkGetter>` |
| FLOOR | HasSpell | FormLinkOrIndex | `Spell` | `IFormLinkOrIndex<ISpellGetter>` |
| FLOOR | GetIsRace | FormLinkOrIndex | `Race` | `IFormLinkOrIndex<IRaceGetter>` |
| FLOOR-AV | GetActorValue | Enum | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` |
| FLOOR-AV | GetBaseActorValue | Enum | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` |
| FLOOR-AV | **GetActorValuePercent** ★ | Enum | `ActorValue` | `Mutagen.Bethesda.Skyrim.ActorValue` |
| STRETCH | GetItemCount | FormLinkOrIndex | `ItemOrList` | `IFormLinkOrIndex<IItemOrListGetter>` |
| STRETCH | IsInList | FormLinkOrIndex | `FormList` | `IFormLinkOrIndex<IFormListGetter>` |
| STRETCH | WornHasKeyword | FormLinkOrIndex | `Keyword` | `IFormLinkOrIndex<IKeywordGetter>` |
| STRETCH | GetEquipped | FormLinkOrIndex | `ItemOrList` | `IFormLinkOrIndex<IItemOrListGetter>` |

★ = corrected per architectural surprises § 1 / § 4.

For the broader 199-function in-scope set, full per-function slot signatures are in scratch under the per-shape detail subsections:

| Shape | Scratch lines | Count |
|---|---|---:|
| Enum | 1136–1219 | 41 |
| FormLinkOrIndex | 1220–1447 | 113 |
| MultiSlot | 1448–1531 | 27 (+ GetEventData absorbed; see § Exotic detail at 1163–1207) |
| PrimitiveOnly | 1532–1554 | 11 |
| Exotic (full detail incl. sub-A + sub-B + GetEventData) | 1162–1207 | 13 |

Phase 2's coverage-smoke harness construction reads slot signatures from the scratch file directly. The dispatcher itself uses runtime reflection — no compile-time per-function table.

---

## Error template confirmation (per PLAN.md § C)

PLAN.md § C names two failure-mode wordings. Both render cleanly with the v2.9.0 in-scope set (199 functions; in-scope-set list elided in actual error message — the bridge can show it on demand or point at a constant URL/file ref to keep the per-record error short):

**Out-of-scope function + supplied `parameters`:**
> `"Condition function 'GetVMScriptVariable' has parameter slots (Target, VariableName) that v2.9 does not yet wire. Authoring this function today produces a structurally-valid but always-false condition. v2.9 in-scope set: see KNOWN_ISSUES.md § v2.9.0 Condition-parameter coverage. Please file a Live Reported Bug if you need this function added."`

**In-scope function + unsupported slot type** (mostly hypothetical post-Phase 2 since IFormLink<T> + IFormLinkOrIndex<T> + enum + int + float + bool covers all 199 in-scope functions; could fire if Mutagen 0.54+ adds a new slot type):
> `"Condition function 'GetIsID' parameter slot 'Object' has type Mutagen.Bethesda.Plugins.IFormLinkOrIndex<Mutagen.Bethesda.Skyrim.IReferenceableObjectGetter> which the bridge doesn't yet route. v2.9 covers IFormLinkOrIndex<T>, IFormLink<T>, enum, int, float, bool. Please file a Live Reported Bug if you need this slot."`

The wording shifted from PLAN.md's draft per the ARCH NOTES — the in-scope set is too long to inline (199 names), so we pivot to a doc reference; and the slot-type list grew to include `IFormLink<T>` per sub-A's absorption.

**Footgun guard recommendation** (Phase 2 decision): the dispatcher should reject `parameters` keys whose names match `*Unused*Parameter*` even though the reflection lookup would technically succeed (those slots ARE properties on the type). Otherwise a typo'd intentional slot name could land on padding silently. One-line guard in RouteParameterSlot.

---

## Pareto framing — counts per option

Aaron locked **Option A (max-band)** at the conductor-relayed mid-halt. Options B/C exist as escape valves only.

| Option | Functions wired | Functions in-scope-no-op | Total in-scope | Phase 2 sub-sessions (est.) |
|---|---:|---:|---:|---:|
| **A — max** ★ Aaron's pick | **199** (113 FLI + 41 Enum + 28 MultiSlot + 11 Prim + 6 sub-A) | 219 NoParam | 418 | ~4 (see § Phase 2 split proposal) |
| B — moderate aggressive | ~60 (top-frequency-weighted slice) | n/a | ~60 | ~2 |
| C — floor + stretch only | ~14 (PLAN.md § Phase 1 baseline) | n/a | ~14 | 1 |

Aaron's directive: "ship the full routable Condition-parameter surface in v2.9.0." Option A.

---

## Phase 2 sub-session split proposal

Conductor expects ~4–5 sub-sessions for 199 functions. Proposing **4** with infra-first by parameter shape (cleanest code path per session, mirrors v2.7.1/v2.8.0 precedent of infra-merged-with-first-feature):

| Sub-phase | Scope | Wired functions | Coverage-smoke +cells (estimate) | Why grouped |
|---|---|---:|---:|---|
| **2A** | Dispatcher infra + all FormLinkOrIndex + sub-A IFormLink<T> branch | 119 (113 + 6) | ~120–240 | Both go through Global-handler pattern; sub-A is one-line extension; biggest single sub-session because infra costs are amortized |
| **2B** | All Enum (carryover ActorValue + 40 others incl. Sex / EquippedItemType / etc.) | 41 | ~80 | Enum.Parse path; v2.8.0 precedent; pure "extend KnownParameterizedFunctions table" once 2A lands |
| **2C** | All MultiSlot + GetEventData | 28 (27 + 1) | ~60–80 | Tests dispatcher per-slot composition; GetEventData (3 mixed-shape slots: 2 nested enums + 1 IFormLink) is the most complex case and exercises the dispatcher's per-slot routing fully |
| **2D** | All PrimitiveOnly | 11 | ~22 | Direct primitive conversion path |

**Notes for the conductor:**
- 2A is the only sub-session that lands new dispatcher code (infra + IFormLink<T> branch). 2B/2C/2D are pure "extend KnownParameterizedFunctions + add coverage-smoke cells" work — much faster per function.
- 2A bumps version to v2.9.0 (per PLAN.md § Conventions: "Phase 2 bumps the version on its first commit"). 2B/2C/2D don't re-bump.
- Per-sub-session handoffs: each ends with PHASE_2A/B/C/D_HANDOFF.md per the standard template.
- Coverage-smoke regression baseline (160 v2.8.0 cells) must stay green across all 4 sub-sessions.
- Layer 3 (Phase 3) doesn't run until all of 2A–2D land.

**Alternative split** if conductor prefers 5 sub-sessions: separate 2A's infra from 2A's FormLinkOrIndex set:
- 2A — Infra + canary functions (1 per shape, ~5 functions, ~10 coverage-smoke cells); validates dispatcher generalizes
- 2B — Remaining FormLinkOrIndex + sub-A (118)
- 2C — All Enum (41)
- 2D — All MultiSlot + GetEventData (28)
- 2E — All PrimitiveOnly (11)

Recommend **4-session split** (the first table). Infra costs are real (RouteParameterSlot helper + ConditionEntry.Parameters field + KnownParameterizedFunctions set + integration into BuildCondition + Models.cs schema) and merging with the largest shape group (FormLinkOrIndex, 119) lets the infra and the cells co-exercise the dispatcher under load. The 5-session canary split adds session-overhead without commensurate risk reduction.

---

## NoParam handling — back-compat per PLAN § C

The 219 NoParam functions need no dispatcher wiring. They were already accepting parameterless `function: "X"` syntax in v2.7.1+ (the existing `Activator.CreateInstance(condDataType)` path) and continue to. KnownParameterizedFunctions does NOT include them — supplying `parameters` for a NoParam function should fire the out-of-scope error per PLAN § C (the function exists but has no parameter slots; either pad the list with these names if Phase 2 wants exhaustive coverage, or rely on the slot-name lookup to surface "no such slot" naturally).

**Phase 2 decision:** simplest is to NOT include NoParam in KnownParameterizedFunctions. Calls like `add_conditions [{function: "GetDead"}]` continue to work (no `parameters`). Calls like `add_conditions [{function: "GetDead", parameters: {Foo: 1}}]` fire the "no such slot" error path naturally (`condDataType.GetProperty("Foo")` returns null). Cleaner than maintaining a 219-name allowlist whose only purpose is to short-circuit the slot-name lookup.

---

## References

- **Probe scratch:** `<workspace>/scratch/v2.9-phase-1-inventory.txt` (1622 lines) — authoritative slot-signature source-of-truth for Phase 2's coverage-smoke harness construction
- **Probe source:** `Claude_MO2/tools/race-probe/Program.cs` lines 1488–1747 (v2.9 P1 inventory section)
- **PatchEngine working precedent:** `tools/mutagen-bridge/PatchEngine.cs` `BuildCondition` (line 1608) — `actor_value` handler at 1631–1645 (Enum.Parse precedent), `Global` handler at 1657–1667 (FormLinkOrIndex<T> ctor pattern)
- **v2.8.0 audit-doc template:** `dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` (this doc mirrors its narrative structure)
- **PLAN.md plan-amend target:** §§ Architecture B (Reference→Object), Phase 1 step 2 (skip-list), Phase 1 conductor decisions (GetActorValuePercentage drop), Carry-overs (sub-B addition)

---

## Bridge SHA snapshot

Phase 1 doesn't touch the bridge. SHA preserved from v2.8.0 ship:
`f998c4e022450633c3a4f3f4e1ee737e6f0f0d8a992c76a3be8efa6d86c8bb04  tools/mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe`

(Recorded for traceability; Phase 2A's first build will produce the new v2.9.0 SHA.)
