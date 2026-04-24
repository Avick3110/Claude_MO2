# Phase 2 Handoff — Python tool_paths.json config layer (priority-0 PapyrusCompiler override + additive scripts-dir)

**Phase:** 2
**Status:** Complete
**Date:** 2026-04-24
**Session length:** ~1.5h
**Commits made:** 1 — see `git log --oneline main | grep "[v2.7 P2]"`
**Live install synced:** Yes — Python-only (`-SyncPython`) to `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp`. MO2 restarted; plugin loaded clean at v2.7.0.

## What was done

One new module + two surgical edits to `tools_papyrus.py`. No changes to `__init__.py` settings, no cross-module ripple, no MCP tool interface changes.

- **New file: `mo2_mcp/tool_paths.py`** — JSON config loader exactly per PLAN.md § Phase 2 spec. Exposes `get(tool)` and `reload()`, with process-lifetime cache. Graceful-degrades to `{}` (returning None from every `get()`) on: absent file, unreadable file, non-object JSON root, `schema_version != 1`, JSON syntax error. When a key is set but the configured path doesn't exist on disk, `get()` returns None and logs a warning. No `mobase` / `PyQt6` imports — stays testable outside MO2 via stdlib `logging`; MO2's log panel captures stdlib warnings.
- **`mo2_mcp/tools_papyrus.py:37`** — added `from . import tool_paths` (grouped with the other relative imports, after `mobase` and before `.config`). Per architect mid-phase guidance: import lives at this module's top, NOT at `__init__.py` — avoids startup weight + circular-import risk.
- **`mo2_mcp/tools_papyrus.py:44-99`** — `_find_papyrus_compiler()` gains a priority 0 check: calls `tool_paths.get("papyrus_compiler")`, wraps the return in `Path(...)`, and returns it if `.is_file()`. On miss (key unset, path invalid), falls through to the v2.6.1 5-candidate chain (2 in-plugin + 3 `%USERPROFILE%`) unchanged. Docstring updated to describe the new priority-0 entry.
- **`mo2_mcp/tools_papyrus.py:285-333`** — `_collect_header_dirs()` gains (a) a one-line fix to the `if not dirs:` fallback branch so it updates the `seen` set with the normalised lowercase candidate, and (b) a new additive-append block before `return dirs` that calls `tool_paths.get("papyrus_scripts_dir")`, normalises with `os.path.normpath`, and appends to `dirs` only if both not-already-in-`seen` AND `os.path.isdir(...)`. The existing `seen` set is the sole dedupe mechanism — a config-pointed dir overlapping a VFS-found dir never double-counts in the `-import=` chain. Docstring added documenting the additive behaviour.
- **No other files touched.** `__init__.py`, `tools_archive.py`, `tools_nif.py`, `config.py`, and the nine other `mo2_mcp/*.py` modules are byte-identical to post-P1.

## Verification performed

**Unit-style smoke test (6 PLAN scenarios, 19 assertions) — all pass.**

Script: `dev/plans/v2.7.0_installer_overhaul/scratch/smoke_tool_paths.py` (gitignored under `dev/`; durable artifact for P5 re-verification if desired). Imports `tool_paths.py` as a standalone module via `importlib.util`, monkey-patches `_config_path()` to point at a per-test tmp file, and cycles through six scenarios with stdlib-logging capture:

| # | Scenario | Expected | Observed |
|---|---|---|---|
| T1 | No JSON file | Both keys → None; no warning | PASS |
| T2 | schema_version=1, both populated with valid disk paths | Both keys → configured path; no warning | PASS |
| T3 | schema_version=99 | Both keys → None; warning mentions `schema_version` | PASS (warning: `tool_paths.json schema_version=99 (expected 1); ignoring config — Python cannot safely read future/unknown schema.`) |
| T4 | schema_version=1, configured path does not exist | Both keys → None (one from null, one from disk-missing); warning mentions "path does not exist" | PASS |
| T5 | Malformed JSON (syntax error) | No exception; both keys → None; warning mentions "read failed" | PASS (warning includes JSON decoder message) |
| T6 | reload() escape hatch | Cache stays stale without reload(); fresh after reload() | PASS |

Total: **19/19 checks passed**. Full output in session transcript.

**Syntax validation on live install copies.**
- `python -m py_compile tool_paths.py tools_papyrus.py` — both compile clean.
- `ast.parse(...)` on both — clean.
- AST walk confirms `from . import tool_paths` at line 37 and exactly 2 `tool_paths.get()` call sites in `tools_papyrus.py` (one per consumer function). No stray references.

