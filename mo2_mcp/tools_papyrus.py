# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""MCP tool for Papyrus script compilation.

Wraps Bethesda's `PapyrusCompiler.exe` (ships with the Creation Kit) to compile
user-supplied `.psc` source into `.pex` bytecode. Headers (base-Skyrim and
SKSE script sources) are resolved from MO2's VFS, so imports work against the
active load order.

We call `PapyrusCompiler.exe` directly rather than going through Spooky's CLI
wrapper because the Bethesda compiler natively accepts semicolon-separated
`-import=dir1;dir2;...` paths, which is the only way to span MO2's VFS merge
of `Scripts/Source/` across many contributing mod folders. Spooky's wrapper
pre-validates each import with `Directory.Exists`, which fails on the joined
string and silently drops the import list.

Decompilation is intentionally not provided: no currently-available Papyrus
decompiler produces clean round-trip-safe output, and shipping an unreliable
tool is worse than pointing users at manual alternatives.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path

from PyQt6.QtCore import qInfo

import mobase

from .config import PLUGIN_NAME


# PapyrusCompiler discovery


def _find_papyrus_compiler() -> Path | None:
    """Locate PapyrusCompiler.exe.

    Spooky's `papyrus download` drops the Bethesda Original Compiler under
    %USERPROFILE%\\Documents\\tools\\papyrus-compiler\\papyrus-compiler\\Original Compiler\\.
    We check that (plus a couple of near-variants).
    """
    home = Path(os.environ.get("USERPROFILE", os.path.expanduser("~")))
    candidates = [
        home / "Documents" / "tools" / "papyrus-compiler" / "papyrus-compiler" / "Original Compiler" / "PapyrusCompiler.exe",
        home / "Documents" / "tools" / "papyrus-compiler" / "Original Compiler" / "PapyrusCompiler.exe",
        home / "Documents" / "tools" / "papyrus-compiler" / "PapyrusCompiler.exe",
    ]
    for p in candidates:
        if p.is_file():
            return p
    return None


def _find_flags_file(papyrus_exe: Path | None) -> Path | None:
    """The -flags file tells PapyrusCompiler which user flags are legal.
    Ships next to PapyrusCompiler.exe for the Bethesda Original Compiler."""
    if papyrus_exe is None:
        return None
    beside = papyrus_exe.parent / "TESV_Papyrus_Flags.flg"
    if beside.is_file():
        return beside
    return None


# Tool registration


def register_papyrus_tools(registry, organizer: mobase.IOrganizer) -> None:
    registry.register(
        name="mo2_compile_script",
        description=(
            "Compile Papyrus .psc source to .pex. Provide the source text and a "
            "filename. The compiled .pex is written to the configured output "
            "mod's Scripts/ folder. Headers resolve from the VFS (typically "
            "'Scripts/Source/') unless overridden. Requires PapyrusCompiler.exe "
            "from the Creation Kit."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "script_name": {
                    "type": "string",
                    "description": "Script filename (e.g. 'MyScript.psc'). Must end in .psc.",
                },
                "source": {
                    "type": "string",
                    "description": "Papyrus source text.",
                },
                "headers_path": {
                    "type": "string",
                    "description": (
                        "Optional VFS path to the headers directory (e.g. "
                        "'Scripts/Source/'). Default: auto-resolve common "
                        "locations."
                    ),
                },
                "optimize": {
                    "type": "boolean",
                    "description": "Enable compiler optimization (default: true).",
                    "default": True,
                },
            },
            "required": ["script_name", "source"],
        },
        handler=lambda args: _handle_compile(organizer, args),
    )


# Handler


