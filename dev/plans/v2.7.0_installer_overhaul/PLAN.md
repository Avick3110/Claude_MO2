# v2.7.0 — Installer Overhaul Plan

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-23; revised 2026-04-24 after Option C architecture lock + code-level audit.
**Baseline:** v2.6.1 (shipped 2026-04-24). The Papyrus discovery dead-path bug that predated this plan was fixed in v2.6.1 as an independent hotfix and is out of scope here.
**Target version:** v2.7.0
**Sessions estimated:** 7 (one per phase).

---

## 📁 Path conventions (RESOLVE BEFORE ANY FILESYSTEM COMMAND)

This plan uses two placeholders in prose. Resolve them to these absolute paths, no ambiguity:

| Placeholder | Absolute path |
|---|---|
| `<workspace>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\` |
| `<repo>` | `C:\Users\compl\Documents\Stuff for Calude\Claude_MO2_project\Claude_MO2\` |

`<repo>` is the git repo (public at github.com/Avick3110/Claude_MO2). `<workspace>` is `<repo>`'s parent, which also contains `Live Reported Bugs/`, `research/`, `archive/`, and the Authoria addon folder — none of which are part of the git repo.

When generating bash commands, always wrap these paths in quotes — both contain spaces (`Stuff for Calude`).

---

## ⚡ Session-start ritual (READ THIS FIRST EVERY SESSION)

You're a fresh Claude Code session opening this plan. **Before touching anything**, do this in order:

1. **Identify your phase.** Look in this directory:
   ```
   Claude_MO2/dev/plans/v2.7.0_installer_overhaul/
   ```
   Find the highest-numbered file matching `PHASE_*_HANDOFF.md`. **Your phase is one greater than that.** If no handoffs exist yet, you are **Phase 0**. If `PHASE_6_HANDOFF.md` exists, **the overhaul is complete** — point the user at it and stop; the plan is done.

2. **Read the previous handoff** (if any) in full. It tells you what was done, what was deviated from, and any preconditions for your phase. **Trust the handoff over this plan when they conflict** — the plan is the original intent; the handoff is the actual state.

3. **Read your phase section in this file** (scroll to "Phase N — ..." below). It tells you the goal, the files to touch, the steps, and what to write in your own handoff.

4. **Run the standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` (top entry — should show v2.6.1 or later)
   - `Claude_MO2/KNOWN_ISSUES.md`
   - **Skip** the session-summaries / handoffs sweep — this plan is your roadmap.
   - Check `<workspace>/Live Reported Bugs/` root for anything new. Should be empty at plan start.

5. **Confirm with the user** which phase you've identified yourself as and any deviations you've noticed from the plan. Wait for go-ahead before making changes.

6. **At the end of your phase**, write `PHASE_N_HANDOFF.md` in this directory, following the template at the bottom of this file. Then tell the user the handoff is written and the next session can begin.

**Do not execute multiple phases in one session.** Each phase is its own work unit. If you finish early, summarise and stop — don't roll into the next phase.

---

## 📋 Background — why this plan exists

v2.6.0 shipped 2026-04-23 and v2.6.1 shipped 2026-04-24. Post-v2.6.0 planning surfaced three issues that v2.7.0 addresses:

**Defects in the v2.6.x installer:**

1. **Path-persistence bug.** `UsePreviousAppDir=yes` in `claude-mo2-installer.iss` causes the installer to silently default the MO2 target folder to the path used in any previous run. Users with multiple MO2 instances (portable installs, separate modlists) can land the plugin into the wrong instance if they don't manually correct the path at the Browse step.

2. **.NET 8 Runtime is only a soft-block.** `InitializeSetup()`'s `MB_YESNOCANCEL` MsgBox has an `IDNO` fall-through that allows install to continue without .NET 8. Tools that depend on the bridge then fail at runtime with a confusing error later when the user first invokes a patch or record-detail call. Inconsistent with the installer's own "required" framing.

**Feature request — configurable tool paths:**

