# v2.8.0 — Verification + Hardening + Effects-list writability

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-25, immediately after v2.7.1 ship.
**Re-scoped:** 2026-04-25, same day, when a real-world consumer hit a SPEL.Effects-write gap during a custom-race rebuild patch. Original framing was pure verification; re-scope adds one bounded capability addition as the headline.
**Baseline:** v2.7.1 (shipped 2026-04-25).
**Target version:** v2.8.0.
**Sessions estimated:** 5–6 (one per phase, plus one per surfaced bug in Phase 4).
**Mandate:** (1) Add Effects-list writability to the bridge so `set_fields: {Effects: [...]}` works on SPEL/ALCH/ENCH/SCRL/INGR records — surfaced from real consumer use of v2.7.1. (2) Real-world exercise of every (operator, record-type) wire-up landed in v2.7.1 plus the new Effects-list capability. (3) Regression-check the pre-existing 24 (operator, record-type) pairs that v2.7.1's Tier D refactor touched. (4) Surface bugs with certainty; root-cause and fix what surfaces, including the documented PerkAdapter/QuestAdapter `attach_scripts` carry-over.

This is a **bounded** capability-expansion release, not pure verification. ONE new mechanism — JSON Array → list-of-LoquiObject conversion in `set_fields` — and the carry-over candidates from v2.7.1's KNOWN_ISSUES that fall out of it for free (per-effect spell conditions). The other carry-over candidates (Quest condition disambiguation, AMMO enchantment, replace-semantics whole-dict, chained dict access) remain deferred unless Phases 2+3 surface them as blockers.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |
| `<modlist>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\` (the MO2 instance root — `<live>`'s grandparent) |

When generating bash commands, always wrap these paths in quotes — they contain spaces (`Stuff for Calude`, `Authoria - Requiem Reforged`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. **Before touching anything**, do this in order:

1. **Identify your phase.** Look in this directory:
   ```
   Claude_MO2/dev/plans/v2.8.0_verification/
   ```
   Find the highest-numbered file matching `PHASE_*_HANDOFF.md`. **Your phase is one greater than that.** If no handoffs exist yet, you are **Phase 0**. If `PHASE_5_HANDOFF.md` exists, **the release is shipped** — point the user at it and stop.

   **Phase 4 special case:** Phase 4 fixes one bug per session. The handoff filename is `PHASE_4_<bug-slug>_HANDOFF.md` (e.g. `PHASE_4_perk_adapter_HANDOFF.md`). The bug list is in `PHASE_3_HANDOFF.md` (or `PHASE_2_HANDOFF.md` if no Phase 3 surfaced bugs); pick the next un-fixed bug. If every listed bug has a corresponding `PHASE_4_<slug>_HANDOFF.md`, advance to Phase 5.

2. **Read the previous handoff** in full. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, and what to write in your own handoff.

4. **Read `MATRIX.md`** in this directory. Phase 0 produces it; Phases 1–5 use it as the authoritative test specification. **Phase 1 also reads `EFFECTS_AUDIT.md`** if Phase 1 produced one (recommended for capturing the API-contract probe results).

5. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry
   - `Claude_MO2/KNOWN_ISSUES.md`
   - **Skip** the session-summaries / handoffs sweep — this plan is your roadmap.
   - Check `<workspace>/Live Reported Bugs/` root for anything new.

6. **Confirm with the user** which phase you've identified yourself as and any deviations you've noticed. Wait for go-ahead before making changes.

7. **At the end of your phase**, write `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md`) in this directory using the template at the bottom of this file.

**One phase per session.** If you finish early, summarise and stop — don't roll into the next phase.

---

## 📋 Background — why this plan exists

v2.7.1 shipped on 2026-04-25 with a substantial bridge expansion: 16 (operator, record-type) wire-ups across 9 record types, plus three generic mechanisms (Tier D silent-failure detection, Tier C bracket-indexer + JSON-object dict syntax, Tier B RACE field aliases). Phase 5's pre-ship smoke matrix exercised every wire-up once with positive cases plus a single Tier D negative; the live sanity check exercised three records.

Two things happened that drove this plan:

1. **Within hours of v2.7.1 ship, a real consumer hit the Effects-list-write gap** during a custom-race rebuild patch. The race-layer edits worked (add_keywords, add_spells on RACE both succeeded — v2.7.1's Tier A landed correctly). The follow-on SPEL edits failed: the consumer attempted `set_fields: {Effects: [...]}` to overwrite a racial ability spell's effect list, and the bridge could not convert a JSON array of magic-effect descriptors into Mutagen's `RefList<Effect>`. Workaround was to write a local Mutagen helper to patch the ESP after `mo2_create_patch` did the race overrides. The bug is documented as a v2.7.1 KNOWN_ISSUES carry-over candidate ("Per-effect spell conditions … v2.8 if a real consumer surfaces"), but the broader gap is the entire `Effects` array, not just nested `Conditions` per effect.

2. **Phase 5's smoke matrix is thin coverage.** Each pair was hit once with one record. Real-world patches stack operations on a record (e.g. RACE with kw + sp + Starting + Regen + flags in one call), and Tier D's coverage check has to handle multi-operator-per-record correctly. Tier C edges weren't exercised (malformed brackets, idempotency, BipedObjectNames, ESL-master FormLink resolution). Pre-existing handlers were heavily refactored in v2.7.1 Phase 1 (15 conditional `if (added > 0)` writes scrubbed; 7 short-circuits refactored; enchantment inverse fix; ApplyRemoveConditions alignment) — the pre-existing 24 (operator, record-type) pairs need a regression check. The PerkAdapter/QuestAdapter `attach_scripts` bug is documented but never directly probed.

The verification mandate from `project_capability_roadmap.md`:

> v2.8 = verification/hardening release. Real-world exercise of every wire-up landed in v2.7.1; bugs surfaced get fixed. No new capabilities planned.

Aaron's amended framing on 2026-04-25 (this re-scope): **prioritize and fold in** the Effects-list capability addition — bounded scope, real consumer surfaced, carry-over promotion. v2.8 becomes "verification + Effects-list write" rather than pure verification.

This is the same re-scoping pattern v2.7.1 used (RACE-only → comprehensive coverage when investigation found the broader bug class). Scope discipline holds: Effects-list write is ONE mechanism, ONE shape (JSON Array → list of constructed LoquiObject entries via reflection), and the records benefiting are a fixed, schema-confirmed set.

---

## 🏗️ Architecture — testing strategy + Effects-list capability (locked)

### A. Effects-list write capability (Phase 1 deliverable)

**The mechanism.** Extend `ConvertJsonValue` in PatchEngine.cs to handle:

```
JSON Array → ExtendedList<T> / RefList<T> where T is a Mutagen LoquiObject (not a FormLink)
```

For each JSON object in the array, construct a fresh `T` via `Activator.CreateInstance(typeof(T))`, then iterate the JSON object's members and set each via the existing `SetPropertyByPath` recursion — including nested FormLinks (e.g. `Effect.BaseEffect`), nested LoquiObjects (e.g. `Effect.Data` if it's structured), and nested lists (e.g. `Effect.Conditions`).

**Replace semantics, not merge.** A whole-array assignment in `set_fields` clears the source list and writes the JSON-supplied entries. This is the natural default for arrays — there's no key-based merge equivalent for ordered lists. (If a user wants to append rather than replace, they'd use the per-record `add_*` operators — e.g. `add_conditions` for the Conditions sub-list. But for `Effects` itself, no `add_effects` operator exists; whole-array `set_fields` is the only path. Replace semantics.)

**Records benefiting** (every magic-effect-list carrier in Mutagen 0.53.1's Skyrim schema):

| Record | Property | Type |
|---|---|---|
| Spell | `Effects` | `ExtendedList<Effect>` |
| Ingestible (ALCH) | `Effects` | `ExtendedList<Effect>` |
| ObjectEffect (ENCH) | `Effects` | `ExtendedList<Effect>` |
| Scroll (SCRL) | `Effects` | `ExtendedList<Effect>` |
| Ingredient (INGR) | `Effects` | `ExtendedList<Effect>` |

`Effect` itself is a LoquiObject with sub-fields:
- `BaseEffect: IFormLink<IMagicEffectGetter>` (FormLink → existing path)
- `Data.Magnitude: float`, `Data.Area: uint`, `Data.Duration: uint` (nested struct/object → reflection)
- `Conditions: ExtendedList<Condition>` (recursion into existing Tier C / `add_conditions` machinery)

**Carry-over absorbed:** Per-effect spell conditions (`Spell.Effects[i].Conditions`) ride the same recursion. No separate work; documented as folded in.

**Out of scope** for Phase 1, even within this mechanism:
- `QUST.Aliases` (`ExtendedList<QuestAlias>`) — different structural shape (aliases carry many sub-fields including FormLinks to Faction/Cell, package overrides, AI data). Probe if the AUDIT in Phase 1 surfaces an easy path; otherwise defer to v2.9 unless a real consumer surfaces.
- `QUST.Stages`, `QUST.Objectives` — same complexity.
- `PERK.Effects` — perk effects are a tagged-union shape with ~13 effect subclasses; out of scope for v2.8.0's bounded capability addition.

If Phase 1's AUDIT shows QUST.Aliases is reachable with the same mechanism for trivial sub-fields, fold in. If it requires sub-class-aware construction (likely), defer.

**Probe-first discipline.** Phase 1 starts with a `race-probe` extension that constructs an `Effect` in-memory, sets BaseEffect + Data.Magnitude + Conditions, appends to `Spell.Effects`, round-trips through binary write/read. If the API contract differs from expected (e.g. `Effect.Data` is a `Record` not a struct), the audit captures the actual shape and Phase 1 implementation transcribes the verified contract.

**Phase 1 deliverables:**
- `tools/race-probe/Program.cs`: extended with Effect API contract verification.
- `tools/mutagen-bridge/PatchEngine.cs`: `ConvertJsonValue` extended for JSON Array → list-of-LoquiObject; `SetPropertyByPath` updated if the assignment path differs from existing collection paths.
- `tools/coverage-smoke/Program.cs`: regression tests for SPEL.Effects, ALCH.Effects (sister case sanity check).
- `mo2_mcp/tools_patching.py`: schema description for `set_fields` updated to mention Effects-list write.
- `KNOWN_ISSUES.md`: per-effect-spell-conditions entry removed (now supported); Effects-list write surface added.
- `mo2_mcp/CHANGELOG.md`: v2.8.0 placeholder created with Phase 1 entry; subsequent phases append.
- Version bump: `config.py`, `installer/.iss`, `README.md` to v2.8.0 (first commit of v2.8.0 era).
- `dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md`: optional but recommended — per-record `Effects` API contract probe results, mirroring AUDIT.md's role in v2.7.1.
- `dev/plans/v2.8.0_verification/PHASE_1_HANDOFF.md`.

### B. Verification testing strategy (Phases 2–3)

Hybrid with matrix bias. Four assertion layers, executed across two harnesses.

#### Layer 1 — Coverage matrix (~57 assertions including the new Effects cells)

Every (operator, record-type) cell — Tier A wire-ups + pre-existing handlers + Tier D negatives + the new Effects-list cells. ≥2 representative records per cell where multiple records of that type exist in vanilla Skyrim.esm. Positive case (operator works on a real record, mod lands in output ESP, readback confirms). Negative case (Tier D catches the gap with structured error and rollback).

**Pre-existing handler regression band** (the 24 pairs not added in v2.7.1 but touched by Phase 1's refactor):
- `add_keywords` / `remove_keywords` × {Armor, Weapon, NPC, Ingestible, Ammunition, Book, Flora, Ingredient, MiscItem, Scroll} = 20
- `add_spells` / `remove_spells` × NPC = 2
- `add_perks` / `remove_perks` × NPC = 2
- `add_packages` / `remove_packages` × NPC = 2
- `add_factions` / `remove_factions` × NPC = 2
- `add_inventory` / `remove_inventory` × {NPC, Container} = 4
- `add_outfit_items` / `remove_outfit_items` × Outfit = 2
- `add_form_list_entries` / `remove_form_list_entries` × FormList = 2
- `add_items` × LeveledItem = 1 (LVLN/LVSP added in v2.7.1)
- `add_conditions` / `remove_conditions` × {MagicEffect, Perk, Package} = 6
- `attach_scripts` × {NPC, Armor, Weapon, Outfit, Container, Door, Activator, Furniture, Light, MagicEffect, Spell} = 11 (selected from the broad reflection-supported list)
- `set_enchantment` / `clear_enchantment` × {Armor, Weapon} = 4
- `set_fields` × {NPC, ARMO, WEAP, RACE} = 4
- `set_flags` / `clear_flags` × NPC = 2

Plus **Layer 1.E** (new): `set_fields: {Effects: [...]}` × {SPEL, ALCH, ENCH, SCRL, INGR} = 5 positive + 1 nested-Conditions = 6.

#### Layer 2 — Combinatorial probes (~12 assertions)

- Multi-operator-per-record, multi-record-per-patch with Tier D rollback isolation, cross-tier compositions (Tier A + Tier B + Tier C in one record), multiple-unmatched-operators, all-keys-overlap whole-dict, empty op set.
- **New:** `set_fields: {Effects: [...]}` combined with other operators on the same SPEL record (e.g. + `add_keywords`).

#### Layer 3 — Workflow scenarios (~5 scenarios, ~28 assertions)

Realistic patching tasks against the **live Authoria modlist**, not vanilla Skyrim.esm. Run via `mo2_create_patch`. Patches go to `<modlist>/mods/Claude Output/` with recognizable test filenames; deleted post-verification.

1. **Reqtify a custom race + ability spells.** Pick a RACE from a non-vanilla plugin. Patch: keywords (creature type + immunity), actor effects (resistance abilities). **Then patch the ability spells themselves** — each SPEL's Effects array via `set_fields` (the workflow that surfaced the gap). ~8 assertions.
2. **Patch a creature mod's keyword + script attachment.** Pick a creature mod with vanilla creatures restructured. Patch: add_keywords + attach_scripts on multiple NPC_/CREA records. ~5 assertions, including readback that script properties round-trip.
3. **Merge a leveled spell list addition.** Pick LVSP records overridden by both an overhaul and a content mod. Use `merge_leveled_list` (the merge path). ~4 assertions.
4. **NPC bundle.** Single NPC with `add_keywords` + `add_spells` + `add_perks` + `add_factions` + `add_inventory` + `set_outfit` (via override + add_outfit_items if applicable) + attach a script. ~8 assertions.
5. **MGEF condition + perk reweighting.** MGEF with `add_conditions` (magnitude check) + a parallel PERK record with `set_fields` on PerkSection entries via reflection. ~5 assertions.

#### Layer 4 — Edges + carry-over probes (~26 assertions)

- Malformed bracket syntax, idempotency, chained dict access rejection, replace-vs-merge confirmation, BipedObjectNames probe, ESL master interaction.
- **Six v2.8 carry-over candidates** (one ABSORBED, the rest stay deferred):
  1. Quest condition disambiguation → expect Tier D error.
  2. ~~Per-effect spell conditions → expect Tier D error.~~ **ABSORBED into Phase 1; expect success.**
  3. AMMO enchantment → expect Tier D error.
  4. Replace-semantics whole-dict → confirm merge-only behavior for Tier C dicts (separate from the new array-replace semantics).
  5. Chained dict access → expect explicit error.
  6. **PerkAdapter/QuestAdapter functional probe** → known concrete bug, fix in Phase 4 if Phase 1's probe confirms.

### C. Harness assignment

| Layer | Harness | Source data |
|---|---|---|
| 1 (matrix, including 1.E Effects) | `tools/coverage-smoke/Program.cs` extension | Vanilla Skyrim.esm (deterministic) |
| 2 (combinatorial) | `tools/coverage-smoke/Program.cs` extension | Vanilla Skyrim.esm |
| 3 (workflow) | `mo2_create_patch` direct calls (manual Phase 3 session) | Live Authoria modlist |
| 4 (edges + carry-overs) | `tools/coverage-smoke/Program.cs` extension + `tools/race-probe/` for the PerkAdapter/QuestAdapter readback (Mutagen direct, not bridge) | Vanilla Skyrim.esm |

`tools/coverage-smoke/Program.cs` is currently 1122 lines / 22 tests. Phase 2 extends it to roughly 100+ tests.

### D. Ship branching — REMOVED

The original v2.8 plan had a contingent ship branch (real release if any code change; verification stamp tag if zero changes). Phase 1 guarantees code changes (Effects-list capability + version bump). v2.8.0 always ships as a real release. Stamp branch is dead.

### E. Scope locks

- **One new mechanism only.** JSON Array → list-of-LoquiObject in `set_fields`. No other new operators, no new `op:` values, no new `set_fields` aliases beyond what Phase 1 specifically requires for Effects-list ergonomics (none currently planned).
- **Five record types only** for Effects-list (SPEL/ALCH/ENCH/SCRL/INGR). QUST.Aliases/Stages/Objectives, PERK.Effects deferred unless Phase 1's AUDIT shows trivial reach.
- **PerkAdapter/QuestAdapter exception.** Known concrete bug, in scope for Phase 4 if Phase 1's probe confirms.
- **The other v2.7.1 KNOWN_ISSUES carry-overs** (Quest conditions, AMMO enchantment, replace-semantics whole-dict, chained dict access) stay deferred. If Layer 2/3/4 surface them as blockers, Phase 4 fixes; otherwise they roll forward.
- **Probe-first discipline.** Phase 1 begins with a probe to verify the Effect API contract before any bridge code lands. Phase 4 fixes that touch reflection paths or dispatch logic begin with a probe demonstrating the failure mode.
- **Bonus-catch precedent.** If a phase fix surfaces a related latent issue in the touched file, fold in (with explicit handoff documentation), per v2.7.1's pattern. Don't punt to a future release if the issue is load-bearing for the current fix.
- **No partial phases.** If a phase can't complete, the handoff records partial state and lists what blocks the next phase.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive. If you find yourself wanting to modify something outside that list, stop and escalate.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Plan + matrix specification + record selection | `MATRIX.md`; `PHASE_0_HANDOFF.md`; PLAN.md force-added | None |
| **1** | **Effects-list write capability** | Bridge support for `set_fields: {Effects: [...]}` on SPEL/ALCH/ENCH/SCRL/INGR; race-probe extended; coverage-smoke regression tests; tools_patching.py schema; CHANGELOG; KNOWN_ISSUES; **version bump to 2.8.0**; optional `EFFECTS_AUDIT.md` | Phase 0 |
| 2 | Build harness + execute Layers 1+2+4 | `tools/coverage-smoke/Program.cs` extended to MATRIX.md scope; harness output captured; bug list documented in `PHASE_2_HANDOFF.md` | Phase 1 |
| 3 | Execute Layer 3 workflow scenarios against live install | Per-scenario assertions in `PHASE_3_HANDOFF.md`; bug list extended | Phase 2 |
| 4 | Triage + per-bug fix sessions (one bug per session) | Per-bug `PHASE_4_<slug>_HANDOFF.md`; code commits; regression test added to `coverage-smoke` | Phase 3 (bug list) |
| 5 | Re-run + ship v2.8.0 | Final smoke run; installer + bridge artifact rebuilt; live sync; tag pushed; `gh release create`; memory updated | Phase 4 |

**Live state at plan creation (2026-04-25):**
- v2.7.1 public on GitHub at `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.7.1`. `origin/main` at `2799789` (handoff hash record for Phase 5). v2.7.1 tag at `c698e16`.
- Live install confirmed at v2.7.1 via `mo2_ping`.
- One real-world bug surfaced from a v2.7.1 consumer session on the same day: SPEL.Effects-write gap. Drives Phase 1's scope. No formal Live Reported Bug filing yet — the consumer's session note is the source.
- Active workstream memory (`project_capability_roadmap.md`) points at this plan as the v2.8 entry point.

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one or more commits per its scope. Commit messages start with `[v2.8 PN]` or `[v2.8 P4 <slug>]` (e.g. `[v2.8 P4 perk_adapter] Fix PerkAdapter auto-create path on PERK records with no existing scripts`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its handoff via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. **Phase 1 bumps to v2.8.0 on its first commit** (Effects-list capability addition is the trigger). Subsequent phases don't re-bump.
- **Live install sync:** Phases 0, 1, 2 do not touch the live install. Phase 3 reads via `mo2_create_patch` against the live install (writes to throwaway scratch ESPs in `<modlist>/mods/Claude Output/`, deletes after). Phase 4 fix sessions live-sync only when the bug requires verification on the live install (most fixes verify in `coverage-smoke` against vanilla Skyrim.esm). Phase 5 live-syncs once and ships.
- **Probe-first discipline:** Phase 1 starts with a race-probe extension verifying the Effect API contract. Any Phase 4 fix that touches PatchEngine.cs's reflection paths or dispatch logic begins with a probe demonstrating the failure mode; the probe (now passing) becomes the regression test.
- **One phase per session, one bug per Phase 4 session.**
- **Don't touch out-of-phase files.** Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves you spot during work.
- **No changes to MCP tool request/response shapes** unless a Phase 4 fix requires it (and even then, prefer additive changes — new fields are safer than rename/restructure). Phase 1 adds capability via the existing `set_fields` field; no shape change.

---

## 🔁 Handoff template

Every phase ends by writing `PHASE_N_HANDOFF.md` (or `PHASE_4_<slug>_HANDOFF.md`) in this directory. Use this exact structure:

```markdown
# Phase N Handoff — <one-line summary>

