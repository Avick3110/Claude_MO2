# KB_Diagnostics.md — Crash & Freeze Diagnosis

Load this at the start of any session where the user reports a crash, freeze, or unexplained game misbehavior that wasn't present before a change. The triage step at the top costs nothing and changes the entire diagnostic path.

---

## First: ask about the failure type

Before any investigation, prompt the user:

> **"Is this a hard freeze (game hangs, no crash log generated) or a CTD (crash to desktop with a crash log)?"**

The answer determines which layer to investigate first. Getting the type wrong means looking in the wrong place and burning significant context on dead ends.

If the user doesn't know:
- Crash Logger SSE AE VR (or similar) writes logs to one of:
  - `<MO2>/overwrite/SKSE/Crashlogs/` (when MO2 captures SKSE's output)
  - `<User Documents>/My Games/Skyrim Special Edition/SKSE/` (default SKSE log dir)
  - Or wherever configured in the crash logger's INI
- No log + game froze on a stuck/black screen → hard freeze.
- Crash log exists → CTD.

---

## Hard freeze (no crash log)

Engine-level: rendering pipeline, threading deadlock, infinite loop. The crash handler never fires because the engine itself is stuck.

### Diagnostic priority

1. **Papyrus log first.** Papyrus itself rarely *causes* hard freezes, but the log shows the last activity before the hang — which cell load, which actor, which quest event. This pinpoints **where** the freeze is, which narrows every downstream step.

   If no Papyrus log exists, it is almost certainly not enabled — logging is off by default. **Stop here and ask the user to enable it** before proceeding:
   - Add to `Skyrim.ini` (or `SkyrimCustom.ini`) under `[Papyrus]`:
     ```
     bEnableLogging=1
     bEnableTrace=1
     bLoadDebugInformation=1
     ```
   - Reproduce the freeze once with logging on.
   - Log lands at `<User Documents>/My Games/Skyrim Special Edition/Logs/Script/Papyrus.0.log`.

   Without this signal, steps 2 and 3 are blind — you'd be searching the entire load order instead of a specific cell/actor/quest. Get the log first.

2. **Assets (filesystem — MCP not required).** Once Papyrus identifies a specific cell or actor, inspect assets in that area:
   - Grass cache (`.cgid`) files in worldspaces that use non-standard lighting (point-light-only caves, some DLC worldspaces). Skyrim's grass renderer only supports directional (sun) light.
   - Corrupted or non-standard NIF files referenced by placed objects — use `mo2_nif_info` on suspects.
   - Missing textures referenced in the last-loaded area.

3. **Records (MCP).** Deleted references, wild WRLD/CELL edits, missing master references. Use `mo2_conflict_chain` on any suspect FormID from the area Papyrus identified, `mo2_record_detail` to see what the winning plugin actually writes.

### Key insight

Papyrus gives you the *location*; assets and records tell you the *cause*. Without the location, you're searching a 3,000-plugin haystack. With it, you're checking a specific cell's asset layer and a handful of records.

---

## CTD (crash log generated)

Code-level: null pointer, access violation, stack overflow. The crash handler fires and writes a log.

### Diagnostic priority

1. **Crash log first.** Ask the user for the log contents — it names the failing function, the module (DLL), and often the specific FormID that triggered the crash.
2. **Records (MCP).** Run `mo2_conflict_chain` on the FormID from the log. Check for bad references and form overrides.
3. **Scripts (Papyrus log).** May show script activity leading to the crash.
4. **Assets (filesystem).** Less common for CTDs, but corrupted meshes cause access violations.

---

## Don'ts

- **Don't skip the triage question** to save one round-trip. It costs nothing; getting the type wrong costs a lot of context.
- **Don't bank unvalidated diagnostic theories as rules.** A pattern that emerges during one session is a tentative finding — not a rule for future sessions until confirmed across multiple independent cases.
- **Don't recommend a mod-install fix based on web research alone.** See `CLAUDE.md`'s "Review Before Recommending Mod Installs" standing rule for the review workflow.

---

## Why this matters

Triaging wrong can turn a 15-minute investigation into a 90-minute one. Asset-layer issues don't show up in MCP queries; record-layer issues don't show up in the filesystem. The triage question costs one turn; the wrong investigation path costs dozens.
