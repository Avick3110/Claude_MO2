# mo2_mcp - MCP server plugin for Mod Organizer 2
# Copyright (c) 2026 Aaronavich
# Licensed under the MIT License. See LICENSE for details.

"""MCP tool for ESP patch creation via the Mutagen bridge.

Handles every ESP operation we support:
- override records with field modifications (set_fields, set_flags, clear_flags)
- add/remove keywords, spells, perks, packages, factions, inventory, outfits, form lists
- add/remove leveled list entries (for override op)
- add/remove conditions (any record with a Conditions property), ConditionFloat + ConditionGlobal
- attach scripts with typed properties (Object/Int/Float/Bool/String/Alias)
- set/clear enchantment
- merge leveled lists (LVLI/LVLN/LVSP) with base + overrides

Calls mutagen-bridge.exe via subprocess — JSON on stdin, JSON from stdout.
The bridge references Mutagen.Bethesda.Skyrim directly via NuGet
(v0.53.1+). All modification logic lives in the bridge's PatchEngine.cs.
"""

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path

from PyQt6.QtCore import qInfo, qWarning

from .config import PLUGIN_NAME
from .tools_records import (
    _get_index,
    _parse_formid_str,
    _refresh_and_wait,
    build_bridge_load_order_context,
)


def register_patching_tools(registry, organizer) -> None:
    """Register ESP patch creation tools with the MCP tool registry."""

    plugin_dir = Path(__file__).resolve().parent

    registry.register(
        name="mo2_create_patch",
        description=(
            "Create an ESP patch plugin. Supports: override records with "
            "field modifications (set_fields, set_flags); add/remove keywords, "
            "spells, perks, packages, factions, inventory, outfits, form list "
            "entries; merge leveled lists (LVLI/LVLN/LVSP) from multiple "
            "plugins; attach scripts with properties (Object/Int/Float/Bool/"
            "String/Alias); add/remove conditions (ConditionFloat against a "
            "literal, or ConditionGlobal against a global variable). All "
            "output is Mutagen-validated. Per-record errors include an "
            "'unmatched_operators' field listing any operators not supported "
            "on the target record type — silent drops were eliminated in "
            "v2.7.1. Requires mutagen-bridge.exe."
        ),
        input_schema={
            "type": "object",
            "properties": {
                "output_name": {
                    "type": "string",
                    "description": (
                        "Filename for the patch (e.g. 'MyPatch.esp'). "
                        "Must end in .esp"
                    ),
                },
                "records": {
                    "type": "array",
                    "description": "Record operations to perform",
                    "items": {
                        "type": "object",
                        "properties": {
                            "op": {
                                "type": "string",
                                "enum": ["override", "merge_leveled_list"],
                                "description": "'override': copy record + apply modifications. 'merge_leveled_list': merge entries from multiple plugins (LVLI/LVLN/LVSP).",
                            },
                            "formid": {"type": "string", "description": "FormID as 'PluginName:LocalID'"},
                            "source_plugin": {"type": "string", "description": "For override: which plugin's version to copy (default: winner)"},
                            "set_fields": {"type": "object", "description": "Field name->value pairs. Aliases: Health/Magicka/Stamina (NPC), ArmorRating/Value/Weight (ARMO), Damage/Speed/Reach (WEAP), BaseHealth/BaseMagicka/BaseStamina/HealthRegen/MagickaRegen/StaminaRegen (RACE). Also accepts Mutagen property paths like 'Configuration.HealthOffset'. Dict-typed fields support bracket syntax: 'Starting[Health]: 100' on RACE; works for any Mutagen IDictionary<,> property. Whole-dict assignment via JSON object: 'Starting: {Health: 100, Magicka: 200}' (merge semantics — only specified keys touched)."},
                            "set_flags": {"type": "array", "items": {"type": "string"}, "description": "Flag names to set (e.g. ['Essential', 'Protected', 'Female'] for NPC)"},
                            "clear_flags": {"type": "array", "items": {"type": "string"}, "description": "Flag names to clear"},
                            "add_keywords": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of keywords to add. Records: Armor, Weapon, NPC, Ingestible (ALCH), Ammunition, Book, Flora, Ingredient, MiscItem, Scroll, Race, Furniture, Activator, Location, Spell, MagicEffect."},
                            "remove_keywords": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of keywords to remove. Same supported records as add_keywords."},
                            "add_spells": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of spells to add (NPC, RACE)."},
                            "remove_spells": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of spells to remove (NPC, RACE)."},
                            "add_perks": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of perks to add (NPC)"},
                            "remove_perks": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of perks to remove (NPC)"},
                            "add_packages": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of AI packages to add (NPC)"},
                            "remove_packages": {"type": "array", "items": {"type": "string"}, "description": "FormIDs of AI packages to remove (NPC)"},
                            "add_factions": {"type": "array", "items": {"type": "object", "properties": {"faction": {"type": "string"}, "rank": {"type": "integer"}}, "required": ["faction"]}, "description": "Factions to add with rank (NPC)"},
                            "remove_factions": {"type": "array", "items": {"type": "string"}, "description": "Faction FormIDs to remove (NPC)"},
                            "add_inventory": {"type": "array", "items": {"type": "object", "properties": {"item": {"type": "string"}, "count": {"type": "integer"}}, "required": ["item"]}, "description": "Items to add to inventory (NPC/container)"},
                            "remove_inventory": {"type": "array", "items": {"type": "string"}, "description": "Item FormIDs to remove from inventory"},
                            "add_outfit_items": {"type": "array", "items": {"type": "string"}, "description": "FormIDs to add to outfit (OTFT)"},
                            "remove_outfit_items": {"type": "array", "items": {"type": "string"}, "description": "FormIDs to remove from outfit (OTFT)"},
                            "add_form_list_entries": {"type": "array", "items": {"type": "string"}, "description": "FormIDs to add to form list (FLST)"},
                            "remove_form_list_entries": {"type": "array", "items": {"type": "string"}, "description": "FormIDs to remove from form list (FLST)"},
                            "add_items": {"type": "array", "items": {"type": "object", "properties": {"reference": {"type": "string"}, "level": {"type": "integer"}, "count": {"type": "integer"}}, "required": ["reference"]}, "description": "Entries to add to leveled lists (LVLI, LVLN, LVSP)."},
                            "add_conditions": {"type": "array", "items": {"type": "object", "properties": {"function": {"type": "string", "description": "Mutagen condition function name (e.g. 'GetLevel', 'GetActorValue', 'GetGlobalValue')"}, "operator": {"type": "string", "description": "Comparison operator: ==, !=, <, <=, >, >=, or Mutagen enum name"}, "value": {"type": "number", "description": "ConditionFloat literal — compare against this value"}, "global": {"type": "string", "description": "ConditionGlobal FormID — compare against this Global (alternative to 'value'). Use with GetGlobalValue functions."}, "run_on": {"type": "string", "description": "Subject / Target / Reference / CombatTarget / LinkedReference / QuestAlias / PackageData / EventData (default: Subject)"}, "or_flag": {"type": "boolean", "description": "OR with the next condition (default: AND)"}}, "required": ["function"]}, "description": "Conditions to add. Supported on records with a top-level Conditions property: MagicEffect, Perk, Package. SPEL records take per-effect conditions on their MGEFs (use the MGEF FormID). QUST records use DialogConditions/EventConditions which require a parameter not yet exposed (see KNOWN_ISSUES)."},
                            "remove_conditions": {"type": "array", "items": {"type": "object", "properties": {"function": {"type": "string"}, "index": {"type": "integer"}}}, "description": "Conditions to remove by function name or index. Same supported records as add_conditions (MagicEffect, Perk, Package)."},
                            "attach_scripts": {"type": "array", "items": {"type": "object", "properties": {"name": {"type": "string"}, "properties": {"type": "array", "items": {"type": "object", "properties": {"name": {"type": "string"}, "type": {"type": "string", "enum": ["Object", "Int", "Float", "Bool", "String", "Alias"], "description": "Alias type expects value as {quest: 'Plugin:FormID', alias_id: int}"}, "value": {}}}}}, "required": ["name"]}, "description": "Scripts to attach with typed properties (VMAD). Supported on records with a VirtualMachineAdapter property (NPC, Quest, Armor, Weapon, Outfit, Container, Door, Activator, Furniture, Light, MagicEffect, Spell, etc.). Auto-creating an adapter on PERK/QUST records with no existing scripts uses the wrong adapter subclass — see KNOWN_ISSUES."},
                            "set_enchantment": {"type": "string", "description": "FormID of enchantment to set on ARMO/WEAP"},
                            "clear_enchantment": {"type": "boolean", "description": "Remove enchantment from ARMO/WEAP"},
                            "base_plugin": {"type": "string", "description": "For merge_leveled_list: plugin whose version to use as base (default: FormID origin)"},
                            "override_plugins": {"type": "array", "items": {"type": "string"}, "description": "For merge_leveled_list: plugins whose entries to merge in"},
                        },
                        "required": ["op", "formid"],
                    },
                },
                "esl_flag": {
                    "type": "boolean",
                    "description": "ESL-flag the patch (default: true)",
                    "default": True,
                },
                "author": {
                    "type": "string",
                    "description": "Author field (default: 'Claude MO2')",
                    "default": "Claude MO2",
                },
            },
            "required": ["output_name", "records"],
        },
        handler=lambda args: _handle_create_patch(organizer, plugin_dir, args),
    )


