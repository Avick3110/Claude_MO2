# Phase 4 Handoff — Cross-master FormLink expansion fix (Option B) + 4.dsl.06 absorption + read-surface carry-over docs

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-28
**Session length:** ~2 h
**Commits made:** see § Commits below
**Live install synced:** No (Phase 5 territory per kickoff)

## Working version slug

**`v2.9.2`** — no version bump in Phase 4 (Phase 2 already bumped to `2.9.2`).

## Conductor decisions inherited

All Phase 4 kickoff locks honored verbatim:

| # | Decision | Lock |
|---|---|---|
| Fix angle | Option B — wrapper passes full load-order plugin list; bridge hot-loads on demand | Lazy; backward-compatible additive surface; foundation for future override-aware expansion |
| 4.dsl.06 absorption | Synthetic missing-master fixture (in-memory plugin via Mutagen with FormLink to non-existent master) | Closes Phase 1's test-coverage gap in same release; ~30 min cost |
| Carry-over docs | 4 v2.9.x read-surface candidates documented in KNOWN_ISSUES.md | Reverse-link search, override-aware expansion, MaxDepth exposure, cross-call caching |
| Out of scope | NO version bump, NO live sync, NO override-aware expansion (foundation only), NO new tool surface | Per kickoff "Out of scope for Phase 4" list |

## What was done

### Bridge code

- **`tools/mutagen-bridge/Models.cs`** — added optional `AvailablePlugins` (`List<string>?`, JSON name `available_plugins`) on both `ReadRequest` and `ReadBatchRequest`. Doc comments name the v2.9.2 P4 lazy hot-load contract (zero cost when in-master, one Mutagen overlay load when miss path fires) and the future override-aware expansion seam.
- **`tools/mutagen-bridge/RecordReader.cs:ExpandFormLinkValue`** — extended with the lazy hot-load fallback. Three changes:
  - New optional `availablePlugins: List<string>?` parameter on the helper signature (default null preserves v2.9.2 P2 behavior bit-identically — when caller omits the list, the original missing-master error envelope surfaces).
  - On in-cache scan miss, walk `availablePlugins` for a path whose leaf filename matches the linked FormID's originating master (case-insensitive). Skip empties + non-existent files + already-cached paths. Load matched plugin via `SkyrimMod.CreateFromBinaryOverlay`, register in `modCache` keyed by the path string, retry the FormKey lookup against the freshly-loaded mod. First match wins (mirrors the in-cache scan's first-match ordering).
  - Plugin-load failures (corrupt header, etc.) silently skip and let the missing-master error fall through — Phase 1's load-error pattern is preserved.
- **Threading:** `availablePlugins` plumbed through `Read`, `ReadOne`, `RenderValueProjected`, `ExpandFormLinkValue`. Threading is opt-in: the parameter defaults to null on every signature, so no existing call site that doesn't pass it changes behavior.

### Python wrapper

