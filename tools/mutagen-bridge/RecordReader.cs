// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Collections;
using System.Reflection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;

namespace MutagenBridge;

// ── v2.9.2 — Read-side efficiency mechanism (PLAN.md § A–F) ─────────
//
// Three composable axes added to the existing read path:
//
//   - `fields` projection — RenderValueProjected walks only the
//     requested dot-segmented paths; out-of-projection branches are
//     omitted. Auto-traverses lists and dicts mid-path per Q1 lock
//     ("Effects.BaseEffect" reads as "the BaseEffect of each Effect").
//   - `expand_links` expansion — ExpandFormLinkValue resolves a
//     FormLink at a named path against the modCache and inlines the
//     linked record's detail in wrapper form per Q2 lock
//     ({formid, EditorID, expanded}). Single-level only — links
//     inside expanded records render as plain FormID strings.
//   - Pre-flight validation — ValidateFieldsAndExpandLinks reflects
//     the record's getter type, walks each requested path against
//     the property tree, and accumulates bad-path / bad-expand /
//     non-FormLink-expand errors per type (§ D pseudocode). Invalid
//     paths surface in one error response; no reads happen on
//     validation failure (Q4 = pre-flight).
//
// FormLink predicate matches PatchEngine.cs:1182 IsFormLinkType plus
// the Mutagen list-of-FormLink shape (`IReadOnlyList<IFormLinkGetter<T>>`).
// Defaults preserve v2.9.1 behavior: when fields and expand_links are
// both null, RenderValueProjected falls through to RenderValue
// bit-identically (no perf overhead, no shape change).

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
    ///
    /// v2.9.2 — applies <c>fields</c> projection + <c>expand_links</c>
    /// expansion per item via the new <see cref="RenderValueProjected"/>
    /// walker. Validation runs pre-flight per Q4 lock (rejects strict-
    /// batch with multi-error accumulation if any path fails on any
    /// record type encountered during the batch).
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

        // v2.9.2 — explicit empty-list rejection per Layer 4.dsl.02 / .03.
        // Absence (null) is full payload / no-expansion; an empty list is
        // request-author-error.
        if (request.Fields != null && request.Fields.Count == 0)
        {
            response.Success = false;
            response.Error = "fields must contain at least one path — empty list rejected. Omit the parameter for full-payload mode.";
            return response;
        }
        if (request.ExpandLinks != null && request.ExpandLinks.Count == 0)
        {
            response.Success = false;
            response.Error = "expand_links must contain at least one path — empty list rejected. Omit the parameter for no-expansion mode.";
            return response;
        }

        var modCache = new Dictionary<string, ISkyrimModGetter>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // v2.9.2 — pre-flight validation per § D. Walk every batch
            // item, resolve each formid → record (if present), accumulate
            // unique record-getter-types encountered, validate the
            // fields + expand_links paths against each unique type. Any
            // bad path surfaces a strict-batch error envelope before any
            // projected reads happen (rollback contract).
            //
            // Performance: validation requires the records to be located
            // (so we know their getter types). The plugin loads happen
            // here anyway and stay cached in modCache for the per-record
            // read pass below — no extra disk I/O. Per-record-type
            // validation runs once per unique type even if N records of
            // the same type appear in the batch.
            if (request.Fields != null || request.ExpandLinks != null)
            {
                var typesByCode = new Dictionary<string, Type>();
                foreach (var item in request.Records)
                {
                    if (string.IsNullOrEmpty(item.PluginPath) || !File.Exists(item.PluginPath))
                        continue;
                    if (string.IsNullOrEmpty(item.FormId))
                        continue;
                    FormKey targetKey;
                    try { targetKey = FormIdHelper.Parse(item.FormId); }
                    catch { continue; }

                    if (!modCache.TryGetValue(item.PluginPath, out var mod))
                    {
                        try
                        {
                            mod = SkyrimMod.CreateFromBinaryOverlay(item.PluginPath, SkyrimRelease.SkyrimSE);
                            modCache[item.PluginPath] = mod;
                        }
                        catch { continue; }
                    }
                    foreach (var r in mod!.EnumerateMajorRecords())
                    {
                        if (r.FormKey == targetKey)
                        {
                            var code = RecordTypeCode(r);
                            if (!typesByCode.ContainsKey(code))
                                typesByCode[code] = GetGetterInterfaceType(r.GetType());
                            break;
                        }
                    }
                }

                Dictionary<string, ValidationErrorDetail>? validationErrors = null;
                foreach (var (code, type) in typesByCode)
                {
                    var detail = ValidateFieldsAndExpandLinks(type, request.Fields, request.ExpandLinks);
                    if (detail != null)
                    {
                        validationErrors ??= new Dictionary<string, ValidationErrorDetail>();
                        validationErrors[code] = detail;
                    }
                }

                if (validationErrors != null)
                {
                    response.Success = false;
                    response.Error = "Field path / expansion target validation failed.";
                    response.ValidationErrors = validationErrors;
                    return response;
                }
            }

            foreach (var item in request.Records)
            {
                response.Records.Add(ReadOne(item, request.MaxDepth, modCache, request.Fields, request.ExpandLinks));
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

    private ReadResponse ReadOne(
        ReadBatchItem item,
        int maxDepth,
        Dictionary<string, ISkyrimModGetter> modCache,
        List<string>? fields = null,
        List<string>? expandLinks = null)
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

            // v2.9.2 — projection + expansion via RenderValueProjected
            // when either parameter is supplied; falls through to
            // RenderValue bit-identically when both are null.
            object? walked;
            if (fields != null || expandLinks != null)
            {
                walked = RenderValueProjected(
                    record,
                    fields,
                    expandLinks,
                    currentPath: "",
                    depth: 0,
                    maxDepth: maxDepth,
                    modCache: modCache);
            }
            else
            {
                walked = RenderValue(record, depth: 0, maxDepth: maxDepth);
            }
            var renderedFields = walked as Dictionary<string, object?>;

            return new ReadResponse
            {
                Success = true,
                FormId = FormIdHelper.Format(record.FormKey),
                RecordType = RecordTypeCode(record),
                EditorId = record.EditorID,
                Plugin = Path.GetFileName(item.PluginPath),
                Fields = renderedFields,
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

            // v2.9.2 — explicit empty-list rejection per Layer 4.dsl.02 / .03.
            if (request.Fields != null && request.Fields.Count == 0)
            {
                return Fail("fields must contain at least one path — empty list rejected. Omit the parameter for full-payload mode.");
            }
            if (request.ExpandLinks != null && request.ExpandLinks.Count == 0)
            {
                return Fail("expand_links must contain at least one path — empty list rejected. Omit the parameter for no-expansion mode.");
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

            var mod = SkyrimMod.CreateFromBinaryOverlay(request.PluginPath, SkyrimRelease.SkyrimSE);
            var modCache = new Dictionary<string, ISkyrimModGetter>(StringComparer.OrdinalIgnoreCase)
            {
                [request.PluginPath] = mod,
            };
            try
            {
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

                // v2.9.2 — pre-flight validation per § D when either
                // fields or expand_links is supplied. Validation runs
                // BEFORE the walk; multi-error accumulation surfaces
                // every bad path in one round-trip.
                if (request.Fields != null || request.ExpandLinks != null)
                {
                    var getterType = GetGetterInterfaceType(record.GetType());
                    var detail = ValidateFieldsAndExpandLinks(getterType, request.Fields, request.ExpandLinks);
                    if (detail != null)
                    {
                        return new ReadResponse
                        {
                            Success = false,
                            Error = "Field path / expansion target validation failed.",
                            ValidationErrors = new Dictionary<string, ValidationErrorDetail>
                            {
                                [RecordTypeCode(record)] = detail,
                            },
                        };
                    }
                }

                object? walked;
                if (request.Fields != null || request.ExpandLinks != null)
                {
                    walked = RenderValueProjected(
                        record,
                        request.Fields,
                        request.ExpandLinks,
                        currentPath: "",
                        depth: 0,
                        maxDepth: request.MaxDepth,
                        modCache: modCache);
                }
                else
                {
                    walked = RenderValue(record, depth: 0, maxDepth: request.MaxDepth);
                }
                var fields = walked as Dictionary<string, object?>;

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
            finally
            {
                if (mod is IDisposable d) d.Dispose();
            }
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

    // ── v2.9.2 — Read-side efficiency helpers ───────────────────────

    /// <summary>
    /// v2.9.2 — resolve the canonical getter interface type for a
    /// concrete record type. Mutagen's concrete classes (e.g.
    /// <c>Race</c>) implement multiple interfaces (<c>IRace</c>,
    /// <c>IRaceGetter</c>, <c>IMajorRecordGetter</c>,
    /// <c>ISkyrimMajorRecordGetter</c>); validation + projection walk
    /// against the **most-derived** Skyrim Getter interface
    /// (<c>IRaceGetter</c>), not the concrete or the writable variant.
    ///
    /// Selection: among all Skyrim-namespaced interfaces ending in
    /// "Getter" that descend from <c>IMajorRecordGetter</c> (and are
    /// not <c>IMajorRecordGetter</c> / <c>ISkyrimMajorRecordGetter</c>
    /// itself), pick the leaf — the one no other candidate is
    /// assignable from. This yields <c>IRaceGetter</c> for Race,
    /// <c>INpcGetter</c> for NPC, etc., regardless of declaration
    /// order in <c>GetInterfaces()</c>. Falls back to the concrete
    /// type if no candidate is found.
    /// </summary>
    private static Type GetGetterInterfaceType(Type concreteType)
    {
        var candidates = new List<Type>();
        foreach (var iface in concreteType.GetInterfaces())
        {
            if (iface.Namespace == null) continue;
            if (!iface.Namespace.StartsWith("Mutagen.Bethesda.Skyrim", StringComparison.Ordinal)) continue;
            if (!iface.Name.EndsWith("Getter", StringComparison.Ordinal)) continue;
            if (!typeof(IMajorRecordGetter).IsAssignableFrom(iface)) continue;
            if (iface == typeof(IMajorRecordGetter)) continue;
            // Exclude the umbrella ISkyrimMajorRecordGetter — it's the
            // common base for every Skyrim record and exposes only
            // SkyrimMajorRecordFlags. Keep the most-derived per-record-
            // type interface.
            if (string.Equals(iface.Name, "ISkyrimMajorRecordGetter", StringComparison.Ordinal)) continue;
            candidates.Add(iface);
        }
        if (candidates.Count == 0) return concreteType;
        // Pick the leaf: the candidate that no other candidate is
        // assignable from. This handles inheritance chains like
        // INpcGetter → INpcSpawnGetter → IActorBaseGetter etc.
        foreach (var c in candidates)
        {
            bool isLeaf = true;
            foreach (var other in candidates)
            {
                if (other == c) continue;
                if (c.IsAssignableFrom(other))
                {
                    isLeaf = false;
                    break;
                }
            }
            if (isLeaf) return c;
        }
        return candidates[0];
    }

    /// <summary>
    /// v2.9.2 — FormLink predicate. Mirrors PatchEngine.cs:1182
    /// IsFormLinkType. Returns true for <c>IFormLinkGetter&lt;T&gt;</c>,
    /// <c>IFormLink&lt;T&gt;</c>, <c>IFormLinkNullable&lt;T&gt;</c>,
    /// <c>FormLink&lt;T&gt;</c>, <c>FormLinkNullable&lt;T&gt;</c>.
    /// </summary>
    private static bool IsFormLinkType(Type t)
    {
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(IFormLinkGetter<>)
            || def == typeof(IFormLink<>)
            || def == typeof(IFormLinkNullable<>)
            || def == typeof(FormLink<>)
            || def == typeof(FormLinkNullable<>);
    }

    /// <summary>
    /// v2.9.2 — list-of-FormLink predicate. Returns true for
    /// <c>IReadOnlyList&lt;IFormLinkGetter&lt;T&gt;&gt;</c> and
    /// related list-shapes carrying FormLink elements. Used by
    /// <see cref="IsFormLinkOrListOfFormLink"/> to recognise both
    /// scalar and list-shaped FormLink-typed properties (per Phase 1's
    /// 148-property record-shape sweep — RACE.ActorEffect is list-
    /// shaped; NPC_.Class is scalar; both qualify as expansion
    /// targets).
    /// </summary>
    private static bool IsListOfFormLinkType(Type t)
    {
        if (!t.IsGenericType) return false;
        // Walk implemented interfaces for IEnumerable<T> with FormLink T.
        var checkTypes = new List<Type> { t };
        checkTypes.AddRange(t.GetInterfaces());
        foreach (var ct in checkTypes)
        {
            if (!ct.IsGenericType) continue;
            var def = ct.GetGenericTypeDefinition();
            if (def == typeof(IEnumerable<>) || def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>) || def == typeof(IList<>) || def == typeof(List<>))
            {
                var elemType = ct.GetGenericArguments()[0];
                if (IsFormLinkType(elemType)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// v2.9.2 — combined predicate matching both scalar and list-of
    /// FormLink shapes. A property typed
    /// <c>IFormLinkGetter&lt;T&gt;</c> or
    /// <c>IReadOnlyList&lt;IFormLinkGetter&lt;T&gt;&gt;</c> qualifies
    /// as an expansion target.
    /// </summary>
    private static bool IsFormLinkOrListOfFormLink(Type t)
        => IsFormLinkType(t) || IsListOfFormLinkType(t);

    /// <summary>
    /// v2.9.2 — pre-flight validate <c>fields</c> + <c>expand_links</c>
    /// paths against the record's getter type. Returns null when every
    /// path is valid; a populated <see cref="ValidationErrorDetail"/>
    /// when one or more paths fail. Multi-error accumulation per § D
    /// pseudocode — every bad entry surfaces in one round-trip.
    ///
    /// Three failure categories:
    ///   1. Bad field path (path's first segment doesn't resolve to a
    ///      property on the type).
    ///   2. Bad expansion target (same shape as #1, tracked separately
    ///      because the caller's intent — expand a link — is distinct).
    ///   3. Non-FormLink expansion target (path resolves but the
    ///      property is not FormLink- or list-of-FormLink-typed).
    ///
    /// Path resolution: only the first segment is validated (whether
    /// the type exposes that property name). Auto-traversal handles
    /// downstream segments at walk time per Q1 lock — pre-flight
    /// can't statically validate "the BaseEffect of each Effect"
    /// without enumerating list-element types, which is more
    /// reflection than the value-prop justifies. The first-segment
    /// check is what catches the 99% case (typo'd field name).
    /// </summary>
    private static ValidationErrorDetail? ValidateFieldsAndExpandLinks(
        Type getterType,
        List<string>? fields,
        List<string>? expandLinks)
    {
        var detail = new ValidationErrorDetail();

        // Build the getter type's top-level property name set + the
        // FormLink-typed-property name subset (for valid-name lists in
        // the error response). For an interface type, GetProperties()
        // returns only directly-declared properties — inherited members
        // (e.g. IMajorRecordGetter's EditorID, FormKey) are NOT
        // included. Walk the interface inheritance chain to collect
        // them all.
        var validFieldNames = new List<string>();
        var validFormLinkNames = new List<string>();
        var propsByName = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        var typesToVisit = new List<Type> { getterType };
        if (getterType.IsInterface)
        {
            typesToVisit.AddRange(getterType.GetInterfaces());
        }
        foreach (var t in typesToVisit)
        {
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (SkipPropertyNames.Contains(prop.Name)) continue;
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                if (propsByName.ContainsKey(prop.Name)) continue;
                propsByName[prop.Name] = prop;
                validFieldNames.Add(prop.Name);
                if (IsFormLinkOrListOfFormLink(prop.PropertyType))
                {
                    validFormLinkNames.Add(prop.Name);
                }
            }
        }
        detail.ValidFieldNames = validFieldNames;
        detail.ValidFormLinkFieldNames = validFormLinkNames;

        if (fields != null)
        {
            foreach (var path in fields)
            {
                if (string.IsNullOrEmpty(path)) { detail.BadFieldPaths.Add(path ?? ""); continue; }
                var first = FirstSegment(path);
                if (!propsByName.ContainsKey(first))
                {
                    detail.BadFieldPaths.Add(path);
                }
            }
        }

        if (expandLinks != null)
        {
            foreach (var path in expandLinks)
            {
                if (string.IsNullOrEmpty(path)) { detail.BadExpansionTargets.Add(path ?? ""); continue; }
                var first = FirstSegment(path);
                if (!propsByName.TryGetValue(first, out var prop))
                {
                    detail.BadExpansionTargets.Add(path);
                    continue;
                }
                // For multi-segment expand paths, the leaf property is
                // what must be FormLink-typed. Without full path
                // resolution at pre-flight, we accept the conservative
                // contract: the first segment must be FormLink- or
                // list-of-FormLink-typed (this is the 99% case for
                // single-segment paths like "ActorEffect" / "Class").
                // Multi-segment expand paths (e.g. "Factions.Faction")
                // are validated at walk time when the walker descends
                // into the list and encounters the named sub-property.
                if (path.IndexOf('.') < 0 && !IsFormLinkOrListOfFormLink(prop.PropertyType))
                {
                    detail.NonFormLinkExpansionTargets.Add(path);
                }
            }
        }

        if (detail.BadFieldPaths.Count + detail.BadExpansionTargets.Count + detail.NonFormLinkExpansionTargets.Count > 0)
        {
            return detail;
        }
        return null;
    }

    private static string FirstSegment(string path)
    {
        var idx = path.IndexOf('.');
        return idx < 0 ? path : path.Substring(0, idx);
    }

    /// <summary>
    /// v2.9.2 — projection walker. Walks <paramref name="value"/> emitting
    /// only the requested <paramref name="fields"/> paths; out-of-projection
    /// branches are omitted. When a property's path appears in
    /// <paramref name="expandLinks"/> AND the property is FormLink-typed,
    /// swaps to <see cref="ExpandFormLinkValue"/> for single-level
    /// inlining. Auto-traverses lists and dicts mid-path per Q1 lock.
    ///
    /// When both <paramref name="fields"/> and <paramref name="expandLinks"/>
    /// are null, behaves bit-identically to <see cref="RenderValue"/> —
    /// the existing v2.9.1 walker — by short-circuiting all projection
    /// logic. Defaults preserved.
    /// </summary>
    /// <param name="currentPath">
    /// Dot-segmented path from the record root to the current value's
    /// position in the tree. Empty string = at the record itself.
    /// </param>
    private static object? RenderValueProjected(
        object? value,
        List<string>? fields,
        List<string>? expandLinks,
        string currentPath,
        int depth,
        int maxDepth,
        Dictionary<string, ISkyrimModGetter> modCache)
    {
        // Bit-identical fall-through when no projection or expansion
        // requested. Preserves v2.9.1 behavior end-to-end.
        bool hasProjection = fields != null && fields.Count > 0;
        bool hasExpansion = expandLinks != null && expandLinks.Count > 0;
        if (!hasProjection && !hasExpansion)
        {
            return RenderValue(value, depth, maxDepth);
        }

        if (value == null) return null;
        if (depth > maxDepth) return "...[max depth reached]";

        var type = value.GetType();

        // Primitives, strings, enums, FormLinks (when not at an
        // expansion path) — terminal. Same shape as RenderValue.
        if (type.IsPrimitive || value is string || value is decimal)
            return value;
        if (type.IsEnum)
            return value.ToString();

        if (value is IFormLinkGetter link)
        {
            if (hasExpansion && PathMatchesAny(currentPath, expandLinks!))
            {
                return ExpandFormLinkValue(link, fields, expandLinks, currentPath, depth, maxDepth, modCache);
            }
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
        if (value is IEnumerable<byte> byteEnumerable && !(value is string))
            return Convert.ToHexString(byteEnumerable.ToArray());

        if (IsAssetLike(type))
        {
            // Asset paths render the same regardless of projection.
            return RenderValue(value, depth, maxDepth);
        }
        if (IsPointLike(type))
        {
            return RenderValue(value, depth, maxDepth);
        }

        // List / collection — auto-traverse per Q1. Each element
        // inherits the parent's currentPath; nested projection
        // applies inside each element. Matches v2.9.1 RenderValue's
        // behavior: dicts/lists/gendered items all flow through
        // IEnumerable (Mutagen exposes them all as IEnumerable; dicts
        // iterate as KeyValuePair entries, gendered items iterate as
        // their Male/Female pair).
        if (value is IEnumerable enumerable)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(RenderValueProjected(item, fields, expandLinks, currentPath, depth + 1, maxDepth, modCache));
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
                if (prop.PropertyType == type || prop.PropertyType.IsAssignableFrom(type)) continue;

                var childPath = string.IsNullOrEmpty(currentPath)
                    ? prop.Name
                    : currentPath + "." + prop.Name;

                // Projection gate: at the record root, drop properties
                // whose name doesn't appear as the first segment of any
                // projected path. Mid-walk, drop child properties whose
                // path doesn't appear as a prefix-or-equal of any
                // projected path. Expansion paths are also accepted as
                // gates so the walker descends to them even if they
                // aren't in `fields`.
                if (hasProjection || hasExpansion)
                {
                    bool keep = false;
                    if (hasProjection && PathIsCoveredByAny(childPath, fields!))
                        keep = true;
                    if (!keep && hasExpansion && PathIsCoveredByAny(childPath, expandLinks!))
                        keep = true;
                    if (!keep) continue;
                }

                object? pval;
                try { pval = prop.GetValue(value); }
                catch { continue; }

                if (pval != null && ReferenceEquals(pval, value)) continue;

                // Expansion check: if the leaf path matches an
                // expand_links entry AND the property is FormLink-
                // (or list-of-FormLink-) typed, route through the
                // expansion shape instead of the default walk.
                if (hasExpansion && PathMatchesAny(childPath, expandLinks!))
                {
                    if (pval is IFormLinkGetter scalarLink)
                    {
                        nested[prop.Name] = ExpandFormLinkValue(scalarLink, fields, expandLinks, childPath, depth + 1, maxDepth, modCache);
                        continue;
                    }
                    if (pval is IEnumerable listLink && !(pval is string))
                    {
                        var expanded = new List<object?>();
                        foreach (var elem in listLink)
                        {
                            if (elem is IFormLinkGetter el)
                            {
                                expanded.Add(ExpandFormLinkValue(el, fields, expandLinks, childPath, depth + 2, maxDepth, modCache));
                            }
                            else
                            {
                                // Element isn't itself a FormLink (e.g. list of
                                // structs whose Faction sub-property IS a
                                // FormLink — Scenario 3.2 pattern). Recurse
                                // into the element with the same expand_links
                                // so nested FormLink sub-properties get
                                // expanded at the named sub-path.
                                expanded.Add(RenderValueProjected(elem, fields, expandLinks, childPath, depth + 2, maxDepth, modCache));
                            }
                        }
                        nested[prop.Name] = expanded;
                        continue;
                    }
                }

                var rendered = RenderValueProjected(pval, fields, expandLinks, childPath, depth + 1, maxDepth, modCache);
                if (rendered != null)
                {
                    nested[prop.Name] = rendered;
                }
            }
            return nested;
        }

        return value.ToString();
    }

    /// <summary>
    /// v2.9.2 — does <paramref name="currentPath"/> exactly equal any
    /// path in <paramref name="paths"/>? Used to detect "the walker
    /// is now at a projected/expanded leaf."
    /// </summary>
    private static bool PathMatchesAny(string currentPath, List<string> paths)
    {
        foreach (var p in paths)
        {
            if (string.Equals(p, currentPath, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// v2.9.2 — is <paramref name="currentPath"/> covered by any path
    /// in <paramref name="paths"/>? Covered means: (a) currentPath
    /// equals a path; (b) currentPath is a prefix of a path (so the
    /// walker descends to reach it); (c) a path is a prefix of
    /// currentPath (so the walker is INSIDE a projected branch and
    /// keeps walking everything below). All three cases keep the
    /// walker descending. Used to gate property emission per § B.
    /// </summary>
    private static bool PathIsCoveredByAny(string currentPath, List<string> paths)
    {
        foreach (var p in paths)
        {
            if (string.Equals(p, currentPath, StringComparison.Ordinal)) return true;
            // currentPath is ancestor of p — keep walking to reach p
            if (p.StartsWith(currentPath + ".", StringComparison.Ordinal)) return true;
            // currentPath is descendant of p — keep walking everything inside p
            if (currentPath.StartsWith(p + ".", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// v2.9.2 — single-level FormLink expansion. Resolves
    /// <paramref name="link"/> against the modCache, picks the latest-
    /// override winning record (caller's modCache iteration order = the
    /// load-order in the request), and returns the wrapper form
    /// <c>{ formid, EditorID, expanded }</c> per Q2 lock. Single-level
    /// only — the inner walk uses <see cref="RenderValue"/>, NOT
    /// RenderValueProjected, so FormLinks inside the expanded record
    /// render as plain FormID strings (no recursion, no cycle hazard).
    ///
    /// Missing-master / unresolvable: returns
    /// <c>{ formid, EditorID: null, expanded: null, error: "..." }</c>
    /// — uniform shape per Q2 rationale point 4 (symmetric with null-
    /// link rendering).
    /// </summary>
    private static object? ExpandFormLinkValue(
        IFormLinkGetter link,
        List<string>? fields,
        List<string>? expandLinks,
        string currentPath,
        int depth,
        int maxDepth,
        Dictionary<string, ISkyrimModGetter> modCache)
    {
        // Null-link case: render uniform-shape wrapper with formid=null.
        if (!link.FormKeyNullable.HasValue || link.FormKeyNullable.Value.IsNull)
        {
            return new Dictionary<string, object?>
            {
                ["formid"] = null,
                ["EditorID"] = null,
                ["expanded"] = null,
            };
        }

        var formKey = link.FormKeyNullable.Value;
        var formIdStr = FormIdHelper.Format(formKey);

        // Search modCache for the linked record. The cache is the set of
        // plugins loaded for the current bridge invocation; cross-master
        // resolution requires those masters to be loaded in the cache.
        // Single-plugin reads typically only carry one cache entry —
        // resolution will fail for cross-master FormLinks unless the
        // wrapper happens to be using the multi-plugin batch path.
        IMajorRecordGetter? linkedRecord = null;
        foreach (var mod in modCache.Values)
        {
            // Match if the FormKey's master mod-key matches the loaded
            // mod's mod-key (i.e. the linked record originates from
            // this mod). Mutagen's FormKey carries the originating
            // master in its ModKey property.
            if (!string.Equals(mod.ModKey.FileName.String, formKey.ModKey.FileName.String, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var r in mod.EnumerateMajorRecords())
            {
                if (r.FormKey == formKey) { linkedRecord = r; break; }
            }
            if (linkedRecord != null) break;
        }

        if (linkedRecord == null)
        {
            return new Dictionary<string, object?>
            {
                ["formid"] = formIdStr,
                ["EditorID"] = null,
                ["expanded"] = null,
                ["error"] = "FormID target not in load order",
            };
        }

        // Single-level walk — RenderValue (not RenderValueProjected) so
        // interior FormLinks render as plain FormID strings.
        var inner = RenderValue(linkedRecord, depth + 1, maxDepth) as Dictionary<string, object?>;

        return new Dictionary<string, object?>
        {
            ["formid"] = formIdStr,
            ["EditorID"] = linkedRecord.EditorID,
            ["expanded"] = inner,
        };
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
