Place PapyrusCompiler.exe (and its supporting files) in this folder.

Claude MO2's Papyrus compile tool (mo2_compile_script) requires Bethesda's
PapyrusCompiler.exe, which ships with the Creation Kit. We cannot bundle it
because it is Bethesda proprietary.

Where to get PapyrusCompiler:

  1. Install the Creation Kit from Steam or Bethesda.net.
  2. Locate PapyrusCompiler.exe inside your CK install:
     <Skyrim SE>\Papyrus Compiler\PapyrusCompiler.exe
  3. Copy the entire "Papyrus Compiler" folder contents into this folder.

Expected path after you're done:

  <this-folder>\PapyrusCompiler.exe
  <this-folder>\TESV_Papyrus_Flags.flg
  (and any DLL dependencies that ship with it)

You also need the base-Skyrim Papyrus script sources (Scripts.zip) for compiles
to resolve base types like Actor, Quest, Debug, etc. Extract Scripts.zip into
an MO2 mod so the VFS includes those sources. This file also ships with the
Creation Kit, typically at:

  <Skyrim SE>\Data\Scripts.zip

If these prerequisites aren't present, compile attempts will still run but will
fail with "unknown type" errors on base-Skyrim types. Install the Creation Kit
to resolve this.

Claude MO2 does not include a Papyrus decompiler — no currently-available
decompiler produces clean round-trip-safe output. If you need to decompile an
existing .pex, use Champollion standalone (https://github.com/Orvid/Champollion)
and manually review its output before any recompile.