**Phase:** N (or "4 — <bug-slug>")
**Status:** Complete | Partial | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** <hashes or "none">
**Live install synced:** Yes/No (path: ...)

## What was done
<Bulleted list of concrete changes — file paths + one-line descriptions.>

## Verification performed
<What tests / smoke checks ran. What evidence shows it worked. For Phase 1 specifically: probe output + coverage-smoke regression results. For Phase 2: harness output table — every assertion row + result. For Phase 3: per-scenario assertion checklist + readback evidence. For Phase 4: probe evidence pre-fix + post-fix.>

## Bugs surfaced (Phase 2, Phase 3 only)
<Per-bug entry: short slug; record type + operator; reproduction (smoke test row or workflow step); failure mode (what the bridge reported vs what readback showed); proposed Phase 4 fix angle.>

## Deviations from plan
<Anything you did differently from PLAN.md. Why. If you didn't deviate, write "None.">

## Known issues / open questions
<Bugs you found but didn't fix (with reason). Questions the next phase needs to answer. If none, write "None.">

## Preconditions for next session
<Confirm each precondition the next session requires. Flag any not met.>

## Files of interest for next session
<List paths the next session will most need to read.>
```

Keep handoffs short — under 400 lines.

---

# PHASES

---

## Phase 0 — Plan + matrix specification + record selection

**Goal:** Produce `MATRIX.md`, the per-cell test specification, mirroring `AUDIT.md`'s role for v2.7.1. Pre-select the vanilla Skyrim.esm test records for Layers 1+2+4 and the live-modlist test records for Layer 3 (Phase 3). **No production code changes.** **No version bump.**

**Files to touch:**
- `Claude_MO2/dev/plans/v2.8.0_verification/PLAN.md` (this file — force-add)
- `Claude_MO2/dev/plans/v2.8.0_verification/MATRIX.md` (NEW)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_0_HANDOFF.md` (NEW — written at end)

