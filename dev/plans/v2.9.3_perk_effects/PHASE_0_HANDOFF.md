# Phase 0 Handoff — Plan + matrix scaffold + design proposal

**Phase:** 0
**Status:** Complete
**Date:** 2026-04-28
**Session length:** ~1h
**Commits made:** `25cef3a` (work) + this hash-record commit
**Live install synced:** No (Phase 0 is docs-only; live remains at v2.9.2 per CLAUDE.md exemption — Phase 0 doesn't touch the live install or invoke MCP tools)

## Locks

- **Working version slug = `v2.9.3`** — locked by Aaron 2026-04-28 (per the conductor's kick-off prompt § State checks: "Locked slug `v2.9.3` recorded in handoff (Aaron 2026-04-28)"). Phase 2 commits the version-bump constants (`config.py`, `.iss`, `README.md`); Phase 0 records the slug in this handoff only.
- Plan dir name (`v2.9.3_perk_effects/`) matches the slug — no rename pending.

## Conductor decisions inherited (locked)

These are pre-litigated and carry forward to Phase 1's executor without re-debate:

1. **Version slug = `v2.9.3`** (above; locked Aaron 2026-04-28).
2. **Q1–Q7 design questions are NOT pre-decided.** Aaron wants all seven surfaced formally at Phase 0 hand-off; conductor relays to Aaron for explicit lock; Phase 1 doesn't open until the lock is in. Phase 0's role is to surface, not decide. Defaults are proposed below per PLAN § A–J rationales; Aaron can hold defaults or flip any.
3. **Single-mechanism scope.** v2.9.3 is one Branch A extension on `ConvertJsonElementToListItem` (special-case `typeof(APerkEffect)` → `BuildPerkEffectFromJson`) + the new factory + PERK addition to v2.8.0's Effects-list carrier set. No other tool changes, no other bridge command additions, no `RecordReader.RenderValue` changes (v2.9.2's read-side already renders correctly). QUST.Aliases / Stages / Objectives stay deferred. v2.9.x carry-overs (Boolean dispatcher branch, sub-B 6 String functions, AMMO enchantment, replace-semantics dict, chained dict access, GetVATSValueUnknown Mutagen gap, etc.) stay deferred.
4. **Scope absorption posture (v2.9.3+ standing rule).** Stricter than legacy ">1 h or new operator" bar per Aaron 2026-04-28: any new write surface (new write functions / write-surface mechanisms / opportunistic carry-over absorptions) escalates to conductor → Aaron BEFORE absorption, even when trivial. Latent-bug fixes in touched code keep the legacy ">1 h or new operator → halt" bar. Phase 0 surfaces no candidates for absorption — the scoping session's PLAN already covers what's in scope. Phase 1+ executors fold in only load-bearing bonus catches per Aaron's call.
5. **Single-commit deliverable for Phase 0 per PLAN § J.** This Phase 0 commit force-adds PLAN.md + MATRIX.md + CONDUCTOR_KICKOFF.md + PHASE_0_HANDOFF.md in one work commit + one hash-record commit pair. The scoping session that wrote PLAN.md + CONDUCTOR_KICKOFF.md left them untracked; Phase 0 force-adds them alongside the new MATRIX + handoff.

## What was done

- **`<plan>/MATRIX.md`** — NEW. Six-layer scaffold (Layer 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention + per-phase fill-in checklists. Mirrors v2.9.2's MATRIX.md structurally; anchored on **PERK.Effects writability** (per-subclass coverage rows in Layer 1.P, new discriminator validation error paths in Layer 1.D, cross-subclass + composition cells in Layer 2, live Requiem-perk-rebalance scenario in Layer 3, DSL-shape + sibling-preservation edges in Layer 4.dsl, full v2.9.2 regression band in Layer 5). 274 lines. Per-subclass rows are placeholders awaiting Phase 1's `APerkEffect` audit; Layer 1.P expandable with `1.P.<Subclass>.basic` rows per additional concrete subclass Phase 1 enumerates per Q2 ship-full-set default.
- **Cell-ID convention** documented at MATRIX.md § Cell-naming convention:
  - `1.P.<Subclass>.<sub>` — Layer 1 positives, anchored on concrete `APerkEffect` subclass + sub-shape descriptor (e.g. `1.P.PerkEntryPointEffect.minimal`, `1.P.PerkEntryPointEffect.with_perk_conditions`, `1.P.PerkAbility.basic`)
  - `1.D.<NN>` — Layer 1.D negatives + new explicit error paths (e.g. `1.D.01` discriminator unknown, `1.D.02` abstract base supplied, `1.D.03` missing discriminator)
  - `2.<NN>` — Layer 2 combinatorial (heterogeneous subclasses, full-stack composition, replace-semantics)
  - `3.<N>` — Layer 3 workflow scenarios (`3.1` Requiem perk rebalance mandatory; `3.2` multi-effect PERK optional)
  - `4.<sub>.<NN>` — Layer 4 edges (only `4.dsl.<NN>` sub-grouping needed for v2.9.3; v2.9.0/v2.9.1's other Layer 4 sub-groups don't apply because v2.9.3 doesn't change the per-Condition or per-FormLink build pipeline; only `dsl` covers parameter-value-form + cross-master + sibling edges)
  - `5.range` — Layer 5 regression (mapped 1:1 to v2.9.2's 425 cells; matches v2.9.2's MATRIX.md convention)
- **Layer 1.P pre-spec'd** with 5 baseline cells covering each PLAN-named subclass's primary success path: `1.P.PerkEntryPointEffect.minimal` (single PerkEntryPointEffect with EntryPoint + Modification + Value, no nested conditions), `1.P.PerkEntryPointEffect.with_perk_conditions` (with single-tab nested conditions per Q7 wrapper-object DSL default), `1.P.PerkEntryPointEffect.with_v290_params` (nested condition uses `parameters: {Perk: ...}` — composition probe verifying v2.9.0 dispatcher untouched per § F default), `1.P.PerkAbility.basic` (FormLink-to-SPEL shape), `1.P.PerkQuestEffect.basic` (FormLink-to-QUST + Stage shape). Per-subclass rows expand at Phase 1 audit time per Q2 ship-full-set default.
- **Layer 1.D pre-spec'd** with 7 cells covering the new discriminator validation surface from PLAN § A factory + § B Q1 lock + the existing v2.8.0 carrier-set rejection path: `1.D.01` (discriminator unknown — BogusType), `1.D.02` (abstract base APerkEffect supplied as type), `1.D.03` (no `type:` key — missing discriminator), `1.D.04` (nested condition uses v2.9.0-out-of-scope function — composition probe verifying v2.9.0 dispatcher's "not yet wired" error surfaces unchanged in v2.9.3-context), `1.D.05` (unknown property on PerkEffect — existing Branch B / Tier C error path), `1.D.06` (Effects on non-carrier record NPC_ — existing v2.8.0 carrier-rejection preserved with PERK added), `1.D.07` (defensive: supplying `type:` on SPEL doesn't reroute to BuildPerkEffectFromJson — verifies gating remains correct). Wording for new error messages locks the shape; Phase 2 finalizes exact strings.
- **Layer 2 pre-spec'd** with 4 combinatorial cells: `2.01` heterogeneous subclasses in one Effects array (3 entries: PerkEntryPointEffect + PerkAbility + PerkQuestEffect — verifies factory dispatches per-element + ExtendedList<APerkEffect> carries heterogeneous concrete entries); `2.02` replace-semantics + Tier C scalar coexistence (`set_fields: {Effects: [...], Level: 25, NumRanks: 3, Trait: true}` on same record — verifies v2.9.3 Effects-array replace-semantics composes with v2.7.x Tier C scalar set_fields); `2.03` full-stack composition (Branch A → factory → wrapper → Condition factory → v2.9.0 dispatcher in one cell — the canonical regression-witness for v2.9.3); `2.04` empty-array clear (`Effects: []` on PERK — mirrors v2.8.0 Test 29 / 1.E.07 + verifies sibling fields preserved).
- **Layer 3 workflow scenarios pre-spec'd** with use-case description + assertions + placeholder FormIDs for Phase 3:
  - **Scenario 3.1 — Requiem-style PERK magnitude rebalance (mandatory).** Real-world AI-driven patcher: Authoria tester rebalancing Requiem's perk magnitudes — change `ModSpellMagnitude` from 1.5× to 1.4× on AugmentedShock60 (`Skyrim.esm:10FCFA`) etc. Today: blocked. v2.9.3 unblocks: single `mo2_create_patch` call with `set_fields: {Effects: [...]}` rewrites the Effects array. PerkConditions structure round-tripped via v2.9.2's `expand_links: ["Effects"]` read.
  - **Scenario 3.2 — Multi-effect PERK preserving subclass mix (optional).** PlayerWerewolfFeed (`Skyrim.esm:02BA1D`) or analog Werewolf/Vampire-style perk — heterogeneous-subclass write surface on real-world PERK shape. Phase 3 confirms multi-subclass precondition via pre-write `mo2_record_detail`; falls back to alternative anchor or skip-with-reason if precondition fails.
  - Both scenarios use pre-write `mo2_record_detail(expand_links: ["Effects"])` (v2.9.2 read-side mechanism) → write via `mo2_create_patch` → post-write readback verification. Test ESPs go to `<modlist>/mods/Claude Output/`, deleted post-verification per `Claude_MO2/CLAUDE.md` § live install sync.
- **Layer 4.dsl pre-spec'd** with 5 DSL-shape + cross-master + sibling edge cells: `4.dsl.01` write/read symmetry round-trip (v2.9.3 write produces a record whose v2.9.2 read-side render is identical to a vanilla PERK with the same effect shape), `4.dsl.02` cross-master FormLink in nested condition (verifies v2.6.0 ESL-flagged compacted FormLink composes with v2.9.3 nested-condition write — synthetic two-plugin fixture mirrors v2.9.2 P4 pattern), `4.dsl.03` enum parse error on `PerkEntryPointEffect.EntryPoint` (verifies enum dispatch on PerkEntryPointType same as on ConditionData enum slots), `4.dsl.04` empty PerkConditions list (verifies non-failure on empty nested list), `4.dsl.05` sibling preservation invariant (Effects replace-semantics doesn't bleed into top-level scalars; Branch B in-place merge invariant on the dict + Effects-array replace-semantics on Effects only).
- **Layer 5 regression band** pointer recorded — single range row covering v2.9.2's 425 coverage-smoke cells unchanged (Phase 2 confirms the actual baseline against `coverage-smoke/Program.cs`).
- **Total assertion count + harness output convention + skip-with-reason** sections mirror v2.9.2's MATRIX.md structurally (~23 matrix rows + Phase 1 additions, ~452 + Phase 1 additions harness cells total).
- **Per-phase fill-in checklists** (Phase 1 hand-back, Phase 2 hand-back, Phase 3 hand-back) document exactly which placeholders each subsequent phase replaces — Phase 1 substitutes confirmed concrete `APerkEffect` subclass names + per-subclass property surface + PERK anchor FormIDs (PerkAbility / PerkQuestEffect placeholders) + heterogeneous-subclass anchor confirmation + EntryPoint enum dump + Q1–Q7 lock audit; Phase 2 confirms Layer 5 cell count + finalizes Layer 1.D validation-error JSON shape + finalizes error-message wording + cross-master synthetic fixture + locks empty-clear regression invariant; Phase 3 picks live FormIDs + lands per-scenario PASS/FAIL + lands Scenario 3.2 in-scope-or-skip + verifies test ESP cleanup.
- **Layer 1 vs Layer 3 disambiguation callout** added at MATRIX.md preamble: Layer 1 = bridge-mechanism verification on vanilla data via race-probe + coverage-smoke; Layer 3 = live workflow scenarios on Authoria modlist via mo2_create_patch + mo2_record_detail. Synthetic-vs-vanilla-record carrier choice for Layer 1 / 2 / 4 cells is Phase 2's call (Phase 0's vanilla FormID spec is the default).
- **`<plan>/PLAN.md` and `<plan>/CONDUCTOR_KICKOFF.md` force-added** in this same commit. The scoping session that wrote them left them untracked; Phase 0's single-commit deliverable per PLAN § J bundles all four artifacts together.
- **`<plan>/PHASE_0_HANDOFF.md`** — NEW (this file).

No production code touched. No version bump. Single-commit deliverable: PLAN.md + MATRIX.md + CONDUCTOR_KICKOFF.md + PHASE_0_HANDOFF.md force-added together via `git add -f Claude_MO2/dev/plans/v2.9.3_perk_effects/{PLAN,MATRIX,CONDUCTOR_KICKOFF,PHASE_0_HANDOFF}.md` in one work commit + one hash-record commit.

## Verification performed

Phase 0 has no test runs — it's structural scaffolding. Verification = the structural mirror of v2.9.2's MATRIX.md adapted for v2.9.3's per-subclass anchor.

| Check | v2.9.2 | v2.9.3 (this matrix) | Match |
|---|---|---|---|
| Header + methodology block | yes | yes (anchor shifted from read-side efficiency to PERK.Effects writability) | ✅ |
| Layer numbering | 1.P + 1.D + 2 + 3 + 4 (dsl only) + 5 | 1.P (per-subclass) + 1.D (discriminator + carrier-rejection + defensive) + 2 + 3 + 4 (dsl only) + 5 | ✅ (anchor shifted from per-axis to per-subclass; Layer 4 sub-group set carried forward — only `dsl` because v2.9.3 doesn't change build pipelines) |
| Cell-ID convention documented | explicit § Cell-naming convention table | explicit § Cell-naming convention table | ✅ (different anchor — `1.P.<Subclass>.<sub>` vs v2.9.2's `1.P.<axis>.<RecordType>[.<sub>]`) |
| Per-row columns (axis / type / source / operation / expected) | yes | yes | ✅ |
| Layer 3 workflow scenarios | 1 mandatory + 1 optional | 1 mandatory (3.1 Requiem perk rebalance) + 1 optional (3.2 multi-effect PERK) | ✅ |
| Total assertion count section | yes (~440 harness cells) | yes (~452 + Phase 1 additions harness cells; Layer 5 regression carries the bulk at 425 cells) | ✅ |
| Harness output convention | yes | yes (mirrors v2.9.2 example block) | ✅ |
| Skip-with-reason convention | yes (RACE anchor fixture availability) | yes (PERK anchor fixture availability per subclass) | ✅ |
| Phase fill-in checklists | three (Phase 1 + Phase 2 + Phase 3 hand-backs) | three (Phase 1 + Phase 2 + Phase 3 hand-backs) | ✅ |
| Layer 1 vs Layer 3 disambiguation | implicit | explicit callout in preamble | ✅ (hardened per conductor's Halt 1 sanity note 1) |

State checks passed at session start:

- `git log -1 --oneline origin/main` → `ce20112 [v2.9.2 P5] Handoff: record commit hash c397e6f` ✅ (matches kick-off prompt's State checks anchor — v2.9.2 ship is the canonical baseline).
- `git status` → clean working tree, untracked files: `dev/plans/v2.9.3_perk_effects/PLAN.md` + `dev/plans/v2.9.3_perk_effects/CONDUCTOR_KICKOFF.md` (from scoping session — Phase 0 force-adds these alongside the new MATRIX + handoff per PLAN § J) ✅.
- `mo2_ping` skipped per CLAUDE.md exemption recorded in kick-off prompt — Phase 0 is doc/matrix scaffolding only, no MCP tool dependence.

## Bugs surfaced

N/A. Phase 0 is scoping-only.

## Deviations from plan

None. Phase 0 ran exactly as PHASE_0 kick-off prompt and PLAN.md § Phase 0 specified. Cell-ID convention adapted to v2.9.3's per-subclass anchor (`1.P.<Subclass>.<sub>` — Phase 0 prerogative, defensible: anchors on the v2.9.3 unit of work which is the concrete `APerkEffect` subclass dispatched in the factory, not v2.9.2's per-axis or v2.9.1's per-list-target).

Layer 1.P count = 5 baseline cells (3 PerkEntryPointEffect sub-shapes + 1 PerkAbility + 1 PerkQuestEffect), expandable per Phase 1 audit (Q2 ship-full-set default).

Layer 1.D count = 7 cells (vs PLAN § Phase 0 step 2 listing 7 cells `1.D.01`–`1.D.07` — matches).

Layer 2 count = 4 cells (vs PLAN § Phase 0 step 2 listing 4 cells `2.01`–`2.04` — matches).

Layer 4.dsl count = 5 cells (vs PLAN § Phase 0 step 2 listing 5 cells `4.dsl.01`–`4.dsl.05` — matches).

Layer 3 scenario count = 1 mandatory + 1 optional (vs PLAN § Phase 0 step 2 mentioning 3.1 Requiem perk rebalance + optional 3.2 multi-effect PERK — matches).

Q-numbering: PLAN § Phase 0 step 4 + § J both list **7 design questions** (Q1 discriminator strategy, Q2 Pareto vs full coverage, Q3 replace semantics — sanity-confirm, Q4 v2.9.0 composition — sanity-confirm, Q5 discriminator canonical form, Q6 QUST sub-records absorb-or-defer, Q7 PerkConditions nested-list shape); kick-off prompt restates them as Q1–Q7 in identical ordering. Phase 0 surfaces all seven in § Conductor asks below in the conductor's required format.

## Known issues / open questions

None Phase 0 needs Phase 1 to know beyond the 7 design questions captured in § Conductor asks. PLAN.md § Phase 1 already covers Phase 1's responsibilities exhaustively.

Layer 1.D.04 expectation depends on Phase 2's pick of an actual v2.9.0-out-of-scope condition function for the composition probe (e.g. a Boolean-dispatcher-branch function or a sub-B 6 String-typed function). The matrix locks the shape (composition probe — v2.9.0's existing "not yet wired" error surfaces unchanged in v2.9.3-context); Phase 2 picks the function + finalizes wording.

Layer 1.D.07 (defensive: `type:` key on SPEL) expectation depends on Phase 2's confirmation that the `type:` key gets treated as an unknown property on `Effect` and surfaces as a `SetPropertyByPath` error. If Phase 2's implementation routes `type:` to a different error class (e.g. silently-ignored unknown key), the matrix updates per Phase 2 hand-back checklist.

Layer 1.P PerkAbility / PerkQuestEffect anchor FormIDs are Phase 0 placeholders — Phase 1's audit picks vanilla PERKs with the canonical subclass entries populated (likely Werewolf/Vampire perk family for PerkAbility per PLAN.md § E read-side observation; Standing Stone perks or quest-rewarded perks for PerkQuestEffect).

Layer 2.01 heterogeneous-subclass anchor (PlayerWerewolfFeed `Skyrim.esm:02BA1D`) is Phase 0 placeholder — Phase 1 confirms multi-subclass Effects array on the picked anchor or substitutes.

## Conductor asks

Seven design questions awaiting Aaron's lock via the conductor relay. Phase 1 doesn't open until all seven are locked. Phase 0 proposes a default for each per PLAN § A–J rationales; Aaron locks via the conductor's relay. Format per the conductor's kick-off prompt § Q1–Q7.

```
CONDUCTOR ASK
Phase: 0
Topic: Q1 — Discriminator strategy for APerkEffect concrete-subclass dispatch
Context:
  - PLAN § B names three options: explicit `type:` field (each Effects entry carries `{type: "PerkEntryPointEffect", ...}` — mirrors v2.8.0's `function:` discriminator on Condition entries; subclass naming is public Mutagen API, stable across point releases per v2.9.0's CONDITIONS_AUDIT) vs distinguish-by-fields-populated (route to subclass whose property surface matches — fragile on inherited-base typos; breaks if Mutagen 0.54+ adds overlapping properties) vs implicit-via-EntryPoint-presence (PerkEntryPointEffect detected by `EntryPoint:` key, others by mutually-exclusive subclass-defining fields — fails the "what about new subclasses Phase 1 hasn't named yet?" generality test).
  - Locks downstream: factory shape (uniform reflection-lookup vs per-subclass branch), schema discoverability (caller schema description), v2.9.x-extensibility (zero-code-change vs per-subclass-add for new subclasses).
  - Phase 1's audit confirms exact subclass names + checks for naming conflicts (vanishingly unlikely in `Mutagen.Bethesda.Skyrim` namespace but worth probing for); if any subclass naming conflict surfaces, Phase 0 escalates Q1 to Aaron.
Question: How does the JSON DSL specify which concrete `APerkEffect` subclass each Effects-list element constructs as?
Suggested options:
  A. Explicit `type:` field per element (e.g. `{type: "PerkEntryPointEffect", EntryPoint: "ModSpellMagnitude", ...}`) — mirrors v2.8.0 Condition pattern; uniform reflection-lookup factory; Mutagen-rename-safe within a major version; zero-code-change for new subclasses.
  B. Distinguish-by-fields-populated — route to subclass whose property surface matches; no `type:` key required; fragile on inherited-base typos.
  C. Implicit-via-EntryPoint-presence — PerkEntryPointEffect detected by `EntryPoint:`, others by mutually-exclusive subclass-defining fields; per-subclass detection rules; less general.
Phase 0 default: A — explicit `type:` field. Mirrors v2.8.0's Condition `function:` pattern; uniform reflection-lookup; v2.9.x-extensible without per-subclass code.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q2 — Pareto vs full coverage of APerkEffect concrete subclasses
Context:
  - PLAN § E names the trade-off: ship every concrete subclass Phase 1 enumerates (uniform factory pattern — adding a 13th subclass is zero new code; only coverage-smoke cell count grows) vs Pareto-defer obscure-tail subclasses (footgun: callers don't know which subclass their workflow needs; Pareto-trim blocks Werewolf/Vampire-style multi-variant perk workflows).
  - Phase 1's audit deliverable is the authoritative subclass count (user task spec named ~13; actual count may be 3 / 5–8 / 13 depending on Mutagen schema decomposition). Real-world signal is uneven — Authoria PERK records sample dominantly PerkEntryPointEffect; PerkAbility / PerkQuestEffect quantitatively rare but load-bearing for specific Bethesda patterns (standing-stone / Werewolf / Vampire perks).
  - Locks downstream: Phase 1 audit-confirms-then-extends Layer 1.P matrix rows; Phase 2 wires per-subclass coverage-smoke cells. **Escalation trigger:** Phase 1 escalates Q2 if audit shows >5 subclasses with substantially-diverging shapes OR any subclass is itself abstract (third-level polymorphism) — Pareto-defer becomes a real call rather than default-no.
Question: Does v2.9.3 ship every concrete `APerkEffect` subclass Phase 1 enumerates, or Pareto-defer obscure-tail subclasses?
Suggested options:
  A. Ship full subclass set Phase 1 enumerates — uniform factory; v2.9.x-extensible; no caller footgun. Phase 1 escalates if audit surfaces complexity cliff.
  B. Pareto subset — ship the common 3 (PerkEntryPointEffect, PerkAbility, PerkQuestEffect); defer obscure tail to v2.9.x.
  C. Phase-1-conditional — Aaron decides post-audit based on actual subclass count + shape divergence.
Phase 0 default: A — ship full set unless Phase 1 surfaces a complexity cliff. Uniform factory pattern handles arbitrary count; Pareto-defer creates a "supported / not-yet-wired" footgun.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q3 — Replace semantics for the Effects array on PERK
Context:
  - PLAN § D names the posture: replace-semantics (whole-array assignment clears source list + writes new entries — matches v2.8.0's Effects-list write across SPEL/ALCH/ENCH/SCRL/INGR; write-time consistency across the family is a UX invariant) vs merge-by-discriminator (e.g. merge by `EntryPoint` — would require per-EntryPoint identity logic; introduces a different invariant from v2.8.0).
  - Locks downstream: Layer 4 cell `4.dsl.05` sibling-preservation invariant; Layer 2 cell `2.04` empty-clear contract (`Effects: []` → count-0 list, mirroring v2.8.0 Test 29 / 1.E.07).
  - Sanity-confirm question. Replace is the established v2.8.0 family posture; flipping would re-architect both the new factory and the v2.8.0 carrier set's existing semantics.
Question: Does `set_fields: {Effects: [...]}` on PERK use replace-semantics (clear + write) matching v2.8.0, or merge-by-discriminator?
Suggested options:
  A. Replace — whole-array assignment clears source list + writes new. Matches v2.8.0 family invariant.
  B. Merge-by-discriminator — merge by EntryPoint or other identity; per-entry update + add + remove.
Phase 0 default: A — replace. Matches v2.8.0 family invariant; flipping would re-architect the existing carrier set.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q4 — v2.9.0 dispatcher composition for nested PerkConditions
Context:
  - PLAN § F names the contract: v2.9.0's per-Condition-function parameter dispatcher (`RouteParameterSlot` + `KnownParameterizedFunctions`) operates on `Condition` entries and is **agnostic** to where the condition lives (top-level `Conditions`, nested `Effect.Conditions` on v2.8.0 carriers, nested `PerkEntryPointEffect.PerkConditions[*].Conditions` on v2.9.3) — composition routes through `BuildCondition`'s foreach over `ce.Parameters`.
  - Phase 2 verifies via probe (write `set_fields: {Effects: [{type: "PerkEntryPointEffect", PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function: "HasPerk", parameters: {Perk: "Skyrim.esm:058200"}}]}], ...}]}` against synthetic PERK; round-trip via `CreateFromBinary` + `WriteToBinary`; confirm the dispatcher routed `parameters.Perk` through v2.9.0's `IFormLinkOrIndex<T>` branch unchanged). Phase 2 cell = `1.P.PerkEntryPointEffect.with_v290_params` (composition probe).
  - Sanity-confirm question. If Phase 2's probe shows a composition gap (e.g. `RouteParameterSlot` requires the condition to be at a specific depth or attached to a specific record type), escalate as a Phase 2 mid-session ask.
Question: Does v2.9.0's `RouteParameterSlot` + `KnownParameterizedFunctions` compose UNTOUCHED for nested `PerkEntryPointEffect.PerkConditions[*].Conditions[*].parameters` in v2.9.3?
Suggested options:
  A. Untouched composition — Phase 2 verifies via probe; no v2.9.0 dispatcher code change. Phase 0 default.
  B. Composition gap requires v2.9.0 dispatcher extension — Phase 2 surfaces if probe fails; conductor → Aaron lock at that time.
Phase 0 default: A — untouched composition; Phase 2 verifies via probe at implementation time.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q5 — Discriminator value canonical form
Context:
  - PLAN § B (rationale point 2 + Q5 reference at § Phase 0 step 4) names two forms: full Mutagen subclass name (e.g. `"PerkEntryPointEffect"` — matches v2.8.0's `function:` reflection-property-name convention; Mutagen-rename-safe within a major version; same string as the type's reflected `Name` property) vs short tag (e.g. `"entry_point"` / `"ability"` / `"quest_effect"` — caller-friendly but requires per-tag-to-Mutagen-name mapping table; breaks Mutagen-rename-safety if Mutagen renames an internal class without updating the mapping).
  - Locks downstream: factory reflection-lookup string (`Mutagen.Bethesda.Skyrim.{Type}` namespace lookup vs `MutagenSubclassMapping[shortTag]` indirect lookup); schema description wording; v2.9.x-extensibility (zero-mapping-table vs per-subclass-add mapping entry).
  - Aligns with Q1's option A: explicit `type:` field with full Mutagen subclass name is the v2.8.0 Condition `function:` shape — `function: "GetIsID"` is structurally identical to `type: "PerkEntryPointEffect"`.
Question: What canonical form does the `type:` discriminator value take?
Suggested options:
  A. Full Mutagen subclass name (e.g. `"PerkEntryPointEffect"`, `"PerkAbility"`, `"PerkQuestEffect"`) — matches v2.8.0 `function:` convention; reflection-lookup direct; Mutagen-rename-safe within major version.
  B. Short tag (e.g. `"entry_point"`, `"ability"`, `"quest_effect"`) — caller-friendly; requires per-tag mapping table; per-subclass-add maintenance.
Phase 0 default: A — full Mutagen subclass name. Matches v2.8.0 family convention; zero-maintenance for new subclasses.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q6 — QUST.Aliases / Stages / Objectives absorb-or-defer
Context:
  - PLAN § H names the trade-off: absorb-via-same-Branch-A-mechanism if Phase 1's audit shows the pattern fits cheaply (the abstract-list factory pattern works structurally) vs keep PERK.Effects-bounded (defer all three QUST sub-records — separate v2.9.x scoping). v2.8.0 KNOWN_ISSUES.md grouped them as one carry-over entry; v2.9.3 closes the PERK half; QUST half stays in carry-over.
  - Per-subclass field surface for QuestAlias is much broader (Faction/Cell FormLinks, package overrides, AI data, conditions); audit + per-subclass coverage-smoke would substantially expand Phase 1 + Phase 2 scope. No real-consumer signal for QUST sub-records yet — PERK has Authoria's modlist signal driving prioritization.
  - Locks downstream: Phase 1 audit scope (PERK-bounded vs PERK + QUST sub-records); Phase 2 implementation scope; release framing (PERK.Effects v2.9.3 vs PERK + QUST v2.9.3).
Question: Does v2.9.3 absorb QUST.Aliases / Stages / Objectives (or any subset) via the same Branch A mechanism, or stay PERK.Effects-bounded?
Suggested options:
  A. Defer all three QUST sub-records — keep v2.9.3 PERK.Effects-bounded; QUST is a separate v2.9.x scoping session.
  B. Absorb all three — extend Phase 1 audit + Phase 2 implementation to cover QuestAlias / QuestLogEntry / QuestObjective alongside PerkEffect.
  C. Absorb QuestAlias only (the highest-value of the three by record-volume) — partial absorb.
Phase 0 default: A — defer all three. Carry-over framing matches v2.8.0's "Effects-list = 5 records, defer others" discipline; no real-consumer signal for QUST sub-records; PERK.Effects volume justifies bounded release.
Default if no response: A.
```

