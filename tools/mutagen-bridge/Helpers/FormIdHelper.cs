// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using Mutagen.Bethesda.Plugins;

namespace MutagenBridge;

/// <summary>
/// Converts between Claude MO2's "PluginName:LocalID" format
/// and Mutagen's FormKey objects.
///
/// Our format:   "Skyrim.esm:012E49"
/// Mutagen:      FormKey with ModKey("Skyrim", ModType.Plugin) + ID 0x012E49
/// </summary>
public static class FormIdHelper
{
    /// <summary>
    /// Parse "PluginName:LocalID" into a Mutagen FormKey.
    /// </summary>
    public static FormKey Parse(string formIdStr)
    {
        if (string.IsNullOrWhiteSpace(formIdStr))
            throw new ArgumentException("FormID string is empty");

        var colonIdx = formIdStr.LastIndexOf(':');
        if (colonIdx < 0)
            throw new ArgumentException(
                $"FormID must be 'PluginName:LocalID', got '{formIdStr}'");

        var pluginName = formIdStr[..colonIdx];
        var localHex = formIdStr[(colonIdx + 1)..];

        if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(localHex))
            throw new ArgumentException(
                $"FormID must be 'PluginName:LocalID', got '{formIdStr}'");

        var modKey = ModKey.FromFileName(pluginName);
        var localId = Convert.ToUInt32(localHex, 16);

        return new FormKey(modKey, localId);
    }

    /// <summary>
    /// Convert a Mutagen FormKey back to our "PluginName:LocalID" format.
    /// </summary>
    public static string Format(FormKey formKey)
    {
        if (formKey.IsNull)
            return "NULL";
        return $"{formKey.ModKey.FileName}:{formKey.ID:X6}";
    }

    /// <summary>
    /// Parse a FormID string into a FormLink of the specified type.
    /// </summary>
    public static Mutagen.Bethesda.Plugins.FormLink<T> ParseLink<T>(string formIdStr)
        where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter
    {
        var formKey = Parse(formIdStr);
        return new Mutagen.Bethesda.Plugins.FormLink<T>(formKey);
    }
}