### Steps

1. **Verify session start.** Confirm `origin/main` is at `2799789` (or later) and clean. Live install at `<live>` running v2.7.1 (`mo2_ping` returns `version: "2.7.1"`).

2. **Confirm `MATRIX.md` covers all four layers** — Tier A wire-ups, Tier B aliases, Tier C bracket/merge, regression band, Tier D negatives, **Layer 1.E Effects-list cells**, combinatorial probes, workflow scenarios, edges, carry-over probes. Total assertion count documented.

3. **Pre-select vanilla Skyrim.esm test records** for Layer 1 / 2 / 4 cells. Use `coverage-smoke`'s existing `FirstOrDefault` predicate-selection pattern (line ~70-90 of current Program.cs). Where multi-record depth is needed, second-matching-record per predicate.

4. **Pre-spec Layer 3 workflow scenarios** with target FormIDs from the live modlist. Aaron may swap during Phase 3. Scenario 3.1 (Reqtify-race + ability spells) is the consumer-driven scenario that surfaced Phase 1's gap — confirm the intended SPEL records.

5. **Force-add PLAN.md and MATRIX.md.** `git add -f Claude_MO2/dev/plans/v2.8.0_verification/{PLAN.md,MATRIX.md}`.

6. **Write `PHASE_0_HANDOFF.md`** confirming MATRIX scope, vanilla record pre-selection, Layer 3 scenarios pre-spec'd, no production code touched, no version bump.

