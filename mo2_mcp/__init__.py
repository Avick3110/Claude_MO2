import json
import os

from PyQt6.QtCore import qInfo, qWarning
from PyQt6.QtGui import QIcon
from PyQt6.QtWidgets import QMessageBox

import mobase

from .config import (
    DEFAULT_PORT,
    DEFAULT_OUTPUT_MOD,
    DEFAULT_AUTO_START,
    PLUGIN_NAME,
    PLUGIN_VERSION,
    PLUGIN_AUTHOR,
    PLUGIN_DESCRIPTION,
)
from .mcp_server import MCPServer
from .tools_modlist import register_modlist_tools
from .tools_filesystem import register_filesystem_tools
from .tools_write import register_write_tools
from .tools_records import register_record_tools
from .tools_dll import register_dll_tools
from .tools_patching import register_patching_tools
from .tools_papyrus import register_papyrus_tools
from .tools_archive import register_archive_tools
from .tools_nif import register_nif_tools
from .tools_audio import register_audio_tools


def _ensure_claude_mcp_config(port: int) -> None:
    """Register this server as a user-scoped MCP server in ~/.claude.json.

    Claude Code stores user-scoped MCP servers under the top-level
    `mcpServers` key of ~/.claude.json. Registering there makes the server
    discoverable from any project directory without per-project `.mcp.json`
    files — which matters on Windows where spaces in the install path can
    break project-level discovery.

    The write is atomic (temp file + os.replace) so a crash mid-write cannot
    corrupt ~/.claude.json. Skips silently if the file does not exist
    (Claude Code not installed) or if anything goes wrong — server startup
    must never fail because of this.
    """
    try:
        config_path = os.path.expanduser("~/.claude.json")
        if not os.path.isfile(config_path):
            return

        with open(config_path, "r", encoding="utf-8") as f:
            config = json.load(f)

        entry = {
            "type": "http",
            "url": f"http://127.0.0.1:{port}/mcp",
        }
        servers = config.setdefault("mcpServers", {})
        if servers.get("mo2") == entry:
            return

        servers["mo2"] = entry

        tmp_path = config_path + ".mo2-tmp"
        with open(tmp_path, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2)
            f.write("\n")
        os.replace(tmp_path, config_path)

        qInfo(f"{PLUGIN_NAME}: registered MCP server with Claude Code in {config_path}")
    except Exception as exc:
        qWarning(f"{PLUGIN_NAME}: failed to update Claude Code MCP config: {exc}")


