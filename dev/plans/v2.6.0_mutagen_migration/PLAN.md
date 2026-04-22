# v2.6.0 — Mutagen Migration Plan

**Owner:** Aaron (`@Avick3110`)
**Created:** 2026-04-22 (after Synthesis-patcher empirical verification — see "Background" below).
**Target version:** v2.6.0
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
   Claude_MO2/dev/plans/v2.6.0_mutagen_migration/
   ```
   Find the highest-numbered file matching `PHASE_*_HANDOFF.md`. **Your phase is one greater than that.** If no handoffs exist yet, you are **Phase 0**. If `PHASE_6_HANDOFF.md` exists, **the migration is complete** — point the user at it and stop; the plan is done.

2. **Read the previous handoff** (if any) in full. It tells you what was done, what was deviated from, and any preconditions for your phase. **Trust the handoff over this plan when they conflict** — the plan is the original intent; the handoff is the actual state.

3. **Read your phase section in this file** (scroll to "Phase N — ..." below). It tells you the goal, the files to touch, the steps, and what to write in your own handoff.

4. **Run the standard dev-startup orientation** (per `feedback_dev_startup.md` memory):
   - `Claude_MO2/README.md`
   - `Claude_MO2/mo2_mcp/CHANGELOG.md` (top entry)
   - `Claude_MO2/KNOWN_ISSUES.md`
   - **Skip** the session-summaries / handoffs sweep — this plan is your roadmap.
   - Check `<workspace>/Live Reported Bugs/` root for anything new. **The `archive/v2.5.6/` and `archive/v2.5.7/` subdirectories are historical bug reports that were resolved in those versions (auto-enable machinery, ExtendedList<T> set_fields, enabled/disabled filtering, implicit-load plugins). v2.5.6 and v2.5.7 are never-publicly-released builds whose changes roll into v2.6.0 — see "Background" above. Do NOT read files under `archive/` as if they're active bugs. If root is empty (which it should be when this plan begins), proceed.**

5. **Confirm with the user** which phase you've identified yourself as and any deviations you've noticed from the plan. Wait for go-ahead before making changes.

6. **At the end of your phase**, write `PHASE_N_HANDOFF.md` in this directory, following the template at the bottom of this file. Then tell the user the handoff is written and the next session can begin.

**Do not execute multiple phases in one session.** Each phase is its own work unit. If you finish early, summarise and stop — don't roll into the next phase.

---

## 📋 Background — why this plan exists

A user-reported bug on 2026-04-21 surfaced that `mo2_create_patch` produces ESPs whose FormLinks to ESL-flagged plugins (specifically `NyghtfallMM.esp`) are unresolved by xEdit and the Skyrim runtime. A 36-record MUSC merge patch had 25 broken FormLinks; a hand-built xEdit patch using xEdit's compacted IDs worked correctly. Full bug write-up: `Claude_MO2/dev/session-summaries/SESSION_SUMMARY_2026-04-21_esl_formid_compaction_bug.md`.

Initial deep research (`Claude_MO2/dev/reports/MUTAGEN_MIGRATION.md`) recommended migrating the bridge to Mutagen's `GameEnvironment`/`LinkCache`/`BeginWrite.WithLoadOrder` APIs. A subsequent agent-led source investigation of the Mutagen 0.54-alpha tree concluded the proposal wouldn't help — Mutagen's source had no ESL-aware compaction code path on Skyrim SE. That conclusion was **empirically wrong**.

A diagnostic Synthesis patcher (in `<workspace>/research/EslReproPatcher/`) was written to settle the question. Synthesis (Mutagen `0.53.1` via `Mutagen.Bethesda.Synthesis 0.35.5`) **resolved every NyghtfallMM MUST FormLink correctly**, both on read (LinkCache returned compacted `000884..000889`, matching xEdit) and on write (output `.esp` opened in xEdit showed all FormLinks resolved cleanly). Console + xEdit screenshots in the session transcript that produced this plan.

**Conclusion:** the bug is fixable by upgrading our Mutagen reference from `0.52.0` to `0.53.1+` and matching Synthesis's API pattern (`GameEnvironment` + `LinkCache` for reads, `BeginWrite.WithLoadOrder` for writes). The deep-research spec was directionally correct; only its mechanism explanation was wrong.

In addition, `esp_index.py` and `esp_reader.py` use raw `<I` byte reads from plugin files, which give us non-compacted FormIDs for ESL plugins — meaning every record-facing tool (`mo2_query_records`, `mo2_record_detail`, `mo2_conflict_chain`, `mo2_plugin_conflicts`, `mo2_conflict_summary`, `mo2_find_conflicts`) returns FormIDs that disagree with xEdit. This silently breaks Claude's lookup-assistant role for any modlist with ESL plugins (i.e., basically all modern modlists). Same migration also fixes this.

Finally, the index has accreted complex event-driven invalidation (`onPluginStateChanged` → rebuild, `mo2_create_patch` → `trigger_refresh_and_wait_for_index`, `next_step` coordination, `mo2_refresh` response fields). The complexity has bug-farmed across releases. Replace it with lazy build + per-query mtime freshness check.

Naming hygiene: `tools/spooky-bridge/` does not use any code from `spooky-toolkit/` (verified by reading the `using` statements). It only inherits Mutagen transitively via the submodule's ProjectReferences. Rename to `tools/mutagen-bridge/` and depend on Mutagen via direct NuGet PackageReference. The `spookys-automod.exe` CLI used by Papyrus / BSA / NIF / Audio (non-FUZ) tools stays — that's a separate binary which genuinely wraps Spooky's subprocess work.

**Mid-Phase-2 update (2026-04-22):** While verifying Phase 2's Mutagen write-path changes, the bridge produced patches with the same broken FormLink symptom the migration was supposed to fix. Diagnosis revealed the actual root cause: `PluginResolver._build` in `esp_index.py` walks the `mods/` directory in lexical order, allowing a non-ESL `NyghtfallMM.esp` in `Replacer - Nyghtfall - Music/` to clobber the active ESL `NyghtfallMM.esp` in `Nyghtfall - ESPFE (Replacer)/` in the name→path map. MO2's actual VFS resolves the opposite direction. The bridge has been reading the wrong file for any plugin name that exists in multiple mods. The Mutagen migration still ships in v2.6.0 — `WithLoadOrder` is independently valuable for write-path correctness on genuinely-ESL masters, and the version bump + naming hygiene stand on their own — but the headline user-facing fix is now the path resolver, which Phase 2 absorbs as a justified scope expansion. The control test result (with/without `WithLoadOrder`, `PluginResolver` fixed both ways) is documented in `PHASE_2_HANDOFF.md`.

---

## 🗺️ Phase map

| # | Phase | Output | Prereqs |
|---|---|---|---|
| 0 | Mutagen 0.53.1 read-path probe | Decision: minimal refactor vs full GameEnvironment migration | None |
| 1 | Rename `spooky-bridge` → `mutagen-bridge`, drop spooky-toolkit ProjectRef | Renamed bridge, direct Mutagen PackageReference, all existing behavior preserved | Phase 0 |
| 2 | Add load-order context, route patch writes through GameEnvironment + LinkCache + WithLoadOrder | ESL FormID write bug fixed end-to-end | Phase 1 |
| 3 | Retire `esp_reader.py`; rewrite `esp_index.py` over the bridge | All Python tool FormIDs match xEdit | Phase 2 |
| 4 | Retire event-driven index invalidation | Lazy build + freshness check; `trigger_refresh_and_wait_for_index` and friends gone | Phase 3 |
| 5 | Live regression on Aaron's modlist | Pass/fail gate — if fail, return to whichever phase introduced the bug | Phase 4 |
| 6 | v2.6.0 ship: changelog, version bump, installer, GitHub release | Public v2.6.0 | Phase 5 |

**Live state at plan creation (2026-04-22):**
- v2.5.5 public on GitHub.
- v2.5.6 + v2.5.7 built locally and synced to live install at `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\`, **not yet pushed to GitHub**. At Phase 1 start the changes were also **uncommitted on main**; Phase 1 committed them as a single local prereq commit (commit message prefix "Commit locally-built v2.5.6 + v2.5.7 before v2.6 P1") before starting the rename work so `[v2.6 P1]+` commits stay surgical about what each phase actually changes. The commit hash is recorded in `PHASE_1_HANDOFF.md`'s "Commits made" line. Decision remains: **do NOT ship v2.5.6/v2.5.7 separately on GitHub** — roll their content into v2.6.0's CHANGELOG in Phase 6. The v2.5.6 + v2.5.7 commit stays visible only in the local/pushed git log; Phase 6's user-facing release notes present one v2.6.0 entry.
- No open Live Reported Bugs.
- `<workspace>/research/EslReproPatcher/` is the diagnostic Synthesis patcher used to settle the design question. Keep it for reference until Phase 5 completes; can be archived after.

---

## ✅ Conventions

- **Branch strategy:** all phases on `main` (small project, single dev). Each phase = one commit (or a small handful of related commits). Commit messages start with `[v2.6 PN]` (e.g. `[v2.6 P1] Rename spooky-bridge to mutagen-bridge`).
- **Live install sync:** Phases 1–4 each end with a sync to `E:\Skyrim Modding\Authoria - Requiem Reforged\plugins\mo2_mcp\` via `build/build-release.ps1 -SyncLive -MO2PluginDir <path>`. Phase 5 verifies on the synced install. Phase 6 builds the installer.
- **Backups:** Phase 3 archives `esp_reader.py` to `Claude_MO2/dev/archive/v2.6_retired/esp_reader.py` before deletion. Same for any other significant retired files.
- **No partial phases.** If a phase can't complete, the handoff records the partial state and lists what blocks the next phase. Don't half-finish and move on.
- **Don't touch out-of-phase files.** Each phase's "Files to touch" list is exhaustive. If you find yourself wanting to modify something outside that list, that's a sign to stop and escalate to the user.
- **Use `mcp__ccd_session__spawn_task` for out-of-scope nice-to-haves** you spot during work. Don't fold them into your phase.
- **Live testing happens in Phase 5.** Earlier phases verify by build + smoke-test only (e.g. "patcher still creates a non-ESL patch"). The full ESL regression is gated to Phase 5.

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

<List paths the next phase will most need to read. Spare it the orientation cost.>
```

