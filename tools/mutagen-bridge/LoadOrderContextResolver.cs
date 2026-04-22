// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace MutagenBridge;

/// <summary>
/// Turns a JSON <see cref="LoadOrderContext"/> into the artefacts
/// Mutagen's write-path needs for ESL-correct FormID compaction:
///
///   - An ordered array of <see cref="IModMasterStyledGetter"/>, one per
///     plugin in load order, each carrying the plugin's master-style
///     (standard / Light / Medium). Built from the plugin's disk header
///     via <see cref="KeyedMasterStyle.FromPath"/> — cheap (header-only
///     read), and tolerant of plugins the user has installed but not
///     enabled.
///
/// Passed to <c>patchMod.BeginWrite.WithLoadOrder(styledGetters)</c> so
/// Mutagen's MasterFlagsLookup resolves ESL-flagged masters correctly
/// and FormLinks pointing at them are written in their compacted form
/// (the v2.5.x write bug that motivated v2.6.0 — see
/// dev/plans/v2.6.0_mutagen_migration/PLAN.md).
///
/// Deliberately narrower than the "full GameEnvironment" the plan
/// originally sketched: Phase 0 established that <c>CreateFromBinaryOverlay</c>
/// in 0.53.1 already returns FormKey-correct results on read, so we
/// only need the master-style lookup for writes. A full LinkCache over
/// a 3000+ plugin load order is expensive and MO2's per-mod-folder
/// physical layout means there's no unified data folder Mutagen can
/// enumerate without symlinking. Keeping the resolver header-only also
/// sidesteps both problems.
/// </summary>
public static class LoadOrderContextResolver
{
    /// <summary>
    /// Build the master-style listing array for the given context.
    /// Skips any listing whose disk path is absent or whose header cannot
    /// be read (logged, non-fatal — a missing master surfaces later if
    /// the patch actually references it, but an orphan in loadorder.txt
    /// shouldn't block a patch whose records don't touch it).
    /// </summary>
    public static IModMasterStyledGetter[] BuildMasterStyledListings(LoadOrderContext ctx)
    {
        var release = ParseRelease(ctx.GameRelease);
        var result = new List<IModMasterStyledGetter>(ctx.Listings.Count);

        foreach (var entry in ctx.Listings)
        {
            if (string.IsNullOrEmpty(entry.ModKey) || string.IsNullOrEmpty(entry.Path))
                continue;
            if (!File.Exists(entry.Path))
                continue;

            ModKey modKey;
            try { modKey = ModKey.FromFileName(entry.ModKey); }
            catch { continue; }

            try
            {
                var modPath = new ModPath(modKey, entry.Path);
                var styled = KeyedMasterStyle.FromPath(modPath, release);
                result.Add(styled);
            }
            catch
            {
                // Header read failed. Skip — Mutagen's write-path
                // will raise a clear error downstream if a record
                // actually references a missing master.
            }
        }

        return result.ToArray();
    }

    private static GameRelease ParseRelease(string s)
    {
        if (Enum.TryParse<GameRelease>(s, ignoreCase: true, out var parsed))
            return parsed;
        // Default the bridge to Skyrim SE — every Python caller today
        // runs against a Skyrim SE modlist.
        return GameRelease.SkyrimSE;
    }
}
