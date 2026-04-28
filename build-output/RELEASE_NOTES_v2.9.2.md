# Claude MO2 v2.9.2 — Read-side efficiency for `mo2_record_detail`

**Real consumer signal collapsed: 168-record patching from ~600+ tool calls to ~1 batched call.**

This release adds three composable optional parameters to `mo2_record_detail` that multiply, not add, on AI-driven read-heavy workflows. Defaults preserve v2.9.1 single-record / full-payload / no-expansion behavior bit-identically — existing callers see no change.

## Headline

A real Authoria tester reported AI-driven patching workflows hitting token-cost ceilings on read-heavy tasks: a 168-RACE patching pass costs ~600+ tool calls today, dominated by per-record `mo2_record_detail` round-trips (each pays the ~889 ms subprocess startup) plus second-tier FormLink-chase calls when the patcher needs to expand a record's linked spells/factions/keywords. v2.9.2's three composable parameters target each cost component:

- **Subprocess startup amortization** — one bridge call reads N records.
- **Per-record payload reduction** — projection narrows the response to only requested fields.
- **Second-tier round-trip elimination** — single-level FormLink expansion inlines linked record detail in one walk.

Combined, the 168-RACE scenario collapses to **~1 batched call** + ~50–200 kB response (vs ~600 calls + ~3–5 MB today).

## What's new

### Three composable read-side parameters on `mo2_record_detail`

- **`formids: [...]`** — batch read mode. List of FormIDs read in one bridge subprocess invocation. Per-record success/error envelope (matches existing `plugin_names` precedent). Tested up to N=200 single-plugin batches and N×M=1000 cross-product (cliff-free at 11.7 s wall-clock; 0.23% of timeout budget).
- **`fields: [...]`** — projection. Dot-segmented paths; the walker auto-traverses lists and dicts mid-path (`Voices.Male` reads as the male side of the gendered struct; `Factions.Faction` reads as the Faction sub-property of each Factions entry). Shrinks RACE full-detail (~8.7 kB / 62 top-level fields per Phase 1 measurement) by ~80% on a 3–5 path subset.
- **`expand_links: [...]`** — single-level FormLink expansion. Inlines the linked record's detail at named positions in a wrapper `{formid, EditorID, expanded: {...}}`. Single-level only — links inside expanded records render as plain FormID strings (no recursion, no cycle detection). 5.11× wall-clock speedup on a 3-spell race; scales with link-count.

All three composable on a single call and orthogonal to `resolve_links` (which annotates FormID strings throughout the response, including inside expanded records).

### Q6 — `formids` × `plugin_names` cross-product (consistency-patch use case)

When both `formids: [...]` and `plugin_names: [...]` are supplied, the response is the N×M cross-product (each FormID × each plugin = one cell with its own success/error envelope). This is the canonical "build a consistency patch across a large modlist" pattern — read each FormID's state in each plugin's view to compare and merge. Architecturally distinct from `formids` alone (multi-record-batch) or `plugin_names` alone (multi-plugin-diff for one record). Tested cliff-free up to N×M=1000.

### Cross-master FormLink expansion fix (Phase 4)

**Bug B5** surfaced live during release verification: `expand_links` on a record whose winning plugin is a mod ESP (e.g. `Authoria - Requiem Master Patch.esp`) whose FormLinks point back into Skyrim.esm-originated records returned the missing-master error envelope, even though the FormID was clearly resolvable via the load-order index. **Fixed via Option B**: the Python wrapper now passes the full enabled load-order plugin path list as `available_plugins` whenever `expand_links` is supplied; the bridge's `ExpandFormLinkValue` walker lazy-hot-loads the matching plugin on the first miss for that originating-master filename, caches it, and retries the lookup. Lazy: zero cost when in-master resolves (the common case); ~one Mutagen plugin load per cross-master target master per batch. Architecturally-correct foundation for future override-aware expansion (a v2.9.x candidate where the lookup-logic swap becomes the only remaining work).

## Verification

- **Coverage-smoke 425/425 PASS** (382 v2.9.0 + 18 v2.9.1 + 25 v2.9.2; 6 SKIPs all documented v2.9.x candidates). New `1.P.expand.crossmaster` cell exercises the Phase 4 fix; `4.dsl.06` synthetic missing-master fixture verifies the Q2 uniform null-safety wrapper-form contract.
- **Race-probe** ALL PASS — 16 v2.9.0 + 8 v2.9.1 + 14 v2.9.2 P2 + 16 v2.9.2 P4 cross-master probes.
- **End-to-end MCP→bridge smoke** 6/6 PASS — wrapper passthrough integrity confirmed; the v2.9.1 P4 lesson discipline closed.
- **Live verification on Authoria**: 8-RACE batch with all axes composed (projection + expansion + resolve_links) at SHIP_SHA. DraugrRace winning in Requiem.esp expands `Skyrim.esm:02431D` cleanly. Tri-master case: DragonRace expands SPELs from Skyrim.esm + Requiem.esp + Fire and Blood.esp all resolved. Q6 cross-product (2 formids × 2 plugins → 4 cells with cross-master expansion within each cell) end-to-end.

## SHAs

- `mutagen-bridge.exe`: `e99cf223c3912ae4f2fb6ead7f9908381ee645ec5ec1502b95707d2978352f00`
- `mutagen-bridge.dll`: `904ffeb2ad8394904bf3fad3f021143bb87b73045bbf24dc28f808c70562fd75`
- `claude-mo2-setup-v2.9.2.exe` (10,630,559 bytes): `c82c902c655ae38492173babaf353005df0d40dbbfd5092731f59f15d354780a`

Single byte-identical anchor across smoke matrix, installer bundle, and live install.

## Backward compatibility

All v2.9.1 callers see bit-identical responses to v2.9.1. The new parameters are optional; their absence routes through the existing single-record / `plugin_name` / `plugin_names` code paths unchanged.

## v2.9.x roadmap (read-surface column)

The xEdit-clarity vision (Claude as both accurate editor AND xEdit-clarity viewer) seeded these v2.9.x candidates during v2.9.2 scoping:

- **Reverse-link search** — given a FormID, find all records that reference it. Highest viewing-assistant impact.
- **Override-aware FormLink expansion** — `ExpandFormLinkValue` returns load-order winner via `LinkCache.TryResolve` instead of originating-master version. Foundation laid in this release.
- **`MaxDepth` MCP-configurable** — currently hardcoded to 6; expose if a real consumer hits the limit on projected/expanded reads.
- **Cross-call result caching** — bridge subprocess is per-call; persistent state for navigation workflows.

Real-consumer signal drives sequencing.