- **`mo2_mcp/tools_records.py`** — three changes:
  - New `_build_available_plugins(idx)` helper (mirrors `build_bridge_load_order_context` line ~1733's `_load_order` walk, but flattened to just the disk-path list — no master-style metadata needed for read-side hot-load). Skips orphans (plugins listed but with missing or absent disk path) silently.
  - **3 forwarding sites** per Phase 2's "explicit forwarding" pattern (no `passthrough_keys` whitelist on this file):
    1. `_handle_record_detail` single-formid path (~line 1040)
    2. `_handle_record_detail` `plugin_names` batch path (~line 1115)
    3. `_handle_formids_batch` cross-product path (~line 1316)
  - Each site forwards `available_plugins` **only when `expand_links` is supplied**. Rationale: Phase 1 measured ~3000 enabled plugins on Authoria → ~180 KB JSON payload per request; pay-only-when-needed avoids the cost on non-expansion calls. The bridge's hot-load logic is also gated to `ExpandFormLinkValue`, so the param is wasted payload otherwise.

### Race-probe synthetic-fixture probes

- **`tools/race-probe/Program.cs`** — new `=== v2.9.2 P4 — Cross-master FormLink expansion (Option B fix) ===` section after Phase 2's read-side functional probes. Builds an in-memory two-plugin fixture (master with one SPEL, override with one RACE whose ActorEffect references the master's SPEL) via Mutagen's `SkyrimMod` + `WriteToBinary`. Three probes:
  - **Probe 1 (pre-fix posture):** `read_record` against the override WITHOUT `available_plugins`. Asserts the missing-master error envelope still surfaces (regression test for backward compat — null parameter preserves v2.9.2 P2 behavior).
  - **Probe 2 (post-fix):** same request WITH `available_plugins` carrying the master's path. Asserts the wrapper resolves: no error key, EditorID == "P4MasterSpell", expanded is an object dict carrying the master's full SPEL detail.
  - **Probe 3 (4.dsl.06 absorption):** orphan override pointing at a SPEL whose originating master is `GhostMaster.esm` (a name never written to disk); `available_plugins` supplied but does NOT include `GhostMaster.esm`. Asserts the Q2 uniform null-safety wrapper-form contract: formid populated (mentions `GhostMaster.esm`), EditorID null, expanded null, error string set.
  - All 3 probes PASS. Synthetic fixture is built per-section run in `%TEMP%/race-probe-p4-crossmaster/` and cleaned up at section end.
- Total race-probe failure-counter aggregation includes `p4ReadSideFailures`; `=== probe complete ===` final line preserved.

### Coverage-smoke regression cells

- **`tools/coverage-smoke/Program.cs`** — two cell changes inside the existing v2.9.2 P2 cell block:
  - **Test 424 [4.dsl.06]** — was `Skip("4.dsl.06", "...")` SKIP-with-reason. Replaced with a `RunCell(424, ...)` call exercising a synthetic orphan-override fixture (RACE.ActorEffect references SPEL whose master `GhostMaster.esm` isn't on disk; `available_plugins` supplied but doesn't include the ghost master). Asserts the Q2 uniform null-safety contract end-to-end. PASS.
  - **Test 425 [1.P.expand.crossmaster]** — new cross-master positive cell. Synthetic two-plugin fixture (CSP4Master + CSP4Override) written to `%TEMP%/coverage-smoke-p4-crossmaster-positive/`; bridge call against the override with `available_plugins=[master, override]`. Asserts no error key, wrapper EditorID == `CSP4MasterSpell`, expanded is an object dict, expanded.EditorID matches the master record. PASS.
- Total coverage-smoke = **425/425 PASS** (424 from Phase 2 + 1 new positive cell; 4.dsl.06 SKIP→PASS within the existing 424-count). All v2.9.0 + v2.9.1 + v2.9.2 P2 cells stay green. Pre-existing 7 SKIPs reduced to 6 (4.dsl.06 dropped from the SKIP list).

### End-to-end MCP→bridge smoke (Phase 4)

- **`<workspace>/scratch/v2.9.2-phase-4-smoke.py`** — extends Phase 2's six-path harness. Each path now also asserts `available_plugins` forwarding parsimony via a `_run_bridge_read` spy that captures the bridge_request dict before forwarding to the real implementation:
  - Paths (a)/(b)/(c)/(e) — no `expand_links` ⇒ `available_plugins` MUST NOT appear in the bridge request (parsimony / payload-overhead protection).
  - Paths (d)/(f) — `expand_links` set ⇒ `available_plugins` MUST appear in the bridge request, MUST be a non-empty list, MUST contain the Skyrim.esm path.
- All 6 paths PASS. Confirms the wrapper's three forwarding sites all wire correctly + the gated-on-expand_links discipline holds.

### Documentation