```
CONDUCTOR ASK
Phase: 0
Topic: Q7 — PerkConditions nested-list shape (wrapper-object DSL vs flat)
Context:
  - PLAN § C names the trade-off: wrapper-object DSL (`PerkConditions: [{RunOnTabIndex: 1, Conditions: [...]}]` — matches Mutagen's actual `PerkCondition` LoquiObject shape per read-side render at `mo2_record_detail` against `Skyrim.esm:10FCFA`; concrete `PerkCondition` Activator-creates cleanly; nested `Conditions` route through Branch A's `typeof(Condition)` special case) vs flat (`PerkConditions: [{function, ..., RunOnTabIndex}]` — caller-friendlier but loses the wrapper struct + tab grouping; would require synthetic-tab-grouping logic in the factory).
  - Phase 1's audit confirms `PerkConditions` element type. Phase 0 default assumes concrete `PerkCondition` per the read-side render shape; if abstract (vanishingly unlikely per render but worth probing), Phase 1 escalates Q7.
  - Locks downstream: factory recursion (wrapper → PerkCondition Activator → SetPropertyByPath nested Conditions → Branch A `typeof(Condition)` route vs synthetic flat-to-tab conversion); Layer 1.P cell `1.P.PerkEntryPointEffect.with_perk_conditions` payload shape.
Question: What DSL shape does `PerkConditions` take?
Suggested options:
  A. Wrapper-object — `PerkConditions: [{RunOnTabIndex: 1, Conditions: [{function, operator, value, parameters}]}]`. Matches Mutagen's actual `PerkCondition` LoquiObject; Branch A composition no new mechanism.
  B. Flat — `PerkConditions: [{function, ..., RunOnTabIndex}]`. Caller-friendlier; requires synthetic-tab-grouping in factory; new mechanism.
Phase 0 default: A — wrapper-object. Matches Mutagen's actual LoquiObject; no new mechanism; reads naturally as the read-side render shape inverse.
Default if no response: A.
```

