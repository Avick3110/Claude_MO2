# Phase 4 Handoff — `condition_target` passthrough plumbing fix

**Phase:** 4
**Status:** Complete
**Date:** 2026-04-27
**Session length:** ~30 min
**Commits made:** `<work-hash>` (work) + this commit (hash-record)
**Live install synced:** No (deferred to conductor per kickoff)

## Working version slug

**`v2.9.1`** — no version bump in Phase 4 (Phase 2 already bumped to `2.9.1`).

## Conductor decisions inherited

All Phase 4 kickoff locks honored verbatim:

| # | Decision | Lock |
|---|---|---|
| Scope | Single-item — add `"condition_target"` to `passthrough_keys` tuple | No bridge code changes; no other absorptions |
| Placement | Alphabetic-ish placement near `add_conditions`/`remove_conditions` | `tools_patching.py:440` (line directly between `add_conditions, remove_conditions` and `attach_scripts`) |
| Defensive verify | Bridge rebuild + race-probe + coverage-smoke | All three required even though fix doesn't traverse those code paths |
| Live re-sync | Conductor handles after push | Phase 4 ends at push |
| Phase 3 resume | Conductor wakes phase-3-executor | Phase 4 doesn't coordinate Phase 3 |

## What was done

### Fix delta — `mo2_mcp/tools_patching.py:428-443`