7. **Commit:** `[v2.8 P0] Plan + matrix specification + record selection`. Push.

### Acceptance

- `MATRIX.md` exists with per-cell spec for all four layers, including Layer 1.E (Effects-list).
- Pre-selected records / predicates documented.
- `git diff main^` shows: PLAN.md (new), MATRIX.md (new), PHASE_0_HANDOFF.md (new). No production code touched. `config.py` / `.iss` / `README.md` / `CHANGELOG.md` unchanged.

---

## Phase 1 — Effects-list write capability

**Goal:** Implement bridge support for `set_fields: {Effects: [...]}` on SPEL/ALCH/ENCH/SCRL/INGR records. JSON Array → `ExtendedList<Effect>` via reflection-based per-entry construction. Per-effect Conditions ride the same recursion (carry-over absorbed). Probe-first; tested via coverage-smoke regression cell.

**Files to touch:**
- `Claude_MO2/tools/race-probe/Program.cs` (extend with Effect API contract verification)
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (`ConvertJsonValue` extension)
- `Claude_MO2/tools/coverage-smoke/Program.cs` (SPEL.Effects + ALCH.Effects regression tests)
- `Claude_MO2/mo2_mcp/tools_patching.py` (schema description for `set_fields`)
- `Claude_MO2/mo2_mcp/CHANGELOG.md` (new `## v2.8.0 — TBD` entry; Phase 1 bullet)
- `Claude_MO2/mo2_mcp/config.py` (`PLUGIN_VERSION = (2, 8, 0)`)
- `Claude_MO2/installer/claude-mo2-installer.iss` (`#define AppVersion "2.8.0"`)
- `Claude_MO2/README.md` (installer download URL → v2.8.0 at lines 7 and 59)
- `Claude_MO2/KNOWN_ISSUES.md` (per-effect-spell-conditions entry removed; Effects-list write surface added)
- `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` (NEW — optional, recommended; per-record Effects API contract probe results)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_1_HANDOFF.md`

### Steps

1. **Read MATRIX.md's Layer 1.E section** for the cells Phase 2 will exercise — that's the contract Phase 1 must deliver against.

2. **Extend `tools/race-probe/Program.cs` with an Effects API contract verification block.** For each of SPEL, ALCH, ENCH, SCRL, INGR:
   - Construct the record in-memory via `Activator.CreateInstance` (or the appropriate Mutagen factory).
   - Confirm `Effects` property exists with type `ExtendedList<Effect>`.
   - Construct an `Effect`: set `BaseEffect` to a known MGEF FormLink; set `Data.Magnitude / Area / Duration` (verify `Data` is a property — struct or sub-LoquiObject); attempt to add a `Condition` to `Effect.Conditions`.
   - Append the Effect to the record's `Effects`.
   - Round-trip: write the record to a binary ESP, re-parse, confirm Effects[0] preserves all fields.
   - Capture the API contract (specifically: `Effect.Data`'s actual runtime type and reflection accessibility) in `EFFECTS_AUDIT.md`.

   If any of the 5 records fails the probe (e.g. ENCH's Effect type is structurally different): document in EFFECTS_AUDIT.md, exclude that record from Phase 1's bridge dispatch list, capture in handoff.

3. **Extend `tools/mutagen-bridge/PatchEngine.cs`'s `ConvertJsonValue`.** Add a branch:
   - When `targetType` is `ExtendedList<T>` (or compatible collection) AND the JSON value is `JsonValueKind.Array` AND `T` is a LoquiObject (not a FormLink — distinguish via `IFormLinkGetter<>` interface check):
     - Iterate each JSON object in the array.
     - For each: `var entry = (T)Activator.CreateInstance(typeof(T))`.
     - For each property in the JSON object: route through `SetPropertyByPath(entry, propertyName, propertyValue)` — recursive use of the existing path machinery covers nested FormLinks, nested structs, and nested lists (Conditions on Effects).
     - Append entry to the constructed `ExtendedList<T>`.
   - Distinguish FormLink-list path (existing v2.5.6 fix) from LoquiObject-list path (new) by checking whether `T` has `IFormLinkGetter<>` ancestry. The two paths must coexist; FormLink lists must continue to work as before.

4. **Extend `SetPropertyByPath`** if needed for whole-list assignment via setter (the existing path may already handle it via `prop.SetValue(target, converted)` — verify in code). RefList<T>/ExtendedList<T> properties typically have setters; this distinguishes them from the no-setter dict properties Tier C handled.

5. **Build:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

6. **Inline smoke test** (Phase 1's "test our assumptions" step). Build a `bridge_request` with `set_fields: {Effects: [...]}` against a SPEL record from Skyrim.esm. Pipe to `mutagen-bridge.exe`. Read back the output ESP via Mutagen direct (not via bridge — independent verification). Confirm:
   - Effects array length matches request.
   - Effects[0].BaseEffect resolves to the requested MGEF.
   - Effects[0].Data.Magnitude/Area/Duration match request.
   - Effects[0].Conditions, if specified, contain the requested conditions.
   - The original Effects from the source record are gone (replace semantics confirmed).

   Repeat for ALCH.Effects (sister case) to confirm the mechanism is genuinely generic, not SPEL-specific.

7. **Update Python schema description** in `tools_patching.py` for `set_fields`: append a section noting that array-typed properties (`Effects` on SPEL/ALCH/ENCH/SCRL/INGR) accept JSON Array form for whole-list replacement, with per-entry sub-fields (BaseEffect, Data.Magnitude, Data.Area, Data.Duration, Conditions). Document replace-semantics explicitly.

8. **Update `KNOWN_ISSUES.md`:**
   - Remove the "Per-effect spell conditions" carry-over entry (now supported).
   - Add a positive entry under v2.8.0's section: "SPEL/ALCH/ENCH/SCRL/INGR Effects array writable via `set_fields: {Effects: [...]}`. Replace-semantics — whole-array assignment clears source and writes new entries."
   - Note any record types Phase 1 excluded (e.g. if ENCH's probe failed) as continuing limitations.

9. **Add CHANGELOG placeholder + Phase 1 entry:**
   ```markdown
   ## v2.8.0 — TBD

   <Phase 5 fills in date.>

   ### Added — bridge

   - **SPEL/ALCH/ENCH/SCRL/INGR Effects array write** via `set_fields: {"Effects": [...]}`. Each JSON object in the array constructs a fresh Mutagen `Effect` with sub-fields: `BaseEffect` (MGEF FormLink), `Data.Magnitude`, `Data.Area`, `Data.Duration`, and optional nested `Conditions`. Replace-semantics — whole-array assignment clears the source list and writes the new entries. The mechanism is generic: JSON Array → `ExtendedList<T>` for any LoquiObject `T` reachable through `set_fields`. Surfaced from a real consumer's custom-race rebuild patch hitting the gap during v2.7.1's first-day use; pre-existing carry-over candidate "per-effect spell conditions" is absorbed (per-effect Conditions ride the same recursion).

   <Subsequent phases append entries — e.g. Phase 4 PerkAdapter/QuestAdapter fix.>

   ---
   ```

10. **Bump version constants:**
    - `config.py`: `PLUGIN_VERSION = (2, 8, 0)`
    - `claude-mo2-installer.iss` line 21: `#define AppVersion "2.8.0"`
    - `README.md` lines 7 and 59: replace both `claude-mo2-setup-v2.7.1.exe` references with `v2.8.0`

