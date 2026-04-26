# Phase 1 Kick-off — Inventory probe + Pareto proposal

Paste this into a fresh Claude Code session opened at `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\`.

---

You are the **Phase 1 executor** for the v2.9.0 Claude_MO2 release. Your job is to enumerate every concrete `*ConditionData` subclass in Mutagen 0.53.1 with its non-base reflection slots, categorize functions by parameter shape, propose a Pareto cluster for the v2.9.0 in-scope function set, surface that proposal to Aaron via the conductor for an explicit lock, and (once lock returns) update `MATRIX.md` per its Phase fill-in checklist. **No bridge code changes. No version bump.**

## Context (read this once, don't search for history)

v2.8.0 shipped at `419a719`. Phase 0 (commits `b5edf14` + `d6e6db6`) landed the v2.9.0 plan, MATRIX scaffold, and Layer 3 scenario pre-spec. Aaron has locked working slug **v2.9.0** and given two pre-litigated guidance signals you inherit:

- **Aggressive Pareto.** Aaron wants as much capability as effort allows — don't default-conservative. The floor (PLAN.md § Phase 1) is a starting point, not a ceiling. Stretch candidates are eligible if Pareto evidence supports; further functions beyond the stretch list are also eligible if your inventory turns up high-value, low-effort additions.
- **Slot-type expansion pre-authorized within the `RouteParameterSlot` envelope.** If you surface an exotic slot type (multi-FormLink, custom Loqui type, etc.) that Phase 2 can absorb cheaply (small extension to `RouteParameterSlot`, no new operator surface, no MCP shape change), Aaron has pre-authorized absorption — flag it in CONDITIONS_AUDIT.md as "in scope; absorbed under § E pre-auth" rather than escalating. **Threshold for escalation: >1h additional Phase 2 work, OR new operator surface, OR new MCP request/response shape.** Anything over those goes via § Conductor asks.

The dispatcher Phase 2 will build is the generalization of v2.8.0's `actor_value` handler. v2.8.0 did `Enum.Parse<ActorValue>` reflection write into `ActorValueConditionData.ActorValue`. v2.9.0 generalizes per PLAN.md § A:

```
For each (slotName, value) in conditionEntry.Parameters:
    var prop = condDataType.GetProperty(slotName);
    Route by prop.PropertyType:
        - IFormLinkOrIndex<T>      → Global-handler pattern (FormKey + ctor)
        - Enum (any)               → Enum.Parse(prop.PropertyType, value, ignoreCase: true)
        - Int32 / Single / Boolean → direct conversion
