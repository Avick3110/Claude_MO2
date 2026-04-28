// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MutagenBridge;

// ── Request Models ──────────────────────────────────────────────────

/// <summary>
/// Wrapper used by Program.cs to dispatch on the top-level "command" field.
/// Defaults to "patch" when absent for backward compatibility with v1.2.0 payloads.
/// </summary>
public class RequestEnvelope
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "patch";
}

public class PatchRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "patch";

    [JsonPropertyName("output_path")]
    public string OutputPath { get; set; } = "";

    [JsonPropertyName("esl_flag")]
    public bool EslFlag { get; set; } = true;

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Claude MO2";

    [JsonPropertyName("records")]
    public List<RecordOperation> Records { get; set; } = new();

    [JsonPropertyName("load_order")]
    public LoadOrderContext? LoadOrder { get; set; }
}

// ── Load-Order Context ──────────────────────────────────────────────
//
// Populated by Python from MO2's profile (loadorder.txt + plugins.txt
// + Skyrim.ccc) plus the per-plugin disk paths already cached in the
// record index. The bridge uses this to drive Mutagen's write-path
// master-style lookup — BeginWrite.WithLoadOrder(styledGetters) — so
// FormLinks pointing at ESL/Light-flagged masters compact correctly
// (the v2.5.x write bug that motivated v2.6.0). Required for "patch"
// command; optional for read commands, where CreateFromBinaryOverlay
// already returns FormKey-correct results (per Phase 0 verification).

public class LoadOrderContext
{
    [JsonPropertyName("game_release")]
    public string GameRelease { get; set; } = "SkyrimSE";

    [JsonPropertyName("listings")]
    public List<LoadOrderListingEntry> Listings { get; set; } = new();

    // Forward-compat for Phase 3 (env-aware reads). Phase 2's
    // LoadOrderContextResolver reads master-style headers directly
    // from each listing's path and does not consult these fields.
    // Populated by the Python caller so Phase 3 / later phases don't
    // need to revise both sides of the JSON contract.
    [JsonPropertyName("data_folder")]
    public string? DataFolder { get; set; }

    [JsonPropertyName("ccc_path")]
    public string? CccPath { get; set; }
}

public class LoadOrderListingEntry
{
    [JsonPropertyName("mod_key")]
    public string ModKey { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

// ── FUZ Audio Request / Response ────────────────────────────────────

public class FuzInfoRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "fuz_info";

    [JsonPropertyName("fuz_path")]
    public string FuzPath { get; set; } = "";
}

public class FuzInfoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("fuz_path")]
    public string? FuzPath { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("lip_size")]
    public long? LipSize { get; set; }

    [JsonPropertyName("xwm_size")]
    public long? XwmSize { get; set; }

    [JsonPropertyName("version_supported")]
    public bool? VersionSupported { get; set; }

    public static FuzInfoResponse Fail(string error) => new() { Success = false, Error = error };
}

public class FuzExtractRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "fuz_extract";

    [JsonPropertyName("fuz_path")]
    public string FuzPath { get; set; } = "";

    [JsonPropertyName("output_dir")]
    public string OutputDir { get; set; } = "";
}

public class FuzExtractResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("fuz_path")]
    public string? FuzPath { get; set; }

    [JsonPropertyName("lip_path")]
    public string? LipPath { get; set; }

    [JsonPropertyName("xwm_path")]
    public string? XwmPath { get; set; }

    [JsonPropertyName("lip_size")]
    public long? LipSize { get; set; }

    [JsonPropertyName("xwm_size")]
    public long? XwmSize { get; set; }

    public static FuzExtractResponse Fail(string error) => new() { Success = false, Error = error };
}

// ── Read Request / Response ─────────────────────────────────────────

