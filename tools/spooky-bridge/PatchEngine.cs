// spooky-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Reflection;
using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace SpookyBridge;

/// <summary>
/// Core patching engine. Creates an ESP patch mod and applies
/// record operations using Mutagen's typed API.
/// </summary>
public class PatchEngine
{
    public PatchResponse Process(PatchRequest request)
    {
        var outputModKey = ModKey.FromFileName(Path.GetFileName(request.OutputPath));
        var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

        if (!string.IsNullOrEmpty(request.Author))
            patchMod.ModHeader.Author = request.Author;

        if (request.EslFlag)
            patchMod.ModHeader.Flags |= (SkyrimModHeader.HeaderFlag)0x200;

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

        patchMod.WriteToBinary(request.OutputPath);

        // Read masters from the written file — Mutagen auto-adds
        // referenced masters during WriteToBinary
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

        // Only add the record's origin plugin and the source plugin as masters.
        // Mutagen handles transitive dependencies during WriteToBinary.
        // Adding ALL source plugin masters would exceed the 255 master limit
        // for deeply-patched plugins.
        AddMasterIfMissing(patchMod, targetFormKey.ModKey);
        if (sourceMod.ModKey != targetFormKey.ModKey)
            AddMasterIfMissing(patchMod, sourceMod.ModKey);

        var sourceRecord = FindRecord(sourceMod, targetFormKey);
        if (sourceRecord == null)
            throw new KeyNotFoundException(
                $"Record {op.FormId} not found in {Path.GetFileName(op.SourcePath)}");

        var overrideRecord = CopyAsOverride(patchMod, sourceRecord);
        if (overrideRecord == null)
            throw new InvalidOperationException(
                $"Could not create override for {sourceRecord.GetType().Name}");

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

        AddMasterIfMissing(patchMod, targetFormKey.ModKey);
        AddMasterIfMissing(patchMod, baseMod.ModKey);

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
            AddMasterIfMissing(patchMod, overrideMod.ModKey);

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
            AddMasterIfMissing(patchMod, overrideMod.ModKey);

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
            AddMasterIfMissing(patchMod, overrideMod.ModKey);

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
    // Apply All Modifications to an Override Record
    // ═══════════════════════════════════════════════════════════════

    private void ApplyModifications(
        IMajorRecord record, RecordOperation op, RecordDetail detail)
    {
        var mods = detail.Modifications!;

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
            if (op.RemoveSpells?.Count > 0 && npc.ActorEffect != null)
                mods["spells_removed"] = RemoveFormLinks(npc.ActorEffect, op.RemoveSpells);

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
                if (added > 0) mods["perks_added"] = added;
            }
            if (op.RemovePerks?.Count > 0 && npc.Perks != null)
            {
                int removed = 0;
                foreach (var pk in op.RemovePerks)
                {
                    var fk = FormIdHelper.Parse(pk);
                    removed += npc.Perks.RemoveAll(p => p.Perk.FormKey == fk);
                }
                if (removed > 0) mods["perks_removed"] = removed;
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
                if (added > 0) mods["packages_added"] = added;
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
                if (removed > 0) mods["packages_removed"] = removed;
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
                if (added > 0) mods["factions_added"] = added;
            }
            if (op.RemoveFactions?.Count > 0 && npc.Factions != null)
            {
                int removed = 0;
                foreach (var fac in op.RemoveFactions)
                {
                    var fk = FormIdHelper.Parse(fac);
                    removed += npc.Factions.RemoveAll(f => f.Faction.FormKey == fk);
                }
                if (removed > 0) mods["factions_removed"] = removed;
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
                if (added > 0) mods["inventory_added"] = added;
            }
            if (op.RemoveInventory?.Count > 0 && npc.Items != null)
            {
                int removed = 0;
                foreach (var inv in op.RemoveInventory)
                {
                    var fk = FormIdHelper.Parse(inv);
                    removed += npc.Items.RemoveAll(i => i.Item.Item.FormKey == fk);
                }
                if (removed > 0) mods["inventory_removed"] = removed;
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
                if (added > 0) mods["inventory_added"] = added;
            }
            if (op.RemoveInventory?.Count > 0 && container.Items != null)
            {
                int removed = 0;
                foreach (var inv in op.RemoveInventory)
                {
                    var fk = FormIdHelper.Parse(inv);
                    removed += container.Items.RemoveAll(i => i.Item.Item.FormKey == fk);
                }
                if (removed > 0) mods["inventory_removed"] = removed;
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
            if (added > 0) mods["items_added"] = added;
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
                if (added > 0) mods["outfit_items_added"] = added;
            }
            if (op.RemoveOutfitItems?.Count > 0 && outfit.Items != null)
            {
                int removed = 0;
                foreach (var oi in op.RemoveOutfitItems)
                {
                    var fk = FormIdHelper.Parse(oi);
                    var link = fk.ToLink<IOutfitTargetGetter>();
                    if (outfit.Items.Remove(link)) removed++;
                }
                if (removed > 0) mods["outfit_items_removed"] = removed;
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
                if (added > 0) mods["form_list_added"] = added;
            }
            if (op.RemoveFormListEntries?.Count > 0 && formList.Items != null)
            {
                int removed = 0;
                foreach (var fle in op.RemoveFormListEntries)
                {
                    var fk = FormIdHelper.Parse(fle);
                    var link = fk.ToLink<ISkyrimMajorRecordGetter>();
                    if (formList.Items.Remove(link)) removed++;
                }
                if (removed > 0) mods["form_list_removed"] = removed;
            }
        }

