---
description: Inspect NIF mesh files via mo2_nif_info (format/version metadata), mo2_nif_list_textures (texture paths referenced by the mesh), and mo2_nif_shader_info (BSLightingShaderProperty flags per block). `list_textures` and `shader_info` require nif-tool.exe (user-provided, ships in Spooky's v1.11.1 release). Use when the user asks about NIF format details, missing texture references, shader flags, lighting/material issues, or is auditing a mod's meshes for texture-path correctness.
---

# NIF Meshes

**`mo2_nif_info`** — Format metadata (version, file size, header string). Library-native — works without any external tool.
- Params: `path`

**`mo2_nif_list_textures`** — Every texture path referenced by the NIF. Useful for auditing missing-texture references or pattern-matching texture prefixes across a mod.
- Params: `path`
- Requires `nif-tool.exe` at `<plugin>/tools/spooky-cli/tools/nif-tool/nif-tool.exe`

**`mo2_nif_shader_info`** — Shader flags per `BSLightingShaderProperty` block. Useful for debugging lighting/material issues.
- Params: `path`
- Requires `nif-tool.exe`

## Notes

- `nif-tool.exe` is a Rust binary from the Spooky team; its license is undetermined, so we don't redistribute. Users extract it from [Spooky's v1.11.1 release](https://github.com/SpookyPirate/spookys-automod-toolkit/releases) and place at the path above.
- NIF paths are VFS-resolved — pass game-relative paths (e.g., `meshes/armor/iron/cuirass.nif`).
- `mo2_nif_info` still works even when `nif-tool.exe` is missing — it's the only library-native NIF operation.