v2.6.x requires users to manually drop BSArch.exe, nif-tool.exe, PapyrusCompiler.exe into fixed subdirectories under `<plugin>\tools\spooky-cli\tools\<name>\`. Users often miss the README stubs that explain this. Users also have no way to point at existing installations — they must copy binaries into the plugin dir. And the Papyrus base-script source directory (required for `mo2_compile_script` on scripts that reference `Actor`, `Quest`, etc.) has no config surface at all; users rely on VFS-overlaid script mods whose aggregation is implicit.

v2.7.0 adds an installer-driven configuration for four tool surfaces, with the model for each surface chosen per the invocation topology — see "Architecture: Option C" below.

## 🏗️ Architecture — Option C (locked)

The four user-provided tools are NOT all invoked the same way. Any uniform "JSON config" model would either contradict Spooky CLI's fixed-path tool lookup (for BSArch/nif-tool) or force a multi-phase rewrite of our Spooky-wrapped BSA/NIF tools. Option C splits by the real invocation topology:

| Tool | How configured in v2.7.0 | Why |
|---|---|---|
| **BSArch.exe** | Installer picker → **copied** into `<plugin>/tools/spooky-cli/tools/bsarch/bsarch.exe` | Spooky CLI discovers it at a fixed path; we can't redirect its internal tool lookup |
| **nif-tool.exe** | Installer picker → **copied** into `<plugin>/tools/spooky-cli/tools/nif-tool/nif-tool.exe` | Same as BSArch |
| **PapyrusCompiler.exe** | Installer picker → **copied** into `<plugin>/tools/spooky-cli/tools/papyrus-compiler/` (default) OR **JSON-referenced** via `tool_paths.json` (checkbox opt-in) | Our Python invokes it directly via `_find_papyrus_compiler()`, so runtime config-path reference is viable. Users benefit from "point at existing CK install" over "copy CK's binary into plugin dir." |
| **Papyrus Scripts sources** (dir) | JSON config in `tool_paths.json`, **additive** to the existing VFS `findFiles` aggregation | Papyrus imports span multiple contributing mods; `_collect_header_dirs()` must aggregate, not replace. A config-pointed dir appends to the VFS-derived list. |

**User-facing refresh/update rules (land in `KNOWN_ISSUES.md` in Phase 6):**

- Refreshing BSArch or nif-tool after an xEdit/Spooky update: re-run the v2.7.0 installer with the new binary.
- Refreshing PapyrusCompiler after a CK update: re-run the installer OR edit `tool_paths.json` directly (if the JSON-reference checkbox was used).
- Adding/changing Papyrus Scripts dir: edit `tool_paths.json` any time; restart MO2 to pick up.

**Scope locks (agreed before plan creation):**

- **JSON schema (2 keys only):**
  ```json
  {
    "schema_version": 1,
    "papyrus_compiler": "<absolute path to PapyrusCompiler.exe>" | null,
    "papyrus_scripts_dir": "<absolute path to extracted Scripts.zip contents>" | null
  }
  ```
  Null/missing key = "not installed / not configured"; Python surfaces that state to Claude at invocation time so Claude can prompt the user.
- **JSON location:** `<plugin>/mo2_mcp/tool_paths.json`.
- **No auto-detection heuristics.** No Steam registry lookup, no `libraryfolders.vdf` parse, no drive scanning. Every path comes from user browse.
- **No Scripts.zip extraction inside the installer.** Scripts.zip location varies too much. Installer picker surfaces a notice recommending the user extract manually, and accepts the resulting folder path.
- **Previous-install detection reads all 5 tool surfaces.** The detector page (Phase 3) checks existing binaries at plugin-side paths (BSArch/nif-tool/PapyrusCompiler) AND reads existing `tool_paths.json` (papyrus_compiler JSON override + papyrus_scripts_dir). Per-surface Keep/Change/Skip.
- **.NET hard-block via TaskDialog.** If .NET 8 Runtime is missing, a TaskDialog with clickable URL and a single custom Cancel button aborts the install. No continue-anyway branch. (Phase 0 confirms exact Inno TaskDialog API or documents MsgBox fallback.)
- **Existing `mutagen-bridge-path` and `spooky-cli-path` MO2 plugin settings stay untouched.** JSON config is additive for the four new surfaces; does not retire existing setting-based overrides.
- **Version: v2.7.0.** Bumped in Phase 1 (per the locked-version-rule: rebuild requires bump, even for dev-only builds).

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Scope lock + audit | Concrete Inno API answers (TaskDialog, JSON parse/write, mixed-widget custom page); JSON schema locked; Python tool-lookup sites confirmed against current v2.6.1 source | None |
| 1 | Installer policy fixes + version bump to 2.7.0 | `UsePreviousAppDir=no` + .NET hard-block via TaskDialog + bump `config.py`/`.iss` to 2.7.0 | Phase 0 |
| 2 | Python config layer | `tool_paths.py` + JSON schema + `_find_papyrus_compiler()` JSON override (priority 0) + `_collect_header_dirs()` additive extension | Phase 1 |
| 3 | Previous-install detector wizard page | Conditional Inno page; reads existing `tool_paths.json` AND checks plugin-side binaries; per-surface Keep/Change/Skip across all 5 tool surfaces | Phase 2 |
| 4 | Optional Tools picker page + install-step wiring | Inno custom page with 3 file pickers (BSArch, nif-tool, PapyrusCompiler combined row) + 1 dir picker (Papyrus Scripts); copy-at-install for file pickers; JSON write at install | Phase 3 |
| 5 | Sandbox install matrix | Fresh / upgrade-from-v2.6.x (no JSON) / simulated v2.7+ upgrade (JSON seeded) / all tool combos / binary-landing verification / VFS-additive Papyrus Scripts verification | Phase 4 |
| 6 | Ship v2.7.0 | CHANGELOG + README URL + KNOWN_ISSUES docs table + live sync verify + tag + `gh release create` | Phase 5 |

**Live state at plan creation (2026-04-24):**
- v2.6.1 public on GitHub at `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.6.1`. `origin/main` at `c70821d`, clean working tree, live install synced.
- No open Live Reported Bugs.
- Active workstream memory (`project_capability_roadmap.md`) points at this plan.

---

## ✅ Conventions

- **Branch strategy:** all phases on `main`. Each phase = one commit (or a small handful of related commits). Commit messages start with `[v2.7 PN]` (e.g. `[v2.7 P1] Installer policy fixes + version bump`).
- **Plan + handoff artifacts are force-added to git.** `dev/` is gitignored (`.gitignore:45`), so each phase commits its `PHASE_N_HANDOFF.md` (and PLAN.md at P0) via `git add -f`. Once a file is tracked, `git add -f` is not needed again for subsequent edits. This keeps plan milestones auditable in `git log` rather than living local-only. (v2.6.0's P0/P1 handoffs were never committed — that was situational, not a pattern to preserve.)
- **Version-locking discipline:** per `feedback_build_artifact_versioning.md` — once a version X.Y.Z installer has been built (even locally), the version is locked. Phase 1 bumps to v2.7.0 up front so every subsequent build-output is correctly versioned. **Do not rebuild v2.6.0's installer OR v2.6.1's installer.** Both are public GitHub release assets (`v2.6.0` and `v2.6.1` tags on `origin/main`); rebuilding either locally would overwrite a released binary. If a hotfix is needed on the v2.6.x line while v2.7.0 is in flight, that's v2.6.2 — out of scope for this plan.
- **User-provided addon files out of scope.** The installer does not touch `<workspace>/Authoria - Claude_MO2 Addon/` or any other user-managed addon content at `<MO2>/plugins/mo2_mcp/` (`CLAUDE_*.md`, `KB_*.md` authored by the user, custom `.claude/skills/` folders the user added). v2.6.0's installer didn't, v2.7.0's doesn't. Phase 5's test matrix includes an explicit preservation check.
- **Live install sync:** Phases 1–4 do not require live sync. Phase 5 is sandbox-only (install into throwaway directories). Phase 6 live-syncs once and ships.
- **No partial phases.** If a phase can't complete, the handoff records the partial state and lists what blocks the next phase.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive. If you find yourself wanting to modify something outside that list, stop and escalate.
- **Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves** you spot during work.
- **No code changes to the 29 MCP tools' request/response shapes.** Tool-lookup refactoring is internal; user-facing tool interfaces stay identical.

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
<What tests / smoke checks ran. What evidence shows it worked.>

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

## Phase 0 — Scope lock + audit

**Goal:** Answer each Inno Setup 6 API question concretely so Phase 1/3/4 implementers don't hedge. Re-verify Python tool-lookup sites against v2.6.1 source. Lock JSON schema.

**Files to touch:** **none.** Output is `PHASE_0_HANDOFF.md` with definitive answers.

### Questions to answer (each produces a concrete decision in the handoff)

1. **Inno Setup 6 TaskDialog support** — does Inno 6 expose a TaskDialog API that supports (a) custom button label (e.g. "Cancel"), (b) clickable hyperlink in message body opening in user's default browser? Investigation options:
   - Search Inno docs and `Examples/` bundled with Inno Setup 6 install for `TaskDialog` usage.
   - Look for `TaskDialogMsgBox` or similar builtins in Inno's Pascal compiler library.
   - If Inno exposes no direct TaskDialog: document the fallback as `MsgBox(... , mbCriticalError, MB_OK)` with the URL as plain text and a `ShellExec` call separate from the dialog. **Produce one definitive decision** — either "use TaskDialog with this exact API shape" OR "use MsgBox + ShellExec fallback." Phase 1 implements the chosen path with no further research.

2. **JSON reading in Inno Pascal** — Inno has no native JSON library. Confirm the approach for reading `tool_paths.json` in Phase 3's detector page:
   - Manual string scan: read file to string via `LoadStringFromFile`, extract each of 3 keys (`schema_version`, `papyrus_compiler`, `papyrus_scripts_dir`) with a small parser that handles: null values, string values with Windows backslash-escaped paths, whitespace/newlines, trailing commas. Schema is flat and small — scan is tractable.
   - Shell out to `powershell -Command "ConvertFrom-Json"`: heavier but bulletproof.
   - Write a Pascal function signature for the chosen approach (either `ReadToolPathsJson(path: String; var papyrusCompiler, papyrusScriptsDir: String; var schemaVersion: Integer): Boolean` or equivalent). Phase 3 implements to this signature.

3. **JSON writing in Inno Pascal** — same decision for Phase 4's install-step JSON write. Recommend a manual string assembler with a single `SaveStringToFile(path, content, False)` call. Confirm escape handling for Windows paths (`\` → `\\` in JSON string literals). Document the function signature: `WriteToolPathsJson(path: String; papyrusCompiler, papyrusScriptsDir: String): Boolean`. Phase 4 implements to this signature.

4. **Inno custom-wizard-page with mixed-type widgets** — confirm that a `CreateCustomPage` with hand-rolled `TNewEdit`, `TButton`, `TNewCheckBox` widgets supports:
   - Per-row: label + text-edit + Browse button + (for PapyrusCompiler only) a "Reference path at runtime (don't copy)" checkbox below the path row.
   - Browse button opens a `OpenFileDialog` for files and `BrowseForFolder` for dirs.
   - Page validation on Next via `NextButtonClick` override.
   - Reference Inno docs/Examples for `TNewFileEdit`, `TNewCheckBox`, `CreateInputFilePage`, `CreateCustomPage`.
   - If combined PapyrusCompiler row with checkbox is genuinely ugly after ~1h of UI work: fallback is two rows (one for "Copy PapyrusCompiler.exe" file picker, one for "JSON-reference PapyrusCompiler path" file picker with description noting the trade-off). Document the trigger criterion.
   - **Output:** decision on combined vs split row, or "combined expected to work, split as fallback" with concrete criterion.

5. **`_find_papyrus_compiler()` and `_collect_header_dirs()` re-verification** — open `mo2_mcp/tools_papyrus.py` at v2.6.1 and confirm:
   - `_find_papyrus_compiler()` candidates list order (post-v2.6.1 has 5 entries: 2 in-plugin + 3 `%USERPROFILE%`). v2.7.0's Phase 2 prepends a JSON-override entry.
   - `_collect_header_dirs(vfs_dir)` uses `organizer.findFiles` + `resolvePath` fallback; collects unique parent dirs of `.psc` files. v2.7.0's Phase 2 appends `papyrus_scripts_dir` from JSON config if set + dir exists + not already in the seen set.
   - No other tool-lookup site references PapyrusCompiler or Scripts dirs directly. Grep confirms.

6. **`mutagen-bridge-path` and `spooky-cli-path` coexistence** — read `mo2_mcp/__init__.py` at v2.6.1 `settings()` method. Confirm both MO2 plugin settings remain registered post-v2.7.0 (Phase 2 does not touch them; Phase 3/4 do not migrate them). Their values take precedence over JSON for bridge and CLI discovery as today — this is orthogonal to the new JSON for Papyrus.

7. **JSON schema final wording** — confirm:
   - Field names: `papyrus_compiler`, `papyrus_scripts_dir` (snake_case Python-idiomatic).
   - Null = not configured. Missing key = same as null (defensive read; older JSON without a newer key still works).
   - `schema_version: 1` for v2.7.0. Future v2.8 bumps to 2 if schema changes.

8. **Legacy README stubs — audit for retirement or retention.** The three README_*.txt stubs at `<plugin>/tools/spooky-cli/tools/{bsarch,nif-tool,papyrus-compiler}/README.txt` were the v2.6.x user-facing guidance. In v2.7.0 the installer wizard explains each tool in-UI.
   - BSArch / nif-tool README stubs: still useful as "what to put here if you want to place manually" guidance — keep.
   - PapyrusCompiler README stub: same — keep; v2.6.1 made its guidance correct (the flat-layout path now works).
   - Verdict: **keep all three**; Phase 4 does not delete them.

### Steps

1. Execute each of the 8 questions above. Document findings and decisions in `PHASE_0_HANDOFF.md`.
2. Write the handoff with per-question sections. Final section: "Decisions locked for implementers" — one-line summary per question.
3. Commit the handoff: `[v2.7 P0] Phase 0 handoff — scope lock + audit`.

### Verification

The handoff IS the verification. Phase 1 consumes it directly.

### Risk / rollback

Zero risk. Audit only.

### Estimated effort

2-3 hours, mostly Inno Setup documentation reading.

---

## Phase 1 — Installer policy fixes + version bump to v2.7.0

**Goal:** Three `.iss`-only changes + version bumps. Smallest-possible verifiable phase so Phase 2+ implementers start from a known-good installer state with the right version labels on output artifacts.

**Prereqs from Phase 0:** TaskDialog-vs-MsgBox decision locked.

### Changes

**Change 1 — path persistence.**

Edit `installer/claude-mo2-installer.iss`:
- Line 39: `UsePreviousAppDir=yes` → `UsePreviousAppDir=no`.

Effect: on reinstall, the wizard's directory-selection page defaults to `DefaultDirName={autopf}\Mod Organizer 2` instead of the previous install path. User sees the default each time and must browse to their target MO2 folder. Existing `NextButtonClick` validation (`ModOrganizer.exe` must exist) remains.

**Change 2 — .NET 8 Runtime hard-block.**

Rewrite `InitializeSetup()` per Phase 0's locked TaskDialog-vs-MsgBox decision. Remove the `IDNO` fall-through.

If **TaskDialog path** (Phase 0 says viable):
- TaskDialog with MainInstruction, Content including clickable URL (`<A HREF="...">...</A>`), single custom "Cancel" button.
- Hyperlink click dispatches `ShellExec('open', url, ...)` via the TaskDialog callback.
- Any button click OR close aborts install (`Result := False`).

If **MsgBox fallback path**:
- `MsgBox('...' + URL text + '...', mbCriticalError, MB_OK)`.
- After user clicks OK, call `ShellExec('open', url, ...)` to open the download page.
- Return `False` to abort install.

Either way: no branch that lets install continue without .NET 8.

**Change 3 — version bump to v2.7.0.**

Source version: v2.6.1 (current on `main` after the 2026-04-24 hotfix). Target:
- `<repo>/mo2_mcp/config.py` — `PLUGIN_VERSION = (2, 6, 1)` → `PLUGIN_VERSION = (2, 7, 0)`.
- `<repo>/installer/claude-mo2-installer.iss` — `#define AppVersion "2.6.1"` → `#define AppVersion "2.7.0"`.

