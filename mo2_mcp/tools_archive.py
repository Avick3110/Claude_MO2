# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""MCP tools for BSA/BA2 archive operations via Spooky's CLI.

Wraps `spookys-automod archive {list,extract,extract-file,validate}` in --json
mode. BSAs can be large (1-4 GB per archive is common in Skyrim mods), so:

- `mo2_list_bsa` supports filters and a default limit so Claude doesn't drown in
  10k-entry file lists.
- `mo2_extract_bsa_file` pulls a single file without unpacking the rest — useful
  when Claude wants to inspect one script or mesh inside a BSA.

BSArch.exe is a runtime dependency. It ships with xEdit (BSArch.exe inside the
xEdit 7z release). If not installed, Spooky's status/error messages include
install suggestions — we pass those through verbatim.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path

from PyQt6.QtCore import qInfo, qWarning

import mobase

from .config import PLUGIN_NAME
from .tools_papyrus import _find_spooky_cli, _invoke_cli


def register_archive_tools(registry, organizer: mobase.IOrganizer) -> None:
    plugin_dir = Path(__file__).resolve().parent

    registry.register(
        name="mo2_list_bsa",
        description=(
            "List files inside a BSA/BA2 archive. Pass a VFS path (e.g. "
            "'Skyrim - Meshes0.bsa') or an absolute path. Optional filter "
            "narrows the list (e.g. '*.nif', 'textures/*'). Default limit is "
            "500 entries; set limit=0 for all. Returns the file list plus "
            "archive metadata."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "archive_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .bsa/.ba2 archive.",
                },
                "filter": {
                    "type": "string",
                    "description": "Glob filter (e.g. '*.nif', 'textures/*'). Default: no filter.",
                },
                "limit": {
                    "type": "integer",
                    "description": "Max files to return (default 500, 0 = all).",
                    "default": 500,
                },
            },
            "required": ["archive_path"],
        },
        handler=lambda args: _handle_list_bsa(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_extract_bsa_file",
        description=(
            "Extract a single file from a BSA to disk. Preferred over "
            "mo2_extract_bsa when you only need one asset (script, mesh, "
            "texture). File is written to the configured output mod, preserving "
            "the archive's internal folder structure."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "archive_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .bsa/.ba2.",
                },
                "file_in_archive": {
                    "type": "string",
                    "description": "Path inside the archive (e.g. 'scripts/MyScript.pex').",
                },
                "output_name": {
                    "type": "string",
                    "description": (
                        "Optional relative path inside the output mod. Default: "
                        "preserves the archive's internal path."
                    ),
                },
            },
            "required": ["archive_path", "file_in_archive"],
        },
        handler=lambda args: _handle_extract_file(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_extract_bsa",
        description=(
            "Extract files from a BSA into the output mod. Use a filter (e.g. "
            "'textures/*') to avoid extracting a 2GB archive when you only "
            "want a subset. For single-file extraction, prefer "
            "mo2_extract_bsa_file."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "archive_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .bsa/.ba2.",
                },
                "filter": {
                    "type": "string",
                    "description": "Glob filter (e.g. '*.nif', 'textures/actors/*'). Required to avoid full extraction.",
                },
                "output_subdir": {
                    "type": "string",
                    "description": "Optional subdirectory inside the output mod (default: archive's basename).",
                },
            },
            "required": ["archive_path", "filter"],
        },
        handler=lambda args: _handle_extract(organizer, plugin_dir, args),
    )

    registry.register(
        name="mo2_validate_bsa",
        description=(
            "Check a BSA/BA2 archive's integrity. Reports format version, "
            "corrupt entries, unreadable files, and any structural issues."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "archive_path": {
                    "type": "string",
                    "description": "VFS or absolute path to the .bsa/.ba2.",
                },
            },
            "required": ["archive_path"],
        },
        handler=lambda args: _handle_validate(organizer, plugin_dir, args),
    )


def _resolve_archive(organizer, archive_path: str) -> tuple[str | None, str | None]:
    """Return (disk_path, error). If archive_path is absolute + exists, use it;
    otherwise VFS-resolve."""
    if not archive_path:
        return None, "archive_path is required."

    if os.path.isabs(archive_path) and os.path.isfile(archive_path):
        return archive_path, None

    vfs_path = archive_path.replace("/", "\\")
    disk = organizer.resolvePath(vfs_path)
    if not disk or not os.path.isfile(disk):
        return None, f"Archive not found in VFS: {archive_path}"
    return disk, None