11. **Add a coverage-smoke regression test** for SPEL.Effects and ALCH.Effects per the smoke step's contract. The test should be the same shape that Phase 2's Layer 1.E will run; Phase 1 lays down the test, Phase 2 runs it as part of the broader matrix.

12. **Force-add EFFECTS_AUDIT.md** (if produced).

13. **Write `PHASE_1_HANDOFF.md`** documenting:
    - Probe results per record type (which 5 of {SPEL, ALCH, ENCH, SCRL, INGR} passed, any exclusions).
    - Bridge ConvertJsonValue extension hunk.
    - Smoke test results.
    - Schema description diff.
    - CHANGELOG/KNOWN_ISSUES diffs.
    - Version bump landed.

14. **Commit:** Two commits (per v2.7.1 per-phase double-commit cadence):
    - Work commit: `[v2.8 P1] Effects-list write capability + version bump to 2.8.0`
    - Handoff hash-record commit: `[v2.8 P1] Handoff: record commit hash <work-hash>`
    Push both.

### Acceptance

- Probe runs to completion; API contract documented in `EFFECTS_AUDIT.md`.
- Bridge builds clean.
- Smoke test for SPEL.Effects + ALCH.Effects passes via Mutagen-direct readback.
- Coverage-smoke regression test added.
- Version bumped to 2.8.0 in all four version-bearing files.
- Schema description, CHANGELOG, KNOWN_ISSUES updated.
- Handoff hash-record commit anchors the work commit.