- **`mo2_mcp/CHANGELOG.md`** — appended `### Fixed — bridge (Phase 4)` + `### Documentation (Phase 4)` subsections under the existing `## v2.9.2 — TBD` heading. Documents Option B fix shape (Models extension + RecordReader hot-load + 3-site wrapper passthrough), 4.dsl.06 absorption, race-probe + coverage-smoke + smoke-harness verification, and the read-surface carry-over docs.
- **`KNOWN_ISSUES.md`** — two changes:
  - Updated the "Covered as of v2.9.2" entry: removed the "missing-master synthetic test fixture deferred" tail line; added a new "Cross-master FormLink expansion (v2.9.2 Phase 4)" entry citing the Option B fix shape.
  - Added a new "Read-surface candidates (v2.9.x)" subsection (placed between the "Covered as of v2.9.2" and "Covered as of v2.9.1" sections) documenting the four candidates surfaced during Phase 3+4 conductor discussion: reverse-link search, override-aware expansion, MaxDepth MCP-configurable, cross-call result caching. Each entry frames the impact + the work needed + the v2.9.x trigger condition.

## Verification performed

### State checks (session start)

| Check | Result |
|---|---|
| `git log -1 --oneline origin/main` top hash | `4cd057f [v2.9.2 P3] Halt — cross-master FormLink expansion bug confirmed live` ✅ matches kickoff |
| `git status` | clean working tree ✅ |
| Bridge build (pre-Phase-4) | 0 warnings, 0 errors ✅ |
| Race-probe build (pre-Phase-4) | 0 warnings, 0 errors ✅ |

### Bridge build (post-Phase-4 extensions)

```
mutagen-bridge -> bin/Release/net8.0/mutagen-bridge.dll
0 Warning(s), 0 Error(s)
```

### Race-probe run (post-Phase-4 functional-probe extension)

Tail confirms all sections preserved + new Phase 4 section landed:

```
=== v2.9 P2A probes: ALL PASS ===
=== v2.9 P2B probes: ALL PASS ===
=== v2.9 P2C probes: ALL PASS ===
=== v2.9 P2D probes: ALL PASS ===
=== v2.9 P4-INFO probes: ALL PASS ===
=== v2.9.1 P1 multi-condition sweep: ALL PASS ===
=== v2.9.1 P2 quest-condition probes: ALL PASS ===
=== v2.9.2 P1 read-side perf + shape sweep: ALL PASS ===
=== v2.9.2 P2 read-side functional probes: ALL PASS ===
=== v2.9.2 P4 cross-master FormLink expansion fix: ALL PASS ===
=== probe complete ===
```

Probe 1 (pre-fix posture): 5 asserts PASS (missing-master error envelope preserved when `available_plugins` absent).
Probe 2 (post-fix): 6 asserts PASS (cross-master expansion resolves; expanded.EditorID matches master).
Probe 3 (4.dsl.06 absorption): 5 asserts PASS (Q2 uniform null-safety contract end-to-end).

### Coverage-smoke run (post-Phase-4 cells)

Tail confirms 4.dsl.06 flipped to PASS + new 1.P.expand.crossmaster PASS:

```
// ── Test 423 (4.dsl.05) — fields=[DeathItem] on NPC_ - shape-preserving null ──
  [4.dsl.05] PASS

// ── Test 424 (4.dsl.06) — missing-master FormLink → uniform null-safety wrapper ──
  [4.dsl.06] PASS

// ── Test 425 (1.P.expand.crossmaster) — cross-master FormLink expansion via available_plugins (Option B) ──
  [1.P.expand.crossmaster] PASS

=== 6 SKIP(s) ===
  1.r.40 — OTFT: ...
  1.r.47 — SPEL: ...
  1.D.04 — Mutagen 0.53.1 CellBinaryOverlay ...
  4.esl.01 — Layer 4 ESL master interaction ...
  1.P.Unknown.MGEF — Mutagen reclassifies UnknownConditionData ...
  1.P.GetVATSValueUnknown.MGEF — Mutagen 0.53.1 schema gap ...

=== smoke complete: ALL PASS ===
```

Total: **425/425 PASS** (424 from Phase 2 + 1 new cross-master positive cell). 4.dsl.06 SKIP→PASS dropped from the SKIPs list (was 7 SKIPs in Phase 2 baseline, now 6). All v2.9.0 + v2.9.1 + v2.9.2 P2 cells stay green; zero regressions.

### End-to-end MCP→bridge smoke (Phase 4)

