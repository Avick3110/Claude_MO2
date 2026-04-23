# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""MCP tools for voice audio file inspection via Spooky's CLI.

Skyrim voice lines are stored as .fuz files = XWM audio + LIP sync data bundled
together. These tools:
- `mo2_audio_info` — metadata for any audio file (FUZ/XWM/WAV).
- `mo2_extract_fuz` — split a .fuz into its .xwm + .lip components. Extracted
  files go to the output mod, preserving the voice-folder tree.
"""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
from pathlib import Path

from PyQt6.QtCore import qInfo, qWarning

import mobase

from .config import PLUGIN_NAME
from .tools_papyrus import _find_spooky_cli, _invoke_cli
from .tools_patching import _find_bridge


def register_audio_tools(registry, organizer: mobase.IOrganizer) -> None:
    plugin_dir = Path(__file__).resolve().parent

    registry.register(
        name="mo2_audio_info",
        description=(
            "Return metadata for a voice/audio file (FUZ, XWM, or WAV): format, "
            "size, sample rate, channels, duration. Takes a VFS or absolute "
            "path."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "audio_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the audio file.",
                },
            },
            "required": ["audio_path"],
        },
        handler=lambda args: _handle_audio_info(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_extract_fuz",
        description=(
            "Split a Skyrim .fuz voice file into its XWM (audio) and LIP (lip "
            "sync) components. Extracted files are written into the output "
            "mod; by default they keep the .fuz's parent folder structure."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "fuz_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .fuz file.",
                },
                "output_subdir": {
                    "type": "string",
                    "description": (
                        "Optional subdirectory inside the output mod "
                        "(default: 'FuzExtract/<basename>')."
                    ),
                },
            },
            "required": ["fuz_path"],
        },
        handler=lambda args: _handle_extract_fuz(organizer, plugin_dir, args),
    )


def _resolve(organizer, path: str, kind: str) -> tuple[str | None, str | None]:
    if not path:
        return None, f"{kind}_path is required."
    if os.path.isabs(path) and os.path.isfile(path):
        return path, None
    vfs = path.replace("/", "\\")
    disk = organizer.resolvePath(vfs)
    if not disk or not os.path.isfile(disk):
        return None, f"{kind} not found in VFS: {path}"
    return disk, None


def _invoke_bridge(bridge: Path, request: dict, timeout: int = 30) -> dict:
    """Run mutagen-bridge with a JSON request on stdin, return the decoded response."""
    try:
        proc = subprocess.run(
            [str(bridge)],
            input=json.dumps(request),
            capture_output=True,
            text=True,
            timeout=timeout,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
        )
    except Exception as e:
        return {"success": False, "error": f"Bridge invocation failed: {e}"}
    stdout = proc.stdout.strip()
    if not stdout:
        return {
            "success": False,
            "error": f"Bridge returned no output. Exit: {proc.returncode}",
            "stderr": (proc.stderr or "").strip()[:500] or None,
        }
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        return {"success": False, "error": "Bridge returned invalid JSON.", "raw_output": stdout[:500]}


def _handle_audio_info(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve(organizer, args.get("audio_path", ""), "audio")
    if err:
        return json.dumps({"error": err})

    # FUZ files: use our own bridge-side parser — Spooky v1.11.1's parser
    # rejects valid FUZes with "Not a valid FUZ file" even though the magic
    # bytes check out. For XWM/WAV, fall back to Spooky's CLI.
    if disk.lower().endswith(".fuz"):
        bridge = _find_bridge(organizer, plugin_dir)
        if bridge is None:
            return json.dumps({"error": "mutagen-bridge.exe not found."})
        resp = _invoke_bridge(bridge, {"command": "fuz_info", "fuz_path": disk}, timeout=30)
        if not resp.get("success"):
            return json.dumps({
                "error": resp.get("error", "fuz_info failed"),
                "detail": resp.get("stderr") or resp.get("raw_output"),
            })
        return json.dumps({
            "success": True,
            "audio": disk.replace("\\", "/"),
            "result": {
                "format": resp.get("format"),
                "fileSize": resp.get("file_size"),
                "version": resp.get("version"),
                "lipSize": resp.get("lip_size"),
                "xwmSize": resp.get("xwm_size"),
                "versionSupported": resp.get("version_supported"),
            },
        }, indent=2)

    cli = _find_spooky_cli(organizer, plugin_dir)
    if cli is None:
        return json.dumps({"error": "spookys-automod.exe not found."})

    result = _invoke_cli(cli, ["audio", "info", disk], timeout=30)
    if not result.get("success"):
        return json.dumps({
            "error": result.get("error", "audio info failed"),
            "suggestions": result.get("suggestions"),
            "detail": result.get("stderr") or result.get("raw_output"),
        })
    return json.dumps({
        "success": True,
        "audio": disk.replace("\\", "/"),
        "result": result.get("result"),
    }, indent=2)


def _handle_extract_fuz(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve(organizer, args.get("fuz_path", ""), "fuz")
    if err:
        return json.dumps({"error": err})

    if not disk.lower().endswith(".fuz"):
        return json.dumps({"error": f"Not a .fuz file: {disk}"})

    output_mod = organizer.pluginSetting(PLUGIN_NAME, "output-mod")
    if not output_mod:
        return json.dumps({"error": "No output mod configured."})

    output_mod_dir = os.path.join(organizer.modsPath(), output_mod)
    basename = os.path.splitext(os.path.basename(disk))[0]

    subdir = args.get("output_subdir")
    if subdir:
        if ".." in subdir or os.path.isabs(subdir):
            return json.dumps({"error": "output_subdir must be a simple relative path."})
        out_dir = os.path.join(output_mod_dir, subdir)
    else:
        out_dir = os.path.join(output_mod_dir, "FuzExtract", basename)

    if not os.path.normpath(out_dir).startswith(os.path.normpath(output_mod_dir)):
        return json.dumps({"error": "Output subdir escapes the output mod directory."})

    # If either output file would collide, refuse.
    for ext in (".xwm", ".lip"):
        candidate = os.path.join(out_dir, basename + ext)
        if os.path.exists(candidate):
            return json.dumps({
                "error": f"Output file already exists: {basename}{ext}. Delete first.",
                "existing_path": candidate,
            })

    os.makedirs(out_dir, exist_ok=True)

    # Use our own bridge-side FUZ splitter — Spooky v1.11.1 rejects real FUZes.
    bridge = _find_bridge(organizer, plugin_dir)
    if bridge is None:
        return json.dumps({"error": "mutagen-bridge.exe not found."})

    resp = _invoke_bridge(
        bridge,
        {"command": "fuz_extract", "fuz_path": disk, "output_dir": out_dir},
        timeout=60,
    )
    if not resp.get("success"):
        return json.dumps({
            "error": resp.get("error", "fuz_extract failed"),
            "detail": resp.get("stderr") or resp.get("raw_output"),
        })

    produced = [f for f in os.listdir(out_dir) if f.lower().endswith((".xwm", ".lip"))]
    qInfo(f"{PLUGIN_NAME}: extracted FUZ '{basename}' -> {len(produced)} files")

    # Fire-and-forget MO2 refresh so the extracted xwm/lip land in the VFS.
    # Phase 4 retired the wait-for-index-rebuild coordination — extracted
    # assets don't affect the record index.
    try:
        organizer.refresh(save_changes=True)
    except Exception as exc:
        qWarning(f"{PLUGIN_NAME}: organizer.refresh() failed after FUZ extract: {exc}")

    return json.dumps({
        "success": True,
        "fuz": disk.replace("\\", "/"),
        "output_dir": out_dir.replace("\\", "/"),
        "produced": sorted(produced),
        "lip_path": resp.get("lip_path"),
        "xwm_path": resp.get("xwm_path"),
        "lip_size": resp.get("lip_size"),
        "xwm_size": resp.get("xwm_size"),
        "next_step": (
            f"Extracted files are visible to mo2_list_files / "
            f"mo2_read_file via MO2's VFS (as long as '{output_mod}' is "
            f"enabled in MO2's left pane). No further action needed."
        ),
    }, indent=2)