def _handle_compile(organizer, args: dict) -> str:
    script_name = args.get("script_name", "")
    source = args.get("source", "")
    headers_arg = args.get("headers_path", "")
    optimize = args.get("optimize", True)
    if isinstance(optimize, str):
        optimize = optimize.lower() in ("true", "1", "yes")

    if not script_name:
        return json.dumps({"error": "script_name is required."})
    if not script_name.lower().endswith(".psc"):
        return json.dumps({"error": "script_name must end in .psc"})
    if "/" in script_name or "\\" in script_name or ".." in script_name:
        return json.dumps({"error": "script_name must be a simple filename."})
    if not source:
        return json.dumps({"error": "source is required."})

    # Resolve header directories. MO2's VFS merges scripts/Source from every
    # mod; each mod's .psc files live in a separate real-disk folder. The
    # Papyrus compiler can take a semicolon-separated -import list, so we
    # collect every unique parent dir of .psc files in the VFS scripts/Source
    # tree and feed them all in.
    def _collect_header_dirs(vfs_dir: str) -> list[str]:
        vfs_norm = vfs_dir.replace("/", "\\").strip("\\")
        dirs: list[str] = []
        seen = set()
        try:
            psc_files = organizer.findFiles(vfs_norm, "*.psc")
        except Exception:
            psc_files = []
        for p in psc_files:
            parent = os.path.normpath(os.path.dirname(p))
            key = parent.lower()
            if key in seen:
                continue
            if os.path.isdir(parent):
                seen.add(key)
                dirs.append(parent)
        # Single-dir fallback for configs where resolvePath does work on dirs
        if not dirs:
            candidate = organizer.resolvePath(vfs_norm)
            if candidate and os.path.isdir(candidate):
                dirs.append(candidate)
        return dirs

    header_dirs: list[str] = []
    if headers_arg:
        header_dirs = _collect_header_dirs(headers_arg)
    else:
        for default_vfs in ("Scripts\\Source", "Source\\Scripts"):
            header_dirs = _collect_header_dirs(default_vfs)
            if header_dirs:
                break

    if not header_dirs:
        return json.dumps({
            "error": (
                "Headers directory not found. Tried VFS 'Scripts/Source' and "
                "'Source/Scripts' (both resolvePath and findFiles). "
                "Pass 'headers_path' explicitly, e.g. 'Scripts/Source'."
            ),
        })

    # Semicolons are the PapyrusCompiler native import-path separator.
    headers_disk = ";".join(header_dirs)

    # Output mod
    output_mod = organizer.pluginSetting(PLUGIN_NAME, "output-mod")
    if not output_mod:
        return json.dumps({"error": "No output mod configured."})

    mods_path = organizer.modsPath()
    output_scripts_dir = os.path.join(mods_path, output_mod, "Scripts")
    pex_basename = os.path.splitext(script_name)[0] + ".pex"
    final_pex_path = os.path.join(output_scripts_dir, pex_basename)

    if not os.path.normpath(final_pex_path).startswith(os.path.normpath(os.path.join(mods_path, output_mod))):
        return json.dumps({"error": "Path escapes the output mod directory."})

    if os.path.exists(final_pex_path):
        return json.dumps({
            "error": f"Compiled file already exists: {pex_basename}. Delete first.",
            "existing_path": final_pex_path,
        })

    # Find PapyrusCompiler.exe. Requires the Creation Kit — it's Bethesda
    # proprietary, not bundled.
    papyrus_exe = _find_papyrus_compiler()
    flags_file = _find_flags_file(papyrus_exe)

    if papyrus_exe is None:
        return json.dumps({
            "error": (
                "PapyrusCompiler.exe not found. Expected under "
                "%USERPROFILE%/Documents/tools/papyrus-compiler/papyrus-compiler/"
                "Original Compiler/PapyrusCompiler.exe (Spooky's default) "
                "or a similar path. Install the Creation Kit (free via Steam) "
                "to get it."
            ),
        })

    with tempfile.TemporaryDirectory(prefix="mo2_compile_") as tmp:
        psc_path = os.path.join(tmp, script_name)
        try:
            with open(psc_path, "w", encoding="utf-8", newline="\r\n") as f:
                f.write(source)
        except Exception as e:
            return json.dumps({"error": f"Failed to write source to tmp: {e}"})

        out_dir = os.path.join(tmp, "out")
        os.makedirs(out_dir, exist_ok=True)

        # PapyrusCompiler resolves the source object-name via import paths.
        # Prepend the tmp dir (where we wrote the .psc) so the compiler can
        # find our script, then every VFS scripts/Source contributor.
        import_chain = [tmp]
        if header_dirs:
            import_chain.extend(header_dirs)
        import_arg = ";".join(import_chain)

        # PapyrusCompiler CLI: <source> -import=... -output=... [-flags=...] [-optimize]
        script_basename = os.path.splitext(os.path.basename(psc_path))[0]
        pc_args = [
            str(papyrus_exe),
            script_basename,  # object name, NOT full path
            f"-import={import_arg}",
            f"-output={out_dir}",
        ]
        if flags_file:
            pc_args.append(f"-flags={flags_file}")
        if optimize:
            pc_args.append("-optimize")

        try:
            proc = subprocess.run(
                pc_args,
                capture_output=True,
                text=True,
                timeout=90,
            )
        except subprocess.TimeoutExpired:
            return json.dumps({"error": "PapyrusCompiler timed out after 90s."})
        except Exception as e:
            return json.dumps({"error": f"Failed to run PapyrusCompiler: {e}"})

        combined_output = (proc.stdout or "") + (proc.stderr or "")

        if proc.returncode != 0:
            return json.dumps({
                "error": "Compilation failed",
                "compiler_output": combined_output.strip()[:4000],
                "returncode": proc.returncode,
                "import_dirs_count": len(import_chain),
            })

        # Find the produced .pex
        produced = None
        for f in os.listdir(out_dir):
            if f.lower() == pex_basename.lower():
                produced = os.path.join(out_dir, f)
                break
        if not produced:
            pex_candidates = [f for f in os.listdir(out_dir) if f.lower().endswith(".pex")]
            if pex_candidates:
                produced = os.path.join(out_dir, pex_candidates[0])

        if not produced or not os.path.isfile(produced):
            return json.dumps({
                "error": "Compile reported success but no .pex produced",
                "output_dir_contents": os.listdir(out_dir),
            })

        try:
            os.makedirs(output_scripts_dir, exist_ok=True)
            shutil.copy2(produced, final_pex_path)
        except Exception as e:
            return json.dumps({"error": f"Failed to copy compiled .pex into output mod: {e}"})

    qInfo(f"{PLUGIN_NAME}: compiled {script_name} -> {final_pex_path}")

    return json.dumps({
        "success": True,
        "script_name": script_name,
        "output_path": final_pex_path.replace("\\", "/"),
        "headers_used": headers_disk.replace("\\", "/"),
        "optimize": optimize,
    }, indent=2)