public class ReadRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "read_record";

    [JsonPropertyName("plugin_path")]
    public string PluginPath { get; set; } = "";

    [JsonPropertyName("formid")]
    public string FormId { get; set; } = "";

    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; } = 6;

    // Optional in Phase 2 — RecordReader falls through to its
    // CreateFromBinaryOverlay-per-plugin path when null. Phase 3 can
    // switch reads to an env-aware path when this is supplied.
    [JsonPropertyName("load_order")]
    public LoadOrderContext? LoadOrder { get; set; }

    /// <summary>
    /// v2.9.2 — optional projection list. Each path is dot-segmented
    /// (e.g. "EditorID", "ActorEffect", "Voices.Male"). The reader walks
    /// only the requested paths; out-of-projection branches are omitted
    /// from the response. Auto-traverses lists and dicts mid-path per
    /// Q1 lock (auto-traversal). Empty list: rejected at validation
    /// time with a clear error. Absence (null): full payload (v2.9.1
    /// default — bit-identical preserved).
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string>? Fields { get; set; }

    /// <summary>
    /// v2.9.2 — optional FormLink expansion list. Each path is dot-
    /// segmented (e.g. "ActorEffect", "Class"). When the walker
    /// encounters a FormLink at a named path, descends into the linked
    /// record and inlines its detail. Single-level only — links inside
    /// expanded records render as plain FormID strings (no recursion,
    /// no cycle detection needed). Output shape per Q2 lock (wrapper
    /// form: <c>{ formid, EditorID, expanded: { ... } }</c>). Empty
    /// list: rejected. Absence (null): no expansion (v2.9.1 default).
    /// FormLink predicate matches PatchEngine.cs:1182 IsFormLinkType.
    /// </summary>
    [JsonPropertyName("expand_links")]
    public List<string>? ExpandLinks { get; set; }

    /// <summary>
    /// v2.9.2 P4 — optional load-order plugin paths the bridge may
    /// hot-load on demand to resolve cross-master FormLink expansions.
    /// Phase 3 surfaced bug B5: <c>ExpandFormLinkValue</c>'s filename-
    /// match scan only sees plugins explicitly loaded for the current
    /// invocation (typically just the parent record's winning plugin),
    /// so a FormLink whose originating master isn't in <c>modCache</c>
    /// returned <c>error: "FormID target not in load order"</c>. Option
    /// B fix (locked 2026-04-28): the wrapper passes the full enabled
    /// load-order plugin file path list; the bridge lazily loads the
    /// matching plugin on the first miss for a given originating-master
    /// filename, caches it in <c>modCache</c>, and retries the lookup.
    /// Lazy: zero cost when a FormLink resolves in-master; pays the
    /// load cost only when the cross-master miss path fires. Absence
    /// (null) preserves v2.9.2 P2 behavior bit-identically (cross-
    /// master expansion fails with the original missing-master error).
    /// </summary>
    [JsonPropertyName("available_plugins")]
    public List<string>? AvailablePlugins { get; set; }
}

public class ReadResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("formid")]
    public string? FormId { get; set; }

    [JsonPropertyName("record_type")]
    public string? RecordType { get; set; }

    [JsonPropertyName("editor_id")]
    public string? EditorId { get; set; }

    [JsonPropertyName("plugin")]
    public string? Plugin { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, object?>? Fields { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_detail")]
    public string? ErrorDetail { get; set; }

    /// <summary>
    /// v2.9.2 — populated by pre-flight validation when one or more
    /// <c>fields</c> / <c>expand_links</c> paths fail. Keyed by record
    /// type code (e.g. "RACE", "QUST"); each value carries three
    /// per-category lists plus the type's valid-name lists for
    /// caller-side fixup. Multi-error accumulation per § D — all bad
    /// entries surface in one round-trip. When non-null,
    /// <see cref="Success"/> is false and <see cref="Error"/> carries
    /// the human-readable summary.
    /// </summary>
    [JsonPropertyName("validation_errors")]
    public Dictionary<string, ValidationErrorDetail>? ValidationErrors { get; set; }
}

/// <summary>
/// v2.9.2 — per-record-type validation error envelope. Three category
/// lists for what failed, plus the valid-name lists for context.
/// Symmetric with v2.9.1's "Tier D" multi-error accumulation pattern.
/// </summary>
public class ValidationErrorDetail
{
    [JsonPropertyName("bad_field_paths")]
    public List<string> BadFieldPaths { get; set; } = new();

    [JsonPropertyName("bad_expansion_targets")]
    public List<string> BadExpansionTargets { get; set; } = new();

    [JsonPropertyName("non_formlink_expansion_targets")]
    public List<string> NonFormLinkExpansionTargets { get; set; } = new();

    [JsonPropertyName("valid_field_names")]
    public List<string> ValidFieldNames { get; set; } = new();

    [JsonPropertyName("valid_formlink_field_names")]
    public List<string> ValidFormLinkFieldNames { get; set; } = new();
}

// ── Batch Read Request / Response ───────────────────────────────────

public class ReadBatchItem
{
    [JsonPropertyName("plugin_path")]
    public string PluginPath { get; set; } = "";

    [JsonPropertyName("formid")]
    public string FormId { get; set; } = "";
}

public class ReadBatchRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "read_records";

    [JsonPropertyName("records")]
    public List<ReadBatchItem> Records { get; set; } = new();

    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; } = 6;

    // Optional in Phase 2 — same semantics as ReadRequest.LoadOrder.
    [JsonPropertyName("load_order")]
    public LoadOrderContext? LoadOrder { get; set; }

    /// <summary>
    /// v2.9.2 — optional projection list applied to every batch item.
    /// Same semantics as <see cref="ReadRequest.Fields"/>. Per-record
    /// validation runs against each unique record type encountered in
    /// the batch (per § D's per-type validation lock); validation
    /// errors accumulate per type with multi-error rollup before any
    /// reads happen.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string>? Fields { get; set; }

    /// <summary>
    /// v2.9.2 — optional FormLink expansion list applied to every
    /// batch item. Same semantics as <see cref="ReadRequest.ExpandLinks"/>.
    /// </summary>
    [JsonPropertyName("expand_links")]
    public List<string>? ExpandLinks { get; set; }

    /// <summary>
    /// v2.9.2 P4 — optional load-order plugin paths for cross-master
    /// FormLink expansion. Same semantics as
    /// <see cref="ReadRequest.AvailablePlugins"/>: the bridge hot-loads
    /// a matching plugin on demand when <c>ExpandFormLinkValue</c>
    /// misses the originating-master filename in <c>modCache</c>. Lazy
    /// — only paid when the miss path fires.
    /// </summary>
    [JsonPropertyName("available_plugins")]
    public List<string>? AvailablePlugins { get; set; }
}

