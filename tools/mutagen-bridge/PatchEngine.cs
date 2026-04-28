// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Reflection;
using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
// Disambiguate Mutagen.Bethesda.Skyrim.Activator from System.Activator under implicit usings.
using SkActivator = Mutagen.Bethesda.Skyrim.Activator;

namespace MutagenBridge;

/// <summary>
/// Core patching engine. Creates an ESP patch mod and applies
/// record operations using Mutagen's typed API.
/// </summary>
public class PatchEngine
{
    public PatchResponse Process(PatchRequest request)
    {
        // v2.6.0 Phase 2: the write path is now load-order-aware so
        // FormLinks to ESL/Light-flagged masters compact correctly in
        // the output ESP. The Python caller must supply load_order —
        // without it, Mutagen's MasterFlagsLookup is empty and we'd
        // reproduce the v2.5.x bug where xEdit cannot resolve the
        // patch's own FormLinks.
        if (request.LoadOrder == null)
        {
            return new PatchResponse
            {
                Success = false,
                Error =
                    "load_order context is required. The Python caller " +
                    "(mo2_mcp.tools_patching) must populate it from MO2's " +
                    "profile. Calls from pre-v2.6 clients will hit this path.",
            };
        }

        var masterStyled = LoadOrderContextResolver.BuildMasterStyledListings(request.LoadOrder);

        var outputModKey = ModKey.FromFileName(Path.GetFileName(request.OutputPath));
        var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

        if (!string.IsNullOrEmpty(request.Author))
            patchMod.ModHeader.Author = request.Author;

        if (request.EslFlag)
            patchMod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;

        var details = new List<RecordDetail>();

        foreach (var record in request.Records)
        {
            try
            {
                var detail = record.Op.ToLowerInvariant() switch
                {
                    "override" => ProcessOverride(patchMod, record),
                    "merge_leveled_list" => ProcessMergeLeveledList(patchMod, record),
                    _ => throw new ArgumentException($"Unknown operation: '{record.Op}'")
                };
                details.Add(detail);
            }
            catch (Exception ex)
            {
                details.Add(new RecordDetail
                {
                    FormId = record.FormId,
                    Op = record.Op,
                    Error = ex.Message,
                });
            }
        }

        var successfulCount = details.Count(d => d.Error == null);
        var failedCount = details.Count - successfulCount;

        var recordCount = patchMod.EnumerateMajorRecords().Count();

        if (recordCount == 0)
        {
            return new PatchResponse
            {
                Success = false,
                Error = "No records were successfully added to the patch.",
                Details = details,
                SuccessfulCount = successfulCount,
                FailedCount = failedCount,
            };
        }

        var outputDir = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // BeginWrite.WithLoadOrder: Mutagen consults the styled
        // listings' MasterStyle during the write to decide whether
        // each referenced master is ESL / Medium / Full, and encodes
        // FormLinks pointing at them correspondingly. Mutagen also
        // recomputes the patch's MasterReferences from the actual
        // referenced FormKeys at write time — AddMasterIfMissing is
        // no longer needed.
        //
        // Phase 2 verification (2026-04-22) showed WithLoadOrder is
        // *defensive* rather than load-bearing for the specific bug
        // that motivated v2.6.0: once PluginResolver reads the right
        // plugin variant, Mutagen's in-memory FormKey is already
        // ESL-compacted and WithNoLoadOrder also produces a correct
        // patch. WithLoadOrder stays in shipped code — it remains
        // correct for MasterFlagsLookup-dependent encoding edge cases
        // (medium masters, non-compacted-on-read variants) and is
        // the forward-compatible call shape Mutagen's write API
        // wants. See PHASE_2_HANDOFF.md for the ablation details.
        patchMod.BeginWrite
            .ToPath(request.OutputPath)
            .WithLoadOrder(masterStyled)
            .Write();

        // Read the masters list back from the written file. BeginWrite
        // recomputes masters into its write pipeline but does not
        // mutate the in-memory patchMod's ModHeader, so reading from
        // patchMod.ModHeader.MasterReferences here returns empty even
        // when the file on disk has the correct masters. Mirror v2.5.x
        // behaviour and open the written file.
        using var written = SkyrimMod.CreateFromBinaryOverlay(
            request.OutputPath, SkyrimRelease.SkyrimSE);
        var masters = written.ModHeader.MasterReferences
            .Select(m => m.Master.FileName.String)
            .ToList();

        return new PatchResponse
        {
            // True only when every record operation succeeded. ProcessOverride
            // rolls back partial overrides on ApplyModifications failure, so
            // the output ESP also reflects only the successful records.
            Success = failedCount == 0,
            OutputPath = request.OutputPath,
            RecordsWritten = recordCount,
            EslFlagged = request.EslFlag,
            Masters = masters,
            Details = details,
            SuccessfulCount = successfulCount,
            FailedCount = failedCount,
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Override Operation
    // ═══════════════════════════════════════════════════════════════

    private RecordDetail ProcessOverride(SkyrimMod patchMod, RecordOperation op)
    {
        if (string.IsNullOrEmpty(op.SourcePath))
            throw new ArgumentException("'source_path' required for override operation");

        if (!File.Exists(op.SourcePath))
            throw new FileNotFoundException($"Source plugin not found: {op.SourcePath}");

        var targetFormKey = FormIdHelper.Parse(op.FormId);

        using var sourceMod = SkyrimMod.CreateFromBinaryOverlay(
            op.SourcePath, SkyrimRelease.SkyrimSE);

        // v2.6.0 Phase 2: BeginWrite.WithLoadOrder recomputes
        // MasterReferences from the actual referenced FormKeys at
        // write time. No pre-seeding required.

        var sourceRecord = FindRecord(sourceMod, targetFormKey);
        if (sourceRecord == null)
            throw new KeyNotFoundException(
                $"Record {op.FormId} not found in {Path.GetFileName(op.SourcePath)}");

        var overrideRecord = CopyAsOverride(patchMod, sourceMod, sourceRecord);
        if (overrideRecord == null)
            throw new InvalidOperationException(
                $"Could not create override for {RecordTypeCode(sourceRecord)}");

        var detail = new RecordDetail
        {
            FormId = op.FormId,
            RecordType = RecordTypeCode(sourceRecord),
            Op = "override",
            Source = Path.GetFileName(op.SourcePath),
            Modifications = new Dictionary<string, object>(),
        };

        try
        {
            ApplyModifications(overrideRecord, op, detail);
        }
        catch (UnsupportedOperatorException ex)
        {
            // Tier D (v2.7.1): one or more requested operators have no
            // handler for this record type. Roll back the override so
            // the output ESP doesn't ship a no-op record, populate the
            // structured fields, and return without re-throwing — the
            // outer Process loop counts this as a failed record but
            // continues processing the remaining records in the patch.
            TryRemoveOverride(patchMod, overrideRecord);
            detail.Modifications = null;
            detail.UnmatchedOperators = ex.UnmatchedOperators;
            detail.Error = ex.Message;
            return detail;
        }
        catch
        {
            // Roll back the no-op override so the output ESP and the
            // success-reporting both reflect what was actually applied.
            TryRemoveOverride(patchMod, overrideRecord);
            throw;
        }

        return detail;
    }

    // ═══════════════════════════════════════════════════════════════
    // Merge Leveled List (LVLI / LVLN / LVSP)
    // ═══════════════════════════════════════════════════════════════

    private RecordDetail ProcessMergeLeveledList(SkyrimMod patchMod, RecordOperation op)
    {
        if (string.IsNullOrEmpty(op.BasePath))
            throw new ArgumentException("'base_path' required for merge_leveled_list");
        if (op.OverridePaths == null || op.OverridePaths.Count == 0)
            throw new ArgumentException("'override_paths' required for merge_leveled_list");

        var targetFormKey = FormIdHelper.Parse(op.FormId);

        using var baseMod = SkyrimMod.CreateFromBinaryOverlay(
            op.BasePath, SkyrimRelease.SkyrimSE);

        // v2.6.0 Phase 2: masters recomputed at write time (see Process).

        // Try LVLI, then LVLN, then LVSP
        var lvli = baseMod.LeveledItems.FirstOrDefault(r => r.FormKey == targetFormKey);
        if (lvli != null)
            return MergeLeveledItems(patchMod, lvli, op, targetFormKey);

        var lvln = baseMod.LeveledNpcs.FirstOrDefault(r => r.FormKey == targetFormKey);
        if (lvln != null)
            return MergeLeveledNpcs(patchMod, lvln, op, targetFormKey);

        var lvsp = baseMod.LeveledSpells.FirstOrDefault(r => r.FormKey == targetFormKey);
        if (lvsp != null)
            return MergeLeveledSpells(patchMod, lvsp, op, targetFormKey);

        throw new KeyNotFoundException(
            $"Leveled list {op.FormId} not found in {Path.GetFileName(op.BasePath)}");
    }

    private RecordDetail MergeLeveledItems(
        SkyrimMod patchMod, ILeveledItemGetter baseRecord,
        RecordOperation op, FormKey targetFormKey)
    {
        var baseEntries = new HashSet<FormKey>();
        if (baseRecord.Entries != null)
            foreach (var e in baseRecord.Entries)
                if (e.Data != null) baseEntries.Add(e.Data.Reference.FormKey);

        var patchList = patchMod.LeveledItems.GetOrAddAsOverride(baseRecord);
        int totalMerged = 0;
        var sourceNames = new List<string>();

        foreach (var overridePath in op.OverridePaths!)
        {
            if (!File.Exists(overridePath)) continue;
            using var overrideMod = SkyrimMod.CreateFromBinaryOverlay(overridePath, SkyrimRelease.SkyrimSE);

            var overrideRecord = overrideMod.LeveledItems.FirstOrDefault(r => r.FormKey == targetFormKey);
            if (overrideRecord?.Entries == null) continue;
            sourceNames.Add(Path.GetFileName(overridePath));

            foreach (var entry in overrideRecord.Entries)
            {
                if (entry.Data == null) continue;
                if (!baseEntries.Contains(entry.Data.Reference.FormKey))
                {
                    patchList.Entries ??= new ExtendedList<LeveledItemEntry>();
                    patchList.Entries.Add(new LeveledItemEntry
                    {
                        Data = new LeveledItemEntryData
                        {
                            Level = entry.Data.Level,
                            Count = entry.Data.Count,
                            Reference = entry.Data.Reference.FormKey.ToLink<IItemGetter>(),
                        }
                    });
                    baseEntries.Add(entry.Data.Reference.FormKey);
                    totalMerged++;
                }
            }
        }

        return new RecordDetail
        {
            FormId = op.FormId, RecordType = "LVLI", Op = "merge_leveled_list",
            EntriesMerged = totalMerged, Sources = sourceNames,
        };
    }

    private RecordDetail MergeLeveledNpcs(
        SkyrimMod patchMod, ILeveledNpcGetter baseRecord,
        RecordOperation op, FormKey targetFormKey)
    {
        var baseEntries = new HashSet<FormKey>();
        if (baseRecord.Entries != null)
            foreach (var e in baseRecord.Entries)
                if (e.Data != null) baseEntries.Add(e.Data.Reference.FormKey);

        var patchList = patchMod.LeveledNpcs.GetOrAddAsOverride(baseRecord);
        int totalMerged = 0;
        var sourceNames = new List<string>();

        foreach (var overridePath in op.OverridePaths!)
        {
            if (!File.Exists(overridePath)) continue;
            using var overrideMod = SkyrimMod.CreateFromBinaryOverlay(overridePath, SkyrimRelease.SkyrimSE);

            var overrideRecord = overrideMod.LeveledNpcs.FirstOrDefault(r => r.FormKey == targetFormKey);
            if (overrideRecord?.Entries == null) continue;
            sourceNames.Add(Path.GetFileName(overridePath));

            foreach (var entry in overrideRecord.Entries)
            {
                if (entry.Data == null) continue;
                if (!baseEntries.Contains(entry.Data.Reference.FormKey))
                {
                    patchList.Entries ??= new ExtendedList<LeveledNpcEntry>();
                    patchList.Entries.Add(new LeveledNpcEntry
                    {
                        Data = new LeveledNpcEntryData
                        {
                            Level = entry.Data.Level,
                            Count = entry.Data.Count,
                            Reference = entry.Data.Reference.FormKey.ToLink<INpcSpawnGetter>(),
                        }
                    });
                    baseEntries.Add(entry.Data.Reference.FormKey);
                    totalMerged++;
                }
            }
        }

        return new RecordDetail
        {
            FormId = op.FormId, RecordType = "LVLN", Op = "merge_leveled_list",
            EntriesMerged = totalMerged, Sources = sourceNames,
        };
    }

    private RecordDetail MergeLeveledSpells(
        SkyrimMod patchMod, ILeveledSpellGetter baseRecord,
        RecordOperation op, FormKey targetFormKey)
    {
        var baseEntries = new HashSet<FormKey>();
        if (baseRecord.Entries != null)
            foreach (var e in baseRecord.Entries)
                if (e.Data != null) baseEntries.Add(e.Data.Reference.FormKey);

        var patchList = patchMod.LeveledSpells.GetOrAddAsOverride(baseRecord);
        int totalMerged = 0;
        var sourceNames = new List<string>();

        foreach (var overridePath in op.OverridePaths!)
        {
            if (!File.Exists(overridePath)) continue;
            using var overrideMod = SkyrimMod.CreateFromBinaryOverlay(overridePath, SkyrimRelease.SkyrimSE);

            var overrideRecord = overrideMod.LeveledSpells.FirstOrDefault(r => r.FormKey == targetFormKey);
            if (overrideRecord?.Entries == null) continue;
            sourceNames.Add(Path.GetFileName(overridePath));

            foreach (var entry in overrideRecord.Entries)
            {
                if (entry.Data == null) continue;
                if (!baseEntries.Contains(entry.Data.Reference.FormKey))
                {
                    patchList.Entries ??= new ExtendedList<LeveledSpellEntry>();
                    patchList.Entries.Add(new LeveledSpellEntry
                    {
                        Data = new LeveledSpellEntryData
                        {
                            Level = entry.Data.Level,
                            Count = entry.Data.Count,
                            Reference = entry.Data.Reference.FormKey.ToLink<ISpellRecordGetter>(),
                        }
                    });
                    baseEntries.Add(entry.Data.Reference.FormKey);
                    totalMerged++;
                }
            }
        }

        return new RecordDetail
        {
            FormId = op.FormId, RecordType = "LVSP", Op = "merge_leveled_list",
            EntriesMerged = totalMerged, Sources = sourceNames,
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Tier D — Silent-Failure Detection (v2.7.1)
    // ═══════════════════════════════════════════════════════════════
    //
    // Canonical operator → mods-key mapping. After ApplyModifications
    // runs, an operator is considered "matched" iff its mods-key is
    // present in RecordDetail.Modifications. Multiple operators can
    // share a mods-key (set_flags / clear_flags both → "flags_changed")
    // — both are satisfied by the shared key.
    //
    // Tier D's coverage check (in ApplyModifications) compares this
    // request's populated operators against the mods-keys actually
    // written by the handlers. Any requested operator without its
    // mods-key present after handlers run is unsupported on this
    // (record-type, operator) pair → UnsupportedOperatorException →
    // ProcessOverride rolls back the override and the per-record
    // detail surfaces the structured `unmatched_operators` field.
    //
    // This dict is the source of truth for AUDIT.md's mapping table at
    // dev/plans/v2.7.1_race_patching/AUDIT.md. Phase 3 wire-ups add
    // (operator, record-type) coverage; this table itself does not
    // change unless a NEW operator is introduced.
    private static readonly Dictionary<string, string> OperatorModsKeys = new()
    {
        ["add_keywords"]            = "keywords_added",
        ["remove_keywords"]         = "keywords_removed",
        ["add_spells"]              = "spells_added",
        ["remove_spells"]           = "spells_removed",
        ["add_perks"]               = "perks_added",
        ["remove_perks"]            = "perks_removed",
        ["add_packages"]            = "packages_added",
        ["remove_packages"]         = "packages_removed",
        ["add_factions"]            = "factions_added",
        ["remove_factions"]         = "factions_removed",
        ["add_inventory"]           = "inventory_added",
        ["remove_inventory"]        = "inventory_removed",
        ["add_outfit_items"]        = "outfit_items_added",
        ["remove_outfit_items"]     = "outfit_items_removed",
        ["add_form_list_entries"]   = "form_list_added",
        ["remove_form_list_entries"]= "form_list_removed",
        ["add_items"]               = "items_added",
        ["add_conditions"]          = "conditions_added",
        ["remove_conditions"]       = "conditions_removed",
        ["attach_scripts"]          = "scripts_attached",
        ["set_enchantment"]         = "enchantment_set",
        ["clear_enchantment"]       = "enchantment_cleared",
        ["set_fields"]              = "fields_set",
        ["set_flags"]               = "flags_changed",
        ["clear_flags"]             = "flags_changed",
    };

    /// <summary>
    /// Build a map from user-facing operator name → mods-key for every
    /// operator field populated on this request. Operators with shared
    /// mods-keys (set_flags / clear_flags) appear as separate entries —
    /// the unmatched check treats the shared key as satisfying both.
    /// </summary>
    private static Dictionary<string, string> RequestedOperatorsOf(RecordOperation op)
    {
        var requested = new Dictionary<string, string>();
        void Mark(string operatorName) => requested[operatorName] = OperatorModsKeys[operatorName];

        if (op.AddKeywords?.Count > 0)           Mark("add_keywords");
        if (op.RemoveKeywords?.Count > 0)        Mark("remove_keywords");
        if (op.AddSpells?.Count > 0)             Mark("add_spells");
        if (op.RemoveSpells?.Count > 0)          Mark("remove_spells");
        if (op.AddPerks?.Count > 0)              Mark("add_perks");
        if (op.RemovePerks?.Count > 0)           Mark("remove_perks");
        if (op.AddPackages?.Count > 0)           Mark("add_packages");
        if (op.RemovePackages?.Count > 0)        Mark("remove_packages");
        if (op.AddFactions?.Count > 0)           Mark("add_factions");
        if (op.RemoveFactions?.Count > 0)        Mark("remove_factions");
        if (op.AddInventory?.Count > 0)          Mark("add_inventory");
        if (op.RemoveInventory?.Count > 0)       Mark("remove_inventory");
        if (op.AddOutfitItems?.Count > 0)        Mark("add_outfit_items");
        if (op.RemoveOutfitItems?.Count > 0)     Mark("remove_outfit_items");
        if (op.AddFormListEntries?.Count > 0)    Mark("add_form_list_entries");
        if (op.RemoveFormListEntries?.Count > 0) Mark("remove_form_list_entries");
        if (op.AddItems?.Count > 0)              Mark("add_items");
        if (op.AddConditions?.Count > 0)         Mark("add_conditions");
        if (op.RemoveConditions?.Count > 0)      Mark("remove_conditions");
        if (op.AttachScripts?.Count > 0)         Mark("attach_scripts");
        if (op.SetEnchantment != null)           Mark("set_enchantment");
        if (op.ClearEnchantment == true)         Mark("clear_enchantment");
        if (op.SetFields?.Count > 0)             Mark("set_fields");
        if (op.SetFlags?.Count > 0)              Mark("set_flags");
        if (op.ClearFlags?.Count > 0)            Mark("clear_flags");

        return requested;
    }

    /// <summary>
    /// Thrown by ApplyModifications when one or more requested operators
    /// have no matching handler for the override's record type.
    /// ProcessOverride catches this, rolls back the override, and
    /// populates the per-record detail's structured error fields.
    /// </summary>
    private class UnsupportedOperatorException : InvalidOperationException
    {
        public string RecordType { get; }
        public List<string> UnmatchedOperators { get; }

        public UnsupportedOperatorException(string recordType, List<string> unmatchedOperators)
            : base($"Record type {recordType} does not support: {string.Join(", ", unmatchedOperators)}")
        {
            RecordType = recordType;
            UnmatchedOperators = unmatchedOperators;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Apply All Modifications to an Override Record
    // ═══════════════════════════════════════════════════════════════

    private void ApplyModifications(
        IMajorRecord record, RecordOperation op, RecordDetail detail)
    {
        var mods = detail.Modifications!;
        var requested = RequestedOperatorsOf(op);

        // Tier D contract: a mods-key is written iff the corresponding
        // handler ran for this record type. "Ran with 0 changes" is
        // success (e.g. all items already present and dedup'd); "no
        // matching arm at all" is failure → unmatched check below.
        // Conditional `if (added > 0) mods[...] = added` writes were
        // scrubbed in v2.7.1 so the present-iff-ran rule holds.

        // ── set_fields ──
        if (op.SetFields?.Count > 0)
            mods["fields_set"] = ApplySetFields(record, op.SetFields);

        // ── set_flags / clear_flags ──
        if (op.SetFlags?.Count > 0 || op.ClearFlags?.Count > 0)
            mods["flags_changed"] = ApplyFlags(record, op.SetFlags, op.ClearFlags);

        // ── Keywords ──
        var keywords = GetKeywordsList(record);
        if (keywords != null)
        {
            if (op.AddKeywords?.Count > 0)
                mods["keywords_added"] = AddFormLinks(keywords, op.AddKeywords);
            if (op.RemoveKeywords?.Count > 0)
                mods["keywords_removed"] = RemoveFormLinks(keywords, op.RemoveKeywords);
        }

        // ── NPC-specific lists ──
        if (record is Npc npc)
        {
            if (op.AddSpells?.Count > 0)
            {
                npc.ActorEffect ??= new ExtendedList<IFormLinkGetter<ISpellRecordGetter>>();
                mods["spells_added"] = AddFormLinks(npc.ActorEffect, op.AddSpells);
            }
            if (op.RemoveSpells?.Count > 0)
            {
                mods["spells_removed"] = npc.ActorEffect != null
                    ? RemoveFormLinks(npc.ActorEffect, op.RemoveSpells)
                    : 0;
            }

            if (op.AddPerks?.Count > 0)
            {
                npc.Perks ??= new ExtendedList<PerkPlacement>();
                int added = 0;
                foreach (var pk in op.AddPerks)
                {
                    var fk = FormIdHelper.Parse(pk);
                    if (!npc.Perks.Any(p => p.Perk.FormKey == fk))
                    {
                        npc.Perks.Add(new PerkPlacement { Perk = fk.ToLink<IPerkGetter>(), Rank = 1 });
                        added++;
                    }
                }
                mods["perks_added"] = added;
            }
            if (op.RemovePerks?.Count > 0)
            {
                int removed = 0;
                if (npc.Perks != null)
                {
                    foreach (var pk in op.RemovePerks)
                    {
                        var fk = FormIdHelper.Parse(pk);
                        removed += npc.Perks.RemoveAll(p => p.Perk.FormKey == fk);
                    }
                }
                mods["perks_removed"] = removed;
            }

            // ── Packages ──
            if (op.AddPackages?.Count > 0)
            {
                int added = 0;
                foreach (var pkg in op.AddPackages)
                {
                    var fk = FormIdHelper.Parse(pkg);
                    var link = fk.ToLink<IPackageGetter>();
                    if (!npc.Packages.Contains(link))
                    {
                        npc.Packages.Add(link);
                        added++;
                    }
                }
                mods["packages_added"] = added;
            }
            if (op.RemovePackages?.Count > 0)
            {
                int removed = 0;
                foreach (var pkg in op.RemovePackages)
                {
                    var fk = FormIdHelper.Parse(pkg);
                    var link = fk.ToLink<IPackageGetter>();
                    if (npc.Packages.Remove(link)) removed++;
                }
                mods["packages_removed"] = removed;
            }

            // ── Factions ──
            if (op.AddFactions?.Count > 0)
            {
                int added = 0;
                foreach (var fe in op.AddFactions)
                {
                    var fk = FormIdHelper.Parse(fe.Faction);
                    if (!npc.Factions.Any(f => f.Faction.FormKey == fk))
                    {
                        npc.Factions.Add(new RankPlacement
                        {
                            Faction = fk.ToLink<IFactionGetter>(),
                            Rank = (sbyte)fe.Rank,
                        });
                        added++;
                    }
                }
                mods["factions_added"] = added;
            }
            if (op.RemoveFactions?.Count > 0)
            {
                int removed = 0;
                if (npc.Factions != null)
                {
                    foreach (var fac in op.RemoveFactions)
                    {
                        var fk = FormIdHelper.Parse(fac);
                        removed += npc.Factions.RemoveAll(f => f.Faction.FormKey == fk);
                    }
                }
                mods["factions_removed"] = removed;
            }

            // ── NPC Inventory ──
            if (op.AddInventory?.Count > 0)
            {
                npc.Items ??= new ExtendedList<ContainerEntry>();
                int added = 0;
                foreach (var inv in op.AddInventory)
                {
                    var fk = FormIdHelper.Parse(inv.Item);
                    npc.Items.Add(new ContainerEntry
                    {
                        Item = new ContainerItem
                        {
                            Item = fk.ToLink<IItemGetter>(),
                            Count = inv.Count,
                        }
                    });
                    added++;
                }
                mods["inventory_added"] = added;
            }
            if (op.RemoveInventory?.Count > 0)
            {
                int removed = 0;
                if (npc.Items != null)
                {
                    foreach (var inv in op.RemoveInventory)
                    {
                        var fk = FormIdHelper.Parse(inv);
                        removed += npc.Items.RemoveAll(i => i.Item.Item.FormKey == fk);
                    }
                }
                mods["inventory_removed"] = removed;
            }
        }

        // ── RACE actor effects (add_spells / remove_spells on RACE) ──
        // v2.7.1 Tier A wire-up. Mirrors the NPC ActorEffect pattern above.
        // Tier D contract: mods key written unconditionally inside the matched
        // arm; the null-check on race.ActorEffect controls only the iteration,
        // not the mods-key write.
        if (record is Race race)
        {
            if (op.AddSpells?.Count > 0)
            {
                race.ActorEffect ??= new ExtendedList<IFormLinkGetter<ISpellRecordGetter>>();
                mods["spells_added"] = AddFormLinks(race.ActorEffect, op.AddSpells);
            }
            if (op.RemoveSpells?.Count > 0)
            {
                mods["spells_removed"] = race.ActorEffect != null
                    ? RemoveFormLinks(race.ActorEffect, op.RemoveSpells)
                    : 0;
            }
        }

        // ── Container Inventory ──
        if (record is Container container)
        {
            if (op.AddInventory?.Count > 0)
            {
                container.Items ??= new ExtendedList<ContainerEntry>();
                int added = 0;
                foreach (var inv in op.AddInventory)
                {
                    var fk = FormIdHelper.Parse(inv.Item);
                    container.Items.Add(new ContainerEntry
                    {
                        Item = new ContainerItem
                        {
                            Item = fk.ToLink<IItemGetter>(),
                            Count = inv.Count,
                        }
                    });
                    added++;
                }
                mods["inventory_added"] = added;
            }
            if (op.RemoveInventory?.Count > 0)
            {
                int removed = 0;
                if (container.Items != null)
                {
                    foreach (var inv in op.RemoveInventory)
                    {
                        var fk = FormIdHelper.Parse(inv);
                        removed += container.Items.RemoveAll(i => i.Item.Item.FormKey == fk);
                    }
                }
                mods["inventory_removed"] = removed;
            }
        }

        // ── Leveled item entries (add_items on LVLI) ──
        if (record is LeveledItem leveledItem && op.AddItems?.Count > 0)
        {
            leveledItem.Entries ??= new ExtendedList<LeveledItemEntry>();
            int added = 0;
            foreach (var item in op.AddItems)
            {
                var fk = FormIdHelper.Parse(item.Reference);
                leveledItem.Entries.Add(new LeveledItemEntry
                {
                    Data = new LeveledItemEntryData
                    {
                        Level = item.Level, Count = item.Count,
                        Reference = fk.ToLink<IItemGetter>(),
                    }
                });
                added++;
            }
            mods["items_added"] = added;
        }

        // ── Leveled NPC entries (add_items on LVLN) ──
        // v2.7.1 Tier A wire-up. Mirrors the LVLI shape; entry construction
        // matches MergeLeveledNpcs at line 303. Reference target is INpcSpawnGetter.
        if (record is LeveledNpc leveledNpc && op.AddItems?.Count > 0)
        {
            leveledNpc.Entries ??= new ExtendedList<LeveledNpcEntry>();
            int added = 0;
            foreach (var item in op.AddItems)
            {
                var fk = FormIdHelper.Parse(item.Reference);
                leveledNpc.Entries.Add(new LeveledNpcEntry
                {
                    Data = new LeveledNpcEntryData
                    {
                        Level = item.Level, Count = item.Count,
                        Reference = fk.ToLink<INpcSpawnGetter>(),
                    }
                });
                added++;
            }
            mods["items_added"] = added;
        }

        // ── Leveled Spell entries (add_items on LVSP) ──
        // v2.7.1 Tier A wire-up. Mirrors the LVLI shape; entry construction
        // matches MergeLeveledSpells at line 353. Reference target is ISpellRecordGetter.
        if (record is LeveledSpell leveledSpell && op.AddItems?.Count > 0)
        {
            leveledSpell.Entries ??= new ExtendedList<LeveledSpellEntry>();
            int added = 0;
            foreach (var item in op.AddItems)
            {
                var fk = FormIdHelper.Parse(item.Reference);
                leveledSpell.Entries.Add(new LeveledSpellEntry
                {
                    Data = new LeveledSpellEntryData
                    {
                        Level = item.Level, Count = item.Count,
                        Reference = fk.ToLink<ISpellRecordGetter>(),
                    }
                });
                added++;
            }
            mods["items_added"] = added;
        }

        // ── Outfit items ──
        if (record is Outfit outfit)
        {
            if (op.AddOutfitItems?.Count > 0)
            {
                outfit.Items ??= new ExtendedList<IFormLinkGetter<IOutfitTargetGetter>>();
                int added = 0;
                foreach (var oi in op.AddOutfitItems)
                {
                    var fk = FormIdHelper.Parse(oi);
                    outfit.Items.Add(fk.ToLink<IOutfitTargetGetter>());
                    added++;
                }
                mods["outfit_items_added"] = added;
            }
            if (op.RemoveOutfitItems?.Count > 0)
            {
                int removed = 0;
                if (outfit.Items != null)
                {
                    foreach (var oi in op.RemoveOutfitItems)
                    {
                        var fk = FormIdHelper.Parse(oi);
                        var link = fk.ToLink<IOutfitTargetGetter>();
                        if (outfit.Items.Remove(link)) removed++;
                    }
                }
                mods["outfit_items_removed"] = removed;
            }
        }

        // ── FormList entries ──
        if (record is FormList formList)
        {
            if (op.AddFormListEntries?.Count > 0)
            {
                int added = 0;
                foreach (var fle in op.AddFormListEntries)
                {
                    var fk = FormIdHelper.Parse(fle);
                    formList.Items.Add(fk.ToLink<ISkyrimMajorRecordGetter>());
                    added++;
                }
                mods["form_list_added"] = added;
            }
            if (op.RemoveFormListEntries?.Count > 0)
            {
                int removed = 0;
                if (formList.Items != null)
                {
                    foreach (var fle in op.RemoveFormListEntries)
                    {
                        var fk = FormIdHelper.Parse(fle);
                        var link = fk.ToLink<ISkyrimMajorRecordGetter>();
                        if (formList.Items.Remove(link)) removed++;
                    }
                }
                mods["form_list_removed"] = removed;
            }
        }

        // ── Conditions ──
        // v2.8.0: ApplyAddConditions / ApplyRemoveConditions return int?;
        // null signals the record type has no Conditions property. Skip
        // writing the mods key in that case so Tier D's coverage check at
        // the tail of this method surfaces a uniform
        // unmatched_operators: ["add_conditions"] / ["remove_conditions"]
        // shape — same as every other unsupported (operator, record-type)
        // combo. Pre-v2.8.0 the helpers threw directly, producing a
        // structurally different error path.
        if (op.AddConditions?.Count > 0)
        {
            var added = ApplyAddConditions(record, op.AddConditions, op.ConditionTarget);
            if (added.HasValue) mods["conditions_added"] = added.Value;
        }

        if (op.RemoveConditions?.Count > 0)
        {
            var removed = ApplyRemoveConditions(record, op.RemoveConditions, op.ConditionTarget);
            if (removed.HasValue) mods["conditions_removed"] = removed.Value;
        }

        // ── VMAD script attachment ──
        if (op.AttachScripts?.Count > 0)
            mods["scripts_attached"] = ApplyAttachScripts(record, op.AttachScripts);

        // ── Enchantment ──
        // Only Armor and Weapon carry ObjectEffect — write the mods key
        // iff one of those arms actually fired. Tier D treats absent
        // mods key as "no handler matched" → unmatched-operator error.
        if (op.SetEnchantment != null)
        {
            var enchFk = FormIdHelper.Parse(op.SetEnchantment);
            bool applied = false;
            if (record is Armor armor)
            {
                armor.ObjectEffect.SetTo(enchFk);
                applied = true;
            }
            else if (record is Weapon weapon)
            {
                weapon.ObjectEffect.SetTo(enchFk);
                applied = true;
            }
            if (applied)
                mods["enchantment_set"] = op.SetEnchantment;
        }
        if (op.ClearEnchantment == true)
        {
            bool applied = false;
            if (record is Armor armor)
            {
                armor.ObjectEffect.Clear();
                applied = true;
            }
            else if (record is Weapon weapon)
            {
                weapon.ObjectEffect.Clear();
                applied = true;
            }
            if (applied)
                mods["enchantment_cleared"] = true;
        }

        // ── Tier D — silent-failure detection ──
        // Any requested operator whose mods-key is absent after every
        // handler ran is unsupported on this record type. Throw with
        // structured fields; ProcessOverride catches, rolls back the
        // override, and surfaces the error in the per-record detail.
        var unmatched = requested
            .Where(kv => !mods.ContainsKey(kv.Value))
            .Select(kv => kv.Key)
            .ToList();
        if (unmatched.Count > 0)
        {
            throw new UnsupportedOperatorException(
                detail.RecordType ?? RecordTypeCode(record),
                unmatched);
        }

        // Clean up empty modifications dict
        if (mods.Count == 0)
            detail.Modifications = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // set_fields — Reflection-Based Field Setting
    // ═══════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, Dictionary<string, string>> FieldAliases = new()
    {
        ["NPC_"] = new()
        {
            ["Health"] = "Configuration.HealthOffset",
            ["HealthOffset"] = "Configuration.HealthOffset",
            ["Magicka"] = "Configuration.MagickaOffset",
            ["MagickaOffset"] = "Configuration.MagickaOffset",
            ["Stamina"] = "Configuration.StaminaOffset",
            ["StaminaOffset"] = "Configuration.StaminaOffset",
            ["SpeedMultiplier"] = "Configuration.SpeedMultiplier",
        },
        ["ARMO"] = new()
        {
            ["ArmorRating"] = "ArmorRating",
            ["Value"] = "Value",
            ["Weight"] = "Weight",
        },
        ["WEAP"] = new()
        {
            ["Damage"] = "BasicStats.Damage",
            ["Value"] = "BasicStats.Value",
            ["Weight"] = "BasicStats.Weight",
            ["Speed"] = "Data.Speed",
            ["Reach"] = "Data.Reach",
            ["CriticalDamage"] = "Critical.Damage",
        },
        ["ALCH"] = new()
        {
            ["Value"] = "Value",
            ["Weight"] = "Weight",
        },
        ["RACE"] = new()
        {
            ["BaseHealth"]   = "Starting[Health]",
            ["BaseMagicka"]  = "Starting[Magicka]",
            ["BaseStamina"]  = "Starting[Stamina]",
            ["HealthRegen"]  = "Regen[Health]",
            ["MagickaRegen"] = "Regen[Magicka]",
            ["StaminaRegen"] = "Regen[Stamina]",
        },
    };

    private int ApplySetFields(IMajorRecord record, Dictionary<string, JsonElement> fields)
    {
        int count = 0;
        var typeCode = RecordTypeCode(record);

        foreach (var (fieldName, value) in fields)
        {
            // Resolve alias to property path
            string path = fieldName;
            if (FieldAliases.TryGetValue(typeCode, out var aliases))
                if (aliases.TryGetValue(fieldName, out var resolved))
                    path = resolved;

            SetPropertyByPath(record, path, value);
            count++;
        }
        return count;
    }

    private static void SetPropertyByPath(object target, string path, JsonElement value)
    {
        var parts = path.Split('.');
        object current = target;

        // Navigate to the parent of the target property.
        // Intermediate segments must be plain names — bracket syntax is final-segment-only in v2.7.1.
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].IndexOf('[') >= 0 || parts[i].IndexOf(']') >= 0)
                throw new ArgumentException(
                    $"Bracket syntax is not supported on intermediate path segment '{parts[i]}'. " +
                    "v2.7.1 supports terminal-segment dict access only (Foo[Key], not Foo[Key].Sub).");

            var prop = current.GetType().GetProperty(parts[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                throw new ArgumentException($"Property '{parts[i]}' not found on {current.GetType().Name}");

            var next = prop.GetValue(current);
            if (next == null)
            {
                // Try to instantiate the intermediate object
                next = System.Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(current, next);
            }
            current = next!;
        }

        var (finalName, finalKey) = ParsePathSegment(parts[^1]);
        var finalProp = current.GetType().GetProperty(finalName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (finalProp == null)
            throw new ArgumentException($"Property '{finalName}' not found on {current.GetType().Name}");

        // Tier C — bracket-indexer write: single dict entry via the property's IDictionary<,> indexer.
        if (finalKey != null)
        {
            WriteDictEntry(finalProp, current, finalKey, value);
            return;
        }

        // Tier C — whole-dict object form against a dict-typed property:
        // merge each JSON member via the indexer. v2.7.1 ships merge-only semantics
        // regardless of setter presence (single-path collapse — replace deferred to v2.8).
        if (value.ValueKind == JsonValueKind.Object &&
            IsClosedDictionary(finalProp.PropertyType, out _, out _))
        {
            WriteDictMerge(finalProp, current, value);
            return;
        }

        // v2.8.0 Branch B — JSON Object → sub-LoquiObject in-place merge.
        // When the JSON value is an object and the target property is a constructible
        // non-dict, non-FormLink reference type (e.g. Effect.Data : EffectData),
        // get-or-Activator-create the sub-instance, then recursively SetPropertyByPath
        // each JSON member. In-place merge — preserves sibling fields not named in JSON.
        //
        // Probe-justified: EFFECTS_AUDIT.md § Effect class shape captures Effect.Data
        // as a settable EffectData sub-LoquiObject, null-initialized after `new Effect()`.
        // Required for set_fields: {Effects: [{Data: {Magnitude, Area, Duration}, ...}]}.
        //
        // Guards: !IsFormLinkType excludes IFormLink<>/IFormLinkNullable<>/FormLink<>
        // and friends — those receive JSON strings, not objects; the Activator path
        // would produce the wrong shape. !=typeof(string) prevents Activator on string.
        // IsClass excludes value types (structs/primitives) — they fall through to
        // ConvertJsonValue and surface the existing "Cannot convert" error.
        //
        // Side effect: incidentally enables set_fields: {Configuration: {Health: 200}}
        // sub-LoquiObject merges on every record. Per scope-lock, NOT advertised in
        // the schema description — the only Phase 1 user-facing surface is the
        // Effects-array form.
        if (value.ValueKind == JsonValueKind.Object &&
            finalProp.PropertyType.IsClass &&
            finalProp.PropertyType != typeof(string) &&
            !IsFormLinkType(finalProp.PropertyType))
        {
            var subTarget = finalProp.GetValue(current);
            if (subTarget == null)
            {
                if (!finalProp.CanWrite)
                    throw new ArgumentException(
                        $"Sub-object property '{finalProp.Name}' is null and has no setter; cannot merge JSON Object into it.");
                subTarget = System.Activator.CreateInstance(finalProp.PropertyType)
                    ?? throw new InvalidOperationException(
                        $"Activator.CreateInstance returned null for {FriendlyTypeName(finalProp.PropertyType)}.");
                finalProp.SetValue(current, subTarget);
            }
            foreach (var member in value.EnumerateObject())
            {
                SetPropertyByPath(subTarget, member.Name, member.Value);
            }
            return;
        }

        var converted = ConvertJsonValue(value, finalProp.PropertyType);
        finalProp.SetValue(current, converted);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Tier C — Bracket-indexer dict mutation (v2.7.1)
    //
    // Adds path syntax `PropertyName[Key]` to set_fields, plus whole-dict JSON-object
    // form against any IDictionary<TKey, TValue> property. Both routes go through the
    // dict's `set_Item` indexer — single-path merge semantics, regardless of whether
    // Mutagen exposes a setter on the property. RACE.Starting / RACE.Regen /
    // RACE.BipedObjectNames are the v2.7.1 targets (all setter-less, indexer-only).
    //
    // Scope locks (per PLAN.md):
    //   - Final-segment only. `Foo[Key].Sub` (chained dict access) → ArgumentException.
    //   - Merge semantics, not replace — only the keys named in the JSON object are
    //     touched; existing keys preserved.
    //   - Key types: enums (Enum.Parse, ignoreCase) and common primitives (int/uint/
    //     short/ushort/byte/long/string). Other key types → ArgumentException.
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse a path segment of the form "Property" or "Property[Key]".
    /// Returns (name, null) for plain names, (name, keyText) for bracket form.
    /// Throws on malformed brackets ("Foo[", "Foo]", "Foo[]", "[Foo]", "Foo[a]b").
    /// </summary>
    private static (string name, string? key) ParsePathSegment(string segment)
    {
        var openIdx = segment.IndexOf('[');
        if (openIdx < 0)
        {
            if (segment.IndexOf(']') >= 0)
                throw new ArgumentException($"Malformed path segment '{segment}': stray ']' without '['.");
            return (segment, null);
        }

        var closeIdx = segment.IndexOf(']', openIdx + 1);
        if (closeIdx < 0)
            throw new ArgumentException($"Malformed path segment '{segment}': missing ']' to match '['.");
        if (closeIdx != segment.Length - 1)
            throw new ArgumentException($"Malformed path segment '{segment}': content after ']'.");
        if (openIdx == 0)
            throw new ArgumentException($"Malformed path segment '{segment}': missing property name before '['.");

        var name = segment.Substring(0, openIdx);
        var key = segment.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
        if (key.Length == 0)
            throw new ArgumentException($"Malformed path segment '{segment}': empty key inside brackets.");

        return (name, key);
    }

    /// <summary>
    /// v2.8.0 — Test whether <paramref name="t"/> is one of the Mutagen FormLink
    /// generic shapes (<c>IFormLinkGetter&lt;T&gt;</c>, <c>IFormLink&lt;T&gt;</c>,
    /// <c>IFormLinkNullable&lt;T&gt;</c>, <c>FormLink&lt;T&gt;</c>,
    /// <c>FormLinkNullable&lt;T&gt;</c>). Used by Branch B in <c>SetPropertyByPath</c>
    /// to keep FormLink-typed properties out of the JSON-Object → sub-LoquiObject
    /// path — those receive JSON string values (parsed via <c>FormIdHelper.Parse</c>),
    /// not JSON objects, and Activator-creating an empty FormLink would produce
    /// the wrong shape if a user mistakenly passes a JSON object.
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
    /// v2.8.0 — Test whether <paramref name="t"/> is one of the Mutagen
    /// FormLink shapes that requires the <c>FormLinkNullable&lt;T&gt;</c>
    /// concrete (rather than the non-nullable <c>FormLink&lt;T&gt;</c>) when
    /// constructing an instance for assignment. Used by ConvertJsonValue's
    /// JSON String → single-field FormLink branch — properties typed
    /// IFormLinkNullable&lt;T&gt; reject FormLink&lt;T&gt; instances at the
    /// reflection setter because FormLink&lt;T&gt; doesn't implement
    /// IFormLinkNullable&lt;T&gt;.
    /// </summary>
    private static bool IsNullableFormLinkType(Type t)
    {
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(IFormLinkNullable<>) || def == typeof(FormLinkNullable<>);
    }

    /// <summary>
    /// Test whether <paramref name="t"/> is (or implements) a closed
    /// <c>IDictionary&lt;TKey, TValue&gt;</c>. Returns the generic arguments via out
    /// parameters when true.
    /// </summary>
    private static bool IsClosedDictionary(Type t, out Type? keyType, out Type? valueType)
    {
        keyType = null;
        valueType = null;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        {
            var args = t.GetGenericArguments();
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        foreach (var iface in t.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = iface.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parse a bracket-key string into the dict's <c>TKey</c> type. Enums use
    /// case-insensitive name parsing; common integer primitives use invariant culture.
    /// </summary>
    private static object ParseDictKey(string keyText, Type keyType)
    {
        if (keyType.IsEnum)
            return Enum.Parse(keyType, keyText, ignoreCase: true);

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (keyType == typeof(string)) return keyText;
        if (keyType == typeof(int))    return int.Parse(keyText, ci);
        if (keyType == typeof(uint))   return uint.Parse(keyText, ci);
        if (keyType == typeof(short))  return short.Parse(keyText, ci);
        if (keyType == typeof(ushort)) return ushort.Parse(keyText, ci);
        if (keyType == typeof(byte))   return byte.Parse(keyText, ci);
        if (keyType == typeof(long))   return long.Parse(keyText, ci);

        throw new ArgumentException(
            $"Unsupported dict key type '{FriendlyTypeName(keyType)}' " +
            "(supported: enum, string, int, uint, short, ushort, byte, long).");
    }

    /// <summary>
    /// Locate the single-arg indexer on <paramref name="dictInstance"/> whose parameter
    /// type matches <paramref name="expectedKeyType"/>.
    /// </summary>
    private static PropertyInfo GetDictIndexer(object dictInstance, Type expectedKeyType)
    {
        foreach (var prop in dictInstance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var indexParams = prop.GetIndexParameters();
            if (indexParams.Length == 1 && indexParams[0].ParameterType == expectedKeyType)
                return prop;
        }
        throw new ArgumentException(
            $"Could not locate IDictionary<,> indexer on {FriendlyTypeName(dictInstance.GetType())}.");
    }

    /// <summary>
    /// Write a single dict entry via the property's indexer (bracket-key form).
    /// </summary>
    private static void WriteDictEntry(PropertyInfo prop, object owner, string keyText, JsonElement value)
    {
        if (!IsClosedDictionary(prop.PropertyType, out var keyType, out var valueType))
            throw new ArgumentException(
                $"Bracket syntax requires an IDictionary<,> property; {prop.Name} is " +
                $"{FriendlyTypeName(prop.PropertyType)}.");

        var dict = prop.GetValue(owner);
        if (dict == null)
            throw new ArgumentException(
                $"Dict-typed property {prop.Name} is null at write time " +
                "(no auto-init path — Mutagen typically initializes these).");

        var parsedKey = ParseDictKey(keyText, keyType!);
        var convertedValue = ConvertJsonValue(value, valueType!);
        var indexer = GetDictIndexer(dict, keyType!);
        indexer.SetValue(dict, convertedValue, new[] { parsedKey });
    }

    /// <summary>
    /// Merge each member of a JSON object into the property's dict via the indexer.
    /// Only the named keys are touched — existing keys are preserved (merge, not replace).
    /// </summary>
    private static void WriteDictMerge(PropertyInfo prop, object owner, JsonElement objectValue)
    {
        if (!IsClosedDictionary(prop.PropertyType, out var keyType, out var valueType))
            throw new ArgumentException(
                $"JSON object form requires an IDictionary<,> property; {prop.Name} is " +
                $"{FriendlyTypeName(prop.PropertyType)}.");

        var dict = prop.GetValue(owner);
        if (dict == null)
            throw new ArgumentException(
                $"Dict-typed property {prop.Name} is null at write time " +
                "(no auto-init path — Mutagen typically initializes these).");

        var indexer = GetDictIndexer(dict, keyType!);
        foreach (var member in objectValue.EnumerateObject())
        {
            var parsedKey = ParseDictKey(member.Name, keyType!);
            var convertedValue = ConvertJsonValue(member.Value, valueType!);
            indexer.SetValue(dict, convertedValue, new[] { parsedKey });
        }
    }

    private static object? ConvertJsonValue(JsonElement value, Type targetType)
    {
        // Handle nullable types
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(short)) return value.GetInt16();
        if (underlying == typeof(ushort)) return value.GetUInt16();
        if (underlying == typeof(int)) return value.GetInt32();
        if (underlying == typeof(uint)) return value.GetUInt32();
        if (underlying == typeof(float)) return value.GetSingle();
        if (underlying == typeof(double)) return value.GetDouble();
        if (underlying == typeof(byte)) return value.GetByte();
        if (underlying == typeof(bool)) return value.GetBoolean();
        if (underlying == typeof(string)) return value.GetString();

        // v2.9.3 — JSON String → TranslatedString convenience path. Used by
        // PerkEntryPointSetText.Text (Mutagen.Bethesda.Strings.TranslatedString
        // Sub-Loqui slot) under set_fields:{Effects:[{type:"PerkEntryPointSetText",
        // Text:"..."}]}. Without this branch the JSON-string flows through to
        // Branch B's sub-LoquiObject merge (line ~1097) which expects a JSON
        // Object, surfacing a confusing "Cannot convert JSON String to
        // TranslatedString" error from this method's tail throw. TranslatedString
        // has an implicit conversion from System.String — match that contract.
        // Targeted: only fires when targetType is exactly TranslatedString
        // (single-language Sub-Loqui — covers PEPSetText.Text + ButtonLabel on
        // APerkEffect base + the same shape on other rec-type slots).
        if (value.ValueKind == JsonValueKind.String
            && underlying == typeof(Mutagen.Bethesda.Strings.TranslatedString))
        {
            return new Mutagen.Bethesda.Strings.TranslatedString(
                Mutagen.Bethesda.Strings.Language.English, value.GetString());
        }

        // Enum support — try parsing the string as enum name
        if (underlying.IsEnum && value.ValueKind == JsonValueKind.String)
            return Enum.Parse(underlying, value.GetString()!, ignoreCase: true);
        if (underlying.IsEnum && value.ValueKind == JsonValueKind.Number)
            return Enum.ToObject(underlying, value.GetInt32());

        // v2.8.0 — JSON String → single-field FormLink. Bonus-catch from Phase 1
        // Layer 1.E smoke: Effect.BaseEffect is IFormLinkNullable<IMagicEffectGetter>,
        // not IFormLink<>. ConvertJsonElementToListItem's FormLink branch only handles
        // list-element types (IFormLinkGetter<>/IFormLink<>/FormLink<>), and Mutagen
        // documents that "list elements are non-nullable" — so single-field FormLink
        // properties (BaseEffect on Effect, ArmorRace on Race, etc.) flow here via
        // SetPropertyByPath fallback. Without this branch, every set_fields against
        // a single FormLink fails. Probe-predicted (EFFECTS_AUDIT.md § Open
        // questions item 3); confirmed by smoke; fix is one-liner-y by design.
        //
        // For nullable target types the concrete must be FormLinkNullable<T> —
        // FormLink<T> doesn't implement IFormLinkNullable<T>, so a setter that
        // expects the nullable interface would reject a non-nullable instance.
        if (value.ValueKind == JsonValueKind.String && IsFormLinkType(underlying))
        {
            var formKey = FormIdHelper.Parse(value.GetString()!);
            var inner = underlying.GetGenericArguments()[0];
            var concreteType = IsNullableFormLinkType(underlying)
                ? typeof(FormLinkNullable<>).MakeGenericType(inner)
                : typeof(FormLink<>).MakeGenericType(inner);
            return System.Activator.CreateInstance(concreteType, formKey);
        }

        // JSON arrays into Mutagen list-typed fields (ExtendedList<T> / IExtendedList<T> / IList<T>).
        // Common case: properties typed ExtendedList<IFormLinkGetter<X>> — e.g. MUSC.Tracks.
        if (value.ValueKind == JsonValueKind.Array)
            return ConvertJsonArray(value, underlying);

        throw new ArgumentException(
            $"Cannot convert JSON {value.ValueKind} to {FriendlyTypeName(targetType)}");
    }

    /// <summary>
    /// Build a Mutagen list (ExtendedList&lt;T&gt;) from a JSON array, applying per-element
    /// conversion. Supports element types of FormLink&lt;X&gt; / IFormLink&lt;X&gt; /
    /// IFormLinkGetter&lt;X&gt; (parsed from "Plugin:LocalID" strings) and primitives/enums
    /// (delegated back to ConvertJsonValue). Complex Mutagen entry types
    /// (LeveledItemEntry, ContainerEntry, etc.) are not supported here — callers should
    /// use the dedicated add_* operations for those.
    /// </summary>
    private static object ConvertJsonArray(JsonElement array, Type targetType)
    {
        var elementType = ExtractListElementType(targetType);
        if (elementType == null)
            throw new ArgumentException(
                $"Cannot convert JSON Array to {FriendlyTypeName(targetType)}: " +
                "target is not a list type (no IList<T> interface).");

        // Mutagen accepts ExtendedList<T> for any IList<T> / IExtendedList<T> property setter.
        var listType = typeof(ExtendedList<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)System.Activator.CreateInstance(listType)!;

        int index = 0;
        foreach (var element in array.EnumerateArray())
        {
            try
            {
                list.Add(ConvertJsonElementToListItem(element, elementType));
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Element [{index}] of JSON Array could not be converted to " +
                    $"{FriendlyTypeName(elementType)}: {ex.Message}", ex);
            }
            index++;
        }

        return list;
    }

    private static Type? ExtractListElementType(Type targetType)
    {
        if (targetType.IsGenericType)
        {
            var def = targetType.GetGenericTypeDefinition();
            if (def == typeof(IList<>) || def == typeof(ICollection<>) || def == typeof(IEnumerable<>))
                return targetType.GetGenericArguments()[0];
        }

        // Walk implemented/inherited interfaces — finds IList<T> on
        // ExtendedList<T>, IExtendedList<T>, and any other IList<T> implementer.
        foreach (var iface in targetType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    private static object? ConvertJsonElementToListItem(JsonElement element, Type elementType)
    {
        // FormLink family. List elements are non-nullable in Mutagen
        // (FormLinkNullable only appears in single-field slots, not list elements),
        // so a single FormLink<T> instance satisfies all three target shapes.
        if (elementType.IsGenericType)
        {
            var def = elementType.GetGenericTypeDefinition();
            if (def == typeof(IFormLinkGetter<>) || def == typeof(IFormLink<>) || def == typeof(FormLink<>))
            {
                if (element.ValueKind != JsonValueKind.String)
                    throw new ArgumentException(
                        "FormLink elements must be JSON strings (e.g. \"Plugin.esp:01ABCD\"), " +
                        $"got {element.ValueKind}.");

                var formKey = FormIdHelper.Parse(element.GetString()!);
                var inner = elementType.GetGenericArguments()[0];
                var formLinkType = typeof(FormLink<>).MakeGenericType(inner);
                return System.Activator.CreateInstance(formLinkType, formKey);
            }
        }

        // v2.8.0 Branch A — JSON Object → constructed LoquiObject element.
        // Used by set_fields: {Effects: [{...}, ...]} on SPEL/ALCH/ENCH/SCRL/INGR
        // (and by any future list-of-LoquiObject property the bridge dispatches).
        // Each JSON object constructs a fresh elementType instance; per-property
        // values flow back through SetPropertyByPath (recursive — so nested
        // sub-objects, FormLinks, and lists all reuse existing machinery).
        //
        // Special case typeof(Condition): the generic Activator path can't
        // construct an abstract class — see EFFECTS_AUDIT.md § Constructibility.
        // BuildConditionFromJson uses the {function, operator, value, global, ...}
        // DSL the existing ApplyAddConditions handler has used since v2.4.1.
        //
        // v2.9.3 — APerkEffect is also abstract (12 concrete leaves: PerkAbilityEffect,
        // PerkQuestEffect, and 10 PerkEntryPoint* leaves under abstract intermediate
        // APerkEntryPointEffect — see APERK_EFFECTS_AUDIT.md § Inventory totals).
        // Mirrors the typeof(Condition) special case; routes to BuildPerkEffectFromJson
        // which dispatches via an explicit "type" discriminator naming the concrete
        // Mutagen leaf class. Carrier dispatch is property-type-driven via reflection
        // (Perk.Effects exposes ExtendedList<APerkEffect>), so adding the type-special-case
        // here automatically opens set_fields:{Effects:[...]} on PERK with no carrier-list
        // edit needed.
        if (element.ValueKind == JsonValueKind.Object && elementType.IsClass && elementType != typeof(string))
        {
            if (elementType == typeof(Condition))
                return BuildConditionFromJson(element);

            if (elementType == typeof(APerkEffect))
                return BuildPerkEffectFromJson(element);

            object? entry;
            try
            {
                entry = System.Activator.CreateInstance(elementType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Cannot construct list element of type {FriendlyTypeName(elementType)} via Activator.CreateInstance: {ex.Message}",
                    ex);
            }
            if (entry == null)
                throw new InvalidOperationException(
                    $"Activator.CreateInstance returned null for {FriendlyTypeName(elementType)}.");

            foreach (var member in element.EnumerateObject())
            {
                SetPropertyByPath(entry, member.Name, member.Value);
            }
            return entry;
        }

        // Primitive / string / enum element — recurse.
        return ConvertJsonValue(element, elementType);
    }

    private static string FriendlyTypeName(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var def = t.GetGenericTypeDefinition().Name;
        var tick = def.IndexOf('`');
        if (tick > 0) def = def.Substring(0, tick);
        var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyTypeName));
        return $"{def}<{args}>";
    }

    // ═══════════════════════════════════════════════════════════════
    // set_flags / clear_flags
    // ═══════════════════════════════════════════════════════════════

    private static int ApplyFlags(
        IMajorRecord record, List<string>? setFlags, List<string>? clearFlags)
    {
        int changed = 0;

        if (record is Npc npc)
        {
            foreach (var flag in setFlags ?? Enumerable.Empty<string>())
            {
                if (Enum.TryParse<NpcConfiguration.Flag>(flag, true, out var f))
                { npc.Configuration.Flags |= f; changed++; }
            }
            foreach (var flag in clearFlags ?? Enumerable.Empty<string>())
            {
                if (Enum.TryParse<NpcConfiguration.Flag>(flag, true, out var f))
                { npc.Configuration.Flags &= ~f; changed++; }
            }
        }

        // Record-level flags via reflection — works on any record type
        // Handles: NonPlayable, Deleted, InitiallyDisabled, etc.
        if (record is SkyrimMajorRecord smr)
        {
            foreach (var flag in setFlags ?? Enumerable.Empty<string>())
            {
                if (Enum.TryParse<SkyrimMajorRecord.SkyrimMajorRecordFlag>(
                        flag, true, out var f))
                { smr.SkyrimMajorRecordFlags |= f; changed++; }
            }
            foreach (var flag in clearFlags ?? Enumerable.Empty<string>())
            {
                if (Enum.TryParse<SkyrimMajorRecord.SkyrimMajorRecordFlag>(
                        flag, true, out var f))
                { smr.SkyrimMajorRecordFlags &= ~f; changed++; }
            }
        }

        return changed;
    }

    // ═══════════════════════════════════════════════════════════════
    // Conditions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// v2.9.1 — friendly-name → property-name map for the
    /// <c>condition_target</c> operator parameter. Phase 1 schema probe
    /// confirmed Quest is the SOLE multi-condition record type in
    /// Mutagen.Bethesda.Skyrim 0.53.1 (zero other carriers expose ≥2
    /// <c>*Conditions</c> properties). Both targeted properties are
    /// <c>Noggog.ExtendedList&lt;Condition&gt;</c> on the writer side. Keyed
    /// case-insensitive per Phase 0 lock. See PHASE_1_HANDOFF.md.
    /// </summary>
    private static readonly Dictionary<string, string> ConditionTargetMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dialog"] = "DialogConditions",
            ["event"] = "EventConditions",
        };

    /// <summary>
    /// v2.9.1 — resolves which <c>*Conditions</c> property on the record to
    /// write to / remove from for <c>add_conditions</c> / <c>remove_conditions</c>.
    /// Defaults to the legacy <c>"Conditions"</c> lookup when
    /// <c>conditionTarget</c> is null (back-compat preserved for all v2.9.0
    /// carriers: MGEF / PERK / PACK / IDLE / INFO etc.).
    ///
    /// Three new error paths land in v2.9.1 per PLAN § C:
    /// <list type="number">
    ///   <item>QUST without <c>condition_target</c> — pre-flight throw with
    ///   the explicit "available targets" message.</item>
    ///   <item>Unknown <c>condition_target</c> value — string-enum throw
    ///   listing the valid set.</item>
    ///   <item>Single-Conditions carrier with <c>condition_target</c>
    ///   supplied — Q4 reject throw. Distinguished from the
    ///   no-condition-list case (e.g. ARMO) by probing for the legacy
    ///   <c>"Conditions"</c> property: present → Q4 reject; absent →
    ///   return null so Tier D's uniform <c>unmatched_operators</c> shape
    ///   fires (matches MATRIX 1.D.04 vs 1.D.05 separation).</item>
    /// </list>
    /// </summary>
    private static PropertyInfo? ResolveConditionListProperty(
        IMajorRecord record, string? conditionTarget, string operatorName)
    {
        // Pre-flight 1: QUST requires condition_target. § C #2.
        if (record is IQuestGetter && conditionTarget == null)
        {
            throw new ArgumentException(
                $"Record type Quest requires a condition_target parameter on {operatorName}. " +
                "Available targets: 'dialog' (DialogConditions) | 'event' (EventConditions). " +
                "Quest records carry two condition lists rather than a single Conditions list — " +
                "see KNOWN_ISSUES.md § Patching write surface.");
        }

        if (conditionTarget == null)
        {
            // Legacy path — single-Conditions carriers (MGEF / PERK / PACK / IDLE
            // / INFO etc.). Null return = unsupported carrier (e.g. ARMO) →
            // Tier D unmatched_operators shape.
            return record.GetType().GetProperty("Conditions",
                BindingFlags.Public | BindingFlags.Instance);
        }

        // Pre-flight 2: bad target value. § C #3.
        if (!ConditionTargetMap.TryGetValue(conditionTarget, out var propName))
        {
            throw new ArgumentException(
                $"Unknown condition_target: '{conditionTarget}'. " +
                "Valid values: 'dialog' | 'event'.");
        }

        var resolved = record.GetType().GetProperty(propName,
            BindingFlags.Public | BindingFlags.Instance);
        if (resolved != null) return resolved;

        // Q4 reject path: condition_target supplied but the targeted property
        // doesn't exist on this record type. Distinguish PERK-style (HAS a
        // single Conditions list, just not the targeted alias) from ARMO-style
        // (no condition list at all → Tier D fallthrough).
        var legacyProp = record.GetType().GetProperty("Conditions",
            BindingFlags.Public | BindingFlags.Instance);
        if (legacyProp != null)
        {
            throw new ArgumentException(
                $"Record type {record.GetType().Name} uses a single Conditions list — " +
                $"omit condition_target. (condition_target='{conditionTarget}' resolved to " +
                $"{propName}, which this record does not expose.)");
        }

        // No condition list at all — fall through to Tier D's uniform shape.
        return null;
    }

    /// <summary>
    /// v2.8.0 — return type changed from <c>int</c> to <c>int?</c>: null
    /// signals "this record type has no Conditions property", letting the
    /// caller skip writing the <c>conditions_added</c> mods key so Tier D's
    /// coverage check at <c>Apply</c>'s tail surfaces a uniform
    /// <c>unmatched_operators: ["add_conditions"]</c> error shape — same as
    /// every other unsupported (operator, record-type) combo. Pre-v2.8.0
    /// the helper threw <c>"Record type X does not support conditions"</c>
    /// directly, producing a structurally different error response.
    /// v2.9.1 — adds <c>conditionTarget</c> parameter routing to alternate
    /// <c>*Conditions</c> properties (e.g. QUST.DialogConditions /
    /// EventConditions) via <see cref="ResolveConditionListProperty"/>.
    /// </summary>
    private static int? ApplyAddConditions(
        IMajorRecord record, List<ConditionEntry> conditions, string? conditionTarget)
    {
        var condProp = ResolveConditionListProperty(record, conditionTarget, "add_conditions");
        if (condProp == null) return null; // unsupported — let Tier D fire

        var condList = condProp.GetValue(record) as ExtendedList<Condition>;
        if (condList == null)
        {
            condList = new ExtendedList<Condition>();
            condProp.SetValue(record, condList);
        }

        int added = 0;
        foreach (var ce in conditions)
        {
            condList.Add(BuildCondition(ce));
            added++;
        }
        return added;
    }

    /// <summary>
    /// v2.8.0 — Single-source-of-truth Condition factory. Builds one Mutagen
    /// <c>Condition</c> (concrete <c>ConditionFloat</c> or <c>ConditionGlobal</c>)
    /// from the friendly <c>ConditionEntry</c> DSL: <c>{function, operator, value,
    /// global, run_on, or_flag}</c>. Used by <c>ApplyAddConditions</c> AND by
    /// <c>BuildConditionFromJson</c> (Branch A's <c>typeof(Condition)</c>
    /// special case for nested-Conditions arrays inside an Effect entry).
    ///
    /// Probe-justified single-source-of-truth: <c>Activator.CreateInstance(typeof(Condition))</c>
    /// throws (Condition is abstract — see EFFECTS_AUDIT.md Constructibility section),
    /// so the generic Branch A Activator path can't construct a Condition.
    /// </summary>
    private static Condition BuildCondition(ConditionEntry ce)
    {
        var typeName = $"Mutagen.Bethesda.Skyrim.{ce.Function}ConditionData";
        var condDataType = typeof(ISkyrimMod).Assembly.GetType(typeName);
        if (condDataType == null)
            throw new ArgumentException($"Unknown condition function: '{ce.Function}'");

        var condData = System.Activator.CreateInstance(condDataType) as ConditionData;
        if (condData == null)
            throw new InvalidOperationException($"Could not create {typeName}");

        // Set RunOnType
        if (Enum.TryParse<Condition.RunOnType>(ce.RunOn, true, out var runOn))
            condData.RunOnType = runOn;

        // v2.8.0 — optional ActorValue argument. ConditionData subclasses
        // for GetActorValue / GetBaseActorValue / GetActorValuePercent etc.
        // expose an `ActorValue` enum property on the data record. Without
        // this routing, the enum defaults to index 0 (Aggression), which is
        // semantically meaningless for typical "GetActorValue Health >= 50"
        // checks. Scope-locked to ActorValue per Phase 4 plan.
        // v2.9 — retained as back-compat syntactic sugar for
        // `parameters: {ActorValue: "..."}`; the new generic dispatcher
        // (RouteParameterSlot) handles the equivalent slot through reflection.
        // Supplying both forms for the same condition is an unambiguous-DSL
        // violation — see the v2.9 dispatcher block below.
        if (!string.IsNullOrEmpty(ce.ActorValue))
        {
            var avProp = condDataType.GetProperty("ActorValue",
                BindingFlags.Public | BindingFlags.Instance);
            if (avProp == null)
                throw new ArgumentException(
                    $"Condition function '{ce.Function}' has no ActorValue parameter slot — drop the actor_value field, or use a function that takes one (GetActorValue, GetBaseActorValue, etc.).");
            if (!avProp.PropertyType.IsEnum)
                throw new InvalidOperationException(
                    $"ActorValue property on {typeName} is not an enum (got {avProp.PropertyType.FullName}).");
            if (!Enum.TryParse(avProp.PropertyType, ce.ActorValue, ignoreCase: true, out var avValue))
                throw new ArgumentException(
                    $"Unknown actor_value: '{ce.ActorValue}' — must be a Mutagen ActorValue enum name (e.g. 'Health', 'Magicka', 'Stamina', 'OneHanded', 'Sneak').");
            avProp.SetValue(condData, avValue);
        }

        // v2.9 — generic Condition-function parameter dispatch. Routes each
        // entry of ce.Parameters to the function's ConditionData via
        // reflection. Functions not in KnownParameterizedFunctions surface
        // a clean per-record "not yet wired" error per PLAN § C; functions
        // in the set route per-slot through RouteParameterSlot. The v2.8
        // actor_value handler above stays live as back-compat sugar.
        if (ce.Parameters != null && ce.Parameters.Count > 0)
        {
            // DSL-ambiguity check: actor_value + parameters.ActorValue both
            // supplied = error. Per PLAN § A back-compat contract. Layer 4.dsl.01.
            if (!string.IsNullOrEmpty(ce.ActorValue)
                && ce.Parameters.ContainsKey("ActorValue"))
            {
                throw new ArgumentException(
                    "Both 'actor_value' and 'parameters: {ActorValue: ...}' supplied — choose one. " +
                    "The v2.8 'actor_value' field is back-compat syntactic sugar for the v2.9 " +
                    "'parameters: {ActorValue: ...}' form; supplying both is ambiguous.");
            }

            // Out-of-scope check: function not in v2.9.0 in-scope set.
            if (!KnownParameterizedFunctions.Contains(ce.Function))
            {
                var slotNames = string.Join(", ", ce.Parameters.Keys);
                throw new ArgumentException(
                    $"Condition function '{ce.Function}' has parameter slots ({slotNames}) " +
                    "that v2.9.0 does not yet wire. Authoring this function with parameters today " +
                    "produces a structurally-valid but always-false condition. " +
                    "v2.9.0 in-scope set: see KNOWN_ISSUES.md § Condition-parameter coverage. " +
                    "Please file a Live Reported Bug if you need this function added.");
            }

            // Per-slot dispatch.
            foreach (var kvp in ce.Parameters)
            {
                RouteParameterSlot(condData, condDataType, ce.Function, kvp.Key, kvp.Value);
            }
        }

        Condition condition;
        if (!string.IsNullOrEmpty(ce.Global))
        {
            // ConditionGlobal: compare against a Global variable (e.g., MCM toggles)
            var globalKey = FormIdHelper.Parse(ce.Global);
            var globalLink = globalKey.ToLink<IGlobalGetter>();

            // The function's own Global parameter (CTDA Parameter #1 in xEdit) — Mutagen stores
            // these in FormLinkOrIndex<T> (which supports either a FormKey or an alias index).
            // Without setting this, xEdit reports "NULL reference, expected: GLOB".
            var globalProp = condDataType.GetProperty("Global");
            if (globalProp != null
                && globalProp.PropertyType.IsGenericType
                && globalProp.PropertyType.GetGenericTypeDefinition().Name.StartsWith("IFormLinkOrIndex"))
            {
                var targetType = globalProp.PropertyType.GetGenericArguments()[0];
                var concreteType = typeof(FormLinkOrIndex<>).MakeGenericType(targetType);
                // ctor: FormLinkOrIndex<T>(IFormLinkOrIndexFlagGetter parent, FormKey key)
                var newInstance = System.Activator.CreateInstance(concreteType, new object[] { condData, globalKey });
                globalProp.SetValue(condData, newInstance);
            }

            condition = new ConditionGlobal
            {
                ComparisonValue = globalLink,
                CompareOperator = ParseCompareOperator(ce.Operator),
                Data = condData,
            };
        }
        else if (ce.Value.HasValue)
        {
            // ConditionFloat: compare against a literal value
            condition = new ConditionFloat
            {
                ComparisonValue = ce.Value.Value,
                CompareOperator = ParseCompareOperator(ce.Operator),
                Data = condData,
            };
        }
        else
        {
            throw new ArgumentException(
                $"Condition for '{ce.Function}' must provide either 'value' (float) or 'global' (FormID) — got neither.");
        }

        if (ce.OrFlag)
            condition.Flags |= Condition.Flag.OR;

        return condition;
    }

    /// <summary>
    /// v2.9.0 — frozen set of Condition function names whose parameter slots the
    /// bridge dispatches via <see cref="RouteParameterSlot"/>. Sourced from
    /// CONDITIONS_AUDIT.md (Mutagen 0.53.1 inventory probe).
    /// 199 functions total — closes the max-band Pareto Aaron locked at session
    /// start:
    /// <list type="bullet">
    /// <item>P2A: 113 single-<c>IFormLinkOrIndex&lt;T&gt;</c> + 6 sub-A
    ///   single-<c>IFormLink&lt;T&gt;</c> (GetVATSValue* FormLink family).</item>
    /// <item>P2B: 41 single-Enum (ActorValue family + 38 others — Axis / Sex /
    ///   CastSource / FormType / MiscStatEnum / nested
    ///   GetVATSValueActionConditionData+Action / etc.).</item>
    /// <item>P2C: 28 MultiSlot (incl. GetEventData absorbed via sub-A's
    ///   IFormLink&lt;T&gt; branch — 3-slot mixed Enum + Enum + IFormLink).</item>
    /// <item>P2D: 11 PrimitiveOnly (single- or dual-slot Int32 — alias indices,
    ///   package data accessors, VATS Int32 Value, IsLimbGone). Pure HashSet
    ///   extension: zero new dispatcher code, since the Int32 branch landed in
    ///   P2C for MultiSlot consumers (GetStageDone.Stage and friends).</item>
    /// </list>
    /// NoParam (219) is in-scope-no-op — they accept parameterless invocation as
    /// v2.7.1+ behavior; supplying <c>parameters</c> for a NoParam function fires
    /// the natural slot-name-not-found path or the out-of-scope check.
    /// Sub-B (6 String-typed VariableName/GraphVariable functions) deferred to
    /// v2.9.x — see PLAN.md § Carry-overs entry 7.
    /// </summary>
    private static readonly HashSet<string> KnownParameterizedFunctions = new(StringComparer.Ordinal)
    {
        // ── 113 IFormLinkOrIndex<T> functions (scratch lines 1220–1447) ──
        "CanPayCrimeGold",
        "EPAlchemyEffectHasKeyword",
        "EPMagic_SpellHasKeyword",
        "EPModSkillUsage_AdvanceObjectHasKeyword",
        "EPTemperingItemHasKeyword",
        "Exists",
        "GetCombatTargetHasKeyword",
        "GetCrimeGold",
        "GetCrimeGoldNonviolent",
        "GetCrimeGoldViolent",
        "GetDeadCount",
        "GetDetected",
        "GetDistance",
        "GetEquipped",
        "GetEquippedShout",
        "GetFactionRank",
        "GetFactionRelation",
        "GetGlobalValue",
        "GetHeadingAngle",
        "GetInCell",
        "GetInContainer",
        "GetInCurrentLoc",
        "GetInCurrentLocFormList",
        "GetInFaction",
        "GetInSameCell",
        "GetInSharedCrimeFaction",
        "GetInWorldspace",
        "GetInZone",
        "GetInfamy",
        "GetInfamyNonViolent",
        "GetInfamyViolent",
        "GetIsClass",
        "GetIsClassDefault",
        "GetIsCrimeFaction",
        "GetIsCurrentPackage",
        "GetIsCurrentWeather",
        "GetIsEditorLocation",
        "GetIsID",
        "GetIsRace",
        "GetIsReference",
        "GetIsUsedItem",
        "GetIsUsedItemEquipType",
        "GetIsVoiceType",
        "GetItemCount",
        "GetKeywordDataForCurrentLocation",
        "GetKeywordItemCount",
        "GetLineOfSight",
        "GetLocationCleared",
        "GetPCEnemyofFaction",
        "GetPCExpelled",
        "GetPCFactionAttack",
        "GetPCFactionMurder",
        "GetPCInFaction",
        "GetPCIsClass",
        "GetPCIsRace",
        "GetQuestCompleted",
        "GetQuestRunning",
        "GetRelationshipRank",
        "GetShouldAttack",
        "GetShouldHelp",
        "GetSpellUsageNum",
        "GetStage",
        "GetStolenItemValue",
        "GetStolenItemValueNoCrime",
        "GetTalkedToPCParam",
        "GetTargetHeight",
        "GetThreatRatio",
        "GetVATSBackAreaFree",
        "GetVATSFrontAreaFree",
        "GetVATSFrontTargetVisible",
        "GetVATSLeftAreaFree",
        "GetVATSLeftTargetVisible",
        "GetVATSRightAreaFree",
        "GetVATSRightTargetVisible",
        "HasAssociationTypeAny",
        "HasFamilyRelationship",
        "HasKeyword",
        "HasLinkedRef",
        "HasMagicEffect",
        "HasMagicEffectKeyword",
        "HasParentRelationship",
        "HasPerk",
        "HasRefType",
        "HasShout",
        "HasSpell",
        "IsAttackType",
        "IsCombatTarget",
        "IsCurrentFurnitureObj",
        "IsCurrentFurnitureRef",
        "IsHostileToActor",
        "IsInList",
        "IsKiller",
        "IsKillerObject",
        "IsLastIdlePlayed",
        "IsLocationLoaded",
        "IsOwner",
        "IsPlayerGrabbedRef",
        "IsPlayerInRegion",
        "IsScenePlaying",
        "IsSpellTarget",
        "IsTalkingActivatorActor",
        "IsWarningAbout",
        "IsWeaponInList",
        "LocationHasKeyword",
        "LocationHasRefType",
        "PlayerKnows",
        "SameFaction",
        "SameRace",
        "SameSex",
        "ShouldAttackKill",
        "SpellHasCastingPerk",
        "WornApparelHasKeywordCount",
        "WornHasKeyword",

        // ── 6 sub-A IFormLink<T> functions (CONDITIONS_AUDIT.md § Sub-A) ──
        "GetVATSValueCriticalEffect",
        "GetVATSValueCriticalEffectOrList",
        "GetVATSValueTarget",
        "GetVATSValueTargetOrList",
        "GetVATSValueWeapon",
        "GetVATSValueWeaponOrList",

        // ── 41 Enum functions (P2B; scratch lines 1136–1219) ──
        // Slot signatures: ActorValue / Axis / CastSource / Alignment / FormType /
        // MaleFemaleGender / MiscStatEnum / AdvanceAction / nested
        // GetVATSValueActionConditionData+Action / CastType / TargetType /
        // Projectile+TypeEnum / WeaponAnimationType / FurnitureAnimType /
        // FurnitureEntryType / CriticalStage / PlayerAction / WardState — 18
        // distinct enum types across 41 functions. All route uniformly via the
        // Enum branch in RouteParameterSlot (Enum.Parse with ignoreCase: true).
        // GetActorValue / GetBaseActorValue / GetActorValuePercent are the
        // v2.8.0 carryovers — back-compat sugar via the actor_value field
        // remains live; supplying both forms surfaces the unambiguous-DSL
        // error per BuildCondition's check above.
        "EPMagic_IsAdvanceSkill",
        "EPMagic_SpellHasSkill",
        "EPModSkillUsage_IsAdvanceAction",
        "GetActorValue",
        "GetActorValuePercent",
        "GetAngle",
        "GetBaseActorValue",
        "GetCurrentCastingType",
        "GetCurrentDeliveryType",
        "GetEquippedItemType",
        "GetIsAlignment",
        "GetIsObjectType",
        "GetIsSex",
        "GetIsUsedItemType",
        "GetPCIsSex",
        "GetPCMiscStat",
        "GetPathingCurrentSpeedAngle",
        "GetPathingTargetAngleOffset",
        "GetPathingTargetOffset",
        "GetPathingTargetSpeedAngle",
        "GetPermanentActorValue",
        "GetPos",
        "GetReplacedItemType",
        "GetStartingAngle",
        "GetStartingPos",
        "GetVATSValueAction",
        "GetVATSValueCastingType",
        "GetVATSValueDeliveryType",
        "GetVATSValueProjectileType",
        "GetVATSValueTargetPart",
        "GetVATSValueWeaponType",
        "GetVelocity",
        "HasBoundWeaponEquipped",
        "HasEquippedSpell",
        "IsFurnitureAnimType",
        "IsFurnitureEntryType",
        "IsInCriticalStage",
        "IsInFurnitureState",
        "IsPlayerActionActive",
        "IsWardState",
        "IsWeaponSkillType",

        // ── 28 MultiSlot functions (P2C; scratch lines 1448–1531; incl. GetEventData
        // absorbed under sub-A's IFormLink<T> branch per CONDITIONS_AUDIT.md
        // § GetEventData re-triage decision) ──
        // Slot signatures span FLI + IFormLink + Enum + Int32 + Single across the
        // 28 functions; each multi-slot function dispatches per-slot through
        // BuildCondition's foreach over ce.Parameters (lines ~1681-1685). The
        // dispatcher itself doesn't distinguish "single-slot" from "multi-slot"
        // — the foreach handles both uniformly. GetStageDone (Quest FLI + Stage
        // Int32) is the canonical 2-slot probe; GetEventData (EventFunction nested
        // enum + EventMember nested enum + Record IFormLink<ISkyrimMajorRecordGetter>)
        // is the 3-slot mixed-shape canary. Unknown is Mutagen's generic-fallback
        // ConditionData (Function: Condition+Function nested enum + ParameterOne/
        // ParameterTwo: Int32×2) for unknown CTDA function codes; included for
        // completeness even though typical patchers wouldn't author it directly.
        "GetCrime",
        "GetEventData",
        "GetFactionCombatReaction",
        "GetFactionRankDifference",
        "GetInCellParam",
        "GetKeywordDataForAlias",
        "GetKeywordDataForLocation",
        "GetLocAliasRefTypeAliveCount",
        "GetLocAliasRefTypeDeadCount",
        "GetRefTypeAliveCount",
        "GetRefTypeDeadCount",
        "GetRelativeAngle",
        "GetStageDone",
        "GetWithinDistance",
        "HasAssociationType",
        "HasSameEditorLocAsRef",
        "HasSameEditorLocAsRefAlias",
        "IsCellOwner",
        "IsCloserToAThanB",
        "IsCurrentSpell",
        "IsInSameCurrentLocAsRef",
        "IsInSameCurrentLocAsRefAlias",
        "IsLinkedTo",
        "IsSceneActionComplete",
        "LocAliasHasKeyword",
        "LocAliasIsLocation",
        "SpellHasKeyword",
        "Unknown",

        // ── 11 PrimitiveOnly functions (P2D; scratch lines 1532–1554; all Int32
        // slots — branches landed in P2C) ──
        // Pure HashSet extension closing the max-band Pareto: zero new dispatcher
        // code, the Int32 branch in RouteParameterSlot already routes every slot
        // these functions carry. Alias-index lookups (GetInCurrentLocAlias /
        // GetIsAliasRef / GetIsEditorLocAlias / GetLocationAliasCleared /
        // IsLocAliasLoaded — quest-alias-index gating used by dialog/quest
        // patchers); package data accessors (GetNumericPackageData /
        // GetWithinPackageLocation / IsNullPackageData — Package-byte-index lookup);
        // GetVATSValueUnknown is the genuinely-Int32-typed VATS variant (Sub-A's
        // six GetVATSValue* covered the IFormLink&lt;T&gt; Value-slot variants —
        // CriticalEffect / Target / Weapon ± OrList — distinct from this Int32
        // Value+ValueType pair). GetPlayerControlsDisabled uses dual Int32 slots
        // (PlayerControlsParameterOne + PlayerControlsParameterTwo) which both
        // route through the existing per-slot foreach.
        "GetInCurrentLocAlias",
        "GetIsAliasRef",
        "GetIsEditorLocAlias",
        "GetLocationAliasCleared",
        "GetNumericPackageData",
        "GetPlayerControlsDisabled",
        "GetVATSValueUnknown",
        "GetWithinPackageLocation",
        "IsLimbGone",
        "IsLocAliasLoaded",
        "IsNullPackageData",
    };

    /// <summary>
    /// v2.9.0 — generic Condition-function parameter dispatcher. Routes one
    /// (slotName, jsonValue) entry from <see cref="ConditionEntry.Parameters"/>
    /// to the corresponding reflection property on a <c>{Function}ConditionData</c>
    /// instance. **Phase 2 feature-complete after P2D**: 5 of 6 PLAN.md § A
    /// branches landed across 199 dispatcher-wired functions (Boolean is the
    /// single design-vs-implementation gap, deferred to first v2.9.x consumer
    /// trigger).
    /// <list type="bullet">
    /// <item>P2A: <c>IFormLinkOrIndex&lt;T&gt;</c> (existing Global-handler pattern —
    ///   parent + FormKey ctor) and <c>IFormLink&lt;T&gt;</c> (sub-A absorption —
    ///   single FormKey ctor). 119 functions wired.</item>
    /// <item>P2B: <c>System.Enum</c> via <c>Enum.Parse(propType, value, ignoreCase: true)</c>
    ///   — generalizes v2.8.0's <c>actor_value</c> handler. Numeric-vs-string
    ///   posture: error if <c>jsonValue.ValueKind != String</c> (strings are the
    ///   documented form; matches the back-compat <c>actor_value</c> contract).
    ///   41 functions wired.</item>
    /// <item>P2C: <c>Int32</c> and <c>Single</c> primitive branches via
    ///   <c>JsonElement.GetInt32()</c> / <c>GetSingle()</c>. Required by 10 native
    ///   MultiSlot functions whose secondary slots are Int32 (Stage / *AliasIndex
    ///   / SceneActionIndex / Unknown's ParameterOne+ParameterTwo) and by
    ///   GetWithinDistance's Distance slot (Single — only Single-bearing function
    ///   in the v2.9.0 in-scope set). Numeric-vs-string posture: error if
    ///   <c>jsonValue.ValueKind != Number</c>. 28 functions wired.</item>
    /// <item>P2D: 11 PrimitiveOnly functions wired through the existing P2C Int32
    ///   branch (alias indices, package data accessors, VATS Int32 Value+ValueType,
    ///   IsLimbGone). Zero new dispatcher code — pure
    ///   <see cref="KnownParameterizedFunctions"/> extension closing the max-band
    ///   Pareto.</item>
    /// </list>
    /// Multi-slot composition is handled by <see cref="BuildCondition"/>'s foreach
    /// over <c>ce.Parameters</c> (lines ~1681-1685) — each slot routes independently
    /// through whichever branch matches its prop type, so P2C's 28 MultiSlot and
    /// P2D's 11 PrimitiveOnly functions wire purely via
    /// <see cref="KnownParameterizedFunctions"/> additions. No new dispatcher
    /// abstraction across either phase.
    /// Boolean primitive intentionally NOT landed: zero v2.9.0 in-scope functions
    /// need it (verified across 199 dispatcher-wired functions). PLAN.md § A names
    /// Boolean as one of the design's six branches — first v2.9.x consumer trigger
    /// lands the branch + cell + name. See KNOWN_ISSUES.md § Condition-parameter
    /// coverage.
    /// Footgun-guard: rejects any slot name containing <c>"Unused"</c> per
    /// CONDITIONS_AUDIT.md § Architectural surprises §3 (CTDA padding pattern).
    /// 1436 *Unused*Parameter* slots exist across 424 ConditionData types as
    /// padding mirroring CTDA's 4-parameter binary format; reflection lookup
    /// would technically succeed on them, so a typo'd intentional slot name
    /// could land on padding silently. The guard surfaces this as a clean
    /// error.
    /// </summary>
    private static void RouteParameterSlot(
        ConditionData condData,
        Type condDataType,
        string functionName,
        string slotName,
        JsonElement jsonValue)
    {
        // Footgun-guard — reject CTDA padding slot names. Per audit §3.
        if (slotName.Contains("Unused", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Parameter slot name '{slotName}' on function '{functionName}' looks like " +
                "a CTDA padding slot (the *Unused*Parameter* pattern Mutagen mirrors from " +
                "CTDA's 4-parameter binary format). These are never set in practice; this is " +
                "almost certainly a typo of the intended slot name. " +
                "See CONDITIONS_AUDIT.md § Architectural surprises §3.");
        }

        var prop = condDataType.GetProperty(slotName,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        {
            throw new ArgumentException(
                $"Function '{functionName}' has no parameter slot named '{slotName}' " +
                $"on its Mutagen ConditionData ({condDataType.Name}). " +
                "Slot names are case-sensitive Mutagen reflection property names — " +
                "see CONDITIONS_AUDIT.md for the v2.9.0 in-scope per-function slot signatures.");
        }

        var propType = prop.PropertyType;

        // IFormLinkOrIndex<T> branch — existing v2.8 Global-handler pattern.
        // FormLinkOrIndex<T> ctor: (parent: ConditionData, key: FormKey).
        if (propType.IsGenericType
            && propType.GetGenericTypeDefinition().Name.StartsWith("IFormLinkOrIndex"))
        {
            if (jsonValue.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects a " +
                    $"FormID string (e.g. \"Skyrim.esm:0001A696\"); got JSON {jsonValue.ValueKind}.");
            }
            var formKey = FormIdHelper.Parse(jsonValue.GetString()!);
            var targetType = propType.GetGenericArguments()[0];
            var concreteType = typeof(FormLinkOrIndex<>).MakeGenericType(targetType);
            var newInstance = System.Activator.CreateInstance(concreteType,
                new object[] { condData, formKey });
            prop.SetValue(condData, newInstance);
            return;
        }

        // IFormLink<T> branch (NOT IFormLinkOrIndex) — sub-A absorption.
        // FormLink<T> ctor: (key: FormKey). Distinct from IFormLinkOrIndex<T>'s
        // parent + FormKey ctor; FormLink<T> doesn't carry a parent reference.
        if (propType.IsGenericType
            && propType.GetGenericTypeDefinition() == typeof(IFormLink<>))
        {
            if (jsonValue.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects a " +
                    $"FormID string (e.g. \"Skyrim.esm:0001A696\"); got JSON {jsonValue.ValueKind}.");
            }
            var formKey = FormIdHelper.Parse(jsonValue.GetString()!);
            var targetType = propType.GetGenericArguments()[0];
            var concreteType = typeof(FormLink<>).MakeGenericType(targetType);
            var newInstance = System.Activator.CreateInstance(concreteType, formKey);
            prop.SetValue(condData, newInstance);
            return;
        }

        // Enum branch (2B) — generalizes v2.8.0's actor_value handler at
        // BuildCondition lines ~1634-1648. Reflection-property type drives the
        // Enum.Parse target, so any *ConditionData enum slot routes uniformly
        // (ActorValue, Sex, Axis, CastSource, FormType, etc. — 41 functions in
        // 2B's KnownParameterizedFunctions extension). Strings only — numeric
        // input is a DSL violation per kickoff §3 lock; strings match the
        // back-compat actor_value contract.
        if (propType.IsEnum)
        {
            if (jsonValue.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects an " +
                    $"enum name string (e.g. \"Health\" for ActorValue); got JSON {jsonValue.ValueKind}. " +
                    "Numeric enum-index input is not accepted — supply the member name.");
            }
            object parsed;
            try
            {
                parsed = Enum.Parse(propType, jsonValue.GetString()!, ignoreCase: true);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(
                    $"Unknown enum name '{jsonValue.GetString()}' for parameter slot " +
                    $"'{slotName}' on function '{functionName}' (enum type {propType.FullName}). " +
                    "Slot names accept any defined member of the enum (case-insensitive).");
            }
            prop.SetValue(condData, parsed);
            return;
        }

        // Int32 branch (2C) — direct numeric conversion. Required by 10 native
        // MultiSlot functions whose secondary slots are Int32 (Stage / *AliasIndex
        // / SceneActionIndex / Unknown's ParameterOne+ParameterTwo); pre-positions
        // for 2D's 11 PrimitiveOnly functions which all use Int32. JsonElement.GetInt32()
        // throws on non-integral numerics or overflow (1.5 → InvalidOperationException;
        // 2_147_483_648 → OverflowException) — those surface here as clean per-record
        // errors rather than silent truncation.
        if (propType == typeof(int))
        {
            if (jsonValue.ValueKind != JsonValueKind.Number)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects an " +
                    $"Int32 (e.g. 50); got JSON {jsonValue.ValueKind}.");
            }
            int intValue;
            try { intValue = jsonValue.GetInt32(); }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects an " +
                    $"Int32; the JSON number isn't a valid Int32 ({ex.Message}).");
            }
            prop.SetValue(condData, intValue);
            return;
        }

        // Single branch (2C) — direct float conversion. Required by exactly one
        // v2.9.0 in-scope function: GetWithinDistance.Distance. Landed alongside
        // Int32 because Aaron's "ship the full routable surface" posture covers
        // it as a one-line drop-in even though only one function exercises the
        // path; defers SKIP-shuffling if a Single-bearing function ever appears.
        if (propType == typeof(float))
        {
            if (jsonValue.ValueKind != JsonValueKind.Number)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects a " +
                    $"Single (e.g. 1024.0); got JSON {jsonValue.ValueKind}.");
            }
            float floatValue;
            try { floatValue = jsonValue.GetSingle(); }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Parameter slot '{slotName}' on function '{functionName}' expects a " +
                    $"Single; conversion failed ({ex.Message}).");
            }
            prop.SetValue(condData, floatValue);
            return;
        }

        // Other shapes (Boolean + any future Mutagen schema additions) — out of
        // scope for v2.9.0. PLAN.md § A names Boolean as one of the design's six
        // branches; zero v2.9.0 in-scope functions need it (verified across 199
        // dispatcher-wired functions). Reaching here means either (a) a Boolean-
        // bearing function landed in KnownParameterizedFunctions (caller bug —
        // should re-trigger the branch decision via PLAN amend); or (b) Mutagen
        // 0.54+ added a new slot type the dispatcher doesn't route. Surface a
        // clean error rather than silent default.
        throw new InvalidOperationException(
            $"Parameter slot '{slotName}' on function '{functionName}' has type " +
            $"'{propType.FullName}' which v2.9.0 does not route. " +
            "v2.9.0 covers IFormLinkOrIndex<T> + IFormLink<T> + System.Enum + Int32 + Single. " +
            "See KNOWN_ISSUES.md § Condition-parameter coverage.");
    }

    /// <summary>
    /// v2.8.0 — JsonElement entry-point for <see cref="BuildCondition"/>. Used by
    /// Branch A in <see cref="ConvertJsonElementToListItem"/> when the array
    /// element type is <c>typeof(Condition)</c> — keeps the friendly
    /// <c>{function, operator, value, ...}</c> DSL working inside nested
    /// <c>Effect.Conditions</c> arrays under <c>set_fields: {Effects: [...]}</c>.
    /// </summary>
    private static Condition BuildConditionFromJson(JsonElement entryJson)
    {
        if (entryJson.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Condition entry must be a JSON object, got {entryJson.ValueKind}.");
        var ce = entryJson.Deserialize<ConditionEntry>()
            ?? throw new ArgumentException("Failed to deserialize JSON object as ConditionEntry.");
        if (string.IsNullOrEmpty(ce.Function))
            throw new ArgumentException("Condition entry requires a 'function' field.");
        return BuildCondition(ce);
    }

    /// <summary>
    /// v2.9.3 — JsonElement entry-point for constructing concrete APerkEffect
    /// subclasses. Used by Branch A in <see cref="ConvertJsonElementToListItem"/>
    /// when the array element type is <c>typeof(APerkEffect)</c>.
    ///
    /// Discriminator: explicit "type" field naming the concrete Mutagen leaf class
    /// per Phase 0 Q1+Q5 lock (e.g. "PerkEntryPointModifyValue", "PerkAbilityEffect",
    /// "PerkQuestEffect"). Reflection-resolves Mutagen.Bethesda.Skyrim.{TypeName},
    /// rejects abstract types + non-APerkEffect-assignable types, Activator-creates
    /// the concrete leaf, then walks each non-discriminator JSON member through
    /// <see cref="SetPropertyByPath"/> — which recurses into v2.8.0's Branch A
    /// (for nested Conditions: ExtendedList&lt;PerkCondition&gt; and the inner
    /// PerkCondition.Conditions: ExtendedList&lt;Condition&gt;) and v2.9.0's
    /// per-function parameter dispatcher untouched.
    ///
    /// Mirrors v2.8.0's BuildConditionFromJson extracted-from-ApplyAddConditions
    /// pattern. See APERK_EFFECTS_AUDIT.md § Per-subclass property surface for
    /// the 12 concrete leaves and their property types.
    /// </summary>
    private static APerkEffect BuildPerkEffectFromJson(JsonElement entryJson)
    {
        if (entryJson.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"PerkEffect entry must be a JSON object, got {entryJson.ValueKind}.");

        if (!entryJson.TryGetProperty("type", out var typeElem) || typeElem.ValueKind != JsonValueKind.String)
            throw new ArgumentException(
                "PerkEffect entry requires a 'type' field naming the concrete subclass " +
                "(e.g. \"PerkEntryPointModifyValue\", \"PerkAbilityEffect\", \"PerkQuestEffect\"). " +
                "Valid values: PerkAbilityEffect, PerkEntryPointAbsoluteValue, " +
                "PerkEntryPointAddActivateChoice, PerkEntryPointAddLeveledItem, " +
                "PerkEntryPointAddRangeToValue, PerkEntryPointModifyActorValue, " +
                "PerkEntryPointModifyValue, PerkEntryPointModifyValues, " +
                "PerkEntryPointSelectSpell, PerkEntryPointSelectText, " +
                "PerkEntryPointSetText, PerkQuestEffect.");

        var typeName = typeElem.GetString()!;
        var fullTypeName = $"Mutagen.Bethesda.Skyrim.{typeName}";
        var resolved = typeof(ISkyrimMod).Assembly.GetType(fullTypeName);
        if (resolved == null)
            throw new ArgumentException(
                $"PerkEffect type '{typeName}' not found in Mutagen.Bethesda.Skyrim namespace. " +
                "Valid values: PerkAbilityEffect, PerkEntryPointAbsoluteValue, " +
                "PerkEntryPointAddActivateChoice, PerkEntryPointAddLeveledItem, " +
                "PerkEntryPointAddRangeToValue, PerkEntryPointModifyActorValue, " +
                "PerkEntryPointModifyValue, PerkEntryPointModifyValues, " +
                "PerkEntryPointSelectSpell, PerkEntryPointSelectText, " +
                "PerkEntryPointSetText, PerkQuestEffect.");
        if (resolved.IsAbstract)
            throw new ArgumentException(
                $"PerkEffect type '{typeName}' is abstract — must specify a concrete leaf subclass. " +
                "Valid values: PerkAbilityEffect, PerkEntryPointAbsoluteValue, " +
                "PerkEntryPointAddActivateChoice, PerkEntryPointAddLeveledItem, " +
                "PerkEntryPointAddRangeToValue, PerkEntryPointModifyActorValue, " +
                "PerkEntryPointModifyValue, PerkEntryPointModifyValues, " +
                "PerkEntryPointSelectSpell, PerkEntryPointSelectText, " +
                "PerkEntryPointSetText, PerkQuestEffect.");
        if (!typeof(APerkEffect).IsAssignableFrom(resolved))
            throw new ArgumentException(
                $"PerkEffect type '{typeName}' is not a subclass of APerkEffect. " +
                "Valid values: PerkAbilityEffect, PerkEntryPointAbsoluteValue, " +
                "PerkEntryPointAddActivateChoice, PerkEntryPointAddLeveledItem, " +
                "PerkEntryPointAddRangeToValue, PerkEntryPointModifyActorValue, " +
                "PerkEntryPointModifyValue, PerkEntryPointModifyValues, " +
                "PerkEntryPointSelectSpell, PerkEntryPointSelectText, " +
                "PerkEntryPointSetText, PerkQuestEffect.");

        var entry = (APerkEffect)System.Activator.CreateInstance(resolved)!;

        foreach (var member in entryJson.EnumerateObject())
        {
            if (member.Name == "type") continue;  // discriminator consumed above

            // Reject opaque MemorySlice<Byte> blob slot on PerkQuestEffect.Unknown
            // up-front with a clean error — it's not a write-target (per APERK_EFFECTS
            // _AUDIT.md § Per-subclass property surface). Falling through to
            // SetPropertyByPath would surface a confusing ConvertJsonValue error.
            if (resolved == typeof(PerkQuestEffect) && member.Name == "Unknown")
                throw new ArgumentException(
                    "PerkQuestEffect.Unknown is an opaque binary blob (MemorySlice<Byte>) " +
                    "and is not a writable field. Omit it from the Effects entry; " +
                    "the source record's value carries through unchanged.");

            SetPropertyByPath(entry, member.Name, member.Value);
        }
        return entry;
    }

    /// <summary>
    /// v2.8.0 — return type changed from <c>int</c> to <c>int?</c> matching
    /// <see cref="ApplyAddConditions"/>. Null signals "this record type has
    /// no Conditions property"; the caller skips writing the
    /// <c>conditions_removed</c> mods key so Tier D's coverage check fires
    /// with uniform <c>unmatched_operators</c> shape. Pre-v2.7.1 silently
    /// returned 0 (Tier D misread as "supported but empty"); v2.7.1 threw
    /// directly (Tier D bypassed); v2.8.0 returns null (Tier D fires
    /// uniformly across every unsupported combo).
    /// v2.9.1 — adds <c>conditionTarget</c> parameter routing to alternate
    /// <c>*Conditions</c> properties (e.g. QUST.DialogConditions /
    /// EventConditions) via <see cref="ResolveConditionListProperty"/>.
    /// </summary>
    private static int? ApplyRemoveConditions(
        IMajorRecord record, List<ConditionRemoval> removals, string? conditionTarget)
    {
        var condProp = ResolveConditionListProperty(record, conditionTarget, "remove_conditions");
        if (condProp == null) return null; // unsupported — let Tier D fire

        // condList == null is the legitimate "supported but empty" state —
        // 0 to remove is genuinely 0, NOT a coverage failure.
        var condList = condProp.GetValue(record) as ExtendedList<Condition>;
        if (condList == null) return 0;

        int removed = 0;
        foreach (var rem in removals)
        {
            if (rem.Index.HasValue && rem.Index.Value < condList.Count)
            {
                condList.RemoveAt(rem.Index.Value);
                removed++;
            }
            else if (!string.IsNullOrEmpty(rem.Function))
            {
                var targetTypeName = $"Mutagen.Bethesda.Skyrim.{rem.Function}ConditionData";
                var targetType = typeof(ISkyrimMod).Assembly.GetType(targetTypeName);
                if (targetType != null)
                {
                    removed += condList.RemoveAll(c => c.Data.GetType() == targetType);
                }
            }
        }
        return removed;
    }

    private static CompareOperator ParseCompareOperator(string op) => op switch
    {
        "==" or "=" => CompareOperator.EqualTo,
        "!=" or "<>" => CompareOperator.NotEqualTo,
        ">" => CompareOperator.GreaterThan,
        ">=" => CompareOperator.GreaterThanOrEqualTo,
        "<" => CompareOperator.LessThan,
        "<=" => CompareOperator.LessThanOrEqualTo,
        _ => Enum.TryParse<CompareOperator>(op, true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown operator: '{op}'"),
    };

    // ═══════════════════════════════════════════════════════════════
    // VMAD Script Attachment
    // ═══════════════════════════════════════════════════════════════

    private static int ApplyAttachScripts(IMajorRecord record, List<ScriptAttachment> scripts)
    {
        // Access VirtualMachineAdapter via reflection (many record types have it)
        var vmadProp = record.GetType().GetProperty("VirtualMachineAdapter",
            BindingFlags.Public | BindingFlags.Instance);
        if (vmadProp == null)
            throw new InvalidOperationException(
                $"Record type {record.GetType().Name} does not support scripts");

        // v2.8.0 — type the local as the abstract base AVirtualMachineAdapter.
        // race-probe Phase 4 inheritance probe found that Mutagen 0.53.1's three
        // concrete adapters (VirtualMachineAdapter, PerkAdapter, QuestAdapter)
        // are siblings under the abstract AVirtualMachineAdapter, not subclasses
        // of one another. Pre-v2.8.0 typed the local as VirtualMachineAdapter,
        // so the `as` cast returned null on PERK/QUST values (PerkAdapter is
        // not a VirtualMachineAdapter) and the auto-create path then built
        // a base VirtualMachineAdapter that the SetValue cast rejected. Using
        // the common abstract base accommodates all three concretes.
        var vmad = vmadProp.GetValue(record) as AVirtualMachineAdapter;
        if (vmad == null)
        {
            // Construct the matching concrete via the property's declared
            // type — PerkAdapter for PERK, QuestAdapter for QUST,
            // VirtualMachineAdapter for everyone else. EFFECTS_AUDIT.md
            // § Constructibility (Phase 1) confirmed all three are
            // activator-creatable and auto-initialize their Scripts collection.
            vmad = (AVirtualMachineAdapter)System.Activator.CreateInstance(vmadProp.PropertyType)!;
            vmadProp.SetValue(record, vmad);
        }

        int attached = 0;
        foreach (var sa in scripts)
        {
            var script = new ScriptEntry
            {
                Name = sa.Name,
                Flags = ScriptEntry.Flag.Local,
            };

            if (sa.Properties != null)
            {
                foreach (var prop in sa.Properties)
                {
                    ScriptProperty sp = prop.Type.ToLowerInvariant() switch
                    {
                        "object" => new ScriptObjectProperty
                        {
                            Name = prop.Name,
                            Flags = ScriptProperty.Flag.Edited,
                            Object = FormIdHelper.Parse(prop.Value.GetString()!)
                                .ToLink<ISkyrimMajorRecordGetter>(),
                            Alias = -1,
                        },
                        "int" or "int32" => new ScriptIntProperty
                        {
                            Name = prop.Name,
                            Flags = ScriptProperty.Flag.Edited,
                            Data = prop.Value.GetInt32(),
                        },
                        "float" => new ScriptFloatProperty
                        {
                            Name = prop.Name,
                            Flags = ScriptProperty.Flag.Edited,
                            Data = prop.Value.GetSingle(),
                        },
                        "bool" => new ScriptBoolProperty
                        {
                            Name = prop.Name,
                            Flags = ScriptProperty.Flag.Edited,
                            Data = prop.Value.GetBoolean(),
                        },
                        "string" => new ScriptStringProperty
                        {
                            Name = prop.Name,
                            Flags = ScriptProperty.Flag.Edited,
                            Data = prop.Value.GetString() ?? "",
                        },
                        "alias" => BuildAliasProperty(prop),
                        _ => throw new ArgumentException(
                            $"Unknown script property type: '{prop.Type}'"),
                    };
                    script.Properties.Add(sp);
                }
            }

            vmad.Scripts.Add(script);
            attached++;
        }
        return attached;
    }

    /// <summary>
    /// Build a VMAD alias property. In Mutagen this is a ScriptObjectProperty
    /// where Object links to the owning quest and Alias is the alias index.
    /// JSON shape: { "quest": "Plugin:FormID", "alias_id": short }
    /// </summary>
    private static ScriptObjectProperty BuildAliasProperty(ScriptPropertyEntry prop)
    {
        if (prop.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Alias property '{prop.Name}' expects an object value with 'quest' and 'alias_id' fields.");

        if (!prop.Value.TryGetProperty("quest", out var questElem) || questElem.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"Alias property '{prop.Name}' missing 'quest' (string FormID).");

        if (!prop.Value.TryGetProperty("alias_id", out var aliasIdElem) || aliasIdElem.ValueKind != JsonValueKind.Number)
            throw new ArgumentException($"Alias property '{prop.Name}' missing 'alias_id' (integer).");

        var questFormKey = FormIdHelper.Parse(questElem.GetString()!);
        return new ScriptObjectProperty
        {
            Name = prop.Name,
            Flags = ScriptProperty.Flag.Edited,
            Object = questFormKey.ToLink<ISkyrimMajorRecordGetter>(),
            Alias = aliasIdElem.GetInt16(),
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Generic Helpers
    // ═══════════════════════════════════════════════════════════════

    private static int AddFormLinks<T>(
        ExtendedList<IFormLinkGetter<T>> list, List<string> formIds)
        where T : class, IMajorRecordGetter
    {
        int added = 0;
        foreach (var fid in formIds)
        {
            var fk = FormIdHelper.Parse(fid);
            if (!list.Any(l => l.FormKey == fk))
            {
                list.Add(fk.ToLink<T>());
                added++;
            }
        }
        return added;
    }

    private static int RemoveFormLinks<T>(
        ExtendedList<IFormLinkGetter<T>> list, List<string> formIds)
        where T : class, IMajorRecordGetter
    {
        int removed = 0;
        foreach (var fid in formIds)
        {
            var fk = FormIdHelper.Parse(fid);
            removed += list.RemoveAll(l => l.FormKey == fk);
        }
        return removed;
    }

    private static IMajorRecordGetter? FindRecord(ISkyrimModGetter mod, FormKey formKey)
    {
        foreach (var record in mod.EnumerateMajorRecords())
            if (record.FormKey == formKey) return record;
        return null;
    }

    private static string RecordTypeCode(IMajorRecordGetter record) => record switch
    {
        IArmorGetter => "ARMO", IWeaponGetter => "WEAP", INpcGetter => "NPC_",
        IIngestibleGetter => "ALCH", IAmmunitionGetter => "AMMO", IBookGetter => "BOOK",
        IFloraGetter => "FLOR", IIngredientGetter => "INGR", IMiscItemGetter => "MISC",
        IScrollGetter => "SCRL", ILeveledItemGetter => "LVLI", ILeveledNpcGetter => "LVLN",
        ILeveledSpellGetter => "LVSP", ISpellGetter => "SPEL", IPerkGetter => "PERK",
        IOutfitGetter => "OTFT", IFactionGetter => "FACT", IQuestGetter => "QUST",
        IKeywordGetter => "KYWD", IGlobalGetter => "GLOB", IEncounterZoneGetter => "ECZN",
        IFormListGetter => "FLST", IMagicEffectGetter => "MGEF",
        IContainerGetter => "CONT", IPackageGetter => "PACK",
        IFurnitureGetter => "FURN", IActivatorGetter => "ACTI", ILocationGetter => "LCTN",
        // INFO records — the Mutagen class name (DialogResponses) doesn't match
        // Bethesda's 4-char code. Without this case the fallback yields
        // "DIALOGRESPONSES" — symmetry with RecordReader.cs:365 where the same
        // mapping already lives.
        IDialogResponsesGetter => "INFO",
        _ => record.Registration.ClassType.Name.ToUpperInvariant(),
    };

    private static ExtendedList<IFormLinkGetter<IKeywordGetter>>? GetKeywordsList(
        IMajorRecord record) => record switch
    {
        Armor r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Weapon r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Npc r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Ingestible r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Ammunition r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Book r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Flora r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Ingredient r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        MiscItem r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Scroll r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        // v2.7.1 Tier A wire-ups (probe-verified in P0; AUDIT.md add_keywords/remove_keywords).
        Race r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Furniture r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        SkActivator r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Location r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        Spell r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        MagicEffect r => r.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
        _ => null,
    };

    private static IMajorRecord? CopyAsOverride(
        SkyrimMod patchMod, ISkyrimModGetter sourceMod, IMajorRecordGetter sourceRecord)
    {
        // INFO records can't go through the standard 1:1 group->GetOrAddAsOverride
        // pattern: Mutagen 0.53.1 doesn't surface DialogResponses as a top-level
        // group, only as a child list under DialogTopic.Responses. Override path
        // requires resolving the parent topic from sourceMod (linear scan;
        // IDialogResponsesGetter exposes no direct parent getter, only an
        // ambiguous Topic FormLink), overriding the parent topic, then locating
        // the matching response by FormKey inside the override topic's Responses.
        // See dev/plans/v2.9.X_condition_parameters/PHASE_4_INFO_HANDOFF.md.
        if (sourceRecord is IDialogResponsesGetter response)
            return CopyDialogResponseAsOverride(patchMod, sourceMod, response);

        return sourceRecord switch
        {
            // Items & equipment
            IArmorGetter r => patchMod.Armors.GetOrAddAsOverride(r),
            IWeaponGetter r => patchMod.Weapons.GetOrAddAsOverride(r),
            IAmmunitionGetter r => patchMod.Ammunitions.GetOrAddAsOverride(r),
            IBookGetter r => patchMod.Books.GetOrAddAsOverride(r),
            IIngredientGetter r => patchMod.Ingredients.GetOrAddAsOverride(r),
            IIngestibleGetter r => patchMod.Ingestibles.GetOrAddAsOverride(r),
            IMiscItemGetter r => patchMod.MiscItems.GetOrAddAsOverride(r),
            IScrollGetter r => patchMod.Scrolls.GetOrAddAsOverride(r),
            IKeyGetter r => patchMod.Keys.GetOrAddAsOverride(r),
            ISoulGemGetter r => patchMod.SoulGems.GetOrAddAsOverride(r),
            // Actors & AI
            INpcGetter r => patchMod.Npcs.GetOrAddAsOverride(r),
            IFactionGetter r => patchMod.Factions.GetOrAddAsOverride(r),
            IPackageGetter r => patchMod.Packages.GetOrAddAsOverride(r),
            IOutfitGetter r => patchMod.Outfits.GetOrAddAsOverride(r),
            ICombatStyleGetter r => patchMod.CombatStyles.GetOrAddAsOverride(r),
            IClassGetter r => patchMod.Classes.GetOrAddAsOverride(r),
            IRaceGetter r => patchMod.Races.GetOrAddAsOverride(r),
            // Leveled lists
            ILeveledItemGetter r => patchMod.LeveledItems.GetOrAddAsOverride(r),
            ILeveledNpcGetter r => patchMod.LeveledNpcs.GetOrAddAsOverride(r),
            ILeveledSpellGetter r => patchMod.LeveledSpells.GetOrAddAsOverride(r),
            // Magic
            ISpellGetter r => patchMod.Spells.GetOrAddAsOverride(r),
            IPerkGetter r => patchMod.Perks.GetOrAddAsOverride(r),
            IMagicEffectGetter r => patchMod.MagicEffects.GetOrAddAsOverride(r),
            IShoutGetter r => patchMod.Shouts.GetOrAddAsOverride(r),
            IWordOfPowerGetter r => patchMod.WordsOfPower.GetOrAddAsOverride(r),
            IObjectEffectGetter r => patchMod.ObjectEffects.GetOrAddAsOverride(r),
            // World & cells
            IActivatorGetter r => patchMod.Activators.GetOrAddAsOverride(r),
            IContainerGetter r => patchMod.Containers.GetOrAddAsOverride(r),
            IDoorGetter r => patchMod.Doors.GetOrAddAsOverride(r),
            IFloraGetter r => patchMod.Florae.GetOrAddAsOverride(r),
            IFurnitureGetter r => patchMod.Furniture.GetOrAddAsOverride(r),
            ILightGetter r => patchMod.Lights.GetOrAddAsOverride(r),
            ITreeGetter r => patchMod.Trees.GetOrAddAsOverride(r),
            // Quests & dialogue
            IQuestGetter r => patchMod.Quests.GetOrAddAsOverride(r),
            IDialogTopicGetter r => patchMod.DialogTopics.GetOrAddAsOverride(r),
            // Data records
            IKeywordGetter r => patchMod.Keywords.GetOrAddAsOverride(r),
            IGlobalGetter r => patchMod.Globals.GetOrAddAsOverride(r),
            IFormListGetter r => patchMod.FormLists.GetOrAddAsOverride(r),
            IEncounterZoneGetter r => patchMod.EncounterZones.GetOrAddAsOverride(r),
            ILocationGetter r => patchMod.Locations.GetOrAddAsOverride(r),
            IConstructibleObjectGetter r => patchMod.ConstructibleObjects.GetOrAddAsOverride(r),
            // Hazards, explosions, projectiles
            IExplosionGetter r => patchMod.Explosions.GetOrAddAsOverride(r),
            IHazardGetter r => patchMod.Hazards.GetOrAddAsOverride(r),
            IProjectileGetter r => patchMod.Projectiles.GetOrAddAsOverride(r),
            // Misc
            IRelationshipGetter r => patchMod.Relationships.GetOrAddAsOverride(r),
            IMessageGetter r => patchMod.Messages.GetOrAddAsOverride(r),
            IWeatherGetter r => patchMod.Weathers.GetOrAddAsOverride(r),
            IImageSpaceGetter r => patchMod.ImageSpaces.GetOrAddAsOverride(r),
            IImageSpaceAdapterGetter r => patchMod.ImageSpaceAdapters.GetOrAddAsOverride(r),
            IMusicTypeGetter r => patchMod.MusicTypes.GetOrAddAsOverride(r),
            _ => null,
        };
    }

    /// <summary>
    /// INFO override path. Resolves the parent DialogTopic from the source mod
    /// (linear scan), overrides the parent topic, then deep-copies the source
    /// INFO into the override topic's Responses list. Mutagen's
    /// DialogTopic.GetOrAddAsOverride does NOT auto-carry the topic's nested
    /// Responses list — INFO records are independent major records nested under
    /// the topic GRUP only for organization, so the bridge has to explicitly
    /// clone the targeted INFO via DialogResponsesMixIn.DeepCopy. Idempotent on
    /// repeat overrides of the same INFO within one patch session: returns the
    /// existing override response if one is already present.
    /// Failure modes documented at PHASE_4_INFO_HANDOFF.md § Failure modes.
    /// </summary>
    private static IMajorRecord? CopyDialogResponseAsOverride(
        SkyrimMod patchMod, ISkyrimModGetter sourceMod, IDialogResponsesGetter sourceResponse)
    {
        var responseFk = sourceResponse.FormKey;

        // Approach A: linear scan source mod's DialogTopics for the parent topic
        // whose Responses list contains the target INFO. IDialogResponsesGetter
        // exposes no direct parent-topic getter; the .Topic IFormLinkNullable
        // field has ambiguous semantics vs WalkAwayTopic / LinkTo, so we resolve
        // the parent structurally via the GRUP-nesting that the source plugin
        // already encodes.
        var parentTopic = sourceMod.DialogTopics
            .FirstOrDefault(t => t.Responses.Any(r => r.FormKey == responseFk));
        if (parentTopic == null)
            throw new InvalidOperationException(
                $"INFO {responseFk} has no parent DialogTopic in {sourceMod.ModKey} — record may be orphaned.");

        var parentOverride = patchMod.DialogTopics.GetOrAddAsOverride(parentTopic);

        // Idempotency: if a prior override of this INFO already lives in the
        // patch topic's Responses list (e.g. caller invoked add_conditions on
        // the same INFO twice in one patch session), return it instead of
        // appending a second copy.
        var existing = parentOverride.Responses.FirstOrDefault(r => r.FormKey == responseFk);
        if (existing != null)
            return existing;

        // Deep-copy the source INFO (FormKey preserved) and append to the
        // override topic. The bridge then mutates the override response through
        // the standard ApplyModifications path.
        var responseOverride = sourceResponse.DeepCopy();
        parentOverride.Responses.Add(responseOverride);
        return responseOverride;
    }

    /// <summary>
    /// Remove a previously-copied override from the patch mod. Mirrors
    /// <see cref="CopyAsOverride"/>'s group dispatch — when CopyAsOverride
    /// learns a new record type, this switch must too.
    /// Used by <see cref="ProcessOverride"/> to roll back when
    /// <see cref="ApplyModifications"/> fails partway through, so the output
    /// ESP doesn't end up with no-op overrides masquerading as real changes.
    /// </summary>
    private static void TryRemoveOverride(SkyrimMod patchMod, IMajorRecord record)
    {
        var fk = record.FormKey;
        try
        {
            switch (record)
            {
                // Items & equipment
                case IArmorGetter: patchMod.Armors.Remove(fk); break;
                case IWeaponGetter: patchMod.Weapons.Remove(fk); break;
                case IAmmunitionGetter: patchMod.Ammunitions.Remove(fk); break;
                case IBookGetter: patchMod.Books.Remove(fk); break;
                case IIngredientGetter: patchMod.Ingredients.Remove(fk); break;
                case IIngestibleGetter: patchMod.Ingestibles.Remove(fk); break;
                case IMiscItemGetter: patchMod.MiscItems.Remove(fk); break;
                case IScrollGetter: patchMod.Scrolls.Remove(fk); break;
                case IKeyGetter: patchMod.Keys.Remove(fk); break;
                case ISoulGemGetter: patchMod.SoulGems.Remove(fk); break;
                // Actors & AI
                case INpcGetter: patchMod.Npcs.Remove(fk); break;
                case IFactionGetter: patchMod.Factions.Remove(fk); break;
                case IPackageGetter: patchMod.Packages.Remove(fk); break;
                case IOutfitGetter: patchMod.Outfits.Remove(fk); break;
                case ICombatStyleGetter: patchMod.CombatStyles.Remove(fk); break;
                case IClassGetter: patchMod.Classes.Remove(fk); break;
                case IRaceGetter: patchMod.Races.Remove(fk); break;
                // Leveled lists
                case ILeveledItemGetter: patchMod.LeveledItems.Remove(fk); break;
                case ILeveledNpcGetter: patchMod.LeveledNpcs.Remove(fk); break;
                case ILeveledSpellGetter: patchMod.LeveledSpells.Remove(fk); break;
                // Magic
                case ISpellGetter: patchMod.Spells.Remove(fk); break;
                case IPerkGetter: patchMod.Perks.Remove(fk); break;
                case IMagicEffectGetter: patchMod.MagicEffects.Remove(fk); break;
                case IShoutGetter: patchMod.Shouts.Remove(fk); break;
                case IWordOfPowerGetter: patchMod.WordsOfPower.Remove(fk); break;
                case IObjectEffectGetter: patchMod.ObjectEffects.Remove(fk); break;
                // World & cells
                case IActivatorGetter: patchMod.Activators.Remove(fk); break;
                case IContainerGetter: patchMod.Containers.Remove(fk); break;
                case IDoorGetter: patchMod.Doors.Remove(fk); break;
                case IFloraGetter: patchMod.Florae.Remove(fk); break;
                case IFurnitureGetter: patchMod.Furniture.Remove(fk); break;
                case ILightGetter: patchMod.Lights.Remove(fk); break;
                case ITreeGetter: patchMod.Trees.Remove(fk); break;
                // Quests & dialogue
                case IQuestGetter: patchMod.Quests.Remove(fk); break;
                case IDialogTopicGetter: patchMod.DialogTopics.Remove(fk); break;
                case IDialogResponsesGetter:
                    // Option α — response-only rollback. INFO records live nested
                    // inside DialogTopic.Responses, so we scan patchMod.DialogTopics
                    // for the override topic that holds this response and remove
                    // just the response. Leave the parent topic override in place
                    // even if it now carries no real changes — the outer
                    // ApplyModifications exception is what surfaces to the caller,
                    // and a no-op-shaped parent override is strictly less misleading
                    // than silently swallowing the failure (consistent with this
                    // method's overall posture for unknown record types).
                    foreach (var topicOverride in patchMod.DialogTopics)
                    {
                        var match = topicOverride.Responses.FirstOrDefault(r => r.FormKey == fk);
                        if (match != null)
                        {
                            topicOverride.Responses.Remove(match);
                            break;
                        }
                    }
                    break;
                // Data records
                case IKeywordGetter: patchMod.Keywords.Remove(fk); break;
                case IGlobalGetter: patchMod.Globals.Remove(fk); break;
                case IFormListGetter: patchMod.FormLists.Remove(fk); break;
                case IEncounterZoneGetter: patchMod.EncounterZones.Remove(fk); break;
                case ILocationGetter: patchMod.Locations.Remove(fk); break;
                case IConstructibleObjectGetter: patchMod.ConstructibleObjects.Remove(fk); break;
                // Hazards, explosions, projectiles
                case IExplosionGetter: patchMod.Explosions.Remove(fk); break;
                case IHazardGetter: patchMod.Hazards.Remove(fk); break;
                case IProjectileGetter: patchMod.Projectiles.Remove(fk); break;
                // Misc
                case IRelationshipGetter: patchMod.Relationships.Remove(fk); break;
                case IMessageGetter: patchMod.Messages.Remove(fk); break;
                case IWeatherGetter: patchMod.Weathers.Remove(fk); break;
                case IImageSpaceGetter: patchMod.ImageSpaces.Remove(fk); break;
                case IImageSpaceAdapterGetter: patchMod.ImageSpaceAdapters.Remove(fk); break;
                case IMusicTypeGetter: patchMod.MusicTypes.Remove(fk); break;
                // Unknown type — leave it; the outer ApplyModifications exception
                // is what surfaces to the caller, and the no-op override is
                // strictly less misleading than silently swallowing the failure.
            }
        }
        catch
        {
            // Removal failure is non-fatal — the original ApplyModifications
            // exception is what the caller needs to see.
        }
    }

}