## Preconditions for Phase 1

Phase 1's responsibilities (per PLAN.md § Phase 1):

- `tools/race-probe/Program.cs` v2.9.3 P1 inventory section — reflect over `IAPerkEffectGetter`-implementing concrete classes in `Mutagen.Bethesda.Skyrim` 0.53.1; produce Activator constructibility table; per-subclass property dump with `[base]` / `[subclass-specific]` annotation; dump `PerkEntryPointType` enum's full value set (informational); confirm `PerkConditions` element type is concrete `PerkCondition` (or escalate Q7).
- `<plan>/APERK_EFFECTS_AUDIT.md` (NEW) — mirrors `<v2.8.0-plan>/EFFECTS_AUDIT.md` and `<v2.9.0-plan>/CONDITIONS_AUDIT.md` layout. Per-subclass categorization, anchor sanity-check, Pareto framing if Q2 escalation triggers.
- MATRIX.md updates per Phase 1 hand-back checklist (substitute placeholder subclass names + per-subclass anchor FormIDs + heterogeneous-subclass anchor confirmation).
- `PHASE_1_HANDOFF.md`.

**Phase 1 cannot begin until the Q1–Q7 design lock is in.** Per PLAN.md § J ("Design lock sign-off … Phase 1 doesn't begin until the lock is in"), the conductor relays Q1–Q7 to Aaron, gets locks, then writes Phase 1's kick-off prompt carrying the locked answers as authoritative for Phase 1's executor to transcribe. If Aaron flips any default, Phase 1's audit + scope adjusts accordingly (e.g. Q1 flip to option B/C re-architects the factory; Q2 flip to Pareto subset reduces Phase 1 + Phase 2 scope; Q6 flip to absorb QUST sub-records expands Phase 1 + Phase 2 scope substantially).