public class ReadBatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("records")]
    public List<ReadResponse> Records { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_detail")]
    public string? ErrorDetail { get; set; }

    /// <summary>
    /// v2.9.2 — per § D's strict-batch validation contract: when one
    /// or more <c>fields</c> / <c>expand_links</c> paths fail
    /// validation against any record type encountered in the batch,
    /// validation errors accumulate keyed by type and surface here
    /// with <see cref="Success"/> = false. Per-record reads do NOT
    /// run on validation failure (rollback contract; pre-flight per
    /// Q4 lock).
    /// </summary>
    [JsonPropertyName("validation_errors")]
    public Dictionary<string, ValidationErrorDetail>? ValidationErrors { get; set; }
}

// ── Scan Request / Response ─────────────────────────────────────────
//
// v2.6.0 Phase 3: bridge-fed record index. Python sends a list of plugin
// file paths; bridge opens each via CreateFromBinaryOverlay, reads
// the header + every Major record, and returns origin-resolved FormKeys
// (which match xEdit by construction for ESL plugins — Mutagen's FormKey
// already encodes the compacted slot ID).
//
// Per-plugin failures surface inline on `ScannedPlugin.Error` so a single
// bad file doesn't abort the batch. Top-level `Error` is set only when
// every plugin in the batch failed (or the request was malformed).
//
// `ScannedRecord` is deliberately minimal — type, formid, edid only. The
// aggregate response for a 3000+ plugin Skyrim modlist is ~2.9M records,
// so every extra field per record bloats the JSON envelope significantly.
// Add fields when a Python consumer explicitly needs them.

public class ScanRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "scan";

    [JsonPropertyName("plugins")]
    public List<string> Plugins { get; set; } = new();
}

public class ScanResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("plugins")]
    public List<ScannedPlugin> Plugins { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_detail")]
    public string? ErrorDetail { get; set; }
}

public class ScannedPlugin
{
    [JsonPropertyName("plugin_name")]
    public string PluginName { get; set; } = "";

    [JsonPropertyName("plugin_path")]
    public string PluginPath { get; set; } = "";

    [JsonPropertyName("masters")]
    public List<string> Masters { get; set; } = new();

    [JsonPropertyName("is_master")]
    public bool IsMaster { get; set; }

    [JsonPropertyName("is_light")]
    public bool IsLight { get; set; }

    [JsonPropertyName("is_localized")]
    public bool IsLocalized { get; set; }

    [JsonPropertyName("record_count")]
    public int RecordCount { get; set; }

    [JsonPropertyName("records")]
    public List<ScannedRecord> Records { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class ScannedRecord
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("formid")]
    public string FormId { get; set; } = "";

    [JsonPropertyName("edid")]
    public string? EditorId { get; set; }
}

