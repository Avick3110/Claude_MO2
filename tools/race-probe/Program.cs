using System.Reflection;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
// Disambiguate Mutagen.Bethesda.Skyrim.Activator from System.Activator
// (the latter is in scope via implicit usings).
using SkActivator = Mutagen.Bethesda.Skyrim.Activator;

// Probe Mutagen.Bethesda.Skyrim 0.53.1 — what shape is Race in?
// Discovery from build errors: <Data> in the Loqui XML is FLATTENED onto Race,
// so Starting/Regen/UnarmedDamage/etc. are direct properties on Race itself,
// not nested under Data. This already simplifies Tier B alias scope.

var modKey = ModKey.FromNameAndExtension("RaceProbe.esp");
var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
var race = new Race(new FormKey(modKey, 0x800), SkyrimRelease.SkyrimSE);
mod.Races.Add(race);

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

void DumpProperty(object? holder, string propName)
{
    if (holder == null) { Console.WriteLine($"  {propName}: holder is null"); return; }
    var prop = holder.GetType().GetProperty(propName,
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    if (prop == null) { Console.WriteLine($"  {propName}: NOT FOUND on {holder.GetType().Name}"); return; }
    var val = prop.GetValue(holder);
    Console.WriteLine($"  {propName}:");
    Console.WriteLine($"    declared type:  {FriendlyType(prop.PropertyType)}");
    Console.WriteLine($"    runtime type:   {(val == null ? "<null>" : FriendlyType(val.GetType()))}");
    Console.WriteLine($"    has setter:     {prop.CanWrite}");
    Console.WriteLine($"    is null:        {val == null}");
}

string FriendlyType(Type t)
{
    if (!t.IsGenericType) return t.FullName ?? t.Name;
    var name = t.Name.Substring(0, t.Name.IndexOf('`'));
    var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyType));
    return $"{t.Namespace}.{name}<{args}>";
}

Section("Race top-level — what Mutagen actually exposes");
Console.WriteLine($"  Race runtime type: {race.GetType().FullName}");
foreach (var p in race.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Where(p => !p.Name.StartsWith("Form") && p.Name != "EditorID" && p.Name != "VirtualMachineAdapter"
                && p.Name != "MajorRecordFlagsRaw" && p.Name != "VersionControl" && p.Name != "FormVersion")
    .OrderBy(p => p.Name))
{
    Console.WriteLine($"    {p.Name,-32} {FriendlyType(p.PropertyType)}");
}

Section("Critical fields");
DumpProperty(race, "Starting");
DumpProperty(race, "Regen");
DumpProperty(race, "UnarmedDamage");
DumpProperty(race, "UnarmedReach");
DumpProperty(race, "BaseMass");
DumpProperty(race, "Keywords");
DumpProperty(race, "ActorEffect");
DumpProperty(race, "BipedObjectNames");

