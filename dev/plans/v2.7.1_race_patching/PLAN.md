# v2.7.1 — Bridge Coverage Expansion + Silent-Failure Detection

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-25, after a user-reported RACE-patching limitation surfaced from a v2.7.0 session.
**Baseline:** v2.7.0 (shipped 2026-04-24).
**Target version:** v2.7.1
**Sessions estimated:** 6 (one per phase).
**Re-scoped 2026-04-25** from a 5-phase RACE-only hotfix to a comprehensive bridge-coverage pass after the strategic call: shipping a RACE-only fix while leaving the same silent-failure bug class active for Container, Door, Light, Activator, Location, Spell, MagicEffect (and any future record/operator combo) is incoherent. v2.7.1 now ships:

1. **Tier D — Silent-failure detection.** Generic safety net: any requested operator with no matching handler returns an explicit error instead of silently dropping. Closes the bug class.
2. **Tier C — Bracket-indexer dict mutation.** Generic mechanism for writing to `IDictionary<K,V>` properties without setters (RACE.Starting/Regen, RACE.BipedObjectNames, plus any other Mutagen dict-shaped field).
3. **Tier A — Comprehensive operator wire-ups.** Every operator/record-type combo where Mutagen supports the API but the bridge doesn't dispatch yet. Driven by a Phase 0 audit.
4. **Tier B — Cosmetic stat aliases for RACE.** Polish on top of Tier C.

**v2.8 is the verification/hardening release** — no new capabilities, real-world exercise of every wire-up landed in v2.7.1, fixing whatever surfaces.

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |
| `<live>` | `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` |

