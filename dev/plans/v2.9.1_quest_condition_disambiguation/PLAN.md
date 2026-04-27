# v2.9.1 — Quest Condition disambiguation (`DialogConditions` / `EventConditions`)

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-27, post-v2.9.0 ship.
**Baseline:** v2.9.0 (shipped 2026-04-27 — generic Condition-parameter dispatch + INFO override).
**Target version:** v2.9.1 (working slug — confirm at PLAN review).
**Sessions estimated:** 4–6 phase sessions plus 1 conductor session running across them. Phase 4 is conditional (skipped if Phase 3 surfaces nothing). No Phase 2 split contemplated — capability surface is a single list-target dispatch, not a function inventory.

**Mandate.** QUST records carry **two** condition lists — `DialogConditions` and `EventConditions` — not the single `Conditions` list every other condition-bearing record uses (PERK / PACK / IDLE / MGEF / INFO). The bridge's `ApplyAddConditions` / `ApplyRemoveConditions` look up a `Conditions` property by reflection (PatchEngine.cs:1576 + 2264) and return null if absent — so QUST falls through to Tier D and surfaces `unmatched_operators: ["add_conditions"]` (or `remove_conditions`). Real dialog patchers can't add a `GetIsID` / `HasPerk` / `GetStage` condition to a quest's dialog or event lists today. Fix shape: introduce a `condition_target` operator parameter selecting which list to write to. v2.9.0's generic dispatcher composes underneath untouched — the per-Condition build pipeline doesn't change; only the carrier lookup does.

This is a **single-mechanism, scope-locked** point release like v2.8.0's Effects-list addition and v2.9.0's parameter dispatcher. ONE new parameter on the `add_conditions` / `remove_conditions` operator surface; one paired dispatch path in the bridge's reflection lookup; everything else (ConditionEntry shape, BuildCondition factory, `parameters` map, `actor_value` back-compat, error path through Tier D for non-QUST callers using `condition_target`) preserved unchanged.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |
| `<plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.1_quest_condition_disambiguation\` |
| `<v2.9.0-plan>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\dev\plans\v2.9.X_condition_parameters\` (foundation; reference only — closed) |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. The conductor session has already told you which phase you are via the kickoff prompt that spawned this session. **Before touching anything**, do this in order:

1. **Confirm your phase.** The conductor's kickoff prompt named your phase. If it didn't, halt and ask the conductor — don't infer it from the handoff numbering yourself (the conductor owns phase identification).

2. **Read the previous handoff** in full (if any). The conductor's kickoff prompt named which one. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, conductor decisions relevant to your phase, and what to write in your own handoff. **Do not read other phases' sections** — they're scoped to other executors and consume context for no benefit.

4. **Read `MATRIX.md`** in this directory. Phase 0 produces it; Phases 1–5 use it as the authoritative test specification. Phase 1 may extend it with whatever generalization scope the schema probe surfaces; Phase 2 onward reads the post-Phase-1 form.

5. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.0)
   - `Claude_MO2/KNOWN_ISSUES.md` § Patching write surface (the "Quest condition disambiguation" entry — your fix surface)
   - **Skip** the prior-plan handoff sweep — `<v2.9.0-plan>` is closed; only relevant sections (PHASE_2A/2B/2C/2D dispatcher pattern; PHASE_4_INFO override pattern as the v2.9.x reference for "child major nested under organizational GRUP parent") referenced inline below as needed.
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

v2.7.1 added the `add_conditions` / `remove_conditions` operators with reflection-based carrier lookup: `record.GetType().GetProperty("Conditions")`. Records exposing a `Conditions: ExtendedList<Condition>` property are supported (PERK, PACK, IDLE, MGEF, INFO via response-level conditions, and any other carrier whose Mutagen schema named the slot `Conditions`). v2.7.1 also documented QUST as carry-over: QUST's Mutagen schema doesn't expose a `Conditions` property — it splits into `DialogConditions` and `EventConditions`, and the bridge can't pick between them without an explicit signal from the caller.

v2.8.0 carried the gap forward; v2.9.0 carried it forward again (it remains in `KNOWN_ISSUES.md § Patching write surface` post-v2.9.0-ship). v2.9.0's generic Condition-parameter dispatcher (199 functions across 5 of 6 PLAN-named branches; `RouteParameterSlot` + `KnownParameterizedFunctions`) is **orthogonal** to this gap: the dispatcher's job is "given a ConditionEntry, build the right Condition object." The QUST gap's job is "given a Condition object, append it to the right list." v2.9.1 lands the second half, composing on top of v2.9.0 untouched.

The shape of the gap:

- `ApplyAddConditions(record, conditions)` at `PatchEngine.cs:1573` pulls `record.GetType().GetProperty("Conditions", ...)`. If null, returns null → caller skips writing `conditions_added` in the per-record `mods` dict → Tier D fires uniformly with `unmatched_operators: ["add_conditions"]`. This is the v2.8.0 architecture for unsupported-record-type errors (uniform shape, no exception leakage, clean diagnostic surface). Today it fires correctly for QUST — but the underlying capability is missing, not the diagnostic.
- `ApplyRemoveConditions(record, removals)` at `PatchEngine.cs:2262` is the symmetric pair. Same `Conditions` property lookup, same null-return-skip pattern.
- QUST's Mutagen schema at 0.53.1 (per [Mutagen-Modding/Mutagen](https://github.com/Mutagen-Modding/Mutagen)): `IQuestGetter` exposes `DialogConditions: IReadOnlyList<IConditionGetter>` and `EventConditions: IReadOnlyList<IConditionGetter>`. The two lists serve distinct in-game purposes:
  - **DialogConditions** — top-level QUST gating. Whether the quest is even relevant in dialog. xEdit shows these under the QSTA → DNAM block.
  - **EventConditions** — story-manager event-payload gating. Used by SMQN / SMEN to filter which quests respond to which Story Manager events.

Real Skyrim patchers (Authoria/Requiem-style modlists with thousands of dialog + event conditions across thousands of quests) hit this gap whenever they need to:
- Add a `GetIsID` condition gating a quest to a specific actor (DialogConditions — quest visibility).
- Add a `HasPerk` / `GetStageDone` precondition for a quest's eligibility for a given Story Manager event (EventConditions).
- Migrate Tier D `add_conditions` calls from MGEF carriers (which work today) to QUST carriers (which fall through to Tier D).

The fix is **narrow-scoped**: the dispatcher infrastructure already exists (v2.9.0's `BuildCondition` + `RouteParameterSlot` + `KnownParameterizedFunctions`). v2.9.1 adds *list-target dispatch* — pick which list to append to/remove from — which is a delta on the ApplyAddConditions/ApplyRemoveConditions reflection-lookup path, not a new mechanism layer.

---

## 🏗️ Architecture — Quest condition disambiguation (locked + open questions)

### A. Dispatch model — list-target parameter

The bridge today does:

```csharp
var condProp = record.GetType().GetProperty("Conditions", BindingFlags.Public | BindingFlags.Instance);
if (condProp == null) return null; // unsupported — let Tier D fire
```

v2.9.1 generalizes the lookup. Pseudocode:

```
slot := "Conditions" by default
if op.ConditionTarget != null:
    slot := MapConditionTarget(record, op.ConditionTarget)   // "dialog" → "DialogConditions" on QUST
    if slot == null:
        throw "Record type {RecordTypeCode(record)} does not support condition_target='{op.ConditionTarget}'."

