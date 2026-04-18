# Known Issues & Limitations

Current as of v2.4.0. These are known limitations, not bugs — all reported bugs have been resolved. For the full version history see `mo2_mcp/CHANGELOG.md`.

---

## User-provided prerequisites

These are by design — we don't bundle proprietary or license-undecidable tools. Missing any of these disables only the capabilities that depend on them; everything else continues to work.

### Papyrus compilation requires Creation Kit

`mo2_compile_script` uses Bethesda's `PapyrusCompiler.exe` and needs the base-Skyrim script sources (`Scripts.zip` ships inside the Creation Kit install). Without them, the compiler fails with "unknown type" errors on SKSE's and base-Skyrim's `.psc` files whenever your script extends `Actor` / `Quest` / etc. or calls anything like `Debug.Notification`.

**Workaround:** Install Creation Kit. Extract `Scripts.zip` into a mod that MO2 sees, so the VFS includes the base headers.

**Impact:** Affects `mo2_compile_script` only. All other MCP tools work without the Creation Kit.

### BSA tools require BSArch.exe

`mo2_list_bsa`, `mo2_extract_bsa`, `mo2_extract_bsa_file`, and `mo2_validate_bsa` shell out through Spooky's CLI to `BSArch.exe`, which we do not redistribute.

**Where to get it:** BSArch ships inside [xEdit](https://github.com/TES5Edit/TES5Edit)'s release archive. Extract it and place `bsarch.exe` at `<plugin>/tools/spooky-cli/tools/bsarch/bsarch.exe`.

**Impact:** Those four archive tools fail clearly until BSArch is installed. Everything else works.

### NIF extras require nif-tool.exe

`mo2_nif_list_textures` and `mo2_nif_shader_info` invoke `nif-tool.exe`, a Rust binary created for Spooky's toolkit. Its license is currently undetermined, so we don't redistribute it.

**Where to get it:** Shipped in Spooky's v1.11.1 release 7z. Place at `<plugin>/tools/spooky-cli/tools/nif-tool/nif-tool.exe`.

**Impact:** Those two tools fail with guidance if the binary is missing. `mo2_nif_info` works without it (library-native via Spooky).

---

## Design-trade-off limitations

### Leveled list merges require user judgment

`mo2_create_patch` can merge LVLI / LVLN / LVSP entries across conflicting plugins, but the **base plugin** (whose records are used as-is) must be chosen by the caller. For an overhaul conflict with a content mod, using the vanilla master as base would revert the overhaul's intentional restructuring (deleveling, reweighting) — you want the overhaul as the base and the content mod's unique entries merged in.

See `kb/KB_LeveledListPatching.md` for the reasoning framework.

### Spell conditions apply at effect level, not record level

The bridge refuses `add_conditions` on SPEL records with "Record type Spell does not support conditions." This matches Mutagen's model — Skyrim spells carry conditions per magic effect, not on the spell record itself. To condition a spell, attach the condition to its MGEF (magic effect) record instead.

### RecordReader depth limit

`mo2_record_detail` walks Mutagen object graphs with a depth limit of 6 via reflection. Most records fit easily; extremely deep QUST/PACK/CELL structures could truncate as `"...[max depth reached]"`. If you encounter this, the depth is tunable in the bridge source (`ReadRequest.MaxDepth`).

### `mo2_record_detail` FormID resolution is opt-in

By default, FormIDs in the output are rendered as `Plugin:HexID`. Pass `resolve_links: true` to annotate each with its EditorID via the record index (`"Skyrim.esm:000019"` → `"Skyrim.esm:000019 (NordRace)"`). Opt-in because the extra lookup takes time on large records and most callers don't need it.

---

## Environmental quirks (not code bugs, but worth knowing)

- **Claude Code caches the MCP tool list at session start.** If you start the server in MO2 mid-session, Claude Code doesn't see the new tools until you restart Claude Code.
- **MO2 doesn't reload Python modules on server stop/start.** After editing any `.py` inside the plugin, delete `__pycache__/` AND fully restart MO2 (not just the Tools > Start/Stop Claude Server toggle).
- **Losing the MCP server during MO2 restart breaks the Claude Code connection** for that session. Restart Claude Code to rediscover. Known Claude Code limitation, not fixable on our side.

---

## Upstream (Spooky) issues we work around

These are reported or reportable to Spooky upstream; our wrappers already work around them:

- **`archive extract --filter` is ignored upstream.** Our `mo2_extract_bsa` full-extracts to a temp dir then filters on our side. Disk-usage trade-off for correctness; cleanup is automatic.
- **`audio info` rejects valid FUZ files.** Our bridge includes a local FUZ parser (`AudioCommands.cs`) so `mo2_audio_info` and `mo2_extract_fuz` don't depend on Spooky's broken path. XWM/WAV still go through Spooky's CLI.
- **`tools/` resolution is 5-up from the CLI exe.** Spooky's CLI looks for external tools at an unusual relative path. Our direct `PapyrusCompiler.exe` invocation sidesteps this, but if Spooky CLI is ever used directly, the user should be aware.

---

## Resolved bugs (history)

| Bug | Fixed in |
|-----|----------|
| `mo2_query_records` int/str type error | v1.0.1 |
| `mo2_list_files` VFS leak | v1.0.1 / v1.0.6 |
| `mo2_find_conflicts` parameter confusion | v1.0.1 |
| Editor ID filter timeouts | v1.0.1 |
| Record index build encoding error | v1.0.1 |
| `mo2_list_files` root path empty | v1.0.6 |
| `mo2_read_file` no pagination | v1.0.6 |
| ESP writer master-limit (258 masters → invalid ESP) | v2.0.0 (Mutagen bridge) |
| `add_inventory` broken on Container records | v2.0.0 |
| CopyAsOverride missing ~25 record types | v2.0.0 |
| Localized strings returned as `[lstring ID]` placeholders | v2.0.0 (Mutagen reads STRINGS files) |
| VMAD fragments rendered as raw hex | v2.0.0 |
| `mo2_record_detail` lost nested enumerable-of-byte blobs as int arrays | v2.4.0 |
| `ConditionGlobal` produced NULL reference in xEdit | v2.4.1 (reflection fix) |
| BSA extract ignored filter, storming subprocesses | v2.4.1 (full-extract-then-filter) |
| FUZ parser rejected valid files | v2.4.1 (local bridge parser) |
| Champollion tool-path resolution broken on live install | v2.4.1 (5-up placement) |
