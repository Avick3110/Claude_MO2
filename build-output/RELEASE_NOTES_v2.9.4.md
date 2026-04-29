# Claude MO2 v2.9.4 — Auto-stop deny-list (xEdit-clarity capability)

**Real consumer signal: Claude reading records while the user has xEdit open viewing the same data — workflow blocked pre-v2.9.4 because the v1.0.3 auto-stop-on-launch fired on every executable launch including xEdit. This release ships the read half of the xEdit-clarity vision; the v3.0 daemon ships the latency-amortization half later.**

This release narrows MO2's auto-stop-on-launch behavior to a regex deny-list that exempts the xEdit family of executables, so the MCP server stays alive during xEdit sessions. Claude can issue read queries (`mo2_record_detail`, `mo2_query_records`, `mo2_conflict_chain`, etc.) concurrent with an active xEdit window. First concrete shipped instance of the read-surface-equally-with-write pillar of the xEdit-clarity vision.

## Headline

Concurrent MCP query during a live xEdit session — the workflow that was impossible at v2.9.3 and earlier:

| Step | Action | Outcome |
|---|---|---|
| 1 | User launches `SSEEdit64.exe` from MO2's executables list | MO2 log: `keeping server alive across launch of E:/...SSEEdit64.exe (exempt)` (v2.9.4 qInfo from `_AUTOSTOP_EXEMPT_PATTERN` early-return) |
| 2 | Claude calls `mo2_record_detail` on a record the user is also viewing in xEdit | Returns immediately — server stayed alive |
| 3 | Record-index lazy build fires during xEdit's USVFS-setup window (highest-risk concurrent-load scenario, the original v1.0.3 hang race) | 9.9 s build, 2.9M records, 427k conflicts — completes cleanly |
| 4 | User exits xEdit | `_was_running_before_launch` was zeroed at exempt early-return → no spurious restart action |

Pre-v2.9.4: step 1 fires `stopping server before launch` and the MCP server stops for the duration of xEdit. Steps 2-4 don't happen.

## What's new

### `_AUTOSTOP_EXEMPT_PATTERN` regex deny-list

`mo2_mcp/__init__.py` adds a regex deny-list (lines 96-103) covering the xEdit family across 14 game-edition variants + the user-renamed `xEdit.exe` wildcard:

`sseedit · tes5edit · tes5vredit · enderalseedit · enderaledit · fo4edit · fo4vredit · fo76edit · fnvedit · fo3edit · tes4edit · tes4redit · tes3edit · sf1edit · xedit`

Tail `[\w \-]*\.exe$` tolerates version/build suffixes (`SSEEdit64.exe`, `xEdit64.exe`, `TES5Edit32.exe`, etc.). Match is case-insensitive against `os.path.basename(app_path)`.

### `_on_about_to_run` early-return on exempt match