```
=== (a) plain v2.9.1-shape formid (regression) ===
  [PASS] available_plugins NOT forwarded (expand_links absent)
=== (b) formids batch alone ===
  [PASS] available_plugins NOT forwarded (expand_links absent)
=== (c) fields projection alone ===
  [PASS] available_plugins NOT forwarded (expand_links absent — fields-only)
=== (d) expand_links expansion alone ===
  [PASS] available_plugins forwarded (expand_links present)
  [PASS] available_plugins is a non-empty list
  [PASS] available_plugins contains Skyrim.esm path
=== (e) formids × plugin_names cross-product (Q6) ===
  [PASS] available_plugins NOT forwarded (expand_links absent)
=== (f) all four composed with resolve_links: true ===
  [PASS] available_plugins forwarded (expand_links present in cross-product)
  [PASS] available_plugins is a non-empty list

=== 6/6 paths PASS ===
```

Wrapper passthrough integrity confirmed end-to-end. The new gated-forwarding discipline (`available_plugins` flows iff `expand_links` is set) holds at all three call sites.

## Bugs surfaced

None. Phase 4's scope was the B5 fix landing; no new bugs surfaced during implementation or verification.

## Deviations from plan

1. **Pre-fix probe runs in the same race-probe section as post-fix** — the kickoff item 1 deliverable noted "Optional — Phase 3's live reproduction is documented." The synthetic fixture works for both pre-fix posture (no `available_plugins`) and post-fix (with `available_plugins`) by parameterizing the request, so I included both in the same Probe 1 + Probe 2 pair rather than separating into a separate "pre-fix only" run. This makes the regression test self-documenting (the same fixture exercises both halves of the contract).
2. **End-to-end smoke harness does NOT build a synthetic two-plugin fixture in Python** — building Mutagen plugins from Python is heavyweight (no PyPI Mutagen.Bethesda.Skyrim binding). Instead the smoke harness verifies the **wrapper passthrough** (the new gap per v2.9.1 P4 lesson) by spy-capturing the bridge_request dict and asserting `available_plugins` is forwarded iff `expand_links` is set. The cross-master end-to-end resolution is verified separately in race-probe (synthetic fixture via Mutagen-direct C#) + coverage-smoke (same approach). Per the v2.9.1 P4 discipline, both surfaces matter: race-probe confirms the bridge bridge handles it; smoke confirms the wrapper feeds the bridge. Phase 4's coverage is complete via the trio.

## Known issues / open questions

None new. The four v2.9.x read-surface candidates documented in KNOWN_ISSUES.md (reverse-link search, override-aware expansion, MaxDepth exposure, cross-call caching) are the carry-over open items by design.

## Conductor asks

```
CONDUCTOR ASK
Phase: 4
Topic: Phase 4 default-auto-accept (no Aaron-decision items surfaced)
Context:
  - All kickoff deliverables landed: B5 fix (Option B), 4.dsl.06 absorption, read-surface carry-over docs.
  - Bridge clean (0/0); race-probe ALL PASS (incl. new P4 section, 16 asserts); coverage-smoke 425/425 PASS; end-to-end smoke 6/6 PASS.
  - No bugs surfaced. No scope creep. No new operator surface added.
  - Proposing: conductor accepts default and proceeds to Phase 5 kickoff write-up.
Question: any objection to default-auto-accept on this work, or is there a Phase 5 sequencing concern that needs surfacing?
Suggested options:
  A — Accept; proceed to Phase 5 (re-run + ship cadence).
Default if no response: A.
```

## Preconditions for Phase 5

| Precondition | State |
|---|---|
| Phase 4 fix landed + tested (bridge + wrapper + smoke) | ✅ this handoff |
| Coverage-smoke 425/425 PASS | ✅ |
| Race-probe ALL PASS incl. new P4 section | ✅ |
| End-to-end MCP→bridge smoke 6/6 PASS | ✅ |
| `KNOWN_ISSUES.md` updated (B5 covered + 4 v2.9.x read-surface candidates) | ✅ |
| `CHANGELOG.md` Phase 4 entry under `## v2.9.2 — TBD` | ✅ |
| Bridge SHA captured for SHA-preservation chain | ⏳ Phase 5 invokes `dotnet publish` (different SHA from Release dll); the canonical ship SHA is produced there per PLAN § Phase 5 step 4 |
| Live install at v2.9.2 (Phase 2 sync state) | ✅ unchanged since Phase 2 — Phase 5 re-syncs after producing the publish SHA |
| Layer 3 workflow re-run scoped | ⏳ Phase 5 re-runs Phase 3 scenarios (3.1 168-RACE + 3.2 NPC_.Factions + 3.3 Q6 cross-product) against the post-P4 bridge to confirm the live cross-master fix |

## Files of interest for next phase

| Path | Why |
|---|---|
| `Claude_MO2/tools/mutagen-bridge/RecordReader.cs:ExpandFormLinkValue` | Phase 5 re-runs Phase 3's RACE.ActorEffect cross-master scenarios against the live install; this is the fix site whose live behavior is being verified. |
| `Claude_MO2/mo2_mcp/tools_records.py:_build_available_plugins` + 3 forwarding sites | Wrapper-side Phase 5 sanity check (live load order has ~3000+ plugins → ~180 KB JSON payload per call; verify subprocess transport doesn't truncate). |
| `Claude_MO2/mo2_mcp/CHANGELOG.md` `## v2.9.2 — TBD` heading | Phase 5 inserts the ship date here after the GitHub release. |
| `Claude_MO2/dev/plans/v2.9.2_read_side_efficiency/PHASE_3_HANDOFF.md` § Bug B5 reproduction | Phase 5 re-runs the same Authoria reproducer (Skyrim.esm:000D53 RACE.ActorEffect via mo2_record_detail) to confirm post-fix resolution at v2.9.2 ship SHA. |
| `<workspace>/scratch/v2.9.2-phase-4-smoke.py` | Phase 5 re-runs as part of the live sanity 3-path check (per PLAN § Phase 5 step 7 (c)). |
| `Claude_MO2/tools/mutagen-bridge/Models.cs:ReadRequest.AvailablePlugins` + `ReadBatchRequest.AvailablePlugins` | The new request-side schema additions; live Phase 5 sanity confirms the JSON wire format survives the live MCP→bridge transport. |

## Commits

Two-commit cadence per kickoff:

| Commit | Subject |
|---|---|
| Work | `[v2.9.2 P4] Cross-master FormLink expansion fix (Option B) + 4.dsl.06 absorption + v2.9.x carry-over docs` |
| Hash-record | `[v2.9.2 P4] Handoff: record commit hash <work-hash>` |

Both pushed.

## Acceptance — Phase 4 (per kickoff)

- ✅ Bridge clean (0 warnings, 0 errors).
- ✅ Pre-fix probe captured B5 reproduction; post-fix probe shows cross-master expansion resolves.
- ✅ 4.dsl.06 cell flipped from SKIP-with-reason to PASS via synthetic missing-master fixture.
- ✅ New cross-master positive cell `1.P.expand.crossmaster` added to coverage-smoke; PASS.
- ✅ Coverage-smoke total 425/425 PASS (424 from Phase 2 + 1 new; 4.dsl.06 flips from SKIP to PASS within the existing count).
- ✅ Race-probe preserves all v2.9.0/v2.9.1/v2.9.2-P1/v2.9.2-P2 sections + new Phase 4 cross-master probes; all PASS.
- ✅ End-to-end MCP→bridge smoke 6/6 PASS post-Phase-4 changes.
- ✅ CHANGELOG.md Phase 4 entry under `## v2.9.2 — TBD` documents fix + 4.dsl.06 absorption + carry-over candidates.
- ✅ KNOWN_ISSUES.md updated: cross-master expansion in "covered as of v2.9.2"; 4 read-surface v2.9.x candidates documented.
- ✅ PHASE_4_HANDOFF.md (this file) under 400 lines.
- ✅ Work commit + hash-record commit, both pushed.