---

## Phase 2 — Build harness + execute Layers 1, 2, 4

**Goal:** Extend `tools/coverage-smoke/Program.cs` to execute every assertion in MATRIX.md's Layers 1 (including the new 1.E Effects cells from Phase 1), 2, and 4 against vanilla Skyrim.esm. Run it. Capture pass/fail per row. Document any failures as bugs for Phase 4.

**Files to touch:**
- `Claude_MO2/tools/coverage-smoke/Program.cs` (extend test rows)
- `Claude_MO2/tools/race-probe/Program.cs` (extend with PerkAdapter/QuestAdapter readback probe)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_2_HANDOFF.md`

### Steps

1. **Read MATRIX.md.** Treat it as the authoritative test spec.

2. **Extend `coverage-smoke/Program.cs`.** For each Layer 1 / 2 / 4 row not already covered by Phase 1's regression tests:
   - Add a test block following the existing pattern: build a `bridge_request`, pipe to `mutagen-bridge.exe`, parse the response, assert `success` + mods key + (where applicable) read back the output ESP via Mutagen and assert the mutation landed.
   - For Layer 1.D negatives: assert `success: false` AND `unmatched_operators` contains the expected operator AND no output ESP is written for the rolled-back record.
   - For Layer 1.E (Effects-list): exercises Phase 1's new path. Test each of {SPEL, ALCH, ENCH, SCRL, INGR}.Effects positive case + nested Conditions.
   - For Layer 2.combinatorial: build patches with multiple records and/or multiple operators per record. Assert per-record `success` flags and rollback isolation. Include an Effects+keywords combo on a SPEL record.
   - For Layer 4.malformed: feed the bridge intentionally-bad path syntax. Assert clean error.
   - For Layer 4.idempotency: run repeats and document actual behavior.

3. **Build PerkAdapter/QuestAdapter probe in `race-probe/Program.cs`.** Bypass the bridge and use Mutagen directly:
   - Construct a fresh PERK with no `VirtualMachineAdapter`.
   - Call the bridge to attach a script.
   - Read the output ESP back into a fresh `SkyrimMod` via Mutagen.
   - Inspect `output.Perks[0].VirtualMachineAdapter` — assert it's a `PerkAdapter` instance.
   - Repeat for QUST → `QuestAdapter`.
   - Document the failure mode if it reproduces.

4. **Build:** `cd tools/coverage-smoke && dotnet build -c Release`. Fix any compile errors. **Don't commit yet.**

5. **Run:** `dotnet run -c Release --project tools/coverage-smoke`. Capture full output to `<workspace>/scratch/v2.8-layer-1-2-4-results.txt`.

6. **Run the PerkAdapter/QuestAdapter probe** via `dotnet run -c Release --project tools/race-probe`. Capture output.

7. **Triage failures.** For each FAIL row:
   - If real bug: add to `PHASE_2_HANDOFF.md`'s "Bugs surfaced" section.
   - If harness bug: fix harness, re-run, don't log.

8. **Write `PHASE_2_HANDOFF.md`** documenting:
   - Total assertion count run, total pass, total fail.
   - Per-failure bug entry (slug, repro, failure mode, proposed fix angle).
   - Per-layer summary.
   - Any harness deviations from MATRIX.md.

9. **Commit:** `[v2.8 P2] Verification harness + Layers 1+2+4 results — N bugs surfaced`. Push.

### Acceptance

- `coverage-smoke/Program.cs` runs and exits cleanly. Failures show as `FAIL` rows, not crashes.
- Every MATRIX.md row in Layers 1, 2, 4 has a corresponding test or a documented skip-with-reason.
- PerkAdapter/QuestAdapter probe runs to completion. Output documents the runtime adapter type.
- Bug list captured for Phase 4.

---

## Phase 3 — Execute Layer 3 workflow scenarios

**Goal:** Run the 5 realistic patching scenarios against the live Authoria modlist via `mo2_create_patch`. Verify each scenario's assertions via `mo2_record_detail` readback. Capture surfaced bugs.

**Files to touch:**
- `<modlist>/mods/Claude Output/v2.8-scenario-*.esp` (test patches; created + deleted within the phase)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_3_HANDOFF.md`