When generating bash commands, always wrap these paths in quotes — both contain spaces (`Stuff for Calude`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. **Before touching anything**, do this in order:

1. **Identify your phase.** Look in this directory:
   ```
   Claude_MO2/dev/plans/v2.7.1_race_patching/
   ```
   Find the highest-numbered file matching `PHASE_*_HANDOFF.md`. **Your phase is one greater than that.** If no handoffs exist yet, you are **Phase 0**. If `PHASE_5_HANDOFF.md` exists, **the release is shipped** — point the user at it and stop.

2. **Read the previous handoff** (if any) in full. Trust the handoff over this plan when they conflict — the plan is original intent; the handoff is actual state.

3. **Read your phase section in this file** below. It tells you the goal, files to touch, steps, and what to write in your own handoff.

4. **Read `AUDIT.md`** in this directory if it exists (Phase 0 produces it). Phases 1–5 use it as the authoritative wire-up scope.

5. **Standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry
   - `Claude_MO2/KNOWN_ISSUES.md`
   - **Skip** the session-summaries / handoffs sweep — this plan is your roadmap.
   - Check `<workspace>/Live Reported Bugs/` root for anything new. Should be empty unless a fresh report landed.

6. **Confirm with the user** which phase you've identified yourself as and any deviations you've noticed. Wait for go-ahead before making changes.

7. **At the end of your phase**, write `PHASE_N_HANDOFF.md` in this directory using the template at the bottom of this file.

**One phase per session.** If you finish early, summarise and stop — don't roll into the next phase.

---

## 📋 Background — why this plan exists

A user running v2.7.0 attempted a 47-race combat-feel + Reqtified-style stat pass via `mo2_create_patch`. The patch built without error, but post-build inspection showed:

- **Keywords were not added.** Creature-type keywords (ActorTypeNPC etc.) and immunity keywords (ImmuneParalysis etc.) requested via `add_keywords` did not land in the output ESP.
- **Actor effects were not added.** Resistance abilities and racial-armor abilities (the SPLO list, e.g. ResistFrostRace) requested via `add_spells` did not land in the output ESP.
- **Stat fields could not be set.** `RACE.Starting` and `RACE.Regen` (per-stat starting values and regen rates) rejected `set_fields` writes.

The user's session reported "patch built clean" on the silent failures, masking the missing data.

### Investigation findings (2026-04-25 architect session)

Confirmed in `tools/mutagen-bridge/PatchEngine.cs`:

1. **Keywords on RACE silently no-op.** `GetKeywordsList()` ([PatchEngine.cs:1242](../../../tools/mutagen-bridge/PatchEngine.cs)) is a type switch covering ARMO/WEAP/NPC/ALCH/AMMO/BOOK/FLOR/INGR/MISC/SCRL — **no Race case**. Returns null, the keyword block at [PatchEngine.cs:407](../../../tools/mutagen-bridge/PatchEngine.cs) skips. **No error returned.** The same gap exists for every record type Mutagen exposes Keywords on but isn't in the switch — Container, Door, Furniture, Light, Activator, Location, Spell, MagicEffect (audit will produce the exact list).

2. **Spells on RACE silently no-op.** `add_spells`/`remove_spells` logic at [PatchEngine.cs:419](../../../tools/mutagen-bridge/PatchEngine.cs) is gated by `if (record is Npc npc)`. RACE has the same `ActorEffect` property, but the bridge never reaches it.

3. **`Starting` / `Regen` cannot be set today.** Both are `IDictionary<BasicStat, float>` with **no public setter** (verified by C# probe at `tools/race-probe/`). The bridge's `SetPropertyByPath` ([PatchEngine.cs:745](../../../tools/mutagen-bridge/PatchEngine.cs)) only knows `prop.SetValue(target, converted)`. With no setter to invoke, any attempt fails. `ConvertJsonValue` also has no path for `JsonValueKind.Object` against dict-typed properties.

4. **Silent failure as a class.** Items 1–3 share a root cause: when an operator is requested but the bridge has no handler for the (operator, record-type) pair, the bridge returns success with empty `mods`. There is no general mechanism for "modifications were requested but no handler matched." Tier D fixes this generically.

The override copy itself works (RACE is in the dispatch at [PatchEngine.cs:1279](../../../tools/mutagen-bridge/PatchEngine.cs)), and `RecordReader.cs` already returns RACE record details on reads. The read side is fine; the write side has both specific gaps (operator/type combos missing) and a generic gap (no detection of those combos).

### Probe artifact

`tools/race-probe/` is a standalone .NET 8 console that constructs a Race in-memory, mutates Keywords/ActorEffect/Starting/Regen/UnarmedDamage, writes to a 561-byte ESP, and reads back. It confirmed:

- `race.Keywords` and `race.ActorEffect` are `Noggog.ExtendedList<IFormLinkGetter<...>>` — identical type signature to NPC's. The existing NPC `??= new ExtendedList<...>(); list.Add(fk)` idiom transfers verbatim.
- `<Data>` block in the Loqui XML is **flattened** onto Race. So `Starting`/`Regen`/`UnarmedDamage`/`UnarmedReach`/`BaseMass`/etc. are direct properties of Race — no `Data.` prefix needed. Plain-float fields already work via the existing `set_fields` reflection path (no code change needed for them).
- `Starting` and `Regen` are concrete `Dictionary<BasicStat, float>` (3 enum keys: Health/Magicka/Stamina). **No public setter — only mutable via the indexer.** Round-trip through `WriteToBinary`/`CreateFromBinary` preserves values exactly.
- `BipedObjectNames` is also a no-setter dict on RACE — same fix shape; freebie when Tier C lands.

**Phase 0 extends this probe** to cover every record type targeted for wire-up (Container, Door, Furniture, Light, Activator, Location, Spell, MagicEffect for keywords; LeveledNpc and LeveledSpell for `add_items`; etc.). The extended probe verifies API contract per record type before Phase 3 commits to dispatch code. Keep the probe in the repo as a regression check before any future Mutagen version bump.

---

## 🏗️ Architecture — four tiers + meta-tier (locked)

| Tier | Scope | Bridge changes | Python schema changes | Order |
|---|---|---|---|---|
| **D** | Silent-failure detection | Wrap `ApplyModifications` with operator-coverage tracking: capture the list of operators specified by the request before processing; after processing, every requested operator must have a corresponding `mods` entry (entry value of 0 is valid — "0 added" is a real outcome; missing entry means no handler matched). Any unmatched requested operator → roll back the override and return an explicit error naming the (record-type, operator) pair. Closes the silent-no-op bug class for any current or future operator/record combo. | None — error surfaces via the existing JSON response error field. | **First** — safety net for every subsequent wire-up. |
| **C** | Bracket-indexer path syntax + dict mutation | `SetPropertyByPath`: recognize `Property[Key]` at the final segment and invoke the dict's indexer with the parsed key. `ConvertJsonValue`: handle `JsonValueKind.Object` against `IDictionary<TKey, TValue>` properties (merge semantics — only specified keys touched, existing keys untouched). Whole-dict assignment to setter-less dict properties uses indexer-per-entry. | None at the schema level — `set_fields` already accepts arbitrary `object`; new path syntax is documented in the description. | **Second** — generic, independent of A. |
| **A** | Comprehensive operator wire-ups (RACE + every other Mutagen-supported gap) | Per the Phase 0 audit: extend `GetKeywordsList` switch with every missing record type; extend the `if (record is Npc npc)` ActorEffect block with parallel blocks per audit-identified type; same pattern for any other operator surface with gaps (LVLN/LVSP entries, AMMO enchantment, attach_scripts, conditions). | Update `add_keywords`/`remove_keywords` description to drop "(NPC)" framing and list all supported types from the audit. Update `add_spells` similarly. Other operators per audit. | **Third** — the bulk of the wire-up work; validated by D and benefits from C. |
| **B** | Cosmetic stat-name aliases | `FieldAliases`: add `["RACE"]` block with `BaseHealth` → `Starting[Health]`, `BaseMagicka` → `Starting[Magicka]`, `BaseStamina` → `Starting[Stamina]`, `HealthRegen` → `Regen[Health]`, `MagickaRegen` → `Regen[Magicka]`, `StaminaRegen` → `Regen[Stamina]`. Plain floats (UnarmedDamage etc.) need no aliases — they already work via canonical name. | None. | **Alongside A** — small cosmetic add. |
| *Meta* | Documentation + assumption-testing as we go | Each phase ships its own CHANGELOG fragment, KNOWN_ISSUES update, and inline smoke test against a real ESP build. Phase 5's pre-ship matrix integrates them. | n/a | Continuous. |

### Path-syntax design (Tier C)

- **Final segment only.** `Foo.Bar[Key]` is supported; `Foo[Key].Sub` is not (Tier C scope locked at terminal-dict access — no chained dict access, no nested mutation through dicts). If a future case needs deeper traversal, defer to v2.8.
- **Key parsing rules:**
  - Dict's key type is an enum → `Enum.Parse(keyType, keyString, ignoreCase: true)`.
  - Dict's key type is a primitive (int/string/etc.) → use the existing `ConvertJsonValue` primitive branches.
  - Dict's key type is anything else → throw `ArgumentException("Unsupported dict key type")`.
- **Value conversion:** dispatch through the existing `ConvertJsonValue` against the dict's `TValue` generic argument. No special cases.
- **Mutation:** invoke the dict's `set_Item` indexer (PropertyInfo with non-empty `GetIndexParameters()`) via reflection. Standard `Dictionary<K,V>` indexer is add-or-update — partial writes don't disturb other keys.
- **Whole-dict JSON object form** (`set_fields: { "Starting": { "Health": 100, "Magicka": 200 } }`):
  - Detect `value.ValueKind == JsonValueKind.Object` AND target property type implements `IDictionary<,>`.
  - Iterate JSON object members, parse each key against `TKey`, convert each value against `TValue`, call indexer per entry.
  - **Merge semantics, not replace.** Only the keys named in the JSON object are touched.

### Operator-coverage detection design (Tier D)

The detection has to distinguish three states for each operator named in the incoming request:

1. **Handler matched, ≥1 entries applied.** `mods["spells_added"] = 5`. Success.
2. **Handler matched, 0 entries applied.** `mods["spells_added"] = 0`. Success — the user asked to add zero items, or all items were already present and dedup'd. NOT an error.
3. **No handler matched.** No corresponding `mods` key exists at all. **Error** — the request is unsupported.

Implementation pattern:
- At entry to `ApplyModifications`, build a `HashSet<string> requestedOps` from the operation object — every operator field with a non-null/non-empty value contributes its canonical mods-key (e.g. `op.AddKeywords?.Count > 0` → "keywords_added").
- After all handlers run, compute `unmatched = requestedOps.Except(mods.Keys)`.
- If `unmatched` is non-empty: roll back the override via `TryRemoveOverride(...)` (existing helper), return an error response listing each unmatched (record-type, operator) pair.

The "canonical mods-key" mapping is the only contract that connects requested operators to handler outputs. Phase 1 codifies it as a single private dict in PatchEngine.cs.

### Scope locks (revised 2026-04-25)

- **v2.7.1 = comprehensive coverage expansion.** Every Mutagen-supported (operator, record-type) pair the audit identifies as missing gets wired. No NEW operators, no NEW `op:` values. Just connect what Mutagen already exposes.
- **v2.8 = verification/hardening.** Real-world usage exercises everything landed here. Bugs surfaced get fixed. No new capabilities. Memory's roadmap entry will reflect this on v2.7.1 ship.
- **All wire-ups must be probe-verified before committing.** The race-probe extends in Phase 0 to cover every record type targeted by the audit. Phase 3 wire-ups are transcriptions of probe-verified API contracts, not speculative coding.
- **Inline smoke testing per phase.** Each phase that lands code ends with a smoke run proving the change works end-to-end. No phase ships green without its own validation.
- **Inline documentation per phase.** CHANGELOG fragments, KNOWN_ISSUES updates, and Python schema description updates land in the phase that introduces the change. Phase 4 is the final roll-up, not the only place docs live.
- **No `Data.X` path support.** The Loqui flattening means `Data.Starting` would route to a non-existent `Data` property and throw. Schema description must clarify users should use `Starting`, not `Data.Starting`.
- **Probe stays in the repo.** `tools/race-probe/` is committed as a regression check. Phase 0 extends its scope.
- **Version: v2.7.1.** Bumped in Phase 0 (per locked-version-rule: rebuild requires bump).

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Audit + scope lock + version bump | `AUDIT.md` (operator/record-type matrix); probe extended to cover audit-identified types; `config.py` + `.iss` + `README.md` bumped to 2.7.1; `CHANGELOG.md` placeholder; KNOWN_ISSUES placeholder | None |
| 1 | Tier D — silent-failure detection | `PatchEngine.cs`: operator-coverage tracking around `ApplyModifications`; rollback + explicit error on any requested operator with no handler; inline smoke test confirming the error fires for a deliberate unsupported request | Phase 0 (especially the canonical operator/mods-key mapping established in AUDIT.md) |
| 2 | Tier C — bracket-indexer dict mutation | `PatchEngine.cs`: bracket-indexer path syntax; JSON Object → IDictionary; whole-dict via indexer when no setter; inline smoke test against RACE.Starting and RACE.Regen | Phase 1 |
| 3 | Tier A — comprehensive wire-ups | `PatchEngine.cs`: every audit-identified gap closed (keyword switch additions, ActorEffect block parallels, any other operator surface gaps); inline smoke test exercises one example per newly-wired (operator, record-type) pair | Phases 1+2 (D validates gaps; C unlocks dict writes during smoke) |
| 4 | Tier B aliases + Python schema + final docs roll-up | `PatchEngine.cs`: `["RACE"]` aliases in `FieldAliases`; `tools_patching.py`: schema description updates per audit; `CHANGELOG.md` finalized with full v2.7.1 entry; `KNOWN_ISSUES.md` reflects expanded write surface; bridge rebuilt as a production artifact | Phase 3 |
| 5 | Ship v2.7.1 | Pre-ship comprehensive smoke matrix against live modlist data (all wire-ups exercised); installer rebuilt; SHA256 captured; live install synced; tag pushed; `gh release create`; memory updated | Phase 4 |

**Live state at plan creation (2026-04-25):**
- v2.7.0 public on GitHub at `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.7.0`. `origin/main` at `e77afcd`, clean working tree, live install synced and running.
- One Live Reported Bug surfaced (the limitation that prompted this plan).
- Active workstream memory (`project_capability_roadmap.md`) points at this plan.

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one commit (or a small handful of related commits). Commit messages start with `[v2.7.1 PN]` (e.g. `[v2.7.1 P1] Tier D — silent-failure detection`).
- **Plan + handoff artifacts force-added to git.** `dev/` is gitignored; each phase commits its `PHASE_N_HANDOFF.md` (and `PLAN.md` + `AUDIT.md` at P0) via `git add -f`. Once tracked, `git add -f` is not needed for subsequent edits.
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer or bridge has been built, that version is locked. Phase 0 bumps to v2.7.1 up front. **Do not rebuild v2.7.0's installer or bridge.** Both are public release assets; rebuilding either locally would overwrite a released binary.
- **Live install sync:** Phases 1–4 do not touch the live install. Phase 4 sandbox-tests on the live modlist data via a fresh test-output directory (read-only against the modlist, write-only into a throwaway). Phase 5 live-syncs once and ships.
- **Probe-first discipline:** any wire-up that hasn't been verified by the probe DOES NOT GET COMMITTED in Phase 3. If Phase 3 surfaces an API surface the Phase 0 probe missed, the implementer extends the probe first, runs it, then transcribes the result.
- **Inline smoke tests per phase:** each phase that lands code ends with a smoke run that exercises the change against a real ESP build. Failure means the phase is not complete; the handoff is `Status: Partial`.
- **Inline documentation per phase:** CHANGELOG fragments, KNOWN_ISSUES updates, and Python schema description deltas land in the phase that introduces the change. Phase 4 is the final roll-up, not the only place docs live.
- **No partial phases.** If a phase can't complete, the handoff records partial state and lists what blocks the next phase.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive. If you find yourself wanting to modify something outside that list, stop and escalate.
- **Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves** you spot during work.
- **No changes to MCP tool request/response shapes.** Internal-only changes — `mo2_create_patch`'s public schema gains documentation precision and an explicit error path for unsupported requests, but no breaking changes to the request shape.

---

## 🔁 Handoff template

Every phase ends by writing `PHASE_N_HANDOFF.md` in this directory. Use this exact structure:

```markdown
# Phase N Handoff — <one-line summary>

**Phase:** N
**Status:** Complete | Partial | Blocked
**Date:** YYYY-MM-DD
**Session length:** ~Xh
**Commits made:** <hashes or "none">
**Live install synced:** Yes/No (path: ...)

## What was done
<Bulleted list of concrete changes — file paths + one-line descriptions.>

## Verification performed
<What tests / smoke checks ran. What evidence shows it worked. For Phase 3 specifically: list each (operator, record-type) pair newly-wired and the smoke result for each.>

## Deviations from plan
<Anything you did differently from PLAN.md. Why. If you didn't deviate, write "None.">

## Known issues / open questions
<Bugs you found but didn't fix (with reason). Questions the next phase needs to answer. If none, write "None.">

## Preconditions for Phase (N+1)
<Confirm each precondition the next phase requires. Flag any not met.>

## Files of interest for next phase
<List paths the next phase will most need to read.>
```

Keep handoffs short — under 400 lines.

---

# PHASES

---

## Phase 0 — Audit + scope lock + version bump

**Goal:** Produce the authoritative wire-up scope as `AUDIT.md`. Extend the probe to verify every record type the audit names. Bump version constants. **No production code logic changes.**

**Files to touch:**
- `Claude_MO2/dev/plans/v2.7.1_race_patching/AUDIT.md` (NEW — the operator/record-type matrix)
- `Claude_MO2/tools/race-probe/Program.cs` (extend coverage — adds a verification block per audit-identified record type)
- `Claude_MO2/tools/race-probe/race-probe.csproj` (no change expected; flagged in case a Mutagen API requires an additional package reference)
- `Claude_MO2/mo2_mcp/config.py` — `PLUGIN_VERSION` tuple
- `Claude_MO2/installer/claude-mo2-installer.iss` — `#define AppVersion`
- `Claude_MO2/README.md` — installer download URL + reference at lines 7 and 59
- `Claude_MO2/mo2_mcp/CHANGELOG.md` — new top entry placeholder
- `Claude_MO2/KNOWN_ISSUES.md` — RACE limitations get a "fixed in v2.7.1" follow-up note finalized in Phase 4
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PLAN.md` — force-add this file
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_0_HANDOFF.md` — write at end

### Steps

1. **Verify session start.** Confirm `origin/main` is at `e77afcd` (or later) and clean. Live install at `<live>` is on v2.7.0 (`mo2_ping` returns the v2.7.0 banner; if the MO2 MCP server is disconnected, restart it from MO2's Tools menu).

2. **Build `AUDIT.md`** — the operator-by-record-type matrix. For each operator currently in PatchEngine.cs (`add_keywords`, `add_spells`, `add_perks`, `add_packages`, `add_factions`, `add_inventory`, `add_items` (LVLI), `add_outfit_items`, `add_form_list_entries`, `add_conditions`, `attach_scripts`, `set_enchantment`/`clear_enchantment`, plus `set_fields` and `set_flags` which are generic), enumerate:
   - Which record types Mutagen exposes the underlying API for (e.g. for `add_keywords`: which record types have a `Keywords` property typed `Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>`).
   - Which of those the bridge currently dispatches to (the existing switch coverage).
   - The delta — record types Mutagen supports but the bridge doesn't dispatch.
   - **For each delta entry: classify "wire up in Phase 3" or "intentionally out of scope" with rationale.**

   Methodology:
   - Read PatchEngine.cs `GetKeywordsList`, `ApplyModifications`, `ApplyAddConditions`, `ApplyAttachScripts`, `ApplySetFields`, the enchantment block, and the leveled-list `add_items` block.
   - Cross-reference against Mutagen's Race.xml schema (and other major records' schemas) at `https://github.com/Mutagen-Modding/Mutagen/tree/dev/Mutagen.Bethesda.Skyrim/Records/Major%20Records` — fetch what's needed via `WebFetch` (use the `dev` branch; Mutagen 0.53.1 is recent so the dev schemas should match closely). Specifically check at minimum: Container.xml, Door.xml, Furniture.xml, Light.xml, Activator.xml, Location.xml, Spell.xml, MagicEffect.xml (keyword carriers); LeveledNpc.xml, LeveledSpell.xml (entry-list parallels); Ammunition.xml (enchantment).
   - If any uncertainty surfaces (does record X really expose property Y as the type the bridge expects?), DEFER classification of that row to "needs probe verification in step 3" rather than guessing.

   AUDIT.md format:
   ```markdown
   # v2.7.1 Bridge Coverage Audit

   ## Operator: add_keywords / remove_keywords

   ### Currently dispatched
   - Armor, Weapon, Npc, Ingestible, Ammunition, Book, Flora, Ingredient, MiscItem, Scroll

   ### Mutagen-supported but not dispatched (ACTION: wire up in P3)
   | Record | Property | Type | Probe-verified? |
   |---|---|---|---|
   | Race | Keywords | ExtendedList<IFormLinkGetter<IKeywordGetter>> | Yes (race-probe) |
   | Container | Keywords | <type from probe or schema> | <pending> |
   | ... | | | |

   ### Out of scope rationale
   <Any record types Mutagen exposes Keywords on but we deliberately skip — with reason.>

   ---

   ## Operator: add_spells / remove_spells

   ### Currently dispatched
   - Npc

   ### Mutagen-supported but not dispatched (ACTION: wire up in P3)
   | Record | Property | Type | Probe-verified? |
   |---|---|---|---|
   | Race | ActorEffect | ExtendedList<IFormLinkGetter<ISpellRecordGetter>> | Yes (race-probe) |

   <Confirmed via probe: nothing else has ActorEffect — Spell.Effects is a different shape, not in scope.>

   ...
   ```

   Continue for every operator. Keep the audit file concise — one section per operator, terse rationale.

3. **Extend `tools/race-probe/Program.cs`.** For every "wire up in P3" row in AUDIT.md, add a verification block to the probe that:
   - Constructs the record type in-memory.
   - Confirms the property exists with the expected name and type (via reflection — same `DumpProperty` helper the probe already uses).
   - Mutates it (Add to a list, indexer-write to a dict, etc.) and confirms the count.
   - Round-trips through `WriteToBinary` / `CreateFromBinary` and confirms read-back.

   If a probe block fails for any record type: AUDIT.md gets that row reclassified to "out of scope — Mutagen API differs from expected" with the failure mode documented. Phase 3 honors AUDIT.md, so failed probes self-prune the wire-up list.

   Re-run the probe at the end. Confirm zero failures. Capture the probe's output in `PHASE_0_HANDOFF.md`.

4. **Bump version constants:**
   - `config.py`: `PLUGIN_VERSION = (2, 7, 1)`
   - `claude-mo2-installer.iss` line 21: `#define AppVersion "2.7.1"`
   - `README.md` lines 7 and 59: replace both `claude-mo2-setup-v2.7.0.exe` references with `v2.7.1`

5. **Add CHANGELOG placeholder** at top of `mo2_mcp/CHANGELOG.md`:

   ```markdown
   ## v2.7.1 — TBD

   <Phase 4 fills this in. Sections expected: Fixed — bridge (RACE silent failures, dict-stat unsettable); Added — bridge (silent-failure detection, bracket-indexer dict syntax, comprehensive operator wire-ups per AUDIT.md, RACE aliases); Note (v2.8 = verification release).>

   ---
   ```

6. **Add KNOWN_ISSUES placeholder section** for the RACE write surface — exact wording finalized in Phase 4 alongside the rest of the docs roll-up.

7. **Force-add this PLAN.md and AUDIT.md.** `git add -f Claude_MO2/dev/plans/v2.7.1_race_patching/{PLAN.md,AUDIT.md}`. Commit `tools/race-probe/Program.cs` extension as part of the same commit.

8. **Write `PHASE_0_HANDOFF.md`** confirming:
   - AUDIT.md committed; total wire-ups identified; any reclassifications driven by probe failures.
   - Probe builds and passes for every "wire up in P3" record type.
   - Version bumps landed across the four files.
   - No production code (PatchEngine.cs, tools_patching.py) touched.

9. **Commit:** `[v2.7.1 P0] Audit + scope lock + version bump to 2.7.1`. Push to `origin/main`.

### Acceptance

- `AUDIT.md` exists at `Claude_MO2/dev/plans/v2.7.1_race_patching/AUDIT.md` with one section per operator.
- Every "wire up in P3" row in AUDIT.md has a corresponding probe block in `Program.cs`, and the probe runs green.
- `git diff main^` shows: AUDIT.md (new), PLAN.md (new — this file), PHASE_0_HANDOFF.md (new), Program.cs (extended), 4 version-bump lines, the new CHANGELOG and KNOWN_ISSUES placeholders. No production code touched.
- `cd tools/race-probe && dotnet run -c Release` succeeds with the message "=== probe complete ===" at the end.

---

## Phase 1 — Tier D: silent-failure detection

**Goal:** Build the safety net first. Every subsequent phase's wire-ups land into a bridge that fails loud on unsupported (operator, record-type) pairs.

**Files to touch:**
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs`
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_1_HANDOFF.md`

### Steps

1. **Define the canonical operator → mods-key mapping** as a private static dict in PatchEngine.cs (near the top of the class, alongside `FieldAliases`):

   ```csharp
   private static readonly Dictionary<string, string> OperatorModsKeys = new()
   {
       ["AddKeywords"]         = "keywords_added",
       ["RemoveKeywords"]      = "keywords_removed",
       ["AddSpells"]           = "spells_added",
       ["RemoveSpells"]        = "spells_removed",
       ["AddPerks"]            = "perks_added",
       ["RemovePerks"]         = "perks_removed",
       ["AddPackages"]         = "packages_added",
       ["RemovePackages"]      = "packages_removed",
       ["AddFactions"]         = "factions_added",
       ["RemoveFactions"]      = "factions_removed",
       ["AddInventory"]        = "inventory_added",
       ["RemoveInventory"]     = "inventory_removed",
       ["AddOutfitItems"]      = "outfit_items_added",
       ["RemoveOutfitItems"]   = "outfit_items_removed",
       ["AddFormListEntries"]  = "form_list_added",
       ["RemoveFormListEntries"]= "form_list_removed",
       ["AddItems"]            = "items_added",
       ["AddConditions"]       = "conditions_added",
       ["RemoveConditions"]    = "conditions_removed",
       ["AttachScripts"]       = "scripts_attached",
       ["SetEnchantment"]      = "enchantment_set",
       ["ClearEnchantment"]    = "enchantment_cleared",
       ["SetFields"]           = "fields_set",
       ["SetFlags"]            = "flags_changed",  // shared with ClearFlags
       ["ClearFlags"]          = "flags_changed",
       // SetFields/SetFlags are generic; they always match a handler.
       // Listed here so the coverage check accepts them when present.
   };
   ```

   This mapping must align EXACTLY with the keys the existing handlers write to `mods`. Read each handler in `ApplyModifications` and confirm.

2. **`RequestedOperatorsOf(RecordOperation op)` helper.** Returns the set of canonical mods-keys for operators with non-null/non-empty values on the request. Check each operator field and add the corresponding mods-key if populated.

3. **Wrap `ApplyModifications`.** At the top: capture `var requested = RequestedOperatorsOf(op);`. At the bottom (after the existing `if (mods.Count == 0) detail.Modifications = null;` line, but BEFORE returning): compute `var unmatched = requested.Where(k => !mods.ContainsKey(k)).ToList();`. If `unmatched.Count > 0`, throw a new internal `UnsupportedOperatorException` (or equivalent) carrying the record type code and the unmatched operator list.

4. **Catch and roll back at the call site.** Find where `ApplyModifications(overrideRecord, op, detail);` is called ([PatchEngine.cs:191](../../../tools/mutagen-bridge/PatchEngine.cs)). Wrap in try/catch:
   - On `UnsupportedOperatorException`: call `TryRemoveOverride(patchMod, overrideRecord)` (existing helper), then propagate an error response with structured fields: `{"error": "Unsupported operator(s) for record type", "record_type": "<code>", "unmatched_operators": ["add_spells"]}`. The Python side surfaces this as a normal error.
   - Other exceptions: existing behavior.

5. **Update the existing "empty mods" branch.** Today, `if (mods.Count == 0) detail.Modifications = null;` is purely cosmetic. With Tier D, `mods.Count == 0` AND `requested.Count == 0` means "the request was a pure `op: override` with no modifications" — still valid. `mods.Count == 0` AND `requested.Count > 0` is the case Tier D errors on (handled by step 3). No change to the cosmetic null-out, but document the invariant in a comment.

6. **Build the bridge:** `cd tools/mutagen-bridge && dotnet build -c Release`. Zero warnings, zero errors.

7. **Inline smoke test** (this phase's "test our assumptions" step). Construct a minimal failing case:
   - Quick-and-dirty: extend race-probe (or write a sibling tools/coverage-smoke/) that builds a `bridge_request` containing `add_perks` against a CONT (Container) record. Pipe the JSON to `mutagen-bridge.exe` via stdin.
   - Expected: bridge returns an error response with `record_type: "CONT"` and `unmatched_operators: ["add_perks"]`.
   - Inverse case: same shape but with `set_fields` (which always matches via reflection) — expected to succeed without rollback.

   Capture both outputs in PHASE_1_HANDOFF.md.

8. **Write `PHASE_1_HANDOFF.md`** documenting:
   - The operator/mods-key mapping committed (the entire dict from step 1).
   - The exception type and error response shape.
   - Smoke test stdin/stdout for both the failing and passing case.

9. **Commit:** `[v2.7.1 P1] Tier D — silent-failure detection`. Push.

### Acceptance

- Bridge builds clean.
- Smoke test: unsupported operator → error response with structured fields, override rolled back (no spurious record in patch ESP).
- Smoke test: supported operator → success, normal mods accounting.
- The `OperatorModsKeys` mapping in code matches every key existing handlers actually write to `mods`. Any mismatch caught here is a bug in the existing handlers, not in Tier D — flag it in handoff for Phase 3 attention.

---

## Phase 2 — Tier C: bracket-indexer dict mutation

**Goal:** Teach `SetPropertyByPath` and `ConvertJsonValue` how to write to dict-typed properties whose `set` accessor is missing.

**Files to touch:**
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs`
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_2_HANDOFF.md`

### Steps

1. **Path-segment parser.** Add a private helper that splits a segment like `"Starting[Health]"` into `("Starting", "Health")`, or returns `("Starting", null)` when no bracket. Trim whitespace inside brackets. Reject malformed brackets (`Starting[`, `Starting[]`, `Starting]`) with `ArgumentException`.

2. **`SetPropertyByPath` extension** ([PatchEngine.cs:745](../../../tools/mutagen-bridge/PatchEngine.cs)).

   Modify the **final-segment** branch (after the for-loop that walks intermediate properties):
   - Parse the final segment with the helper.
   - **No bracket, value is JSON Object, target is a dict-typed property:** invoke whole-dict path (step 4).
   - **No bracket, otherwise:** existing behavior. Get property, convert value to `prop.PropertyType`, call `prop.SetValue(current, converted)`.
   - **Bracket present:** get the property, get its current value. Verify the runtime type implements `IDictionary<,>` (closed generic). Extract `TKey` and `TValue` generic arguments. Parse the bracket contents:
     - If `TKey.IsEnum`: `Enum.Parse(TKey, bracketContents, ignoreCase: true)`.
     - Else: pass through `ConvertJsonValue` against `TKey` using a synthetic `JsonElement` (or inline-handle string/int).
   - Convert the JSON value to `TValue` via existing `ConvertJsonValue`.
   - Locate the indexer (the single `PropertyInfo` whose `GetIndexParameters().Length == 1` matches `[TKey]`).
   - Invoke `indexer.SetValue(dictInstance, convertedValue, new[] { parsedKey })`.

   **Intermediate-segment behavior is unchanged** — no chained dict access in v2.7.1.

3. **`ConvertJsonValue` extension** ([PatchEngine.cs:777](../../../tools/mutagen-bridge/PatchEngine.cs)).

   Add a branch handling `JsonValueKind.Object` against dict-typed `targetType`:
   - Check `targetType` (or its underlying type) implements a closed `IDictionary<TKey, TValue>`. If not, fall through to existing "Cannot convert" exception.
   - Build a fresh `Dictionary<TKey, TValue>` instance via `System.Activator.CreateInstance`.
   - Iterate JSON object members; for each: parse property name as `TKey` (same enum/primitive parsing as step 2), convert value as `TValue`, call the new dict's `Add(key, value)`.
   - Return the constructed dict.

   This branch fires when a property HAS a setter accepting `IDictionary<,>` AND the user passes a JSON object value.

4. **Whole-dict via `set_fields` against a setter-less dict property** (the RACE.Starting case). When `SetPropertyByPath`'s final segment has no bracket AND the value is a JSON Object AND the target property is a dict AND the property has no setter:
   - Get the existing dict (Mutagen initializes to empty).
   - Iterate JSON object members; parse keys, convert values, call indexer per entry. (Merge semantics — preserves existing entries.)

5. **Build the bridge:** `dotnet build -c Release`. Zero warnings, zero errors.

6. **Inline smoke test** (this phase's "test our assumptions" step). Add a Tier C verification block to race-probe (or write a sibling) that:
   - Builds a `bridge_request` setting `Starting[Health]: 250` on a NordRace override.
   - Pipes the JSON to `mutagen-bridge.exe`.
   - Reads back the output ESP via the bridge's own RecordReader, confirms `Starting[Health] == 250`, `Magicka` and `Stamina` unchanged from the source.
   - Repeats for whole-dict form `Starting: { "Health": 100, "Magicka": 200, "Stamina": 300 }` and confirms all three.
   - Tests error case: `Starting[Bogus]` (unparseable enum) → expects an error response.

7. **Write `PHASE_2_HANDOFF.md`** documenting:
   - Hunks added (helper, SetPropertyByPath final-segment branch, ConvertJsonValue object branch, whole-dict branch).
   - Smoke test results: indexer form, whole-dict form, error case.
   - Note that intermediate-segment dict access is intentionally out of scope.

8. **Commit:** `[v2.7.1 P2] Tier C — bracket-indexer dict mutation`. Push.

### Acceptance

- Bridge builds clean.
- All three smoke variants pass.
- The error case (`Bogus` enum) hits Tier D's roll-back path and returns a clean error.

---

## Phase 3 — Tier A: comprehensive operator wire-ups

**Goal:** Close every gap in `AUDIT.md` classified "wire up in P3." Every wire-up landing here was probe-verified in Phase 0; this phase transcribes the verified contracts into bridge dispatch code.

**Files to touch:**
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs`
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_3_HANDOFF.md`

### Steps

1. **Read `AUDIT.md`.** Treat it as the authoritative scope. Every "wire up in P3" row is a deliverable; every "out of scope" row is honored without revisiting.

2. **For each AUDIT.md `add_keywords` / `remove_keywords` wire-up row:** add one case to the `GetKeywordsList` switch ([PatchEngine.cs:1242](../../../tools/mutagen-bridge/PatchEngine.cs)). One line per record type. Match the existing alphabetical-ish ordering. Example for RACE:

   ```csharp
   Race r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
   ```

3. **For each AUDIT.md `add_spells` / `remove_spells` wire-up row:** add a parallel `if (record is X x)` block below the existing NPC block (line 419). For RACE specifically:

   ```csharp
   if (record is Race race)
   {
       if (op.AddSpells?.Count > 0)
       {
           race.ActorEffect ??= new ExtendedList<IFormLinkGetter<ISpellRecordGetter>>();
           mods["spells_added"] = AddFormLinks(race.ActorEffect, op.AddSpells);
       }
       if (op.RemoveSpells?.Count > 0 && race.ActorEffect != null)
           mods["spells_removed"] = RemoveFormLinks(race.ActorEffect, op.RemoveSpells);
   }
   ```

4. **For other operator gaps in AUDIT.md** (`add_items` on LVLN/LVSP, enchantment on AMMO, attach_scripts on additional record types, conditions on additional types — exact list per audit): mirror the existing dispatch pattern for that operator. Reference the probe-verified API contract from AUDIT.md for each new block.

5. **Build the bridge:** `dotnet build -c Release`. Zero warnings, zero errors.

6. **Inline smoke test (this phase's biggest one).** For EACH new (operator, record-type) pair wired up:
   - Pick a representative record from the live modlist (e.g. for keyword-on-Container: a vanilla container; for keyword-on-Spell: a vanilla spell).
   - Build a `bridge_request` exercising the operator.
   - Pipe to `mutagen-bridge.exe`. Confirm the response includes the corresponding `mods` key.
   - Read back the output ESP, confirm the change landed.
   - Capture the result in a per-pair row in the handoff.

   Expected output: a table with N rows, all `pass`. Any failure → handoff is `Status: Partial`, list which pair(s) failed, AUDIT.md updated to mark them "needs investigation," and Phase 3 is incomplete.

7. **Update Python schema descriptions inline** (don't wait for Phase 4): for each operator that gained a record type, update the description in `tools_patching.py`. The full Phase 4 docs roll-up will reconcile, but the live state of descriptions should match the live state of code at every commit boundary.

   Actually — defer this to Phase 4 because the alias roll-up there reorganizes the descriptions anyway, and updating twice is wasted work. **Exception:** if any operator's (record-type) list materially changes the user-facing semantics (e.g. a new record type that wouldn't have been guessable), call it out in the handoff so Phase 4 doesn't miss it.

8. **Write `PHASE_3_HANDOFF.md`** documenting:
   - Per-pair smoke result table (N rows, one per newly-wired pair).
   - Total wire-ups landed.
   - Bridge build clean confirmation.
   - Any AUDIT.md reclassifications driven by smoke failures.

9. **Commit:** `[v2.7.1 P3] Tier A — comprehensive operator wire-ups (N pairs)`. Push.

### Acceptance

- Every "wire up in P3" row in AUDIT.md has a smoke `pass` in the handoff.
- Bridge builds clean.
- No `Partial` status — if anything failed, AUDIT.md was updated to reflect the new reality and the handoff explains.

---

## Phase 4 — Tier B aliases + Python schema + final docs roll-up + bridge rebuild

**Goal:** Cosmetic polish, schema reconciliation, KNOWN_ISSUES update, CHANGELOG finalization, production bridge artifact.

**Files to touch:**
- `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (Tier B aliases only)
- `Claude_MO2/mo2_mcp/tools_patching.py` (schema descriptions)
- `Claude_MO2/build-output/mutagen-bridge/mutagen-bridge.exe` (rebuilt artifact)
- `Claude_MO2/KNOWN_ISSUES.md` (RACE write surface section + general write-surface update reflecting AUDIT.md scope)
- `Claude_MO2/mo2_mcp/CHANGELOG.md` (finalize the placeholder)
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_4_HANDOFF.md`

### Steps

1. **`FieldAliases` `["RACE"]` block** ([PatchEngine.cs:692](../../../tools/mutagen-bridge/PatchEngine.cs)). Add after the existing `["ALCH"]` block:

   ```csharp
   ["RACE"] = new()
   {
       ["BaseHealth"]   = "Starting[Health]",
       ["BaseMagicka"]  = "Starting[Magicka]",
       ["BaseStamina"]  = "Starting[Stamina]",
       ["HealthRegen"]  = "Regen[Health]",
       ["MagickaRegen"] = "Regen[Magicka]",
       ["StaminaRegen"] = "Regen[Stamina]",
   },
   ```

2. **Confirm `RecordTypeCode` returns `"RACE"` for `IRaceGetter`.** It currently falls into the `_ => record.Registration.ClassType.Name.ToUpperInvariant()` default. Verify via a quick reflection call. If the default returns `"RACE"` already (which it should), no change needed; otherwise add an explicit `IRaceGetter => "RACE"` line.

3. **Python schema description full reconciliation** ([tools_patching.py:82-105](../../../mo2_mcp/tools_patching.py)).

   Use AUDIT.md as the source of truth. For each operator, the description should list the supported record types post-v2.7.1.

   - `add_keywords` / `remove_keywords`: list every type with a Keywords property the bridge now dispatches on.
   - `add_spells` / `remove_spells`: list `(NPC, RACE)`.
   - `add_perks` / `add_packages` / `add_factions`: NPC-only stays.
   - `add_inventory`: `(NPC, container)` stays.
   - `add_outfit_items`: Outfit-only.
   - `add_form_list_entries`: FormList-only.
   - `add_items`: list every leveled-list type now wired (LVLI definitely, LVLN/LVSP if AUDIT added them).
   - `set_fields`: append documentation for bracket-indexer syntax: `"Dict-typed fields support bracket syntax: 'Starting[Health]: 100' on RACE; works for any Mutagen IDictionary<,> property. Whole-dict assignment via JSON object: 'Starting: {Health: 100, Magicka: 200}' (merge semantics — only specified keys touched)."`
   - `attach_scripts`: list types per AUDIT.
   - `set_enchantment` / `clear_enchantment`: list types per AUDIT.
   - **NEW: error response shape.** Add a top-level note in the `mo2_create_patch` description: `"Returns an error with 'unmatched_operators' if a requested operator is not supported on the target record type — silent drops were eliminated in v2.7.1."`

4. **`KNOWN_ISSUES.md` update.** Find the existing patching-related section (or add one). Document:
   - As of v2.7.1: comprehensive write surface — list the operators × record types matrix at a high level (link to PLAN.md / AUDIT.md for the full table).
   - Silent drops eliminated: any unsupported (operator, record-type) pair now returns an explicit error.
   - **Carried-over limitations** (not addressed in v2.7.1):
     - Chained dict access (`Foo[Key].Sub`) — terminal-dict only.
     - Replace-semantics whole-dict assignment — merge-only today.
     - **v2.8 = verification release.** Real-world usage will surface bugs in the v2.7.1 wire-ups; v2.8 fixes them. No new capabilities planned.

5. **`CHANGELOG.md` finalization.** Replace the Phase 0 placeholder with a real v2.7.1 entry:

   ```markdown
   ## v2.7.1 — TBD (Phase 5 inserts the date)

   Comprehensive bridge coverage expansion driven by a user report on RACE patching. The surfaced limitation was a single (record-type, operator) gap; investigation found a class of silent-failure bugs covering many record types. v2.7.1 closes the bug class generically (Tier D), expands the bracket-indexer write capability (Tier C), wires up every Mutagen-supported (operator, record-type) gap the audit identified (Tier A), and adds RACE stat aliases for ergonomics (Tier B). v2.8 will be the verification/hardening release — no new capabilities, real-world exercise of v2.7.1's wire-ups, fix what surfaces.

   ### Fixed — bridge

   - **RACE keywords** (`add_keywords`/`remove_keywords`) now write to the output ESP. Previously silently dropped.
   - **RACE actor effects** (`add_spells`/`remove_spells`) now write to the output ESP. Previously silently dropped.
   - **RACE per-stat starting values and regen rates** (`Starting[Health]`, `Regen[Magicka]`, etc.) are now writable via `set_fields`. Previously rejected — the underlying Mutagen properties have no public setter, only an indexer.
   - **Silent-failure bug class eliminated.** Any requested operator without a matching handler for the record type now returns an explicit error with `unmatched_operators` field listing each unsupported (operator, record-type) pair, and rolls back the override so no no-op records ship in the patch.
   - **<Other gaps from AUDIT.md, listed by category — keywords on Container/Door/Furniture/Light/Activator/Location/Spell/MagicEffect; LVLN/LVSP entry-add; AMMO enchantment if AUDIT included; etc.>**

   ### Added — bridge

   - **Bracket-indexer path syntax** in `set_fields` for dict-typed Mutagen properties (`PropertyName[Key]`). Works for any Mutagen dict whose key type is an enum or primitive.
   - **JSON-object form of `set_fields`** for dict-typed properties (merge semantics — only specified keys touched).
   - **RACE field aliases:** `BaseHealth`, `BaseMagicka`, `BaseStamina`, `HealthRegen`, `MagickaRegen`, `StaminaRegen` — shortcuts for the corresponding `Starting[…]` / `Regen[…]` paths.

   ### Out of scope (v2.8 candidates)

   - Chained dict access (`Foo[Key].Sub`).
   - Replace-semantics whole-dict assignment (today's whole-dict form is merge-only).
   - The v2.8 verification release exercises every wire-up in real workflows; bugs surfaced get fixed.

   ---
   ```

6. **Rebuild production bridge.** From `tools/mutagen-bridge/`:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false -o ../../build-output/mutagen-bridge/
   ```
   (Reference v2.7.0's PHASE_6_HANDOFF.md for the exact command if uncertain.)

   Verify the rebuilt `mutagen-bridge.exe` runs as expected.

7. **Write `PHASE_4_HANDOFF.md`** documenting:
   - Aliases added.
   - Python schema diffs (one-line summary per operator).
   - KNOWN_ISSUES diff.
   - CHANGELOG finalized.
   - Bridge build SHA256 (`sha256sum build-output/mutagen-bridge/mutagen-bridge.exe`).

8. **Commit:** `[v2.7.1 P4] Tier B aliases + Python schema + docs roll-up + bridge rebuild`. Include the rebuilt `mutagen-bridge.exe`. Push.

### Acceptance

- Bridge builds clean.
- Every operator description in `tools_patching.py` matches AUDIT.md's post-P3 reality.
- CHANGELOG entry comprehensive but concise; KNOWN_ISSUES reflects new write surface.
- Bridge SHA256 captured.

---

## Phase 5 — Ship v2.7.1

**Goal:** Pre-ship comprehensive smoke matrix (final assumption test); installer build; live install sync; tag; GitHub release.

**Files to touch:**
- `Claude_MO2/build-output/installer/claude-mo2-setup-v2.7.1.exe` (built artifact)
- `Claude_MO2/mo2_mcp/CHANGELOG.md` (insert ship date)
- `<live>/` (live install — synced once at end)
- `Claude_MO2/dev/plans/v2.7.1_race_patching/PHASE_5_HANDOFF.md`

### Steps

1. **Pre-ship comprehensive smoke matrix.** Run a final integration test that exercises every wire-up landed across Phases 1-4 in a single bridge session against live modlist data:
   - For each AUDIT.md row: build a `bridge_request` that exercises that (operator, record-type) pair against a real record from the modlist.
   - For Tier D: a deliberately-unsupported request (e.g. `add_perks` on a Container) — confirm the error response.
   - For Tier C: bracket-indexer write to RACE.Starting AND whole-dict form.
   - For Tier B: an alias write (`BaseHealth: 250`) — confirm it resolves to `Starting[Health]`.
   - Bundle all into a single output ESP for inspection. Verify each change via read-back.
   - Output goes to a throwaway directory (`<workspace>/scratch/v2.7.1-final-smoke.esp`), NOT the live install.

   Capture the matrix result in PHASE_5_HANDOFF.md as a table — every row pass.

2. **Build installer.** Run the same Inno Setup compile command v2.7.0 used. Confirm output is `build-output/installer/claude-mo2-setup-v2.7.1.exe`. Capture SHA256.

3. **Live sync.** Copy `build-output/mutagen-bridge/mutagen-bridge.exe` to `<live>/tools/mutagen-bridge/`. Copy any changed Python files (`mo2_mcp/tools_patching.py` and any other touched in Phase 4) to `<live>/mo2_mcp/`. Restart the MCP server in MO2. Run `mo2_ping` — confirm v2.7.1 banner.

4. **Live sanity check.** Re-run two or three smoke operations from step 1 against the live install (read-only against modlist data; write into the user's MO2 output mod under a recognizable test filename). Verify with `mo2_record_detail`. Then **delete the test patch** — don't ship test artifacts to the user's modlist.

5. **Insert ship date in CHANGELOG.** Replace `## v2.7.1 — TBD` with `## v2.7.1 — 2026-MM-DD`.

6. **Tag + release.** From `<repo>`:
   ```bash
   git tag v2.7.1
   git push origin v2.7.1
   gh release create v2.7.1 \
     build-output/installer/claude-mo2-setup-v2.7.1.exe \
     --title "v2.7.1 — Bridge coverage expansion + silent-failure detection" \
     --notes-file <path-to-release-notes>
   ```

   Release notes = a condensed CHANGELOG entry, release-page-friendly.

7. **Update memory** (per project memory rules — `project_capability_roadmap.md`):
   - Title becomes "v2.7.1 shipped — bridge coverage expansion; v2.8 = verification release".
   - Body documents: silent-failure detection landed; AUDIT.md count of (operator, record-type) wire-ups; v2.8 framed as "real-world exercise + bug fixes only."

8. **Write `PHASE_5_HANDOFF.md`** documenting:
   - Pre-ship smoke matrix results.
   - Installer SHA256.
   - Bridge SHA256 (re-confirm matches Phase 4).
   - GitHub release URL.
   - Live install confirmation.
   - Memory updated.

9. **Final commit:** `[v2.7.1 P5] Ship v2.7.1`. Include the built installer. Push.

### Acceptance

- `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.7.1` resolves with the installer attached.
- `<live>/` running v2.7.1 (verified via `mo2_ping`).
- Memory reflects v2.7.1 shipped + v2.8 framed as verification release.
- `origin/main` ahead by exactly 6 commits from v2.7.0 tag (one per phase).

---

## ⚠️ Carry-overs (NOT addressed in v2.7.1; v2.8 candidates)

These are explicitly out of scope for v2.7.1 but flagged so future sessions don't re-discover them as new gaps:

1. **Chained dict access (`Foo[Key].Sub`).** Tier C limited to terminal dict access. No known consumers today. v2.8 if a real case appears.

2. **Replace-semantics whole-dict assignment.** `set_fields: { "Starting": { "Health": 100 } }` merges (only Health touched). To clear-then-set, no syntax today. v2.8 if a real case appears.

3. **MCP tool surface for `tool_paths.json`.** Carried over from v2.7.0 — `mo2_get_tool_paths` / `mo2_set_tool_path` would let Claude inspect/update config without manual JSON edits.

4. **Plugin-setting unification into `tool_paths.json`.** Carried over from v2.7.0.

5. **Inno static-AppId registry hygiene.** Carried over from v2.7.0 — multi-instance MO2 installs accumulate registry entries.

6. **v2.8 itself = verification release.** No new capabilities. Real workflow exercise of every wire-up landed in v2.7.1; whatever fails, gets fixed. Driven by user reports + a one-pass run against the modlist exercising AUDIT.md's full matrix in realistic scenarios. Plan when v2.7.1 ships.
