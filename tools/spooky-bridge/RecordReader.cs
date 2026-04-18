// spooky-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Collections;
using System.Reflection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;

namespace SpookyBridge;

/// <summary>
/// Loads a single plugin via Mutagen and renders one record's fields as a
/// JSON-serializable Dictionary. Supersedes v1.2.0's esp_schema / esp_fields
/// Python schema walker — Mutagen's typed API is the source of truth.
///
/// Single-plugin read (no link cache / master resolution). FormLinks render
/// as "Plugin:FormID" strings. Enums render as their names. Nested Mutagen
/// records render recursively as nested dicts.
/// </summary>
public class RecordReader
{
    private static readonly HashSet<string> SkipPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mutagen infrastructure — not useful to Claude
        "FormKey",
        "FormVersion",
        "VersionControl",
        "Registration",
        "StaticRegistration",
        "Version2",
    };

    /// <summary>
    /// Batch-read N records. Plugins are loaded at most once per batch
    /// (keyed by path), so "read same FormID from 8 plugins" costs 8 plugin
    /// loads but "read 50 FormIDs from 1 plugin" costs only 1 load.
    /// </summary>
    public ReadBatchResponse ReadBatch(ReadBatchRequest request)
    {
        var response = new ReadBatchResponse { Success = true };

        if (request.Records.Count == 0)
        {
            response.Success = false;
            response.Error = "records list is empty.";
            return response;
        }

        var modCache = new Dictionary<string, ISkyrimModGetter>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in request.Records)
            {
                response.Records.Add(ReadOne(item, request.MaxDepth, modCache));
            }
        }
        finally
        {
            foreach (var mod in modCache.Values)
            {
                if (mod is IDisposable d) d.Dispose();
            }
        }

        return response;
    }

    private ReadResponse ReadOne(ReadBatchItem item, int maxDepth, Dictionary<string, ISkyrimModGetter> modCache)
    {
        try
        {
            if (string.IsNullOrEmpty(item.PluginPath))
                return Fail("plugin_path is required.");
            if (!File.Exists(item.PluginPath))
                return Fail($"Plugin not found: {item.PluginPath}");
            if (string.IsNullOrEmpty(item.FormId))
                return Fail("formid is required.");

            FormKey targetKey;
            try { targetKey = FormIdHelper.Parse(item.FormId); }
            catch (Exception ex) { return Fail($"Invalid formid '{item.FormId}': {ex.Message}"); }

            if (!modCache.TryGetValue(item.PluginPath, out var mod))
            {
                mod = SkyrimMod.CreateFromBinaryOverlay(item.PluginPath, SkyrimRelease.SkyrimSE);
                modCache[item.PluginPath] = mod;
            }

            IMajorRecordGetter? record = null;
            foreach (var r in mod.EnumerateMajorRecords())
            {
                if (r.FormKey == targetKey) { record = r; break; }
            }

            if (record == null)
                return Fail($"Record {item.FormId} not found in {Path.GetFileName(item.PluginPath)}");

            var fields = RenderValue(record, depth: 0, maxDepth: maxDepth) as Dictionary<string, object?>;
            return new ReadResponse
            {
                Success = true,
                FormId = FormIdHelper.Format(record.FormKey),
                RecordType = RecordTypeCode(record),
                EditorId = record.EditorID,
                Plugin = Path.GetFileName(item.PluginPath),
                Fields = fields,
            };
        }
        catch (Exception ex)
        {
            return new ReadResponse
            {
                Success = false,
                Error = $"Read failed for {item.FormId}: {ex.Message}",
                ErrorDetail = ex.ToString(),
            };
        }
    }

    public ReadResponse Read(ReadRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.PluginPath))
            {
                return Fail("plugin_path is required.");
            }
            if (!File.Exists(request.PluginPath))
            {
                return Fail($"Plugin not found: {request.PluginPath}");
            }
            if (string.IsNullOrEmpty(request.FormId))
            {
                return Fail("formid is required.");
            }

            FormKey targetKey;
            try
            {
                targetKey = FormIdHelper.Parse(request.FormId);
            }
            catch (Exception ex)
            {
                return Fail($"Invalid formid '{request.FormId}': {ex.Message}");
            }

            using var mod = SkyrimMod.CreateFromBinaryOverlay(request.PluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? record = null;
            foreach (var r in mod.EnumerateMajorRecords())
            {
                if (r.FormKey == targetKey)
                {
                    record = r;
                    break;
                }
            }

            if (record == null)
            {
                return Fail($"Record {request.FormId} not found in {Path.GetFileName(request.PluginPath)}");
            }

            var fields = RenderValue(record, depth: 0, maxDepth: request.MaxDepth) as Dictionary<string, object?>;

            return new ReadResponse
            {
                Success = true,
                FormId = FormIdHelper.Format(record.FormKey),
                RecordType = RecordTypeCode(record),
                EditorId = record.EditorID,
                Plugin = Path.GetFileName(request.PluginPath),
                Fields = fields,
            };
        }
        catch (Exception ex)
        {
            return new ReadResponse
            {
                Success = false,
                Error = $"RecordReader failed: {ex.Message}",
                ErrorDetail = ex.ToString(),
            };
        }
    }

    private static ReadResponse Fail(string msg) => new() { Success = false, Error = msg };

    /// <summary>
    /// Render any Mutagen value as JSON-friendly:
    /// - primitives/strings → passthrough
    /// - enums → string name
    /// - FormLinks → "Plugin:FormID"
    /// - ITranslatedStringGetter → .String
    /// - IEnumerable (except string) → List of rendered items
    /// - Mutagen-namespaced objects → recursive Dictionary
    /// - other → ToString()
    /// </summary>
    private static object? RenderValue(object? value, int depth, int maxDepth)
    {
        if (value == null) return null;
        if (depth > maxDepth) return "...[max depth reached]";

        var type = value.GetType();

        if (type.IsPrimitive || value is string || value is decimal)
            return value;

        if (type.IsEnum)
            return value.ToString();

        if (value is IFormLinkGetter link)
        {
            if (link.FormKeyNullable.HasValue && !link.FormKeyNullable.Value.IsNull)
                return FormIdHelper.Format(link.FormKeyNullable.Value);
            return null;
        }

        if (value is ITranslatedStringGetter ts)
            return ts.String;

        if (value is Noggog.MemorySlice<byte> bytes)
            return Convert.ToHexString(bytes.ToArray());

        if (value is byte[] byteArr)
            return Convert.ToHexString(byteArr);

        // Mutagen frequently exposes byte blobs as IReadOnlyList<byte> or
        // IEnumerable<byte> wrappers — render them as hex, not as integer arrays.
        if (value is IEnumerable<byte> byteEnumerable && !(value is string))
            return Convert.ToHexString(byteEnumerable.ToArray());

        // AssetLink / AssetPath — render as the path string, not a nested wrapper tree.
        if (IsAssetLike(type))
        {
            var pathProp = type.GetProperty("GivenPath") ?? type.GetProperty("RawPath") ?? type.GetProperty("Path");
            if (pathProp != null)
            {
                var pathVal = pathProp.GetValue(value);
                if (pathVal is string s) return s;
                if (pathVal != null) return pathVal.ToString();
            }
            return value.ToString();
        }

        // 2D/3D point types self-reference via a "Point" property. Flatten to {X,Y,Z}.
        if (IsPointLike(type))
        {
            var pt = new Dictionary<string, object?>();
            foreach (var axis in new[] { "X", "Y", "Z", "W" })
            {
                var p = type.GetProperty(axis);
                if (p == null) continue;
                var v = p.GetValue(value);
                if (v != null) pt[axis] = v;
            }
            return pt;
        }

        if (value is IEnumerable enumerable)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(RenderValue(item, depth + 1, maxDepth));
            }
            return items;
        }

        if (IsMutagenType(type))
        {
            var nested = new Dictionary<string, object?>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (SkipPropertyNames.Contains(prop.Name)) continue;
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                // Skip self-referential properties (e.g., P3Int16.Point returns the same struct).
                if (prop.PropertyType == type || prop.PropertyType.IsAssignableFrom(type)) continue;

                object? pval;
                try
                {
                    pval = prop.GetValue(value);
                }
                catch
                {
                    continue;
                }

                // Skip properties that return the same reference we're already walking.
                if (pval != null && ReferenceEquals(pval, value)) continue;

                var rendered = RenderValue(pval, depth + 1, maxDepth);
                if (rendered != null)
                {
                    nested[prop.Name] = rendered;
                }
            }
            return nested;
        }

        return value.ToString();
    }

    private static bool IsAssetLike(Type type)
    {
        var name = type.Name;
        return name.StartsWith("AssetLink", StringComparison.Ordinal)
            || name.StartsWith("AssetPath", StringComparison.Ordinal)
            || name == "ModelFile";
    }

    private static bool IsPointLike(Type type)
    {
        var name = type.Name;
        return name.StartsWith("P2", StringComparison.Ordinal)
            || name.StartsWith("P3", StringComparison.Ordinal)
            || name.StartsWith("P4", StringComparison.Ordinal);
    }

    private static bool IsMutagenType(Type type)
    {
        var ns = type.Namespace;
        if (ns == null) return false;
        return ns.StartsWith("Mutagen.Bethesda", StringComparison.Ordinal)
            || ns.StartsWith("Noggog", StringComparison.Ordinal);
    }

    private static string RecordTypeCode(IMajorRecordGetter record) => record switch
    {
        IArmorGetter => "ARMO",
        IWeaponGetter => "WEAP",
        INpcGetter => "NPC_",
        IIngestibleGetter => "ALCH",
        IAmmunitionGetter => "AMMO",
        IBookGetter => "BOOK",
        IFloraGetter => "FLOR",
        IIngredientGetter => "INGR",
        IMiscItemGetter => "MISC",
        IScrollGetter => "SCRL",
        ILeveledItemGetter => "LVLI",
        ILeveledNpcGetter => "LVLN",
        ILeveledSpellGetter => "LVSP",
        ISpellGetter => "SPEL",
        IPerkGetter => "PERK",
        IOutfitGetter => "OTFT",
        IFactionGetter => "FACT",
        IQuestGetter => "QUST",
        IKeywordGetter => "KYWD",
        IGlobalGetter => "GLOB",
        IEncounterZoneGetter => "ECZN",
        IFormListGetter => "FLST",
        IMagicEffectGetter => "MGEF",
        IContainerGetter => "CONT",
        IPackageGetter => "PACK",
        ICellGetter => "CELL",
        IWorldspaceGetter => "WRLD",
        IDialogTopicGetter => "DIAL",
        IDialogResponsesGetter => "INFO",
        IObjectEffectGetter => "ENCH",
        IRaceGetter => "RACE",
        IClassGetter => "CLAS",
        ILocationGetter => "LCTN",
        IShoutGetter => "SHOU",
        IWordOfPowerGetter => "WOOP",
        _ => record.Registration.ClassType.Name.ToUpperInvariant(),
    };
}