| Precondition | State |
|---|---|
| `tools/race-probe/Program.cs` editable + builds clean as-is | ✅ presumed (existing v2.9.0 + v2.9.1 + v2.9.2 P1 artifact; Phase 1's first step is to confirm with `cd tools/race-probe && dotnet build -c Release`) |
| MATRIX.md exists with Layer 1.P / 1.D / 2 / 3 / 4 / 5 scaffold + naming convention | ✅ landed in this commit |
| MATRIX.md § Phase fill-in checklists enumerate exact post-Phase-N edits | ✅ landed at MATRIX.md bottom (3 checklists: Phase 1 / Phase 2 / Phase 3 hand-back) |
| Conductor decisions inherited (slug=v2.9.3 locked Aaron 2026-04-28, single-mechanism scope, scope absorption posture stricter for v2.9.3+, single-commit deliverable for Phase 0 per § J) | ✅ recorded above § Locks + § Conductor decisions inherited |
| PLAN.md + CONDUCTOR_KICKOFF.md committed in this Phase 0 work commit and readable | ✅ |
| 7 design questions (Q1–Q7) awaiting Aaron lock | ✅ posted in § Conductor asks above in conductor's required format |
| v2.9.2 PHASE_2 + PHASE_4 + PHASE_5 handoffs available as reference for Phase 2 wrapper-passthrough discipline + Phase 5 ship cadence | ✅ (`dev/plans/v2.9.2_read_side_efficiency/`) |
| v2.8.0 EFFECTS_AUDIT.md available as reference for APERK_EFFECTS_AUDIT.md format + Branch A factory pattern reference | ✅ (`<v2.8.0-plan>/EFFECTS_AUDIT.md` + `<v2.8.0-plan>/PHASE_1_HANDOFF.md`) |
| v2.9.0 CONDITIONS_AUDIT.md available as reference for inventory-probe pattern (per-shape categorization, padding-pattern filter, anchor sanity-check, Pareto framing) | ✅ (`<v2.9.0-plan>/CONDITIONS_AUDIT.md`) |

**Phase 1 cannot open** until Aaron locks all 7 design questions via the conductor relay. The locks are inputs to Phase 1's kick-off prompt (which restates them as authoritative for Phase 1's executor to transcribe). If any lock is undecided when Phase 1 needs to open, the conductor either holds Phase 1 or spawns it with the Phase-0-default and a "lock-pending" annotation.