```

Your inventory tells the conductor and Aaron how big the addressable set is at each slot-type shape and which functions are highest-value-per-effort.

## Session-start ritual

1. **Verify session start.**
   - `git rev-parse HEAD` → `d6e6db6…` (Phase 0 hash-record commit; v2.8.0 ship is at `419a719`).
   - Working tree clean.
   - `mo2_ping` returns `version: "2.8.0"` (live install untouched in Phase 1).
2. **Read these files in full, in order:**
   - `dev/plans/v2.9.X_condition_parameters/PLAN.md` § Session-start ritual + § Phase 1 + § E (cross-phase decisions) + § Handoff template + § Communicating with the conductor.
   - `dev/plans/v2.9.X_condition_parameters/PHASE_0_HANDOFF.md` — most-recent state. Conductor decisions inherited are restated here.
   - `dev/plans/v2.9.X_condition_parameters/MATRIX.md` — focus on § Cell-naming convention + § Layer 1.P scaffold + § Phase fill-in checklist. You'll edit Layer 1.P / 1.D rows post-Pareto-lock.
   - `dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` — audit-doc template. CONDITIONS_AUDIT.md mirrors this role: per-shape categorization, per-function slot signatures, architectural-surprise capture, Pareto evidence.
3. **Skim, don't memorize:**
   - `tools/race-probe/Program.cs` — find the existing v2.8 P4 probe sections; your inventory dump appends after them in the same style. Note the Mutagen reflection helpers already established (you'll reuse them rather than rebuild).
   - `tools/mutagen-bridge/PatchEngine.cs` `BuildCondition` (~line 1608) — read the existing `actor_value`, `Global`, and `RunOnType` handlers. They're the working-precedent shape `RouteParameterSlot` will generalize; you don't modify them in Phase 1, only confirm you understand the precedent for CONDITIONS_AUDIT.md notes.

## Conductor decisions (locked — do not re-litigate)

1. **Version slug = `v2.9.0`** (Phase 0 locked; Phase 2 commits the bump constants).
2. **Aggressive Pareto guidance** — see Context above.
3. **Slot-type expansion pre-authorized within `RouteParameterSlot` envelope** — see Context above. Threshold for escalation is sharp: >1h, new operator, new MCP shape.
4. **Phase 2 split trigger:** if your locked Pareto pick exceeds ~12 in-scope functions, the conductor will split Phase 2 into 2A (infrastructure + first ~7 functions) and 2B (remaining). This doesn't affect Phase 1's deliverable — propose what you propose; the conductor handles the split decision.

## Phase 1 deliverables

| # | Item | Files |
|---|---|---|
| 1 | Inventory dump section appended to race-probe | `tools/race-probe/Program.cs` |
| 2 | Probe build + run; full output captured | `<workspace>/scratch/v2.9-phase-1-inventory.txt` |
| 3 | CONDITIONS_AUDIT.md — total count, per-shape categorization, per-candidate slot signatures, architectural surprises, error-template confirmation | `<plan>/CONDITIONS_AUDIT.md` (NEW) |
| 4 | Pareto proposal in handoff § Conductor asks (CONDUCTOR ASK format per PLAN.md § Communicating with the conductor) | `<plan>/PHASE_1_HANDOFF.md` |
| 5 | **Halt** — wait for Aaron's lock via conductor relay | — |
| 6 | _Once lock returns:_ update MATRIX.md per its Phase fill-in checklist | `<plan>/MATRIX.md` |

## Working pattern: propose, then execute

Before making ANY changes:

1. Identify yourself to Aaron as "Phase 1 executor" and confirm session-start state checks.
2. Recap deliverables in your own words. Note that Phase 1 ends with a halt — Aaron's Pareto lock is required before MATRIX update or Phase 2 spawn.
3. Propose your work plan: probe extension order (NoParam → Enum → FormLinkOrIndex → MultiSlot → PrimitiveOnly → Exotic), how you'll filter abstract / overlay / interface types, where in `Program.cs` the new section appends, what scratch file path you'll use.
4. Wait for go-ahead.

## Standard halt-and-report points (mid-session)

- **After inventory probe runs to completion, BEFORE drafting the Pareto proposal.** Show Aaron the raw per-shape summary (counts + function names per shape — keep it tight, no slot signatures yet) so he can interject if anything jumps out before you commit a Pareto recommendation. This is the natural inspection point for the "aggressive Pareto" guidance to land informed by what's actually there.
- After CONDITIONS_AUDIT.md draft is complete, BEFORE writing the handoff, confirm with Aaron that the Pareto proposal you're about to formalize matches what he'd expect from the categorization.

## Mandatory halt-and-report triggers (any → halt immediately)

- Race-probe build fails (`dotnet build -c Release` returns non-zero or surfaces warnings).
- Probe surfaces a function whose ConditionData breaks the uniform reflection convention (e.g. nested sub-objects on a parameter slot, computed properties, slot types outside `IFormLinkOrIndex<T>` / enum / int / float / bool that don't absorb cheaply).
- Probe surfaces an exotic slot shape that exceeds the pre-auth envelope (>1h Phase 2 work OR new operator OR new MCP shape) — write a `CONDUCTOR ASK` for Aaron's call rather than assuming.
- Bonus-catch surfaces in race-probe that's > 1h additional or adds new operator surface.
- Pre-existing test (1 → N) starts failing after a Phase 1 change to race-probe (Phase 1 doesn't touch coverage-smoke or the bridge, but race-probe regressions still warrant a halt).

## Pareto proposal — CONDUCTOR ASK format

Write the proposal at the bottom of your in-progress `PHASE_1_HANDOFF.md` under § Conductor asks. Use this exact format (per PLAN.md § Communicating with the conductor):

```
CONDUCTOR ASK
Phase: 1
Topic: v2.9.0 in-scope function set — Pareto lock
Context:
  - Inventory total: <N> concrete *ConditionData types; per-shape: NoParam <a>, Enum <b>, FormLinkOrIndex <c>, MultiSlot <d>, PrimitiveOnly <e>, Exotic <f>.
  - Floor (per PLAN.md § Phase 1): GetIsID, GetInFaction, GetInCell, HasMagicEffect, HasPerk, HasSpell, GetIsRace + ActorValue carryover.
  - Stretch (per PLAN.md § Phase 1): GetItemCount, IsInList, WornHasKeyword, GetEquipped.
  - Aaron has signalled aggressive Pareto guidance — pull beyond stretch if evidence supports.
Question: Lock the in-scope function set for v2.9.0?
Suggested options:
  A: <floor only — N functions> — rationale: <one-line>
  B: <floor + stretch — N functions> — rationale: <one-line>
  C: <floor + stretch + additional <list> — N functions> — rationale: <one-line>; per-function-effort delta documented in CONDITIONS_AUDIT.md.
  (Add D/E if your inventory turns up additional clusters worth offering.)