condProp := record.GetType().GetProperty(slot, ...)
if condProp == null:
    return null  // unsupported — let Tier D fire (carrier truly doesn't have this list)

// existing append-or-remove logic unchanged
```

The dispatcher is **target-name → property-name**, NOT per-record-type-table. `MapConditionTarget` handles the naming convention (friendly aliases `"dialog"` / `"event"` → property names `"DialogConditions"` / `"EventConditions"` per v2.8.0+ DSL idiom of friendly names over property literals). On records where the targeted property doesn't exist (e.g. caller supplies `condition_target: "dialog"` against a PERK), the existing `condProp == null → return null → Tier D` path fires uniformly. No new error shape for this case.

**Default behavior — QUST without `condition_target` errors, not silently picks one.** When a caller invokes `add_conditions` against a QUST without supplying `condition_target`, the bridge returns a clean per-record error naming the missing parameter and the available targets. Per task spec: "Erroring is safer — explicit choice required." Picking a default (e.g. always `DialogConditions`) would silently route conditions intended for `EventConditions` and vice versa. The error message names the target parameter and lists the valid options.

**Non-QUST records ignore `condition_target` if supplied?** OPEN QUESTION (Phase 0 surfaces; Phase 1 confirms via probe). Two defensible postures:
  - **(a) Ignore on records with a single `Conditions` list.** PERK / PACK / etc. accept `condition_target: "dialog"` as a no-op — the call still works because the carrier has only one list. Permissive; matches "DSL flexibility" posture.
  - **(b) Reject if not needed.** PERK with `condition_target: "dialog"` → error "PERK has a single Conditions list; do not specify condition_target." Strict; matches v2.9.0 footgun-guard ("Unused" slot rejection) and dispatcher's unambiguous-DSL posture.

Phase 0 proposes (b) (strict — symmetric with v2.9.0's footgun-guard discipline; ambiguity gets surfaced not absorbed). Phase 1's probe may surface a third multi-list record type (PACK has `OnBegin` / `OnEnd` / `OnChange` script blocks but conditions live elsewhere; SCEN's per-action conditions; SMQN/SMEN). If a third type exists, the generality decision shifts.

### B. Schema — `condition_target` operator parameter

Two placement options. Pick at Phase 0 design lock:

**(1) Operator-level (ScopeOps).** `condition_target` is a sibling field on the per-record op, applying to the entire `add_conditions` / `remove_conditions` list:

```jsonc
{
  "op": "override",
  "formid": "Skyrim.esm:01EAFD",  // some QUST
  "condition_target": "dialog",
  "add_conditions": [
    { "function": "GetStageDone", "operator": ">=", "value": 1, "parameters": {"Quest": "Skyrim.esm:0003372", "Stage": 50}},
    { "function": "GetIsID",      "operator": "==", "value": 1, "parameters": {"Object": "Skyrim.esm:000019"}}
  ]
}
```

**(2) Entry-level (ConditionEntry).** Each entry carries its own `condition_target`, allowing a single op to mix dialog + event:

```jsonc
{
  "op": "override",
  "formid": "Skyrim.esm:01EAFD",
  "add_conditions": [
    { "function": "GetStageDone", "condition_target": "dialog", "operator": ">=", "value": 1, "parameters": {...}},
    { "function": "HasPerk",       "condition_target": "event",  "operator": "==", "value": 1, "parameters": {...}}
  ]
}
```

**Phase 0 proposal: option (1) — operator-level.** Rationale:
- A single `add_conditions` op is a logical group ("these conditions go together"). Splitting across two lists in one call is operationally rare and conceptually muddled.
- Validation simpler — one parameter to check per op, not one per entry.
- Dispatch site narrower — the property-name resolution happens once at the outer scope, not inside `BuildCondition`'s per-entry foreach.
- Symmetric with `remove_conditions` (which has fewer entries per op typically; entry-level `condition_target` on remove with `index`-based removal would be confusing — index relative to which list?).
- Real callers needing both lists in one record pass two `add_conditions` ops (one with `condition_target: "dialog"`, one with `condition_target: "event"`) — but the operator request shape allows only one `add_conditions` field per record. This is a structural limitation worth surfacing — but it's a v2.9.x further-limitation, not a v2.9.1 blocker.

If Phase 0 surfaces a real consumer needing single-op mixed lists, escalate to Aaron via conductor. Otherwise lock option (1).

**Naming:** `condition_target` (PLAN baseline; user-stated leading candidate). Alternatives: `target_list`, `list_target`, `conditions_on`. `condition_target` reads cleanly alongside `add_conditions` ("condition target = where these conditions go"); `target_list` ambiguous (could be the leveled-list-merge target); `list_target` reverses the natural noun-phrase order; `conditions_on` is verbose. Lock at Phase 0; alternatives surfaced via conductor ask if Phase 0 prefers one of the alternatives.

### C. Out-of-scope handling — Tier-D-style + new explicit errors

Three failure modes, all surface as per-record `details[].error`:

1. **`condition_target` supplied but record doesn't have that list.** E.g. caller sends `condition_target: "dialog"` against an MGEF. Property lookup returns null → existing Tier D path fires uniformly: `unmatched_operators: ["add_conditions"]`. Same shape as today's "MGEF doesn't support add_conditions" wouldn't fire (because MGEF DOES have Conditions); but if `condition_target` is supplied and resolution maps to a property MGEF doesn't have, the lookup fails cleanly.

2. **QUST without `condition_target`.** New explicit per-record error: `"Record type Quest requires a condition_target parameter on add_conditions. Available targets: 'dialog' (DialogConditions) | 'event' (EventConditions). Quest records carry two condition lists rather than a single Conditions list — see KNOWN_ISSUES.md § Patching write surface."` This is a NEW error path — distinct from Tier D. It surfaces explicitly because the operation IS supported on QUST; the call just lacks the disambiguation.

3. **Bad `condition_target` value.** Caller sends `condition_target: "story"` (typo or wrong vocabulary). New explicit error: `"Unknown condition_target: 'story'. Valid values: 'dialog' | 'event'."` This is a string-enum validation, mirrors `actor_value` enum-name validation pattern.

The existing `unmatched_operators` Tier D path stays unchanged for non-QUST records that don't support conditions at all (e.g. ARMO with `add_conditions`).

### D. Scope locks

- **One new mechanism only.** A `condition_target` operator parameter routing to alternate `*Conditions` properties via reflection. No other new operators, no other `set_fields` shape changes, no changes to v2.9.0's dispatcher, no changes to ConditionEntry's `parameters` shape.
- **Generality scope locked at Phase 1.** If the schema probe finds QUST is the only multi-condition record type in Mutagen 0.53.1, scope is QUST-only — `MapConditionTarget` handles `"dialog"` / `"event"` and rejects others. If the probe finds 2+ record types with multiple condition lists (PACK / SCEN / SMQN / SMEN), scope generalizes to a target-name → property-name table. Decision lock comes via conductor relay after Phase 1's probe.
- **Slot types covered:** none new. v2.9.0's dispatcher handles all condition entries the same way regardless of carrier list. v2.9.1 changes only the carrier-list lookup, not the per-condition build.
- **Back-compat:** all v2.9.0 condition usage (parameterless functions, `actor_value`, `global`, ConditionFloat, ConditionGlobal, the 199 dispatcher-wired functions across MGEF/PERK/PACK/INFO carriers) continues to work unchanged. The 22 pre-v2.8.0 + 138 v2.8.0 + 134 P2A + 45 P2B + 32 P2C + 11 P2D = 382 v2.9.0 coverage-smoke cells must all stay green.
- **Probe-first discipline.** Phase 1 starts with the schema probe before anything else lands. Phase 2 transcribes the verified property names; doesn't speculate.
- **Bonus-catch precedent.** If a phase fix surfaces a related latent issue in the touched code (e.g. Tier D coverage check rendering a wrong record-type-code for QUST, error-message DX for the new explicit errors), fold in (with explicit handoff documentation), per v2.7.1/v2.8.0/v2.9.0 pattern. >1h additional work or new operator surface → halt, ask conductor.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive.

### E. Conductor decisions (cross-phase, locked at PLAN write-time)

Things the conductor enforces or decides between phases without re-litigating:

- **Phase identification.** Conductor identifies current phase from highest-numbered handoff in `<plan>/`. Phase executors don't self-identify.
- **Design lock sign-off.** Phase 0's executor proposes the placement (operator-level vs entry-level), naming (`condition_target` vs alternatives), default behavior (error vs implicit one-list), and non-QUST handling (ignore vs reject). Conductor relays to Aaron for explicit lock. Phase 1 doesn't begin until the lock is in.
- **Generality scope sign-off.** Phase 1 surfaces the schema probe finding (QUST-only vs 2+ multi-condition records). Conductor relays to Aaron. Phase 2 doesn't begin until the lock is in.
- **No Phase 2 split contemplated.** Capability surface is single — list-target dispatch on add/remove. No inventory probe Pareto-locking that could exceed a session budget.
- **Phase 4 spawn decision.** If Phase 2 + Phase 3 surface zero bridge bugs and zero matrix corrections, conductor skips Phase 4 directly to Phase 5. Otherwise spawns Phase 4 (single session, items 1–N model from v2.9.0 P4) or Phase 4 sub-sessions per bug if items don't fit one budget.
- **Live install sync timing.** Phase 0 + 1 don't touch live. Phase 3 reads via `mo2_create_patch` against live (test patches in `<modlist>/mods/Claude Output/`, deleted after). Phase 4 syncs to live only if a fix needs verification on the live install. Phase 5 syncs once and ships. Conductor confirms sync state before each Phase 3 / 4 / 5 kickoff.
- **Schema migration vs additive.** v2.9.1 is purely additive. No deprecation of existing fields. The new `condition_target` field is optional on every record type except QUST (where it's required). Conductor rejects any phase proposing a schema break.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Plan + matrix specification + design proposal | `MATRIX.md` (NEW); `PHASE_0_HANDOFF.md`; PLAN.md force-added; design questions surfaced under § Conductor asks (placement, naming, default, non-QUST posture) | None |
| 1 | Schema probe + generality lock | `tools/race-probe/Program.cs` extended with `*Conditions` property sweep across all `*Getter` interfaces in `Mutagen.Bethesda.Skyrim`; `PHASE_1_HANDOFF.md` with finding + generality proposal; `MATRIX.md` updated post-lock if scope generalizes; Aaron's generality-scope sign-off via conductor relay | Phase 0 with design lock (placement, naming, default, non-QUST posture) |
| **2** | **Bridge implementation + functional probes + coverage-smoke regression cells** | `PatchEngine.cs` `ApplyAddConditions`/`ApplyRemoveConditions` extended with list-target dispatch; `Models.cs` `ScopeOps.ConditionTarget` field (or `ConditionEntry.ConditionTarget` per Phase 0 lock); `race-probe` per-list-target functional probes; `coverage-smoke` +N regression cells; `tools_patching.py` schema; CHANGELOG; `KNOWN_ISSUES.md`; **version bump to v2.9.1** (Phase 2's first commit) | Phase 1 with generality lock |
| 3 | Workflow scenario(s) on live | Per-scenario assertions in `PHASE_3_HANDOFF.md`; bug list extended | Phase 2 |
| 4 | Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL — conductor decides) | `PHASE_4_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md` for sub-sessions); code commits; regression tests | Phase 3 with surfaced findings |
| 5 | Re-run + ship v2.9.1 | Final smoke run; installer + bridge artifact rebuilt; live sync; tag pushed; `gh release create`; memory updated | Phase 4 (or Phase 3 if Phase 4 skipped) |

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one or more commits per its scope. Commit messages start with `[v2.9.1 PN]` (e.g. `[v2.9.1 P2] Quest condition disambiguation + version bump to v2.9.1`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its handoff via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. **Phase 2 bumps the version** on its first commit (Quest disambiguation is the trigger). Subsequent phases don't re-bump. The version slug (`v2.9.1` vs further) is confirmed at PLAN review.
- **Live install sync:** Phases 0, 1, 2 do not touch the live install. Phase 3 reads via `mo2_create_patch` against the live install. Phase 4 fix sessions live-sync only when the bug requires verification on the live install. Phase 5 live-syncs once and ships.
- **Probe-first discipline:** Phase 1 starts with the schema probe. Any Phase 4 fix that touches PatchEngine.cs's reflection paths or list-target dispatch logic begins with a probe demonstrating the failure mode.
- **One phase per session, with conductor-mediated handoff between phases.**
- **Don't touch out-of-phase files.** Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves you spot during work.
- **No changes to MCP tool request/response shapes** unless a Phase 4 fix requires it. Phase 2 adds capability via a new optional `condition_target` field on the operator request (or per-entry, per Phase 0 lock); no shape change beyond the new optional field.
- **Double-commit cadence per phase** (work commit + hash-record commit), matching v2.7.1/v2.8.0/v2.9.0.

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
<What tests / smoke checks ran. What evidence shows it worked. For Phase 1: probe output + schema finding. For Phase 2: per-list-target smoke results + coverage-smoke counts. For Phase 3: per-scenario assertion checklist + readback evidence. For Phase 4: probe evidence pre-fix + post-fix.>

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

## Phase 0 — Plan + matrix specification + design proposal

**Goal:** Produce `MATRIX.md`, the per-cell test specification scaffolding for v2.9.1. Pre-spec Layer 1 / 2 / 4 cells against vanilla Skyrim.esm and Layer 3 workflow scenarios against the live Authoria modlist. Surface design questions to Aaron via the conductor: parameter placement (operator-level vs entry-level), naming (`condition_target` vs alternatives), default behavior on QUST (error vs implicit list), non-QUST posture (ignore vs reject). **No production code changes. No version bump.**

**Files to touch:**
- `<plan>/PLAN.md` (this file — force-add)
- `<plan>/MATRIX.md` (NEW)
- `<plan>/PHASE_0_HANDOFF.md` (NEW — written at end)

**Conductor decisions relevant to this phase:**
- The version slug `v2.9.1` is decided at PLAN review (this phase). If Aaron hasn't decided yet, Phase 0 records the working slug and notes the decision is open; Phase 2 commits the actual version bump.
- Phase 0 does not touch the schema probe — that's Phase 1's deliverable.

### Steps

1. **Verify session start.** Confirm `origin/main` is at v2.9.0 ship commit (the conductor's kickoff prompt will name the exact hash) and clean. Live install at `<live>` running v2.9.0 (`mo2_ping` returns `version: "2.9.0"`).

2. **Draft `MATRIX.md`** with the four-layer scaffold mirroring v2.9.0's MATRIX.md but anchored on Quest condition disambiguation cells:
   - **Layer 1 — Per-list-target coverage (positives).** Cells: `1.P.add.dialog.QUST` (add_conditions to DialogConditions on QUST), `1.P.add.event.QUST` (add to EventConditions), `1.P.remove.dialog.QUST` (remove from DialogConditions by index), `1.P.remove.event.QUST` (remove from EventConditions by index), `1.P.remove.dialog.byfunc.QUST` (remove by function name from DialogConditions), `1.P.remove.event.byfunc.QUST` (symmetric). Each row: cell ID, operation, source record, expected. Use vanilla Skyrim.esm QUST records — Phase 1 picks specific FormIDs from probe output (e.g. `MQ101` `Skyrim.esm:000242` or similar quest with both DialogConditions + EventConditions populated for round-trip-distinguishability).
   - **Layer 1.D — Negatives + new explicit error paths.**
     - `1.D.01` — QUST `add_conditions` without `condition_target` → new explicit error per § C #2.
     - `1.D.02` — QUST `remove_conditions` without `condition_target` → new explicit error.
     - `1.D.03` — `condition_target: "story"` (bad value) → new explicit error per § C #3.
     - `1.D.04` — `condition_target: "dialog"` on a non-QUST record (e.g. PERK). Per Phase 0 design proposal § A: reject with explicit error. If Phase 0 locks "ignore" instead, expectation flips to no-op + condition lands in `Conditions`.
     - `1.D.05` — `condition_target: "dialog"` on ARMO (a record with no condition list at all) → existing Tier D path fires (`unmatched_operators: ["add_conditions"]`).
   - **Layer 2 — Combinatorial.** `2.01` — multi-condition single QUST `add_conditions` op (3 conditions, all dialog target — verifies foreach iterates correctly within one list-target call). `2.02` — same record, two separate ops in one `mo2_create_patch` call (one with `condition_target: "dialog"`, one with `"event"`) — verifies cross-op independence. `2.03` — QUST `add_conditions` composing with v2.9.0 `parameters` dispatch (e.g. `{function: "GetIsID", parameters: {Object: "..."}, condition_target: "dialog"}`) — verifies v2.9.0 dispatcher composes underneath untouched.
   - **Layer 3 — Workflow scenario on live.** 1 scenario (real dialog patch use case — add a `GetIsID` or `HasPerk` condition to a quest's DialogConditions, gating quest visibility on a follower having a specific perk). Phase 0 names the scenario + describes the patcher use case; Phase 3 picks the live FormIDs at execution time. Optional 2nd scenario for EventConditions if a real consumer surfaces.
   - **Layer 4 — Edges.**
     - `4.dsl.01` — empty `condition_target: ""` → bad-value error.
     - `4.dsl.02` — `condition_target` is JSON null → treated as omitted (existing Tier D / new "missing target" error per record type).
     - `4.dsl.03` — case sensitivity: `condition_target: "Dialog"` vs `"dialog"`. Lock at Phase 0 (case-insensitive matches v2.9.0 enum-parse posture; case-sensitive matches schema literal posture).
     - `4.dsl.04` — `condition_target` supplied alongside an unrelated operator (`add_keywords`) on the same record op → ignored (it's an operand for the conditions sub-operator, not a global flag).
   - **Layer 5 — Regression.** All 382 v2.9.0 coverage-smoke cells run unchanged. Specifically: every `add_conditions` cell against a non-QUST carrier (MGEF/PERK/PACK/INFO) stays green without `condition_target` — the bridge's reflection lookup defaults to `"Conditions"` when `condition_target` is absent, preserving v2.9.0 behavior bit-identical.
3. **Pre-spec Layer 3 workflow scenario** with placeholder FormIDs from the live modlist that Phase 3 will swap. Anchor on real dialog-patcher use case per task spec (DialogConditions on a quest, GetIsID condition gating a follower's quest involvement).

4. **Surface design questions to Aaron via conductor ask** in PHASE_0_HANDOFF.md § Conductor asks (token-efficient bullets):
   - **Q1: Placement.** Operator-level vs entry-level? Phase 0 default: operator-level. Rationale per § B.
   - **Q2: Naming.** `condition_target` vs `target_list` / `list_target` / `conditions_on`? Phase 0 default: `condition_target`. Rationale per § B.
   - **Q3: Default behavior on QUST.** Error if omitted, or implicit-default to one list? Phase 0 default: error. Rationale per § A.
   - **Q4: Non-QUST records receiving `condition_target`.** Ignore (no-op, condition lands in `Conditions`) or reject (explicit error)? Phase 0 default: reject — symmetric with v2.9.0 footgun-guard discipline. Rationale per § A.
   - **Q5: Case sensitivity for `condition_target` value.** Case-insensitive (v2.9.0 enum-parse posture) or case-sensitive (schema literal posture)? Phase 0 default: case-insensitive.
5. **Force-add PLAN.md and MATRIX.md.** `git add -f Claude_MO2/dev/plans/v2.9.1_quest_condition_disambiguation/{PLAN.md,MATRIX.md}`.

6. **Write `PHASE_0_HANDOFF.md`** confirming MATRIX scaffold landed, Layer 3 scenario pre-spec'd, no production code touched, no version bump. Record the working version slug + open-or-decided status. Include the design-question § Conductor asks block.

7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.1 P0] Plan + matrix scaffold + design proposal`
   - Hash-record commit: `[v2.9.1 P0] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 0

- `MATRIX.md` exists with four-layer scaffold + cell-naming convention. Per-list-target rows are placeholders awaiting Phase 1's probe-confirmed FormIDs.
- Layer 3 scenario named with use-case description; live-FormID picks deferred to Phase 3.
- `git diff main^` shows: PLAN.md (new), MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched.
- Working version slug recorded in handoff.
- § Conductor asks populated with the 5 design questions in the agreed format.

---

## Phase 1 — Schema probe + generality lock

**Goal:** Confirm Mutagen 0.53.1's `IQuestGetter` exposes exactly `DialogConditions` and `EventConditions` and no third condition list. Sweep every other concrete `*Getter` interface in `Mutagen.Bethesda.Skyrim` for any property whose name ends in `Conditions` (case-insensitive) — surface any other multi-condition record types (PACK / SCEN / SMQN / SMEN / etc.). Decide generality scope based on findings: QUST-specific table or generalized property-name routing. **No bridge code changes.** **No version bump.**

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (extend with `*Conditions`-property sweep section)
- `<plan>/MATRIX.md` (update post-Phase-1 with confirmed property names + any second multi-condition record type's cells)
- `<plan>/PHASE_1_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Phase 0's design lock (placement, naming, default, non-QUST posture, case sensitivity) is recorded in Phase 0's handoff under § Conductor asks; the conductor's Phase 1 kickoff prompt restates it as the authoritative locked design. If the kickoff prompt lacks the lock, halt and ask conductor — don't infer from PHASE_0_HANDOFF.md.
- Generality scope sign-off is mandatory before Phase 2 begins. Phase 1 ends with the proposal; Phase 1's executor writes the proposal to its handoff under § Conductor asks. Conductor relays to Aaron, gets the lock.
- If the probe surfaces something architecturally unexpected — e.g. QUST exposing a third condition list, or PACK exposing nested condition lists per phase rather than one flat list — Phase 1 documents it in PHASE_1_HANDOFF.md and writes a CONDUCTOR ASK for whether to expand v2.9.1's scope or punt to a later release.