def _handle_create_patch(organizer, plugin_dir: Path, args: dict) -> str:
    """Handle the mo2_create_patch tool call."""

    output_name = args.get("output_name", "")
    records_arg = args.get("records", [])
    author = args.get("author", "Claude MO2")
    esl_flag = args.get("esl_flag", True)
    if isinstance(esl_flag, str):
        esl_flag = esl_flag.lower() in ('true', '1', 'yes')

    # Validate output name

    if not output_name:
        return json.dumps({"error": "output_name is required."})

    if not output_name.lower().endswith('.esp'):
        return json.dumps({"error": "output_name must end in .esp"})

    if '/' in output_name or '\\' in output_name or '..' in output_name:
        return json.dumps({"error": "output_name must be a simple filename."})

    if not records_arg:
        return json.dumps({"error": "records list is empty."})

    # Find the bridge exe

    bridge_path = _find_bridge(organizer, plugin_dir)
    if bridge_path is None:
        return json.dumps({
            "error": (
                "mutagen-bridge.exe not found. "
                "Expected at: {plugin_dir}/tools/mutagen-bridge.exe "
                "or {plugin_dir}/tools/mutagen-bridge/mutagen-bridge.exe, "
                "or configure via 'mutagen-bridge-path' plugin setting."
            ),
        })

    # Resolve output path

    output_mod = organizer.pluginSetting(PLUGIN_NAME, "output-mod")
    if not output_mod:
        return json.dumps({"error": "No output mod configured."})

    mods_path = organizer.modsPath()
    output_dir = os.path.join(mods_path, output_mod)
    output_path = os.path.join(output_dir, output_name)

    if not os.path.normpath(output_path).startswith(os.path.normpath(output_dir)):
        return json.dumps({"error": "Path escapes the output mod directory."})

    if os.path.exists(output_path):
        return json.dumps({
            "error": f"File already exists: {output_name}. Delete first.",
            "existing_path": output_path,
        })

    # Check index

    idx = _get_index()
    if not idx or not idx.is_built:
        return json.dumps({
            "error": "Record index not built. Call mo2_build_record_index first.",
        })

    # Resolve all plugin names to disk paths

    bridge_records = []
    errors = []

    for i, rec_spec in enumerate(records_arg):
        try:
            bridge_rec = _resolve_record_paths(
                idx, organizer, rec_spec, i,
            )
            bridge_records.append(bridge_rec)
        except Exception as e:
            errors.append({
                "index": i,
                "formid": rec_spec.get("formid", ""),
                "error": str(e),
            })

    if not bridge_records:
        return json.dumps({
            "error": "No records could be resolved.",
            "details": errors,
        }, indent=2)

    # Pre-validate VMAD Alias properties: alias_id must exist in the referenced quest.
    # Bridge writes them either way (xEdit only warns, not errors) but users won't notice
    # until they run Check Errors. Catch it here with a clear message.

    alias_errors = _validate_alias_properties(bridge_records, idx, bridge_path)
    if alias_errors:
        return json.dumps({
            "error": "Alias property validation failed.",
            "details": alias_errors,
        }, indent=2)

    # Ensure output directory exists

    if not os.path.isdir(output_dir):
        try:
            os.makedirs(output_dir)
        except OSError as e:
            return json.dumps({"error": f"Cannot create output dir: {e}"})

    # Build the bridge request

    # Use forward slashes for JSON (avoids backslash escaping issues)
    bridge_request = {
        "output_path": output_path.replace("\\", "/"),
        "esl_flag": esl_flag,
        "author": author,
        "records": bridge_records,
        # v2.6.0 Phase 2: load-order context drives Mutagen's write-path
        # master-style lookup so FormLinks to ESL/Light masters compact
        # correctly in the output ESP. Bridge rejects the request without
        # this field — patching genuinely needs it for correctness.
        "load_order": build_bridge_load_order_context(organizer, idx),
    }

    # Call the bridge

    try:
        result = subprocess.run(
            [str(bridge_path)],
            input=json.dumps(bridge_request),
            capture_output=True,
            text=True,
            timeout=60,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
        )
    except subprocess.TimeoutExpired:
        return json.dumps({"error": "mutagen-bridge timed out after 60s."})
    except FileNotFoundError:
        return json.dumps({"error": f"Bridge exe not found: {bridge_path}"})
    except Exception as e:
        return json.dumps({"error": f"Failed to run bridge: {e}"})

    # Parse the response

    stdout = result.stdout.strip()
    if not stdout:
        stderr = result.stderr.strip()
        return json.dumps({
            "error": f"Bridge returned no output. Exit code: {result.returncode}",
            "stderr": stderr[:500] if stderr else None,
        })

    try:
        response = json.loads(stdout)
    except json.JSONDecodeError:
        return json.dumps({
            "error": "Bridge returned invalid JSON.",
            "raw_output": stdout[:500],
        })

    # Add any path-resolution warnings
    if errors:
        response.setdefault("warnings", []).extend(errors)

    qInfo(
        f"{PLUGIN_NAME}: mutagen bridge - "
        f"{'success' if response.get('success') else 'failed'}: "
        f"{output_name}"
    )

    # Fire MO2's directory refresh and block until onRefreshed signals,
    # so the next read-back query sees the newly-written plugin in
    # pluginList — organizer.refresh() is async and ensure_fresh would
    # otherwise race with MO2's internal rebuild. Phase 4d.
    #
    # Skipped when nothing was actually written (full-failure or pre-
    # bridge error). On timeout, the tool call still succeeds — the
    # patch is already on disk, and MO2 will finish refreshing
    # asynchronously. The response surfaces refresh_status so Claude
    # can tell the user to press F5 manually if needed.
    wrote_anything = (
        response.get('success')
        or response.get('successful_count', 0) > 0
        or (response.get('records_written') or 0) > 0
    )
    if wrote_anything:
        refresh_completed, refresh_elapsed_ms = _refresh_and_wait(organizer)
        response['refresh_status'] = 'complete' if refresh_completed else 'timeout'
        response['refresh_elapsed_ms'] = refresh_elapsed_ms
        if refresh_completed:
            response['next_step'] = (
                f"Patch written. Tell the user to tick the checkbox next "
                f"to '{output_name}' in MO2's right pane when they want "
                f"to load the patch in-game. Read-back queries "
                f"(mo2_record_detail, mo2_query_records) see the new "
                f"records immediately — no need to wait for user "
                f"confirmation before chaining reads."
            )
        else:
            response['next_step'] = (
                f"Patch written successfully, but MO2's directory refresh "
                f"did not signal within 30s. The patch is on disk but may "
                f"not appear in the load order or be read-back-able until "
                f"MO2 finishes refreshing. Tell the user to press F5 in "
                f"MO2 to force the refresh, then read-back queries will "
                f"see the new records. They should tick the checkbox next "
                f"to '{output_name}' in MO2's right pane when they want "
                f"to load the patch in-game."
            )

    return json.dumps(response, indent=2)