Note: README installer URL and CHANGELOG entry are NOT bumped in Phase 1 — Phase 6 owns final release documentation. Interim phases may build installers labeled v2.7.0 with a placeholder CHANGELOG; that's fine. Phase 6 writes the actual v2.7.0 CHANGELOG entry and updates README from its current v2.6.1 installer URL.

### Files to touch

- `<repo>/installer/claude-mo2-installer.iss` — Changes 1, 2, 3.
- `<repo>/mo2_mcp/config.py` — Change 3.

**Files NOT to touch:**
- README, CHANGELOG — Phase 6.
- KNOWN_ISSUES — Phase 6 (docs table).
- Python side — Phase 2.

### Steps

1. Apply Changes 1-3 per above.
2. Build installer: `powershell -File build/build-release.ps1 -BuildInstaller`. Confirm output at `build-output/installer/claude-mo2-setup-v2.7.0.exe`.
3. **Sandbox test A — path prompt on reinstall:**
   - Create throwaway dir `test-mo2-A` with a stub `ModOrganizer.exe` (any file named `ModOrganizer.exe` satisfies `FileExists`; `touch test-mo2-A/ModOrganizer.exe` is enough).
   - Install v2.7.0 into it.
   - Run the installer a second time. Confirm directory-select page default is NOT `test-mo2-A` — should be `DefaultDirName` or equivalent.