### Steps

1. **Verify live install + MCP server.** `mo2_ping` returns the latest version. If disconnected, restart MO2's Claude server before proceeding.

2. **Verify Phase 1's Effects-list capability landed in the live install.** A pre-flight check: build a single `mo2_create_patch` call exercising `set_fields: {Effects: [...]}` on one SPEL. If it fails, the live bridge is stale — sync from `build-output/mutagen-bridge/` first.

3. **For each Layer 3 scenario in MATRIX.md:**
   - Confirm the target records still exist at the expected FormIDs. Swap if needed; document.
   - Build the `mo2_create_patch` call. Output filename: `v2.8-scenario-<N>.esp`.
   - Capture response. Per-record `mods` keys must match expected.
   - Run `mo2_record_detail` against each modified record. Compare to expected.
   - **Delete the test patch** before the next scenario: Bash `rm` + ask user to F5 in MO2.
   - Capture per-scenario result table in handoff.
   - **Scenario 3.1 (Reqtify a custom race + ability spells)** is the most sensitive — it directly exercises the v2.7.1 → v2.8 transition. If this scenario doesn't fully pass, that's a Phase 1 implementation regression worth re-investigating before Phase 4.

4. **Cross-scenario rollup.** Summarise pass/fail counts; group failures by suspected root cause if a pattern emerges.

5. **Triage failures.** For each FAIL: bug entry as in Phase 2.