Lines 247-260 of `__init__.py` — exempt-pattern matches early-return with `keeping server alive across launch of {app_path} (exempt)` qInfo and zero `_was_running_before_launch` (so `_on_finished_run`'s existing flag-guard takes no restart action when the exempt exe exits). Non-exempt path (game launches, Synthesis, BodySlide, etc.) is structurally unchanged from v2.9.3 — same `stopping server before launch of {app_path}` qInfo, same `self._server.stop()` + None-out, same `_was_running_before_launch = True`.

### Synthesis intentionally NOT exempted

Synthesis is a batch patcher with no concurrent-read use case (user invokes it end-to-end, not interactively). Mutagen overlay shape is similar to game launches. Easy 1-line regex extension if real consumer signal surfaces.

### Visibility-lag UX caveat (v3.0 daemon territory)

xEdit reads load order at startup, so MCP-driven plugin writes mid-xEdit-session are invisible to xEdit until reload. Write-time detection-and-warn is a v3.0 daemon candidate (the daemon's persistent state makes detection cleaner architecturally). v2.9.4 ships the deny-list as a pure capability landing; the visibility-lag rough edge is documented in `KNOWN_ISSUES.md` § Environmental quirks.

## Verification

- **Coverage-smoke 449/455** (449 PASS + 6 SKIP) at SHIP SHA — all 6 SKIPs are pre-v2.9.4 carry-overs from v2.9.3 final state. Zero regressions across the v2.7.x / v2.8.x / v2.9.0 / v2.9.1 / v2.9.2 / v2.9.3 cumulative test surface.
- **Empirical viability validated** (2026-04-29 evening dev-build SHA): 13/13 MCP queries succeeded across a 6-minute live xEdit session. MO2 log smoking gun on `keeping server alive ... (exempt)` qInfo. Critical sub-test: record-index lazy build fired during xEdit's USVFS-setup window (9.9 s, 2.9M records, 427k conflicts) — the highest-risk concurrent-load scenario for the original v1.0.3 race. Completed cleanly, no deadlock.
- **Pre-tag re-test at post-build live SHA** (Q6 lock): re-ran the exempt qInfo + 3-MCP-call sweep on the v2.9.4 build. Smoking-gun line landed at `__init__.py:249`, `mo2_ping` returned `version: "2.9.4"` during xEdit's lifetime, `mo2_record_index_status` and `mo2_query_records` both succeeded mid-xEdit-session, post-exit `mo2_ping` confirmed no spurious restart.
- **Carryover-by-analogy correction validated**: the v1.0.3 auto-stop-on-launch was added to prevent an MO2 hang caused by HTTP server thread × VFS setup contention during executable launches. The original failure mode was empirically observed only on **game launches**; xEdit / BodySlide / etc. were added by analogy without independent verification. v2.9.4's empirical test confirms the hang race is specific to game-engine launches, not to xEdit's incremental open-records-on-demand pattern.

## SHAs

- `mutagen-bridge.dll`: `cc6e069e3cf15f9a1289c14e03c0b550c11b1db7d8ab485649ff570e5cad2bda`
- `mutagen-bridge.exe`: `5ab43f9c83a98e981ab9558797828bec0c7547d7c0f5e553b6bce415c7b62821`
- `claude-mo2-setup-v2.9.4.exe` (10,645,349 bytes): `17f05ec591a284df7591e028884e93160849a6e3807ed4d1086bf798b15c5a03`

3-way byte-identical anchor across publish output, build-output stage, and live install for the bridge. v2.9.4 has zero bridge code changes; SHIP SHAs differ from v2.9.3's only by .NET build determinism factors (re-published per Q7 lock for SHA-chain hygiene).

## Backward compatibility

All v2.9.3 callers see bit-identical responses. The deny-list change is purely additive in `_on_about_to_run` (early-return on exempt match); the non-exempt path is structurally unchanged. No write-surface, read-surface, or MCP-tool-shape changes.

## What's still deferred

- **xEdit write-time detection-and-warn** — v3.0 daemon territory (Q2 lock). Once Claude can write plugins while xEdit is open, the MCP-driven patch.esp is invisible to xEdit until reload. Daemon-side detection (running-xEdit polling at write-time) + warning emission is specced; defers to v3.0 where the daemon's persistent state makes detection architecturally cleaner.
- **Synthesis exemption** — easy 1-line regex extension if consumer signal surfaces. Default no.
- **v3.0 daemon mode** — separate workstream; v2.9.4 ships in the per-call subprocess architecture. Daemon's per-call latency amortization (the 9-13 s subprocess cost during the empirical test → sub-microsecond after daemon mode) is the next-tier value-prop story, not a v2.9.4 deliverable.
- **All v2.6.0–v2.9.3 deferreds** — read-surface candidates (reverse-link search, override-aware FormLink expansion, MaxDepth MCP-configurable, cross-call result caching), QUST.Aliases / Stages / Objectives, etc.

See `KNOWN_ISSUES.md` for the full carry-over inventory.