4. **Sandbox test B — .NET hard-block:**
   - On a machine or VM without .NET 8 Runtime: run the installer. Confirm:
     - No `YES/NO/CANCEL` branches appear.
     - Single-button dialog (TaskDialog or MsgBox per Phase 0's path) shows the download URL.
     - Clicking the URL opens browser to Microsoft's .NET 8 page.
     - Clicking the Cancel/OK button aborts install — no file writes to target.
   - If no machine without .NET 8 is available: temporarily rename `%PROGRAMFILES%\dotnet\` or use a VM image.
5. Commit: `[v2.7 P1] Installer policy fixes + version bump to 2.7.0`.
6. Write `PHASE_1_HANDOFF.md`.

### Verification

- Sandbox test A: fresh prompt confirmed on reinstall.
- Sandbox test B: .NET hard-block confirmed.
- `build-output/installer/claude-mo2-setup-v2.7.0.exe` exists. ISCC compile clean.
- Git log shows single `[v2.7 P1]` commit.

### Risk / rollback

Low risk. `git revert HEAD` restores v2.6.1 behavior. Sandbox testing isolates from live install.

### Estimated effort

2-4 hours, mostly sandbox iteration (Sandbox test B requires a clean .NET-absent environment).

---

## Phase 2 — Python config layer

**Goal:** Add `tool_paths.json` as the source of truth for the two JSON-configured surfaces (PapyrusCompiler override + Papyrus Scripts dir). Refactor `_find_papyrus_compiler()` to check JSON first. Extend `_collect_header_dirs()` to append the Scripts dir to the VFS-derived import chain.

**Prereqs from Phase 2's Python side tying to the installer** is zero — v2.7.0 installer writes JSON in Phase 4; Phase 2's Python graceful-degrades to "JSON not present" behavior equivalent to v2.6.1.

**Prereqs from Phase 0:** JSON schema locked; tool-lookup sites confirmed.

### Files to touch

**New file:**
- `<repo>/mo2_mcp/tool_paths.py` — module that loads, caches, and exposes JSON-configured paths. Suggested API:
  ```python
  # tool_paths.py
  from __future__ import annotations
  from pathlib import Path
  import json
  import logging

  _CONFIG_FILENAME = "tool_paths.json"
  _SCHEMA_VERSION = 1
  _cache: dict | None = None

  def _config_path() -> Path:
      return Path(__file__).resolve().parent / _CONFIG_FILENAME

  def _load() -> dict:
      global _cache
      if _cache is not None:
          return _cache
      path = _config_path()
      if not path.exists():
          _cache = {}
          return _cache
      try:
          with open(path, "r", encoding="utf-8") as f:
              raw = json.load(f)
      except Exception as exc:
          logging.warning(f"tool_paths.json read failed: {exc}")
          _cache = {}
          return _cache
      if not isinstance(raw, dict):
          logging.warning("tool_paths.json is not a JSON object; ignoring.")
          _cache = {}
          return _cache
      sv = raw.get("schema_version")
      if sv != _SCHEMA_VERSION:
          logging.warning(
              f"tool_paths.json schema_version={sv!r} (expected {_SCHEMA_VERSION}); "
              f"ignoring config — Python cannot safely read future/unknown schema."
          )
          _cache = {}
          return _cache
      _cache = raw
      return _cache

  def get(tool: str) -> str | None:
      """
      Returns the configured absolute path for `tool`
      ("papyrus_compiler" | "papyrus_scripts_dir"), or None if:
        - JSON missing
        - key missing or null
        - schema_version mismatch
        - configured path does not exist on disk (with warning log)
      """
      paths = _load()
      value = paths.get(tool)
      if not value or not isinstance(value, str):
          return None
      if not Path(value).exists():
          logging.warning(
              f"tool_paths.json configured {tool}={value!r} but path does not exist"
          )
          return None
      return value

  def reload() -> None:
      """Invalidate the cache. Next get() re-reads the JSON."""
      global _cache
      _cache = None
  ```

  Cache is process-lifetime — MO2 restart is required to pick up JSON edits made outside the installer. That's fine; same pattern as `config.py`'s plugin settings.

**Edits:**

- `<repo>/mo2_mcp/tools_papyrus.py`:
  - Add `from . import tool_paths` at top.
  - Edit `_find_papyrus_compiler()`:
    - **Priority 0:** check `tool_paths.get("papyrus_compiler")` first. If returns a valid path, return it immediately.
    - Priorities 1-5: existing v2.6.1 fallback chain (2 in-plugin + 3 `%USERPROFILE%`).
  - Edit `_collect_header_dirs(vfs_dir)`:
    - Keep existing VFS aggregation logic unchanged.
    - After the VFS-aggregation block, check `tool_paths.get("papyrus_scripts_dir")`. If set, normalize the path and append to `dirs` (skipping duplicates via the existing `seen` set). This is the **additive** behavior locked in scope.
  - Update docstrings to reflect the new priority 0 + additive behavior.

**Files NOT to touch:**
- `tools_archive.py`, `tools_nif.py` — BSArch and nif-tool are in Option C's copy-at-install model; no Python changes. Spooky CLI continues to find them at fixed paths.
- `__init__.py` plugin settings — MO2 plugin settings (`mutagen-bridge-path`, `spooky-cli-path`) unchanged.
- `config.py` — version already bumped in Phase 1.

### Steps

1. Create `mo2_mcp/tool_paths.py` per spec above. Ensure no `mobase` / `PyQt6` imports (stays testable outside MO2 for unit-style smoke tests).
2. Edit `tools_papyrus.py` to import `tool_paths` and wire the priority-0 + additive-append patterns.
3. Update docstrings in both modified functions to reflect the new behavior.
4. **Unit-style smoke test** — Python REPL or throwaway script:
   - Empty / missing JSON → `get("papyrus_compiler")` returns None; `get("papyrus_scripts_dir")` returns None.
   - JSON with `schema_version: 1` and one populated key → `get()` returns the path.
   - JSON with `schema_version: 99` → all `get()` calls return None (+ warning logged).
   - JSON with a valid key pointing at nonexistent path → `get()` returns None (+ warning logged).
   - Malformed JSON (syntax error) → `get()` returns None (+ warning logged); no exception propagates.
   - `reload()` clears cache; next `get()` re-reads.
5. **MO2-integration smoke test** — sync Python to live install via `build-release.ps1 -SyncPython -MO2PluginDir "E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp"` (no `-SyncLive`, Python-only sync). Restart MO2. Confirm:
   - Plugin loads (MO2 log shows `plugin loaded` line without Python traceback).
   - `mo2_ping` returns successfully (tools registered).
   - `mo2_compile_script` with a trivial source still works (no `tool_paths.json` yet → priority 0 miss → falls through to existing v2.6.1 path behavior).
6. Commit: `[v2.7 P2] Python tool_paths.json config layer (papyrus_compiler override + papyrus_scripts_dir additive)`.
7. Write `PHASE_2_HANDOFF.md`.

### Verification

- Unit-style smoke: all 6 bullets pass.
- MO2-integration smoke: plugin loads clean; `mo2_compile_script` works (v2.6.1 equivalence confirmed — regression-free).
- Git log shows single `[v2.7 P2]` commit.
- Grep confirms no other tool-lookup sites reference PapyrusCompiler / Scripts.

### Risk / rollback

Medium risk — changing tool-invocation paths used by `mo2_compile_script`. Mitigation: graceful degradation to v2.6.1 behavior when JSON is absent. `git revert` restores v2.6.1.

### Estimated effort

3-5 hours.

---

## Phase 3 — Previous-install detector wizard page

**Goal:** Add a conditional Inno wizard page that fires on every install and checks 5 tool surfaces for prior state: BSArch binary, nif-tool binary, PapyrusCompiler binary (in-plugin), JSON `papyrus_compiler` override, JSON `papyrus_scripts_dir`. For each found surface, show "Keep / Change / Skip" per surface. Feed user's selections into globals that Phase 4's picker page uses as pre-populated defaults.

**Prereqs from Phase 2:** `tool_paths.json` schema locked; Python side reads it.
**Prereqs from Phase 0:** JSON-read function signature locked.

### Page behavior

**Invocation:**
- Runs after the Dir-select page and after `NextButtonClick` validation of the MO2 dir.
- Auto-skips via `ShouldSkipPage(PageID)` returning `True` if NONE of the 5 surfaces have existing state at the target. For first-time installs: skips silently.
- If ANY surface has existing state: page renders with one row per surface that has state.

**Per-surface detection:**

| Surface | Check |
|---|---|
| BSArch | `FileExists('<target>\plugins\mo2_mcp\tools\spooky-cli\tools\bsarch\bsarch.exe')` |
| nif-tool | `FileExists('<target>\plugins\mo2_mcp\tools\spooky-cli\tools\nif-tool\nif-tool.exe')` |
| PapyrusCompiler (in-plugin binary) | `FileExists('<target>\plugins\mo2_mcp\tools\spooky-cli\tools\papyrus-compiler\PapyrusCompiler.exe')` OR `FileExists('<target>\plugins\mo2_mcp\tools\spooky-cli\tools\papyrus-compiler\Original Compiler\PapyrusCompiler.exe')` |
| JSON `papyrus_compiler` | Read `<target>\plugins\mo2_mcp\tool_paths.json`; if `schema_version == 1` and `papyrus_compiler` non-null, surface the path |
| JSON `papyrus_scripts_dir` | Read same JSON; if `schema_version == 1` and `papyrus_scripts_dir` non-null, surface the path |

Reading JSON: use the Phase 0-locked function (manual scan or PowerShell shellout). If JSON is unparseable or has `schema_version` mismatch, treat both JSON surfaces as absent.

**Per-row UI:**
- Label: surface name ("BSArch", "nif-tool", "PapyrusCompiler (in-plugin binary)", "PapyrusCompiler (JSON reference)", "Papyrus Scripts sources (dir)").
- Current state: detected path or `(from previous install)`.
- Three radio buttons:
  - **Keep** (default if detected)
  - **Change** (sends user to Phase 4 picker for this row pre-populated with existing path, editable)
  - **Skip** (removes the surface — for binaries: file deleted during Phase 4; for JSON keys: key set to null)

**Globals set on Next:**

```pascal
g_Keep_bsarch: Boolean;
g_Change_bsarch: Boolean;
g_Skip_bsarch: Boolean;
g_ExistingPath_bsarch: String;
// (same pattern for nif_tool, papyrus_compiler_binary, papyrus_compiler_json, papyrus_scripts_dir)
```

Phase 4's picker page reads these to decide the initial state of each row.

### Files to touch

- `<repo>/installer/claude-mo2-installer.iss` — new `[Code]` section implementing:
  - `InitializeWizard()` hook: register the detector page after the Dir page.
  - `DetectorPage_OnPageShow()`: run detection + populate labels/radios.
  - `DetectorPage_OnNextClick()`: record globals.
  - `ShouldSkipPage(PageID)`: returns True when no surfaces have state.
  - `ReadToolPathsJson()` helper (per Phase 0 signature).

**Files NOT to touch:**
- Python side.

### Steps

1. Read the Phase 0 handoff's locked JSON-read signature. Implement in `[Code]`.
2. Add detection functions for each of the 5 surfaces.
3. Register the detector page via `CreateCustomPage` (after `wpSelectDir`).
4. Implement `ShouldSkipPage` for auto-skip.
5. Implement page UI: labels, radio groups per row.
6. Implement Next-click handler to record globals.
7. **Sandbox test A — fresh install, no JSON, no binaries:** detector page auto-skips. Wizard proceeds to Phase 4 picker page (which will render blank until Phase 4 is built — P3 smoke-tests the skip behavior only).
8. **Sandbox test B — upgrade-from-v2.6.1 (no JSON, but binaries present):** detector page shows 3 rows (BSArch, nif-tool, PapyrusCompiler binary) if any of those binaries are in Aaron's real live install; JSON surfaces absent. Radio defaults: Keep.
9. **Sandbox test C — simulated v2.7+ upgrade:** manually seed a `tool_paths.json` at the target (schema_version: 1, both keys populated) + place dummy binaries. Detector shows all 5 rows. Test all three radios per row.
10. **Sandbox test D — malformed JSON:** seed JSON with syntax error. Detector skips JSON surfaces, binaries still surface.
11. **Sandbox test E — schema_version mismatch:** seed JSON with `schema_version: 99`. Detector skips JSON surfaces.
12. Commit: `[v2.7 P3] Installer previous-install detector page (5 tool surfaces; Keep/Change/Skip)`.
13. Write `PHASE_3_HANDOFF.md`.

### Verification

- Sandbox tests A-E all pass.
- `ShouldSkipPage` behaves correctly on no-state installs.
- Globals recorded per surface; Phase 4 can read them.

### Risk / rollback

Low-medium risk. Pure UI + detection; no file writes yet. `git revert` removes the page cleanly.

### Estimated effort

4-6 hours. Inno Pascal scripting + 5 sandbox scenarios.

---

## Phase 4 — Optional Tools picker page + install-step wiring

**Goal:** Custom wizard page with 4 rows (3 file pickers + 1 dir picker). PapyrusCompiler row is combined: file picker + a "Reference path at runtime (don't copy)" checkbox below it. On install (post-copy), write `tool_paths.json` and copy user-selected binaries to the plugin dir.

**Prereqs from Phase 3:** detector globals populated.
**Prereqs from Phase 0:** JSON-write function signature locked; combined-row-vs-split-row decision locked with concrete fallback criterion.

### Page layout

Header:
> **Optional Tools**
>
> Claude MO2 works with BSArch, nif-tool, and the Creation Kit's PapyrusCompiler for certain capabilities. Point the installer at them below to enable. Leave any field empty to skip — you can configure paths later by editing `<plugin>\mo2_mcp\tool_paths.json` (Papyrus surfaces) or re-running this installer (BSArch/nif-tool).

**Row 1 — BSArch (file picker, copy-at-install):**
- Label: "BSArch.exe"
- Description: "Part of xEdit. Required for `mo2_list_bsa`, `mo2_extract_bsa`, `mo2_validate_bsa`."
- Path text field + Browse button (file filter: `BSArch.exe|bsarch.exe`)
- State from Phase 3:
  - `g_Keep_bsarch`: show path read-only, no Browse. No change during install.
  - `g_Change_bsarch`: editable, pre-populated, Browse enabled. On Next: validated path gets **copied** to `<target>\plugins\mo2_mcp\tools\spooky-cli\tools\bsarch\bsarch.exe` overwriting existing.
  - `g_Skip_bsarch`: blank. On Next: no copy; any existing plugin-dir binary is **deleted** during install.
  - No prior state: blank. On Next: copy if user provided a path, nothing if blank.

**Row 2 — nif-tool (file picker, copy-at-install):**
- Label: "nif-tool.exe"
- Description: "From Spooky's AutoMod Toolkit v1.11.1 release. Required for `mo2_nif_list_textures`, `mo2_nif_shader_info`. `mo2_nif_info` works without it."
- Same state pattern as BSArch. Target path: `<target>\plugins\mo2_mcp\tools\spooky-cli\tools\nif-tool\nif-tool.exe`.

**Row 3 — PapyrusCompiler (combined: file picker + checkbox):**
- Label: "PapyrusCompiler.exe"
- Description: "Ships with the Creation Kit. Required for `mo2_compile_script`."
- Path text field + Browse button (file filter: `PapyrusCompiler.exe`).
- Below the path row, indented: checkbox "Reference this path at runtime (don't copy into plugin folder)".
  - Unchecked (default): installer **copies** the picked binary into `<plugin>\tools\spooky-cli\tools\papyrus-compiler\PapyrusCompiler.exe`. No JSON write.
  - Checked: installer writes the picked path into `tool_paths.json["papyrus_compiler"]`. No copy.
- State from Phase 3 considers two separate surfaces (binary + JSON):
  - `g_Keep_papyrus_compiler_binary`: copy preserved; JSON override untouched.
  - `g_Keep_papyrus_compiler_json`: JSON preserved; binary untouched.
  - `g_Change_papyrus_compiler_binary`: editable path with checkbox unchecked; picked binary copies, JSON stays null.
  - `g_Change_papyrus_compiler_json`: editable path with checkbox **checked** (auto-set by detector's Change-on-JSON intent); picked path writes to JSON, no copy.
  - Edge case: both binary and JSON exist prior — two independent rows? Per Phase 0's locked decision, this is a single row with a checkbox that mirrors the detector's detected mode. If both exist, the page displays a one-line warning "Detected both a copied binary and a JSON reference. Choose one." + defaults the checkbox to the JSON-reference mode (since it's the more explicit signal).
- **Fallback (if Phase 0's ~1h UI-work criterion trips):** split into two separate rows (Row 3a: "PapyrusCompiler.exe (copy)", Row 3b: "PapyrusCompiler.exe path (JSON reference)"). Page becomes 5 rows. Phase 4 handoff documents which path was taken.

**Row 4 — Papyrus Scripts sources (dir picker, JSON-only):**
- Label: "Papyrus Scripts sources directory"
- Description (multi-line):
  > Required for `mo2_compile_script` to resolve base-Skyrim types (`Actor`, `Quest`, `Debug`, etc.).
  >
  > The Creation Kit ships `Scripts.zip` containing base Papyrus sources. **Extract `Scripts.zip` manually** first — install layouts vary too much to automate. Then select the extracted folder here.
  >
  > This path is additive to MO2's VFS script aggregation — it supplements any extracted-into-a-mod-folder scripts.
- Dir text field + Browse button (folder picker).
- State from Phase 3: `g_Keep_papyrus_scripts_dir` / `g_Change_papyrus_scripts_dir` / `g_Skip_papyrus_scripts_dir`.
- On Next: writes `tool_paths.json["papyrus_scripts_dir"]`. No copy.

**Validation on Next (per row):**
- File pickers (Rows 1-3): if non-empty, path must exist. If path exists but filename mismatch (e.g. user picked `xEdit64.exe` instead of `BSArch.exe`): show warning dialog with Yes/No "Continue anyway?". Yes keeps user's path (honors override).
- Dir picker (Row 4): if non-empty, dir must exist. No content validation (don't gate on presence of `Actor.psc` etc.; user may have partial extractions).
- Path-missing is a hard error (force user to fix); filename-mismatch is a soft warning (allow override).
- Empty = skip (valid state, no validation).

### Install-step wiring (`CurStepChanged(ssPostInstall)`)

After Inno's standard file copy, execute in order:
1. For each of BSArch / nif-tool / PapyrusCompiler (copy mode):
   - If user chose Keep: nothing (binary already at plugin path).
   - If user chose Change + provided path: copy from picked source to plugin-dir target (overwriting any existing).
   - If user chose Skip: delete any existing plugin-dir binary.
2. Write `tool_paths.json` via Phase 0-locked `WriteToolPathsJson` helper:
   - `papyrus_compiler`: non-null only if PapyrusCompiler row had the JSON-reference checkbox checked AND a valid path; else null.
   - `papyrus_scripts_dir`: user-selected path or null.
3. Update post-install status report (`CurStepChanged(ssPostInstall)` existing MsgBox) to reflect the final state: BSArch/nif-tool/PapyrusCompiler binary paths present-or-not; JSON keys populated-or-not.

### Files to touch

- `<repo>/installer/claude-mo2-installer.iss`:
  - Add custom wizard page implementation in `[Code]` (labels, edits, buttons, checkbox, validation).
  - Add copy/delete logic in `CurStepChanged(ssPostInstall)`.
  - Add `WriteToolPathsJson` helper per Phase 0 signature.
  - Update existing post-install status MsgBox for new surface reporting.
  - Add `tool_paths.json` to the list of files uninstaller should NOT delete — explicitly omit it from `[UninstallDelete]`.

**Files NOT to touch:**
- Python side.
- README stubs (all three kept per Phase 0 verdict).

### Steps

1. Implement picker page UI per Phase 0's locked combined-vs-split decision. First attempt combined PapyrusCompiler row; at ~1h mark, evaluate against the locked criterion — if ugly, split to two rows.
2. Wire Phase 3 globals into initial field/state values per-row.
3. Implement Next-button validation (path-exists + filename-mismatch warnings).
4. Implement `WriteToolPathsJson` helper.
5. Implement `CurStepChanged(ssPostInstall)` copy/delete/JSON-write logic.
6. Update post-install status MsgBox.
7. Verify uninstaller behavior: install, modify `tool_paths.json` manually, uninstall. Confirm JSON is NOT removed by uninstall.
8. **Sandbox test matrix:**
   - Fresh install, all 4 rows skipped → JSON has null values; no binaries copied.
   - Fresh install, all 4 rows populated with valid paths (PapyrusCompiler: copy mode) → JSON has `papyrus_scripts_dir` populated + `papyrus_compiler: null`; BSArch/nif-tool/PapyrusCompiler binaries landed at plugin paths.
   - Fresh install, PapyrusCompiler in JSON mode → JSON has both `papyrus_compiler` + `papyrus_scripts_dir` populated; no PapyrusCompiler binary in plugin dir.
   - Fresh install, filename-mismatch (BSArch pointer at `xEdit64.exe`) → warning → override → file copied as-named at target path.
   - Upgrade-from-v2.6.1 (binaries present): Keep on all → no-op install, JSON has null values (no prior JSON), binaries preserved.
   - Upgrade-from-v2.7 simulated: Keep on all → no-op.
   - Upgrade-from-v2.7 simulated: Change papyrus_compiler from JSON to binary copy → JSON `papyrus_compiler` becomes null, binary lands at plugin path.
   - Upgrade-from-v2.7 simulated: Skip BSArch → existing BSArch binary at plugin path gets deleted.
   - Uninstall: `tool_paths.json` NOT deleted (verify post-uninstall).
9. Commit: `[v2.7 P4] Installer Optional Tools picker + install-step wiring (4 surfaces)`.
10. Write `PHASE_4_HANDOFF.md`.

### Verification

- All sandbox matrix scenarios land the expected JSON + binary state.
- Post-install status MsgBox reflects configured paths correctly.
- Uninstall preserves `tool_paths.json`.

### Risk / rollback

Medium-high risk — largest UI + logic surface in the plan. Mitigation: heavy sandbox matrix. `git revert` restores Phase 3 end state.

### Estimated effort

5-8 hours (combined-row threshold applies; if split-row fallback triggers, add 1-2h for the extra row).

---

## Phase 5 — Sandbox install matrix

**Goal:** Comprehensive regression test of all installer changes together. **No code changes** unless a regression is found.

**Prereqs from Phase 4:** all code in; installer builds clean.

### Test matrix

Execute each scenario in a throwaway sandbox directory with a stub `ModOrganizer.exe`. Record: scenario, expected JSON content, expected binary landings, observed state, pass/fail.

| # | Scenario | Expected JSON | Expected binaries |
|---|---|---|---|
| T1 | Fresh install, skip all 4 rows | `{schema_version: 1, papyrus_compiler: null, papyrus_scripts_dir: null}` | No bsarch/nif-tool/PapyrusCompiler in plugin dir |
| T2 | Fresh install, all 4 populated (PapyrusCompiler copy mode) | `{..., papyrus_compiler: null, papyrus_scripts_dir: "<path>"}` | All 3 binaries at plugin paths |
| T3 | Fresh install, PapyrusCompiler JSON mode + others copy | `{..., papyrus_compiler: "<path>", papyrus_scripts_dir: "<path>"}` | BSArch + nif-tool at plugin paths; no PapyrusCompiler in plugin dir |
| T4 | Fresh install, filename-mismatch warning acknowledged | JSON null for relevant surface; binary landed with mismatched name | Override behavior documented |
| T5 | Upgrade-from-v2.6.1 (binaries present, no JSON) → Keep all | JSON written with null values (first-time JSON) | Binaries preserved as-is |
| T6 | Upgrade-from-v2.7 simulated (pre-seeded JSON + binaries) → Keep all | JSON unchanged | Binaries unchanged |
| T7 | Upgrade-from-v2.7, Change PapyrusCompiler copy→JSON | JSON papyrus_compiler populated; binary at plugin path deleted | BSArch/nif-tool unchanged |
| T8 | Upgrade-from-v2.7, Change PapyrusCompiler JSON→copy | JSON papyrus_compiler null; binary landed at plugin path | BSArch/nif-tool unchanged |
| T9 | Upgrade-from-v2.7, Skip all → all detached | JSON has both null values | All plugin-dir binaries deleted |
| T10 | Simulated future-schema JSON (`schema_version: 99`) | Detector skips JSON rows; picker renders blank; install overwrites JSON with `schema_version: 1` | n/a |
| T11 | Path-persistence regression re-run (Phase 1 Test A) | Fresh prompt on reinstall | n/a |
| T12 | .NET hard-block regression re-run (Phase 1 Test B) | Installer aborts; no JSON or binary writes | n/a |
| T13 | **Python-side additive VFS test** — install with papyrus_scripts_dir pointing at a real Scripts.zip-extracted dir (Aaron provides); live-sync Python; restart MO2; call `mo2_compile_script` against a script that references `Actor` | n/a | Compile succeeds; `headers_used` in response shows BOTH the VFS-aggregated dirs AND the configured scripts dir |
| T14 | **Python-side MO2-settings-precedence test** — ensure `mutagen-bridge-path` MO2 plugin setting still takes precedence over any JSON surface (JSON doesn't define bridge paths, so this is just a non-interference check) | n/a | `mo2_create_patch` still works via MO2-setting-configured bridge |
| T15 | **User addon preservation** — before install, manually place representative addon artifacts (`CLAUDE_TestAddon.md`, `kb/KB_TestAddon.md`, `.claude/skills/test-addon/SKILL.md`) into the sandbox's `<target>/plugins/mo2_mcp/` dir. Install v2.7.0 over the existing state. Verify: all three addon files are still present post-install, byte-identical. Also verify uninstall does NOT remove them. Reflects the Authoria workflow (Aaron syncs `<workspace>/Authoria - Claude_MO2 Addon/` into his live install manually; installer must never touch those files). | n/a | Addon files preserved across install and uninstall |

### Steps

1. Build installer: `powershell -File build/build-release.ps1 -BuildInstaller`. Confirm `build-output/installer/claude-mo2-setup-v2.7.0.exe` exists.
2. Run each test T1–T15. Document results.
3. For each pass: move on. For each fail: identify which phase introduced the bug. Pause Phase 5; fix-up commit in the responsible phase's scope (not a new phase). Re-test.
4. T13 requires Aaron to provide a valid Scripts.zip-extracted dir path. Treat as a "conduct" step — ask Aaron mid-phase if not already arranged.
5. Write `PHASE_5_HANDOFF.md` with the full matrix results.

### Verification

The test matrix IS the verification. All 15 must pass before Phase 6.

### Risk / rollback

Read-only phase other than fix-ups. Finding regressions here beats finding them post-ship.

### Estimated effort

4-6 hours, mostly install iteration + Python-side live testing.

---

## Phase 6 — Ship v2.7.0

**Goal:** Public release. v2.7.0 installer on GitHub. CHANGELOG, README, KNOWN_ISSUES updated.

**Prereqs from Phase 5:** all 15 tests pass.

### Files to touch

- `<repo>/mo2_mcp/CHANGELOG.md` — new top entry `## v2.7.0 — YYYY-MM-DD`. Cover:
  - **Headline:** Installer overhaul — path-persistence fix, .NET 8 hard-block, configurable tool paths via `tool_paths.json` + installer pickers.
  - **Path persistence fix:** `UsePreviousAppDir=no`; installer always prompts fresh for MO2 directory.
  - **.NET 8 Runtime now required at install:** installer aborts if .NET 8 Runtime is not detected. TaskDialog (or MsgBox) shows clickable download URL + single-button exit. No continue-anyway.
  - **Configurable tool paths via installer:**
    - BSArch, nif-tool, PapyrusCompiler binaries: installer picker; copied into plugin's `tools/spooky-cli/tools/<name>/` paths.
    - PapyrusCompiler also supports JSON-reference mode via checkbox in installer.
    - Papyrus Scripts sources: JSON-only, additive to VFS script aggregation.
    - All surfaces tracked in `<plugin>/mo2_mcp/tool_paths.json` (schema_version: 1).
  - **Previous-install detector:** on upgrade, installer reads existing state (plugin-dir binaries + JSON) and offers per-surface Keep/Change/Skip before the picker page.
  - **No auto-detection heuristics:** paths come from user browse, not drive scanning.
  - **Scripts.zip extraction is manual:** installer does not automate extraction; user extracts and points at the result.
  - **Migration for v2.6.x users:**
    - On upgrade to v2.7.0, re-run installer. Existing tool binaries in plugin dir are detected; user confirms Keep/Change/Skip.
    - `tool_paths.json` is a new file; v2.6.x users have none. Detector shows only binary surfaces on upgrade.
    - To configure Papyrus Scripts dir (new capability): edit JSON after install, or use installer picker.
    - `mutagen-bridge-path` and `spooky-cli-path` MO2 plugin settings are unchanged.
- `<repo>/README.md`:
  - Installer URL: `claude-mo2-setup-v2.6.1.exe` → `claude-mo2-setup-v2.7.0.exe` (2 occurrences).
  - Quick Install section: add bullet about the new optional-tools page.
  - Requirements section: tighten the .NET 8 Runtime language ("Required at install time").
  - Manual Install section: update if it still references the legacy fixed-path drop-in instructions.
- `<repo>/KNOWN_ISSUES.md`:
  - Header bump: v2.6.1 → v2.7.0.
  - **Add Aaron's Option C docs table** under a new section "User-provided tools — how they're configured":

    | Tool | How configured | Refresh method |
    |---|---|---|
    | BSArch | Installer picker; copied into plugin dir | Re-run installer with new binary |
    | nif-tool | Installer picker; copied into plugin dir | Re-run installer with new binary |
    | PapyrusCompiler | Installer picker (copy by default; JSON-reference via checkbox) | Re-run installer OR edit `tool_paths.json` |
    | Papyrus Scripts sources | `tool_paths.json` (additive to VFS) | Edit `tool_paths.json` + restart MO2 |

  - Update the existing "user-provided prerequisites" section to reference `tool_paths.json` where applicable.
  - Add a row to the resolved-bugs table for the path-persistence fix (v2.7.0) and .NET hard-block (v2.7.0).
- `<repo>/CLAUDE.md`:
  - Verify `tool_paths.json` is referenced where modlist-specific config would otherwise be expected (skim).
- `<repo>/.claude/skills/bsa-archives/SKILL.md`, `<repo>/.claude/skills/nif-meshes/SKILL.md`, `<repo>/.claude/skills/papyrus-compilation/SKILL.md`:
  - Each skill's prerequisites section updates language: "BSArch is user-provided; point the installer at it, or drop manually into `<plugin>/tools/spooky-cli/tools/bsarch/`" (etc.).

**Files NOT to touch:**
- Anything behavioral. Release prep only.

### Steps

1. Write v2.7.0 CHANGELOG entry.
2. Bump README installer URL (2 places).
3. Update KNOWN_ISSUES — header, docs table, existing sections.
4. Update skill files' prerequisite language.
5. Build installer: `powershell -File build/build-release.ps1 -BuildInstaller`.
6. Confirm output at `build-output/installer/claude-mo2-setup-v2.7.0.exe`.
7. Sandbox-test the v2.7.0 installer one more time (smoke only; full matrix was Phase 5).
8. **Live install sync:** `powershell -File build/build-release.ps1 -SyncLive -MO2PluginDir "E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp"`. Confirm plugin still loads; `mo2_ping` returns `version: "2.7.0"`; `mo2_compile_script` still works with Aaron's existing setup.
9. Commit: `[v2.7 P6] v2.7.0 release prep: CHANGELOG, README, KNOWN_ISSUES, skills, installer`.
10. Tag: `git tag -a v2.7.0 -m "v2.7.0 release"`.
11. Gate on Aaron's explicit approval before push (same discipline as v2.6.0/v2.6.1).
12. On approval: `git push origin main --tags` + `gh release create v2.7.0 --title "v2.7.0 — Installer overhaul: configurable tool paths, .NET hard-block, fresh path prompt" --notes-file <...> "build-output/installer/claude-mo2-setup-v2.7.0.exe"`.
13. Write `PHASE_6_HANDOFF.md` marking the overhaul complete.

### Verification

- Public release exists: `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.7.0`.
- Release asset downloads and installs.
- Live smoke on Aaron's install post-sync: `mo2_ping` reports 2.7.0; one tool-bound MCP tool call succeeds.

### Risk / rollback

Low risk if Phase 5 passed. Hotfix path is v2.7.1 if post-release issues surface.

### Estimated effort

2-3 hours.

---

## 📦 Cleanup (post-Phase 6, optional)

- Document the `tool_paths.json` schema in a new `kb/KB_ToolPaths.md` or a README section. Future session.
- Consider MCP tool surface for inspecting/editing `tool_paths.json` (`mo2_get_tool_paths` / `mo2_set_tool_path`) — out of scope for v2.7.0; candidate for v2.8.
- Consider folding `mutagen-bridge-path` and `spooky-cli-path` into `tool_paths.json` for consistency — out of scope for v2.7.0; candidate for v2.8 with the plugin-setting layer documented as deprecated.

---

## ✏️ Plan revisions

If a phase finds the plan wrong (API differs from expectation, Inno doesn't expose what was assumed, etc.), update this PLAN.md as part of that phase's commit. Note the revision in the phase handoff. The plan is a living document until Phase 6 ships.

---

End of plan.