# Path Resolution Helpers


def _find_bridge(organizer, plugin_dir: Path) -> Path | None:
    """Find the mutagen-bridge executable.

    Search order (mutagen-named first; spooky-named is a one-release shim
    for v2.5.x installs where the user has not yet run the v2.6 installer
    or -SyncLive wipe-and-copy, and for users who explicitly set the
    legacy 'spooky-bridge-path' plugin setting):
    1. Plugin setting 'mutagen-bridge-path' (explicit override)
    2. Plugin setting 'spooky-bridge-path' (legacy fallback)
    3. {plugin_dir}/tools/mutagen-bridge.exe
    4. {plugin_dir}/tools/mutagen-bridge/mutagen-bridge.exe
    5. {plugin_dir}/tools/spooky-bridge.exe (pre-rename fallback)
    6. {plugin_dir}/tools/spooky-bridge/spooky-bridge.exe (pre-rename fallback)
    """
    # Check explicit setting (prefer new name; legacy fallback for v2.5.x).
    setting = organizer.pluginSetting(PLUGIN_NAME, "mutagen-bridge-path")
    if not setting:
        setting = organizer.pluginSetting(PLUGIN_NAME, "spooky-bridge-path")
    if setting and os.path.isfile(str(setting)):
        return Path(str(setting))

    # Check standard locations.
    candidates = [
        plugin_dir / "tools" / "mutagen-bridge.exe",
        plugin_dir / "tools" / "mutagen-bridge" / "mutagen-bridge.exe",
        plugin_dir / "tools" / "spooky-bridge.exe",
        plugin_dir / "tools" / "spooky-bridge" / "spooky-bridge.exe",
    ]
    for path in candidates:
        if path.is_file():
            return path

    return None