Section("Starting — interfaces, indexer, Add methods");
var startingProp = race.GetType().GetProperty("Starting",
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
var starting = startingProp?.GetValue(race);
if (starting != null)
{
    Console.WriteLine($"  Starting concrete type: {FriendlyType(starting.GetType())}");
    Console.WriteLine($"  Implements:");
    foreach (var i in starting.GetType().GetInterfaces().OrderBy(i => i.Name))
        Console.WriteLine($"    - {FriendlyType(i)}");
    var indexer = starting.GetType().GetProperties()
        .FirstOrDefault(p => p.GetIndexParameters().Length > 0);
    if (indexer != null)
    {
        var paramTypes = string.Join(",", indexer.GetIndexParameters().Select(ip => ip.ParameterType.Name));
        Console.WriteLine($"  Indexer: this[{paramTypes}] => {indexer.PropertyType.Name}, settable={indexer.CanWrite}");
    }
    var addMethods = starting.GetType().GetMethods()
        .Where(m => m.Name == "Add" && m.GetParameters().Length is 1 or 2)
        .ToList();
    Console.WriteLine($"  Add overloads: {addMethods.Count}");
    foreach (var m in addMethods)
        Console.WriteLine($"    - Add({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
}

Section("Mutate test 1: indexer write");
try
{
    Console.WriteLine($"  before: Count={race.Starting.Count}");
    race.Starting[BasicStat.Health] = 250f;
    race.Starting[BasicStat.Magicka] = 150f;
    race.Starting[BasicStat.Stamina] = 175f;
    Console.WriteLine($"  after:  Count={race.Starting.Count}, H={race.Starting[BasicStat.Health]} M={race.Starting[BasicStat.Magicka]} S={race.Starting[BasicStat.Stamina]}");
}
catch (Exception ex)
{
    Console.WriteLine($"  EXCEPTION: {ex.GetType().Name}: {ex.Message}");
}

Section("Mutate test 2: assign Dictionary<K,V> via reflection setter");
try
{
    var newDict = new Dictionary<BasicStat, float>
    {
        [BasicStat.Health] = 100f,
        [BasicStat.Magicka] = 200f,
        [BasicStat.Stamina] = 300f,
    };
    var setProp = race.GetType().GetProperty("Starting",
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
    Console.WriteLine($"  setProp.CanWrite: {setProp.CanWrite}");
    if (setProp.CanWrite)
    {
        try
        {
            setProp.SetValue(race, newDict);
            Console.WriteLine($"  setProp(Dictionary<K,V>) OK -> Count={race.Starting.Count}, H={race.Starting[BasicStat.Health]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  setProp(Dictionary<K,V>) FAILED: {ex.GetType().Name}: {ex.Message}");
            // Try wrapping in concrete dict type if Mutagen needs it
            if (starting != null)
            {
                var concreteType = starting.GetType();
                Console.WriteLine($"  Retrying with concrete type: {FriendlyType(concreteType)}");
                try
                {
                    var concreteInstance = System.Activator.CreateInstance(concreteType);
                    var addMethod = concreteType.GetMethod("Add", new[] { typeof(BasicStat), typeof(float) });
                    if (addMethod != null)
                    {
                        addMethod.Invoke(concreteInstance, new object[] { BasicStat.Health, 100f });
                        addMethod.Invoke(concreteInstance, new object[] { BasicStat.Magicka, 200f });
                        addMethod.Invoke(concreteInstance, new object[] { BasicStat.Stamina, 300f });
                    }
                    setProp.SetValue(race, concreteInstance);
                    Console.WriteLine($"  setProp(concrete) OK -> Count={race.Starting.Count}");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"  concrete-type retry FAILED: {ex2.GetType().Name}: {ex2.Message}");
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  outer EXCEPTION: {ex.GetType().Name}: {ex.Message}");
}

Section("Round-trip write -> read (full Tier C verification)");
try
{
    race.Starting[BasicStat.Health] = 111f;
    race.Starting[BasicStat.Magicka] = 222f;
    race.Starting[BasicStat.Stamina] = 333f;
    race.Regen[BasicStat.Health] = 1.1f;
    race.Regen[BasicStat.Magicka] = 2.2f;
    race.Regen[BasicStat.Stamina] = 3.3f;
    race.UnarmedDamage = 7.5f;
    race.UnarmedReach = 1.25f;

    var tmp = Path.Combine(Path.GetTempPath(), "RaceProbe.esp");
    if (File.Exists(tmp)) File.Delete(tmp);
    try
    {
        mod.WriteToBinary(tmp);
        var sz = new FileInfo(tmp).Length;
        Console.WriteLine($"  wrote: {tmp} ({sz} bytes)");

        var readBack = SkyrimMod.CreateFromBinary(tmp, SkyrimRelease.SkyrimSE);
        var rb = readBack.Races.First();
        Console.WriteLine($"  readback Starting:    H={rb.Starting[BasicStat.Health]} M={rb.Starting[BasicStat.Magicka]} S={rb.Starting[BasicStat.Stamina]}");
        Console.WriteLine($"  readback Regen:       H={rb.Regen[BasicStat.Health]} M={rb.Regen[BasicStat.Magicka]} S={rb.Regen[BasicStat.Stamina]}");
        Console.WriteLine($"  readback UnarmedDmg:  {rb.UnarmedDamage}");
        Console.WriteLine($"  readback UnarmedRch:  {rb.UnarmedReach}");
    }
    finally
    {
        if (File.Exists(tmp)) File.Delete(tmp);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  EXCEPTION: {ex.GetType().Name}: {ex.Message}");
}

Section("Tier A: Keywords + ActorEffect mutation (already supported by Mutagen)");
try
{
    var dummySpellFk = new FormLink<ISpellRecordGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x12345));
    Console.WriteLine($"  ActorEffect runtime type: {FriendlyType(race.ActorEffect?.GetType() ?? typeof(object))}");
    race.ActorEffect ??= new Noggog.ExtendedList<IFormLinkGetter<ISpellRecordGetter>>();
    race.ActorEffect.Add(dummySpellFk);
    Console.WriteLine($"  ActorEffect.Add OK -> Count={race.ActorEffect.Count}");

    var dummyKwFk = new FormLink<IKeywordGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x6789));
    Console.WriteLine($"  Keywords runtime type: {FriendlyType(race.Keywords?.GetType() ?? typeof(object))}");
    race.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    race.Keywords.Add(dummyKwFk);
    Console.WriteLine($"  Keywords.Add OK -> Count={race.Keywords.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"  EXCEPTION: {ex.GetType().Name}: {ex.Message}");
}

// ═══════════════════════════════════════════════════════════════════════════
// v2.7.1 Phase 0 audit verification — extend the probe per AUDIT.md.
// For each record type the audit identifies as "wire up in P3", construct
// the record in-memory, mutate the relevant property, round-trip through
// WriteToBinary / CreateFromBinary, and confirm read-back matches.
// Anything that fails here gets reclassified in AUDIT.md as out-of-scope.
// ═══════════════════════════════════════════════════════════════════════════

int auditFailures = 0;
void RecordFailure(string what, Exception ex)
{
    Console.WriteLine($"  *** FAIL: {what}: {ex.GetType().Name}: {ex.Message}");
    auditFailures++;
}

// Build a single mod containing one record of every audit-identified type,
// mutate each, write once, read back, verify each.
var auditModKey = ModKey.FromNameAndExtension("AuditProbe.esp");
var auditMod = new SkyrimMod(auditModKey, SkyrimRelease.SkyrimSE);

var dummyKw1 = new FormLink<IKeywordGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0xA001));
var dummyKw2 = new FormLink<IKeywordGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0xA002));
var dummyNpcSpawn = new FormLink<INpcSpawnGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0xB001));
var dummySpell = new FormLink<ISpellRecordGetter>(new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0xC001));