Default if no response in 24h: floor (option A) — most conservative; preserves Phase 2 scope.
```

Recommend an option in the proposal — don't be neutral. Aaron can override, but a recommendation backed by your evidence is the highest-value version of this ask.

## Acceptance criteria (Phase 1 complete — from PLAN.md § Phase 1)

- Inventory probe runs to completion; CONDITIONS_AUDIT.md captures total + per-shape categorization + per-candidate-function slot signatures (slot name + type, including the inner T for FormLink slots).
- Pareto proposal written in handoff § Conductor asks; conductor has it for Aaron sign-off.
- Race-probe builds clean (0 warnings, 0 errors).
- MATRIX.md updated with in-scope function cells per the Phase fill-in checklist (or noted as pending Phase 2's first step if the lock lands too late in this session — explicit in handoff).
- Handoff under 400 lines.

## Commit format

Subject: `[v2.9 P1] <description>`. Body: bullets. End with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Heredoc for multi-line.

Double-commit cadence:
- Work commit: `[v2.9 P1] Condition-parameter inventory probe + Pareto proposal`
- Hash-record commit: `[v2.9 P1] Handoff: record commit hash <work-hash>`

Push both.

## Out of scope for Phase 1

- Touching bridge code (`PatchEngine.cs`, `Models.cs`) — Phase 2 owns the dispatcher.
- Touching `coverage-smoke` — Phase 2 owns regression cells.
- Touching `tools_patching.py` schema — Phase 2 owns.
- `CHANGELOG.md` / `KNOWN_ISSUES.md` updates — Phase 2 owns.
- Version bump (`config.py` / `.iss` / `README.md`) — Phase 2 owns.
- Live install sync — Phase 1 doesn't touch live (Phase 3 first does).
- Picking live FormIDs for Layer 3 scenarios — Phase 3 owns.
- Designing or implementing `RouteParameterSlot` itself — Phase 2 owns. You only confirm the existing precedent (`actor_value`, `Global`) shapes in PatchEngine for CONDITIONS_AUDIT.md notes.

## End-of-phase ritual

When done:

1. Confirm final state matches acceptance criteria.
2. Write `dev/plans/v2.9.X_condition_parameters/PHASE_1_HANDOFF.md` per the template at the bottom of `PLAN.md`. Sections you must populate:
   - **What was done** — race-probe inventory section, scratch capture, CONDITIONS_AUDIT.md, MATRIX update status.
   - **Verification performed** — probe build clean; probe ran to completion; output line counts; per-shape totals.
   - **Bugs surfaced** — any race-probe regression (none expected); any architectural surprise that needs escalation.
   - **Deviations from plan** — anything that drifted from PLAN.md § Phase 1.
   - **Known issues / open questions** — particularly: anything Phase 2 needs to know about exotic shapes, abstract subclasses, or non-uniform reflection cases.
   - **Conductor asks** — the Pareto proposal in the format above. **Required for Phase 1.**
   - **Preconditions for Phase 2** — confirm the lock-state cleanly: "Pareto lock pending conductor relay" OR "Lock in: <function set>" if Aaron responded in-session.
   - **Files of interest for Phase 2** — PatchEngine.cs:1608 (BuildCondition entry-point), Models.cs (ConditionEntry definition), CONDITIONS_AUDIT.md (slot signatures source-of-truth), MATRIX.md § Phase fill-in checklist remainder if lock-update was deferred.
3. **Do NOT write the next phase's kick-off prompt.** The conductor will write Phase 2's after Aaron's Pareto lock returns.
4. Force-add new files: `git add -f Claude_MO2/dev/plans/v2.9.X_condition_parameters/{CONDITIONS_AUDIT.md,PHASE_1_HANDOFF.md,PHASE_1_KICKOFF_PROMPT.md}` plus `Claude_MO2/dev/plans/v2.9.X_condition_parameters/MATRIX.md` if you updated it.
5. Work commit + hash-record commit + push.

## What "good" looks like

- A `CONDITIONS_AUDIT.md` opened side-by-side with v2.8.0's `EFFECTS_AUDIT.md` reads as a structural sibling: per-shape categorization with counts + function lists, per-candidate-function slot signature dump, architectural-surprise section (even if empty — say so), error-template confirmation that the wording in PLAN.md § C renders cleanly.
- A Pareto proposal under § Conductor asks that Aaron can act on in 60 seconds: counts up top, options A/B/C with one-line rationale each, recommendation explicit, default-if-no-response named.
- A race-probe inventory section that future v2.9.x point releases can re-run without modification when adding more functions to the in-scope set (probe is a tool, not a one-shot).

---

Confirm you've identified yourself as Phase 1 executor + state-checks pass, then propose your work plan.