Keep handoffs short — under 400 lines. The plan has the rationale; the handoff is just state-of-play.

---

# PHASES

---

## Phase 0 — Mutagen 0.53.1 read-path probe

**Goal:** Determine whether `Mutagen.Bethesda.Skyrim 0.53.1`'s `SkyrimMod.CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE)` alone applies ESL FormID compaction, or whether compaction only kicks in via `GameEnvironment` + `LinkCache`. This decides Phase 2's scope.

**Why it matters:** if raw overlay reads already produce compacted IDs, Phase 2 is small (mostly write-path changes). If they produce raw IDs, Phase 2 must build a `GameEnvironment` for every read too. Cheap to test, big scope difference.

**Files to touch:** **none in the main repo.** Work happens in `<workspace>/research/Phase0Probe/` which is outside the repo (gitignored / not committed).

### Steps

1. Create `<workspace>/research/Phase0Probe/` with a minimal `.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Exe</OutputType>
       <TargetFramework>net8.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Mutagen.Bethesda.Skyrim" Version="0.53.1" />
     </ItemGroup>
   </Project>
   ```
2. Write `Program.cs` that:
   - Takes one argument: path to `NyghtfallMM.esp`.
   - Calls `using var mod = SkyrimMod.CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE);`.
   - Enumerates the first 10 `mod.MusicTracks` and prints `FormKey` + `EditorID` for each.
   - Also enumerates `mod.MusicTypes` for one specific record (e.g. `MUSReveal` if it exists locally — but this is harder without a LinkCache; just dump MUSTs).
3. Get the path to NyghtfallMM.esp. Aaron's modlist install:
   `E:\Skyrim Modding\Authoria - Requiem Reforged\mods\<some-mod-folder>\NyghtfallMM.esp`. Use Glob to locate; if not found, ask Aaron.
4. Run the probe. Print the output.

### Decision criteria

- **If output shows FormKeys like `000800:NyghtfallMM.esp..000808:NyghtfallMM.esp`** (compacted) → raw overlay reads apply compaction. Phase 2 can keep `CreateFromBinaryOverlay`-style reads, just needs the write-path changes (`BeginWrite.WithLoadOrder`).
- **If output shows FormKeys like `002E55:NyghtfallMM.esp..` (or other large IDs)** → raw overlay does NOT compact. Phase 2 must build a `GameEnvironment` and route reads through `LinkCache` to get compacted IDs.
- **If something else weird happens** (errors, partial output, wrong record types) → record the actual output verbatim in the handoff, escalate to Aaron before starting Phase 1.

### Verification

The probe IS the verification. No tests beyond this.

### Handoff requirements

Write `PHASE_0_HANDOFF.md` with at minimum:
- Verbatim probe console output.
- Explicit statement: "Phase 2 scope = MINIMAL (write-path only)" or "Phase 2 scope = FULL (read + write GameEnvironment-aware)".
- Path to the probe project (so Phase 2 can run it again if useful).
- Confirm Mutagen.Bethesda.Skyrim 0.53.1 was actually resolved by NuGet (not silently substituted with `7.x` — past gotcha).

### Risk / rollback

Zero risk. Probe is throwaway. Nothing in the repo changes.

### Estimated effort

15-30 minutes.

---

## Phase 1 — Rename `spooky-bridge` → `mutagen-bridge`, drop spooky-toolkit ProjectReference

**Goal:** Honest naming + cleaner build graph. The bridge does not use a single line of `SpookysAutomod.*` code (verified at plan time — only `Mutagen.Bethesda.*` and `Noggog` imports). The `spooky-toolkit/` ProjectReferences are vestigial transitive-Mutagen plumbing inherited from v2.0.0.