def _require_cli(organizer, plugin_dir: Path):
    cli = _find_spooky_cli(organizer, plugin_dir)
    if cli is None:
        return None, json.dumps({
            "error": (
                "spookys-automod.exe not found. Expected at "
                "{plugin_dir}/tools/spooky-cli/spookys-automod.exe."
            ),
        })
    return cli, None


def _handle_list_bsa(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve_archive(organizer, args.get("archive_path", ""))
    if err:
        return json.dumps({"error": err})

    cli, err_resp = _require_cli(organizer, plugin_dir)
    if err_resp:
        return err_resp

    cli_args = ["archive", "list", disk]
    if args.get("filter"):
        cli_args.extend(["-f", str(args["filter"])])
    limit = args.get("limit", 500)
    if limit is not None:
        cli_args.extend(["--limit", str(int(limit))])

    result = _invoke_cli(cli, cli_args, timeout=60)
    if not result.get("success"):
        return json.dumps({
            "error": result.get("error", "archive list failed"),
            "suggestions": result.get("suggestions"),
            "detail": result.get("stderr") or result.get("raw_output"),
        })

    res = result.get("result") or {}
    return json.dumps({
        "success": True,
        "archive": disk.replace("\\", "/"),
        "file_count": res.get("fileCount") or res.get("totalFiles") or len(res.get("files", [])),
        "files": res.get("files", []),
        "truncated": res.get("truncated"),
        "archive_info": {k: v for k, v in res.items() if k not in ("files",)},
    }, indent=2)


def _handle_extract_file(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve_archive(organizer, args.get("archive_path", ""))
    if err:
        return json.dumps({"error": err})

    file_in_archive = args.get("file_in_archive", "")
    if not file_in_archive:
        return json.dumps({"error": "file_in_archive is required."})
    if ".." in file_in_archive:
        return json.dumps({"error": "file_in_archive must not contain '..'."})

    output_mod = organizer.pluginSetting(PLUGIN_NAME, "output-mod")
    if not output_mod:
        return json.dumps({"error": "No output mod configured."})

    output_mod_dir = os.path.join(organizer.modsPath(), output_mod)
    rel_out = args.get("output_name") or file_in_archive.replace("/", os.sep)
    rel_out = rel_out.lstrip("/\\")
    final_path = os.path.join(output_mod_dir, rel_out)

    if not os.path.normpath(final_path).startswith(os.path.normpath(output_mod_dir)):
        return json.dumps({"error": "Output path escapes the output mod directory."})

    if os.path.exists(final_path):
        return json.dumps({
            "error": f"Output file already exists: {rel_out}. Delete first.",
            "existing_path": final_path,
        })

    cli, err_resp = _require_cli(organizer, plugin_dir)
    if err_resp:
        return err_resp

    os.makedirs(os.path.dirname(final_path), exist_ok=True)

    result = _invoke_cli(
        cli,
        ["archive", "extract-file", disk, "--file", file_in_archive, "--output", final_path],
        timeout=120,
    )
    if not result.get("success"):
        if os.path.exists(final_path) and os.path.getsize(final_path) == 0:
            try:
                os.remove(final_path)
            except OSError:
                pass
        return json.dumps({
            "error": result.get("error", "archive extract-file failed"),
            "suggestions": result.get("suggestions"),
            "detail": result.get("stderr") or result.get("raw_output"),
        })

    if not os.path.isfile(final_path):
        return json.dumps({
            "error": "CLI reported success but no file was produced.",
            "cli_result": result.get("result"),
        })

    size = os.path.getsize(final_path)
    qInfo(f"{PLUGIN_NAME}: extracted '{file_in_archive}' ({size} bytes) from {os.path.basename(disk)}")

    return json.dumps({
        "success": True,
        "archive": disk.replace("\\", "/"),
        "file_in_archive": file_in_archive,
        "output_path": final_path.replace("\\", "/"),
        "size_bytes": size,
    }, indent=2)


def _handle_extract(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve_archive(organizer, args.get("archive_path", ""))
    if err:
        return json.dumps({"error": err})

    glob_filter = args.get("filter")
    if not glob_filter:
        return json.dumps({
            "error": (
                "filter is required to prevent accidental full archive extraction. "
                "Use a glob like 'textures/*' or '*.nif'. For single-file extraction "
                "prefer mo2_extract_bsa_file."
            ),
        })

    output_mod = organizer.pluginSetting(PLUGIN_NAME, "output-mod")
    if not output_mod:
        return json.dumps({"error": "No output mod configured."})

    output_mod_dir = os.path.join(organizer.modsPath(), output_mod)
    subdir = args.get("output_subdir")
    if subdir:
        if ".." in subdir or os.path.isabs(subdir):
            return json.dumps({"error": "output_subdir must be a simple relative path."})
        out_dir = os.path.join(output_mod_dir, subdir)
    else:
        # Default: use archive basename to keep extracts grouped
        basename = os.path.splitext(os.path.basename(disk))[0]
        out_dir = os.path.join(output_mod_dir, basename)

    if not os.path.normpath(out_dir).startswith(os.path.normpath(output_mod_dir)):
        return json.dumps({"error": "Output subdir escapes the output mod directory."})

    os.makedirs(out_dir, exist_ok=True)

    cli, err_resp = _require_cli(organizer, plugin_dir)
    if err_resp:
        return err_resp

    # Spooky CLI's `archive extract` accepts a -f filter but doesn't actually
    # apply it — BSArch's unpack has no filter flag and Spooky's wrapper never
    # post-filters. Previous workaround (list then loop extract-file) was
    # correct but spawned one subprocess per match, which hits ~5s/file and
    # blocks the MCP server for many minutes on large filter matches.
    #
    # New approach: one full extract to a tmp dir (BSArch is fast at dumping),
    # then walk the tmp tree and move only matches into the output dir. Costs
    # the full-archive disk footprint transiently but completes in tens of
    # seconds instead of many minutes.
    import fnmatch
    import shutil as _shutil

    pattern_fwd = glob_filter.replace("\\", "/")

    with tempfile.TemporaryDirectory(prefix="mo2_bsa_extract_") as tmp_extract:
        result = _invoke_cli(
            cli,
            ["archive", "extract", disk, "-o", tmp_extract],
            timeout=600,
        )
        if not result.get("success"):
            return json.dumps({
                "error": result.get("error", "archive extract failed"),
                "detail": result.get("stderr") or result.get("raw_output"),
            })

        extracted = 0
        errors: list[dict] = []
        tmp_base = os.path.normpath(tmp_extract)
        out_base = os.path.normpath(out_dir)
        for root, _dirs, files in os.walk(tmp_extract):
            for f in files:
                full = os.path.join(root, f)
                rel = os.path.relpath(full, tmp_base).replace("\\", "/")
                if not fnmatch.fnmatch(rel, pattern_fwd):
                    continue
                dest = os.path.normpath(os.path.join(out_base, rel))
                if not dest.startswith(out_base):
                    errors.append({"file": rel, "error": "path escapes output dir"})
                    continue
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                try:
                    _shutil.move(full, dest)
                    extracted += 1
                except Exception as move_err:
                    errors.append({"file": rel, "error": f"move failed: {move_err}"})

    qInfo(
        f"{PLUGIN_NAME}: extracted {extracted} files from "
        f"{os.path.basename(disk)} matching '{glob_filter}'"
    )
    return json.dumps({
        "success": True,
        "archive": disk.replace("\\", "/"),
        "filter": glob_filter,
        "output_dir": out_dir.replace("\\", "/"),
        "cli_result": {
            "extractedCount": extracted,
            "errors": errors,
        },
    }, indent=2)


def _handle_validate(organizer, plugin_dir: Path, args: dict) -> str:
    disk, err = _resolve_archive(organizer, args.get("archive_path", ""))
    if err:
        return json.dumps({"error": err})

    cli, err_resp = _require_cli(organizer, plugin_dir)
    if err_resp:
        return err_resp

    result = _invoke_cli(cli, ["archive", "validate", disk], timeout=120)
    if not result.get("success"):
        return json.dumps({
            "error": result.get("error", "archive validate failed"),
            "suggestions": result.get("suggestions"),
            "detail": result.get("stderr") or result.get("raw_output"),
            "cli_result": result.get("result"),
        })

    return json.dumps({
        "success": True,
        "archive": disk.replace("\\", "/"),
        "result": result.get("result"),
    }, indent=2)