### Steps

1. **Read MATRIX.md** to understand the Layer 1 cell shape Phase 1 needs to validate FormIDs for.

2. **Extend `tools/race-probe/Program.cs` with a multi-condition-record sweep section** appended after the existing v2.9.0 P4-INFO regression block:
   - Enumerate every concrete `Mutagen.Bethesda.Skyrim.I*Getter` interface whose corresponding setter side is a major record type (filter via `typeof(IMajorRecordGetter).IsAssignableFrom` and not abstract).
   - For each: enumerate public-instance properties whose name ends in `"Conditions"` (case-insensitive). Print: record type code (4-char ESP code), Mutagen interface name, property name, declared type. Use the existing `RecordTypeCode` helper if available; otherwise derive from class-name conventions.
   - **Specifically dump QUST.** Use a vanilla Skyrim.esm QUST FormID known to populate both lists in xEdit (e.g. MQ101 `Skyrim.esm:000242` or similar) and confirm `DialogConditions.Count` + `EventConditions.Count` both > 0 via Mutagen-direct read against `E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm`. This validates the property names empirically, not just by-symbol-table.
   - **Negative confirmation.** Iterate `IQuestGetter`'s public-instance properties — confirm no third property whose name contains `"Conditions"` exists. The expected output is exactly: `DialogConditions`, `EventConditions`, and (if the schema includes them) any nested-conditions on quest aliases or stages — those are NOT top-level QUST condition lists and aren't in v2.9.1's scope. Flag them in the dump output for Phase 1's handoff documentation but don't expand scope.