// ── Furniture.Keywords ──────────────────────────────────────────────────────
Section("P0 audit: Furniture.Keywords (ExtendedList<IFormLinkGetter<IKeywordGetter>>)");
Furniture? furn = null;
try
{
    furn = new Furniture(new FormKey(auditModKey, 0x100), SkyrimRelease.SkyrimSE);
    auditMod.Furniture.Add(furn);
    DumpProperty(furn, "Keywords");
    furn.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    furn.Keywords.Add(dummyKw1);
    furn.Keywords.Add(dummyKw2);
    Console.WriteLine($"  in-memory Keywords.Count = {furn.Keywords.Count}");
}
catch (Exception ex) { RecordFailure("Furniture.Keywords mutation", ex); }

// ── Activator.Keywords ──────────────────────────────────────────────────────
Section("P0 audit: Activator.Keywords");
SkActivator? acti = null;
try
{
    acti = new SkActivator(new FormKey(auditModKey, 0x200), SkyrimRelease.SkyrimSE);
    auditMod.Activators.Add(acti);
    DumpProperty(acti, "Keywords");
    acti.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    acti.Keywords.Add(dummyKw1);
    Console.WriteLine($"  in-memory Keywords.Count = {acti.Keywords.Count}");
}
catch (Exception ex) { RecordFailure("Activator.Keywords mutation", ex); }