**MO2-integration smoke test (PLAN.md § Phase 2 Step 5).**
- Sync command ran: `powershell -ExecutionPolicy Bypass -File build/build-release.ps1 -SyncPython -MO2PluginDir "E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp"` → 21 .py files synced, no errors. Bridge built but not touched (sync script reports `[sync] -SyncLive not set. Bridge built at ... mutagen-bridge.exe`).
- Aaron fully restarted MO2 (close + reopen). Claude Code's MCP connection survived the restart — no Claude Code restart needed to continue verification.
- `mo2_ping` → `{"status": "ok", "server": "MO2 MCP Server", "version": "2.7.0", "mo2_version": "2.5.2.0", ...}`. Plugin loaded clean; version bump from P1 visible; no Python traceback.
- `mo2_compile_script` with trivial source (`Scriptname ClaudeMo2P2Smoke` + `int Function Add(int a, int b) return a+b EndFunction`, no base-Skyrim imports) → `{"success": true, ...}`. Compiled `.pex` landed at `E:/Skyrim Modding/Authoria - Requiem Reforged/mods/Claude Output/Scripts/ClaudeMo2P2Smoke.pex`. `headers_used` shows ~150 VFS-aggregated contributor dirs from Aaron's modlist — no `tool_paths.json` present, so priority-0 missed, fallback chain (priority 1) hit at the in-plugin `papyrus-compiler/` dir, additive-append contributed nothing (config-null). **v2.6.1-equivalence confirmed when JSON is absent** — the regression-free baseline the PLAN explicitly required.

**Grep audit.** `grep -rn "PapyrusCompiler\|papyrus-compiler\|Scripts\.zip" mo2_mcp/*.py` — matches confined to `tools_papyrus.py` (pre-existing + new docstring strings) and `tool_paths.py` (module docstring only). No new tool-lookup sites introduced outside the planned surfaces.

## Deviations from plan

1. **`_collect_header_dirs()` fallback branch now writes to `seen`.** The v2.6.1 fallback (`if not dirs: candidate = organizer.resolvePath(...)`) appended to `dirs` but did NOT update `seen` — defensible when `seen` was only consumed by the preceding findFiles loop. With Phase 2's additive-append consuming `seen` after the fallback, leaving `seen` empty there breaks dedupe when a user's configured `papyrus_scripts_dir` happens to resolve to the same folder as `resolvePath`. Fix: one line — `seen.add(os.path.normpath(candidate).lower())` — added in the fallback before the `dirs.append(candidate)`. No change to what `dirs` contains in the fallback path; the contribution is still the raw `candidate` return. Addresses the architect's mid-phase guidance: *"A config-pointed dir that overlaps a VFS-found dir should NOT double-count in the -import= chain."* Cleanest way to honour the spec's "use the existing seen set" constraint.

2. **No other deviations.** No new Python files beyond `tool_paths.py`. No new imports beyond the one `from . import tool_paths` line. `__init__.py` plugin settings untouched. Priority-0 override + additive append match PLAN.md § Phase 2's spec + P0 Q5's locked description byte-for-byte.

## Known issues / open questions

1. **`reload()` escape hatch — users editing JSON while MO2 is running must restart MO2.** `_cache` is a module-level mutable populated on first `get()`; there is no file-watch, no mtime-check, no hook into `organizer.onRefreshed`. Users who hand-edit `tool_paths.json` after plugin load see no change until either (a) MO2 is fully restarted, or (b) some caller invokes `tool_paths.reload()`. v2.7.0 exposes `reload()` only as a Python module function — no MCP tool surface, no plugin setting hook. **Practical answer for end users: restart MO2.** Phase 6 KNOWN_ISSUES/README copy should mention this alongside the existing "after editing any .py, delete __pycache__/ AND restart MO2" note — same operational pattern, consistent guidance. A future v2.8 could add an `mo2_reload_tool_paths` MCP tool or wire `reload()` into `onRefreshed`; out of scope here.

