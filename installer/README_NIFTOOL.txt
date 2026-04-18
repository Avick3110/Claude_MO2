Place nif-tool.exe in this folder.

Claude MO2's NIF texture/shader tools (mo2_nif_list_textures, mo2_nif_shader_info)
require nif-tool.exe — a Rust binary maintained by the Spooky AutoMod Toolkit team.

It is NOT bundled with Claude MO2 because its license is not yet confirmed as
MIT-compatible for redistribution.

Where to get nif-tool:

  Download the release archive from Spooky's AutoMod Toolkit:
  https://github.com/SpookyPirate/spookys-automod-toolkit/releases

  Extract the archive and copy nif-tool.exe from tools\nif-tool\ into this folder.

Expected path after you're done:

  <this-folder>\nif-tool.exe

The library-native NIF tool (mo2_nif_info) works WITHOUT nif-tool.exe. Only the
texture listing and shader inspection tools need it.

Note: this requirement may disappear in a future release if we confirm the
license terms and add nif-tool to the installer bundle.
