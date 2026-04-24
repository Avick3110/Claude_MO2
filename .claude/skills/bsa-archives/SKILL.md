---
description: List, extract, and validate BSA/BA2 archives via mo2_list_bsa, mo2_extract_bsa, mo2_extract_bsa_file, mo2_validate_bsa. Requires BSArch.exe (user-provided, ships inside xEdit's release archive). Use when the user wants to extract files from a mod's BSA, inspect what's packed in an archive, pull one asset out of a BSA, verify archive integrity, or access scripts/meshes/textures that are packed into a BSA rather than loose.
---

# BSA / BA2 Archives

All four tools shell out through Spooky's CLI to `BSArch.exe`. BSArch is user-provided — extract from xEdit's release archive, then either point the v2.7.0 installer's Optional Tools page at it (the installer copies it into the plugin dir for you) or drop it manually at `<plugin>/tools/spooky-cli/tools/bsarch/bsarch.exe`.

**`mo2_list_bsa`** — List contents of an archive.
- Params: `archive_path`, `filter` (glob, optional), `limit` (default 500, 0 for all)

**`mo2_extract_bsa`** — Extract files matching a filter. The filter is **required** to prevent accidental full-archive dumps (can be 2+ GB).
- Params: `archive_path`, `filter` (required glob, e.g., `*.nif`, `textures/*`), `output_subdir`
- Output goes to `{output_mod}/{archive_basename}/` by default
- Internally full-extracts to a temp dir then filters on our side (upstream Spooky's `--filter` is ignored; we work around it). Cleanup is automatic.

**`mo2_extract_bsa_file`** — Pull one specific file. Preferred over bulk extract when only one asset is needed (e.g., extracting a single script from a mod's BSA).
- Params: `archive_path`, `file_in_archive`
- Writes to the configured output mod preserving archive path

**`mo2_validate_bsa`** — Integrity report.
- Params: `archive_path`
- Reports format version, corrupt entries, unreadable files

## Notes

- Archive paths are VFS-resolved — pass the game-relative path (e.g., `Skyrim - Misc.bsa`) and the tool finds the providing mod.
- Once `BSArch.exe` is installed, Spooky's CLI reports `{"success": true, "bsarchPath": "..."}` from `spookys-automod archive status --json`.
- If a user needs the scripts inside a mod's BSA for analysis (e.g., to read `.psc` sources), use `mo2_extract_bsa` with a `*.psc` filter, then read the extracted files via `mo2_read_file`.