6. **Write `PHASE_3_HANDOFF.md`** documenting:
   - Per-scenario assertion table.
   - Bug list (extending Phase 2's).
   - Confirmation that test patches were deleted.

7. **Commit:** `[v2.8 P3] Layer 3 workflow scenarios — N bugs surfaced`. Push.

### Acceptance

- All 5 scenarios executed.
- Each assertion documented as pass/fail.
- Test patches deleted; modlist clean.
- Bug list extended with workflow-scenario finds.

---

## Phase 4 — Per-bug fix sessions

**Goal:** Fix each bug surfaced in Phase 2 + Phase 3. **One bug per session.** Each session produces one work commit (or a small number of related commits) and one `PHASE_4_<bug-slug>_HANDOFF.md`.

The PerkAdapter/QuestAdapter bug is the priority-zero entry — known concrete fix-it; Phase 2's probe confirms.

**Files to touch (per session):**
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (or whichever bridge file the bug lives in)
- `Claude_MO2/tools/coverage-smoke/Program.cs` (add regression test)
- `Claude_MO2/mo2_mcp/tools_patching.py` (only if the bug is a Python-side issue)
- `Claude_MO2/mo2_mcp/CHANGELOG.md` (append fix bullet under v2.8.0 entry)
- `Claude_MO2/KNOWN_ISSUES.md` (if the fix changes documented behavior)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_4_<slug>_HANDOFF.md`

**No version bump in Phase 4** — Phase 1 already bumped to v2.8.0.

### Steps (per Phase 4 session)

1. **Identify your bug.** Read `PHASE_3_HANDOFF.md` (or `PHASE_2_HANDOFF.md` if Phase 3 didn't surface bugs). Pick the next un-fixed entry.

2. **Read the bug's repro from the relevant handoff.**

3. **Probe-first if necessary.** Extend `coverage-smoke` or `race-probe` to demonstrate the failure mode. Run it. Capture failing output.

4. **Root-cause.** Read the bridge code at file:line. Articulate root cause in handoff (one paragraph, link to file:line).

5. **Fix.** Minimal change addressing root cause. Bonus-catch if the touched file surfaces a related latent issue. No drive-by cleanup.

6. **Add a regression test in `coverage-smoke`.** Same probe shape that demonstrated the bug, now passing.

7. **Build the bridge:** `dotnet build -c Release`. Zero warnings, zero errors.

8. **Run `coverage-smoke` end-to-end.** Confirm new test passes AND every previously-passing test still passes.

9. **Update CHANGELOG** (append fix bullet under existing `## v2.8.0 — TBD` entry).

10. **Update KNOWN_ISSUES.md** if the fix changes a documented limitation (e.g. removes the PerkAdapter/QuestAdapter entry).

11. **Write `PHASE_4_<slug>_HANDOFF.md`** documenting bug repro, root cause, fix, regression test, end-to-end smoke result.

12. **Commit:** `[v2.8 P4 <slug>] <one-line fix description>`. Push.

### Acceptance (per Phase 4 session)

- Targeted bug's regression test passes.
- All previously-passing tests still pass.
- Bridge builds clean.
- Handoff captures root cause + fix + regression evidence.
- CHANGELOG entry lists the fix.

---

## Phase 5 — Re-run + ship v2.8.0

**Goal:** Final verification pass + ship the v2.8.0 release. Phase 1 guaranteed code changes; this is always a real release.

**Files to touch:**
- `Claude_MO2/build-output/installer/claude-mo2-setup-v2.8.0.exe` (built artifact)
- `Claude_MO2/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `Claude_MO2/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_5_HANDOFF.md`

### Steps

1. **Verify session start.** `origin/main` at the latest Phase 4 commit (or Phase 3 if no Phase 4 needed) and clean. Live install at `<live>` running v2.7.1 (will be bumped at Phase 5 sync). All `PHASE_4_*_HANDOFF.md` files have `Status: Complete`.

2. **Final coverage-smoke run** against the latest bridge build. Confirm 100% pass.

3. **Final live workflow re-run.** Re-execute any Layer 3 scenario that surfaced bugs in Phase 3, against the latest bridge. Confirm previously-failing assertions now pass. Test patches deleted.

4. **Build production bridge:**
   ```bash
   cd tools/mutagen-bridge
   dotnet publish -c Release -r win-x64 --self-contained false -o ../../build-output/mutagen-bridge/
   ```
   Capture SHA256 of `build-output/mutagen-bridge/mutagen-bridge.exe`.

5. **Build installer.** Direct ISCC invocation (preserves bridge SHA bit-for-bit, mirroring v2.7.1 Phase 5):
   ```bash
   "C:\Utilities\Inno Setup 6\ISCC.exe" installer/claude-mo2-installer.iss
   ```
   Capture SHA256 of `build-output/installer/claude-mo2-setup-v2.8.0.exe`.

6. **Live sync.** Copy `build-output/mutagen-bridge/*` to `<live>/tools/mutagen-bridge/`. Copy any changed Python files to `<live>/`. Remove `<live>/__pycache__/`. Restart MO2's MCP server. `mo2_ping` returns `version: "2.8.0"`.

7. **Live sanity check.** Pick 2–3 representative scenarios (one Tier D negative, one Tier C bracket, one Effects-list write). Run via `mo2_create_patch` against the live install. Verify with `mo2_record_detail`. Delete test patches.

8. **Insert ship date in CHANGELOG.** Replace `## v2.8.0 — TBD` with `## v2.8.0 — 2026-MM-DD`.

9. **Tag + release:**
   ```bash
   git tag v2.8.0
   git push origin v2.8.0
   gh release create v2.8.0 \
     build-output/installer/claude-mo2-setup-v2.8.0.exe \
     --title "v2.8.0 — Verification + Effects-list writability" \
     --notes-file <release-notes>
   ```

10. **Update memory** (`project_capability_roadmap.md`): title becomes "v2.8.0 shipped — verification + Effects-list writability", body documents the Effects-list capability + verification matrix results + Phase 4 fixes.

11. **Write `PHASE_5_HANDOFF.md`** documenting final coverage-smoke results, live workflow re-run results, installer + bridge SHA256s, GitHub release URL, live install confirmation, memory updated.

12. **Final commit:** `[v2.8 P5] Ship v2.8.0`. Push.

### Acceptance

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.8.0` resolves with installer attached.
- `<live>/` running v2.8.0 (`mo2_ping`).
- Memory reflects v2.8.0 shipped.
- SHAs captured.
- Bridge SHA matches across smoke matrix, installer bundle, and live install (single audit anchor).

---

## ⚠️ Carry-overs (NOT addressed in v2.8.0; future-release candidates)

These are explicitly out of scope for v2.8.0 unless real-world testing surfaces them as actually-blocking. If Phase 2/3 surface them as bugs, they get promoted to Phase 4 fix scope.

1. **Quest condition disambiguation** (`DialogConditions` / `EventConditions`). v2.7.1 surfaces these as clean Tier D errors. v2.9 if a real consumer surfaces.
2. ~~**Per-effect spell conditions** (`Spell.Effects[i].Conditions`).~~ **ABSORBED into Phase 1's Effects-list mechanism.**
3. **AMMO enchantment.** Mutagen schema absence; requires upstream change.
4. **Replace-semantics whole-dict assignment** (Tier C dicts). Today's whole-dict is uniform merge; clearing keys requires future operator parameter or sentinel. Note that array-replace (Phase 1) is a separate semantics — Phase 1 ships array-replace by default, but Tier C dict-merge is unchanged.
5. **Chained dict access** (`Foo[Key].Sub`). Tier C terminal-bracket only; rejected with explicit error.
6. **MCP tool surface for `tool_paths.json`.** Carried over from v2.7.0.
7. **Plugin-setting unification into `tool_paths.json`.** Carried over from v2.7.0.
8. **Inno static-AppId registry hygiene.** Carried over from v2.7.0.
9. **Back-nav re-detection** (installer UX quirk from v2.7.0).
10. **QUST.Aliases / Stages / Objectives, PERK.Effects** — out of scope for v2.8.0's Effects-list mechanism even though the schema shape is similar (sub-class polymorphism makes them harder; no real consumer surfaced yet).
11. **All v2.6.0 deferrals** — see `Claude_MO2/dev/plans/v2.6.0_mutagen_migration/PHASE_6_HANDOFF.md`.

The PerkAdapter/QuestAdapter bug is the only carry-over candidate that v2.8.0 promotes into scope by default — it's a known concrete bug, not speculative.