public class RecordOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("formid")]
    public string FormId { get; set; } = "";

    [JsonPropertyName("source_path")]
    public string? SourcePath { get; set; }

    // ── Field modifications ──

    [JsonPropertyName("set_fields")]
    public Dictionary<string, JsonElement>? SetFields { get; set; }

    [JsonPropertyName("set_flags")]
    public List<string>? SetFlags { get; set; }

    [JsonPropertyName("clear_flags")]
    public List<string>? ClearFlags { get; set; }

    // ── List modifications (keywords, spells, perks) ──

    [JsonPropertyName("add_keywords")]
    public List<string>? AddKeywords { get; set; }

    [JsonPropertyName("remove_keywords")]
    public List<string>? RemoveKeywords { get; set; }

    [JsonPropertyName("add_spells")]
    public List<string>? AddSpells { get; set; }

    [JsonPropertyName("remove_spells")]
    public List<string>? RemoveSpells { get; set; }

    [JsonPropertyName("add_perks")]
    public List<string>? AddPerks { get; set; }

    [JsonPropertyName("remove_perks")]
    public List<string>? RemovePerks { get; set; }

    // ── List modifications (packages, factions, inventory) ──

    [JsonPropertyName("add_packages")]
    public List<string>? AddPackages { get; set; }

    [JsonPropertyName("remove_packages")]
    public List<string>? RemovePackages { get; set; }

    [JsonPropertyName("add_factions")]
    public List<FactionEntry>? AddFactions { get; set; }

    [JsonPropertyName("remove_factions")]
    public List<string>? RemoveFactions { get; set; }

    [JsonPropertyName("add_inventory")]
    public List<InventoryEntry>? AddInventory { get; set; }

    [JsonPropertyName("remove_inventory")]
    public List<string>? RemoveInventory { get; set; }

    // ── Outfit, FormList ──

    [JsonPropertyName("add_outfit_items")]
    public List<string>? AddOutfitItems { get; set; }

    [JsonPropertyName("remove_outfit_items")]
    public List<string>? RemoveOutfitItems { get; set; }

    [JsonPropertyName("add_form_list_entries")]
    public List<string>? AddFormListEntries { get; set; }

    [JsonPropertyName("remove_form_list_entries")]
    public List<string>? RemoveFormListEntries { get; set; }

    // ── Leveled list entries (for override op) ──

    [JsonPropertyName("add_items")]
    public List<LeveledEntry>? AddItems { get; set; }

    // ── Conditions ──

    [JsonPropertyName("add_conditions")]
    public List<ConditionEntry>? AddConditions { get; set; }

    [JsonPropertyName("remove_conditions")]
    public List<ConditionRemoval>? RemoveConditions { get; set; }

    /// <summary>
    /// v2.9.1 — selects which condition list on a multi-condition-list carrier
    /// to write to / remove from. Required on QUST records (which expose
    /// <c>DialogConditions</c> and <c>EventConditions</c> rather than a single
    /// <c>Conditions</c> list); rejected on records exposing a single
    /// <c>Conditions</c> property (PERK / PACK / IDLE / MGEF / INFO via
    /// response-level conditions). Records with no condition list at all
    /// fall through to Tier D's uniform <c>unmatched_operators</c> shape.
    /// Valid values: "dialog" (→ DialogConditions), "event" (→ EventConditions).
    /// Case-insensitive per Phase 0 lock. See KNOWN_ISSUES.md § Patching write
    /// surface.
    /// </summary>
    [JsonPropertyName("condition_target")]
    public string? ConditionTarget { get; set; }

    // ── VMAD script attachment ──

    [JsonPropertyName("attach_scripts")]
    public List<ScriptAttachment>? AttachScripts { get; set; }

    // ── Enchantment ──

    [JsonPropertyName("set_enchantment")]
    public string? SetEnchantment { get; set; }

    [JsonPropertyName("clear_enchantment")]
    public bool? ClearEnchantment { get; set; }

    // ── Merge leveled list (for "merge_leveled_list" op) ──

    [JsonPropertyName("base_path")]
    public string? BasePath { get; set; }

    [JsonPropertyName("override_paths")]
    public List<string>? OverridePaths { get; set; }
}

// ── Supporting Models ───────────────────────────────────────────────

public class LeveledEntry
{
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = "";

    [JsonPropertyName("level")]
    public short Level { get; set; } = 1;

    [JsonPropertyName("count")]
    public short Count { get; set; } = 1;
}

public class FactionEntry
{
    [JsonPropertyName("faction")]
    public string Faction { get; set; } = "";

    [JsonPropertyName("rank")]
    public int Rank { get; set; } = 0;
}