// ── Location.Keywords ───────────────────────────────────────────────────────
Section("P0 audit: Location.Keywords");
Location? lctn = null;
try
{
    lctn = new Location(new FormKey(auditModKey, 0x300), SkyrimRelease.SkyrimSE);
    auditMod.Locations.Add(lctn);
    DumpProperty(lctn, "Keywords");
    lctn.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    lctn.Keywords.Add(dummyKw1);
    Console.WriteLine($"  in-memory Keywords.Count = {lctn.Keywords.Count}");
}
catch (Exception ex) { RecordFailure("Location.Keywords mutation", ex); }

// ── Spell.Keywords ──────────────────────────────────────────────────────────
Section("P0 audit: Spell.Keywords");
Spell? spel = null;
try
{
    spel = new Spell(new FormKey(auditModKey, 0x400), SkyrimRelease.SkyrimSE);
    auditMod.Spells.Add(spel);
    DumpProperty(spel, "Keywords");
    spel.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    spel.Keywords.Add(dummyKw1);
    Console.WriteLine($"  in-memory Keywords.Count = {spel.Keywords.Count}");
}
catch (Exception ex) { RecordFailure("Spell.Keywords mutation", ex); }

// ── MagicEffect.Keywords ────────────────────────────────────────────────────
Section("P0 audit: MagicEffect.Keywords");
MagicEffect? mgef = null;
try
{
    mgef = new MagicEffect(new FormKey(auditModKey, 0x500), SkyrimRelease.SkyrimSE);
    auditMod.MagicEffects.Add(mgef);
    DumpProperty(mgef, "Keywords");
    mgef.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
    mgef.Keywords.Add(dummyKw1);
    Console.WriteLine($"  in-memory Keywords.Count = {mgef.Keywords.Count}");
}
catch (Exception ex) { RecordFailure("MagicEffect.Keywords mutation", ex); }

// ── LeveledNpc.Entries (add_items target) ───────────────────────────────────
Section("P0 audit: LeveledNpc.Entries (LeveledNpcEntry, Reference IFormLinkGetter<INpcSpawnGetter>)");
LeveledNpc? lvln = null;
try
{
    lvln = new LeveledNpc(new FormKey(auditModKey, 0x600), SkyrimRelease.SkyrimSE);
    auditMod.LeveledNpcs.Add(lvln);
    DumpProperty(lvln, "Entries");
    lvln.Entries ??= new Noggog.ExtendedList<LeveledNpcEntry>();
    lvln.Entries.Add(new LeveledNpcEntry
    {
        Data = new LeveledNpcEntryData
        {
            Level = 5,
            Count = 1,
            Reference = dummyNpcSpawn,
        }
    });
    Console.WriteLine($"  in-memory Entries.Count = {lvln.Entries.Count}, " +
        $"Entry[0].Reference.FormKey = {lvln.Entries[0].Data!.Reference.FormKey}");
}
catch (Exception ex) { RecordFailure("LeveledNpc.Entries mutation", ex); }

// ── LeveledSpell.Entries (add_items target) ─────────────────────────────────
Section("P0 audit: LeveledSpell.Entries (LeveledSpellEntry, Reference IFormLinkGetter<ISpellRecordGetter>)");
LeveledSpell? lvsp = null;
try
{
    lvsp = new LeveledSpell(new FormKey(auditModKey, 0x700), SkyrimRelease.SkyrimSE);
    auditMod.LeveledSpells.Add(lvsp);
    DumpProperty(lvsp, "Entries");
    lvsp.Entries ??= new Noggog.ExtendedList<LeveledSpellEntry>();
    lvsp.Entries.Add(new LeveledSpellEntry
    {
        Data = new LeveledSpellEntryData
        {
            Level = 10,
            Count = 1,
            Reference = dummySpell,
        }
    });
    Console.WriteLine($"  in-memory Entries.Count = {lvsp.Entries.Count}, " +
        $"Entry[0].Reference.FormKey = {lvsp.Entries[0].Data!.Reference.FormKey}");
}
catch (Exception ex) { RecordFailure("LeveledSpell.Entries mutation", ex); }