def _resolve_record_paths(idx, organizer, rec_spec: dict, index: int) -> dict:
    """Resolve plugin names in a record spec to disk paths.

    Takes the user-facing record spec (plugin names) and returns
    a bridge-ready spec (absolute disk paths with forward slashes).
    """
    op = rec_spec.get("op", "")
    formid_str = rec_spec.get("formid", "")

    if not op:
        raise ValueError("'op' is required")
    if not formid_str:
        raise ValueError("'formid' is required")

    result: dict = {"op": op, "formid": formid_str}

    if op == "override":
        # Resolve source plugin path
        source_plugin = rec_spec.get("source_plugin")
        origin, local_id = _parse_formid_str(formid_str)
        if origin is None:
            raise ValueError(f"Invalid FormID: {formid_str}")

        chain = idx.get_conflict_chain(origin, local_id, include_disabled=True)
        if not chain:
            raise ValueError(f"Record {formid_str} not found in index")

        if source_plugin:
            sp_lower = source_plugin.lower()
            ref = None
            for r in chain:
                if r.plugin.lower() == sp_lower:
                    ref = r
                    break
            if ref is None:
                raise ValueError(
                    f"Record not found in plugin '{source_plugin}'"
                )
        else:
            ref = chain[-1]  # winning record

        pinfo = idx.get_plugin_info(ref.plugin)
        if pinfo is None or not os.path.exists(pinfo.path):
            raise ValueError(f"Plugin file not found: {ref.plugin}")

        result["source_path"] = pinfo.path.replace("\\", "/")

        # Pass through all modification parameters
        passthrough_keys = (
            "set_fields", "set_flags", "clear_flags",
            "add_keywords", "remove_keywords",
            "add_spells", "remove_spells",
            "add_perks", "remove_perks",
            "add_packages", "remove_packages",
            "add_factions", "remove_factions",
            "add_inventory", "remove_inventory",
            "add_outfit_items", "remove_outfit_items",
            "add_form_list_entries", "remove_form_list_entries",
            "add_items",
            "add_conditions", "remove_conditions",
            "attach_scripts",
            "set_enchantment", "clear_enchantment",
        )
        for key in passthrough_keys:
            val = rec_spec.get(key)
            if val is not None:
                result[key] = val

    elif op == "merge_leveled_list":
        origin, local_id = _parse_formid_str(formid_str)
        if origin is None:
            raise ValueError(f"Invalid FormID: {formid_str}")

        # Base plugin: explicit base_plugin param, or fall back to FormID origin
        base_plugin = rec_spec.get("base_plugin", origin)
        base_pinfo = idx.get_plugin_info(base_plugin)
        if base_pinfo is None or not os.path.exists(base_pinfo.path):
            raise ValueError(f"Base plugin not found: {base_plugin}")

        result["base_path"] = base_pinfo.path.replace("\\", "/")

        # Override plugins
        override_plugins = rec_spec.get("override_plugins", [])
        if not override_plugins:
            raise ValueError("'override_plugins' required for merge_leveled_list")

        override_paths = []
        for pname in override_plugins:
            pi = idx.get_plugin_info(pname)
            if pi is None or not os.path.exists(pi.path):
                raise ValueError(f"Override plugin not found: {pname}")
            override_paths.append(pi.path.replace("\\", "/"))

        result["override_paths"] = override_paths

    else:
        raise ValueError(f"Unknown operation: '{op}'")

    return result