2. **`schema_version` mismatch → treat as empty + log warning.** Any JSON whose `schema_version` is not the integer `1` — including unset, strings, floats, future `2`, random `99` — degrades to the same "not configured" state as a missing JSON file. A warning line goes to stdlib `logging` (visible in MO2's log panel), using the exact wording from PLAN spec: `tool_paths.json schema_version=<val!r> (expected 1); ignoring config — Python cannot safely read future/unknown schema.` **Verified end-to-end in T3 of the unit-style smoke test.** This is the intended behaviour per P0 Q7 — a future v2.8 that ships `schema_version: 2` will not accidentally load under a stale v2.7 Python; users will see the warning and the plugin falls through to its v2.6.1 fallback chain.

3. **Disk-missing configured paths → None + warning per `get()` call.** When a JSON key is populated but the target file/dir doesn't exist on disk (stale config, user moved CK, typo), `get()` returns None and logs `tool_paths.json configured <tool>=<value!r> but path does not exist`. The disk check runs on every `get()` call — not just cache miss — because the cache stores the raw JSON values, not the post-disk-check result. In practice this means one warning per `_find_papyrus_compiler()` / `_collect_header_dirs()` call site per tool invocation when a stale path is configured (e.g. one warning per `mo2_compile_script` call). **Tolerable volume** for the expected misconfiguration frequency; also gives users a clear signal that the config is broken without silent fallback. **Verified in T4 of the unit-style smoke test.**

4. **Test artifact left in live install.** The trivial `ClaudeMo2P2Smoke.pex` compiled during the MO2-integration smoke sits at `E:/Skyrim Modding/Authoria - Requiem Reforged/mods/Claude Output/Scripts/ClaudeMo2P2Smoke.pex`. Safe to delete by hand — no plugin references it. Phase 5's test matrix may do analogous compiles; Aaron can clean up at leisure.

5. **No code defects found. No open questions for Phase 3.**

## Preconditions for Phase 3

Phase 3 builds the previous-install detector wizard page in `installer/claude-mo2-installer.iss`. It reads existing `tool_paths.json` on the target and inspects plugin-side binary surfaces.

- ✅ **Python side reads JSON at process start.** `tool_paths.py` is wired and regression-free; the consumer contract (schema_version=1, field names `papyrus_compiler`/`papyrus_scripts_dir`, null = not-configured) is fixed. Phase 3's Pascal-side `ReadToolPathsJson` (per P0 Q2 locked signature) is a separate implementation but must produce/accept JSON that `tool_paths.py` round-trips cleanly. Phase 4's writer (per P0 Q3 locked signature) must emit exactly the shape `tool_paths.py` accepts.
- ✅ **Schema locked** per P0 Q7. No additions. `tool_paths.py` hard-codes `_SCHEMA_VERSION = 1`; any future schema bump must edit that constant AND every consumer.
- ✅ **`_find_papyrus_compiler()` priority 0 + `_collect_header_dirs()` additive append wired and behaviour-complete.** Phase 3 can assume the Python consumer is done — it only needs to detect prior state + record Keep/Change/Skip globals for Phase 4.
- ✅ **`__init__.py` plugin settings untouched.** `mutagen-bridge-path` + `spooky-cli-path` continue to work orthogonally. Phase 3's detector does not interact with MO2 plugin settings.
- ✅ **MO2-integration smoke passes at v2.7.0.** Plugin loads clean, `mo2_ping` → 2.7.0, trivial `mo2_compile_script` → success. P1's version bump is visible end-to-end.
- ✅ **No Live Reported Bugs.** `<workspace>/Live Reported Bugs/` contains only the `archive/` folder from prior triage; no new entries at Phase 2 start or end.

All Phase 3 preconditions met.

## Files of interest for next phase

Phase 3 implements the detector wizard page in `installer/claude-mo2-installer.iss`. Files the P3 implementer should read:

- `dev/plans/v2.7.0_installer_overhaul/PLAN.md` § "Phase 3 — Previous-install detector wizard page" — step-by-step spec + 5-surface detection matrix.
- `dev/plans/v2.7.0_installer_overhaul/PHASE_0_HANDOFF.md` § Q2 (JSON read signature) + § Q4 (custom-page widgets) + § Q7 (schema wording) — locked Pascal shapes P3 implements against.
- `mo2_mcp/tool_paths.py` (new from P2) — authoritative schema reference for P3's `ReadToolPathsJson` Pascal scanner: field names, `schema_version: 1` integer, null semantics, tolerance for trailing commas / whitespace / backslash-escaped Windows paths.
- `installer/claude-mo2-installer.iss` — current state post-P1 (`UsePreviousAppDir=no`, `.NET` hard-block, `#define AppVersion "2.7.0"`). Phase 3 adds new `[Code]` functions + page registration via `CreateCustomPage(wpSelectDir, ...)`.
- `mo2_mcp/tools_papyrus.py:44-99` — `_find_papyrus_compiler()` post-P2 state. P3 is read-only here; the detector's binary-surface check for PapyrusCompiler inspects the same two paths as priorities 1-2 (`<target>\plugins\mo2_mcp\tools\spooky-cli\tools\papyrus-compiler\[Original Compiler\]PapyrusCompiler.exe`).
- `mo2_mcp/tools_papyrus.py:285-333` — `_collect_header_dirs()` post-P2 state. P3 need not inspect it; only P4's JSON writer needs to know the Python consumer exists.
- `dev/plans/v2.7.0_installer_overhaul/scratch/smoke_tool_paths.py` — durable unit-style smoke test. If P5 or a future maintenance session wants to re-verify the Python config layer in isolation, run `python dev/plans/v2.7.0_installer_overhaul/scratch/smoke_tool_paths.py` from repo root. No MO2 required.
