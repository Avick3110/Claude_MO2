# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""MCP tools for NIF (mesh) inspection via Spooky's CLI.

Two tiers of tool here:
- `mo2_nif_info` is library-native in Spooky — works out of the box.
- `mo2_nif_list_textures` / `mo2_nif_shader_info` shell out to `nif-tool.exe`,
  a separate Rust binary that ships in Spooky's release 7z but is NOT in the
  source clone. If missing, Spooky's CLI returns a "nif-tool not found" error
  with suggestions — we pass those through.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

from PyQt6.QtCore import qInfo

import mobase

from .config import PLUGIN_NAME
from .tools_papyrus import _find_spooky_cli, _invoke_cli


def register_nif_tools(registry, organizer: mobase.IOrganizer) -> None:
    plugin_dir = Path(__file__).resolve().parent

    registry.register(
        name="mo2_nif_info",
        description=(
            "Return NIF format metadata: version, file size, header string. "
            "Takes a VFS or absolute path to a .nif. Library-native — no "
            "nif-tool.exe dependency."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "nif_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .nif file.",
                },
            },
            "required": ["nif_path"],
        },
        handler=lambda args: _handle_nif_info(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_nif_list_textures",
        description=(
            "List every texture path referenced by a NIF. Useful for spotting "
            "missing-texture references or auditing texture-path prefixes. "
            "Requires nif-tool.exe (Spooky's Rust binary — see "
            "spooky-cli/tools/nif-tool/)."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "nif_path": {
                    "type": "string",
                    "description": "VFS or absolute path to a .nif file (or a folder — recursive).",
                },
            },
            "required": ["nif_path"],
        },
        handler=lambda args: _handle_nif_list_textures(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_nif_shader_info",
        description=(
            "Show shader flags on BSLightingShaderProperty blocks in a NIF. "
            "Useful for debugging material/lighting issues. Requires "
            "nif-tool.exe."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "nif_path": {
                    "type": "string",
                    "description": "VFS or absolute path to a .nif file (or a folder — recursive).",
                },
            },
            "required": ["nif_path"],
        },
        handler=lambda args: _handle_nif_shader_info(organizer, plugin_dir, args),
    )


def _resolve_nif(organizer, nif_path: str) -> tuple[str | None, str | None]:
    if not nif_path:
        return None, "nif_path is required."
    if os.path.isabs(nif_path) and os.path.exists(nif_path):
        return nif_path, None
    vfs = nif_path.replace("/", "\\")
    disk = organizer.resolvePath(vfs)
    if not disk or not os.path.exists(disk):
        return None, f"NIF not found in VFS: {nif_path}"
    return disk, None


def _nif_cli_passthrough(organizer, plugin_dir: Path, subcmd: str, args: dict, timeout: int = 60) -> str:
    disk, err = _resolve_nif(organizer, args.get("nif_path", ""))
    if err:
        return json.dumps({"error": err})

    cli = _find_spooky_cli(organizer, plugin_dir)
    if cli is None:
        return json.dumps({
            "error": (
                "spookys-automod.exe not found. Expected at "
                "{plugin_dir}/tools/spooky-cli/spookys-automod.exe."
            ),
        })

    result = _invoke_cli(cli, ["nif", subcmd, disk], timeout=timeout)
    if not result.get("success"):
        return json.dumps({
            "error": result.get("error", f"nif {subcmd} failed"),
            "suggestions": result.get("suggestions"),
            "detail": result.get("stderr") or result.get("raw_output"),
        })
    return json.dumps({
        "success": True,
        "nif": disk.replace("\\", "/"),
        "result": result.get("result"),
    }, indent=2)


def _handle_nif_info(organizer, plugin_dir: Path, args: dict) -> str:
    return _nif_cli_passthrough(organizer, plugin_dir, "info", args)


def _handle_nif_list_textures(organizer, plugin_dir: Path, args: dict) -> str:
    return _nif_cli_passthrough(organizer, plugin_dir, "list-textures", args)


def _handle_nif_shader_info(organizer, plugin_dir: Path, args: dict) -> str:
    return _nif_cli_passthrough(organizer, plugin_dir, "shader-info", args)
