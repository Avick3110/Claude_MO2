---
description: Compile user-provided Papyrus source (.psc) into .pex using mo2_compile_script. Requires Creation Kit (for PapyrusCompiler.exe + base-Skyrim script sources in Scripts.zip). Use when the user wants to compile a Papyrus script, deploy a modified script, or build a .pex for a patch. Does NOT cover script reading/analysis (see mod-dissection) or decompilation (not supported — Champollion's round-trip output is unreliable).
---

# Papyrus Compilation

**`mo2_compile_script`** — Compile a user-provided `.psc` into `.pex`.

- Params: `script_name`, `source` (the .psc text)
- Output lands in `Claude Output/Scripts/<name>.pex`

## Prerequisites

Requires the Creation Kit installed for `PapyrusCompiler.exe` and its base-Skyrim script headers (`Actor`, `Quest`, `Debug`, etc.). Without those headers any user script that extends a base class or calls a base-Skyrim function will fail with "unknown type" errors.

**`PapyrusCompiler.exe` discovery (priority order):**
1. `tool_paths.json["papyrus_compiler"]` (JSON-reference mode — set via the v2.7.0 installer's PapyrusCompiler row with the "Reference this path at runtime" checkbox checked, or by editing the JSON directly).
2. `<plugin>/tools/spooky-cli/tools/papyrus-compiler/PapyrusCompiler.exe` (copy mode — the v2.7.0 installer's default; can also be dropped manually).
3. Legacy fallbacks at `%USERPROFILE%/Documents/tools/papyrus-compiler/...` for users who let Spooky auto-download the compiler.

**Base-Skyrim script headers** (used by `_collect_header_dirs()` for `-i` import paths):
- VFS-aggregated `Source/Scripts` dirs across all active mods (the standard path — extract `Scripts.zip` into a mod MO2 sees).
- `tool_paths.json["papyrus_scripts_dir"]` is **additive** to the VFS list — point it at a non-MO2-managed extraction (e.g. `<Steam>\Skyrim Special Edition\Data\Source\Scripts`) to supplement VFS-derived dirs without setting up a Scripts mod.

## Notes

- Decompile is intentionally not included in the plugin. No currently-available decompiler produces clean round-trip output (Champollion loses operator precedence, drops float casts, misses CustomEvents, mis-tags events as functions, strips fragment comment wrappers). Users who need to decompile a `.pex` should use Champollion standalone and review output manually before any recompile.
- For analyzing script behavior from source, read the `.psc` directly via `mo2_read_file` with `offset`/`limit` to avoid full-file reads. See the `mod-dissection` skill for script reading strategy and the script health check workflow.
- Compile error messages pass through from Spooky's CLI response as the raw `cli_result` field.