// ── Round-trip the whole audit mod through binary write + read ──────────────
Section("P0 audit: round-trip all audit records through WriteToBinary + CreateFromBinary");
try
{
    var auditPath = Path.Combine(Path.GetTempPath(), "AuditProbe.esp");
    if (File.Exists(auditPath)) File.Delete(auditPath);
    try
    {
        auditMod.WriteToBinary(auditPath);
        var sz = new FileInfo(auditPath).Length;
        Console.WriteLine($"  wrote: {auditPath} ({sz} bytes)");

        var rb = SkyrimMod.CreateFromBinary(auditPath, SkyrimRelease.SkyrimSE);

        var rbFurn = rb.Furniture.FirstOrDefault();
        Console.WriteLine($"  Furniture readback:    Keywords.Count = {rbFurn?.Keywords?.Count ?? -1}");
        if (rbFurn?.Keywords == null || rbFurn.Keywords.Count != 2)
            RecordFailure("Furniture.Keywords readback (expected 2)", new Exception($"got {rbFurn?.Keywords?.Count ?? -1}"));

        var rbActi = rb.Activators.FirstOrDefault();
        Console.WriteLine($"  Activator readback:    Keywords.Count = {rbActi?.Keywords?.Count ?? -1}");
        if (rbActi?.Keywords == null || rbActi.Keywords.Count != 1)
            RecordFailure("Activator.Keywords readback (expected 1)", new Exception($"got {rbActi?.Keywords?.Count ?? -1}"));

        var rbLctn = rb.Locations.FirstOrDefault();
        Console.WriteLine($"  Location readback:     Keywords.Count = {rbLctn?.Keywords?.Count ?? -1}");
        if (rbLctn?.Keywords == null || rbLctn.Keywords.Count != 1)
            RecordFailure("Location.Keywords readback (expected 1)", new Exception($"got {rbLctn?.Keywords?.Count ?? -1}"));

        var rbSpel = rb.Spells.FirstOrDefault();
        Console.WriteLine($"  Spell readback:        Keywords.Count = {rbSpel?.Keywords?.Count ?? -1}");
        if (rbSpel?.Keywords == null || rbSpel.Keywords.Count != 1)
            RecordFailure("Spell.Keywords readback (expected 1)", new Exception($"got {rbSpel?.Keywords?.Count ?? -1}"));

        var rbMgef = rb.MagicEffects.FirstOrDefault();
        Console.WriteLine($"  MagicEffect readback:  Keywords.Count = {rbMgef?.Keywords?.Count ?? -1}");
        if (rbMgef?.Keywords == null || rbMgef.Keywords.Count != 1)
            RecordFailure("MagicEffect.Keywords readback (expected 1)", new Exception($"got {rbMgef?.Keywords?.Count ?? -1}"));

        var rbLvln = rb.LeveledNpcs.FirstOrDefault();
        Console.WriteLine($"  LeveledNpc readback:   Entries.Count  = {rbLvln?.Entries?.Count ?? -1}, " +
            $"Entry[0].Reference.FormKey = {rbLvln?.Entries?[0].Data?.Reference.FormKey}");
        if (rbLvln?.Entries == null || rbLvln.Entries.Count != 1)
            RecordFailure("LeveledNpc.Entries readback (expected 1)", new Exception($"got {rbLvln?.Entries?.Count ?? -1}"));

        var rbLvsp = rb.LeveledSpells.FirstOrDefault();
        Console.WriteLine($"  LeveledSpell readback: Entries.Count  = {rbLvsp?.Entries?.Count ?? -1}, " +
            $"Entry[0].Reference.FormKey = {rbLvsp?.Entries?[0].Data?.Reference.FormKey}");
        if (rbLvsp?.Entries == null || rbLvsp.Entries.Count != 1)
            RecordFailure("LeveledSpell.Entries readback (expected 1)", new Exception($"got {rbLvsp?.Entries?.Count ?? -1}"));
    }
    finally
    {
        if (File.Exists(auditPath)) File.Delete(auditPath);
    }
}
catch (Exception ex)
{
    RecordFailure("audit-mod round-trip", ex);
}

Console.WriteLine();
if (auditFailures > 0)
{
    Console.WriteLine($"=== probe FAILED: {auditFailures} audit failure(s) — reclassify in AUDIT.md ===");
    Environment.Exit(1);
}
Console.WriteLine("=== probe complete ===");
