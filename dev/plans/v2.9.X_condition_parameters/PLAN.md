# v2.9.X — Other Condition-function parameter slots

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-26, post-v2.8.0 ship.
**Baseline:** v2.8.0 (shipped 2026-04-26).
**Target version:** v2.9.X (working slug — confirm at PLAN review whether this becomes v2.9.0, v2.9.1, or further).
**Sessions estimated:** 5–7 phase sessions plus 1 conductor session running across them. Phase 4 is conditional (skipped if Phase 3 surfaces nothing). Phase 2 may split into 2A/2B if Aaron's Phase 1 Pareto pick exceeds ~12 functions.

**Mandate.** Generalize the v2.8.0 `actor_value` mechanism into a reusable Condition-function-parameter dispatch infrastructure. Cover the high-traffic FormLink-typed and enum-typed parameter slots that real Skyrim patchers (dialog, perk, package, magic-effect conditions) hit constantly — `GetIsID`, `GetInFaction`, `GetInCell`, `HasMagicEffect`, `HasPerk`, `HasSpell`, `GetIsRace`, etc. Today the bridge accepts any function name but leaves parameter slots at `Activator.CreateInstance` defaults (FormID 0, enum index 0) — conditions are structurally valid but functionally always-false. Out-of-scope functions (after Aaron's Phase 1 Pareto lock) get a clean Tier-D-style "parameter not yet supported" error, never silent default-zero.

This is a **single-mechanism, scope-locked** release like v2.8.0's Effects-list addition. ONE new dispatch infrastructure + per-function reflection table; the in-scope function set is locked at Phase 1 sign-off and doesn't grow mid-release.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |
| `<plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.X_condition_parameters\` |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. The conductor session has already told you which phase you are via the kickoff prompt that spawned this session. **Before touching anything**, do this in order:

1. **Confirm your phase.** The conductor's kickoff prompt named your phase. If it didn't, halt and ask the conductor — don't infer it from the handoff numbering yourself (the conductor owns phase identification).

2. **Read the previous handoff** in full (if any). The conductor's kickoff prompt named which one. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, conductor decisions relevant to your phase, and what to write in your own handoff. **Do not read other phases' sections** — they're scoped to other executors and consume context for no benefit.

4. **Read `MATRIX.md`** in this directory if your phase needs it (see your phase section). Phase 0 produces it; Phases 1–5 use it as the authoritative test specification. Phase 1 updates it post-Pareto-lock; Phase 2 onward reads the updated form.

5. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry
   - `Claude_MO2/KNOWN_ISSUES.md`
   - **Skip** the session-summaries / handoffs sweep — your phase section is your roadmap, the conductor handles cross-phase orientation.
   - Check `<workspace>/Live Reported Bugs/` root for anything new.

6. **Confirm phase identity + work plan with the user (Aaron) before any code changes.** Wait for go-ahead.

7. **At the end of your phase**, write `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` if Phase 4 spawns a sub-session) in this directory using the template at the bottom of this file. **Do not write the next phase's kickoff prompt** — the conductor owns that.

**One phase per session.** If you finish early, summarise and stop — don't roll into the next phase.

### Communicating with the conductor

The conductor session is a separate Claude Code session orchestrating this plan. It runs between phases (reading your handoff, writing the next phase's kickoff). If your phase needs guidance the plan doesn't already give you (scope ambiguity, an unexpected probe result that changes the dispatch architecture, a bridge bug that needs Aaron's call to absorb-vs-defer), write a short note to the conductor.

**Token-efficient format** — bullets, no prose, no transcripts:

```
CONDUCTOR ASK
Phase: N
Topic: <one-line topic>
Context: <2-3 bullets max>
Question: <single specific question>
Suggested options: <A/B/C with one-line rationale each>
Default if no response in <X minutes>: <whatever the executor will do absent guidance>
```

Drop this at the bottom of your in-progress handoff under § Conductor asks. The conductor reads it, summarizes for Aaron if needed, and either replies (you proceed with that answer) or escalates to Aaron (who replies via the conductor or interjects directly).

---

## 📋 Background — why this plan exists

v2.8.0 wired the `actor_value` parameter on the GetActorValue family of Condition functions — single `Enum.Parse<ActorValue>` reflection write into the Mutagen `ConditionData` subclass. Scope was deliberately locked to ActorValue; the broader gap (FormLink-typed and other enum-typed parameter slots on ~30 other condition functions) was punted to v2.9 as the most "you'll definitely hit it" gap of the carry-over set.

The shape of the gap:

- Condition functions in Mutagen are dispatched per-function via `Mutagen.Bethesda.Skyrim.{Function}ConditionData` — a concrete class per function with parameter slots as public properties.
- Examples: `GetIsIDConditionData.Reference: IFormLinkOrIndex<ISkyrimMajorRecordGetter>`, `GetInFactionConditionData.Faction: IFormLinkOrIndex<IFactionGetter>`, `HasMagicEffectConditionData.MagicEffect: IFormLinkOrIndex<IMagicEffectGetter>`, `GetIsRaceConditionData.Race: IFormLinkOrIndex<IRaceGetter>`, etc.
- v2.8.0's `BuildCondition` already proves the `IFormLinkOrIndex<T>` construction pattern (the existing `Global` handler — FormKey + parent `ConditionData` ctor). The v2.9 work generalizes this from one-special-case-per-slot to one-generic-router.
- Bridge today: any call with `function: "GetIsID"` (or any other parameterized function) succeeds at the structural level — the ConditionData is constructed via `Activator.CreateInstance` — but every parameter slot is left at default (FormID 0 = no record, enum index 0). The condition is structurally valid in the output ESP but functionally always-false. Bridge does not error.
- Real patchers (Authoria/Requiem-style modlists with thousands of dialog/perk/package conditions) hit this fast. The v2.7.1 → v2.8.0 → v2.9 progression is the standard "one capability surface per release" cadence, and Condition-function parameters is the highest-demand remaining gap per the v2.8.0 post-ship limitation review.

The fix is **medium-scoped**: the dispatch infrastructure is new code (one generic mechanism), but once it exists, adding functions is cheap (one row in a table per function). Phase 2 lands the infrastructure + the in-scope set Aaron picks at Phase 1; future v2.9.x point releases can extend the table cheaply if more functions surface.

---

## 🏗️ Architecture — Condition-parameter dispatch (locked)

### A. Dispatch model — generic-by-slot-type, not per-function table

The bridge's existing `BuildCondition` (PatchEngine.cs:1608) already routes `RunOnType`, `ActorValue`, and `Global` by reflection-property-name on the `{Function}ConditionData` instance. v2.9 generalizes this:

```
For each (slotName, value) in conditionEntry.Parameters:
    var prop = condDataType.GetProperty(slotName);
    if prop == null:
        throw "Function {function} has no parameter slot named '{slotName}' on its Mutagen ConditionData."
    Route value to prop based on prop.PropertyType:
        - IFormLinkOrIndex<T>      → existing Global-handler pattern (FormKey + ctor(condData, key))
        - IFormLink<T>             → simpler ctor: FormLink<T>(formKey) (no parent — sub-A absorption per CONDITIONS_AUDIT.md)
        - Enum (any)               → Enum.Parse(prop.PropertyType, value, ignoreCase: true)
        - Int32 / Single / Boolean → direct conversion
        - Anything else            → "parameter slot type not yet supported" error
```

This is generic-by-slot-type, NOT a per-function dispatch table. Adding a new function to v2.9 scope is purely a probe + matrix update + coverage-smoke cell — no new bridge code.

**Per-function table is the wrong abstraction here.** Mutagen's `{Function}ConditionData` types use uniform reflection conventions (slot name = property name, type drives marshalling), and the slot types are a small, closed set (`IFormLinkOrIndex<T>`, `IFormLink<T>`, enum, int, float, bool). A per-function table would re-encode information already present in the type system. The generic router reads what's there.

**Hybrid kept:** the v2.8.0 `actor_value` JSON field stays as syntactic sugar for `parameters: {ActorValue: ...}`. Both forms route through the same dispatch. If both are supplied for ActorValue, the bridge errors (unambiguous DSL). The existing `global` JSON field stays as-is — different DSL (FormID string + comparison operator on the Condition itself, not on ConditionData), kept for back-compat.

### B. Schema — `parameters: {SlotName: Value}` JSON object

ConditionEntry gains a new optional field:

```jsonc
{
  "function": "GetIsID",
  "operator": "==",
  "value": 1,
  "parameters": {
    "Object": "Skyrim.esm:0001A696"      // IFormLinkOrIndex<IReferenceableObjectGetter> — GetIsID's function-specific slot is `Object`, not `Reference` (Reference is a base prop, used for RunOnType: Reference mode). See CONDITIONS_AUDIT.md § Architectural surprises §1.
  }
}
```

Multi-slot example (e.g. `GetStageDone(Quest, Stage)`):

```jsonc
{
  "function": "GetStageDone",
  "operator": ">=",
  "value": 1,
  "parameters": {
    "Quest": "Skyrim.esm:00021555",
    "Stage": 50
  }
}
```

SlotName = Mutagen reflection property name. Documented in `tools_patching.py` schema description as a function → required-slot lookup table for the in-scope set; out-of-scope functions get a "see KNOWN_ISSUES" pointer.

### C. Out-of-scope handling — Tier-D-style, not silent default

Two failure modes, both surface as per-record `details[].error`:

1. **Function in v2.9 in-scope set, but slot type the dispatcher can't route** (e.g. a multi-FormLink slot type that doesn't match `IFormLinkOrIndex<T>` shape). Error: `"Condition function '{function}' parameter slot '{slotName}' has type {slotType} which the bridge doesn't yet route. v2.9 covers IFormLinkOrIndex<T>, enum, int, float, bool. Please file a Live Reported Bug if you need this slot."`
2. **Function NOT in v2.9 in-scope set, user supplies `parameters` anyway** (or omits it but the function has known parameter slots). Error: `"Condition function '{function}' has parameter slots ({list}) that v2.9 does not yet wire. Authoring this function today produces a structurally-valid but always-false condition. v2.9 in-scope set: {list}. Please file a Live Reported Bug if you need this function added."`

Detection: bridge's `BuildCondition` consults a v2.9-frozen `KnownParameterizedFunctions` set (built from Phase 1's probe). Functions in the set are dispatched. Functions NOT in the set, when called, log a warning to the per-record details (does NOT block — back-compat for callers who knowingly use parameterless functions like `GetLevel`). Functions NOT in the set + caller supplied `parameters` → hard error.

**Why warning-not-error for unknown functions called without `parameters`:** v2.7.1 already accepts any function name with default param slots — breaking that would be a back-compat regression. The warning surfaces the silent-default risk; the hard error fires only when caller signals intent (supplied `parameters`) for a function the bridge can't route.

### D. Scope locks

- **One new mechanism only.** Generic Condition-parameter dispatch via reflection. No other new operators, no other `set_fields` shape changes, no new `op:` values.
- **In-scope function set locked at Phase 1.** Aaron picks the cluster from Phase 1's full inventory. Hard floor proposal: GetIsID, GetInFaction, GetInCell, HasMagicEffect, HasPerk, HasSpell, GetIsRace + ActorValue carryover. Stretch if Phase 1's Pareto evidence is favorable: GetItemCount, IsInList, WornHasKeyword, GetEquipped.
- **Slot types covered by the dispatcher**: `IFormLinkOrIndex<T>`, enum (any), `Int32`, `Single`, `Boolean`. Anything else → "not yet routed" error per § C.
- **Back-compat:** all v2.8.0 condition usage (parameterless functions, `actor_value`, `global`, ConditionFloat, ConditionGlobal) continues to work unchanged. The 22 pre-v2.8.0 + 138 v2.8.0 coverage-smoke tests must all stay green.
- **Probe-first discipline.** Phase 1 starts with the inventory probe before anything else lands. Phase 2 transcribes the verified slot contracts; doesn't speculate.
- **Bonus-catch precedent.** If a phase fix surfaces a related latent issue in the touched code, fold in (with explicit handoff documentation), per v2.7.1/v2.8.0 pattern. >1h additional work or new operator surface → halt, ask conductor.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive.

### E. Conductor decisions (cross-phase, locked at PLAN write-time)

Things the conductor enforces or decides between phases without re-litigating:

- **Phase identification.** Conductor identifies current phase from highest-numbered handoff in `<plan>/`. Phase executors don't self-identify.
- **Pareto sign-off.** Phase 1's executor proposes the in-scope set; conductor relays to Aaron for explicit lock. Phase 2 doesn't begin until the lock is in.
- **Phase 2 split decision.** If Aaron's Pareto pick exceeds ~12 in-scope functions, conductor splits Phase 2 into 2A (infrastructure + first ~7 functions) and 2B (remaining functions). Trigger threshold is rough; conductor uses judgment based on Phase 1's per-function complexity findings.
- **Phase 4 spawn decision.** If Phase 2 + Phase 3 surface zero bridge bugs and zero matrix corrections, conductor skips Phase 4 directly to Phase 5. Otherwise spawns Phase 4 (single session, items 1–N model from v2.8.0 P4) or Phase 4 sub-sessions per bug if items don't fit one budget.
- **Live install sync timing.** Phase 1 + 2 don't touch live. Phase 3 reads via `mo2_create_patch` against live (test patches in `<modlist>/mods/Claude Output/`, deleted after). Phase 4 syncs to live only if a fix needs verification on the live install. Phase 5 syncs once and ships. Conductor confirms sync state before each Phase 3 / 4 / 5 kickoff.
- **Schema migration vs additive.** v2.9 is purely additive. No deprecation of existing fields. Conductor rejects any phase proposing a schema break.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Plan + matrix specification + record selection | `MATRIX.md` (NEW); `PHASE_0_HANDOFF.md`; PLAN.md force-added | None |
| 1 | Inventory probe + Pareto proposal + Aaron sign-off | `tools/race-probe/Program.cs` extended with full ConditionData inventory dump; `PHASE_1_HANDOFF.md` with Pareto evidence; `MATRIX.md` updated post-lock with in-scope function cells; `CONDITIONS_AUDIT.md` (NEW — recommended; mirrors v2.8.0's EFFECTS_AUDIT.md) | Phase 0; Aaron's Pareto lock via conductor relay |
| **2** | **Bridge dispatch infrastructure + functional probes + coverage-smoke regression cells** | `PatchEngine.cs` `BuildCondition` extended with generic slot dispatch; `Models.cs` `ConditionEntry.Parameters` field; `race-probe` per-in-scope-function functional probes; `coverage-smoke` +N regression cells; `tools_patching.py` schema; CHANGELOG; `KNOWN_ISSUES.md`; **version bump to v2.9.X** (Phase 2's first commit, since this is the first phase landing user-facing capability) | Phase 1 with Pareto lock |
| 3 | Workflow scenarios on live | Per-scenario assertions in `PHASE_3_HANDOFF.md`; bug list extended | Phase 2 |
| 4 | Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL — conductor decides) | `PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` for sub-sessions); code commits; regression tests | Phase 3 with surfaced findings |
| 5 | Re-run + ship v2.9.X | Final smoke run; installer + bridge artifact rebuilt; live sync; tag pushed; `gh release create`; memory updated | Phase 4 (or Phase 3 if Phase 4 skipped) |

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one or more commits per its scope. Commit messages start with `[v2.9 PN]` (e.g. `[v2.9 P2] Generic Condition-parameter dispatch + N functions in-scope`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its handoff via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. **Phase 2 bumps the version** on its first commit (Condition-parameter capability is the trigger). Subsequent phases don't re-bump. The version slug (`v2.9.0` vs `v2.9.1` vs further) is confirmed at PLAN review.
- **Live install sync:** Phases 0, 1, 2 do not touch the live install. Phase 3 reads via `mo2_create_patch` against the live install. Phase 4 fix sessions live-sync only when the bug requires verification on the live install. Phase 5 live-syncs once and ships.
- **Probe-first discipline:** Phase 1 starts with the inventory probe. Any Phase 4 fix that touches PatchEngine.cs's reflection paths or dispatch logic begins with a probe demonstrating the failure mode.
- **One phase per session, with conductor-mediated handoff between phases.**
- **Don't touch out-of-phase files.** Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves you spot during work.
- **No changes to MCP tool request/response shapes** unless a Phase 4 fix requires it (and even then, prefer additive changes — new fields are safer than rename/restructure). Phase 2 adds capability via the existing `add_conditions` field; no shape change beyond a new optional `parameters` member on each condition entry.
- **Double-commit cadence per phase** (work commit + hash-record commit), matching v2.7.1/v2.8.0.

---

## 🔁 Handoff template

Every phase ends by writing `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md`) in this directory. **Do not write the next phase's kickoff prompt — the conductor owns that.** Use this exact structure:

```markdown
# Phase N Handoff — <one-line summary>

**Phase:** N (or "4 — <slug>")
**Status:** Complete | Partial | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** <hashes or "none">
**Live install synced:** Yes/No (path: ...)

## What was done
<Bulleted list of concrete changes — file paths + one-line descriptions.>

## Verification performed
<What tests / smoke checks ran. What evidence shows it worked. For Phase 1: probe output + Pareto evidence. For Phase 2: per-in-scope-function smoke results + coverage-smoke counts. For Phase 3: per-scenario assertion checklist + readback evidence. For Phase 4: probe evidence pre-fix + post-fix.>

## Bugs surfaced (Phase 2, Phase 3 only)
<Per-bug entry: short slug; record type + operator; reproduction; failure mode; proposed fix angle.>

## Deviations from plan
<Anything you did differently from PLAN.md. Why. If you didn't deviate, write "None.">

## Known issues / open questions
<Bugs you found but didn't fix (with reason). Questions the next phase needs to answer. If none, write "None.">

## Conductor asks
<Optional. Use the format from § Communicating with the conductor at the top of PLAN.md. If none, omit.>

## Preconditions for next phase
<Confirm each precondition the next phase requires per PLAN.md. Flag any not met.>

## Files of interest for next phase
<List paths the next phase will most need to read.>
```

Keep handoffs short — under 400 lines.

---

# PHASES

---

## Phase 0 — Plan + matrix specification + record selection

**Goal:** Produce `MATRIX.md`, the per-cell test specification scaffolding for v2.9. Pre-spec Layer 1 / 2 / 4 cells against vanilla Skyrim.esm and Layer 3 workflow scenarios against the live Authoria modlist. **No production code changes. No version bump.** Phase 1's inventory probe will populate the per-function cell rows after Aaron's Pareto lock; Phase 0 lays down the layer structure and the cell-naming convention.

**Files to touch:**
- `<plan>/PLAN.md` (this file — force-add)
- `<plan>/MATRIX.md` (NEW)
- `<plan>/PHASE_0_HANDOFF.md` (NEW — written at end)

**Conductor decisions relevant to this phase:**
- The version slug for `v2.9.X` is decided at PLAN review (this phase). If Aaron hasn't decided yet, Phase 0 records the working slug and notes the decision is open; Phase 2 commits the actual version bump.
- Phase 0 does not touch the in-scope function list — that's Phase 1's deliverable post-Pareto-lock.

### Steps

1. **Verify session start.** Confirm `origin/main` is at v2.8.0 ship commit (the conductor's kickoff prompt will name the exact hash) and clean. Live install at `<live>` running v2.8.0 (`mo2_ping` returns `version: "2.8.0"`).

2. **Draft `MATRIX.md`** with the four-layer scaffold mirroring v2.8.0's MATRIX.md but anchored on Condition-parameter cells:
   - **Layer 1 — Per-function coverage (positive cases).** One row per in-scope function × test-record-type. Per-row columns: cell ID, function, parameter slots, source record, operation, expected. Phase 1 fills in the rows post-lock; Phase 0 writes the column headers + naming convention (e.g. `1.P.GetIsID.MGEF`, `1.P.GetInFaction.PERK`).
   - **Layer 1.D — Per-function negatives + out-of-scope errors.** One row per in-scope function with a deliberately-bad parameter (bad FormID, bad enum name) → expect record-level error with named slot. Plus one row per out-of-scope function exercising `parameters` → expect "not yet wired" error.
   - **Layer 2 — Combinatorial.** Multi-parameter functions (e.g. GetStageDone with both Quest + Stage). Multiple conditions per record with mixed in-scope functions. Effects-list-with-in-scope-conditions composition (v2.8.0's per-effect Conditions surface + v2.9's slot dispatch).
   - **Layer 3 — Workflow scenarios on live.** 2 scenarios (dialog GetIsID condition + perk HasPerk/HasSpell condition). Phase 0 names the scenarios + describes the patcher use case; Phase 3 picks the live FormIDs at execution time.
   - **Layer 4 — Edges.** Empty `parameters` object (no slots) on a function known to need them → warning-or-error per § C above. Both `actor_value` AND `parameters: {ActorValue: ...}` supplied → unambiguous DSL error. Unknown SlotName for in-scope function → "no such slot" error.
   - **Layer 5 — Regression.** All 160 v2.8.0 coverage-smoke cells run unchanged.
3. **Pre-spec Layer 3 workflow scenarios** with placeholder FormIDs from the live modlist that Phase 3 will swap. Anchor on dialog + perk use cases per task spec.

4. **Force-add PLAN.md and MATRIX.md.** `git add -f Claude_MO2/dev/plans/v2.9.X_condition_parameters/{PLAN.md,MATRIX.md}`.

5. **Write `PHASE_0_HANDOFF.md`** confirming MATRIX scaffold landed, Layer 3 scenarios pre-spec'd, no production code touched, no version bump. Record the working version slug + open-or-decided status.

6. **Commit** (double-commit cadence):
   - Work commit: `[v2.9 P0] Plan + matrix scaffold + record selection`
   - Hash-record commit: `[v2.9 P0] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 0

- `MATRIX.md` exists with four-layer scaffold + cell-naming convention. Per-function rows are placeholders awaiting Phase 1's Pareto lock.
- Layer 3 scenarios named with use-case descriptions; live-FormID picks deferred to Phase 3.
- `git diff main^` shows: PLAN.md (new), MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched.
- Working version slug recorded in handoff.

---

## Phase 1 — Inventory probe + Pareto proposal + Aaron sign-off

**Goal:** Enumerate every concrete `*ConditionData` subclass in Mutagen 0.53.1 with its non-base reflection slots (slot name + type). Categorize by parameter shape (`IFormLinkOrIndex<T>`, enum, int, float, bool, multi-param, exotic). Propose a Pareto cluster for v2.9 in-scope set anchored on real Skyrim patcher use (dialog/perk/package/magic-effect conditions). Surface to Aaron via the conductor for an explicit lock; update `MATRIX.md` post-lock with the in-scope function cells. **No bridge code changes.** **No version bump.**

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (extend with inventory dump section + per-function probe utilities for Phase 2 to reuse)
- `<plan>/CONDITIONS_AUDIT.md` (NEW — recommended; mirrors v2.8.0's EFFECTS_AUDIT.md role)
- `<plan>/MATRIX.md` (update post-Pareto-lock with per-in-scope-function cells)
- `<plan>/PHASE_1_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- The Pareto sign-off is mandatory before Phase 2 begins. Phase 1 ends with the proposal; Phase 1's executor writes the proposal to its handoff under § Conductor asks (per the format at the top of PLAN.md). Conductor relays to Aaron, gets the lock, updates MATRIX.md (or asks Phase 1 to update it before closing the session — conductor's call based on context budget).
- Floor proposal Phase 1 starts from: **GetIsID, GetInFaction, GetInCell, HasMagicEffect, HasPerk, HasSpell, GetIsRace** + the v2.8.0 ActorValue carryover treated as already-in-scope (the dispatcher generalizes the existing `actor_value` handler). Stretch candidates if Pareto evidence supports: GetItemCount, IsInList, WornHasKeyword, GetEquipped.
- If the inventory probe surfaces something architecturally unexpected — e.g. a slot type beyond the 5 types the dispatcher § C plans to cover (`IFormLinkOrIndex<T>`, enum, int, float, bool), or a function whose ConditionData uses a non-uniform reflection convention — Phase 1 documents it in `CONDITIONS_AUDIT.md` and writes a `CONDUCTOR ASK` for whether to expand the dispatcher's slot-type coverage in Phase 2 or punt to a later release.

### Steps

1. **Read `MATRIX.md`** to understand the cell shape Phase 1 needs to fill in post-lock.

2. **Extend `tools/race-probe/Program.cs` with an inventory dump section** appended after the existing v2.8 P4 sections:
   - Enumerate every concrete `Mutagen.Bethesda.Skyrim.*ConditionData` (filter: class, not abstract, not `*BinaryOverlay`, not interface). v2.8.0's Condition Examples research note ~157 condition data types — Phase 1 produces the exact authoritative list.
   - For each: print function name (= type name minus `ConditionData` suffix), every public-instance non-base property — base-prop detection is **dynamic** (walk `DeclaringType` against `typeof(ConditionData)`, not a static skip list per CONDITIONS_AUDIT.md § Architectural surprises §2). Apply CTDA padding-slot filter `IsPaddingSlot(p) := p.Name.Contains("Unused")` per CONDITIONS_AUDIT.md § Architectural surprises §3. The actual base prop set the dynamic detector finds is `{Reference, RunOnType, RunOnTypeIndex, UseAliases, UsePackageData}` — different from PLAN's original static guess; the audit captures the discrepancy.
   - Categorize each function by parameter shape:
     - **NoParam** — function takes only base slots (parameterless).
     - **Enum** — one or more enum-typed slots (ActorValue family, Sex, etc.).
     - **FormLinkOrIndex** — one `IFormLinkOrIndex<T>` slot (GetIsID, GetInFaction, GetInCell, HasMagicEffect, etc.).
     - **MultiSlot** — multiple slots of any types (e.g. GetStageDone with Quest + Stage).
     - **PrimitiveOnly** — one or more int/float/bool slots only (no FormLinks or enums).
     - **Exotic** — anything else (multi-FormLink, custom Loqui types, unusual shapes). Flag for Aaron's review.
   - Output a frequency-sortable summary: per-shape count, plus the function list within each shape.
3. **Build** `cd tools/race-probe && dotnet build -c Release` (zero warnings, zero errors). **Run** `dotnet run -c Release --no-build --project tools/race-probe`. Capture full output to `<workspace>/scratch/v2.9-phase-1-inventory.txt`.

4. **Write `CONDITIONS_AUDIT.md`** capturing:
   - Total concrete `*ConditionData` count.
   - Per-shape categorization (NoParam / Enum / FormLinkOrIndex / MultiSlot / PrimitiveOnly / Exotic) with function counts and full per-function lists.
   - Pareto evidence: for each candidate function in the floor + stretch set, document its slot signature exactly (slot name + type, including the inner T for FormLink slots). Note any that don't fit the dispatcher's 5-type coverage (per § C).
   - Architectural surprises: any function whose ConditionData breaks the uniform reflection pattern (e.g. nested sub-objects, computed properties, slot types outside the 5-type set). Capture for Phase 2 to handle or Aaron to defer.
   - Out-of-scope-error message templates: confirm the per-record error wording per § C lands cleanly (no template rendering issues).
5. **Write Pareto proposal to `PHASE_1_HANDOFF.md` § Conductor asks:**
   - Proposed in-scope set with one-line rationale per function (use frequency in real Skyrim conditions).
   - Stretch goals with rationale.
   - Any architectural surprises requiring Aaron's call.
   - Default-if-no-response: floor set (the seven listed above), no stretch goals.
6. **Halt and let the conductor relay to Aaron.** Phase 1 does NOT proceed past this point in the same session — the lock comes back via conductor as the input to Phase 2's kickoff.

7. **Once the lock is in** (either via the conductor calling Phase 1 back to update MATRIX.md, or by Phase 2's kickoff carrying the lock into a fresh session that does the MATRIX update first), update `MATRIX.md` Layer 1 and Layer 1.D with one row per in-scope function. Cell IDs follow the convention from Phase 0.

8. **Force-add CONDITIONS_AUDIT.md** if produced. Force-add updated MATRIX.md.

9. **Write `PHASE_1_HANDOFF.md`** documenting:
   - Inventory total + per-shape categorization.
   - Pareto proposal (or final lock if Aaron responded in-session via conductor).
   - Probe build + run evidence.
   - Architectural surprises.
   - MATRIX update status (done in this session, or pending Phase 2 first-step depending on lock cadence).
10. **Commit** (double-commit cadence):
    - Work commit: `[v2.9 P1] Condition-parameter inventory probe + Pareto proposal`
    - Hash-record commit: `[v2.9 P1] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 1

- Inventory probe runs to completion; CONDITIONS_AUDIT.md captures total + per-shape categorization + per-candidate-function slot signatures.
- Pareto proposal written; conductor has it for Aaron sign-off.
- Race-probe build clean.
- MATRIX.md updated with in-scope function cells (or noted as pending Phase 2 if the lock landed too late in this session for Phase 1 to do the update).
- Handoff under 400 lines; § Conductor asks populated with the Pareto proposal in the agreed format.

---

## Phase 2 — Bridge dispatch infrastructure + functional probes + coverage-smoke regression cells

**Goal:** Implement the generic Condition-parameter dispatch mechanism in `BuildCondition` per § A. Add `parameters` field to `ConditionEntry`. Wire the in-scope function set Aaron locked at Phase 1. Lay down per-in-scope-function functional probes in race-probe (Mutagen-direct round-trip — slot survives WriteToBinary→CreateFromBinary). Lay down coverage-smoke regression cells per MATRIX Layer 1 + 1.D + 2 + 4 rows. Bump version to v2.9.X (this phase's first commit).

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (per-in-scope-function functional probes)
- `<repo>/tools/mutagen-bridge/PatchEngine.cs` (`BuildCondition` extension; new helper `RouteParameterSlot`)
- `<repo>/tools/mutagen-bridge/Models.cs` (`ConditionEntry.Parameters` field + ParameterValue type if needed)
- `<repo>/tools/coverage-smoke/Program.cs` (per-in-scope-function regression cells per MATRIX)
- `<repo>/mo2_mcp/tools_patching.py` (schema description for `add_conditions` parameters)
- `<repo>/mo2_mcp/CHANGELOG.md` (new `## v2.9.X — TBD` entry; Phase 2 bullet)
- `<repo>/mo2_mcp/config.py` (`PLUGIN_VERSION = (2, 9, X)`)
- `<repo>/installer/claude-mo2-installer.iss` (`#define AppVersion "2.9.X"`)
- `<repo>/README.md` (installer download URL → v2.9.X — both occurrences per v2.8.0 P1 pattern)
- `<repo>/KNOWN_ISSUES.md` (entry update — "Other Condition-function parameter slots" moves from carry-over to "covered for {in-scope set}", new entry for "out-of-scope functions surface clean error" with the in-scope set listed)
- `<plan>/PHASE_2_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- The Pareto-locked in-scope function set is recorded in Phase 1's handoff under § Conductor asks; the conductor's Phase 2 kickoff prompt restates it as the authoritative list. If the kickoff prompt lacks the lock, halt and ask conductor — don't infer from CONDITIONS_AUDIT.md.
- **Phase 2 split trigger:** if the locked set exceeds ~12 functions, the conductor will have already split this into Phase 2A (infrastructure + first ~7 functions) and Phase 2B (remaining functions). Your kickoff prompt names which sub-phase you are. If you're 2A, leave 2B's functions as `// TODO 2B` placeholders in the dispatch table; if you're 2B, the infrastructure is already in place from 2A — only the table extends.
- **No expansion of slot-type coverage beyond the 5 types** (`IFormLinkOrIndex<T>`, enum, int, float, bool) without explicit conductor approval. If Phase 1 surfaced an exotic shape, the kickoff will tell you whether Aaron expanded scope.

### Steps

**Phase 2 (or 2A) is one session. Phase 2B if it exists is its own kickoff.**

1. **Confirm Pareto lock** from kickoff prompt. List the in-scope function names in your acknowledgement to Aaron.

2. **Read CONDITIONS_AUDIT.md** for the exact slot signatures of every in-scope function. Phase 2 transcribes these — don't speculate.

3. **Extend `Models.cs` `ConditionEntry`** with the new optional field:
   ```csharp
   /// <summary>
   /// v2.9 — generic Condition-function parameter slot map. Each key is a Mutagen
   /// reflection property name on the function's ConditionData class (e.g. "Reference",
   /// "Faction", "Cell", "MagicEffect", "Stage"). Each value is JSON-typed per the
   /// slot's runtime type — string for IFormLinkOrIndex<T>, string for enum,
   /// number for int/float, bool for bool. See CONDITIONS_AUDIT.md for the per-
   /// function slot signatures of the v2.9 in-scope set; functions outside the
   /// in-scope set surface a clean per-record "not yet wired" error.
   /// Back-compat: the v2.8.0 actor_value field is still accepted as syntactic
   /// sugar for parameters: {ActorValue: ...}; the bridge errors if both forms
   /// are supplied for ActorValue.
   /// </summary>
   [JsonPropertyName("parameters")]
   public Dictionary<string, JsonElement>? Parameters { get; set; }
   ```
   Decide locally: store as `Dictionary<string, JsonElement>` or as a custom `ParameterValue` type. `JsonElement` is simpler and matches the existing JSON-passthrough in `set_fields`. Pick one; document in handoff.

4. **Extend `PatchEngine.cs` `BuildCondition`** with the dispatcher per § A. Add a helper `RouteParameterSlot(condData, condDataType, slotName, jsonValue)` that does the type-routing. Call it in a foreach over `ce.Parameters` after the existing `Global` handler. The existing `actor_value` handler stays — but at the top of the dispatcher, if both `ce.ActorValue != null` AND `ce.Parameters?.ContainsKey("ActorValue") == true`, throw the unambiguous-DSL error.

5. **Add the v2.9-frozen `KnownParameterizedFunctions` set** in `BuildCondition` or a static constructor — populated from the in-scope function set Phase 1 locked. Used per § C to detect out-of-scope-function calls. Functions in the set: `parameters` is dispatched as above. Functions NOT in the set + caller supplied `parameters`: throw with the function + slot name + in-scope-set list per § C wording. Functions NOT in the set + no `parameters`: existing behavior preserved (no warning yet — see § C "warning-not-error" rationale).

   Optional bonus-catch consideration: an out-of-scope-function-no-parameters warning routed through the per-record details. Decide locally based on plumbing complexity; if it requires changing the response shape of `apply_modifications`, defer to v2.9.x and document.

6. **Build the bridge:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

7. **Extend `tools/race-probe/Program.cs` with per-in-scope-function functional probes.** For each in-scope function:
   - Construct an in-memory MGEF (or other host) with a Conditions list.
   - Build a `ConditionEntry` exercising the function's slots with a known FormID / enum / int.
   - Call `BuildCondition` (via the bridge — pipe a synthetic `bridge_request` through `mutagen-bridge.exe`).
   - Read back the output ESP via Mutagen-direct (NOT via bridge — independent verification).
   - Confirm: `condition.Data.GetType() == {Function}ConditionData`; for each slot, the property's runtime value matches what was sent.
   - For multi-slot functions, exercise each slot in the same probe.
8. **Inline smoke test** (Phase 2's "test our assumptions" step). Pick one in-scope function, build a `bridge_request` exercising it, pipe to bridge, read back via Mutagen-direct, confirm the slot landed. Repeat for one function from each parameter-shape category Phase 1 surfaced (ensures the dispatcher generalizes correctly).

9. **Add coverage-smoke regression cells** per MATRIX § Layer 1 + 1.D + 2 + 4 rows. Use the existing condition test patterns in `coverage-smoke/Program.cs` as templates. For each in-scope function: positive cell (slot lands) + negative cell (bad slot value → record-level error with slot name) + at least one Layer 4 cell exercising the unambiguous-DSL error or unknown-SlotName error. Multi-slot functions get at least one combinatorial cell. Keep cell IDs consistent with MATRIX.

10. **Update Python schema description** in `tools_patching.py` for `add_conditions`. Append a section listing the in-scope functions with their parameter slots:
    ```
    Parameter slots (v2.9.X — supplied via 'parameters' key on each condition entry):
    - GetIsID: Reference (FormLink to any record)
    - GetInFaction: Faction (FormLink<Faction>)
    - GetInCell: Cell (FormLink<Cell>)
    - HasMagicEffect: MagicEffect (FormLink<MagicEffect>)
    - HasPerk: Perk (FormLink<Perk>)
    - HasSpell: Spell (FormLink<Spell>)
    - GetIsRace: Race (FormLink<Race>)
    - {ActorValue family}: ActorValue (string enum) — also accepted via the back-compat 'actor_value' field
    {... rest of in-scope set ...}
    Functions outside this list produce a structurally-valid but always-false
    condition if called with default slots; supplying 'parameters' for an
    out-of-scope function surfaces a 'not yet wired' error per record.
    ```
    Document the per-record-error shape too.

11. **Update `KNOWN_ISSUES.md`:**
    - Move "Other Condition-function parameter slots" from the v2.8.0 carry-over section to a covered-for entry: "v2.9.X covers {list}. Functions outside this set produce a clean per-record 'not yet wired' error if called with `parameters`; called without `parameters` they preserve v2.8.0 behavior (structurally-valid but always-false)."
    - Update the v2.8.0 carry-over list to drop the "Other Condition-function parameter slots" line.
12. **Add CHANGELOG entry:**
    ```markdown
    ## v2.9.X — TBD

    <Phase 5 fills in date.>

    ### Added — bridge

    - **Generic Condition-function parameter dispatch.** The v2.8.0 `actor_value`
      handler is generalized into a reflection-based slot dispatcher in
      `BuildCondition`. Each `add_conditions` entry now accepts an optional
      `parameters: {SlotName: Value}` map; SlotName matches Mutagen's reflection
      property name on the function's `ConditionData` class. Slot types covered:
      `IFormLinkOrIndex<T>` (string FormID), enum (string name), int / float / bool.
      v2.9.X in-scope function set: {list}. Out-of-scope functions called with
      `parameters` surface a clean per-record "not yet wired" error naming the
      function and its slots. Back-compat: existing `actor_value` field is still
      accepted (treated as syntactic sugar for `parameters: {ActorValue: ...}`);
      supplying both forms for ActorValue surfaces an unambiguous-DSL error.
      All 160 v2.8.0 coverage-smoke tests pass unchanged.

    <Subsequent phases append entries.>

    ---
    ```

13. **Bump version constants:**
    - `config.py`: `PLUGIN_VERSION = (2, 9, X)` per the slug Aaron locked at PLAN review (or P1 if it slipped).
    - `claude-mo2-installer.iss`: `#define AppVersion "2.9.X"`.
    - `README.md`: replace v2.8.0 references at lines 7 and 59 with v2.9.X.

14. **Run coverage-smoke end-to-end.** `dotnet run -c Release --no-build --project tools/coverage-smoke`. Capture full output to `<workspace>/scratch/v2.9-phase-2-coverage.txt`. Expected: all 160 v2.8.0 cells pass + N new cells pass (N = 2 × in-scope-function-count + Layer 2/4 additions). All green.

15. **Write `PHASE_2_HANDOFF.md`** documenting:
    - Dispatcher implementation hunk + RouteParameterSlot helper signature.
    - In-scope function set landed (matches Pareto lock; flag any deviation with rationale).
    - Functional probe results per function.
    - Inline smoke results.
    - Coverage-smoke total counts (pre-existing + new = total; PASS / FAIL / SKIP).
    - Schema description diff.
    - CHANGELOG / KNOWN_ISSUES diffs.
    - Version bump landed.
    - Bonus-catch decisions (e.g. did out-of-scope-function-no-parameters warning land or defer).
16. **Commit** (double-commit cadence):
    - Work commit: `[v2.9 P2] Generic Condition-parameter dispatch + N functions in-scope + version bump to v2.9.X`
    - Hash-record commit: `[v2.9 P2] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 2

- Inventory-probe-confirmed slot signatures transcribed into bridge code; no speculation.
- Bridge builds clean (0 warnings, 0 errors).
- Inline smoke + per-function functional probes pass via Mutagen-direct readback.
- Coverage-smoke runs to total (160 v2.8.0 + N v2.9), all PASS or documented SKIP.
- Version bumped in all four version-bearing files.
- Schema description, CHANGELOG, KNOWN_ISSUES updated.
- All 22 pre-v2.8.0 + 138 v2.8.0 coverage-smoke tests stay green (no regression).
- Handoff under 400 lines.

---

## Phase 3 — Workflow scenarios on live install

**Goal:** Run 2 realistic patcher scenarios against the live Authoria modlist via `mo2_create_patch`. Verify each scenario's Condition-parameter assertions via `mo2_record_detail` readback. Capture surfaced bugs.

**Files to touch:**
- `<modlist>/mods/Claude Output/v2.9-scenario-*.esp` (test patches; created + deleted within the phase)
- `<plan>/PHASE_3_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Live install must be at v2.9.X (the conductor's kickoff prompt will confirm this and tell you whether a sync was needed). If `mo2_ping` returns < v2.9.X, halt and ask conductor.
- Scenarios are picked from MATRIX.md § Layer 3 (Phase 0 named them; Phase 3 picks the live FormIDs at execution time). Aaron may swap during Phase 3 if the named records aren't ideal in the live modlist.

### Steps

1. **Verify live install + MCP server.** `mo2_ping` returns `version: "2.9.X"`. If disconnected or wrong version: halt and ask conductor.

2. **Verify Phase 2's dispatcher landed in the live install.** Pre-flight: build a single `mo2_create_patch` call exercising one in-scope function with `parameters`. If the bridge errors with "no such field 'parameters'" or accepts `parameters` but doesn't write the slot (default-zero confirmed via readback), the live bridge is stale — halt and ask conductor to re-sync.

3. **For each Layer 3 scenario in MATRIX.md** (target: 2 scenarios — dialog GetIsID + perk HasPerk/HasSpell, swappable per Aaron's call):
   - Confirm the target records exist at expected FormIDs in the live modlist. Swap if needed; document.
   - Build the `mo2_create_patch` call. Output filename: `v2.9-scenario-<N>.esp`.
   - Capture response. Per-record `mods` keys must match expected.
   - Run `mo2_record_detail` against each modified record. For each Condition-parameter assertion, readback must show the slot's runtime value matches what was sent (NOT the FormID 0 / enum index 0 default).
   - **Delete the test patch** before the next scenario: Bash `rm` + ask user to F5 in MO2.
   - Capture per-scenario result table in handoff.
4. **Cross-scenario rollup.** Summarise pass/fail counts; group failures by suspected root cause if a pattern emerges.

5. **Triage failures.** For each FAIL: bug entry with slug, repro, failure mode, proposed Phase 4 fix angle.

6. **Write `PHASE_3_HANDOFF.md`** documenting:
   - Per-scenario assertion table.
   - Bug list (extending Phase 2's, if any).
   - Confirmation that test patches were deleted.
   - § Conductor asks: any decisions for the conductor (e.g. "Phase 4 needed?" recommendation based on findings).
7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9 P3] Layer 3 workflow scenarios — N bugs surfaced`
   - Hash-record commit: `[v2.9 P3] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 3

- Both Layer 3 scenarios executed.
- Each Condition-parameter assertion documented as pass/fail with readback evidence.
- Test patches deleted; modlist clean.
- Bug list extended with workflow-scenario finds.
- Handoff § Conductor asks names whether Phase 4 is needed.

---

## Phase 4 — Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL)

**Goal:** Land all v2.9.X-bound bridge fixes, schema enhancements, matrix corrections, and docs hygiene that Phase 2 + Phase 3 surfaced. Conductor decides whether this phase runs at all (skip if zero findings) and whether it splits into sub-sessions per bug if findings don't fit one budget.

**Files to touch:** Variable per finding. Common candidates:
- `<repo>/tools/mutagen-bridge/PatchEngine.cs`
- `<repo>/tools/mutagen-bridge/Models.cs`
- `<repo>/tools/race-probe/Program.cs`
- `<repo>/tools/coverage-smoke/Program.cs`
- `<repo>/mo2_mcp/tools_patching.py`
- `<repo>/mo2_mcp/CHANGELOG.md`
- `<repo>/KNOWN_ISSUES.md`
- `<plan>/PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` per sub-session)

**No version bump in Phase 4** — Phase 2 already bumped.

**Conductor decisions relevant to this phase:**
- Conductor reads Phase 2 + Phase 3 handoffs and writes the kickoff naming the specific items in scope. If multiple items, conductor decides single-session-with-items-N vs sub-session-per-bug based on estimated complexity.
- **Scope-lock for Phase 4:** items the kickoff names are in scope. Other v2.7.1/v2.8.0 carry-overs (Quest condition disambiguation, AMMO enchantment, replace-semantics dict, chained dict access) stay deferred unless the kickoff explicitly absorbs them per Aaron's call. The discipline from v2.8.0 P4 holds: "don't punt v2.9.X-uncovered findings; pre-existing carry-overs not surfaced fresh stay deferred."
- **Bonus-catch precedent:** fold in only if load-bearing for the current item. >1h additional or new operator surface → halt + conductor ask + Aaron decision.

### Steps

(Per-item steps depend on what the conductor's kickoff names. The general shape mirrors v2.8.0 Phase 4: pre-fix probe → fix → regression test → build clean → coverage-smoke green. See v2.8.0 PLAN.md § Phase 4 for the canonical step structure.)

1. **Confirm scope from kickoff.** List the items in scope to Aaron in your acknowledgement.

2. **Per item:** probe → fix → regression test → smoke green.

3. **Build the bridge** post all fixes. Zero warnings, zero errors.

4. **Run coverage-smoke end-to-end.** All cells from prior phases + new regression cells, all PASS.

5. **Update CHANGELOG + KNOWN_ISSUES** per items landed.

6. **Write `PHASE_4_HANDOFF.md`** documenting per-item completion, smoke counts, change summaries.

7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9 P4] Bridge fixes + matrix corrections + docs hygiene`
   - Hash-record commit: `[v2.9 P4] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 4

- All items the kickoff named are landed (or partial state is documented in handoff with reason).
- Bridge builds clean.
- Coverage-smoke at total (v2.8.0 baseline + Phase 2 cells + Phase 4 regression cells), all PASS.
- CHANGELOG + KNOWN_ISSUES updated.
- Handoff under 400 lines.

---

## Phase 5 — Re-run + ship v2.9.X

**Goal:** Final verification pass + ship the v2.9.X release. Phase 2 guaranteed code changes; this is always a real release.

**Files to touch:**
- `<repo>/build-output/installer/claude-mo2-setup-v2.9.X.exe` (built artifact)
- `<repo>/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `<repo>/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `<plan>/PHASE_5_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Bridge SHA preservation chain matters. Phase 5's `dotnet publish` produces a NEW SHA (different from Phase 2/4's build SHA). That new SHA is the canonical v2.9.X ship SHA. It must be byte-identical across smoke matrix, installer bundle, and live install. To preserve: build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller`, which rebuilds the bridge and breaks the chain).
- Layer 3 workflow re-run is required if Phase 4 ran (Phase 4 may have introduced bridge changes Phase 3 didn't see). If Phase 4 was skipped, Phase 3's runs satisfy the re-run requirement.
- Full MO2 process restart required after live sync (not just Tools menu Stop/Start). Conductor confirms this in kickoff.

### Steps

(Mirrors v2.8.0 Phase 5 — see v2.8.0 PLAN.md § Phase 5 + v2.7.1 PHASE_5_HANDOFF.md for the canonical 12-step ship sequence with halt cadence.)

1. Verify session start (state checks per kickoff).

2. Final coverage-smoke run against latest bridge build. Confirm 100% pass.

3. **If Phase 4 ran:** re-run Layer 3 scenarios against the post-Phase-4 bridge. **If Phase 4 skipped:** skip this step.

4. Build production bridge via `dotnet publish`. Capture SHA.

5. Build installer via direct ISCC invocation. Capture installer SHA.

6. Live sync: copy bridge + Python files to `<live>/`. Aaron full-restarts MO2. `mo2_ping` returns v2.9.X.

7. Live sanity check: 2–3 representative scenarios (one in-scope condition, one out-of-scope-error case, one regression — Tier D negative or Effects-list write).

8. Insert ship date in CHANGELOG.

9. **Tag + push tag + GitHub release** (PUBLIC; hard to undo). MANDATORY HALT — show Aaron the prepared release-notes draft + exact command sequence; wait for explicit "ship" go-ahead.

10. Update memory (`project_capability_roadmap.md`).

11. Write `PHASE_5_HANDOFF.md`.

12. Final commit + handoff hash-record commit + push.

### Acceptance — Phase 5

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.X` resolves with installer attached.
- `<live>/` running v2.9.X (`mo2_ping`).
- Memory reflects v2.9.X shipped.
- SHAs captured.
- Bridge SHA matches across smoke matrix, installer bundle, and live install (single audit anchor).

---

## ⚠️ Carry-overs (NOT addressed in v2.9.X; future-release candidates)

These are explicitly out of scope for v2.9.X unless real-world testing surfaces them as actually-blocking. If Phase 2/3 surface them as bugs, conductor decides whether to promote to Phase 4 fix scope per the discipline from v2.8.0 P4.

1. **Quest condition disambiguation** (`DialogConditions` / `EventConditions`). Carried over from v2.7.1 + v2.8.0. Surfaces as clean Tier D error today.
2. **AMMO enchantment.** Mutagen schema absence; requires upstream change.
3. **Replace-semantics whole-dict assignment** (Tier C dicts). Carried over.
4. **Chained dict access** (`Foo[Key].Sub`). Carried over.
5. **QUST.Aliases / Stages / Objectives, PERK.Effects.** Out of scope for v2.8.0's bounded Effects-list mechanism — sub-class polymorphism harder; defer until real consumer surfaces.
6. **Condition functions outside Phase 1's Pareto lock.** v2.9.X covers the locked set; further functions land in v2.9.x point releases as real consumers surface them. The dispatcher is generic — adding a function is purely a Pareto-update + matrix cell + coverage-smoke regression cell, no new bridge code.
7. **Sub-B Condition functions with String-typed slots** — 6 functions: `GetGraphVariableFloat`, `GetGraphVariableInt`, `GetQuestVariable`, `GetScriptVariable`, `GetVMQuestVariable`, `GetVMScriptVariable`. Each carries a `VariableName: String` or `GraphVariable: String` slot referencing a Papyrus / Behavior-Graph runtime-only identifier. Defer until a real consumer surfaces and we have signal on the validation contract — routing them needs either a new accept-any-string operator surface (breaks the bridge's existing schema-validation posture) or a new MCP shape for Papyrus-introspection round-trip (out of scope). See CONDITIONS_AUDIT.md § Sub-B deferral.
8. **Multi-FormLink slot types and other exotic ConditionData shapes** if Phase 1 surfaced them and Aaron deferred. Documented in CONDITIONS_AUDIT.md if applicable.
9. **All v2.6.0 / v2.7.0 / v2.7.1 / v2.8.0 deferrals** — see prior plan handoffs.
