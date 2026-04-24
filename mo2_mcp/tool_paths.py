# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""JSON-configured tool-path overrides.

Loads `<plugin>/mo2_mcp/tool_paths.json` and exposes configured absolute
paths for Papyrus tool surfaces that are discovered at runtime (not
copied into the plugin dir by the installer).

Surfaces (v2.7.0):
  - "papyrus_compiler"     — overrides `_find_papyrus_compiler()` priority 0.
                              Lets users point at an existing Creation Kit
                              install rather than copying `PapyrusCompiler.exe`
                              into the plugin tree.
  - "papyrus_scripts_dir"  — additively appended to the VFS-derived
                              `-import=` chain in `_collect_header_dirs()`.
                              Supplements (never replaces) MO2's VFS
                              aggregation of Scripts/Source contributions.

The JSON is written by the v2.7.0 installer's Optional Tools picker page,
or may be hand-edited by the user. Absent keys, null values, schema-version
mismatches, malformed JSON, and configured paths that don't exist on disk
all degrade gracefully to "not configured" (None from `get()`) and emit a
warning — the consuming tool can fall through to its v2.6.1 fallback chain.

Cache is process-lifetime. Users who edit the JSON while MO2 is running
must restart MO2 for changes to take effect; `reload()` exists as an
escape hatch for testing and for future tool surfaces that may want to
re-read on demand.

Intentionally no PyQt6 / mobase imports — this module stays testable
outside the MO2 host (unit-style smoke tests under `dev/` use plain stdlib
logging; MO2's log panel still captures `logging.warning` output).
"""

from __future__ import annotations

import json
import logging
from pathlib import Path

_CONFIG_FILENAME = "tool_paths.json"
_SCHEMA_VERSION = 1
_cache: dict | None = None


def _config_path() -> Path:
    """Absolute path to `tool_paths.json` next to this module."""
    return Path(__file__).resolve().parent / _CONFIG_FILENAME


def _load() -> dict:
    """Read and validate the JSON config. Cached for process lifetime.

    Returns an empty dict on any failure path (missing file, unreadable,
    non-object root, `schema_version` mismatch, JSON syntax error). A
    warning is logged for every non-absent failure; the caller sees the
    same "not configured" state as a clean-install user with no JSON.
    """
    global _cache
    if _cache is not None:
        return _cache
    path = _config_path()
    if not path.exists():
        _cache = {}
        return _cache
    try:
        with open(path, "r", encoding="utf-8") as f:
            raw = json.load(f)
    except Exception as exc:
        logging.warning(f"tool_paths.json read failed: {exc}")
        _cache = {}
        return _cache
    if not isinstance(raw, dict):
        logging.warning("tool_paths.json is not a JSON object; ignoring.")
        _cache = {}
        return _cache
    sv = raw.get("schema_version")
    if sv != _SCHEMA_VERSION:
        logging.warning(
            f"tool_paths.json schema_version={sv!r} (expected {_SCHEMA_VERSION}); "
            f"ignoring config — Python cannot safely read future/unknown schema."
        )
        _cache = {}
        return _cache
    _cache = raw
    return _cache


def get(tool: str) -> str | None:
    """Return the configured absolute path for `tool`, or None if unset.

    `tool` must be one of: "papyrus_compiler", "papyrus_scripts_dir".

    Returns None when:
      - `tool_paths.json` is absent or unreadable
      - the key is missing or null
      - `schema_version` does not match this release
      - the configured path does not exist on disk (warning logged)

    Callers fall through to their existing v2.6.1 discovery chain when
    None is returned — no configured path should never be a hard error.
    """
    paths = _load()
    value = paths.get(tool)
    if not value or not isinstance(value, str):
        return None
    if not Path(value).exists():
        logging.warning(
            f"tool_paths.json configured {tool}={value!r} but path does not exist"
        )
        return None
    return value


def reload() -> None:
    """Invalidate the cache. Next `get()` re-reads the JSON from disk.

    Useful for tests and for callers that want to pick up JSON edits
    without restarting MO2. In normal MO2 operation, the cache lasts for
    the lifetime of the Python interpreter — users editing the JSON
    while MO2 is running should restart MO2.
    """
    global _cache
    _cache = None
