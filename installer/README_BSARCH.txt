Place BSArch.exe in this folder.

Claude MO2's BSA/BA2 tools (mo2_list_bsa, mo2_extract_bsa, mo2_extract_bsa_file,
mo2_validate_bsa) require BSArch.exe, which is not bundled with Claude MO2.

Where to get BSArch:

  BSArch ships inside xEdit's release archive on GitHub:
  https://github.com/TES5Edit/TES5Edit/releases

  Download the latest release .7z, extract it, and copy BSArch.exe (or BSArch64.exe,
  renamed to bsarch.exe) into this folder.

Expected path after you're done:

  <this-folder>\bsarch.exe

Claude MO2 will auto-detect BSArch here on the next server start. If the file is
not present, the BSA tools will return a clear error pointing you back here.

License:

  BSArch is distributed under the Mozilla Public License 2.0. By downloading and
  using it, you agree to its terms. See the xEdit release for license details.