## Files of interest for Phase 1

| Path | Why |
|---|---|
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/PLAN.md` § Phase 1 | Authoritative steps + § Conductor decisions for Phase 1 |
| `Claude_MO2/dev/plans/v2.9.3_perk_effects/MATRIX.md` § Phase fill-in checklist (Phase 1 hand-back) | Exact rows Phase 1 lands post-audit |
| `Claude_MO2/dev/plans/v2.8.0_verification/EFFECTS_AUDIT.md` | Canonical Branch A + `BuildConditionFromJson` shape — structural template v2.9.3 follows |
| `Claude_MO2/dev/plans/v2.8.0_verification/PHASE_1_HANDOFF.md` | Reference shape for Phase 1's audit format |
| `Claude_MO2/dev/plans/v2.9.0_X_condition_parameters/CONDITIONS_AUDIT.md` (or current `v2.9.X_condition_parameters/`) | Canonical inventory-probe pattern (per-shape categorization, padding-pattern filter, anchor sanity-check, Pareto framing) — APERK_EFFECTS_AUDIT.md mirrors this layout |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/MATRIX.md` § Phase fill-in checklist | v2.9.2 reference for the format Phase 1 hand-back follows |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PHASE_1_HANDOFF.md` | Reference shape for Phase 1's perf-and-shape probe section + record-shape sweep table format (most recent precedent) |
| `Claude_MO2/tools/race-probe/Program.cs` | Probe extension target (append after existing v2.9.0 / v2.9.1 / v2.9.2 P1 sections); Phase 1 reads existing sections to understand the pattern for both inventory-probe and reflection-sweep blocks |
| `Claude_MO2/tools/mutagen-bridge/PatchEngine.cs` (existing `ConvertJsonElementToListItem` at line 1441 + `BuildConditionFromJson` at ~line 2331) | Phase 1 reads to understand the Branch A factory pattern that Phase 2's `BuildPerkEffectFromJson` mirrors; informs Phase 1's audit predicate (what subclass surface needs to round-trip cleanly through Activator + SetPropertyByPath) |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` top entry (v2.9.2) + `Claude_MO2/KNOWN_ISSUES.md` § "Patching write surface — current limitations" (PERK.Effects entry) | Standard dev-startup orientation per `feedback_dev_startup.md` memory; v2.9.2's CHANGELOG documents recent context; KNOWN_ISSUES § Patching documents the carry-over inventory v2.9.3 closes the PERK half of |
| `<workspace>/Live Reported Bugs/` | Standard dev-startup orientation per `feedback_dev_startup.md` memory |