public class InventoryEntry
{
    [JsonPropertyName("item")]
    public string Item { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class ConditionEntry
{
    [JsonPropertyName("function")]
    public string Function { get; set; } = "";

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "==";

    [JsonPropertyName("value")]
    public float? Value { get; set; }

    [JsonPropertyName("global")]
    public string? Global { get; set; }

    [JsonPropertyName("run_on")]
    public string RunOn { get; set; } = "Subject";

    [JsonPropertyName("parameter_a")]
    public string? ParameterA { get; set; }

    /// <summary>
    /// v2.8.0 — optional ActorValue argument for Condition functions whose
    /// underlying Mutagen ConditionData carries an `ActorValue` enum slot
    /// (GetActorValue, GetBaseActorValue, GetActorValuePercent, etc.). When
    /// supplied, the bridge parses this string via <c>Enum.Parse&lt;ActorValue&gt;</c>
    /// and assigns it to the constructed ConditionData. Without it, the
    /// underlying enum default (index 0 = Aggression) is preserved — the
    /// pre-v2.8.0 behavior; this field is purely additive.
    /// v2.9 — kept as back-compat syntactic sugar for
    /// <c>parameters: {ActorValue: ...}</c>; both forms route to the same
    /// ConditionData slot. Supplying both for the same condition surfaces
    /// an unambiguous-DSL error (see <see cref="Parameters"/>).
    /// </summary>
    [JsonPropertyName("actor_value")]
    public string? ActorValue { get; set; }

    /// <summary>
    /// v2.9 — generic Condition-function parameter slot map. Each key is a
    /// Mutagen reflection property name on the function's
    /// <c>{Function}ConditionData</c> class (e.g. <c>"Object"</c> on
    /// <c>GetIsIDConditionData</c>, <c>"Faction"</c> on
    /// <c>GetInFactionConditionData</c>, <c>"Stage"</c> on
    /// <c>GetStageDoneConditionData</c>). Each value is JSON-typed per the
    /// slot's runtime type — string FormID for <c>IFormLinkOrIndex&lt;T&gt;</c>
    /// / <c>IFormLink&lt;T&gt;</c>, string for enum, number for int/float,
    /// bool for bool. Per-function slot signatures live in
    /// <c>dev/plans/v2.9.X_condition_parameters/CONDITIONS_AUDIT.md</c>;
    /// functions outside the v2.9.x in-scope set surface a clean per-record
    /// "not yet wired" error when <c>parameters</c> is supplied.
    /// Back-compat: the v2.8 <see cref="ActorValue"/> field is still accepted
    /// as syntactic sugar for <c>parameters: {ActorValue: ...}</c>; supplying
    /// both forms for the same condition surfaces an unambiguous-DSL error.
    /// CTDA padding slots (any name containing "Unused") are explicitly
    /// rejected by the dispatcher (footgun-guard) — see CONDITIONS_AUDIT.md
    /// § Architectural surprises §3.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, JsonElement>? Parameters { get; set; }

    [JsonPropertyName("or_flag")]
    public bool OrFlag { get; set; } = false;
}

public class ConditionRemoval
{
    [JsonPropertyName("function")]
    public string? Function { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }
}

public class ScriptAttachment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("properties")]
    public List<ScriptPropertyEntry>? Properties { get; set; }
}

public class ScriptPropertyEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Int";

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

// ── Response Models ─────────────────────────────────────────────────

public class PatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("output_path")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("records_written")]
    public int RecordsWritten { get; set; }

    [JsonPropertyName("successful_count")]
    public int SuccessfulCount { get; set; }

    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    [JsonPropertyName("esl_flagged")]
    public bool EslFlagged { get; set; }

    [JsonPropertyName("masters")]
    public List<string> Masters { get; set; } = new();

    [JsonPropertyName("details")]
    public List<RecordDetail> Details { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_detail")]
    public string? ErrorDetail { get; set; }
}

public class RecordDetail
{
    [JsonPropertyName("formid")]
    public string FormId { get; set; } = "";

    [JsonPropertyName("record_type")]
    public string? RecordType { get; set; }

    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("modifications")]
    public Dictionary<string, object>? Modifications { get; set; }

    [JsonPropertyName("entries_merged")]
    public int? EntriesMerged { get; set; }

    [JsonPropertyName("sources")]
    public List<string>? Sources { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    // Populated by Tier D silent-failure detection (v2.7.1) when one or more
    // requested operators have no handler for this record type. Each entry is
    // a user-facing operator name (e.g. "add_perks"). When set, the override
    // has been rolled back and Error carries the human-readable summary.
    [JsonPropertyName("unmatched_operators")]
    public List<string>? UnmatchedOperators { get; set; }
}