def _validate_alias_properties(bridge_records: list, idx, bridge_path: Path) -> list:
    """Walk bridge-ready records for VMAD attach_scripts Alias properties and
    verify each referenced (quest, alias_id) pair resolves to a real alias.

    Returns a list of error dicts. Empty list = all good.

    For each unique quest FormID, fires one bridge read_record call to fetch
    the quest's Aliases list, then checks every alias_id referenced against it.
    Quests not resolvable in the load order are reported as errors so the user
    knows something is off rather than writing a patch that xEdit will warn on.
    """
    # Collect (record_index, quest_formid, alias_id, prop_name) tuples
    references = []
    for i, rec in enumerate(bridge_records):
        scripts = rec.get("attach_scripts") or []
        for script in scripts:
            for prop in script.get("properties", []) or []:
                if (prop.get("type") or "").lower() != "alias":
                    continue
                val = prop.get("value") or {}
                if not isinstance(val, dict):
                    continue
                quest = val.get("quest")
                aid = val.get("alias_id")
                if quest and aid is not None:
                    references.append((i, quest, aid, prop.get("name", "")))

    if not references:
        return []

    # For each unique quest FormID, fetch aliases once
    unique_quests = {ref[1] for ref in references}
    quest_aliases: dict[str, set[int] | None] = {}
    quest_names: dict[str, str] = {}

    for quest_fid in unique_quests:
        origin, local_id = _parse_formid_str(quest_fid)
        if origin is None:
            quest_aliases[quest_fid] = None
            continue

        chain = idx.get_conflict_chain(origin, local_id, include_disabled=True)
        if not chain:
            quest_aliases[quest_fid] = None
            continue

        ref = chain[-1]  # winning record for alias list
        pinfo = idx.get_plugin_info(ref.plugin)
        if pinfo is None or not os.path.exists(pinfo.path):
            quest_aliases[quest_fid] = None
            continue

        response = _run_bridge_read_patching(bridge_path, {
            "command": "read_record",
            "plugin_path": pinfo.path.replace("\\", "/"),
            "formid": f"{origin}:{local_id:06X}",
        })
        if not response.get("success"):
            quest_aliases[quest_fid] = None
            continue

        fields = response.get("fields") or {}
        aliases = fields.get("Aliases") or []
        ids = set()
        for a in aliases:
            if isinstance(a, dict) and "ID" in a:
                try:
                    ids.add(int(a["ID"]))
                except (TypeError, ValueError):
                    pass
        quest_aliases[quest_fid] = ids
        edid = response.get("editor_id") or ""
        quest_names[quest_fid] = edid

    # Validate each reference
    errors = []
    for rec_idx, quest_fid, aid, prop_name in references:
        valid_ids = quest_aliases.get(quest_fid)
        if valid_ids is None:
            errors.append({
                "record_index": rec_idx,
                "quest": quest_fid,
                "property": prop_name,
                "alias_id": aid,
                "error": f"Quest {quest_fid} not resolvable in load order — cannot verify alias_id {aid}.",
            })
            continue
        try:
            aid_int = int(aid)
        except (TypeError, ValueError):
            errors.append({
                "record_index": rec_idx,
                "quest": quest_fid,
                "property": prop_name,
                "alias_id": aid,
                "error": f"alias_id must be an integer, got {aid!r}.",
            })
            continue
        if aid_int not in valid_ids:
            qname = quest_names.get(quest_fid) or quest_fid
            sorted_ids = sorted(valid_ids)
            errors.append({
                "record_index": rec_idx,
                "quest": quest_fid,
                "quest_editor_id": qname,
                "property": prop_name,
                "alias_id": aid_int,
                "error": (
                    f"Quest {qname} ({quest_fid}) has no alias with ID {aid_int}. "
                    f"Available IDs: {sorted_ids}"
                ),
            })
    return errors


def _run_bridge_read_patching(bridge: Path, request: dict, timeout: int = 15) -> dict:
    """Lightweight bridge invocation used by alias pre-validation.
    Mirrors tools_records._run_bridge_read so we don't import across modules."""
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
        return {"success": False, "error": "Bridge returned no output."}
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        return {"success": False, "error": "Bridge returned invalid JSON."}