3. **Build** `cd tools/race-probe && dotnet build -c Release` (zero warnings, zero errors). **Run** `dotnet run -c Release --no-build --project tools/race-probe`. Capture full output to `<workspace>/scratch/v2.9.1-phase-1-multi-condition-sweep.txt`.

4. **Document findings in PHASE_1_HANDOFF.md:**
   - Per-record-type list of every `*Conditions` property found (record type code + interface + property name + declared type).
   - Confirm QUST has exactly DialogConditions + EventConditions and no third top-level list.
   - Flag any nested-conditions surfaces (quest-alias-level, quest-stage-level, scene-action-level, package-procedure-level) — these are out of scope for v2.9.1's mechanism but worth documenting for future v2.9.x candidates.
   - Generality finding: QUST-only, or 2+ multi-condition record types.
5. **Write generality proposal to PHASE_1_HANDOFF.md § Conductor asks:**
   - Proposed generality scope (QUST-specific table / generalized target-name → property-name table) with rationale based on the probe finding.
   - Naming-table proposal: which friendly target names map to which property names. Phase 0 baseline: `"dialog" → "DialogConditions"`, `"event" → "EventConditions"`. If a second multi-condition record type surfaces, propose its target names + property mappings.
   - Default-if-no-response: QUST-only scope (narrowest); the existing Phase 0 baseline naming.