## Acceptance — Phase 0

Per CONDUCTOR_KICKOFF prompt § Acceptance criteria + PLAN.md § Phase 0 § Acceptance:

- `MATRIX.md` exists with five-layer scaffold (six layers: 1.P / 1.D / 2 / 3 / 4 / 5) + cell-naming convention mirroring v2.9.2's MATRIX shape. Per-subclass rows are placeholders awaiting Phase 1's `APerkEffect` audit.
- Layer 3 scenarios named (3.1 Requiem perk rebalance mandatory; 3.2 multi-effect PERK optional, conditional on Phase 1 audit + Phase 3 precondition) with use-case descriptions; live-FormID picks deferred to Phase 3.
- `git diff origin/main^...HEAD` (after work commit) shows: PLAN.md (NEW), MATRIX.md (NEW), CONDUCTOR_KICKOFF.md (NEW), PHASE_0_HANDOFF.md (NEW). No production code touched. No version bump.
- Locked version slug `v2.9.3` recorded in handoff (Aaron 2026-04-28; § Locks section above).
- § Conductor asks populated with the 7 design questions in the conductor's required format.
- Single work commit + single hash-record commit, both pushed (per PLAN § J single-commit deliverable lock + Conventions § "double-commit cadence per phase").
- Handoff under 400 lines.