Single-line addition to the explicit `passthrough_keys` tuple inside the `set_fields` op-handler block (the tuple that marshals record-level fields from the MCP layer's `rec_spec` dict into bridge stdin):

```python
        # Pass through all modification parameters
        passthrough_keys = (
            "set_fields", "set_flags", "clear_flags",
            "add_keywords", "remove_keywords",
            "add_spells", "remove_spells",
            "add_perks", "remove_perks",
            "add_packages", "remove_packages",
            "add_factions", "remove_factions",
            "add_inventory", "remove_inventory",
            "add_outfit_items", "remove_outfit_items",
            "add_form_list_entries", "remove_form_list_entries",
            "add_items",
            "add_conditions", "remove_conditions",
            "condition_target",                          # <-- added (Phase 4)
            "attach_scripts",
            "set_enchantment", "clear_enchantment",
        )
```

Placement is between `add_conditions, remove_conditions` and `attach_scripts` per the kickoff's "alphabetic placement near `add_conditions`/`remove_conditions` for readability" directive — the natural conceptual neighbor for the new field.

### Why this gap existed

- Phase 2's `2d6c717` work commit added `condition_target` to:
  - The C# `RecordOperation.ConditionTarget` field (`Models.cs:430`)
  - The `ResolveConditionListProperty` helper + dispatch wiring (`PatchEngine.cs`)
  - The Python tool argument schema description (`tools_patching.py:~104` — operator-level schema entry)
- Phase 2 missed adding it to the **explicit `passthrough_keys` whitelist** at `tools_patching.py:428-442` — the tuple that marshals record-level fields from `rec_spec` into the dict forwarded to bridge stdin.
- Result: every `condition_target` value the user supplied was silently dropped by the Python wrapper before it reached bridge stdin → the bridge's `RecordOperation.ConditionTarget` was always null → Q3 missing-target error fired legitimately on every QUST `add_conditions`/`remove_conditions` call.

### Why race-probe + coverage-smoke didn't catch this

Both Phase 2 test harnesses invoke `mutagen-bridge.exe` directly with hand-built JSON, bypassing `tools_patching.py`:

- `tools/race-probe/Program.cs` — builds JSON in-process, writes to bridge stdin via `Process.Start`. No MCP layer.
- `tools/coverage-smoke/Program.cs` — same pattern. Hand-built JSON → bridge subprocess.

Both correctly tested the bridge's C# dispatcher. Neither exercised the Python wrapper's `passthrough_keys` filter. Phase 3 (live workflow via `mo2_create_patch` → MCP server → `tools_patching.py` → bridge) is the first end-to-end MCP→bridge exercise on the v2.9.1 surface, and preflight caught the gap on the first call.

### Defensive verification

Per kickoff's "rebuild bridge + re-run race-probe + re-run coverage-smoke" defensive run — all should still pass identically since the fix doesn't traverse those code paths.

| Check | Result |
|---|---|
| Bridge rebuild (`dotnet build -c Release`) | 0 warnings, 0 errors. Built `mutagen-bridge -> bin/Release/net8.0/mutagen-bridge.dll`. Time elapsed 00:00:02.95 |
| Race-probe (`dotnet run -c Release` in `tools/race-probe`) | All sections PASS. Final tail confirms: `=== v2.9 P2A probes: ALL PASS ===` ... `=== v2.9.1 P2 quest-condition probes: ALL PASS ===` ... `=== probe complete ===`. 8 v2.9.1 P2 probes (Tests 1-8) all PASS, including positive add×{dialog,event}, remove byfunc×{dialog,event}, Q3 error, §C#3 bad-value, Q4 PERK reject, Q5 case-insensitivity. Exit 0. |
| Coverage-smoke (`dotnet run -c Release` in `tools/coverage-smoke`) | `=== smoke complete: ALL PASS ===` + exit 0. **400/400 PASS** (382 v2.9.0 + 1 v2.9.0-flipped Test 157 + 18 v2.9.1). 0 FAIL. 6 SKIP-with-reason (pre-existing, documented in KNOWN_ISSUES.md). |

No deltas vs Phase 2's defensive runs. Fix is orthogonal to the bridge subprocess test paths, as expected.

### CHANGELOG entry — `mo2_mcp/CHANGELOG.md`

Added new `### Fixed — bridge` subsection under the existing `## v2.9.1 — TBD` block, after the `### Documentation` section (the natural conceptual close — "we documented the new param, then fixed the plumbing for it"). Entry text matches kickoff template verbatim. ~17 lines.

## Verification performed

### State checks (session start)

| Check | Result |
|---|---|
| `git log -3 --oneline` top hash | `7964678 [v2.9.1 P2] Handoff: record commit hash 2d6c717` ✅ |
| `git status` | clean ✅ |
| `mo2_ping` | `version: "2.9.1"` ✅ (live's C# bridge label is right; Python wrapper still has the broken `passthrough_keys` per kickoff context) |

### Post-fix bridge build

`mutagen-bridge -> bin/Release/net8.0/mutagen-bridge.dll`. 0 warnings, 0 errors. (Sanity check — Phase 4 didn't touch C#, so no expected delta.)

### Post-fix race-probe run

Tail:

```
=== v2.9 P2A probes: ALL PASS ===
=== v2.9 P2B probes: ALL PASS ===
=== v2.9 P2C probes: ALL PASS ===
=== v2.9 P2D probes: ALL PASS ===
=== v2.9 P4-INFO probes: ALL PASS ===
=== v2.9.1 P1 multi-condition sweep: ALL PASS ===
=== v2.9.1 P2 quest-condition probes: ALL PASS ===
=== probe complete ===
```

`p2QustFailures = 0`. `totalFailures = 0`. Exit 0. Identical to Phase 2's tail.

### Post-fix coverage-smoke run

`=== smoke complete: ALL PASS ===` + exit 0. 400/400 PASS. 0 FAIL. 6 SKIP-with-reason (pre-existing). Identical to Phase 2's result.

## Bugs surfaced (Phase 4 in-phase)

None. Single-item scope; clean fix; defensive verification all green.

## Deviations from plan

None. Kickoff scope-locked + executed as written.

## Known issues / open questions

### v2.9.x candidate — Python-layer test infrastructure

The fact that `condition_target` slipped through Phase 2's full verification gauntlet — race-probe (8 probes all anchored on the carrier we care about) + coverage-smoke (400 cells including 18 dedicated v2.9.1 cells) + clean bridge build — without surfacing the gap is structurally interesting:

- The bridge subprocess **was** correctly receiving `condition_target` in race-probe + coverage-smoke (because both write JSON directly).
- The **MCP→bridge marshaling layer** (`tools_patching.py`'s `passthrough_keys` whitelist) is **not currently exercised by any test in the v2.9.1 test infrastructure**.
- Any future addition of a record-level operator field (sibling to `set_fields`, `add_keywords`, `condition_target`, etc.) will hit the same gap: schema entry + bridge wiring will both look correct, race-probe + coverage-smoke will both pass, and the field will be silently dropped by the wrapper.

Phase 3's live workflow exercise is the canonical post-Phase-2 catch for this class of gap, but Phase 3 is human/conductor-driven, slow, and gated on a live install sync. A lightweight Python-layer test harness (e.g. `tests/test_passthrough_keys.py`) that asserts every operator-level schema field has a matching `passthrough_keys` entry would catch this class statically.

**Recommendation:** Document as a v2.9.x candidate, not a v2.9.1 blocker. Per kickoff: "would expand significantly — no Python test framework in `mo2_mcp/` today." Standing up `pytest` in `mo2_mcp/` is a meaningful infrastructure ask (test discovery, CI integration if any, fixture patterns for the schema-vs-passthrough invariant). Out of scope for v2.9.1 ship; worth flagging for the v2.9.x roadmap.

A simpler intermediate fix worth considering: a runtime assertion in `tools_patching.py` that cross-checks the operator-level schema's keys against `passthrough_keys` at module load time, surfacing a `RuntimeError` on mismatch rather than silently dropping. Single-file, no test framework needed. Out of scope here, but cheap to add in a future v2.9.x patch session.

## Conductor asks

```
CONDUCTOR ASK
Phase: 4
Topic: (none — single-item phase, clean fix, all defensive checks green)
Context:
  - Phase 4 deliverables 1-7 all complete per kickoff acceptance criteria.
  - condition_target now in passthrough_keys (line 440).
  - Bridge clean. Race-probe ALL PASS. Coverage-smoke 400/400 PASS.
  - CHANGELOG ### Fixed — bridge subsection added under ## v2.9.1 — TBD.
  - Phase 3 unblocked: phase-3-executor's preflight halt now resolvable
    (after live re-sync delivers the new tools_patching.py to the live
    install).
Question: None.
Default if no response: Conductor proceeds with live re-sync + Phase 3 resume.
```

## Preconditions for next phases

### Phase 3 resume (phase-3-executor)

| Precondition | State |
|---|---|
| `condition_target` reaches bridge stdin from MCP layer | ✅ Phase 4 |
| Live install synced with new `tools_patching.py` | ⏳ **NOT MET** — conductor handles per kickoff |
| Phase 3 anchor + scenario plan | ✅ phase-3-executor holds these from preflight halt |
| `mo2_ping` returns `2.9.1` | ✅ already true (Phase 2's bump) |

### Phase 5 ship (deferred)

| Precondition | State |
|---|---|
| All v2.9.1 fixes landed | ✅ Phase 2 + Phase 4 |
| Phase 3 Layer 3 scenarios PASS | ⏳ Phase 3 resume |
| Bridge SHA preservation chain (build via direct ISCC, not `-BuildInstaller`) | Phase 5 concern — not Phase 4's responsibility |

## Files of interest for next phases

| Path | Why |
|---|---|
| `mo2_mcp/tools_patching.py:440` | The Phase 4 fix line — `condition_target` in `passthrough_keys`. Live re-sync delivers this to the live install. |
| `dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_4_HANDOFF.md` (this file) | Phase 4 reference for Phase 3 resume + Phase 5 ship |
| `dev/plans/v2.9.1_quest_condition_disambiguation/PHASE_2_HANDOFF.md` | Phase 2 reference (sentinels + bridge dispatch logic — unchanged in Phase 4) |
| `mo2_mcp/CHANGELOG.md` § `## v2.9.1 — TBD` § `### Fixed — bridge` | Public record of Phase 4 fix |

## Acceptance — Phase 4 (per kickoff)

- ✅ `"condition_target",` added to `passthrough_keys` tuple at `tools_patching.py:440` (alphabetic placement near `add_conditions`/`remove_conditions`).
- ✅ Bridge build clean (0 warnings, 0 errors — defensive sanity check, no expected delta since C# unchanged).
- ✅ Race-probe ALL PASS — preserves Phase 2 result (8 v2.9.1 P2 probes + all v2.9.0 P2A/P2B/P2C/P2D + P4-INFO + P1 sweep).
- ✅ Coverage-smoke 400/400 PASS — preserves Phase 2 result.
- ✅ CHANGELOG `### Fixed — bridge` entry under `## v2.9.1 — TBD`.
- ✅ Handoff under 400 lines (this file).

Phase 4 done. Phase 3 unblocked pending conductor's live re-sync.
