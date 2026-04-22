# Third-Party Notices

This project includes or depends on the following third-party software:

---

## pefile (v2024.8.26)

**Bundled as:** `mo2_mcp/pefile.py` and `mo2_mcp/ordlookup/`

Copyright (c) 2005-2024 Ero Carrera <ero.carrera@gmail.com>

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

**Source:** https://github.com/erocarrera/pefile

**Modifications:** `mmap` and `ordlookup` imports wrapped in try/except for compatibility with MO2's embedded Python environment.

---

## Mutagen.Bethesda.Skyrim (v0.53.1)

**Linked by:** `tools/mutagen-bridge/mutagen-bridge.exe` (NuGet dependency, direct `PackageReference`)

Copyright (c) Noggog and Mutagen contributors

MIT License

The full license text is reproduced above under the pefile section and applies identically.

**Source:** https://github.com/Mutagen-Modding/Mutagen

**Usage:** ESP/ESM/ESL reading and writing — field interpretation, override record creation, FormID management, subrecord serialization. Linked directly into `mutagen-bridge.exe` via NuGet (v2.6+ dropped the indirection through Spooky's toolkit). Upgraded from 0.52.0 → 0.53.1 to get correct ESL FormID compaction on the write path (`BeginWrite.WithLoadOrder`).

---

## Spooky's AutoMod Toolkit (v1.11.1)

**Bundled as:** `tools/spooky-cli/` (CLI binary `spookys-automod.exe` and dependencies).

Copyright (c) SpookyPirate and contributors

MIT License

The full license text is reproduced above under the pefile section and applies identically.

**Source:** https://github.com/SpookyPirate/spookys-automod-toolkit

**Usage:**
- **Spooky CLI subprocess** — BSA/BA2 list/extract/validate, NIF info/list-textures/shader-info, audio format probing (XWM/WAV — FUZ handled by our own bridge parser). Papyrus compile calls `PapyrusCompiler.exe` directly, bypassing Spooky's wrapper.

**Note:** v2.0.0 through v2.5.7 also linked `SpookysAutomod.Esp.dll` as a library reference from the bridge (then named `spooky-bridge.exe`). v2.6.0 drops that dependency — the bridge (renamed `mutagen-bridge.exe`) now references `Mutagen.Bethesda.Skyrim` directly via NuGet. Verified at rename time: the bridge's C# source never used any `SpookysAutomod.*` API, only `Mutagen.Bethesda.*` and `Noggog`. The Spooky toolkit is still bundled for the CLI subprocess workloads above.

---

## System.CommandLine, System.Text.Json, SharpCompress (NuGet dependencies of mutagen-bridge.exe)

These are standard MIT-licensed .NET libraries pulled in as transitive dependencies of `mutagen-bridge.exe` via NuGet. See `tools/mutagen-bridge/mutagen-bridge.csproj` for exact versions.

Source URLs:
- System.CommandLine — https://github.com/dotnet/command-line-api
- System.Text.Json — https://github.com/dotnet/runtime
- SharpCompress — https://github.com/adamhathcock/sharpcompress

All three are MIT-licensed; see their respective repositories for full license text.

---

## External tools — user provides, NOT redistributed

The following are referenced by Claude MO2 but NOT bundled with it. Users install them separately:

- **BSArch.exe** — ships inside [xEdit](https://github.com/TES5Edit/TES5Edit)'s release archive. Required for BSA/BA2 tools.
- **nif-tool.exe** — a separate Rust binary referenced by Spooky's NIF CLI. Required for `mo2_nif_list_textures` / `mo2_nif_shader_info`.
- **PapyrusCompiler.exe + Scripts.zip** — part of the Creation Kit (Bethesda proprietary). Required for `mo2_compile_script`.

Claude MO2 does not redistribute any of these. Users obtain them directly from their respective sources.