After this phase, the bridge:
- Lives at `tools/mutagen-bridge/`.
- References `Mutagen.Bethesda.Skyrim 0.53.1` directly via NuGet PackageReference.
- Has no `spooky-toolkit/` dependency.
- Has the same exact JSON contract and behavior as before. **Zero functional change.** This phase is build-graph-only.

**Why this is its own phase:** keeps the rename diff clean and reviewable. Phase 2's substantial code changes don't get tangled with file moves.

**Prereqs from Phase 0:** Confirmation that Mutagen.Bethesda.Skyrim 0.53.1 resolves correctly via NuGet. (Phase 0 verifies this.)

### Files to touch

**Renames (use `git mv` to preserve history):**
- `Claude_MO2/tools/spooky-bridge/` → `Claude_MO2/tools/mutagen-bridge/` (directory rename, all files inside)
- `Claude_MO2/tools/mutagen-bridge/spooky-bridge.csproj` → `Claude_MO2/tools/mutagen-bridge/mutagen-bridge.csproj`

**Edits inside the renamed bridge:**
- `Claude_MO2/tools/mutagen-bridge/mutagen-bridge.csproj`:
  - `<RootNamespace>SpookyBridge</RootNamespace>` → `MutagenBridge`
  - `<AssemblyName>spooky-bridge</AssemblyName>` → `mutagen-bridge`
  - Replace the two `<ProjectReference>` items with:
    ```xml
    <PackageReference Include="Mutagen.Bethesda.Skyrim" Version="0.53.1" />
    ```
- All `.cs` files inside `tools/mutagen-bridge/`: `namespace SpookyBridge` → `namespace MutagenBridge`. Touches `Program.cs`, `Models.cs`, `RecordReader.cs`, `PatchEngine.cs`, `AudioCommands.cs`, `Helpers/FormIdHelper.cs`. Use Edit with `replace_all` once per file or grep-then-edit.
- `using SpookyBridge;` in `Program.cs` → `using MutagenBridge;`.

**Python side:**
- `Claude_MO2/mo2_mcp/tools_patching.py`:
  - `_find_bridge` candidates list: add `plugin_dir / "tools" / "mutagen-bridge.exe"` and `plugin_dir / "tools" / "mutagen-bridge" / "mutagen-bridge.exe"` **as the first two candidates**. Keep the existing `spooky-bridge.exe` candidates as the next two for one-release backward compatibility (a user updating from v2.5.x to v2.6.0 may have the old binary still present until they reinstall).
  - Plugin setting key: rename `"spooky-bridge-path"` → `"mutagen-bridge-path"` in the lookup. Add a fallback: if `mutagen-bridge-path` is unset and `spooky-bridge-path` exists, use it (one-release shim).
  - Rename internal variables `bridge_path` etc. for clarity (not required, but match the new name).
- `Claude_MO2/mo2_mcp/tools_records.py`: same `_find_bridge` changes (it has its own `_run_bridge_read` that also locates the binary).
- `Claude_MO2/mo2_mcp/tools_audio.py`: FUZ tools also call this bridge — same `_find_bridge` updates.

**Build pipeline:**
- `Claude_MO2/build/build-release.ps1`: every reference to `tools\spooky-bridge` or `spooky-bridge.exe` → `tools\mutagen-bridge` / `mutagen-bridge.exe`. Includes:
  - `$BridgeProj = "$RepoRoot\tools\spooky-bridge\spooky-bridge.csproj"` (or equivalent)
  - Output path expectations
  - `-SyncLive` copy logic
  - Any logging strings