class Mo2McpPlugin(mobase.IPluginTool):

    def __init__(self):
        super().__init__()
        self._organizer = None
        self._parent_widget = None
        self._server = None
        self._was_running_before_launch = False

    # ── IPlugin interface ────────────────────────────────────────────

    def init(self, organizer: mobase.IOrganizer) -> bool:
        self._organizer = organizer
        organizer.onAboutToRun(self._on_about_to_run)
        organizer.onFinishedRun(self._on_finished_run)
        qInfo(f"{PLUGIN_NAME}: plugin loaded")
        return True

    def name(self) -> str:
        return PLUGIN_NAME

    def author(self) -> str:
        return PLUGIN_AUTHOR

    def description(self) -> str:
        return PLUGIN_DESCRIPTION

    def version(self) -> mobase.VersionInfo:
        major, minor, patch = PLUGIN_VERSION
        return mobase.VersionInfo(major, minor, patch, mobase.ReleaseType.FINAL)

    def settings(self) -> list[mobase.PluginSetting]:
        return [
            mobase.PluginSetting(
                "port",
                "TCP port for the MCP server",
                DEFAULT_PORT,
            ),
            mobase.PluginSetting(
                "output-mod",
                "Name of the mod folder for Claude's file output",
                DEFAULT_OUTPUT_MOD,
            ),
            mobase.PluginSetting(
                "auto-start",
                "Start the MCP server automatically when MO2 launches",
                DEFAULT_AUTO_START,
            ),
            mobase.PluginSetting(
                "spooky-bridge-path",
                "Path to spooky-bridge.exe (leave empty for auto-detect)",
                "",
            ),
            mobase.PluginSetting(
                "spooky-cli-path",
                "Path to spookys-automod.exe (leave empty for auto-detect)",
                "",
            ),
        ]

    def requirements(self) -> list[mobase.IPluginRequirement]:
        return [
            mobase.PluginRequirementFactory.gameDependency({
                "Skyrim Special Edition",
            })
        ]

    # ── IPluginTool interface ────────────────────────────────────────

    def displayName(self) -> str:
        return "Start/Stop Claude Server"

    def tooltip(self) -> str:
        return "Toggle the MCP server for Claude integration"

    def icon(self) -> QIcon:
        return QIcon()

    def setParentWidget(self, widget) -> None:
        self._parent_widget = widget

    def display(self) -> None:
        if self._server and self._server.is_running():
            self._stop_server()
        else:
            self._start_server()

    # ── Server lifecycle ─────────────────────────────────────────────

    def _start_server_core(self) -> bool:
        """Start the MCP server. Returns True on success, False on failure."""
        port = self._organizer.pluginSetting(self.name(), "port")
        self._server = MCPServer(port)
        self._register_tools()
        try:
            self._server.start()
        except OSError as e:
            qWarning(f"{PLUGIN_NAME}: failed to start server: {e}")
            self._server = None
            return False
        return True

    def _start_server(self) -> None:
        port = self._organizer.pluginSetting(self.name(), "port")
        if not self._start_server_core():
            QMessageBox.critical(
                self._parent_widget,
                PLUGIN_NAME,
                f"Failed to start MCP server on port {port}.",
            )
            return
        _ensure_claude_mcp_config(port)
        QMessageBox.information(
            self._parent_widget,
            PLUGIN_NAME,
            f"MCP server started on localhost:{port}\n\n"
            f"Claude Code MCP config updated automatically.\n"
            f"Restart Claude Code if this is the first time.",
        )

    def _stop_server(self) -> None:
        if self._server:
            self._server.stop()
            self._server = None
        QMessageBox.information(
            self._parent_widget,
            PLUGIN_NAME,
            "MCP server stopped.",
        )

    # ── Auto-stop/restart around executable launches ────────────────

    def _on_about_to_run(self, app_path: str) -> bool:
        """Called by MO2 before launching any executable."""
        if self._server and self._server.is_running():
            qInfo(f"{PLUGIN_NAME}: stopping server before launch of {app_path}")
            self._server.stop()
            self._server = None
            self._was_running_before_launch = True
        else:
            self._was_running_before_launch = False
        return True

    def _on_finished_run(self, app_path: str, exit_code: int) -> None:
        """Called by MO2 after a launched executable finishes."""
        if self._was_running_before_launch:
            self._was_running_before_launch = False
            qInfo(f"{PLUGIN_NAME}: restarting server after {app_path} exited (code {exit_code})")
            if self._start_server_core():
                port = self._organizer.pluginSetting(self.name(), "port")
                _ensure_claude_mcp_config(port)
                qInfo(f"{PLUGIN_NAME}: server restarted successfully")
            else:
                qWarning(f"{PLUGIN_NAME}: failed to restart server after launch")

    # ── Tool registration ────────────────────────────────────────────

    def _register_tools(self) -> None:
        reg = self._server.registry
        organizer = self._organizer

        reg.register(
            name="mo2_ping",
            description="Check if the MO2 MCP server is running. Returns server version and MO2 info.",
            input_schema={
                "type": "object",
                "properties": {},
            },
            handler=lambda args: json.dumps({
                "status": "ok",
                "server": PLUGIN_NAME,
                "version": ".".join(str(v) for v in PLUGIN_VERSION),
                "mo2_version": str(organizer.appVersion()),
                "game": organizer.managedGame().gameName(),
                "profile": organizer.profile().name(),
            }, indent=2),
        )

        # Register mod/plugin query tools
        register_modlist_tools(reg, organizer)

        # Register filesystem tools
        register_filesystem_tools(reg, organizer)

        # Register write tools
        register_write_tools(reg, organizer)

        # Register record-level query tools (Phase 2)
        register_record_tools(reg, organizer)

        # Register DLL analysis tools
        register_dll_tools(reg, organizer)

        # Register patch creation tools (Spooky-backed Mutagen bridge)
        register_patching_tools(reg, organizer)

        # Register Papyrus compile tool (calls PapyrusCompiler.exe directly)
        register_papyrus_tools(reg, organizer)

        # Register BSA/BA2 archive tools (Spooky CLI subprocess, needs BSArch.exe)
        register_archive_tools(reg, organizer)

        # Register NIF mesh inspection tools (Spooky CLI; texture/shader ops need nif-tool.exe)
        register_nif_tools(reg, organizer)

        # Register audio/voice tools (Spooky CLI)
        register_audio_tools(reg, organizer)


def createPlugin() -> mobase.IPlugin:
    return Mo2McpPlugin()
