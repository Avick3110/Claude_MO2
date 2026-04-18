---
description: Compile user-provided Papyrus source (.psc) into .pex using mo2_compile_script. Requires Creation Kit (for PapyrusCompiler.exe + base-Skyrim script sources in Scripts.zip). Use when the user wants to compile a Papyrus script, deploy a modified script, or build a .pex for a patch. Does NOT cover script reading/analysis (see mod-dissection) or decompilation (not supported — Champollion's round-trip output is unreliable).
---

# Papyrus Compilation

**`mo2_compile_script`** — Compile a user-provided `.psc` into `.pex`.

- Params: `script_name`, `source` (the .psc text)
- Output lands in `Claude Output/Scripts/<name>.pex`

## Prerequisites

Requires the Creation Kit installed and its `Scripts.zip` extracted into a mod MO2 sees, so the VFS includes base-Skyrim script headers (`Actor`, `Quest`, `Debug`, etc.). Without these, any user script that extends a base class or calls a base-Skyrim function will fail with "unknown type" errors.

The plugin invokes `PapyrusCompiler.exe` directly (not via Spooky's CLI), so a user-provided binary at `<plugin>/tools/spooky-cli/tools/papyrus-compiler/PapyrusCompiler.exe` is required.

## Notes

- Decompile is intentionally not included in the plugin. No currently-available decompiler produces clean round-trip output (Champollion loses operator precedence, drops float casts, misses CustomEvents, mis-tags events as functions, strips fragment comment wrappers). Users who need to decompile a `.pex` should use Champollion standalone and review output manually before any recompile.
- For analyzing script behavior from source, read the `.psc` directly via `mo2_read_file` with `offset`/`limit` to avoid full-file reads. See the `mod-dissection` skill for script reading strategy and the script health check workflow.
- Compile error messages pass through from Spooky's CLI response as the raw `cli_result` field.