6. **Halt and let the conductor relay to Aaron.** Phase 1 does NOT proceed past this point in the same session — the lock comes back via conductor as the input to Phase 2's kickoff.

7. **Once the lock is in** (either via the conductor calling Phase 1 back to update MATRIX.md, or by Phase 2's kickoff carrying the lock into a fresh session that does the MATRIX update first), update MATRIX.md Layer 1 / 1.D rows with confirmed property names + any second multi-condition record's cells.

8. **Force-add updated MATRIX.md.**

9. **Write `PHASE_1_HANDOFF.md`** documenting:
   - Probe build + run evidence.
   - Multi-condition record type findings (full list).
   - QUST DialogConditions + EventConditions confirmation (counts from vanilla MQ101 or chosen target QUST).
   - Generality proposal (or final lock if Aaron responded in-session via conductor).
   - MATRIX update status (done in this session, or pending Phase 2 first-step depending on lock cadence).
10. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.1 P1] Multi-condition record schema probe`
    - Hash-record commit: `[v2.9.1 P1] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 1

- Schema probe runs to completion; PHASE_1_HANDOFF.md captures per-record-type `*Conditions` property list.
- QUST DialogConditions + EventConditions confirmed via Mutagen-direct read against vanilla Skyrim.esm.
- No third top-level QUST condition list (negative confirmation documented).
- Generality proposal written; conductor has it for Aaron sign-off.
- Race-probe build clean.
- MATRIX.md updated (or noted as pending Phase 2 first-step if lock landed too late in this session).
- Handoff under 400 lines; § Conductor asks populated with the generality proposal in the agreed format.

---

## Phase 2 — Bridge implementation + functional probes + coverage-smoke regression cells

**Goal:** Implement the list-target dispatch in `ApplyAddConditions` / `ApplyRemoveConditions` per § A. Add `condition_target` field on the locked location (ScopeOps OR ConditionEntry per Phase 0 lock). Wire QUST (and any second multi-condition record type Phase 1 surfaced + Aaron's generality lock approved). Lay down per-list-target functional probes in race-probe (Mutagen-direct round-trip — condition lands in DialogConditions vs EventConditions correctly + survives WriteToBinary→CreateFromBinary). Lay down coverage-smoke regression cells per MATRIX Layer 1 + 1.D + 2 + 4 rows. Bump version to v2.9.1 (this phase's first commit).