**Installer:**
- `Claude_MO2/installer/claude-mo2-installer.iss`: source paths under `tools/spooky-bridge/` → `tools/mutagen-bridge/`. Destination paths in the installed plugin folder also rename to `tools\mutagen-bridge\`.

**Docs:**
- `Claude_MO2/README.md`: any `spooky-bridge` mention → `mutagen-bridge`.
- `Claude_MO2/CLAUDE.md`: shouldn't reference the bridge by name; check anyway.
- `Claude_MO2/KNOWN_ISSUES.md`: any mention.
- `Claude_MO2/kb/KB_Tools.md`: any mention.
- `Claude_MO2/.claude/skills/esp-patching/SKILL.md`: any mention.
- `Claude_MO2/THIRD_PARTY_NOTICES.md`: the Spooky AutoMod Toolkit attribution stays (we still ship `spookys-automod.exe` for non-bridge work). But if it mentions the bridge being built ON Spooky, correct that — we now reference Mutagen directly via NuGet.
- `Claude_MO2/mo2_mcp/CHANGELOG.md`: do NOT touch yet — Phase 6 owns the v2.6.0 entry.

**Files NOT to touch in this phase:**
- Anything in `Claude_MO2/spooky-toolkit/` (the submodule). Stays as-is for `spookys-automod.exe` builds.
- `Claude_MO2/mo2_mcp/tools_papyrus.py`, `tools_archive.py`, `tools_nif.py` — these use `spookys-automod.exe`, not our bridge. Don't conflate.
- `Claude_MO2/mo2_mcp/esp_reader.py`, `esp_index.py` — Phase 3.
- Bridge `.cs` logic — only namespace updates this phase, not behavior.

### Steps

1. **Audit.** Grep for all references that will need updating:
   - `grep -r "spooky-bridge"` in the repo (excluding `spooky-toolkit/`).
   - `grep -r "spooky_bridge"` in the repo (might exist as Python identifier).
   - `grep -r "SpookyBridge"` in the repo (the C# namespace).
   - Confirm the touch list above is complete; if grep finds more, add them.
2. **Rename the directory and csproj** with `git mv`.
3. **Update the csproj contents** (RootNamespace, AssemblyName, replace ProjectReferences with PackageReference).
4. **Update the C# namespace** in every .cs file under `tools/mutagen-bridge/`.
5. **Update Python `_find_bridge` candidate lists** in tools_patching.py / tools_records.py / tools_audio.py.
6. **Add backward-compat shim** for the `spooky-bridge-path` plugin setting (read it if `mutagen-bridge-path` is empty).
7. **Update build pipeline** (build-release.ps1).
8. **Update installer** (claude-mo2-installer.iss).
9. **Update docs** (README, CLAUDE, KNOWN_ISSUES, KB_Tools, esp-patching skill, THIRD_PARTY_NOTICES).
10. **Build the renamed bridge**:
    ```
    cd Claude_MO2 && powershell -File build/build-release.ps1
    ```
    Confirm it builds clean and the output is at `build-output/mutagen-bridge/mutagen-bridge.exe`.
11. **Smoke test:** sync to live install, run one MO2 + Claude Code session, perform a simple `mo2_create_patch` (a one-record override of any vanilla record — e.g., set a keyword on `Skyrim.esm` Iron Sword). Verify it succeeds. **Do NOT test ESL patches yet** — that's Phase 5.
12. **Commit:** `[v2.6 P1] Rename spooky-bridge to mutagen-bridge; drop spooky-toolkit ProjectRef`.
13. **Write `PHASE_1_HANDOFF.md`.**

### Verification

- `dotnet build Claude_MO2/tools/mutagen-bridge/mutagen-bridge.csproj` succeeds with no warnings related to the rename.
- The output binary `build-output/mutagen-bridge/mutagen-bridge.exe` exists.
- A live `mo2_create_patch` of one vanilla record produces an output ESP that opens cleanly in xEdit (no FormLink errors, correct override).
- Grep confirms no `spooky-bridge` strings remain in the bridge build output, build pipeline, installer, or Python `_find_bridge` primary candidates.

### Risk / rollback

Low risk. If the build breaks or smoke test fails, `git revert HEAD` and the previous spooky-bridge build is intact. Worst case is a few hours lost. **Do not delete the old `spooky-bridge` build artifact** in `build-output/` — let `build-release.ps1` overwrite it normally.

### Estimated effort

2-3 hours.

---

## Phase 2 — GameEnvironment + LinkCache + WithLoadOrder in the bridge

**Goal:** Bridge becomes load-order-aware. Every record read goes through `env.LinkCache.TryResolve`. Every patch write goes through `BeginWrite.WithLoadOrder(env.LoadOrder)`. ESL FormID encoding is correct end-to-end.

**Prereqs from Phase 1:** bridge renamed, Mutagen 0.53.1 directly referenced, smoke-tested.
**Prereqs from Phase 0:** decision on minimal vs full scope.

### Files to touch

**Bridge (`tools/mutagen-bridge/`):**
- `Models.cs` — add a new shared type `LoadOrderContext` and embed it as a nullable optional field on `PatchRequest`, `ReadRequest`, `ReadBatchRequest`. Schema:
  ```csharp
  public class LoadOrderContext
  {
      [JsonPropertyName("data_folder")]    public string DataFolder { get; set; } = "";
      [JsonPropertyName("plugins_txt")]    public string PluginsTxt { get; set; } = "";
      [JsonPropertyName("loadorder_txt")]  public string LoadOrderTxt { get; set; } = "";  // accepted but currently unused; Mutagen reads plugins.txt
      [JsonPropertyName("ccc_path")]       public string? CccPath { get; set; }            // optional Skyrim.ccc for CC masters
      [JsonPropertyName("game_release")]   public string GameRelease { get; set; } = "SkyrimSE";
  }
  ```
- New file `EnvironmentFactory.cs` — builds an `IGameEnvironment<ISkyrimMod, ISkyrimModGetter>` from a `LoadOrderContext`. Uses `GameEnvironment.Typical.Builder<ISkyrimMod, ISkyrimModGetter>(GameRelease.SkyrimSE).WithTargetDataFolder(ctx.DataFolder).WithLoadOrder(LoadOrder.GetListings(...)).Build()`. The exact builder spelling for plugins.txt / Skyrim.ccc paths — verify via the cloned Mutagen source at `<workspace>/research/Mutagen/Mutagen.Bethesda.Core/`. If `LoadOrder.GetListings` doesn't accept all the paths we need, fall back to building the listings list manually and passing it to `.WithLoadOrder(IEnumerable<IModListingGetter>)`.
- `PatchEngine.cs` — substantial rewrite:
  - `Process(PatchRequest)` checks `request.LoadOrder` is non-null; returns clean error if absent.
  - Builds a `using var env = EnvironmentFactory.Build(request.LoadOrder);` at the top.
  - Threads `env` through `ProcessOverride` and `ProcessMergeLeveledList`.
  - `ProcessOverride` resolves source via `env.LinkCache.TryResolve<IMajorRecordGetter>(targetFormKey, out var sourceRecord)` instead of `CreateFromBinaryOverlay(op.SourcePath, ...)`. Honors `op.SourcePath` as a "non-winning override" disambiguator via `env.LinkCache.ResolveAllContexts<IMajorRecord, IMajorRecordGetter>(targetFormKey)` filtered by ModKey.
  - Drops the `AddMasterIfMissing` calls. Mutagen's write-time master recomputation handles this when `WithLoadOrder` is set.
  - Replaces the final `patchMod.WriteToBinary(request.OutputPath)` + read-back-masters block (lines 73-86) with:
    ```csharp
    patchMod.BeginWrite
        .ToPath(request.OutputPath)
        .WithLoadOrder(env.LoadOrder)
        .Write();
    var masters = patchMod.ModHeader.MasterReferences
        .Select(m => m.Master.FileName.String).ToList();
    ```
  - Replace raw cast `(SkyrimModHeader.HeaderFlag)0x200` with the named constant `SkyrimModHeader.HeaderFlag.Small` (verified in Phase 0 / pinned Mutagen 0.53.1's source).
  - Same env-routing applies to `ProcessMergeLeveledList` and its three Merge* helpers — they all currently use `CreateFromBinaryOverlay(op.BasePath, ...)` and `CreateFromBinaryOverlay(overridePath, ...)`. Replace with LinkCache resolves.
  - **Don't** delete `AddMasterIfMissing` outright — comment out call sites first; if the test passes after WithLoadOrder, then delete in a follow-up commit within the phase.
- `RecordReader.cs` — `Read` and `ReadBatch`:
  - If `request.LoadOrder` is non-null, route through `env.LinkCache.TryResolve<IMajorRecordGetter>(formKey, ...)`. This automatically gives compacted FormKeys for ESLs (per Phase 0's expected outcome) and resolves cross-mod FormLinks correctly.
  - If `request.LoadOrder` is null, fall back to the existing `CreateFromBinaryOverlay`-per-plugin path. **Keep** this fallback for one release as a safety net, AND for callers that genuinely don't need load-order context (e.g. inspecting one isolated plugin from outside MO2).
  - Note: per Phase 0 outcome, raw `CreateFromBinaryOverlay` may already do compaction for ESLs. If so, the read fallback is also "correct" for ESLs and the env path is just a feature-add (cross-mod resolution). If Phase 0 says raw doesn't compact, then the env path is required for correctness.
- `Program.cs` — no changes needed (handlers already deserialize the per-command request type, which now has the optional load_order field).

**Python side (`mo2_mcp/`):**
- `tools_patching.py`:
  - Add a helper `_build_load_order_context(organizer)` that returns the JSON dict the bridge expects. It needs to determine:
    - `data_folder`: `organizer.managedGame().dataDirectory().absolutePath()` (Qt API — verify exact spelling against MO2's Python plugin API surface; check `__init__.py` for existing examples).
    - `plugins_txt`: `<organizer.profilePath()>/plugins.txt`.
    - `loadorder_txt`: `<organizer.profilePath()>/loadorder.txt`.
    - `ccc_path`: try `<game_root>/Skyrim.ccc` (game root = parent of data folder, or Stock Game folder). Existing `read_ccc_plugins(game_root)` in `esp_index.py` derives this — reuse the path.
    - `game_release`: hardcode `"SkyrimSE"` for now (this is a Skyrim SE plugin).
  - `_handle_create_patch` injects `bridge_request["load_order"] = _build_load_order_context(organizer)` before `subprocess.run`.
- `tools_records.py`:
  - The `_run_bridge_read` calls (single-record and batch) similarly inject `load_order`.
  - This applies to BOTH the patching tool and the record-detail tool. Refactor `_build_load_order_context` to a shared helper if it's not already (probably belongs in `mo2_mcp/__init__.py` or a new `mo2_mcp/_load_order.py`).
- **Don't change `tools_audio.py`** — FUZ commands (`fuz_info`, `fuz_extract`) don't need load-order context.

**Files NOT to touch:**
- `esp_index.py`, `esp_reader.py` — Phase 3.
- Anything in event-driven invalidation (`onPluginStateChanged` handler in `__init__.py`, `trigger_refresh_and_wait_for_index` in `tools_records.py`) — Phase 4.
- Bridge's FUZ commands (`AudioCommands.cs`) — out of scope.

### Steps

1. **Sketch `EnvironmentFactory.Build`** by reading the cloned Mutagen source at `<workspace>/research/Mutagen/Mutagen.Bethesda.Core/Plugins/Environments/` (or wherever `GameEnvironment.Typical.Builder` lives) and confirm the exact fluent method names. Look at `<workspace>/research/Synthesis/Mutagen.Bethesda.Synthesis/Pipeline/SynthesisPipeline.cs` lines around 613-639 for the Synthesis equivalent — that's a working reference.
2. **Add `LoadOrderContext` to `Models.cs`** and embed nullably on the three request types.
3. **Write `EnvironmentFactory.cs`.** Test it compiles.
4. **Rewrite `PatchEngine.Process` + `ProcessOverride`** to use env. Comment out `AddMasterIfMissing` call sites; don't delete the method yet.
5. **Rewrite the three `MergeLeveled*` paths** similarly.
6. **Rewrite `RecordReader.Read` / `ReadBatch`** to optionally use env.
7. **Build the bridge**, fix compile errors. Iterate against the cloned Mutagen for API names.
8. **Write `_build_load_order_context` in Python**, wire into both patching and reads.
9. **Sync to live install.**
10. **Smoke test 1:** non-ESL patch (e.g. add a keyword to a Skyrim.esm armor). Should still work.
11. **Smoke test 2:** the original ESL test — `mo2_create_patch` doing an `override` op against `Skyrim.esm:05221E` (MUSReveal, the test record from Phase 0 / EslReproPatcher). Open in xEdit, confirm Tracks FormLinks resolve to NYReveal01..06. **This is the bug fix proof.**
12. If both pass: delete the commented-out `AddMasterIfMissing` calls (and the method itself if unreferenced).
13. **Commit:** `[v2.6 P2] Bridge uses GameEnvironment + LinkCache + WithLoadOrder; ESL FormID encoding fixed`.
14. **Write `PHASE_2_HANDOFF.md`.**

### Verification

- Bridge builds with no warnings (other than the known `AssemblyVersions` source-generator warning which is harmless).
- Non-ESL patch creation still works on live install.
- ESL patch (the MUSReveal test) produces a `.esp` whose FormLinks resolve in xEdit. **This is the bug-fix smoking gun.**
- A `mo2_record_detail` call on `Skyrim.esm:05221E` returns the NyghtfallMM-overridden version with Tracks at compacted IDs (000884..000889).

### Risk / rollback

Medium-high risk — this is the core change. If it fails, `git revert` returns to the renamed-but-unchanged Phase 1 bridge. Don't delete `esp_reader.py` / `esp_index.py` yet — Phase 3 owns that and they're still functional.

### Estimated effort

6-10 hours over one session. Plan around Mutagen API discovery — most time will go to "what's the exact spelling of method X in 0.53.1."

---

## Phase 3 — Mutagen-authoritative; Python is a thin cache

**Goal:** Every place where Python currently makes its own FormID, VFS, or plugin-state decisions gets deleted. The bridge owns FormID semantics; MO2's API owns plugin-list and VFS semantics; Python keeps a dumb cache of bridge-derived record data for interactive performance. The cache makes no decisions of its own.

**Framing motivation (from Phase 2's discovery):** Phase 2 proved that when Python parallel-implements MO2's domain (plugin-path resolution via alphabetical `mods/` walk vs MO2's priority-based VFS), the two diverge and the discrepancy silently produces wrong outputs. The same pattern exists in every place `esp_reader.py` and `esp_index.py` re-implement Mutagen/MO2 behaviour — raw `<I` FormID reads, hand-rolled implicit-load classification, hand-rolled master-flag detection. Phase 3 eliminates these parallel implementations.

**Prereqs from Phase 2:** bridge has `LoadOrderContext` plumbing; `build_bridge_load_order_context` helper works; `organizer.resolvePath`-backed resolver in place on the Python side.

### Concrete deletions / replacements (Phase 3 session produces a deletion checklist and checks it off)

- `esp_reader.py` — **delete entirely.** Hand-rolled binary parser; replaced by Mutagen via the bridge.
- `esp_index.py:PluginResolver` — **delete.** Phase 2's stop-gap `resolve_fn` injection is replaced by `organizer.modList()` + `organizer.pluginList()` calls that use MO2's actual mod priority order directly. (Phase 2's `PluginResolver` patch is defence-in-depth that lives until Phase 3 deletes the resolver.)
- `esp_index.py:resolve_formid` — **delete.** Mutagen's `FormKey` is the source of truth.
- `esp_index.py:read_active_plugins` — **delete.** Use `organizer.pluginList().pluginStates()` (or equivalent — see "Open questions" below). MO2's API knows which plugins are active.
- `esp_index.py:read_implicit_plugins`, `read_ccc_plugins`, `IMPLICIT_MASTERS` — **delete.** MO2's `pluginList()` already includes implicit-load plugins. The v2.5.7 hand-rolled implementation is no longer needed once the index queries MO2 instead of parsing `plugins.txt` + `Skyrim.ccc` itself.
- The `_PluginCache` / `RecordRef` data classes and the on-disk pickle (`.record_index.pkl`) format — **delete or simplify drastically.** Whatever cache structure remains is populated from bridge scan responses, not raw byte parsing.
- The `_FMT_RECORD`, `_FMT_GRUP`, `_FMT_SUBREC` struct formats — **delete with `esp_reader.py`.**

### What stays in Python after Phase 3

- The MCP tool registrations and request/response shapes (`tools_records.py`, `tools_patching.py`, etc.).
- A thin in-memory cache mapping `(plugin, formid) → {record_type, edid, override_chain_plugins}` populated by the bridge.
- Cache invalidation via plugin-file mtime + `plugins.txt` mtime (Phase 4 implements this).

The bridge gains a new `scan` command that takes a `LoadOrderContext` + optional plugin filter and returns a record table. Python populates its cache from that. All FormIDs in the cache are Mutagen's view → matches xEdit by construction.

### v2.5.6/v2.5.7-functionality migration note

The implicit-load classification, enabled filtering, and `include_disabled` semantics still need to work — but they're now answered from MO2's `pluginList()` rather than from our Python implementation. Behaviour preserved, implementation deleted.

Every Phase 3 session should write a before/after assertion for each of these surface behaviours to confirm nothing regresses:

- `mo2_conflict_summary` default (enabled-only) total_conflicts ≈ v2.5.7 baseline (~428,260 on Aaron's modlist; drops slightly with Phase 2's `PluginResolver` fix to ~427,232).
- `mo2_conflict_summary(include_disabled=true)` returns a larger number than default.
- `mo2_query_records(plugin_name="Skyrim.esm", record_type="MUSC")` returns base-game MUSCs (proves implicit-load classification works).
- `mo2_record_detail(formid="<disabled-plugin-record>")` with default flags returns the "record exists only in disabled plugins" error; with `include_disabled=true` returns the record.

### Open question Phase 3 must answer before committing to full deletion

MO2's Python API surface for the capabilities the deletions depend on has NOT been fully verified yet. Phase 3's first act is to verify via a Python harness:

- Does `organizer.pluginList()` include implicit-load plugins (base ESMs + CC masters from `Skyrim.ccc`) without explicit effort on our part? If YES → delete `read_implicit_plugins` etc. If NO → keep hand-rolled classification but add a note explaining why.
- Is there an equivalent of `organizer.modList().byProfilePriority()` that iterates in the priority order MO2's VFS uses? If YES → use it in place of the alphabetical walk. If NO → keep a shim that sorts mods by `mod_list.priority(name)` and iterates ascending.
- Does the bridge's scan output match what xEdit reports for every record type we care about, or are there edge cases (CELL/WRLD record children, VMAD fragment records, etc.) where the enumeration paths diverge?

Phase 3 falls back to "MO2 API where possible, hand-rolled where required, but never duplicating Mutagen's FormID work" if any of these answer NO.

### Files NOT to touch

- Phase 4 territory: event-driven invalidation handlers. Index is still rebuilt by `mo2_build_record_index` and on `onPluginStateChanged` for now.

### Bridge side (scaffolding for the scan command)

- `Models.cs` — add new request/response types:
  ```csharp
  public class ScanRequest {
      [JsonPropertyName("command")]    public string Command { get; set; } = "scan";
      [JsonPropertyName("load_order")] public LoadOrderContext? LoadOrder { get; set; }
      [JsonPropertyName("plugins")]    public List<string> Plugins { get; set; } = new();   // file paths to scan
      [JsonPropertyName("include_record_types")] public List<string>? IncludeRecordTypes { get; set; }  // optional filter (e.g. ["MUSC","MUST"]); null = all
  }
  public class ScanResponse {
      [JsonPropertyName("success")] public bool Success { get; set; }
      [JsonPropertyName("plugins")] public List<ScannedPlugin> Plugins { get; set; } = new();
      [JsonPropertyName("error")]   public string? Error { get; set; }
  }
  public class ScannedPlugin {
      [JsonPropertyName("plugin_name")]  public string PluginName { get; set; } = "";
      [JsonPropertyName("masters")]      public List<string> Masters { get; set; } = new();
      [JsonPropertyName("is_master")]    public bool IsMaster { get; set; }
      [JsonPropertyName("is_light")]     public bool IsLight { get; set; }
      [JsonPropertyName("is_localized")] public bool IsLocalized { get; set; }
      [JsonPropertyName("records")]      public List<ScannedRecord> Records { get; set; } = new();
      [JsonPropertyName("error")]        public string? Error { get; set; }
  }
  public class ScannedRecord {
      [JsonPropertyName("type")]   public string Type { get; set; } = "";
      [JsonPropertyName("formid")] public string FormId { get; set; } = "";
      [JsonPropertyName("edid")]   public string? EditorId { get; set; }
  }
  ```
- New file `IndexScanner.cs` — iterate `request.Plugins` via `CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE)`, emit one `ScannedRecord` per `mod.EnumerateMajorRecords()` entry with `FormIdHelper.Format(r.FormKey)`.
- `Program.cs` — dispatch `"scan"` command.

### Steps

1. Run the MO2 API discovery harness (the "Open question" verification). Write results into the session doc before touching any deletion.
2. Add `ScanRequest` / `ScanResponse` / `IndexScanner` to the bridge. Compile.
3. Stand up a Python harness to call the bridge's new `scan` command on a small subset (10-20 plugins from Aaron's modlist). Verify the response shape and FormIDs match xEdit for ESL plugins specifically.
4. Rewrite the cache population path in `esp_index.py` (or a successor module) to call the bridge. Deletion checklist: every item listed above, checked off as the rewrite lands.
5. Bump the cache format key — either a new filename (`.record_index.pkl` → `.record_index_v2.pkl`) or a version field with invalidate-on-mismatch.
6. Archive `esp_reader.py` to `Claude_MO2/dev/archive/v2.6_retired/esp_reader.py` before deletion.
7. Sync to live, full rebuild of the index (`mo2_build_record_index(force_rebuild=true)`). Time it; confirm stats are in the same ballpark.
8. Walk the v2.5.6/v2.5.7 regression assertions listed above; every one must pass before commit.
9. Commit: `[v2.6 P3] Index over Mutagen bridge; Python parallel implementations deleted`.
10. Write `PHASE_3_HANDOFF.md`.

### Verification

- Every v2.5.6/v2.5.7 regression assertion above passes.
- All FormIDs returned by Python tools match xEdit for both vanilla and ESL records.
- Index rebuild completes successfully on Aaron's full modlist.
- Cache format invalidation works: an old `.record_index.pkl` (or whatever the new name is) doesn't crash a v2.6 install.

### Risk / rollback

High risk — this is the deepest change. The index is used by every record-facing tool. If it breaks, everything breaks.

Mitigation:
- **Don't delete the archived `esp_reader.py`** — keep it for revert reference.
- Test on a non-live MO2 install if possible before syncing live.
- The "Open question" harness MUST pass for every bullet before committing to the deletions. If any answer is NO, adjust scope rather than plow through.
- Phase 5's regression suite is the final guard; if Phase 3 breaks something subtle, Phase 5 catches it before Phase 6 ships.

### Estimated effort

6-10 hours over one session.

---

## Phase 4 — Retire event-driven index invalidation

**Goal:** Replace `onPluginStateChanged` rebuilds, `trigger_refresh_and_wait_for_index`, and the `mo2_refresh` response coordination with lazy-build + per-query freshness check. Simpler, faster, less bug-farmy.

Post-Phase-3, the freshness check is trivial because the Python cache has nothing to be stale about except plugin file mtimes; no complex "which records changed inside which plugins" logic is required.

**Prereqs from Phase 3:** index is Mutagen-backed.

### Files to touch

**Python (`mo2_mcp/`):**
- `esp_index.py`:
  - Add `ensure_fresh()` method: stats `plugins.txt`, `loadorder.txt`, and every plugin file. For any whose mtime is newer than the cached entry, mark for re-scan. If `plugins.txt`/`loadorder.txt` changed, recompute the active set. Re-scans (just) the changed plugins via the bridge. Returns the list of changed plugins for telemetry.
  - `ensure_fresh()` is fast in the no-changes case (~50-100ms stat walk). Slow only when changes are detected (one bridge call to scan changed plugins).
  - `set_plugin_enabled(plugin, enabled)` stays as an in-place flip (still useful — avoids even the freshness check overhead when MO2 dispatches an `onPluginStateChanged` event we can act on directly).
- `tools_records.py`:
  - At the start of every query handler, call `idx.ensure_fresh()` if `idx.is_built`. If `not idx.is_built`, build it lazily (same behavior as before — error out and ask caller to run `mo2_build_record_index` first, OR build automatically; pick one and document).
  - Decision: **auto-build on first query** if not built, with a clear log line. Removes the "you must call build first" friction. Existing `mo2_build_record_index` and `mo2_record_index_status` tools stay for explicit control.
  - Delete `trigger_refresh_and_wait_for_index` and its supporting state.
- `tools_patching.py`:
  - Remove the `response['mo2_refresh'] = trigger_refresh_and_wait_for_index(organizer)` block.
  - Simplify `next_step`: drop the language about "MO2's plugin-state-change event auto-triggers a record-index rebuild" — that's no longer how it works. Replace with: "Plugin written. To load it in-game, tick its checkbox in MO2's right pane. Once enabled, the next read-back query will pick up the changes automatically." (No more "do NOT chain read-back calls in the same turn" — chaining is now safe.)
- `tools_write.py`, `tools_archive.py`, `tools_papyrus.py`, `tools_audio.py`:
  - Any of these that currently set a `next_step` field needs to be reviewed. Most of them describe non-ESP outputs (loose files, extracted assets, compiled .pex) where there's no MO2 refresh dependency. Simplify the language but keep informative.
- `__init__.py`:
  - The `onPluginStateChanged` handler currently does an in-place flip via `set_plugin_enabled`, falling back to a full rebuild if the plugin isn't in the index. Keep the in-place flip; remove the fallback rebuild. If the plugin isn't in the index, the next query's `ensure_fresh()` will pick it up via mtime check.
  - Same for `onModInstalled` / `onModRemoved` / `onPluginMoved` if any of those trigger rebuilds.

**Files NOT to touch:**
- The bridge — Phase 4 is Python-only.
- `esp_reader.py` — already gone in Phase 3.
- `mo2_record_index_status` — keep its existing reporting, just stop populating the dropped fields.

### Steps

1. **Add `ensure_fresh()` to `esp_index.py`.** Test in isolation: build index, verify nothing changes the second time; touch a plugin file's mtime, verify only that one plugin re-scans.
2. **Wire `ensure_fresh()` into every query handler in `tools_records.py`.**
3. **Auto-build on first query** if not built — implement and document.
4. **Delete `trigger_refresh_and_wait_for_index`** and any other refresh-coordination machinery.
5. **Simplify `tools_patching.py`'s response shape.** Drop `mo2_refresh`. Update `next_step` wording.
6. **Audit other write tools' `next_step`** fields, simplify as needed.
7. **Simplify `__init__.py`'s `onPluginStateChanged`** to in-place flip only.
8. **Sync to live, smoke test:**
   - Create a patch. Confirm response no longer has `mo2_refresh`. Confirm `next_step` reads cleanly.
   - Tick the patch in MO2.
   - Immediately call `mo2_record_detail` on a record in the new patch — should resolve via `ensure_fresh()` picking up the change.
   - Disable a plugin via MO2, call `mo2_query_records` with that plugin's name — should return empty (or `include_disabled=true` workaround).
9. **Commit:** `[v2.6 P4] Lazy build + freshness check; retire event-driven invalidation`.
10. **Write `PHASE_4_HANDOFF.md`.**

### Verification

- Patch creation completes in well under 60s (was sometimes 60-120s with old refresh wait).
- Read-back queries after a patch+enable cycle return data without manual rebuild.
- `mo2_record_index_status` stats look right.
- No tool returns `mo2_refresh`, `last_auto_refresh`, or related fields anymore.

### Risk / rollback

Medium risk. The freshness-check logic must be correct or we'll silently serve stale data. Test thoroughly. `git revert` brings back the event-driven model.

### Estimated effort

3-5 hours.

---

## Phase 5 — Live regression on Aaron's modlist

**Goal:** Verify everything works end-to-end on a real modlist before shipping. **No code changes** in this phase (other than emergency fixes for regressions found).

**Prereqs from Phase 4:** all migration phases complete and synced live.

### Test matrix

For each test, record: command, response, xEdit verification (where relevant), pass/fail.

**T1 — Original bug repro (THE headline regression test):**
- Re-execute the 2026-04-21 MUSC merge workflow:
  - `mo2_query_records` for vanilla MUSCs (in Skyrim.esm and DLCs).
  - For each affected MUSC, fetch both versions' Tracks via `mo2_record_detail(formid, plugin_names=[origin_master, winner_plugin])`.
  - Compute the union: `new_tracks = winner_tracks + (vanilla_tracks - tracks_already_in_winner)`.
  - Single `mo2_create_patch` call writing all overrides.
- Open the resulting `.esp` in xEdit. **Pass = every Track FormLink resolves cleanly.** Fail = any "Could not be resolved" entry.

**T2 — Non-ESL patch (regression check):**
- `mo2_create_patch` with a single override op on a vanilla NPC (e.g. add a faction). No ESL involvement.
- Open in xEdit, confirm the override is correct and FormLinks resolve.

**T3 — Leveled list merge across ESL + non-ESL plugins:**
- Pick a vanilla LVLI with at least one ESL-plugin override and one non-ESL-plugin override in the modlist. Run `merge_leveled_list`.
- Open in xEdit, confirm entries from both sources are present and their FormLinks resolve.

**T4 — Record detail on an ESL plugin's own record:**
- `mo2_record_detail(formid="NyghtfallMM.esp:000884", plugin_name="NyghtfallMM.esp")`.
- Confirm it returns NYReveal01's data, with the FormID rendered as `000884` (compacted) — not `002E55`.

**T5 — Index FormIDs match xEdit:**
- Pick 10 records from random plugins (mix of ESM, ESP, ESL).
- For each: `mo2_query_records` with EDID filter, then verify the returned FormID exactly matches what xEdit displays for the same record.

**T6 — Lazy build + freshness check:**
- Restart MO2 + Claude Code.
- First query: `mo2_query_records(plugin_name="Skyrim.esm", record_type="MUSC")`. Confirm it auto-builds the index.
- Disable a plugin in MO2's right pane.
- Re-run the query. Confirm the disabled plugin's records drop from default results.
- Re-enable. Re-run. Confirm they come back.

Note: some v2.5.6/v2.5.7 regression scenarios (implicit-load classification, enabled-filter behaviour) now exercise MO2 API integration rather than our parser. Test the same surface-level outcomes; the implementation underneath has changed.

**T7 — `mo2_create_patch` workflow with the new `next_step`:**
- Create a patch.
- Read the response. Confirm `next_step` is informative and the `mo2_refresh` field is absent.
- Tick the patch in MO2.
- Immediately call `mo2_record_detail` on a record in the patch — confirm it resolves without explicit `mo2_build_record_index` call.

**T8 — Conflict summary sanity:**
- `mo2_conflict_summary()` — total_conflicts in the same ballpark as v2.5.7's known good (~428,260 for Aaron's modlist).
- Compare top-overriding plugins to the v2.5.7 baseline. Should be similar (slight shifts from now-correctly-resolved ESL records are okay).

**T9 — Original session's "FIXED" reference patch:**
- The hand-built `Vanilla Music Restored - FIXED.esp` in the modlist still works. Confirm it doesn't conflict with our new merge patch in unexpected ways. (Optional — may want to disable it during T1 to avoid double-overriding.)

### Steps

1. **Pre-flight:** confirm live install matches repo (`git status` clean; `cmp` spot check on key files).
2. **Run each test in T1–T9.** Document results in the handoff.
3. **If any test fails:** identify which phase introduced the regression. Pause Phase 5; the broken phase needs a fix-up (in its own commit, NOT a new phase). Re-test the affected test only.
4. **If all tests pass:** write `PHASE_5_HANDOFF.md` with the test matrix results, declare ready to ship, and Phase 6 may proceed.

### Verification

The test matrix IS the verification.

### Risk / rollback

This phase is read-only on the codebase (no edits). The risk is in finding regressions — but finding them now is cheap; finding them post-ship is expensive. Take time.

### Estimated effort

2-4 hours.

---

## Phase 6 — Ship v2.6.0

**Goal:** Public release. v2.6.0 installer on GitHub. CHANGELOG updated. Docs reflect new state.

**Prereqs from Phase 5:** all tests pass.

### Files to touch

- `Claude_MO2/mo2_mcp/config.py`: `PLUGIN_VERSION = (2, 6, 0)`.
- `Claude_MO2/mo2_mcp/CHANGELOG.md`: new top entry. **Roll v2.5.6 + v2.5.7 + v2.6.0 changes into one v2.6.0 entry** (since v2.5.6/v2.5.7 were never publicly released — see "Live state at plan creation" above). Cover:
  - **The big change:** Mutagen-backed bridge with load-order awareness. ESL FormID compaction now correct. (Aaron-facing summary, not Mutagen-API-detailed.)
  - **Naming:** `spooky-bridge` → `mutagen-bridge`. One-release backward-compat shim for the `spooky-bridge-path` plugin setting.
  - **Index:** rewritten over Mutagen; FormIDs now match xEdit for ESL records.
  - **Lazy index:** event-driven invalidation removed; freshness check on every query.
  - **Patch response shape:** `mo2_refresh` field gone; `next_step` simplified.
  - **From v2.5.6** (rolled in): `set_fields` on `ExtendedList<T>` works; honest `success` reporting; subprocess windows hidden; record index tracks enabled state with `include_disabled` opt-in.
  - **From v2.5.7** (rolled in): implicit-load plugins (base masters + Creation Club) correctly classified; `errors` list passes through `mo2_record_index_status`; `force_rebuild=true` actually rebuilds.
  - Migration notes for upgraders.
- `Claude_MO2/README.md`:
  - Installer filename URL: `claude-mo2-setup-v2.5.5.exe` → `claude-mo2-setup-v2.6.0.exe`.
  - "Mutagen v0.53.1" mention if relevant (currently mentions v0.52.0).
- `Claude_MO2/KNOWN_ISSUES.md`:
  - Add ESL-FormID-correct note to the resolved-bugs table.
  - Remove any wording that suggests ESL handling is incomplete.
- `Claude_MO2/.claude/skills/esp-patching/SKILL.md`:
  - If any guidance was added about ESL caveats during v2.5.6/v2.5.7, remove it (no longer applies).
- `Claude_MO2/installer/claude-mo2-installer.iss`:
  - `AppVersion=2.5.5` → `AppVersion=2.6.0`.
  - Output filename version string.
- `Claude_MO2/build/build-release.ps1`:
  - Any version strings.
- `Claude_MO2/THIRD_PARTY_NOTICES.md`:
  - The Spooky toolkit attribution still applies (we still ship `spookys-automod.exe` for Papyrus/BSA/NIF/Audio non-FUZ work).
  - Add Mutagen attribution if Phase 1 didn't already (we're now NuGet-direct on Mutagen).

**Files NOT to touch:**
- Anything code-behavioral. This is a release prep phase only.

### Steps

1. Bump `PLUGIN_VERSION`.
2. Write the v2.6.0 CHANGELOG entry. Be thorough — this is the version where v2.5.6, v2.5.7, and v2.6.0's content lands publicly all at once.
3. Update README installer URL.
4. Update KNOWN_ISSUES resolved bugs.
5. Update installer .iss version strings.
6. Build:
   ```
   cd Claude_MO2
   .\build\build-release.ps1 -BuildInstaller
   ```
7. Confirm output: `build-output/installer/claude-mo2-setup-v2.6.0.exe`.
8. Sync the binary to a fresh test location, run the installer (NOT into the live MO2 — into a sandbox folder), verify it lands the expected files.
9. Sync built binaries to live MO2 install via `-SyncLive`. Confirm Aaron's MO2 still runs the plugin successfully (pre-installer-test is enough; installer-on-live is unnecessary risk).
10. **Commit:** `[v2.6 P6] v2.6.0 release: Mutagen-backed bridge, lazy index, ESL FormIDs correct end-to-end`.
11. **Tag:** `git tag -a v2.6.0 -m "v2.6.0 release"`.
12. **Push to GitHub** (with tags): `git push origin main --tags`.
13. **Create GitHub release:**
    - Title: `v2.6.0 — Mutagen-backed bridge, ESL FormIDs end-to-end`
    - Body: condensed CHANGELOG entry.
    - Attach `claude-mo2-setup-v2.6.0.exe` as the release asset.
14. **Write `PHASE_6_HANDOFF.md`** marking the migration complete. The handoff doubles as the session-summary equivalent for this work stream.

### Verification

- Public release exists: `https://github.com/Avick3110/Claude_MO2/releases/tag/v2.6.0`.
- Release asset downloads and installs.
- `git log` shows clean phase-by-phase history.

### Risk / rollback

Low risk if Phase 5 passed. If a problem surfaces post-release, it's a hotfix scenario (v2.6.1) — don't try to unpublish.

### Estimated effort

2-3 hours.

---

## 📦 Cleanup (post-Phase 6, optional)

Not part of any phase, but worth considering after v2.6.0 ships:

- `<workspace>/research/` cleanup: archive the EslReproPatcher, Mutagen clone, Synthesis clone, Phase0Probe. They've served their purpose.
- `<workspace>/music_merge_payload.json` and `music_merge_records.json` — diagnostic artifacts from the original 2026-04-21 session. Archive to `<workspace>/archive/` or delete.
- `Claude_MO2/dev/archive/v2.6_retired/esp_reader.py` — keep for one release as a reference; delete in v2.7 if unneeded.

---

## ✏️ Plan revisions

If a phase finds the plan wrong (file paths shifted, API changed, etc.), update this PLAN.md as part of that phase's commit. Note the revision in the phase handoff. The plan is a living document until Phase 6 ships.

---

End of plan.