        // ── Conditions ──
        if (op.AddConditions?.Count > 0)
            mods["conditions_added"] = ApplyAddConditions(record, op.AddConditions);

        if (op.RemoveConditions?.Count > 0)
            mods["conditions_removed"] = ApplyRemoveConditions(record, op.RemoveConditions);

        // ── VMAD script attachment ──
        if (op.AttachScripts?.Count > 0)
            mods["scripts_attached"] = ApplyAttachScripts(record, op.AttachScripts);

        // ── Enchantment ──
        if (op.SetEnchantment != null)
        {
            var enchFk = FormIdHelper.Parse(op.SetEnchantment);
            if (record is Armor armor)
                armor.ObjectEffect.SetTo(enchFk);
            else if (record is Weapon weapon)
                weapon.ObjectEffect.SetTo(enchFk);
            mods["enchantment_set"] = op.SetEnchantment;
        }
        if (op.ClearEnchantment == true)
        {
            if (record is Armor armor)
                armor.ObjectEffect.Clear();
            else if (record is Weapon weapon)
                weapon.ObjectEffect.Clear();
            mods["enchantment_cleared"] = true;
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

        // Navigate to the parent of the target property
        for (int i = 0; i < parts.Length - 1; i++)
        {
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

        var finalProp = current.GetType().GetProperty(parts[^1],
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (finalProp == null)
            throw new ArgumentException($"Property '{parts[^1]}' not found on {current.GetType().Name}");

        var converted = ConvertJsonValue(value, finalProp.PropertyType);
        finalProp.SetValue(current, converted);
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

        // Enum support — try parsing the string as enum name
        if (underlying.IsEnum && value.ValueKind == JsonValueKind.String)
            return Enum.Parse(underlying, value.GetString()!, ignoreCase: true);
        if (underlying.IsEnum && value.ValueKind == JsonValueKind.Number)
            return Enum.ToObject(underlying, value.GetInt32());

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

    private static int ApplyAddConditions(IMajorRecord record, List<ConditionEntry> conditions)
    {
        // Get the conditions list via reflection (many record types have it)
        var condProp = record.GetType().GetProperty("Conditions",
            BindingFlags.Public | BindingFlags.Instance);
        if (condProp == null)
            throw new InvalidOperationException(
                $"Record type {record.GetType().Name} does not support conditions");

        var condList = condProp.GetValue(record) as ExtendedList<Condition>;
        if (condList == null)
        {
            condList = new ExtendedList<Condition>();
            condProp.SetValue(record, condList);
        }

        int added = 0;
        foreach (var ce in conditions)
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

            condList.Add(condition);
            added++;
        }
        return added;
    }

    private static int ApplyRemoveConditions(IMajorRecord record, List<ConditionRemoval> removals)
    {
        var condProp = record.GetType().GetProperty("Conditions",
            BindingFlags.Public | BindingFlags.Instance);
        if (condProp == null) return 0;

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

        var vmad = vmadProp.GetValue(record) as VirtualMachineAdapter;
        if (vmad == null)
        {
            vmad = new VirtualMachineAdapter();
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
        _ => null,
    };

    private static IMajorRecord? CopyAsOverride(
        SkyrimMod patchMod, IMajorRecordGetter sourceRecord) => sourceRecord switch
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

    private static void AddMasterIfMissing(SkyrimMod patchMod, ModKey master)
    {
        if (master == patchMod.ModKey) return;
        if (!patchMod.ModHeader.MasterReferences.Any(m => m.Master == master))
            patchMod.ModHeader.MasterReferences.Add(new MasterReference { Master = master });
    }
}