**Files to touch:**
- `<repo>/tools/race-probe/Program.cs` (per-list-target functional probes + the schema-sweep section from Phase 1 stays)
- `<repo>/tools/mutagen-bridge/PatchEngine.cs` (`ApplyAddConditions` + `ApplyRemoveConditions` extension; new helper `ResolveConditionListProperty`)
- `<repo>/tools/mutagen-bridge/Models.cs` (`ScopeOps.ConditionTarget` field, OR `ConditionEntry.ConditionTarget` per Phase 0 lock)
- `<repo>/tools/coverage-smoke/Program.cs` (per-list-target regression cells per MATRIX)
- `<repo>/mo2_mcp/tools_patching.py` (schema description for `condition_target` parameter on `add_conditions` / `remove_conditions`)
- `<repo>/mo2_mcp/CHANGELOG.md` (new `## v2.9.1 — TBD` entry; Phase 2 bullet)
- `<repo>/mo2_mcp/config.py` (`PLUGIN_VERSION = (2, 9, 1)`)
- `<repo>/installer/claude-mo2-installer.iss` (`#define AppVersion "2.9.1"`)
- `<repo>/README.md` (installer download URL → v2.9.1 — both occurrences per v2.9.0 P2 pattern)
- `<repo>/KNOWN_ISSUES.md` (entry update — "Quest condition disambiguation" moves from carry-over to "covered for QUST.DialogConditions / EventConditions" with the new operator-parameter mechanism documented)
- `<plan>/PHASE_2_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- The Phase 0 design lock (placement, naming, default, non-QUST posture, case sensitivity) and the Phase 1 generality lock (QUST-specific or generalized) are recorded in their respective handoffs under § Conductor asks; the conductor's Phase 2 kickoff prompt restates both as authoritative. If the kickoff prompt lacks either, halt and ask conductor — don't infer from prior handoffs.
- **No expansion of generality scope beyond Phase 1's lock** without explicit conductor approval. If Phase 1 found PACK/SCEN/SMQN/SMEN with multiple condition lists and Aaron locked QUST-only, those records stay deferred to v2.9.x — even if Phase 2's wiring would be cheap.
- **Composition with v2.9.0's dispatcher MUST NOT regress.** All 382 v2.9.0 coverage-smoke cells must stay green. The new code path is additive; existing `Conditions`-property carriers (MGEF/PERK/PACK/INFO/IDLE) must behave bit-identically.

### Steps

1. **Confirm Phase 0 + Phase 1 locks** from kickoff prompt. State both back to Aaron in your acknowledgement: design-lock summary (placement / naming / default / non-QUST / case) + generality-lock summary (QUST-only or generalized).

2. **Read PHASE_1_HANDOFF.md** for the exact property-name table Phase 2 transcribes. Phase 2 uses Phase 1's findings — don't speculate.

3. **Extend `Models.cs`** with the new optional field at the locked location:

   **If operator-level (Phase 0 default lock):** Add to `ScopeOps`:
   ```csharp
   /// <summary>
   /// v2.9.1 — selects which condition list on a multi-condition-list carrier
   /// to write to / remove from. Required on QUST records (which expose
   /// DialogConditions and EventConditions rather than a single Conditions
   /// list); optional on records with a single Conditions property (rejected
   /// per § A's footgun-guard policy if Phase 0 locked the strict posture).
   /// Valid values: "dialog" (→ DialogConditions), "event" (→ EventConditions).
   /// Case-insensitive per Phase 0 lock. See KNOWN_ISSUES.md § Patching write
   /// surface.
   /// </summary>
   [JsonPropertyName("condition_target")]
   public string? ConditionTarget { get; set; }
   ```

   **If entry-level:** Add to `ConditionEntry` instead. Decide field type at Phase 0 lock — `string?` (case-insensitive parse to internal enum) is the baseline; an explicit enum type (`ConditionTarget?`) is the alternative for type-safety. Document choice in handoff.

4. **Extend `PatchEngine.cs` `ApplyAddConditions` / `ApplyRemoveConditions`** with a list-target dispatch helper:

   ```csharp
   /// <summary>
   /// v2.9.1 — resolves which *Conditions property on the record to write to
   /// / remove from. Defaults to "Conditions" when condition_target is null
   /// (back-compat preserved for all v2.9.0 carriers). When condition_target
   /// is supplied, maps friendly names ("dialog" / "event") to property
   /// literals ("DialogConditions" / "EventConditions") via a frozen lookup
   /// table; bad target names surface as ArgumentException with the valid set.
   /// </summary>
   private static string ResolveConditionListProperty(IMajorRecord record, string? conditionTarget) { ... }
   ```

   Call it at the top of each method, replacing the hardcoded `"Conditions"` lookup string. Preserve the `condProp == null → return null → Tier D` path for record types that don't have the targeted property.

5. **Add the new explicit error paths per § C:**
   - QUST without `condition_target`: pre-flight check before the property lookup. If `record is IQuestGetter && conditionTarget == null` → throw `ArgumentException` with the message from § C #2.
   - Bad `condition_target` value: thrown by `ResolveConditionListProperty` per the lookup-table miss path.
   - Non-QUST + `condition_target` (per Phase 0's locked posture): if Phase 0 locked "reject", `ResolveConditionListProperty` throws when the record type doesn't have the targeted property (handled by the existing `condProp == null` path returning a uniform Tier D error). If Phase 0 locked "ignore", the helper silently maps bad-for-this-record targets to the default `"Conditions"`.

6. **Build the bridge:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

7. **Extend `tools/race-probe/Program.cs` with per-list-target functional probes.** For each in-scope (record type, list target):
   - Construct a vanilla QUST in-memory or load from Skyrim.esm.
   - Build a `ScopeOps` (or `ConditionEntry`) request exercising both add + remove on each list target.
   - Pipe a synthetic `bridge_request` through `mutagen-bridge.exe`.
   - Read back the output ESP via Mutagen-direct (NOT via bridge — independent verification).
   - Confirm: condition appears in the targeted list, doesn't appear in the other list. For remove: condition removed from the targeted list, the other list unchanged.
   - Add at least one race-probe for the new explicit error paths (QUST without `condition_target` → bridge error; bad `condition_target` value → bridge error).
8. **Inline smoke test.** Pick the canonical positive case (`add_conditions` to QUST DialogConditions, single condition with parameters: GetIsID/Object), build a bridge_request, pipe to bridge, read back via Mutagen-direct, confirm the condition lands in DialogConditions and not EventConditions. Repeat for EventConditions.

9. **Add coverage-smoke regression cells** per MATRIX § Layer 1 + 1.D + 2 + 4 rows. Use the existing condition test patterns in `coverage-smoke/Program.cs` as templates. For QUST + each list target: positive cell (condition lands) + negative cell (bad target value → record-level error) + at least one Layer 4 cell exercising the case-sensitivity lock or empty-string error. Layer 2.03 cell composes with v2.9.0's `parameters` dispatch (e.g. GetIsID/Object on QUST DialogConditions). Keep cell IDs consistent with MATRIX.

10. **Update Python schema description** in `tools_patching.py` for `add_conditions` / `remove_conditions`. Add a `condition_target` parameter description to the operator-level schema:

    ```
    condition_target: Selects which condition list to write to (add_conditions)
    or remove from (remove_conditions) on multi-condition-list records.
    Required on QUST records (which carry DialogConditions and EventConditions
    rather than a single Conditions list). Valid values: "dialog" (→
    DialogConditions), "event" (→ EventConditions). Case-insensitive. Records
    with a single Conditions property (MGEF, PERK, PACK, IDLE, INFO via
    response-level conditions): {Phase 0 lock — "ignore" or "reject"}.
    ```

    Update the existing `add_conditions`/`remove_conditions` descriptions to remove the v2.9.0 "QUST records use DialogConditions/EventConditions which require a parameter not yet exposed" caveat — the parameter now exists.

11. **Update `KNOWN_ISSUES.md`:**
    - Move "Quest condition disambiguation" from § Patching write surface (write-surface gaps) to a covered-for entry: "v2.9.1 covers QUST.DialogConditions and QUST.EventConditions via the `condition_target` operator parameter on `add_conditions` / `remove_conditions`. Required on QUST; {Phase 0-locked posture} on records with a single Conditions list."
    - If Phase 1 surfaced additional multi-condition record types Aaron deferred (e.g. PACK/SCEN/SMQN if applicable), add a new gap-list entry: "Multi-condition record types beyond QUST: {list}. Deferred to v2.9.x — requires extension of v2.9.1's `condition_target` mapping table."
12. **Add CHANGELOG entry:**
    ```markdown
    ## v2.9.1 — TBD

    <Phase 5 fills in date.>

    ### Added — bridge

    - **Quest condition disambiguation via `condition_target` operator parameter.**
      QUST records carry `DialogConditions` and `EventConditions` rather than a
      single `Conditions` list. The new `condition_target` operator parameter
      on `add_conditions` / `remove_conditions` selects which list to write to /
      remove from. Valid values: `"dialog"` (→ DialogConditions), `"event"`
      (→ EventConditions). Case-insensitive. Required on QUST; missing
      `condition_target` on a QUST `add_conditions` call surfaces a clean
      per-record error naming the available targets.
      v2.9.1 in-scope record types: QUST {+ any additional Phase 1 surfaced}.
      Records with a single `Conditions` property (MGEF, PERK, PACK, IDLE, INFO):
      {Phase 0-locked posture}.
      Composes with v2.9.0's generic Condition-parameter dispatcher untouched —
      the per-Condition build pipeline is unchanged; only the carrier list
      lookup changes. All 382 v2.9.0 coverage-smoke cells pass unchanged.

    <Subsequent phases append entries.>

    ---
    ```

13. **Bump version constants:**
    - `config.py`: `PLUGIN_VERSION = (2, 9, 1)`.
    - `claude-mo2-installer.iss`: `#define AppVersion "2.9.1"`.
    - `README.md`: replace v2.9.0 references at lines 7 and 59 with v2.9.1.

14. **Run coverage-smoke end-to-end.** `dotnet run -c Release --no-build --project tools/coverage-smoke`. Capture full output to `<workspace>/scratch/v2.9.1-phase-2-coverage.txt`. Expected: all 382 v2.9.0 cells pass + N new cells pass (N = Layer 1 list-target cells + Layer 1.D negatives + Layer 2 combinatorial + Layer 4 edges, ~10–15 new cells). All green.

15. **Write `PHASE_2_HANDOFF.md`** documenting:
    - List-target dispatcher implementation hunk + ResolveConditionListProperty helper signature.
    - In-scope record types landed (matches Phase 1 lock).
    - Functional probe results per list target.
    - Inline smoke results.
    - Coverage-smoke total counts (pre-existing + new = total; PASS / FAIL / SKIP).
    - Schema description diff.
    - CHANGELOG / KNOWN_ISSUES diffs.
    - Version bump landed.
    - Bonus-catch decisions (anything related the phase touched and folded in).
16. **Commit** (double-commit cadence):
    - Work commit: `[v2.9.1 P2] Quest condition disambiguation + version bump to v2.9.1`
    - Hash-record commit: `[v2.9.1 P2] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance — Phase 2

- Phase 1-confirmed property names transcribed into bridge code; no speculation.
- Bridge builds clean (0 warnings, 0 errors).
- Inline smoke + per-list-target functional probes pass via Mutagen-direct readback.
- Coverage-smoke runs to total (382 v2.9.0 + N v2.9.1), all PASS or documented SKIP.
- Version bumped in all four version-bearing files.
- Schema description, CHANGELOG, KNOWN_ISSUES updated.
- All 382 v2.9.0 coverage-smoke tests stay green (no regression).
- Handoff under 400 lines.

---

## Phase 3 — Workflow scenario(s) on live install

**Goal:** Run the live workflow scenario(s) against the Authoria modlist via `mo2_create_patch`. Verify each scenario's QUST condition-list assertions via `mo2_record_detail` readback. Capture surfaced bugs.

**Files to touch:**
- `<modlist>/mods/Claude Output/v2.9.1-scenario-*.esp` (test patches; created + deleted within the phase)
- `<plan>/PHASE_3_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Live install must be at v2.9.1 (the conductor's kickoff prompt will confirm this and tell you whether a sync was needed). If `mo2_ping` returns < v2.9.1, halt and ask conductor.
- Scenarios are picked from MATRIX.md § Layer 3 (Phase 0 named them; Phase 3 picks the live FormIDs at execution time). Aaron may swap during Phase 3 if the named records aren't ideal in the live modlist.

### Steps

1. **Verify live install + MCP server.** `mo2_ping` returns `version: "2.9.1"`. If disconnected or wrong version: halt and ask conductor.

2. **Verify Phase 2's dispatcher landed in the live install.** Pre-flight: build a single `mo2_create_patch` call exercising QUST `add_conditions` with `condition_target: "dialog"`. If the bridge errors with "no such field 'condition_target'" or accepts `condition_target` but writes to the wrong list (verified via readback), the live bridge is stale — halt and ask conductor to re-sync.

3. **For each Layer 3 scenario in MATRIX.md** (target: 1 dialog scenario, optional 2nd event scenario):
   - Confirm the target QUST record exists at expected FormID in the live modlist. Swap if needed; document.
   - Build the `mo2_create_patch` call. Output filename: `v2.9.1-scenario-<N>.esp`.
   - Capture response. Per-record `mods` keys must match expected (`conditions_added: 1` or whatever count was sent).
   - Run `mo2_record_detail` against the modified QUST. For each assertion: readback must show condition lands in the targeted list (DialogConditions / EventConditions) and NOT in the other list.
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
   - Work commit: `[v2.9.1 P3] Layer 3 workflow scenarios — N bugs surfaced`
   - Hash-record commit: `[v2.9.1 P3] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 3

- Layer 3 scenario(s) executed.
- Each list-target assertion documented as pass/fail with readback evidence.
- Test patches deleted; modlist clean.
- Bug list extended with workflow-scenario finds.
- Handoff § Conductor asks names whether Phase 4 is needed.

---

## Phase 4 — Bridge fixes + matrix corrections + docs hygiene (CONDITIONAL)

**Goal:** Land all v2.9.1-bound bridge fixes, schema enhancements, matrix corrections, and docs hygiene that Phase 2 + Phase 3 surfaced. Conductor decides whether this phase runs at all (skip if zero findings) and whether it splits into sub-sessions per bug if findings don't fit one budget.

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
- **Scope-lock for Phase 4:** items the kickoff names are in scope. Other v2.7.1/v2.8.0/v2.9.0 carry-overs (Boolean dispatcher branch, sub-B 6 String functions, AMMO enchantment, replace-semantics dict, chained dict access, QUST.Aliases / Stages / Objectives, PERK.Effects) stay deferred unless the kickoff explicitly absorbs them per Aaron's call. The discipline from v2.8.0 P4 + v2.9.0 P4-INFO holds: "don't punt v2.9.1-uncovered findings; pre-existing carry-overs not surfaced fresh stay deferred."
- **Bonus-catch precedent:** fold in only if load-bearing for the current item. >1h additional or new operator surface → halt + conductor ask + Aaron decision.

### Steps

(Per-item steps depend on what the conductor's kickoff names. The general shape mirrors v2.9.0 Phase 4: pre-fix probe → fix → regression test → build clean → coverage-smoke green. See v2.9.0 PLAN.md § Phase 4 for the canonical step structure.)

1. **Confirm scope from kickoff.** List the items in scope to Aaron in your acknowledgement.

2. **Per item:** probe → fix → regression test → smoke green.

3. **Build the bridge** post all fixes. Zero warnings, zero errors.

4. **Run coverage-smoke end-to-end.** All cells from prior phases + new regression cells, all PASS.

5. **Update CHANGELOG + KNOWN_ISSUES** per items landed.

6. **Write `PHASE_4_HANDOFF.md`** documenting per-item completion, smoke counts, change summaries.

7. **Commit** (double-commit cadence):
   - Work commit: `[v2.9.1 P4] Bridge fixes + matrix corrections + docs hygiene`
   - Hash-record commit: `[v2.9.1 P4] Handoff: record commit hash <work-hash>`
   Push both.

### Acceptance — Phase 4

- All items the kickoff named are landed (or partial state is documented in handoff with reason).
- Bridge builds clean.
- Coverage-smoke at total (v2.9.0 baseline + Phase 2 cells + Phase 4 regression cells), all PASS.
- CHANGELOG + KNOWN_ISSUES updated.
- Handoff under 400 lines.

---

## Phase 5 — Re-run + ship v2.9.1

**Goal:** Final verification pass + ship the v2.9.1 release. Phase 2 guaranteed code changes; this is always a real release.

**Files to touch:**
- `<repo>/build-output/installer/claude-mo2-setup-v2.9.1.exe` (built artifact)
- `<repo>/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `<repo>/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `<plan>/PHASE_5_HANDOFF.md`

**Conductor decisions relevant to this phase:**
- Bridge SHA preservation chain matters. Phase 5's `dotnet publish` produces a NEW SHA (different from Phase 2/4's build SHA). That new SHA is the canonical v2.9.1 ship SHA. It must be byte-identical across smoke matrix, installer bundle, and live install. To preserve: build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller`, which rebuilds the bridge and breaks the chain).
- Layer 3 workflow re-run is required if Phase 4 ran (Phase 4 may have introduced bridge changes Phase 3 didn't see). If Phase 4 was skipped, Phase 3's runs satisfy the re-run requirement.
- Full MO2 process restart required after live sync (not just Tools menu Stop/Start). Conductor confirms this in kickoff.

### Steps

(Mirrors v2.9.0 Phase 5 — see v2.9.0 PHASE_5_HANDOFF.md for the canonical 12-step ship sequence with halt cadence.)

1. Verify session start (state checks per kickoff).

2. Final coverage-smoke run against latest bridge build. Confirm 100% pass.

3. **If Phase 4 ran:** re-run Layer 3 scenarios against the post-Phase-4 bridge. **If Phase 4 skipped:** skip this step.

4. Build production bridge via `dotnet publish`. Capture SHA.

5. Build installer via direct ISCC invocation (NOT `build-release.ps1 -BuildInstaller` — preserves SHA chain). Capture installer SHA.

6. Live sync: copy bridge + Python files to `<live>/`. Aaron full-restarts MO2. `mo2_ping` returns v2.9.1.

7. Live sanity check: 2–3 representative scenarios (one QUST DialogConditions add, one QUST EventConditions add, one regression — non-QUST `add_conditions` without `condition_target` confirming v2.9.0 path stays bit-identical).

8. Insert ship date in CHANGELOG.

9. **Tag + push tag + GitHub release** (PUBLIC; hard to undo). MANDATORY HALT — show Aaron the prepared release-notes draft + exact command sequence; wait for explicit "ship" go-ahead.

10. Update memory (`project_capability_roadmap.md`).

11. Write `PHASE_5_HANDOFF.md`.

12. Final commit + handoff hash-record commit + push.

### Acceptance — Phase 5

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.9.1` resolves with installer attached.
- `<live>/` running v2.9.1 (`mo2_ping`).
- Memory reflects v2.9.1 shipped.
- SHAs captured.
- Bridge SHA matches across smoke matrix, installer bundle, and live install (single audit anchor).

---

## ⚠️ Carry-overs (NOT addressed in v2.9.1; future-release candidates)

These are explicitly out of scope for v2.9.1 unless real-world testing surfaces them as actually-blocking. If Phase 2/3 surface them as bugs, conductor decides whether to promote to Phase 4 fix scope per the discipline from v2.9.0 P4.

1. **Boolean dispatcher branch** (deferred from v2.9.0 — design-only, no in-scope consumer). PLAN.md v2.9.X § A names six branches; v2.9.0 ships five. First v2.9.x consumer trigger lands the branch + cell + name simultaneously.
2. **6 sub-B Condition functions with String-typed slots** (deferred from v2.9.0): GetGraphVariableFloat, GetGraphVariableInt, GetQuestVariable, GetScriptVariable, GetVMQuestVariable, GetVMScriptVariable. Routing requires accept-any-string operator-surface decision.
3. **AMMO enchantment.** Mutagen schema gap; upstream change required.
4. **Replace-semantics whole-dict assignment** (Tier C dicts). Carried over from v2.7.1.
5. **Chained dict access** (`Foo[Key].Sub`). Carried over from v2.7.1.
6. **QUST.Aliases / Stages / Objectives, PERK.Effects.** Out of scope for v2.8.0's bounded Effects-list mechanism — sub-class polymorphism harder; defer until real consumer surfaces. Note: distinct from v2.9.1's QUST top-level `DialogConditions`/`EventConditions` — those are major-record-level lists, while Aliases/Stages/Objectives are nested-major-record sub-records.
7. **Multi-condition record types beyond QUST** if Phase 1's probe surfaces them and Aaron locks QUST-only. Documented in PHASE_1_HANDOFF.md if applicable.
8. **GetVATSValueUnknown Mutagen 0.53.1 schema gap.** Deferred from v2.9.0 — bridge dispatcher write is correct; downstream Mutagen serializer throws NotImplementedException. v2.9.x candidate when Mutagen 0.54+ implements the missing override.
9. **All v2.6.0 / v2.7.0 / v2.7.1 / v2.8.0 / v2.9.0 deferrals** — see prior plan handoffs.
