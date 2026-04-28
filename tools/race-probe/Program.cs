using System.Reflection;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
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

// ═══════════════════════════════════════════════════════════════════════════
// v2.8.0 Phase 1 — Effects API contract probe.
//
// Per PLAN.md § Phase 1 / EFFECTS_AUDIT.md target. For each of
// {Spell, Ingestible, ObjectEffect, Scroll, Ingredient}:
//   - Inspect the Effects property: type, setter, initial state.
//   - Inspect Effect.Data shape — sub-LoquiObject (needs bridge Branch B) or
//     flat Magnitude/Area/Duration on Effect (existing reflection covers it).
//   - Inspect Effect.Conditions.
//   - Mutate (BaseEffect + magnitude/area/duration + one Condition).
//   - Round-trip through WriteToBinary + CreateFromBinary; verify read-back.
//
// Constructibility section explicitly tests Activator.CreateInstance on
// Effect, Condition, ConditionFloat, ConditionGlobal, and EffectData
// (if present as a separate type). Bridge Branch A's special-case for
// typeof(Condition) is justified by Condition's likely abstract / no-arg-ctor
// failure here — captured for traceability.
// ═══════════════════════════════════════════════════════════════════════════

int effectsAuditFailures = 0;
void RecordEffectsFailure(string what, Exception ex)
{
    Console.WriteLine($"  *** FAIL: {what}: {ex.GetType().Name}: {ex.Message}");
    effectsAuditFailures++;
}

Section("v2.8 P1 Effects: Constructibility — Activator.CreateInstance");
void TryActivator(string label, Type t)
{
    try
    {
        var inst = System.Activator.CreateInstance(t);
        Console.WriteLine($"  {label,-46} OK -> {(inst == null ? "<null>" : FriendlyType(inst.GetType()))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {label,-46} FAIL ({ex.GetType().Name}: {ex.Message.Split('\n')[0]})");
    }
}
TryActivator("typeof(Effect)", typeof(Effect));
TryActivator("typeof(Condition)", typeof(Condition));
TryActivator("typeof(ConditionFloat)", typeof(ConditionFloat));
TryActivator("typeof(ConditionGlobal)", typeof(ConditionGlobal));
var effectDataType = typeof(ISkyrimMod).Assembly.GetType("Mutagen.Bethesda.Skyrim.EffectData");
if (effectDataType != null)
    TryActivator("typeof(EffectData) [Mutagen.Bethesda.Skyrim]", effectDataType);
else
    Console.WriteLine("  Mutagen.Bethesda.Skyrim.EffectData             ABSENT (Effect probably has flat Magnitude/Area/Duration)");

Section("v2.8 P1 Effects: Effect class — public instance properties");
foreach (var p in typeof(Effect).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .OrderBy(p => p.Name))
{
    Console.WriteLine($"    {p.Name,-32} {FriendlyType(p.PropertyType),-72} setter={p.CanWrite}");
}

// Discover the canonical Magnitude carrier (flat-on-Effect vs sub-LoquiObject).
var dataPropOnce = typeof(Effect).GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
bool dataIsSubLoquiObject;
if (dataPropOnce == null)
{
    dataIsSubLoquiObject = false;
    Console.WriteLine();
    Console.WriteLine("  >>> Effect.Data property ABSENT — flat shape; Branch B NOT required for Effect/Data");
}
else if (!dataPropOnce.PropertyType.IsClass || dataPropOnce.PropertyType == typeof(string))
{
    dataIsSubLoquiObject = false;
    Console.WriteLine();
    Console.WriteLine($"  >>> Effect.Data is non-class ({FriendlyType(dataPropOnce.PropertyType)}) — Branch B NOT required");
}
else
{
    dataIsSubLoquiObject = true;
    Console.WriteLine();
    Console.WriteLine($"  >>> Effect.Data is sub-LoquiObject ({FriendlyType(dataPropOnce.PropertyType)}) — Branch B REQUIRED");
}

// Per-record-type probe.
void ProbeEffectCarrier<TRecord>(
    string recordTypeName,
    Func<FormKey, TRecord> ctor,
    Action<SkyrimMod, TRecord> addToMod,
    Func<SkyrimMod, FormKey, TRecord?> readBack)
    where TRecord : class
{
    Section($"v2.8 P1 Effects: {recordTypeName} — build, mutate Effects, round-trip");

    var carrierModKey = ModKey.FromNameAndExtension($"EffectsProbe_{recordTypeName}.esp");
    var carrierMod = new SkyrimMod(carrierModKey, SkyrimRelease.SkyrimSE);
    var carrierFormKey = new FormKey(carrierModKey, 0x800);

    TRecord record;
    try
    {
        record = ctor(carrierFormKey);
        addToMod(carrierMod, record);
    }
    catch (Exception ex)
    {
        RecordEffectsFailure($"{recordTypeName} construction", ex);
        return;
    }

    var effectsProp = typeof(TRecord).GetProperty("Effects",
        BindingFlags.Public | BindingFlags.Instance);
    if (effectsProp == null)
    {
        RecordEffectsFailure($"{recordTypeName}.Effects property", new Exception("not found"));
        return;
    }
    Console.WriteLine($"  {recordTypeName}.Effects: {FriendlyType(effectsProp.PropertyType)}, setter={effectsProp.CanWrite}");
    var initEffects = effectsProp.GetValue(record);
    Console.WriteLine($"  {recordTypeName}.Effects initial: {(initEffects == null ? "<null>" : $"Count={((System.Collections.IList)initEffects).Count}")}");

    Effect effect;
    try { effect = new Effect(); }
    catch (Exception ex) { RecordEffectsFailure("new Effect()", ex); return; }

    var baseEffectFk = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x12345);
    try
    {
        effect.BaseEffect.SetTo(baseEffectFk);
        Console.WriteLine($"  Effect.BaseEffect.SetTo({baseEffectFk}) OK");
    }
    catch (Exception ex)
    {
        RecordEffectsFailure("Effect.BaseEffect.SetTo", ex);
    }

    float testMag = 50f;
    int testArea = 10;
    int testDur = 30;
    object? dataInstance = null;

    // SetByConvert: reflection setter that respects the actual runtime property type.
    // The bridge's ConvertJsonValue uses prop.PropertyType to drive the JsonElement
    // accessor, so this mirrors the real bridge code path. Convert.ChangeType handles
    // float<->double / int<->uint / etc. on the assignment.
    void SetByConvert(object holder, string propName, object src)
    {
        var p = holder.GetType().GetProperty(propName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p == null) { Console.WriteLine($"    {propName}: NOT FOUND on {FriendlyType(holder.GetType())}"); return; }
        try
        {
            var converted = Convert.ChangeType(src, p.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            p.SetValue(holder, converted);
            Console.WriteLine($"    {propName,-12} ({FriendlyType(p.PropertyType),-14}) <- {src} ({src.GetType().Name})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    {propName,-12} FAIL: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    if (dataIsSubLoquiObject)
    {
        var dataProp = dataPropOnce!;
        try
        {
            dataInstance = dataProp.GetValue(effect);
            Console.WriteLine($"  Effect.Data initial: {(dataInstance == null ? "<null>" : FriendlyType(dataInstance.GetType()))}");
            if (dataInstance == null)
            {
                if (!dataProp.CanWrite)
                    throw new Exception("Effect.Data is null and property has no setter");
                dataInstance = System.Activator.CreateInstance(dataProp.PropertyType);
                dataProp.SetValue(effect, dataInstance);
                Console.WriteLine($"  Effect.Data Activator-created: {FriendlyType(dataInstance!.GetType())}");
            }
            // Dump EffectData's properties so the audit captures actual runtime types.
            Console.WriteLine($"  EffectData properties:");
            foreach (var p in dataInstance!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.Name))
            {
                Console.WriteLine($"      {p.Name,-32} {FriendlyType(p.PropertyType),-32} setter={p.CanWrite}");
            }
            Console.WriteLine($"  Effect.Data set:");
            SetByConvert(dataInstance!, "Magnitude", testMag);
            SetByConvert(dataInstance!, "Area",      testArea);
            SetByConvert(dataInstance!, "Duration",  testDur);
        }
        catch (Exception ex)
        {
            RecordEffectsFailure("Effect.Data sub-object mutation", ex);
        }
    }
    else
    {
        try
        {
            Console.WriteLine($"  Effect (flat) set:");
            SetByConvert(effect, "Magnitude", testMag);
            SetByConvert(effect, "Area",      testArea);
            SetByConvert(effect, "Duration",  testDur);
        }
        catch (Exception ex)
        {
            RecordEffectsFailure("Effect flat Magnitude/Area/Duration mutation", ex);
        }
    }

    var condProp = typeof(Effect).GetProperty("Conditions",
        BindingFlags.Public | BindingFlags.Instance);
    bool conditionAdded = false;
    if (condProp == null)
    {
        RecordEffectsFailure("Effect.Conditions property", new Exception("not found"));
    }
    else
    {
        Console.WriteLine($"  Effect.Conditions: {FriendlyType(condProp.PropertyType)}, setter={condProp.CanWrite}");
        try
        {
            var condList = condProp.GetValue(effect) as System.Collections.IList;
            if (condList == null)
            {
                if (!condProp.CanWrite)
                    throw new Exception("Conditions list is null and property has no setter");
                condList = (System.Collections.IList)System.Activator.CreateInstance(condProp.PropertyType)!;
                condProp.SetValue(effect, condList);
            }
            var gavType = typeof(ISkyrimMod).Assembly.GetType("Mutagen.Bethesda.Skyrim.GetActorValueConditionData");
            if (gavType == null)
            {
                Console.WriteLine("  GetActorValueConditionData not found via reflection — skipping Conditions add");
            }
            else
            {
                var gavData = (ConditionData)System.Activator.CreateInstance(gavType)!;
                var cond = new ConditionFloat
                {
                    ComparisonValue = 50f,
                    CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                    Data = gavData,
                };
                condList.Add(cond);
                conditionAdded = true;
                Console.WriteLine($"  Effect.Conditions added ConditionFloat (GetActorValue >= 50): Count={condList.Count}");
            }
        }
        catch (Exception ex)
        {
            RecordEffectsFailure("Effect.Conditions add", ex);
        }
    }

    System.Collections.IList? carrierEffects;
    try
    {
        carrierEffects = effectsProp.GetValue(record) as System.Collections.IList;
        if (carrierEffects == null)
        {
            if (!effectsProp.CanWrite)
                throw new Exception($"{recordTypeName}.Effects is null and property has no setter");
            carrierEffects = (System.Collections.IList)System.Activator.CreateInstance(effectsProp.PropertyType)!;
            effectsProp.SetValue(record, carrierEffects);
        }
        carrierEffects.Add(effect);
        Console.WriteLine($"  {recordTypeName}.Effects after add: Count={carrierEffects.Count}");
    }
    catch (Exception ex)
    {
        RecordEffectsFailure($"{recordTypeName}.Effects add", ex);
        return;
    }

    var rtPath = Path.Combine(Path.GetTempPath(), $"EffectsProbe_{recordTypeName}.esp");
    if (File.Exists(rtPath)) File.Delete(rtPath);
    try
    {
        try
        {
            carrierMod.WriteToBinary(rtPath);
            var sz = new FileInfo(rtPath).Length;
            Console.WriteLine($"  wrote: {rtPath} ({sz} bytes)");

            var rb = SkyrimMod.CreateFromBinary(rtPath, SkyrimRelease.SkyrimSE);
            var rbRecord = readBack(rb, carrierFormKey);
            if (rbRecord == null)
            {
                RecordEffectsFailure($"{recordTypeName} readback record", new Exception("not found in readback mod"));
                return;
            }

            var rbEffectsProp = rbRecord.GetType().GetProperty("Effects",
                BindingFlags.Public | BindingFlags.Instance);
            var rbEffects = rbEffectsProp?.GetValue(rbRecord) as System.Collections.IList;
            int rbCount = rbEffects?.Count ?? -1;
            Console.WriteLine($"  readback {recordTypeName}.Effects.Count = {rbCount}");
            if (rbEffects == null || rbCount != 1)
            {
                RecordEffectsFailure($"{recordTypeName} readback Effects.Count != 1", new Exception($"got {rbCount}"));
                return;
            }

            var rbEffect = rbEffects[0]!;
            var rbBaseEffect = rbEffect.GetType().GetProperty("BaseEffect")?.GetValue(rbEffect);
            Console.WriteLine($"  readback Effect[0].BaseEffect: {rbBaseEffect}");

            if (dataIsSubLoquiObject)
            {
                var rbData = rbEffect.GetType().GetProperty("Data")?.GetValue(rbEffect);
                var rbMag = rbData?.GetType().GetProperty("Magnitude")?.GetValue(rbData);
                var rbArea = rbData?.GetType().GetProperty("Area")?.GetValue(rbData);
                var rbDur = rbData?.GetType().GetProperty("Duration")?.GetValue(rbData);
                Console.WriteLine($"  readback Effect[0].Data: Magnitude={rbMag} Area={rbArea} Duration={rbDur}");
                if (rbMag is float m && Math.Abs(m - testMag) > 0.001f)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Data.Magnitude mismatch", new Exception($"expected {testMag}, got {m}"));
                if (rbArea != null && Convert.ToInt32(rbArea) != testArea)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Data.Area mismatch", new Exception($"expected {testArea}, got {rbArea}"));
                if (rbDur != null && Convert.ToInt32(rbDur) != testDur)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Data.Duration mismatch", new Exception($"expected {testDur}, got {rbDur}"));
            }
            else
            {
                var rbMag = rbEffect.GetType().GetProperty("Magnitude")?.GetValue(rbEffect);
                var rbArea = rbEffect.GetType().GetProperty("Area")?.GetValue(rbEffect);
                var rbDur = rbEffect.GetType().GetProperty("Duration")?.GetValue(rbEffect);
                Console.WriteLine($"  readback Effect[0] (flat): Magnitude={rbMag} Area={rbArea} Duration={rbDur}");
                if (rbMag is float m && Math.Abs(m - testMag) > 0.001f)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Magnitude mismatch", new Exception($"expected {testMag}, got {m}"));
                if (rbArea != null && Convert.ToInt32(rbArea) != testArea)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Area mismatch", new Exception($"expected {testArea}, got {rbArea}"));
                if (rbDur != null && Convert.ToInt32(rbDur) != testDur)
                    RecordEffectsFailure($"{recordTypeName} Effect[0].Duration mismatch", new Exception($"expected {testDur}, got {rbDur}"));
            }

            var rbConds = rbEffect.GetType().GetProperty("Conditions")?.GetValue(rbEffect) as System.Collections.IList;
            int rbCondCount = rbConds?.Count ?? -1;
            Console.WriteLine($"  readback Effect[0].Conditions.Count = {rbCondCount}");
            if (conditionAdded && rbCondCount != 1)
                RecordEffectsFailure($"{recordTypeName} Effect[0].Conditions.Count mismatch", new Exception($"expected 1, got {rbCondCount}"));
        }
        finally
        {
            if (File.Exists(rtPath)) File.Delete(rtPath);
        }
    }
    catch (Exception ex)
    {
        RecordEffectsFailure($"{recordTypeName} round-trip", ex);
    }
}

ProbeEffectCarrier<Spell>(
    "Spell",
    fk => new Spell(fk, SkyrimRelease.SkyrimSE),
    (m, r) => m.Spells.Add(r),
    (rb, fk) => rb.Spells.FirstOrDefault(s => s.FormKey == fk));

ProbeEffectCarrier<Ingestible>(
    "Ingestible",
    fk => new Ingestible(fk, SkyrimRelease.SkyrimSE),
    (m, r) => m.Ingestibles.Add(r),
    (rb, fk) => rb.Ingestibles.FirstOrDefault(i => i.FormKey == fk));

ProbeEffectCarrier<ObjectEffect>(
    "ObjectEffect",
    fk => new ObjectEffect(fk, SkyrimRelease.SkyrimSE),
    (m, r) => m.ObjectEffects.Add(r),
    (rb, fk) => rb.ObjectEffects.FirstOrDefault(o => o.FormKey == fk));

ProbeEffectCarrier<Scroll>(
    "Scroll",
    fk => new Scroll(fk, SkyrimRelease.SkyrimSE),
    (m, r) => m.Scrolls.Add(r),
    (rb, fk) => rb.Scrolls.FirstOrDefault(s => s.FormKey == fk));

ProbeEffectCarrier<Ingredient>(
    "Ingredient",
    fk => new Ingredient(fk, SkyrimRelease.SkyrimSE),
    (m, r) => m.Ingredients.Add(r),
    (rb, fk) => rb.Ingredients.FirstOrDefault(i => i.FormKey == fk));

// ═══════════════════════════════════════════════════════════════════════════
// v2.8 Phase 2 / Batch 7 — VMAD case (A) vs (B) disambiguator + adapter probe
//
// Aaron's clarification (2026-04-25, post-Batch-6 review): tools_patching.py
// schema description claims VMAD is supported on Outfit + Spell, but Phase 2
// found bridge errors "Record type Outfit/Spell does not support scripts"
// originating from the existing reflection guard at PatchEngine.cs:1732-1734
// (the `vmadProp == null` branch). Two competing hypotheses:
//
//   Case (A): Mutagen 0.53.1 genuinely doesn't expose VMAD on the concrete
//             Outfit/Spell classes. Then the v2.7.1 schema description is
//             wrong and these are MATRIX-only SKIPs (no bridge bug).
//
//   Case (B): Mutagen exposes VMAD via interface (e.g. via explicit interface
//             implementation, or only on a parent interface like
//             IHaveVirtualMachineAdapter). Then the bridge's reflection
//             lookup `record.GetType().GetProperty(...)` misses it and
//             real consumers can't attach scripts on Outfit/Spell.
//             That's a NEW Phase 4 bridge bug.
//
// Disambiguator: probe `typeof(Outfit).GetProperty("VirtualMachineAdapter")`
// AND walk all interfaces of Outfit for any "VirtualMachineAdapter" property.
// Same for Spell. Then the verdict goes in PHASE_2_HANDOFF.md.
// ═══════════════════════════════════════════════════════════════════════════
//
// Flow per record type (PERK, QUST):
//   1. Pick a vanilla Skyrim.esm record with VMAD == null (auto-create path).
//   2. Call mutagen-bridge.exe with attach_scripts on that record.
//   3. Read the output ESP back via SkyrimMod.CreateFromBinary (NOT overlay)
//      so all properties + concrete types are exposed.
//   4. Inspect output.<Records>[0].VirtualMachineAdapter.GetType().
//   5. Document. PerkAdapter / QuestAdapter → bug doesn't reproduce.
//      Base VirtualMachineAdapter → BUG CONFIRMED for Phase 4.
//
// Note: Mutagen's binary readback may schema-coerce the runtime type back to
// the correct subclass even if the bridge wrote the base type. If the runtime
// type IS the correct subclass, that's still useful data — it means the bug
// (if it exists in-memory in the bridge process) doesn't manifest in the
// written ESP.
// ═══════════════════════════════════════════════════════════════════════════

const string SkyrimEsmForBatch7 = @"E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm";

Section("v2.8 P2 Batch 7 — VMAD case (A) vs (B) disambiguator (Outfit / Spell)");

// Probe whether Mutagen 0.53.1 declares "VirtualMachineAdapter" on the
// concrete Outfit and Spell classes — and via any of their interfaces.
void ProbeVmadDisambiguator(string label, Type concreteType)
{
    var classProp = concreteType.GetProperty("VirtualMachineAdapter",
        BindingFlags.Public | BindingFlags.Instance);
    Console.WriteLine($"  {label}.GetProperty(\"VirtualMachineAdapter\"): {(classProp == null ? "null" : "non-null, declared type=" + FriendlyType(classProp.PropertyType))}");

    // DeclaredOnly — does the concrete class itself declare it (vs inherited)?
    var classPropDeclared = concreteType.GetProperty("VirtualMachineAdapter",
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    Console.WriteLine($"  {label} declares VMAD itself (DeclaredOnly): {classPropDeclared != null}");

    // Walk every interface looking for VirtualMachineAdapter.
    var interfaceHits = new List<string>();
    foreach (var iface in concreteType.GetInterfaces())
    {
        var ifaceProp = iface.GetProperty("VirtualMachineAdapter",
            BindingFlags.Public | BindingFlags.Instance);
        if (ifaceProp != null)
            interfaceHits.Add($"{FriendlyType(iface)} → {FriendlyType(ifaceProp.PropertyType)}");
    }
    if (interfaceHits.Count == 0)
        Console.WriteLine($"  {label} interfaces declaring VMAD: <none>");
    else
    {
        Console.WriteLine($"  {label} interfaces declaring VMAD ({interfaceHits.Count}):");
        foreach (var hit in interfaceHits) Console.WriteLine($"    - {hit}");
    }

    // Walk base-class chain. Mutagen often uses MajorRecord parents.
    var baseChain = new List<string>();
    var t = concreteType.BaseType;
    while (t != null && t != typeof(object))
    {
        var bp = t.GetProperty("VirtualMachineAdapter",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (bp != null)
            baseChain.Add($"{FriendlyType(t)} declares VMAD as {FriendlyType(bp.PropertyType)}");
        t = t.BaseType;
    }
    if (baseChain.Count == 0)
        Console.WriteLine($"  {label} base-class chain declaring VMAD: <none>");
    else
    {
        Console.WriteLine($"  {label} base-class chain declaring VMAD:");
        foreach (var b in baseChain) Console.WriteLine($"    - {b}");
    }

    // Verdict heuristic.
    string verdict;
    if (classProp != null) verdict = "Case (B) — concrete class exposes VMAD; bridge reflection lookup ought to find it. If bridge errors, that's a separate Phase 4 bug.";
    else if (interfaceHits.Count == 0 && baseChain.Count == 0) verdict = "Case (A) — Mutagen 0.53.1 genuinely doesn't expose VMAD on this record type. v2.7.1 schema description was incorrect.";
    else verdict = "Case (B) — VMAD declared on interface or base class but NOT visible via concrete-class reflection. Bridge's reflection lookup needs to walk interfaces/base-chain. NEW BRIDGE BUG.";
    Console.WriteLine($"  >>> verdict for {label}: {verdict}");
}

ProbeVmadDisambiguator("typeof(Outfit)", typeof(Outfit));
ProbeVmadDisambiguator("typeof(Spell)", typeof(Spell));

// ═══════════════════════════════════════════════════════════════════════════
// v2.8 Phase 4 — VMAD deeper disambiguation
//
// Phase 2's disambiguator above checked one specific property name
// ("VirtualMachineAdapter") on concrete class + interfaces + base chain — all
// null. That rules out the simplest case but does NOT rule out:
//   (a) a different property name (e.g. "VMad", "ScriptAdapter", "Scripts"),
//   (b) interface-based or extension-method exposure under a different name,
//   (c) Mutagen preserving VMAD opaquely in the binary serialization layer
//       even when no typed property surfaces it.
//
// Phase 4 probes both: (1) full property name dump on Outfit + Spell looking
// for any VMAD-shaped property under any name, and (2) Mutagen-direct
// round-trip preservation test — read Skyrim.esm via SkyrimMod.CreateFromBinary
// (FULL, not overlay), write back via WriteToBinary, scan SPEL/OTFT group
// bytes for the "VMAD" subrecord signature pre/post and compare counts. If
// the input has VMAD-in-SPEL bytes and the output has zero, Mutagen drops
// VMAD on round-trip → Case (A) confirmed. If counts match, Mutagen
// preserves VMAD via some non-property mechanism → Case (C) suspected.
// ═══════════════════════════════════════════════════════════════════════════

Section("v2.8 P4 — VMAD deeper disambiguation: full property dump (Outfit / Spell)");

void DumpAllPropertiesLookingForVmad(string label, Type t)
{
    var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .OrderBy(p => p.Name)
                 .ToList();
    Console.WriteLine($"  {label} — {props.Count} public instance properties:");
    var hits = new List<string>();
    foreach (var p in props)
    {
        // Case-sensitive PascalCase whole-word matches. "Description" once
        // false-flagged because "description".Contains("script") is true;
        // restrict to markers unique to the VMAD hierarchy. Type-level check
        // catches anything assignable to VirtualMachineAdapter under any name.
        bool nameShaped = p.Name.Contains("Vmad") || p.Name.Contains("VMad") || p.Name.Contains("VMAD")
                       || p.Name.Contains("Adapter")
                       || p.Name == "VM" || p.Name == "Scripts";
        bool typeShaped = typeof(VirtualMachineAdapter).IsAssignableFrom(p.PropertyType);
        bool isShaped = nameShaped || typeShaped;
        var marker = isShaped ? "  <<< VMAD-SHAPED >>>" : "";
        Console.WriteLine($"    {p.Name,-40} : {FriendlyType(p.PropertyType)}{marker}");
        if (isShaped) hits.Add($"{p.Name} ({FriendlyType(p.PropertyType)})");
    }
    Console.WriteLine($"  {label} VMAD-shaped property hits: {hits.Count}");
    foreach (var h in hits) Console.WriteLine($"    - {h}");
}

DumpAllPropertiesLookingForVmad("typeof(Outfit)", typeof(Outfit));
DumpAllPropertiesLookingForVmad("typeof(Spell)", typeof(Spell));

Section("v2.8 P4 — VMAD round-trip preservation test (Mutagen-direct, no bridge)");

// Count occurrences of byte sequence `needle` in `hay[start..end)`.
int CountByteSeq(byte[] hay, int start, int end, byte[] needle)
{
    int n = 0;
    int last = Math.Min(end, hay.Length) - needle.Length;
    for (int i = start; i <= last; i++)
    {
        bool match = true;
        for (int j = 0; j < needle.Length; j++)
            if (hay[i + j] != needle[j]) { match = false; break; }
        if (match) n++;
    }
    return n;
}

// Walk top-level GRUPs of an ESM, find the GRUP whose label matches
// `groupLabel` (e.g. "SPEL"), and count "VMAD" byte-signature occurrences
// inside its 24-byte-header-skipped body. Heuristic — false positives
// possible if VMAD bytes appear inside subrecord data, but rare in
// vanilla ESM data and we compare input vs output (any FPs appear in both).
(int count, long bodyBytes, bool foundGroup) CountVmadSigInGroup(string path, string groupLabel)
{
    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < 24) return (0, 0, false);
    if (System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "TES4") return (0, 0, false);
    int tes4BodySize = BitConverter.ToInt32(bytes, 4);
    int pos = 24 + tes4BodySize; // 24-byte record header + body
    var needle = new byte[] { 0x56, 0x4D, 0x41, 0x44 }; // "VMAD"
    while (pos + 24 <= bytes.Length)
    {
        var sig = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
        if (sig != "GRUP") break;
        int grupSize = BitConverter.ToInt32(bytes, pos + 4);
        if (grupSize < 24 || pos + grupSize > bytes.Length) break;
        var label = System.Text.Encoding.ASCII.GetString(bytes, pos + 8, 4);
        int grupEnd = pos + grupSize;
        if (label == groupLabel)
        {
            int regionStart = pos + 24;
            int regionEnd = grupEnd;
            int count = CountByteSeq(bytes, regionStart, regionEnd, needle);
            return (count, regionEnd - regionStart, true);
        }
        pos = grupEnd;
    }
    return (0, 0, false);
}

if (!File.Exists(SkyrimEsmForBatch7))
{
    Console.WriteLine($"  SKIP: Skyrim.esm not found at {SkyrimEsmForBatch7}");
}
else
{
    var roundTripDir = Path.Combine(Path.GetTempPath(), "race-probe-vmad-roundtrip");
    Directory.CreateDirectory(roundTripDir);
    // Mutagen's WriteToBinary requires the output filename to match the mod's
    // ModKey ("Skyrim.esm"); writing to a different basename triggers
    // "ModKeys were misaligned". Use the original name in a separate temp dir.
    var roundTripOutput = Path.Combine(roundTripDir, "Skyrim.esm");
    if (File.Exists(roundTripOutput)) File.Delete(roundTripOutput);

    // Discover BinaryWriteParameters surface so we can disable Mutagen's
    // lower-FormKey-range guard (which fires on round-tripping Skyrim.esm
    // because vanilla 00005B:Skyrim.esm sits below the non-master 0x800 floor).
    Type? bwpType = null;
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        foreach (var t in asm.GetTypes())
        {
            if (t.Name == "BinaryWriteParameters") { bwpType = t; break; }
        }
        if (bwpType != null) break;
    }
    if (bwpType != null)
    {
        Console.WriteLine($"  found {bwpType.FullName}; exposed properties:");
        foreach (var p in bwpType.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
            Console.WriteLine($"    {p.Name,-32} : {FriendlyType(p.PropertyType)}");
    }

    // Discover concrete subclasses of ALowerRangeDisallowedHandlerOption so we
    // can pick the "skip" / "ignore" / "no-check" variant for the round-trip.
    Type? lrdhBase = null;
    var lrdhConcretes = new List<Type>();
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        foreach (var t in asm.GetTypes())
        {
            if (t.Name == "ALowerRangeDisallowedHandlerOption") { lrdhBase = t; break; }
        }
        if (lrdhBase != null) break;
    }
    if (lrdhBase != null)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (lrdhBase.IsAssignableFrom(t) && !t.IsAbstract && t != lrdhBase)
                    lrdhConcretes.Add(t);
            }
        }
        Console.WriteLine($"  ALowerRangeDisallowedHandlerOption concrete subclasses: {lrdhConcretes.Count}");
        foreach (var c in lrdhConcretes) Console.WriteLine($"    - {c.FullName}");
    }

    Console.WriteLine($"  Reading Skyrim.esm via SkyrimMod.CreateFromBinary (FULL read, not overlay)...");
    Console.WriteLine($"    source: {SkyrimEsmForBatch7}");
    Console.WriteLine($"    output: {roundTripOutput}");
    bool roundTripOk = false;
    try
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var srcFull = SkyrimMod.CreateFromBinary(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
        sw.Stop();
        Console.WriteLine($"    read OK in {sw.ElapsedMilliseconds:N0} ms — Spells.Count={srcFull.Spells.Count}, Outfits.Count={srcFull.Outfits.Count}");
        sw.Restart();

        // Build a BinaryWriteParameters that disables every can't-write-this
        // guard (master-flag sync, lower-range, etc.) so the round-trip
        // succeeds against vanilla Skyrim.esm. Set every public enum property
        // whose enum has a "NoCheck" / "Disabled" / "Skip" / "Ignore" value.
        object? param = null;
        if (bwpType != null)
        {
            param = System.Activator.CreateInstance(bwpType);
            foreach (var p in bwpType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType.IsEnum)
                {
                    foreach (var v in Enum.GetNames(p.PropertyType))
                    {
                        var lv = v.ToLowerInvariant();
                        if (lv == "nocheck" || lv == "disabled" || lv == "skip" || lv == "ignore" || lv == "off")
                        {
                            try
                            {
                                var enumVal = Enum.Parse(p.PropertyType, v);
                                p.SetValue(param, enumVal);
                                Console.WriteLine($"    BinaryWriteParameters.{p.Name} = {p.PropertyType.Name}.{v}");
                                break;
                            }
                            catch { }
                        }
                    }
                }
                else if (lrdhBase != null && p.PropertyType == lrdhBase)
                {
                    // Pick a concrete subclass with "AddPlaceholderToMasters",
                    // "SetToZero", "Skip", or "Ignore" semantics — anything
                    // that disables the throw on lower-FormKey range.
                    // Prefer "AddPlaceholderMaster" — it's the only concrete
                    // whose name suggests actually allowing low-range FormIDs
                    // through (by injecting a placeholder master entry).
                    // "NoCheck" turned out to still throw on Skyrim.esm round-trip.
                    Type? pick = lrdhConcretes.FirstOrDefault(c => c.Name.Contains("Placeholder"))
                              ?? lrdhConcretes.FirstOrDefault(c => c.Name.Contains("NoCheck"))
                              ?? lrdhConcretes.FirstOrDefault();
                    if (pick != null)
                    {
                        try
                        {
                            var inst = System.Activator.CreateInstance(pick);
                            p.SetValue(param, inst);
                            Console.WriteLine($"    BinaryWriteParameters.{p.Name} = {pick.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    BinaryWriteParameters.{p.Name} construction FAILED: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        // Find an overload of WriteToBinary that takes (string, BinaryWriteParameters)
        // — typically an extension method on IModExt or similar.
        bool wroteWithParams = false;
        if (param != null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (!t.IsAbstract || !t.IsSealed) continue; // static
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name != "WriteToBinary") continue;
                        var ps = m.GetParameters();
                        if (ps.Length != 3) continue;
                        // (this IMod, string path, BinaryWriteParameters)
                        if (ps[0].ParameterType.IsAssignableFrom(srcFull.GetType())
                            && ps[1].ParameterType == typeof(string)
                            && ps[2].ParameterType == bwpType)
                        {
                            m.Invoke(null, new[] { srcFull, (object)roundTripOutput, param });
                            wroteWithParams = true;
                            break;
                        }
                    }
                    if (wroteWithParams) break;
                }
                if (wroteWithParams) break;
            }
        }
        if (!wroteWithParams)
        {
            // Fall back to the default WriteToBinary; will likely fail again.
            srcFull.WriteToBinary(roundTripOutput);
        }
        sw.Stop();
        var outSize = new FileInfo(roundTripOutput).Length;
        Console.WriteLine($"    write OK in {sw.ElapsedMilliseconds:N0} ms — output size = {outSize:N0} bytes");
        roundTripOk = true;
    }
    catch (Exception ex)
    {
        var inner = ex.InnerException ?? ex;
        Console.WriteLine($"    *** FAIL during read/write: {inner.GetType().Name}: {inner.Message}");
    }

    // Always emit the input-side byte scan — independent of round-trip
    // success, this tells us whether vanilla Skyrim.esm even contains VMAD
    // subrecords in the SPEL/OTFT groups (a separate diagnostic from the
    // API-surface verdict above).
    foreach (var (label, friendlyName) in new[] { ("SPEL", "Spell"), ("OTFT", "Outfit") })
    {
        var (srcCount, srcBytes, srcFound) = CountVmadSigInGroup(SkyrimEsmForBatch7, label);
        Console.WriteLine($"  {label} group VMAD signature scan (vanilla Skyrim.esm):");
        Console.WriteLine($"    source ({(srcFound ? srcBytes.ToString("N0") + " bytes" : "GROUP NOT FOUND")}): {srcCount} VMAD occurrence(s)");
        if (roundTripOk)
        {
            var (outCount, outBytes, outFound) = CountVmadSigInGroup(roundTripOutput, label);
            Console.WriteLine($"    output ({(outFound ? outBytes.ToString("N0") + " bytes" : "GROUP NOT FOUND")}): {outCount} VMAD occurrence(s)");
            string verdictRT;
            if (srcCount == 0 && outCount == 0)
                verdictRT = $"INCONCLUSIVE — vanilla Skyrim.esm has no VMAD signature in {label} group; round-trip can't disambiguate";
            else if (srcCount > 0 && outCount == 0)
                verdictRT = $"Case (A) CONFIRMED for {friendlyName} — Mutagen drops VMAD on round-trip ({srcCount} → 0)";
            else if (srcCount > 0 && outCount == srcCount)
                verdictRT = $"Case (C) SUSPECTED for {friendlyName} — Mutagen preserves VMAD count on round-trip ({srcCount} → {outCount}) via non-property mechanism";
            else
                verdictRT = $"PARTIAL for {friendlyName} — VMAD count changed ({srcCount} → {outCount}); investigate (compression, opaque preservation of subset, etc.)";
            Console.WriteLine($"  >>> {label} round-trip verdict: {verdictRT}");
        }
        else
        {
            Console.WriteLine($"    output: SKIPPED (round-trip write failed; see error above — Mutagen rejects whole-Skyrim.esm round-trip due to master/lower-range invariants)");
            if (srcCount == 0)
                Console.WriteLine($"  >>> {label} input-scan verdict: vanilla Skyrim.esm contains 0 VMAD in {label} group — consumers cannot encounter the preservation question on vanilla data for this record type.");
            else
                Console.WriteLine($"  >>> {label} input-scan verdict: vanilla Skyrim.esm contains {srcCount} VMAD occurrence(s) in {label} group — preservation under round-trip is unanswered (write failed). API-surface evidence (above: 0 VMAD-shaped properties on typeof({friendlyName})) rules out reflection access regardless.");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// v2.8 Phase 4 item 2 — Cross-check VMAD reflection probe on every type
// currently in tools_patching.py:104's attach_scripts supported list.
//
// Phase 2 found Outfit + Spell were schema overstatements (Case A, item 1
// confirmed). This cross-check ensures no other type in the supported list is
// a similar overstatement — for each of {NPC, Quest, Armor, Weapon, Container,
// Door, Activator, Furniture, Light, MagicEffect, Perk}, confirm that
// reflection finds a VirtualMachineAdapter property reachable via the
// concrete class, an interface, or the base-class chain. Anything that comes
// back null is a docs-edit candidate to fold into item 3 inline (or, if many
// surface, halt-and-report for scope decision).
// ═══════════════════════════════════════════════════════════════════════════

Section("v2.8 P4 — Cross-check VMAD reflection probe on every attach_scripts supported type");

var attachScriptsCrossCheckTypes = new (string label, Type t)[]
{
    ("Npc",         typeof(Npc)),
    ("Quest",       typeof(Quest)),
    ("Armor",       typeof(Armor)),
    ("Weapon",      typeof(Weapon)),
    ("Container",   typeof(Container)),
    ("Door",        typeof(Door)),
    ("Activator",   typeof(SkActivator)),
    ("Furniture",   typeof(Furniture)),
    ("Light",       typeof(Light)),
    ("MagicEffect", typeof(MagicEffect)),
    ("Perk",        typeof(Perk)),
};

var crossCheckResults = new List<(string label, bool hasVmad, string evidence)>();
foreach (var (label, t) in attachScriptsCrossCheckTypes)
{
    var p = t.GetProperty("VirtualMachineAdapter",
        BindingFlags.Public | BindingFlags.Instance);
    if (p != null)
    {
        var declared = FriendlyType(p.PropertyType);
        crossCheckResults.Add((label, true, $"declared type = {declared}"));
        Console.WriteLine($"  typeof({label,-12}) → VirtualMachineAdapter PRESENT, declared type = {declared}");
        continue;
    }

    // Fall back to interface/base-chain walk (Phase 2 disambiguator pattern).
    var hits = new List<string>();
    foreach (var i in t.GetInterfaces())
    {
        var ip = i.GetProperty("VirtualMachineAdapter",
            BindingFlags.Public | BindingFlags.Instance);
        if (ip != null) hits.Add($"interface {FriendlyType(i)}");
    }
    var bt = t.BaseType;
    while (bt != null && bt != typeof(object))
    {
        var bp = bt.GetProperty("VirtualMachineAdapter",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (bp != null) hits.Add($"base {FriendlyType(bt)}");
        bt = bt.BaseType;
    }
    if (hits.Count > 0)
    {
        crossCheckResults.Add((label, true, $"via {string.Join(", ", hits)}"));
        Console.WriteLine($"  typeof({label,-12}) → VirtualMachineAdapter via {string.Join(", ", hits)}");
    }
    else
    {
        crossCheckResults.Add((label, false, "<not reachable by reflection>"));
        Console.WriteLine($"  typeof({label,-12}) → *** OVERSTATEMENT *** no VirtualMachineAdapter reachable by reflection");
    }
}

// Probe inheritance hierarchy of VirtualMachineAdapter / PerkAdapter / QuestAdapter
// to find a common base/interface for the bridge cast. Item 4 fix attempt 1
// assumed PerkAdapter : VirtualMachineAdapter; the runtime says otherwise.
foreach (var label in new[] { "VirtualMachineAdapter", "PerkAdapter", "QuestAdapter" })
{
    var t = typeof(ISkyrimMod).Assembly.GetType($"Mutagen.Bethesda.Skyrim.{label}");
    if (t == null) { Console.WriteLine($"  NOT FOUND: {label}"); continue; }
    Console.WriteLine($"  {label} — base chain:");
    var bt = t;
    while (bt != null)
    {
        Console.WriteLine($"    {FriendlyType(bt)}");
        bt = bt.BaseType;
    }
    Console.WriteLine($"  {label} — implemented interfaces:");
    foreach (var i in t.GetInterfaces().OrderBy(i => i.FullName))
        Console.WriteLine($"    {FriendlyType(i)}");
}

int overstatementCount = crossCheckResults.Count(r => !r.hasVmad);
Console.WriteLine($"  Cross-check summary: {crossCheckResults.Count} types probed, {overstatementCount} overstatement(s)");
if (overstatementCount == 0)
{
    Console.WriteLine($"  >>> Cross-check verdict: schema list is accurate post-Outfit/Spell removal. No further docs corrections needed.");
}
else
{
    Console.WriteLine($"  Overstatements (need removal from tools_patching.py:104):");
    foreach (var r in crossCheckResults.Where(r => !r.hasVmad))
        Console.WriteLine($"    - {r.label}");
    Console.WriteLine($"  >>> Cross-check verdict: {overstatementCount} additional schema overstatement(s) found.");
}

Section("v2.8 P2 Batch 7 — PerkAdapter / QuestAdapter functional probe");

if (!File.Exists(SkyrimEsmForBatch7))
{
    Console.WriteLine($"  SKIP: Skyrim.esm not found at {SkyrimEsmForBatch7}");
}
else
{
    var thisDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var bridgeExe = Path.GetFullPath(Path.Combine(thisDir,
        "..", "..", "..", "..", "mutagen-bridge", "bin", "Release", "net8.0", "mutagen-bridge.exe"));
    if (!File.Exists(bridgeExe))
    {
        Console.WriteLine($"  SKIP: mutagen-bridge.exe not found at {bridgeExe}");
    }
    else
    {
        Console.WriteLine($"  bridge: {bridgeExe}");
        Console.WriteLine($"  source: {SkyrimEsmForBatch7}");

        // Local helper: invoke bridge.
        (string stdout, string stderr, int exit) RunBridge(string exe, string stdinJson)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var p = System.Diagnostics.Process.Start(psi)!;
            p.StandardInput.Write(stdinJson);
            p.StandardInput.Close();
            var so = p.StandardOutput.ReadToEnd();
            var se = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (so, se, p.ExitCode);
        }

        string FormatFormKey(FormKey fk) => $"{fk.ModKey.FileName}:{fk.ID:X6}";

        var batch7OutDir = Path.Combine(Path.GetTempPath(), "race-probe-batch7");
        Directory.CreateDirectory(batch7OutDir);

        using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);

        // Local helper: probe a single record type.
        void ProbeAdapter(string recordTypeLabel, string expectedSubclassName,
                          FormKey? targetFk,
                          Func<SkyrimMod, FormKey, ISkyrimMajorRecordGetter?> readbackByFormKey)
        {
            Section($"v2.8 P2 Batch 7 — {recordTypeLabel} adapter probe");
            if (targetFk == null)
            {
                Console.WriteLine($"  SKIP: no {recordTypeLabel} w/o VMAD in Skyrim.esm");
                return;
            }
            var fk = targetFk.Value;
            Console.WriteLine($"  Source {recordTypeLabel}: {fk}");
            var outPath = Path.Combine(batch7OutDir, $"{recordTypeLabel.ToLower()}-probe.esp");
            if (File.Exists(outPath)) File.Delete(outPath);

            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-batch7",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = FormatFormKey(fk),
                        source_path = SkyrimEsmForBatch7,
                        attach_scripts = new[]
                        {
                            new { name = "TestScript", properties = Array.Empty<object>() }
                        },
                    }
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };

            var reqJson = System.Text.Json.JsonSerializer.Serialize(req);
            var (stdout, stderr, exit) = RunBridge(bridgeExe, reqJson);
            Console.WriteLine($"  bridge response (exit={exit}):");
            foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

            if (exit != 0 || !File.Exists(outPath))
            {
                Console.WriteLine($"  *** FAIL: bridge call did not produce output ESP (exit={exit}, file exists={File.Exists(outPath)})");
                if (!string.IsNullOrEmpty(stderr)) Console.WriteLine($"  stderr: {stderr}");
                return;
            }

            // CreateFromBinary (not overlay) — full record reconstitution so
            // VMAD subclass runtime type is observable.
            var outMod = SkyrimMod.CreateFromBinary(outPath, SkyrimRelease.SkyrimSE);
            var rec = readbackByFormKey(outMod, fk);
            if (rec == null)
            {
                Console.WriteLine($"  *** FAIL: {recordTypeLabel} record missing in output ESP");
                return;
            }

            var vmadProp = rec.GetType().GetProperty("VirtualMachineAdapter",
                BindingFlags.Public | BindingFlags.Instance);
            if (vmadProp == null)
            {
                Console.WriteLine($"  *** UNEXPECTED: {recordTypeLabel} record exposes no VirtualMachineAdapter property via reflection");
                return;
            }
            var vmad = vmadProp.GetValue(rec);
            if (vmad == null)
            {
                Console.WriteLine($"  *** UNEXPECTED: VirtualMachineAdapter is null after attach_scripts (bridge claimed success)");
                return;
            }

            var typeName = vmad.GetType().Name;
            var fullName = vmad.GetType().FullName;
            Console.WriteLine($"  >>> output.{recordTypeLabel}.VirtualMachineAdapter runtime type: {typeName}");
            Console.WriteLine($"      full name: {fullName}");

            // Report subclass detection. Note: Mutagen wraps records in *BinaryOverlay
            // when read via CreateFromBinaryOverlay, but CreateFromBinary should
            // produce the concrete (non-overlay) type. The expected subclass for
            // PERK is `PerkAdapter`; for QUST is `QuestAdapter`.
            if (typeName == expectedSubclassName)
                Console.WriteLine($"  ✓ {expectedSubclassName} constructed correctly — bug DOES NOT reproduce on this code path");
            else if (typeName.StartsWith("VirtualMachineAdapter"))
                Console.WriteLine($"  ✗ BUG CONFIRMED: bridge constructed base VirtualMachineAdapter instead of {expectedSubclassName}");
            else if (typeName.Contains(expectedSubclassName))
                Console.WriteLine($"  ~ runtime type contains \"{expectedSubclassName}\" (overlay/wrapper variant?) — likely correct");
            else
                Console.WriteLine($"  ? unexpected runtime type \"{typeName}\" for {recordTypeLabel}.VirtualMachineAdapter — investigate");

            // Bonus diagnostic: count Scripts and the runtime type of script element.
            var scriptsProp = vmad.GetType().GetProperty("Scripts",
                BindingFlags.Public | BindingFlags.Instance);
            var scriptsObj = scriptsProp?.GetValue(vmad) as System.Collections.IEnumerable;
            int scriptCount = 0;
            string? firstScriptType = null;
            if (scriptsObj != null)
                foreach (var s in scriptsObj)
                {
                    if (firstScriptType == null && s != null) firstScriptType = s.GetType().Name;
                    scriptCount++;
                }
            Console.WriteLine($"      VMAD.Scripts count: {scriptCount}; first script runtime type: {firstScriptType ?? "<none>"}");
        }

        // PERK probe (4.c.06).
        var perkNoVmad = srcMod.Perks.FirstOrDefault(p => p.VirtualMachineAdapter == null);
        ProbeAdapter("PERK", "PerkAdapter", perkNoVmad?.FormKey,
            (m, fk) => m.Perks.FirstOrDefault(p => p.FormKey == fk));

        // QUST probe (4.c.07).
        var questNoVmad = srcMod.Quests.FirstOrDefault(q => q.VirtualMachineAdapter == null);
        ProbeAdapter("QUST", "QuestAdapter", questNoVmad?.FormKey,
            (m, fk) => m.Quests.FirstOrDefault(q => q.FormKey == fk));
    }
}

// ═══════════════════════════════════════════════════════════════
// v2.9 P1 — ConditionData inventory dump
// ═══════════════════════════════════════════════════════════════
//
// Goal: enumerate every concrete Mutagen.Bethesda.Skyrim.*ConditionData
// subclass with its non-base, non-padding reflection slots, categorized
// by parameter shape (NoParam / Enum / FormLinkOrIndex / MultiSlot /
// PrimitiveOnly / Exotic). Output drives Phase 1's Pareto proposal in
// CONDITIONS_AUDIT.md.
//
// Two filter axes:
//
// 1) Skip-list discipline (DYNAMIC, not PLAN.md's static list).
//    A property is "base" iff its DeclaringType is the abstract
//    ConditionData class or any of its ancestors. Everything declared
//    below the base is function-specific. PLAN.md § Phase 1 step 2's
//    static skip list (RunOnType / Reference / Function / Unknown1/2/3)
//    is logged as a diff target only — disagreement is captured in
//    CONDITIONS_AUDIT.md (per the conductor's mid-halt resolution after
//    the first probe run surfaced the wrong-skip-list issue).
//
// 2) CTDA padding-slot filter (NEW per conductor mid-halt resolution
//    Option C). Mutagen 0.53.1's *ConditionData classes universally
//    expose 4 function-specific properties to mirror CTDA's 4-parameter
//    binary format. Slots a function doesn't actually use are named
//    *Unused*Parameter* (e.g. SecondUnusedIntParameter). They are never
//    set in practice and never appear in user-supplied 'parameters'
//    maps — the dispatcher's reflection lookup ignores them implicitly.
//    Without filtering them out of the categorizer, every function lands
//    in Exotic (their String typing isn't routable per § A) and the
//    per-shape distribution becomes useless for Pareto. We filter by
//    name (contains "Unused") AND record the universal pattern as a
//    statistical roll-up so CONDITIONS_AUDIT.md can document it as a
//    fact for Phase 2's schema-doc text and any future v2.9.x contributor.
//
// Conductor adjudications already inherited (these no longer halt):
//   - Reference is a BASE property (used for RunOnType: Reference mode);
//     GetIsID's actual function-specific slot is "Object" — captured as
//     ARCH NOTE so Phase 2's plan-amend writes itself from this dump.
//   - GetActorValuePercentage doesn't exist; GetActorValuePercent does
//     — dropped from the floor-AV list, captured as ARCH NOTE alongside.

int inventoryFailures = 0;

Section("v2.9 P1 — ConditionData inventory dump");
{
    var asm = typeof(ISkyrimMod).Assembly;
    var conditionDataBase = typeof(ConditionData);
    Console.WriteLine($"  ConditionData base: {conditionDataBase.FullName}  IsAbstract={conditionDataBase.IsAbstract}");

    var concrete = asm.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract
                 && (t.Namespace?.StartsWith("Mutagen.Bethesda.Skyrim") ?? false)
                 && t.Name.EndsWith("ConditionData")
                 && !t.Name.EndsWith("BinaryOverlay")
                 && conditionDataBase.IsAssignableFrom(t))
        .OrderBy(t => t.Name)
        .ToList();

    Console.WriteLine($"  Concrete *ConditionData count: {concrete.Count}");

    // Predicate: a property is "base" iff its DeclaringType is conditionDataBase
    // or any of its ancestors. Function-specific = !base. Indexers filtered.
    bool IsBaseProp(PropertyInfo p)
    {
        if (p.GetIndexParameters().Length > 0) return true;
        if (p.DeclaringType == null) return true;
        if (p.DeclaringType == conditionDataBase) return true;
        return conditionDataBase.IsSubclassOf(p.DeclaringType);
    }

    // CTDA padding-slot filter — Mutagen names unused CTDA params with
    // "Unused" in the property name (FirstUnusedStringParameter,
    // SecondUnusedIntParameter, etc.). Never set in practice; never
    // surfaced in user 'parameters' maps. Filtered from categorization
    // and per-function detail dump. See CONDITIONS_AUDIT.md.
    bool IsPaddingSlot(PropertyInfo p) => p.Name.Contains("Unused");

    // ─── PLAN-vs-dynamic skip-list diff ──────────────────────────────
    var planStaticSkip = new HashSet<string> { "RunOnType", "Reference", "Function", "Unknown1", "Unknown2", "Unknown3" };
    var dynamicBaseProps = conditionDataBase
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetIndexParameters().Length == 0)
        .Select(p => p.Name)
        .ToHashSet();
    var inPlanNotDynamic = planStaticSkip.Except(dynamicBaseProps).OrderBy(s => s).ToList();
    var inDynamicNotPlan = dynamicBaseProps.Except(planStaticSkip).OrderBy(s => s).ToList();

    Console.WriteLine();
    Console.WriteLine($"  Dynamic base props ({dynamicBaseProps.Count}): {string.Join(", ", dynamicBaseProps.OrderBy(s => s))}");
    Console.WriteLine($"  PLAN static skip ({planStaticSkip.Count}): {string.Join(", ", planStaticSkip.OrderBy(s => s))}");
    if (inPlanNotDynamic.Count > 0)
        Console.WriteLine($"  ARCH NOTE: PLAN names as base but dynamic says function-specific (or absent): {string.Join(", ", inPlanNotDynamic)}");
    if (inDynamicNotPlan.Count > 0)
        Console.WriteLine($"  ARCH NOTE: dynamic base has props PLAN didn't list: {string.Join(", ", inDynamicNotPlan)}");

    // ─── GetIsIDConditionData sanity-check anchor ────────────────────
    Console.WriteLine();
    Console.WriteLine("  GetIsIDConditionData anchor — every property annotated [base] / [padding] / [function-specific]:");
    var getIsIdType = asm.GetType("Mutagen.Bethesda.Skyrim.GetIsIDConditionData");
    if (getIsIdType == null)
    {
        Console.WriteLine($"  *** ARCH SURPRISE: GetIsIDConditionData not found in Mutagen 0.53.1 — floor pick is built around it; halt-worthy");
        inventoryFailures++;
    }
    else
    {
        bool referenceFound = false;
        bool referenceIsBase = false;
        bool objectFound = false;
        bool objectIsFunctionSpecific = false;
        Type? objectSlotType = null;
        foreach (var p in getIsIdType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name))
        {
            string tag = IsBaseProp(p) ? "[base]"
                       : IsPaddingSlot(p) ? "[padding]"
                       : "[function-specific]";
            Console.WriteLine($"    {p.Name,-32} {tag,-22} {FriendlyType(p.PropertyType)}  (declared on {p.DeclaringType?.Name})");
            if (p.Name == "Reference")
            {
                referenceFound = true;
                referenceIsBase = IsBaseProp(p);
            }
            if (p.Name == "Object")
            {
                objectFound = true;
                objectIsFunctionSpecific = !IsBaseProp(p) && !IsPaddingSlot(p);
                objectSlotType = p.PropertyType;
            }
        }
        // Reference: conductor adjudicated as a BASE prop (RunOnType: Reference mode).
        // Failures here mean Mutagen schema changed since adjudication — halt-worthy.
        if (!referenceFound)
        {
            Console.WriteLine($"  *** ARCH SURPRISE: GetIsIDConditionData has no 'Reference' property — Mutagen schema rebased since conductor adjudication; halt-worthy");
            inventoryFailures++;
        }
        else if (!referenceIsBase)
        {
            Console.WriteLine($"  *** ARCH SURPRISE: GetIsIDConditionData.Reference is NOT base — conductor adjudication assumed it was; halt-worthy");
            inventoryFailures++;
        }
        else
        {
            Console.WriteLine($"  ARCH NOTE: GetIsIDConditionData.Reference is BASE (RunOnType: Reference-mode dispatch slot, inherited universally); GetIsID's function-specific parameter slot is 'Object'. PLAN.md § Architecture B example needs Phase 2 plan-amend correction. Captured in CONDITIONS_AUDIT.md.");
        }
        // Object: per conductor, the actual GetIsID parameter slot.
        if (!objectFound)
        {
            Console.WriteLine($"  *** ARCH SURPRISE: GetIsIDConditionData has no 'Object' property — slot-name correction unfounded; halt-worthy");
            inventoryFailures++;
        }
        else if (!objectIsFunctionSpecific)
        {
            Console.WriteLine($"  *** ARCH SURPRISE: GetIsIDConditionData.Object is base or padding — slot-name correction wrong; halt-worthy");
            inventoryFailures++;
        }
        else
        {
            Console.WriteLine($"  ARCH NOTE: GetIsIDConditionData.Object is function-specific ({FriendlyType(objectSlotType!)}) — confirmed correct slot for the GetIsID 'parameters' map.");
        }
    }

    // ─── GetEventData re-triage anchor (per conductor mid-halt ask) ──
    // EventFunction + EventMember appear as nested types on
    // GetEventDataConditionData. The absorb-vs-defer call:
    //   - Both System.Enum → absorb (MultiSlot routable, given Phase 2's
    //     IFormLink<T> sub-A extension covers the Record slot).
    //   - Either is a custom Loqui sub-object → defer (would need
    //     chained-slot DSL, explicit OOS per PLAN.md § Carry-overs #4).
    Console.WriteLine();
    Console.WriteLine("  GetEventData re-triage anchor — nested EventFunction / EventMember inspection:");
    var getEventDataType = asm.GetType("Mutagen.Bethesda.Skyrim.GetEventDataConditionData");
    if (getEventDataType == null)
    {
        Console.WriteLine($"    GetEventDataConditionData NOT FOUND in Mutagen 0.53.1");
    }
    else
    {
        foreach (var nestedName in new[] { "EventFunction", "EventMember" })
        {
            var nestedType = getEventDataType.GetNestedType(nestedName);
            if (nestedType == null)
            {
                Console.WriteLine($"    {nestedName,-16} NOT FOUND as nested type on GetEventDataConditionData");
                continue;
            }
            Console.WriteLine($"    {nestedName,-16} FullName={nestedType.FullName}");
            Console.WriteLine($"    {"",-16} IsEnum={nestedType.IsEnum}  BaseType={nestedType.BaseType?.FullName}");
            if (nestedType.IsEnum)
            {
                var values = Enum.GetNames(nestedType);
                var preview = string.Join(", ", values.Take(8));
                var ellipsis = values.Length > 8 ? $", ... ({values.Length} total)" : $" ({values.Length} total)";
                Console.WriteLine($"    {"",-16} Values: {preview}{ellipsis}");
            }
            else
            {
                Console.WriteLine($"    {"",-16} Public properties (top 10):");
                foreach (var p in nestedType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Take(10))
                    Console.WriteLine($"    {"",-18}- {p.Name,-20} {FriendlyType(p.PropertyType)}");
            }
        }
    }

    // ─── Categorization ──────────────────────────────────────────────
    bool IsFormLinkOrIndex(Type t) => t.IsGenericType
        && t.GetGenericTypeDefinition().Name.StartsWith("IFormLinkOrIndex");
    bool IsRoutablePrimitive(Type t) => t == typeof(int) || t == typeof(float) || t == typeof(bool);
    bool IsRoutable(Type t) => IsFormLinkOrIndex(t) || t.IsEnum || IsRoutablePrimitive(t);

    string Categorize(List<PropertyInfo> slots)
    {
        if (slots.Count == 0) return "NoParam";
        if (slots.Any(p => !IsRoutable(p.PropertyType))) return "Exotic";

        bool allEnum = slots.All(p => p.PropertyType.IsEnum);
        bool allPrim = slots.All(p => IsRoutablePrimitive(p.PropertyType));
        bool allFormLink = slots.All(p => IsFormLinkOrIndex(p.PropertyType));

        if (slots.Count == 1)
        {
            if (allFormLink) return "FormLinkOrIndex";
            if (allEnum) return "Enum";
            if (allPrim) return "PrimitiveOnly";
        }
        else
        {
            // PLAN.md § Phase 1 step 2: "Enum — one or more enum-typed slots",
            // "PrimitiveOnly — one or more int/float/bool slots only", but
            // "FormLinkOrIndex — ONE IFormLinkOrIndex<T> slot". So multi-FormLink
            // (rare) goes to MultiSlot, while multi-enum and multi-primitive
            // stay in their single-shape buckets.
            if (allEnum) return "Enum";
            if (allPrim) return "PrimitiveOnly";
            return "MultiSlot"; // multi-FormLink, or any mix of routable shapes
        }
        return "Exotic"; // unreachable
    }

    var perTypeSlots = new Dictionary<Type, List<PropertyInfo>>();
    var byShape = new Dictionary<string, List<string>>
    {
        ["NoParam"] = new(),
        ["Enum"] = new(),
        ["FormLinkOrIndex"] = new(),
        ["MultiSlot"] = new(),
        ["PrimitiveOnly"] = new(),
        ["Exotic"] = new(),
    };

    // Padding-pattern stats for the audit doc (CTDA universal shape).
    int totalPaddingSlotsFiltered = 0;
    var paddingCountHistogram = new Dictionary<int, int>(); // padding-count → # functions
    var usefulCountHistogram = new Dictionary<int, int>();  // useful-count  → # functions
    var nonUniformFunctions = new List<string>();           // total-fn-props != 4 (GetEventData-class outliers)

    foreach (var t in concrete)
    {
        var allFnSpecific = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsBaseProp(p))
            .ToList();
        int paddingCount = allFnSpecific.Count(p => IsPaddingSlot(p));
        int usefulCount = allFnSpecific.Count - paddingCount;
        totalPaddingSlotsFiltered += paddingCount;
        paddingCountHistogram[paddingCount] = paddingCountHistogram.GetValueOrDefault(paddingCount, 0) + 1;
        usefulCountHistogram[usefulCount] = usefulCountHistogram.GetValueOrDefault(usefulCount, 0) + 1;
        if (allFnSpecific.Count != 4)
            nonUniformFunctions.Add($"{t.Name.Substring(0, t.Name.Length - "ConditionData".Length)} ({allFnSpecific.Count} total: {usefulCount} useful + {paddingCount} padding)");

        var slots = allFnSpecific
            .Where(p => !IsPaddingSlot(p))
            .OrderBy(p => p.Name)
            .ToList();
        perTypeSlots[t] = slots;
        var shape = Categorize(slots);
        var fnName = t.Name.Substring(0, t.Name.Length - "ConditionData".Length);
        byShape[shape].Add(fnName);
    }

    Console.WriteLine();
    Console.WriteLine($"  ─── CTDA padding pattern ─────────────────────────");
    Console.WriteLine($"  {totalPaddingSlotsFiltered} *Unused* slots filtered across {concrete.Count} functions");
    Console.WriteLine($"  Padding-slot histogram:");
    foreach (var kv in paddingCountHistogram.OrderBy(kv => kv.Key))
        Console.WriteLine($"    {kv.Value,4} functions have {kv.Key} padding slot(s)");
    Console.WriteLine($"  Useful-slot histogram:");
    foreach (var kv in usefulCountHistogram.OrderBy(kv => kv.Key))
        Console.WriteLine($"    {kv.Value,4} functions have {kv.Key} useful slot(s)");
    if (nonUniformFunctions.Count > 0)
    {
        Console.WriteLine($"  Non-uniform function-property count (breaks the 4-property universal shape):");
        foreach (var fn in nonUniformFunctions)
            Console.WriteLine($"    {fn}");
    }

    Console.WriteLine();
    Console.WriteLine("  ─── Per-shape summary (post-filter) ──────────────");
    foreach (var shape in new[] { "NoParam", "Enum", "FormLinkOrIndex", "MultiSlot", "PrimitiveOnly", "Exotic" })
        Console.WriteLine($"  {shape,-18} count={byShape[shape].Count,3}");

    Console.WriteLine();
    foreach (var shape in new[] { "NoParam", "Enum", "FormLinkOrIndex", "MultiSlot", "PrimitiveOnly", "Exotic" })
    {
        if (byShape[shape].Count == 0) continue;
        Console.WriteLine($"  ─── {shape} ({byShape[shape].Count}) ────");
        foreach (var fn in byShape[shape].OrderBy(s => s))
            Console.WriteLine($"    {fn}");
    }

    // ─── Full slot detail per shape (post-filter, all non-NoParam) ───
    // Phase 2's coverage-smoke + dispatcher-validation reads these
    // signatures directly from this scratch file; they are the
    // authoritative source-of-truth for the v2.9.0 in-scope function set.
    // NoParam functions are deliberately omitted (no slots to dispatch).
    foreach (var shape in new[] { "Enum", "FormLinkOrIndex", "MultiSlot", "PrimitiveOnly" })
    {
        if (byShape[shape].Count == 0) continue;
        Console.WriteLine();
        Console.WriteLine($"  ─── {shape} full slot detail ({byShape[shape].Count}) ────");
        foreach (var fn in byShape[shape].OrderBy(s => s))
        {
            var t = asm.GetType($"Mutagen.Bethesda.Skyrim.{fn}ConditionData");
            if (t == null) continue;
            var slots = perTypeSlots[t];
            Console.WriteLine($"    {fn} ({slots.Count} useful slot(s)):");
            foreach (var p in slots)
                Console.WriteLine($"      - {p.Name,-32} {FriendlyType(p.PropertyType)}");
        }
    }

    // ─── Floor + stretch detailed slot signatures ────────────────────
    // GetActorValuePercentage dropped per conductor mid-halt resolution
    // (doesn't exist in Mutagen 0.53.1; GetActorValuePercent is the
    // canonical name and is already in this list).
    Console.WriteLine();
    Console.WriteLine("  ─── Floor + stretch detailed slot signatures (post-filter) ────");
    var floorAndStretch = new[]
    {
        ("FLOOR",    "GetIsID"),
        ("FLOOR",    "GetInFaction"),
        ("FLOOR",    "GetInCell"),
        ("FLOOR",    "HasMagicEffect"),
        ("FLOOR",    "HasPerk"),
        ("FLOOR",    "HasSpell"),
        ("FLOOR",    "GetIsRace"),
        ("FLOOR-AV", "GetActorValue"),
        ("FLOOR-AV", "GetBaseActorValue"),
        ("FLOOR-AV", "GetActorValuePercent"),
        ("STRETCH",  "GetItemCount"),
        ("STRETCH",  "IsInList"),
        ("STRETCH",  "WornHasKeyword"),
        ("STRETCH",  "GetEquipped"),
    };
    foreach (var (band, fn) in floorAndStretch)
    {
        var typeName = $"Mutagen.Bethesda.Skyrim.{fn}ConditionData";
        var t = asm.GetType(typeName);
        if (t == null)
        {
            Console.WriteLine($"  [{band,-8}] {fn,-28} *** NOT FOUND in Mutagen 0.53.1");
            continue;
        }
        var slots = perTypeSlots.TryGetValue(t, out var s) ? s : new List<PropertyInfo>();
        var shape = Categorize(slots);
        Console.WriteLine($"  [{band,-8}] {fn,-28} shape={shape,-15} useful_slots={slots.Count}");
        foreach (var p in slots)
            Console.WriteLine($"               - {p.Name,-20} {FriendlyType(p.PropertyType)}");
    }

    // ─── Exotic detail dump (these need Aaron's call) ────────────────
    if (byShape["Exotic"].Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  ─── Exotic shape detail (post-filter, {byShape["Exotic"].Count}) — pre-auth absorbability check needed ────");
        foreach (var fn in byShape["Exotic"].OrderBy(s => s))
        {
            var t = asm.GetType($"Mutagen.Bethesda.Skyrim.{fn}ConditionData");
            if (t == null) continue;
            var slots = perTypeSlots[t];
            Console.WriteLine($"    {fn} ({slots.Count} useful slot(s)):");
            foreach (var p in slots)
                Console.WriteLine($"      - {p.Name,-20} {FriendlyType(p.PropertyType)}");
        }
    }
}

// ─── v2.9 P2A — dispatcher functional probes (Mutagen-direct, in-process) ───
//
// Per Phase 2A kickoff "Race-probe per-function functional probes" deliverable:
// 5 representative functions across IFormLinkOrIndex<T> + IFormLink<T> branches
// covering different inner-T shapes, plus a footgun-guard probe. These are
// in-process Mutagen API-surface checks; bridge subprocess round-trip coverage
// lives in coverage-smoke (119 positives + 14 negatives/edges).
//
// Each probe: construct *ConditionData → simulate RouteParameterSlot's logic
// inline (FormLinkOrIndex<T>(parent, formKey) for FLI; FormLink<T>(formKey)
// for IFormLink<T> branch) → reflect the slot back → assert FormKey matches.
// No file I/O — purely validates that Mutagen 0.53.1's reflection contract
// holds for the dispatcher's two write patterns.

Console.WriteLine();
Console.WriteLine("=== v2.9 P2A — dispatcher functional probes (in-process Mutagen-direct) ===");
int p2aFailures = 0;

void ProbeFLI(string functionName, string slotName, FormKey expectedKey)
{
    var typeName = $"Mutagen.Bethesda.Skyrim.{functionName}ConditionData";
    var t = typeof(IConditionData).Assembly.GetType(typeName);
    if (t == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: type {typeName} not found"); p2aFailures++; return; }
    var condData = (Mutagen.Bethesda.Skyrim.ConditionData)System.Activator.CreateInstance(t)!;
    var prop = t.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
    if (prop == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: no {slotName} property on {typeName}"); p2aFailures++; return; }
    if (!prop.PropertyType.IsGenericType ||
        !prop.PropertyType.GetGenericTypeDefinition().Name.StartsWith("IFormLinkOrIndex"))
    { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName} is {prop.PropertyType.Name}, not IFormLinkOrIndex<T>"); p2aFailures++; return; }
    var inner = prop.PropertyType.GetGenericArguments()[0];
    var concreteType = typeof(Mutagen.Bethesda.Plugins.FormLinkOrIndex<>).MakeGenericType(inner);
    var inst = System.Activator.CreateInstance(concreteType, new object[] { condData, expectedKey });
    prop.SetValue(condData, inst);
    var readBack = prop.GetValue(condData);
    var linkProp = readBack!.GetType().GetProperty("Link", BindingFlags.Public | BindingFlags.Instance);
    var linkVal = linkProp!.GetValue(readBack);
    var fkProp = linkVal!.GetType().GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance);
    var fkVal = fkProp!.GetValue(linkVal);
    if (!expectedKey.Equals(fkVal))
    { Console.WriteLine($"  [{functionName,-30}] FAIL: expected {expectedKey}, got {fkVal}"); p2aFailures++; return; }
    Console.WriteLine($"  [{functionName,-30}] PASS  FLI {slotName}<{inner.Name}> round-trip ✓");
}

void ProbeIFormLink(string functionName, string slotName, FormKey expectedKey)
{
    var typeName = $"Mutagen.Bethesda.Skyrim.{functionName}ConditionData";
    var t = typeof(IConditionData).Assembly.GetType(typeName);
    if (t == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: type {typeName} not found"); p2aFailures++; return; }
    var condData = (Mutagen.Bethesda.Skyrim.ConditionData)System.Activator.CreateInstance(t)!;
    var prop = t.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
    if (prop == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: no {slotName} property on {typeName}"); p2aFailures++; return; }
    if (!prop.PropertyType.IsGenericType ||
        prop.PropertyType.GetGenericTypeDefinition() != typeof(Mutagen.Bethesda.Plugins.IFormLink<>))
    { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName} is {prop.PropertyType.Name}, not IFormLink<T>"); p2aFailures++; return; }
    var inner = prop.PropertyType.GetGenericArguments()[0];
    var concreteType = typeof(Mutagen.Bethesda.Plugins.FormLink<>).MakeGenericType(inner);
    var inst = System.Activator.CreateInstance(concreteType, expectedKey);
    prop.SetValue(condData, inst);
    var readBack = prop.GetValue(condData);
    // IFormLink<T> exposes FormKey directly (no .Link wrapper).
    var fkProp = readBack!.GetType().GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance);
    var fkVal = fkProp!.GetValue(readBack);
    if (!expectedKey.Equals(fkVal))
    { Console.WriteLine($"  [{functionName,-30}] FAIL: expected {expectedKey}, got {fkVal}"); p2aFailures++; return; }
    Console.WriteLine($"  [{functionName,-30}] PASS  IFormLink<{inner.Name}> round-trip ✓");
}

void ProbeFootgunGuard(string functionName, string paddingSlotName)
{
    // Replicate dispatcher's footgun-guard logic — slot names containing
    // "Unused" must be rejected even though reflection lookup would succeed
    // (CTDA padding mirror of 4-parameter binary format). Per CONDITIONS_AUDIT.md
    // § Architectural surprises §3.
    bool guardFires = paddingSlotName.Contains("Unused", StringComparison.Ordinal);
    if (!guardFires)
    { Console.WriteLine($"  [footgun-guard ({paddingSlotName,-25})] FAIL: guard didn't recognize padding pattern"); p2aFailures++; return; }
    // Confirm Mutagen also exposes the slot via reflection (so the guard is
    // load-bearing — without it, this slot WOULD be writable through the
    // dispatcher and silently land on padding).
    var typeName = $"Mutagen.Bethesda.Skyrim.{functionName}ConditionData";
    var t = typeof(IConditionData).Assembly.GetType(typeName);
    var prop = t?.GetProperty(paddingSlotName, BindingFlags.Public | BindingFlags.Instance);
    if (prop == null)
    { Console.WriteLine($"  [footgun-guard ({paddingSlotName,-25})] PASS (no-op — Mutagen doesn't expose {paddingSlotName} on {functionName}; guard would still fire on the name pattern alone)"); return; }
    Console.WriteLine($"  [footgun-guard ({paddingSlotName,-25})] PASS  guard recognizes *Unused* + Mutagen exposes the slot (load-bearing) ✓");
}

// 5 representative probes — one per inner-T shape variation.
var probeKey = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x0001A6E8);
ProbeFLI("GetIsID",       "Object",      probeKey);  // IFormLinkOrIndex<IReferenceableObjectGetter>
ProbeFLI("HasMagicEffect","MagicEffect", probeKey);  // IFormLinkOrIndex<IMagicEffectGetter>
ProbeFLI("GetInFaction",  "Faction",     probeKey);  // IFormLinkOrIndex<IFactionGetter>
ProbeIFormLink("GetVATSValueWeapon", "Value", probeKey);  // IFormLink<IWeaponGetter>
ProbeIFormLink("GetVATSValueTarget", "Value", probeKey);  // IFormLink<INpcGetter>
ProbeFootgunGuard("GetIsID", "SecondUnusedIntParameter");
ProbeFootgunGuard("GetIsID", "FirstUnusedStringParameter");

Console.WriteLine($"=== v2.9 P2A probes: {(p2aFailures == 0 ? "ALL PASS" : $"{p2aFailures} FAILURE(S)")} ===");

// ─── v2.9 P2B — Enum dispatcher functional probes (Mutagen-direct, in-process) ───
//
// Three representative Enum probes covering the dispatcher's reflection path
// across enum-size variation: large (ActorValue, ~100 members), small
// (MaleFemaleGender, 2 members), tiny (Axis, 3 members). Each probe simulates
// RouteParameterSlot's Enum branch logic inline (Enum.Parse with ignoreCase: true,
// reflection setter, readback) → asserts the slot's value matches the chosen
// member and is NOT default index 0. Complements coverage-smoke's bridge-
// subprocess Enum cells (Tests 295–338).

Console.WriteLine();
Console.WriteLine("=== v2.9 P2B — Enum dispatcher functional probes (in-process Mutagen-direct) ===");
int p2bFailures = 0;

void ProbeEnum(string functionName, string slotName, string targetName)
{
    var typeName = $"Mutagen.Bethesda.Skyrim.{functionName}ConditionData";
    var t = typeof(IConditionData).Assembly.GetType(typeName);
    if (t == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: type {typeName} not found"); p2bFailures++; return; }
    var condData = (Mutagen.Bethesda.Skyrim.ConditionData)System.Activator.CreateInstance(t)!;
    var prop = t.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
    if (prop == null) { Console.WriteLine($"  [{functionName,-30}] FAIL: no {slotName} property on {typeName}"); p2bFailures++; return; }
    if (!prop.PropertyType.IsEnum)
    { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName} is {prop.PropertyType.Name}, not an enum"); p2bFailures++; return; }
    var enumType = prop.PropertyType;
    object parsed;
    try { parsed = Enum.Parse(enumType, targetName, ignoreCase: true); }
    catch (Exception ex) { Console.WriteLine($"  [{functionName,-30}] FAIL: Enum.Parse('{targetName}') threw: {ex.Message}"); p2bFailures++; return; }
    prop.SetValue(condData, parsed);
    var readBack = prop.GetValue(condData);
    var readBackName = readBack?.ToString() ?? "<null>";
    if (readBackName != targetName)
    { Console.WriteLine($"  [{functionName,-30}] FAIL: readback was '{readBackName}', expected '{targetName}'"); p2bFailures++; return; }
    var memberCount = Enum.GetValues(enumType).Length;
    Console.WriteLine($"  [{functionName,-30}] PASS  Enum {slotName}<{enumType.Name}> ({memberCount} members) round-trip → {readBackName} ✓");
}

// Three probes spanning enum-size: large / small / tiny.
// ActorValue uses Magicka — well-known Skyrim ActorValue, distinct from
// Test 290's Health and Test 287's Stamina to keep the canary cells visually
// distinguishable in the trace.
ProbeEnum("GetActorValue", "ActorValue",       "Magicka");           // ~100 members (ActorValue)
ProbeEnum("GetIsSex",      "MaleFemaleGender", "Female");            // 2 members (MaleFemaleGender)
ProbeEnum("GetAngle",      "Axis",             "Z");                 // 3 members (Axis)

Console.WriteLine($"=== v2.9 P2B probes: {(p2bFailures == 0 ? "ALL PASS" : $"{p2bFailures} FAILURE(S)")} ===");

// ─── v2.9 P2C — MultiSlot dispatcher functional probes (Mutagen-direct, in-process) ───
//
// Four representative MultiSlot probes covering the dispatcher's per-slot
// composition path across shape combinations:
//   - GetEventData (3-slot mixed-shape: 2 nested System.Enum + 1 IFormLink<T>)
//     — the 3-slot canary; exercises 2B Enum branch + 2A sub-A IFormLink<T> in
//     a single composition.
//   - GetStageDone (FLI + Int32) — Layer 2.01 canonical multi-slot; FLI 2A +
//     Int32 P2C-new.
//   - GetWithinDistance (Single + FLI) — Single P2C-new representative; only
//     Single-bearing function in v2.9.0 in-scope set.
//   - GetRelativeAngle (Enum + FLI) — Axis enum + IPlacedSimple FLI; covers
//     Enum + FLI mixed without the IFormLink/3-slot complexity of GetEventData.
//
// Each probe simulates RouteParameterSlot's per-slot dispatch inline — for
// each slot, picks the right branch (FormLinkOrIndex<T> ctor / FormLink<T>
// ctor / Enum.Parse / direct Int32 / direct Single), reflectively writes the
// value, reads back, asserts. No bridge subprocess — independent verification
// that the per-slot reflection write is round-trip-stable at the Mutagen-direct
// layer. Complements coverage-smoke's bridge-subprocess MultiSlot cells (Tests
// 339–370).

Console.WriteLine();
Console.WriteLine("=== v2.9 P2C — MultiSlot dispatcher functional probes (in-process Mutagen-direct) ===");
int p2cFailures = 0;

void ProbeMultiSlot(string functionName, params (string Slot, object Value)[] slotInputs)
{
    var typeName = $"Mutagen.Bethesda.Skyrim.{functionName}ConditionData";
    var t = typeof(IConditionData).Assembly.GetType(typeName);
    if (t == null)
    {
        Console.WriteLine($"  [{functionName,-30}] FAIL: type {typeName} not found");
        p2cFailures++; return;
    }
    var condData = (Mutagen.Bethesda.Skyrim.ConditionData)System.Activator.CreateInstance(t)!;

    var perSlotTraces = new System.Collections.Generic.List<string>();
    foreach (var (slotName, value) in slotInputs)
    {
        var prop = t.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        { Console.WriteLine($"  [{functionName,-30}] FAIL: no {slotName} property on {t.Name}"); p2cFailures++; return; }
        var pt = prop.PropertyType;

        if (pt.IsGenericType && pt.GetGenericTypeDefinition().Name.StartsWith("IFormLinkOrIndex"))
        {
            if (value is not FormKey fk)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<FLI> input not FormKey ({value?.GetType().Name})"); p2cFailures++; return; }
            var inner = pt.GetGenericArguments()[0];
            var concreteType = typeof(Mutagen.Bethesda.Plugins.FormLinkOrIndex<>).MakeGenericType(inner);
            var inst = System.Activator.CreateInstance(concreteType, new object[] { condData, fk });
            prop.SetValue(condData, inst);
            var readBack = prop.GetValue(condData);
            var linkVal = readBack!.GetType().GetProperty("Link")!.GetValue(readBack);
            var fkVal = linkVal!.GetType().GetProperty("FormKey")!.GetValue(linkVal);
            if (!fk.Equals(fkVal))
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<FLI> expected {fk}, got {fkVal}"); p2cFailures++; return; }
            perSlotTraces.Add($"{slotName}<FLI>={fk}");
        }
        else if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(Mutagen.Bethesda.Plugins.IFormLink<>))
        {
            if (value is not FormKey fk)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<IFormLink> input not FormKey"); p2cFailures++; return; }
            var inner = pt.GetGenericArguments()[0];
            var concreteType = typeof(Mutagen.Bethesda.Plugins.FormLink<>).MakeGenericType(inner);
            var inst = System.Activator.CreateInstance(concreteType, fk);
            prop.SetValue(condData, inst);
            var readBack = prop.GetValue(condData);
            var fkVal = readBack!.GetType().GetProperty("FormKey")!.GetValue(readBack);
            if (!fk.Equals(fkVal))
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<IFormLink> expected {fk}, got {fkVal}"); p2cFailures++; return; }
            perSlotTraces.Add($"{slotName}<IFormLink>={fk}");
        }
        else if (pt.IsEnum)
        {
            if (value is not string enumName)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Enum> input not string"); p2cFailures++; return; }
            object parsed;
            try { parsed = Enum.Parse(pt, enumName, ignoreCase: true); }
            catch (Exception ex)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: Enum.Parse({slotName}, '{enumName}') threw: {ex.Message}"); p2cFailures++; return; }
            prop.SetValue(condData, parsed);
            var readBack = prop.GetValue(condData);
            // Use lower-32-bit comparison per 2B forward-carry (handles MiscStatEnum-style
            // sign-extension if a hash-encoded enum surfaces in MultiSlot scope).
            long sentBits = Convert.ToInt64(parsed);
            long readbackBits = Convert.ToInt64(readBack);
            if (unchecked((uint)sentBits) != unchecked((uint)readbackBits))
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Enum> bit mismatch ('{enumName}' vs readback '{readBack}')"); p2cFailures++; return; }
            perSlotTraces.Add($"{slotName}<Enum>={enumName}");
        }
        else if (pt == typeof(int))
        {
            if (value is not int intVal)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Int32> input not int"); p2cFailures++; return; }
            prop.SetValue(condData, intVal);
            var readBack = prop.GetValue(condData);
            if (readBack is not int readBackInt || readBackInt != intVal)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Int32> readback was '{readBack}', expected {intVal}"); p2cFailures++; return; }
            perSlotTraces.Add($"{slotName}<Int32>={intVal}");
        }
        else if (pt == typeof(float))
        {
            if (value is not float floatVal)
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Single> input not float"); p2cFailures++; return; }
            prop.SetValue(condData, floatVal);
            var readBack = prop.GetValue(condData);
            // Bit-exact comparison via SingleToInt32Bits per 2B forward-carry — handles
            // NaN / sub-normal edge cases (uncommon for Skyrim conditions but cheap to
            // guard against here as a v2.9.x stability anchor).
            if (readBack is not float readBackFloat
                || BitConverter.SingleToInt32Bits(readBackFloat) != BitConverter.SingleToInt32Bits(floatVal))
            { Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName}<Single> readback was '{readBack}', expected {floatVal} (bit-exact)"); p2cFailures++; return; }
            perSlotTraces.Add($"{slotName}<Single>={floatVal}");
        }
        else
        {
            Console.WriteLine($"  [{functionName,-30}] FAIL: {slotName} has unsupported type {pt.FullName}");
            p2cFailures++; return;
        }
    }
    Console.WriteLine($"  [{functionName,-30}] PASS  MultiSlot {slotInputs.Length}-slot: {string.Join(" | ", perSlotTraces)} ✓");
}

// 4 representative MultiSlot probes spanning the dispatcher's branches.
var probeMultiKey = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x0001A6E8);
ProbeMultiSlot("GetEventData",
    ("Function", (object)"GetIsID"),
    ("Member", (object)"Form"),
    ("Record", (object)probeMultiKey));                                   // 2 nested Enum + 1 IFormLink (3-slot canary)
ProbeMultiSlot("GetStageDone",
    ("Quest", (object)probeMultiKey),
    ("Stage", (object)50));                                               // FLI + Int32 (Layer 2.01 canonical)
ProbeMultiSlot("GetWithinDistance",
    ("Distance", (object)1024.0f),
    ("Target", (object)probeMultiKey));                                   // Single + FLI (only Single-bearing function)
ProbeMultiSlot("GetRelativeAngle",
    ("Axis", (object)"Z"),
    ("Target", (object)probeMultiKey));                                   // Enum + FLI (Axis matches P2B probe)

Console.WriteLine($"=== v2.9 P2C probes: {(p2cFailures == 0 ? "ALL PASS" : $"{p2cFailures} FAILURE(S)")} ===");

// ─── v2.9 P2D — PrimitiveOnly dispatcher functional probe (Mutagen-direct, in-process) ───
// 1 representative Int32-only PrimitiveOnly probe for archival completeness.
// P2C's ProbeMultiSlot already covers Int32 (via GetStageDone.Stage), so this
// is forms-completeness rigor — the dispatcher branch is the same code path
// regardless of caller function. IsLimbGone picked (recognizable, distinct from
// Test 371's GetIsAliasRef canary; 1-slot Int32 PrimitiveOnly).
// Failure attribution: ProbeMultiSlot bumps p2cFailures internally; we delta-
// track P2D's contribution and unwind p2cFailures so the per-phase scoreboard
// stays clean across the totalFailures rollup.
int p2dFailures = 0;
{
    int p2cBefore = p2cFailures;
    Console.WriteLine();
    Console.WriteLine("=== v2.9 P2D — PrimitiveOnly dispatcher functional probe (in-process Mutagen-direct) ===");
    ProbeMultiSlot("IsLimbGone",
        ("Limb", (object)42));                                                // 1-slot Int32 (PrimitiveOnly representative)
    p2dFailures = p2cFailures - p2cBefore;
    p2cFailures = p2cBefore;
}
Console.WriteLine($"=== v2.9 P2D probes: {(p2dFailures == 0 ? "ALL PASS" : $"{p2dFailures} FAILURE(S)")} ===");

// ─── v2.9 P4-INFO — INFO override regression (Mutagen-direct via bridge subprocess) ───
//
// Replaces Phase 4's deferred-state architectural archaeology block. Phase 4-INFO
// landed the bridge fix: parent-topic resolution (linear scan via sourceMod.DialogTopics)
// + child-response find-by-FormKey path through CopyAsOverride (signature now
// threads sourceMod) + symmetric INFO removal in TryRemoveOverride.
//
// Reflection at sub-session start refuted "Approach C — direct parent-topic getter
// on IDialogResponsesGetter" (no .ParentTopic property; the .Topic IFormLinkNullable
// field exists but its semantics for "true parent topic of this response" are
// undocumented and ambiguous vs WalkAwayTopic / LinkTo). Approach A (linear scan)
// chosen as deterministic primary path — single override per call, no caching needed.
//
// Probe shape: bridge subprocess → patch INFO Skyrim.esm:000E3D (MQ101 Helgen-escape
// dialog, Phase 3 Scenario 3.1 carrier) with one `add_conditions` entry: GetIsID +
// parameters: {Object: Skyrim.esm:02BF9F (Hadvar)}. Asserts: bridge success=true,
// output ESP exists, override INFO carries the new condition, the GetIsID Object
// slot resolves to Hadvar's FormKey (NOT default 0). Mirrors Scenario 3.1's exact
// shape — Phase 5 re-runs Scenario 3.1 against the live install + this probe + the
// 1.P.GetIsID.INFO coverage-smoke cell as a triple-anchor regression.
//
// FAIL→PASS pattern: pre-fix (Phase 4 archaeology baseline; CopyAsOverride switch
// lacks the IDialogResponsesGetter branch) → bridge returns success=false with
// "Could not create override for INFO" per-record error → probe records FAIL.
// Post-fix (this section, after Items 1b/1c land) → success=true + override INFO
// carries the new condition + slot resolved → probe records PASS.
Section("v2.9 P4-INFO — INFO override regression (bridge subprocess)");

int p4InfoFailures = 0;
{
    var thisDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var bridgeExeP4i = Path.GetFullPath(Path.Combine(thisDir,
        "..", "..", "..", "..", "mutagen-bridge", "bin", "Release", "net8.0", "mutagen-bridge.exe"));
    if (!File.Exists(bridgeExeP4i))
    {
        Console.WriteLine($"  SKIP: mutagen-bridge.exe not found at {bridgeExeP4i}");
    }
    else if (!File.Exists(SkyrimEsmForBatch7))
    {
        Console.WriteLine($"  SKIP: Skyrim.esm not found at {SkyrimEsmForBatch7}");
    }
    else
    {
        var infoFkStr = "Skyrim.esm:000E3D";
        var hadvarFkStr = "Skyrim.esm:02BF9F";
        var infoFk = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x000E3D);
        var hadvarFk = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x02BF9F);

        Console.WriteLine($"  bridge:  {bridgeExeP4i}");
        Console.WriteLine($"  source:  {SkyrimEsmForBatch7}");
        Console.WriteLine($"  carrier: INFO {infoFkStr} (MQ101 Helgen dialog, Scenario 3.1 record)");
        Console.WriteLine($"  append:  GetIsID Object={hadvarFkStr} (Hadvar — distinct from source GetIsID slots)");

        var outDir = Path.Combine(Path.GetTempPath(), "race-probe-p4-info");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "p4-info-regression.esp");
        if (File.Exists(outPath)) File.Delete(outPath);

        var req = new
        {
            command = "patch",
            output_path = outPath,
            esl_flag = false,
            author = "race-probe-p4-info",
            records = new[]
            {
                new
                {
                    op = "override",
                    formid = infoFkStr,
                    source_path = SkyrimEsmForBatch7,
                    add_conditions = new object[]
                    {
                        new
                        {
                            function = "GetIsID",
                            @operator = "==",
                            value = 1f,
                            parameters = new Dictionary<string, object>
                            {
                                ["Object"] = hadvarFkStr,
                            },
                        },
                    },
                },
            },
            load_order = new
            {
                game_release = "SkyrimSE",
                listings = new[]
                {
                    new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                }
            }
        };

        var psi = new System.Diagnostics.ProcessStartInfo(bridgeExeP4i)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var proc = System.Diagnostics.Process.Start(psi)!;
        proc.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(req));
        proc.StandardInput.Close();
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Console.WriteLine($"  bridge exit: {proc.ExitCode}");

        // Bridge convention: when ALL records fail, the process exits non-zero
        // with valid JSON on stdout reporting success=false. So parse JSON first
        // regardless of exit code — the JSON's success field is the primary
        // pass/fail signal; exit code is a secondary cross-check.
        bool ok = true;
        bool jsonParsed = false;
        bool reportedSuccess = false;
        int failedCount = -1;
        string firstErrorText = "<none>";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            jsonParsed = true;
            reportedSuccess = root.TryGetProperty("success", out var sv) && sv.GetBoolean();
            if (root.TryGetProperty("failed_count", out var fc)) failedCount = fc.GetInt32();
            if (root.TryGetProperty("details", out var dets) &&
                dets.ValueKind == System.Text.Json.JsonValueKind.Array && dets.GetArrayLength() > 0)
            {
                var d0 = dets[0];
                if (d0.TryGetProperty("error", out var e)) firstErrorText = e.GetString() ?? "<null>";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  *** FAIL: bridge stdout unparseable as JSON: {ex.Message}");
            Console.WriteLine($"      stdout (first 800 chars): {stdout.Substring(0, Math.Min(800, stdout.Length))}");
            if (!string.IsNullOrEmpty(stderr)) Console.WriteLine($"      stderr: {stderr.Trim()}");
            p4InfoFailures++;
            ok = false;
        }

        if (ok && !jsonParsed)
        {
            // Defensive — should never hit (parse-failure already handled above)
            ok = false;
        }
        else if (ok && !reportedSuccess)
        {
            Console.WriteLine($"  *** FAIL: bridge reports success=false (failed_count={failedCount}, process exit={proc.ExitCode})");
            Console.WriteLine($"      first error: {firstErrorText}");
            Console.WriteLine($"      Pre-fix repro: this is the expected trace before CopyAsOverride's");
            Console.WriteLine($"      IDialogResponsesGetter branch lands. Post-fix, success should be true.");
            p4InfoFailures++;
            ok = false;
        }
        else if (ok && !File.Exists(outPath))
        {
            Console.WriteLine($"  *** FAIL: bridge reported success but output ESP missing at {outPath}");
            p4InfoFailures++;
            ok = false;
        }

        if (ok)
        {
            try
            {
                var outMod = SkyrimMod.CreateFromBinary(outPath, SkyrimRelease.SkyrimSE);
                var info = outMod.EnumerateMajorRecords<IDialogResponsesGetter>()
                    .FirstOrDefault(r => r.FormKey == infoFk);
                if (info == null)
                {
                    Console.WriteLine($"  *** FAIL: override INFO {infoFkStr} not found in output ESP");
                    Console.WriteLine($"      (EnumerateMajorRecords<IDialogResponsesGetter> yielded nothing matching {infoFk})");
                    p4InfoFailures++;
                }
                else
                {
                    Console.WriteLine($"  override INFO {infoFkStr} present in output ESP (Conditions.Count={info.Conditions.Count})");

                    // Find the appended GetIsID condition by Object.FormKey == Hadvar.
                    // Using the FormKey rather than ordinal position guards against any
                    // re-ordering across the GetOrAddAsOverride deep-copy. Reflection
                    // walk Object → .Link → .FormKey mirrors coverage-smoke's pattern
                    // at coverage-smoke/Program.cs:5293.
                    static FormKey? ReadObjectFormKey(object condData)
                    {
                        var obj = condData.GetType().GetProperty("Object")?.GetValue(condData);
                        if (obj == null) return null;
                        var link = obj.GetType().GetProperty("Link")?.GetValue(obj);
                        if (link == null) return null;
                        var fk = link.GetType().GetProperty("FormKey")?.GetValue(link);
                        return fk as FormKey?;
                    }
                    var match = info.Conditions
                        .Where(c => c.Data?.GetType().Name == "GetIsIDConditionData")
                        .Select(c => (Cond: c, FormKey: ReadObjectFormKey(c.Data!)))
                        .FirstOrDefault(t => t.FormKey == hadvarFk);
                    if (match.Cond == null)
                    {
                        var getIsIdCount = info.Conditions.Count(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                        Console.WriteLine($"  *** FAIL: no GetIsIDConditionData with Object={hadvarFkStr} in override INFO");
                        Console.WriteLine($"      ({getIsIdCount} GetIsIDConditionData entries present, none match Hadvar)");
                        p4InfoFailures++;
                    }
                    else
                    {
                        Console.WriteLine($"  PASS  INFO override + GetIsID(Object={hadvarFkStr}) round-trip ✓");
                        Console.WriteLine($"        slot resolved to {match.FormKey} (NOT FormID 0 default)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  *** FAIL: readback threw: {ex.GetType().Name} — {ex.Message}");
                p4InfoFailures++;
            }
        }

        try { if (File.Exists(outPath)) File.Delete(outPath); } catch { /* best-effort cleanup */ }
    }
}
Console.WriteLine($"=== v2.9 P4-INFO probes: {(p4InfoFailures == 0 ? "ALL PASS" : $"{p4InfoFailures} FAILURE(S)")} ===");

// ─── v2.9.1 P1 — Multi-condition record schema sweep ─────────────────────
//
// Phase 1 of v2.9.1 (Quest condition disambiguation). Confirms:
//   1. General sweep: every concrete major-record class in
//      Mutagen.Bethesda.Skyrim with public-instance properties whose name
//      ends in "Conditions" (case-insensitive). Drives the generality-scope
//      decision (QUST-only vs expand) via the conductor relay.
//   2. QUST negative confirmation: IQuestGetter exposes EXACTLY
//      DialogConditions + EventConditions and no third top-level
//      *Conditions* property. v2.9.1's list-target dispatch is bivariate;
//      a third top-level list would change the dispatch design.
//   3. Nested-conditions surfaces: alias-/stage-/objective-/scene-action-/
//      package-procedure-level *Conditions* properties — flagged as
//      out-of-scope-but-documented for v2.9.x candidates per PLAN.md § D.
//   4. QUST anchor selection: a vanilla Skyrim.esm QUST with both
//      DialogConditions.Count > 0 AND EventConditions.Count > 0 for
//      round-trip-distinguishability in Phase 2's coverage-smoke cells.
//      PLAN.md § Phase 1 step 2 names MQ101 (Skyrim.esm:000242) as a
//      candidate; sweep falls through to first qualifying quest if MQ101
//      doesn't qualify.
//
// CONDUCTOR_KICKOFF.md line 38: 5+ additional multi-condition record types
// triggers a halt — exceeds v2.9.1 scope envelope.
Section("v2.9.1 P1 — Multi-condition record schema sweep");

int p1MultiCondFailures = 0;
{
    var skyrimAssembly = typeof(IQuestGetter).Assembly;

    // Concrete major-record classes — what the bridge's runtime dispatch
    // (record.GetType().GetProperty("Conditions", ...) at PatchEngine.cs:1576
    // + :2264) actually sees. SkyrimMajorRecord is the abstract base; filter
    // excludes interfaces, abstracts, and non-Skyrim-namespace types.
    var concreteRecords = skyrimAssembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface)
        .Where(t => t.Namespace == "Mutagen.Bethesda.Skyrim")
        .Where(t => typeof(SkyrimMajorRecord).IsAssignableFrom(t))
        .OrderBy(t => t.Name)
        .ToList();

    Console.WriteLine($"  Sweep: {concreteRecords.Count} concrete major-record classes in Mutagen.Bethesda.Skyrim");

    // Try-derive 4-char ESP record type code from <Class>.StaticRegistration.
    // Print-time aid only — not load-bearing for the schema finding.
    static string TryRecordTypeCode(Type t)
    {
        try
        {
            var regField = t.GetField("StaticRegistration",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            object? reg = regField?.GetValue(null);
            if (reg == null)
            {
                var regProp = t.GetProperty("StaticRegistration",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                reg = regProp?.GetValue(null);
            }
            if (reg == null) return "????";
            var rtProp = reg.GetType().GetProperty("RecordType",
                BindingFlags.Public | BindingFlags.Instance);
            object? rt = rtProp?.GetValue(reg);
            if (rt == null) return "????";
            var typeProp = rt.GetType().GetProperty("Type",
                BindingFlags.Public | BindingFlags.Instance);
            return (typeProp?.GetValue(rt) as string) ?? "????";
        }
        catch { return "????"; }
    }

    // Pass 1: collect every concrete record class with at least one
    // *Conditions property (single or multi).
    var allCondCarriers = new List<(string Code, string ClassName, string GetterInterface,
                                    List<(string PropName, string PropType)> Props)>();
    foreach (var rec in concreteRecords)
    {
        var condProps = rec.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Conditions", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name)
            .ToList();
        if (condProps.Count == 0) continue;
        var props = condProps.Select(p => (p.Name, p.PropertyType.ToString())).ToList();
        allCondCarriers.Add((TryRecordTypeCode(rec), rec.Name, $"I{rec.Name}Getter", props));
    }

    Console.WriteLine();
    Console.WriteLine($"  ── General sweep: {allCondCarriers.Count} concrete major-record class(es) carry *Conditions property(s) ──");
    foreach (var entry in allCondCarriers)
    {
        var marker = entry.Props.Count >= 2 ? "MULTI" : "single";
        Console.WriteLine($"    [{marker,-6}]  {entry.Code,-4}  {entry.ClassName,-22}  {entry.GetterInterface}");
        foreach (var (propName, propType) in entry.Props)
        {
            Console.WriteLine($"             - {propName,-25} {propType}");
        }
    }

    var multiOnly = allCondCarriers.Where(e => e.Props.Count >= 2).ToList();
    Console.WriteLine();
    Console.WriteLine($"  ── Multi-condition record types (≥2 *Conditions properties): {multiOnly.Count} ──");
    if (multiOnly.Count == 0)
    {
        Console.WriteLine($"    (none — single-Conditions carriers only; see general sweep above)");
    }
    else
    {
        foreach (var entry in multiOnly)
        {
            Console.WriteLine($"    {entry.Code,-4}  {entry.ClassName,-22}  {entry.GetterInterface}");
            foreach (var (propName, propType) in entry.Props)
            {
                Console.WriteLine($"             - {propName,-25} {propType}");
            }
        }
    }

    // Sub-section 2: QUST negative confirmation — IQuestGetter top-level
    // properties matching '*Conditions*' (Contains, broader than EndsWith
    // — catches anomalies like ConditionsExtra). v2.9.1 assumes exactly
    // two: DialogConditions + EventConditions.
    Console.WriteLine();
    Console.WriteLine("  ── QUST negative confirmation: IQuestGetter properties matching '*Conditions*' (case-insensitive Contains) ──");
    var iquestGetter = typeof(IQuestGetter);
    var questCondProps = iquestGetter.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.Name.IndexOf("Conditions", StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(p => p.Name)
        .ToList();
    Console.WriteLine($"    IQuestGetter exposes {questCondProps.Count} '*Conditions*' property(s):");
    foreach (var p in questCondProps)
    {
        Console.WriteLine($"      - {p.Name,-25} {p.PropertyType}");
    }
    bool hasDialog = questCondProps.Any(p => p.Name.Equals("DialogConditions", StringComparison.OrdinalIgnoreCase));
    bool hasEvent = questCondProps.Any(p => p.Name.Equals("EventConditions", StringComparison.OrdinalIgnoreCase));
    if (questCondProps.Count != 2 || !hasDialog || !hasEvent)
    {
        Console.WriteLine($"    *** UNEXPECTED: expected exactly 2 (DialogConditions + EventConditions), found {questCondProps.Count} ***");
        Console.WriteLine($"        (DialogConditions={hasDialog}, EventConditions={hasEvent}); v2.9.1 scope assumes bivariate.");
        p1MultiCondFailures++;
    }
    else
    {
        Console.WriteLine($"    PASS  exactly DialogConditions + EventConditions; no third top-level condition list");
    }

    // Sub-section 3: nested-condition surfaces (out-of-scope for v2.9.1).
    Console.WriteLine();
    Console.WriteLine("  ── Nested-condition surfaces (out-of-scope-but-documented for v2.9.x candidates) ──");
    var nestedCandidates = new[]
    {
        "Mutagen.Bethesda.Skyrim.IQuestAliasGetter",
        "Mutagen.Bethesda.Skyrim.IQuestStageGetter",
        "Mutagen.Bethesda.Skyrim.IQuestObjectiveGetter",
        "Mutagen.Bethesda.Skyrim.IQuestLogEntryGetter",
        "Mutagen.Bethesda.Skyrim.ISceneActionGetter",
        "Mutagen.Bethesda.Skyrim.IPackageProcedureGetter",
    };
    int nestedCount = 0;
    foreach (var typeName in nestedCandidates)
    {
        var nt = skyrimAssembly.GetType(typeName);
        var shortName = typeName.Replace("Mutagen.Bethesda.Skyrim.", "");
        if (nt == null)
        {
            Console.WriteLine($"      (interface not found: {shortName})");
            continue;
        }
        var nProps = nt.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Conditions", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name)
            .ToList();
        if (nProps.Count == 0)
        {
            Console.WriteLine($"      [no nested *Conditions on {shortName}]");
            continue;
        }
        foreach (var p in nProps)
        {
            Console.WriteLine($"      [nested] {shortName}.{p.Name,-25} {p.PropertyType}");
            nestedCount++;
        }
    }
    Console.WriteLine($"    Total nested-condition surfaces flagged: {nestedCount}");

    // Sub-section 4: QUST anchor selection — vanilla Skyrim.esm quest with
    // both lists populated (round-trip-distinguishability anchor for
    // Phase 2's coverage-smoke cells per MATRIX.md § Layer 1.P).
    Console.WriteLine();
    Console.WriteLine("  ── QUST anchor selection: vanilla Skyrim.esm quest with both lists populated ──");
    if (!File.Exists(SkyrimEsmForBatch7))
    {
        Console.WriteLine($"    SKIP: Skyrim.esm not found at {SkyrimEsmForBatch7}; QUST anchor selection deferred.");
    }
    else
    {
        try
        {
            Console.WriteLine($"    Loading: {SkyrimEsmForBatch7}");
            var srcMod = SkyrimMod.CreateFromBinary(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            var skyrimEsmKey = ModKey.FromNameAndExtension("Skyrim.esm");

            // PLAN.md § Phase 1 step 2 candidate: MQ101 at Skyrim.esm:000242.
            // Print regardless of qualification — informs anchor pick even
            // if MQ101 doesn't have both lists populated.
            var mq101Fk = new FormKey(skyrimEsmKey, 0x000242);
            var mq101 = srcMod.Quests.FirstOrDefault(q => q.FormKey == mq101Fk);
            if (mq101 != null)
            {
                Console.WriteLine($"    MQ101 candidate (Skyrim.esm:000242):");
                Console.WriteLine($"      EditorID:               {mq101.EditorID ?? "(null)"}");
                Console.WriteLine($"      DialogConditions.Count: {mq101.DialogConditions.Count}");
                Console.WriteLine($"      EventConditions.Count:  {mq101.EventConditions.Count}");
                if (mq101.DialogConditions.Count > 0 && mq101.EventConditions.Count > 0)
                    Console.WriteLine($"      → qualifies as round-trip-distinguishability anchor");
                else
                    Console.WriteLine($"      → does NOT qualify (one or both lists empty); first qualifying anchor below");
            }
            else
            {
                Console.WriteLine($"    MQ101 (Skyrim.esm:000242) not found in Skyrim.esm");
            }

            // Sweep — first 10 quests with both lists populated.
            var qualifying = srcMod.Quests
                .Where(q => q.DialogConditions.Count > 0 && q.EventConditions.Count > 0)
                .OrderBy(q => q.FormKey.ID)
                .Take(10)
                .ToList();
            Console.WriteLine();
            Console.WriteLine($"    First {qualifying.Count} qualifying quest(s) (Dialog>0 AND Event>0):");
            foreach (var q in qualifying)
            {
                Console.WriteLine($"      Skyrim.esm:{q.FormKey.ID:X6}  {q.EditorID ?? "(null)",-30}  Dialog={q.DialogConditions.Count}  Event={q.EventConditions.Count}");
            }

            if (qualifying.Count == 0)
            {
                // Mandatory halt per kickoff: anchor must populate both lists.
                Console.WriteLine($"    *** UNEXPECTED: no vanilla Skyrim.esm QUST has both DialogConditions and EventConditions populated ***");
                Console.WriteLine($"        Phase 2 fixture must come from synthetic in-memory build — escalate to conductor.");
                p1MultiCondFailures++;
            }
            else
            {
                // Per-list function-name distribution for the first qualifying
                // anchor. Forward-look for Phase 2's 1.P.remove.<dialog|event>.
                // byfunc cells — pick a function present in only one list for
                // cleaner round-trip-distinguishability assertions.
                var anchor = qualifying[0];
                Console.WriteLine();
                Console.WriteLine($"    ── Anchor candidate detail: {anchor.EditorID ?? "(null)"} (Skyrim.esm:{anchor.FormKey.ID:X6}) ──");
                Console.WriteLine($"      DialogConditions function-name distribution:");
                foreach (var grp in anchor.DialogConditions
                    .GroupBy(c => c.Data?.GetType().Name ?? "<null>")
                    .OrderBy(g => g.Key))
                {
                    Console.WriteLine($"        {grp.Key,-32} count={grp.Count()}");
                }
                Console.WriteLine($"      EventConditions function-name distribution:");
                foreach (var grp in anchor.EventConditions
                    .GroupBy(c => c.Data?.GetType().Name ?? "<null>")
                    .OrderBy(g => g.Key))
                {
                    Console.WriteLine($"        {grp.Key,-32} count={grp.Count()}");
                }

                var dialogFns = anchor.DialogConditions
                    .Select(c => c.Data?.GetType().Name ?? "<null>")
                    .Distinct().ToHashSet();
                var eventFns = anchor.EventConditions
                    .Select(c => c.Data?.GetType().Name ?? "<null>")
                    .Distinct().ToHashSet();
                var dialogOnly = dialogFns.Except(eventFns).ToList();
                var eventOnly = eventFns.Except(dialogFns).ToList();
                Console.WriteLine($"      Dialog-only function names (Phase 2 byfunc candidates): {(dialogOnly.Count == 0 ? "(none)" : string.Join(", ", dialogOnly))}");
                Console.WriteLine($"      Event-only  function names (Phase 2 byfunc candidates): {(eventOnly.Count == 0 ? "(none)" : string.Join(", ", eventOnly))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** FAIL: load/scan threw: {ex.GetType().Name} — {ex.Message}");
            p1MultiCondFailures++;
        }
    }

    // Summary
    Console.WriteLine();
    Console.WriteLine("  ── Sweep summary ──");
    Console.WriteLine($"    All-Conditions carriers (single + multi):   {allCondCarriers.Count}");
    Console.WriteLine($"    Multi-condition record types (≥2 *Cond):    {multiOnly.Count}");
    Console.WriteLine($"    QUST top-level *Conditions* properties:     {questCondProps.Count} (expected 2)");
    Console.WriteLine($"    Nested-condition surfaces flagged:          {nestedCount}");
}
Console.WriteLine($"=== v2.9.1 P1 multi-condition sweep: {(p1MultiCondFailures == 0 ? "ALL PASS" : $"{p1MultiCondFailures} FAILURE(S)")} ===");

// ─── v2.9.1 P2 — Quest condition disambiguation (bridge subprocess) ─────
//
// Phase 2's bridge dispatch landed: RecordOperation.ConditionTarget routes
// reflection lookup through ResolveConditionListProperty. This section
// exercises the live bridge end-to-end.
//
// Carrier: QUST Skyrim.esm:04C49D (FollowerCommentary01 — Phase 1 anchor;
// disjoint per-list function distribution: GetInFaction in DialogConditions
// only, GetEventData in EventConditions only).
//
// Probe shape (per probe block): bridge subprocess → patch QUST 04C49D with
// the test op → readback via SkyrimMod.CreateFromBinary (independent of
// bridge) → assert (a) bridge success/error per probe verdict, (b) Mutagen-
// direct readback shows condition lands in the targeted list and NOT in the
// other list. The 8 probes below cover:
//   - Positive add: dialog target / event target (smoke; HALT 2 anchor)
//   - Positive remove byfunc: dialog (GetInFaction) / event (GetEventData)
//   - Error: QUST without condition_target → Q3 explicit error
//   - Error: bad condition_target value ("story") → §C#3 explicit error
//   - Error: PERK + condition_target → Q4 reject error
//   - Composition: QUST DialogConditions + GetIsID + parameters{Object} —
//     exercises v2.9.0's RouteParameterSlot dispatcher under v2.9.1's
//     list-target dispatch.
Section("v2.9.1 P2 — Quest condition disambiguation (bridge subprocess)");

int p2QustFailures = 0;
{
    var thisDirP2q = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var bridgeExeP2q = Path.GetFullPath(Path.Combine(thisDirP2q,
        "..", "..", "..", "..", "mutagen-bridge", "bin", "Release", "net8.0", "mutagen-bridge.exe"));
    if (!File.Exists(bridgeExeP2q))
    {
        Console.WriteLine($"  SKIP: mutagen-bridge.exe not found at {bridgeExeP2q}");
    }
    else if (!File.Exists(SkyrimEsmForBatch7))
    {
        Console.WriteLine($"  SKIP: Skyrim.esm not found at {SkyrimEsmForBatch7}");
    }
    else
    {
        // Phase 1 anchor
        var questFkStr = "Skyrim.esm:04C49D";
        var questFk = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x04C49D);

        // Hadvar — already vanilla-confirmed by P4-INFO probe; safe Object slot.
        var hadvarFkStr = "Skyrim.esm:02BF9F";
        var hadvarFk = new FormKey(ModKey.FromNameAndExtension("Skyrim.esm"), 0x02BF9F);

        // PERK FormID for the Q4 reject probe lands in Phase C extension.
        // Hadvar (Object slot) and the QUST anchor are the only fixtures
        // needed for Phase B's two positive-add probes.

        Console.WriteLine($"  bridge:  {bridgeExeP2q}");
        Console.WriteLine($"  source:  {SkyrimEsmForBatch7}");
        Console.WriteLine($"  carrier: QUST {questFkStr} (FollowerCommentary01, Phase 1 anchor)");
        Console.WriteLine();

        var outDirP2q = Path.Combine(Path.GetTempPath(), "race-probe-v291-p2");
        Directory.CreateDirectory(outDirP2q);

        // ── Bridge invocation helper (returns parsed JSON state) ──
        (bool ParsedOk, bool ReportedSuccess, int FailedCount, string FirstErr, int ExitCode) RunBridge(object req)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(bridgeExeP2q)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var proc = System.Diagnostics.Process.Start(psi)!;
            proc.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(req));
            proc.StandardInput.Close();
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                bool succ = root.TryGetProperty("success", out var sv) && sv.GetBoolean();
                int failedCt = root.TryGetProperty("failed_count", out var fc) ? fc.GetInt32() : -1;
                string err = "<none>";
                if (root.TryGetProperty("details", out var dets) &&
                    dets.ValueKind == System.Text.Json.JsonValueKind.Array && dets.GetArrayLength() > 0)
                {
                    var d0 = dets[0];
                    if (d0.TryGetProperty("error", out var e)) err = e.GetString() ?? "<null>";
                }
                return (true, succ, failedCt, err, proc.ExitCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      stdout (first 500 chars): {stdout.Substring(0, Math.Min(500, stdout.Length))}");
                if (!string.IsNullOrEmpty(stderr)) Console.WriteLine($"      stderr: {stderr.Trim()}");
                Console.WriteLine($"      parse exception: {ex.Message}");
                return (false, false, -1, "<parse-failed>", proc.ExitCode);
            }
        }

        // Mutagen-direct readback: returns (DialogConditions, EventConditions)
        // for the override QUST in the output ESP, or null if the override
        // can't be located.
        (IReadOnlyList<IConditionGetter> Dialog, IReadOnlyList<IConditionGetter> Event)?
            ReadbackQuest(string outPath, FormKey fk)
        {
            var outMod = SkyrimMod.CreateFromBinary(outPath, SkyrimRelease.SkyrimSE);
            var q = outMod.EnumerateMajorRecords<IQuestGetter>().FirstOrDefault(r => r.FormKey == fk);
            if (q == null) return null;
            return (q.DialogConditions, q.EventConditions);
        }

        // ── 1. Positive add — dialog target ──
        Console.WriteLine("  [1/8] add condition_target=dialog (GetIsID(Object=Hadvar))");
        {
            var outPath = Path.Combine(outDirP2q, "p2-add-dialog.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "dialog",
                        add_conditions = new object[]
                        {
                            new
                            {
                                function = "GetIsID",
                                @operator = "==",
                                value = 1f,
                                parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                            },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk || !r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge success=false (exit={r.ExitCode}, failed={r.FailedCount}, err={r.FirstErr})");
                p2QustFailures++;
            }
            else if (!File.Exists(outPath))
            {
                Console.WriteLine($"        *** FAIL: output ESP missing");
                p2QustFailures++;
            }
            else
            {
                var rb = ReadbackQuest(outPath, questFk);
                if (rb == null)
                {
                    Console.WriteLine($"        *** FAIL: override QUST not found in output ESP");
                    p2QustFailures++;
                }
                else
                {
                    int dialogN = rb.Value.Dialog.Count;
                    int eventN = rb.Value.Event.Count;
                    bool gotIdInDialog = rb.Value.Dialog.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    bool gotIdInEvent = rb.Value.Event.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    if (dialogN == 2 && eventN == 1 && gotIdInDialog && !gotIdInEvent)
                    {
                        Console.WriteLine($"        PASS  Dialog={dialogN} (1 vanilla GetInFaction + 1 added GetIsID), Event={eventN} (1 vanilla GetEventData, untouched)");
                    }
                    else
                    {
                        Console.WriteLine($"        *** FAIL: counts/distribution unexpected — Dialog={dialogN}, Event={eventN}, GetIsID-in-Dialog={gotIdInDialog}, GetIsID-in-Event={gotIdInEvent}");
                        p2QustFailures++;
                    }
                }
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 2. Positive add — event target ──
        Console.WriteLine("  [2/8] add condition_target=event (GetIsID(Object=Hadvar))");
        {
            var outPath = Path.Combine(outDirP2q, "p2-add-event.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "event",
                        add_conditions = new object[]
                        {
                            new
                            {
                                function = "GetIsID",
                                @operator = "==",
                                value = 1f,
                                parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                            },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk || !r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge success=false (exit={r.ExitCode}, failed={r.FailedCount}, err={r.FirstErr})");
                p2QustFailures++;
            }
            else if (!File.Exists(outPath))
            {
                Console.WriteLine($"        *** FAIL: output ESP missing");
                p2QustFailures++;
            }
            else
            {
                var rb = ReadbackQuest(outPath, questFk);
                if (rb == null)
                {
                    Console.WriteLine($"        *** FAIL: override QUST not found in output ESP");
                    p2QustFailures++;
                }
                else
                {
                    int dialogN = rb.Value.Dialog.Count;
                    int eventN = rb.Value.Event.Count;
                    bool gotIdInDialog = rb.Value.Dialog.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    bool gotIdInEvent = rb.Value.Event.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    if (dialogN == 1 && eventN == 2 && !gotIdInDialog && gotIdInEvent)
                    {
                        Console.WriteLine($"        PASS  Dialog={dialogN} (1 vanilla GetInFaction, untouched), Event={eventN} (1 vanilla GetEventData + 1 added GetIsID)");
                    }
                    else
                    {
                        Console.WriteLine($"        *** FAIL: counts/distribution unexpected — Dialog={dialogN}, Event={eventN}, GetIsID-in-Dialog={gotIdInDialog}, GetIsID-in-Event={gotIdInEvent}");
                        p2QustFailures++;
                    }
                }
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 3. Positive remove byfunc — dialog target (GetInFaction) ──
        // FollowerCommentary01.DialogConditions has exactly 1 GetInFaction
        // condition (Phase 1 disjoint distribution). Pre: Dialog=1, Event=1.
        // Expected post: Dialog=0 (GetInFaction removed), Event=1 unchanged.
        Console.WriteLine("  [3/8] remove byfunc condition_target=dialog (GetInFaction)");
        {
            var outPath = Path.Combine(outDirP2q, "p2-rm-dialog-byfunc.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "dialog",
                        remove_conditions = new object[]
                        {
                            new { function = "GetInFaction" },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk || !r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge success=false (exit={r.ExitCode}, failed={r.FailedCount}, err={r.FirstErr})");
                p2QustFailures++;
            }
            else if (!File.Exists(outPath))
            {
                Console.WriteLine($"        *** FAIL: output ESP missing");
                p2QustFailures++;
            }
            else
            {
                var rb = ReadbackQuest(outPath, questFk);
                if (rb == null) { Console.WriteLine($"        *** FAIL: override QUST not found"); p2QustFailures++; }
                else
                {
                    int dialogN = rb.Value.Dialog.Count;
                    int eventN = rb.Value.Event.Count;
                    bool eventStillHasEventData = rb.Value.Event.Any(c => c.Data?.GetType().Name == "GetEventDataConditionData");
                    if (dialogN == 0 && eventN == 1 && eventStillHasEventData)
                    {
                        Console.WriteLine($"        PASS  Dialog={dialogN} (GetInFaction removed), Event={eventN} (GetEventData untouched)");
                    }
                    else
                    {
                        Console.WriteLine($"        *** FAIL: Dialog={dialogN}, Event={eventN}, EventData-still-present={eventStillHasEventData}");
                        p2QustFailures++;
                    }
                }
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 4. Positive remove byfunc — event target (GetEventData) ──
        Console.WriteLine("  [4/8] remove byfunc condition_target=event (GetEventData)");
        {
            var outPath = Path.Combine(outDirP2q, "p2-rm-event-byfunc.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "event",
                        remove_conditions = new object[]
                        {
                            new { function = "GetEventData" },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk || !r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge success=false (exit={r.ExitCode}, failed={r.FailedCount}, err={r.FirstErr})");
                p2QustFailures++;
            }
            else if (!File.Exists(outPath))
            {
                Console.WriteLine($"        *** FAIL: output ESP missing");
                p2QustFailures++;
            }
            else
            {
                var rb = ReadbackQuest(outPath, questFk);
                if (rb == null) { Console.WriteLine($"        *** FAIL: override QUST not found"); p2QustFailures++; }
                else
                {
                    int dialogN = rb.Value.Dialog.Count;
                    int eventN = rb.Value.Event.Count;
                    bool dialogStillHasInFaction = rb.Value.Dialog.Any(c => c.Data?.GetType().Name == "GetInFactionConditionData");
                    if (dialogN == 1 && eventN == 0 && dialogStillHasInFaction)
                    {
                        Console.WriteLine($"        PASS  Dialog={dialogN} (GetInFaction untouched), Event={eventN} (GetEventData removed)");
                    }
                    else
                    {
                        Console.WriteLine($"        *** FAIL: Dialog={dialogN}, Event={eventN}, InFaction-still-present={dialogStillHasInFaction}");
                        p2QustFailures++;
                    }
                }
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 5. Error: QUST without condition_target → Q3 explicit error ──
        Console.WriteLine("  [5/8] error: QUST without condition_target → Q3 explicit error");
        {
            var outPath = Path.Combine(outDirP2q, "p2-err-q3-no-target.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        // NO condition_target
                        add_conditions = new object[]
                        {
                            new
                            {
                                function = "GetIsID",
                                @operator = "==",
                                value = 1f,
                                parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                            },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk)
            {
                Console.WriteLine($"        *** FAIL: bridge stdout unparseable");
                p2QustFailures++;
            }
            else if (r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge reported success=true; expected per-record error (Q3)");
                p2QustFailures++;
            }
            else if (!r.FirstErr.Contains("requires a condition_target parameter") || !r.FirstErr.Contains("Quest"))
            {
                Console.WriteLine($"        *** FAIL: error text doesn't match Q3 sentinel");
                Console.WriteLine($"            actual: {r.FirstErr}");
                p2QustFailures++;
            }
            else
            {
                Console.WriteLine($"        PASS  bridge success=false; error matches Q3 sentinel");
                Console.WriteLine($"              error: {r.FirstErr}");
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 6. Error: bad condition_target value ("story") → §C#3 ──
        Console.WriteLine("  [6/8] error: bad condition_target='story' → §C#3");
        {
            var outPath = Path.Combine(outDirP2q, "p2-err-bad-target.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "story",
                        add_conditions = new object[]
                        {
                            new
                            {
                                function = "GetIsID",
                                @operator = "==",
                                value = 1f,
                                parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                            },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk) { Console.WriteLine($"        *** FAIL: bridge stdout unparseable"); p2QustFailures++; }
            else if (r.ReportedSuccess) { Console.WriteLine($"        *** FAIL: bridge reported success=true; expected bad-value error"); p2QustFailures++; }
            else if (!r.FirstErr.Contains("Unknown condition_target") || !r.FirstErr.Contains("'story'"))
            {
                Console.WriteLine($"        *** FAIL: error text doesn't match §C#3 sentinel");
                Console.WriteLine($"            actual: {r.FirstErr}");
                p2QustFailures++;
            }
            else
            {
                Console.WriteLine($"        PASS  error matches §C#3 sentinel");
                Console.WriteLine($"              error: {r.FirstErr}");
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }

        // ── 7. Error: PERK + condition_target → Q4 reject error ──
        // Pick any vanilla PERK dynamically from Skyrim.esm.
        Console.WriteLine("  [7/8] error: PERK + condition_target → Q4 reject");
        {
            string? perkFkStr = null;
            try
            {
                var srcModForPerk = SkyrimMod.CreateFromBinary(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
                var anyPerk = srcModForPerk.Perks.FirstOrDefault();
                if (anyPerk != null)
                {
                    var pfk = anyPerk.FormKey;
                    perkFkStr = $"{pfk.ModKey.FileName}:{pfk.ID:X6}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"        *** FAIL: couldn't resolve a vanilla perk: {ex.Message}");
                p2QustFailures++;
            }
            if (perkFkStr != null)
            {
                var outPath = Path.Combine(outDirP2q, "p2-err-q4-perk.esp");
                if (File.Exists(outPath)) File.Delete(outPath);
                var req = new
                {
                    command = "patch",
                    output_path = outPath,
                    esl_flag = false,
                    author = "race-probe-v291-p2",
                    records = new[]
                    {
                        new
                        {
                            op = "override",
                            formid = perkFkStr,
                            source_path = SkyrimEsmForBatch7,
                            condition_target = "dialog",
                            add_conditions = new object[]
                            {
                                new
                                {
                                    function = "GetIsID",
                                    @operator = "==",
                                    value = 1f,
                                    parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                                },
                            },
                        },
                    },
                    load_order = new
                    {
                        game_release = "SkyrimSE",
                        listings = new[]
                        {
                            new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                        }
                    }
                };
                var r = RunBridge(req);
                if (!r.ParsedOk) { Console.WriteLine($"        *** FAIL: bridge stdout unparseable"); p2QustFailures++; }
                else if (r.ReportedSuccess) { Console.WriteLine($"        *** FAIL: bridge reported success=true; expected Q4 reject"); p2QustFailures++; }
                else if (!r.FirstErr.Contains("uses a single Conditions list") || !r.FirstErr.Contains("omit condition_target"))
                {
                    Console.WriteLine($"        *** FAIL: error text doesn't match Q4 reject sentinel");
                    Console.WriteLine($"            actual: {r.FirstErr}");
                    p2QustFailures++;
                }
                else
                {
                    Console.WriteLine($"        PASS  bridge success=false; error matches Q4 reject sentinel (perk={perkFkStr})");
                    Console.WriteLine($"              error: {r.FirstErr}");
                }
                try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
            }
        }

        // ── 8. Case-insensitivity (Q5 lock) — condition_target="Dialog" ──
        Console.WriteLine("  [8/8] case-insensitivity: condition_target=\"Dialog\" → DialogConditions");
        {
            var outPath = Path.Combine(outDirP2q, "p2-case-insensitive.esp");
            if (File.Exists(outPath)) File.Delete(outPath);
            var req = new
            {
                command = "patch",
                output_path = outPath,
                esl_flag = false,
                author = "race-probe-v291-p2",
                records = new[]
                {
                    new
                    {
                        op = "override",
                        formid = questFkStr,
                        source_path = SkyrimEsmForBatch7,
                        condition_target = "Dialog", // TitleCase, not "dialog"
                        add_conditions = new object[]
                        {
                            new
                            {
                                function = "GetIsID",
                                @operator = "==",
                                value = 1f,
                                parameters = new Dictionary<string, object> { ["Object"] = hadvarFkStr },
                            },
                        },
                    },
                },
                load_order = new
                {
                    game_release = "SkyrimSE",
                    listings = new[]
                    {
                        new { mod_key = "Skyrim.esm", path = SkyrimEsmForBatch7, enabled = true }
                    }
                }
            };
            var r = RunBridge(req);
            if (!r.ParsedOk || !r.ReportedSuccess)
            {
                Console.WriteLine($"        *** FAIL: bridge success=false (exit={r.ExitCode}, err={r.FirstErr}) — Q5 case-insensitive lock not honored");
                p2QustFailures++;
            }
            else if (!File.Exists(outPath))
            {
                Console.WriteLine($"        *** FAIL: output ESP missing");
                p2QustFailures++;
            }
            else
            {
                var rb = ReadbackQuest(outPath, questFk);
                if (rb == null) { Console.WriteLine($"        *** FAIL: override QUST not found"); p2QustFailures++; }
                else
                {
                    bool gotIdInDialog = rb.Value.Dialog.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    bool gotIdInEvent = rb.Value.Event.Any(c => c.Data?.GetType().Name == "GetIsIDConditionData");
                    if (gotIdInDialog && !gotIdInEvent)
                    {
                        Console.WriteLine($"        PASS  \"Dialog\" → DialogConditions (case-insensitive Q5 lock)");
                    }
                    else
                    {
                        Console.WriteLine($"        *** FAIL: GetIsID-in-Dialog={gotIdInDialog}, GetIsID-in-Event={gotIdInEvent}");
                        p2QustFailures++;
                    }
                }
            }
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
        }
    }
}
Console.WriteLine($"=== v2.9.1 P2 quest-condition probes: {(p2QustFailures == 0 ? "ALL PASS" : $"{p2QustFailures} FAILURE(S)")} ===");

// ─── v2.9.2 P1 — Read-side perf baseline + record-shape sweep ─────────
//
// Phase 1 of v2.9.2 (read-side efficiency for mo2_record_detail). Quantifies
// the three composable axes' projected gains and reflects over Mutagen 0.53.1
// to enumerate every FormLink-typed property surface across IMajorRecordGetter
// implementations. Six measurement axes:
//
//   1. Subprocess startup cost — wall-clock the bridge invocation for one
//      trivial vanilla GMST read. Repeat 5×; take median + range.
//   2. Per-record marginal cost — read_records batches of N ∈ {1, 5, 20, 50,
//      100, 200} of the same RACE record. Per-record delta over batch-1.
//   3. Per-record full-detail payload baselines — RACE / NPC_ / QUST / MGEF /
//      PERK / ARMO / WEAP / SPEL byte sizes via bridge read_record.
//   4. Projection payload-size impact (PROJECTED — bridge doesn't yet carry
//      `fields` projection; Phase 1 measures full-detail baselines + Phase 2
//      measures actual projected sizes).
//   5. Expansion round-trip elimination (PROJECTED — single-level FormLink
//      expansion not yet wired; Phase 1 measures the without-expansion
//      baseline (RACE + N second-tier SPEL reads) + projects the with-
//      expansion cost at `1 × startup + N × marginal`).
//   6. Cross-product timing per Q6 amendment (allow combination — `formids:
//      [N records of same type] + plugin_names: [M plugins]` returns the N×M
//      cells). Phase 1 simulates by issuing `read_records` with N×M items
//      (each plugin × each formid pairing) and times the wall-clock + total
//      response payload size for N ∈ {10, 50, 100} × M ∈ {2, 5, 10}. Halts +
//      writes CONDUCTOR ASK if cross-product wall-clock exceeds Python's
//      `mo2_record_detail` timeout = max(15s, 5*N×M).
//
// Plus a record-shape sweep — every concrete IMajorRecordGetter-implementing
// interface in Mutagen.Bethesda.Skyrim 0.53.1 × every FormLink-typed property
// (single + list of). Resolves the ActorEffect-vs-ActorEffects naming
// ambiguity flagged in PLAN.md § Phase 1 step 3. FormLink predicates mirror
// PatchEngine.cs:1182 IsFormLinkType (IFormLinkGetter<>, IFormLink<>,
// IFormLinkNullable<>, FormLink<>, FormLinkNullable<>) plus list-of variants.
Section("v2.9.2 P1 — Read-side perf baseline + record-shape sweep");

int p1ReadSideFailures = 0;
{
    var thisDirP1r = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var bridgeExeP1r = Path.GetFullPath(Path.Combine(thisDirP1r,
        "..", "..", "..", "..", "mutagen-bridge", "bin", "Release", "net8.0", "mutagen-bridge.exe"));
    bool haveBridge = File.Exists(bridgeExeP1r);
    bool haveSkyrimEsm = File.Exists(SkyrimEsmForBatch7);

    if (!haveBridge)
    {
        Console.WriteLine($"  SKIP perf section: mutagen-bridge.exe not found at {bridgeExeP1r}");
    }
    if (!haveSkyrimEsm)
    {
        Console.WriteLine($"  SKIP perf section: Skyrim.esm not found at {SkyrimEsmForBatch7}");
    }

    // ── Bridge invocation helper — returns (stdout, ms_wallclock, exitCode).
    // Mirrors v2.9.1 P2's RunBridge but exposes wall-clock + raw stdout for
    // payload-size measurement.
    (string Stdout, long Ms, int ExitCode) RunBridgePerf(object req)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(bridgeExeP1r)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var proc = System.Diagnostics.Process.Start(psi)!;
        proc.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(req));
        proc.StandardInput.Close();
        var stdout = proc.StandardOutput.ReadToEnd();
        var _stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        sw.Stop();
        return (stdout, sw.ElapsedMilliseconds, proc.ExitCode);
    }

    // ── Helper: parse `success` + count `records` array entries from bridge JSON.
    (bool Success, int RecordCount) ParseReadResponse(string stdout)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            bool succ = root.TryGetProperty("success", out var sv) && sv.GetBoolean();
            int recCount = 0;
            if (root.TryGetProperty("records", out var recs) &&
                recs.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                recCount = recs.GetArrayLength();
            }
            return (succ, recCount);
        }
        catch { return (false, 0); }
    }

    // === Axis 1 — Subprocess startup cost (5x median + range) ============
    long startupMedian = -1;
    long startupMin = -1;
    long startupMax = -1;
    if (haveBridge && haveSkyrimEsm)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 1: Subprocess startup cost (read_record on a trivial vanilla record × 5) ──");

        // GMST is the lightest record type; pick the first one in Skyrim.esm.
        // Falls through to the first-enumerated MajorRecord if Skyrim has no
        // GMST somehow.
        FormKey trivialKey = default;
        string trivialFkStr = "";
        try
        {
            using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            var firstGmst = srcMod.GameSettings.FirstOrDefault();
            if (firstGmst != null)
            {
                trivialKey = firstGmst.FormKey;
                trivialFkStr = $"{trivialKey.ModKey.FileName}:{trivialKey.ID:X6}";
            }
            else
            {
                var firstAny = srcMod.EnumerateMajorRecords().FirstOrDefault();
                if (firstAny != null)
                {
                    trivialKey = firstAny.FormKey;
                    trivialFkStr = $"{trivialKey.ModKey.FileName}:{trivialKey.ID:X6}";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** WARN: couldn't enumerate Skyrim.esm for trivial record: {ex.Message}");
        }

        if (string.IsNullOrEmpty(trivialFkStr))
        {
            Console.WriteLine($"    SKIP: no trivial record found");
        }
        else
        {
            Console.WriteLine($"    trivial record: {trivialFkStr}");
            var samples = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                var req = new
                {
                    command = "read_record",
                    plugin_path = SkyrimEsmForBatch7,
                    formid = trivialFkStr,
                    max_depth = 6,
                };
                var r = RunBridgePerf(req);
                var (succ, _) = ParseReadResponse(r.Stdout);
                Console.WriteLine($"    [{i + 1}/5] wall-clock = {r.Ms,5} ms  success={succ}  exit={r.ExitCode}");
                if (succ) samples.Add(r.Ms);
            }
            if (samples.Count >= 3)
            {
                samples.Sort();
                startupMedian = samples[samples.Count / 2];
                startupMin = samples[0];
                startupMax = samples[^1];
                Console.WriteLine($"    summary: median = {startupMedian} ms  min = {startupMin} ms  max = {startupMax} ms");
                Console.WriteLine($"    expected band per PLAN § G #1: 1200–1400 ms typical hardware. {(startupMedian < 800 || startupMedian > 4000 ? "*** BAND ALERT — perf-shape may be off ***" : "(within band)")}");
            }
            else
            {
                Console.WriteLine($"    *** FAIL: <3/5 samples succeeded; cannot compute median");
                p1ReadSideFailures++;
            }
        }
    }

    // === Axis 2 — Per-record marginal cost via read_records batch ==========
    var marginalTable = new List<(int N, long Ms, double PerRecMs, int RecCount)>();
    if (haveBridge && haveSkyrimEsm)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 2: Per-record marginal cost (read_records batch of N RACE records) ──");

        // Collect up to 200 RACE FormIDs from Skyrim.esm. Will pad by repeating
        // if Skyrim has fewer than 200 RACE records (vanilla has ~36 plus
        // animal/creature races; sufficient for batch-50 organically, larger
        // batches sample-with-replacement which still amortizes startup).
        var raceFkStrs = new List<string>();
        try
        {
            using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            foreach (var rec in srcMod.Races)
            {
                raceFkStrs.Add($"{rec.FormKey.ModKey.FileName}:{rec.FormKey.ID:X6}");
                if (raceFkStrs.Count >= 200) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** WARN: couldn't enumerate RACE records: {ex.Message}");
        }

        if (raceFkStrs.Count == 0)
        {
            Console.WriteLine($"    SKIP: no RACE records available");
        }
        else
        {
            int organicCount = raceFkStrs.Count;
            Console.WriteLine($"    organic RACE pool size: {organicCount}");
            var batchSizes = new[] { 1, 5, 20, 50, 100, 200 };
            foreach (int n in batchSizes)
            {
                var items = new List<object>();
                for (int i = 0; i < n; i++)
                {
                    items.Add(new
                    {
                        plugin_path = SkyrimEsmForBatch7,
                        formid = raceFkStrs[i % organicCount],
                    });
                }
                var req = new
                {
                    command = "read_records",
                    records = items,
                    max_depth = 6,
                };
                var r = RunBridgePerf(req);
                var (succ, recCount) = ParseReadResponse(r.Stdout);
                double perRec = n > 0 ? (double)r.Ms / n : 0;
                Console.WriteLine($"    [N={n,3}] wall-clock = {r.Ms,6} ms  per-record = {perRec,7:F2} ms  success={succ}  records={recCount}");
                if (succ) marginalTable.Add((n, r.Ms, perRec, recCount));
            }

            // Compute per-record delta over batch-1 as marginal cost.
            if (marginalTable.Count >= 2)
            {
                var batch1 = marginalTable.FirstOrDefault(x => x.N == 1);
                if (batch1.N == 1)
                {
                    Console.WriteLine();
                    Console.WriteLine($"    Per-record marginal cost over batch-1 baseline ({batch1.Ms} ms):");
                    Console.WriteLine($"    {"N",4}  {"wall-clock",10}  {"marginal",12}  {"per-rec marginal",18}");
                    foreach (var (n, ms, perRec, _) in marginalTable)
                    {
                        if (n == 1) continue;
                        long marginalMs = ms - batch1.Ms;
                        double perRecMarginal = (double)marginalMs / (n - 1);
                        Console.WriteLine($"    {n,4}  {ms,10} ms {marginalMs,9} ms     {perRecMarginal,7:F2} ms");
                    }
                    var max = marginalTable.Where(x => x.N >= 50).Select(x => (double)(x.Ms - batch1.Ms) / (x.N - 1)).DefaultIfEmpty(0).Max();
                    Console.WriteLine($"    expected band per PLAN § G #2: 5–20 ms per-record marginal once subprocess is hot.");
                    Console.WriteLine($"    measured max marginal at N≥50: {max:F2} ms  {(max > 50 ? "*** BAND ALERT — marginal cost > 50 ms; perf-shape may be off ***" : "(within band)")}");
                }
            }
        }
    }

    // === Axis 3 — Per-record full-detail payload baselines per record type =
    var payloadTable = new List<(string Type, string FkStr, int Bytes, int FieldCount)>();
    if (haveBridge && haveSkyrimEsm)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 3: Per-record full-detail payload baselines (bytes + top-level field count) ──");

        // Pick one representative FormID per type. Falls back to the first
        // enumerated record of that type from Skyrim.esm. RACE / NPC_ / QUST /
        // MGEF / PERK / ARMO / WEAP / SPEL per PLAN § G #3.
        var repFkStrs = new Dictionary<string, string>();
        try
        {
            using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            void Pick<T>(string code) where T : class, IMajorRecordGetter
            {
                var first = srcMod.EnumerateMajorRecords<T>().FirstOrDefault();
                if (first != null) repFkStrs[code] = $"{first.FormKey.ModKey.FileName}:{first.FormKey.ID:X6}";
            }
            Pick<IRaceGetter>("RACE");
            Pick<INpcGetter>("NPC_");
            Pick<IQuestGetter>("QUST");
            Pick<IMagicEffectGetter>("MGEF");
            Pick<IPerkGetter>("PERK");
            Pick<IArmorGetter>("ARMO");
            Pick<IWeaponGetter>("WEAP");
            Pick<ISpellGetter>("SPEL");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** WARN: couldn't enumerate representative records: {ex.Message}");
        }

        Console.WriteLine($"    {"type",-5}  {"formid",-25}  {"bytes",10}  {"top-fields",10}");
        foreach (var (code, fkStr) in repFkStrs.OrderBy(kv => kv.Key))
        {
            var req = new
            {
                command = "read_record",
                plugin_path = SkyrimEsmForBatch7,
                formid = fkStr,
                max_depth = 6,
            };
            var r = RunBridgePerf(req);
            int bytes = r.Stdout.Length;
            int fieldCount = -1;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(r.Stdout);
                var root = doc.RootElement;
                if (root.TryGetProperty("fields", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    fieldCount = 0;
                    foreach (var _p in f.EnumerateObject()) fieldCount++;
                }
            }
            catch { /* keep -1 */ }
            Console.WriteLine($"    {code,-5}  {fkStr,-25}  {bytes,10}  {fieldCount,10}");
            payloadTable.Add((code, fkStr, bytes, fieldCount));
        }
    }

    // === Axis 4 — Projection payload-size impact (PROJECTED) ==============
    // Bridge doesn't yet carry `fields` projection (Phase 2 lands it).
    // Phase 1 baseline: full-detail RACE byte size (from Axis 3).
    // Phase 1 projection: caller asking for 3–5 paths typically reduces
    // payload to ~20% of full per PLAN § Background ("~80% token-cost
    // reduction"). Phase 2 measures actual; Phase 1 surfaces the floor.
    long raceFullBytes = -1;
    if (payloadTable.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 4: Projection payload-size impact (PROJECTED — bridge gains `fields` in Phase 2) ──");
        var raceEntry = payloadTable.FirstOrDefault(p => p.Type == "RACE");
        if (raceEntry.Type == "RACE")
        {
            raceFullBytes = raceEntry.Bytes;
            // Estimate projected size — heuristic floor: each retained path
            // contributes O(50–500 bytes) wrapper + one rendered value tree.
            // PLAN § Background headline: ~80% reduction on RACE.
            long projected3to5paths = (long)(raceFullBytes * 0.20);
            Console.WriteLine($"    RACE full-detail baseline:                {raceFullBytes,10} bytes");
            Console.WriteLine($"    projected `fields: [3–5 paths]` (Phase 2): {projected3to5paths,10} bytes (~20% of full)");
            Console.WriteLine($"    projected reduction:                       ~80% (PLAN § Background headline)");
            Console.WriteLine($"    [Phase 2 measures actual; Phase 1 surfaces the baseline floor for comparison]");
        }
        else
        {
            Console.WriteLine($"    SKIP: no RACE entry in payload table");
        }
    }

    // === Axis 5 — Expansion round-trip elimination (PROJECTED) ============
    long withoutExpansionMs = -1;
    int actorEffectCount = -1;
    if (haveBridge && haveSkyrimEsm)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 5: Expansion round-trip elimination (PROJECTED) ──");

        // Pick a vanilla RACE with populated ActorEffect; time
        //   1× read_record(RACE) + N× read_record(each linked SPEL).
        // Project the with-expansion cost as 1× startup + N× marginal.
        try
        {
            using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            var raceWithSpells = srcMod.Races
                .FirstOrDefault(r => r.ActorEffect != null && r.ActorEffect.Count > 0);
            if (raceWithSpells == null)
            {
                Console.WriteLine($"    SKIP: no vanilla RACE has populated ActorEffect");
            }
            else
            {
                var raceFkStr = $"{raceWithSpells.FormKey.ModKey.FileName}:{raceWithSpells.FormKey.ID:X6}";
                actorEffectCount = raceWithSpells.ActorEffect!.Count;
                Console.WriteLine($"    anchor RACE: {raceFkStr} ({raceWithSpells.EditorID})  ActorEffect.Count = {actorEffectCount}");

                // Time without-expansion baseline: read RACE + each linked SPEL.
                var totalSw = System.Diagnostics.Stopwatch.StartNew();
                var raceReq = new
                {
                    command = "read_record",
                    plugin_path = SkyrimEsmForBatch7,
                    formid = raceFkStr,
                    max_depth = 6,
                };
                var raceResp = RunBridgePerf(raceReq);
                int spellsRead = 0;
                foreach (var spellLink in raceWithSpells.ActorEffect!)
                {
                    if (!spellLink.FormKeyNullable.HasValue || spellLink.FormKeyNullable.Value.IsNull) continue;
                    var spellFk = spellLink.FormKeyNullable.Value;
                    if (spellFk.ModKey.FileName != "Skyrim.esm") continue;
                    var spellFkStr = $"{spellFk.ModKey.FileName}:{spellFk.ID:X6}";
                    var spellReq = new
                    {
                        command = "read_record",
                        plugin_path = SkyrimEsmForBatch7,
                        formid = spellFkStr,
                        max_depth = 6,
                    };
                    RunBridgePerf(spellReq);
                    spellsRead++;
                }
                totalSw.Stop();
                withoutExpansionMs = totalSw.ElapsedMilliseconds;

                // Project with-expansion: one bridge call returns RACE + inlined SPELs.
                long projectedExpansionMs = (startupMedian > 0 ? startupMedian : 1300)
                                          + (long)(spellsRead * 10);  // ~10ms per inlined SPEL is the with-expansion floor

                Console.WriteLine($"    without-expansion (RACE + {spellsRead} SPELs serial):  {withoutExpansionMs,7} ms ({1 + spellsRead} subprocess invocations)");
                Console.WriteLine($"    projected with-expansion (one bridge call):           {projectedExpansionMs,7} ms (1 invocation)");
                if (withoutExpansionMs > 0 && projectedExpansionMs > 0)
                {
                    double speedup = (double)withoutExpansionMs / projectedExpansionMs;
                    Console.WriteLine($"    projected speedup ratio:                              {speedup,7:F2}×");
                    Console.WriteLine($"    [Phase 2 measures actual; Phase 1 surfaces the baseline + projection]");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** WARN: expansion-elimination probe threw: {ex.Message}");
        }
    }

    // === Axis 6 — Cross-product timing (Q6 amendment: allow combination) ==
    // formids: [N records] × plugin_names: [M plugins] returns N×M cells.
    // Phase 1 simulates with read_records issuing N×M items (each plugin path
    // × each formid pairing), since the cross-product wrapper layer doesn't
    // exist yet (Phase 2 lands it). Captures wall-clock + payload bytes for
    // N ∈ {10, 50, 100} × M ∈ {2, 5, 10}.
    //
    // Halt threshold: Python's mo2_record_detail uses
    // timeout=max(15s, 5*N×M) per kick-off prompt. If wall-clock exceeds
    // that, increment failure counter so handoff escalates via CONDUCTOR ASK.
    var crossTable = new List<(int N, int M, int Items, long Ms, int Bytes, double TimeoutS, bool TimedOut)>();
    bool crossProductCliff = false;
    if (haveBridge && haveSkyrimEsm)
    {
        Console.WriteLine();
        Console.WriteLine("  ── Axis 6: Cross-product timing (formids × plugin_names per Q6 amendment) ──");
        Console.WriteLine($"    Simulation: read_records with N×M items (one plugin × one formid per item).");
        Console.WriteLine($"    Phase 1 uses Skyrim.esm only as the M=1 case and synthesizes the M>1 case by repeating");
        Console.WriteLine($"    the same plugin path M times — measures the bridge's per-item processing cost without");
        Console.WriteLine($"    exercising plugin-load amortization across distinct plugins (Phase 2's wrapper layer");
        Console.WriteLine($"    does the actual M>1 fan-out across distinct plugin_names).");
        Console.WriteLine();

        // Reuse RACE FormID pool from Axis 2 if available.
        var pool = new List<string>();
        try
        {
            using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
            foreach (var r in srcMod.Races) pool.Add($"{r.FormKey.ModKey.FileName}:{r.FormKey.ID:X6}");
        }
        catch { }

        if (pool.Count == 0)
        {
            Console.WriteLine($"    SKIP: no RACE records for cross-product simulation");
        }
        else
        {
            var nValues = new[] { 10, 50, 100 };
            var mValues = new[] { 2, 5, 10 };
            Console.WriteLine($"    {"N",4}  {"M",4}  {"N×M",6}  {"wall-clock",10}  {"bytes",10}  {"timeout",10}  {"status",-12}");
            foreach (int n in nValues)
            {
                foreach (int m in mValues)
                {
                    int items = n * m;
                    var batch = new List<object>();
                    for (int i = 0; i < items; i++)
                    {
                        // Each cell: (formid_index = i / m), (plugin_repeat = i % m).
                        // Same plugin path M times since Phase 1 has only Skyrim.esm.
                        batch.Add(new
                        {
                            plugin_path = SkyrimEsmForBatch7,
                            formid = pool[(i / m) % pool.Count],
                        });
                    }
                    var req = new
                    {
                        command = "read_records",
                        records = batch,
                        max_depth = 6,
                    };
                    var r = RunBridgePerf(req);
                    int bytes = r.Stdout.Length;
                    double timeoutS = Math.Max(15, 5.0 * items);
                    bool timedOut = r.Ms > timeoutS * 1000;
                    string status = timedOut ? "*** TIMEOUT" : "ok";
                    Console.WriteLine($"    {n,4}  {m,4}  {items,6}  {r.Ms,7} ms  {bytes,10}  {timeoutS,7:F0} s   {status,-12}");
                    crossTable.Add((n, m, items, r.Ms, bytes, timeoutS, timedOut));
                    if (timedOut) crossProductCliff = true;
                }
            }
            Console.WriteLine();
            Console.WriteLine($"    Halt threshold per Q6 amendment: wall-clock > Python timeout = max(15s, 5×N×M).");
            Console.WriteLine($"    Cross-product cliff detected: {crossProductCliff}");
            if (crossProductCliff) p1ReadSideFailures++;
        }
    }

    // ── Record-shape sweep — every IMajorRecordGetter × every FormLink-typed prop.
    Console.WriteLine();
    Console.WriteLine("  ── Record-shape sweep: FormLink-typed properties on IMajorRecordGetter implementations ──");

    // FormLink predicate matches PatchEngine.cs:1182 IsFormLinkType — single-FormLink
    // shape (IFormLinkGetter<>, IFormLink<>, IFormLinkNullable<>, FormLink<>,
    // FormLinkNullable<>) plus list-of variants (IReadOnlyList<IFormLinkGetter<>>,
    // ExtendedList<IFormLinkGetter<>>, etc.).
    static bool IsSingleFormLinkType(Type t)
    {
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(IFormLinkGetter<>)
            || def == typeof(IFormLink<>)
            || def == typeof(IFormLinkNullable<>)
            || def == typeof(FormLink<>)
            || def == typeof(FormLinkNullable<>);
    }

    static (bool IsListOfFormLink, Type? Element) ClassifyListOfFormLink(Type t)
    {
        // Recognize generic single-arg list-shapes that wrap a FormLink type.
        // Mutagen: IReadOnlyList<T>, ExtendedList<T>, plus any IEnumerable<T>
        // where T is a single-FormLink type. Excludes byte arrays / strings.
        if (t == typeof(string)) return (false, null);
        if (t.IsArray)
        {
            var elt = t.GetElementType();
            if (elt != null && IsSingleFormLinkType(elt)) return (true, elt);
            return (false, null);
        }
        if (!t.IsGenericType) return (false, null);
        // Walk generic type itself + all generic interface implementations.
        var candidates = new List<Type> { t };
        candidates.AddRange(t.GetInterfaces().Where(i => i.IsGenericType));
        foreach (var c in candidates)
        {
            var args = c.GetGenericArguments();
            if (args.Length != 1) continue;
            if (IsSingleFormLinkType(args[0])) return (true, args[0]);
        }
        return (false, null);
    }

    static string FriendlyTypeStatic(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var name = t.Name.Substring(0, t.Name.IndexOf('`'));
        var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyTypeStatic));
        return $"{name}<{args}>";
    }

    // Discover concrete record getter interfaces. The bridge's runtime surface
    // is IMajorRecordGetter — every concrete getter interface
    // (IRaceGetter, INpcGetter, IQuestGetter, etc.) extends from it. Sweep
    // every interface in Mutagen.Bethesda.Skyrim that derives from
    // IMajorRecordGetter and ends in "Getter" (excluding common-base layers).
    {
        var skyrimAssembly = typeof(IQuestGetter).Assembly;
        var getterInterfaces = skyrimAssembly.GetTypes()
            .Where(t => t.IsInterface)
            .Where(t => t.Namespace == "Mutagen.Bethesda.Skyrim")
            .Where(t => t.Name.EndsWith("Getter", StringComparison.Ordinal))
            .Where(t => typeof(IMajorRecordGetter).IsAssignableFrom(t))
            .Where(t => t != typeof(IMajorRecordGetter)
                     && t != typeof(ISkyrimMajorRecordGetter))
            .OrderBy(t => t.Name)
            .ToList();

        Console.WriteLine($"    Concrete getter interfaces sweep: {getterInterfaces.Count}");
        Console.WriteLine();

        var sweepRows = new List<(string IfaceName, string PropName, string PropTypeShort, string Classification, string LinkedTargetShort)>();

        foreach (var iface in getterInterfaces)
        {
            // Aggregate properties from interface + parents (IMajorRecordGetter etc).
            // Use full BindingFlags. GetProperties on an interface returns DECLARED only;
            // walk parent interfaces to pick up inherited props (e.g. EditorID).
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void CollectProps(Type t, List<PropertyInfo> acc)
            {
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!seen.Add(p.Name)) continue;
                    acc.Add(p);
                }
                foreach (var pi in t.GetInterfaces()) CollectProps(pi, acc);
            }
            var props = new List<PropertyInfo>();
            CollectProps(iface, props);

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                var pt = prop.PropertyType;

                // Single-FormLink-typed property?
                if (IsSingleFormLinkType(pt))
                {
                    var argShort = FriendlyTypeStatic(pt.GetGenericArguments()[0]);
                    sweepRows.Add((iface.Name, prop.Name, FriendlyTypeStatic(pt), "single", argShort));
                    continue;
                }

                // List-of-FormLink-typed property?
                var (isList, elt) = ClassifyListOfFormLink(pt);
                if (isList && elt != null)
                {
                    var argShort = FriendlyTypeStatic(elt.GetGenericArguments()[0]);
                    sweepRows.Add((iface.Name, prop.Name, FriendlyTypeStatic(pt), "list", argShort));
                    continue;
                }
            }
        }

        // Group by interface for compact output.
        var byIface = sweepRows.GroupBy(r => r.IfaceName).OrderBy(g => g.Key).ToList();
        int withFormLinks = byIface.Count;
        int totalRows = sweepRows.Count;
        Console.WriteLine($"    Interfaces with at least one FormLink-typed property: {withFormLinks} / {getterInterfaces.Count}");
        Console.WriteLine($"    Total FormLink-typed properties across all getters:    {totalRows}");
        Console.WriteLine();
        Console.WriteLine($"    {"interface",-32}  {"property",-25}  {"shape",-7}  {"target",-30}  type");
        Console.WriteLine($"    {new string('-', 32)}  {new string('-', 25)}  {new string('-', 7)}  {new string('-', 30)}  {new string('-', 50)}");
        foreach (var grp in byIface)
        {
            foreach (var (ifaceName, propName, propTypeShort, classification, linkedTargetShort) in grp.OrderBy(r => r.PropName))
            {
                Console.WriteLine($"    {ifaceName,-32}  {propName,-25}  {classification,-7}  {linkedTargetShort,-30}  {propTypeShort}");
            }
        }

        // Specifically resolve the ActorEffect-vs-ActorEffects ambiguity
        // (PLAN.md § Phase 1 step 3 + § Q6 acceptance).
        Console.WriteLine();
        Console.WriteLine("  ── ActorEffect vs ActorEffects resolution (Mutagen 0.53.1 ground truth) ──");
        var raceGetter = typeof(IRaceGetter);
        var raceProps = raceGetter.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasActorEffect = raceProps.Any(p => p.Name == "ActorEffect");
        var hasActorEffects = raceProps.Any(p => p.Name == "ActorEffects");
        Console.WriteLine($"    IRaceGetter.ActorEffect  exists: {hasActorEffect}");
        Console.WriteLine($"    IRaceGetter.ActorEffects exists: {hasActorEffects}");
        if (!hasActorEffect && !hasActorEffects)
        {
            Console.WriteLine($"    *** UNEXPECTED: NEITHER ActorEffect nor ActorEffects exists on IRaceGetter — schema drift?");
            p1ReadSideFailures++;
        }
        else if (hasActorEffect && hasActorEffects)
        {
            Console.WriteLine($"    *** UNEXPECTED: BOTH ActorEffect and ActorEffects exist on IRaceGetter — schema drift?");
            p1ReadSideFailures++;
        }
        else
        {
            string canonical = hasActorEffect ? "ActorEffect" : "ActorEffects";
            Console.WriteLine($"    canonical name: {canonical} (Mutagen 0.53.1 ground truth)");
        }

        // Surface canonical RACE FormLink-typed property list — these are the
        // anchors Phase 2's MATRIX rows substitute into the placeholder
        // <Skeleton-or-confirmed-scalar> / <ActorEffect-or-confirmed-list> /
        // <expanded-list-property> / <projected-list-property> slots.
        Console.WriteLine();
        Console.WriteLine("  ── Canonical RACE FormLink-typed properties (matrix placeholder substitutions) ──");
        var raceLinkRows = sweepRows.Where(r => r.IfaceName == "IRaceGetter").OrderBy(r => r.PropName).ToList();
        if (raceLinkRows.Count == 0)
        {
            Console.WriteLine($"    (no FormLink-typed properties on IRaceGetter)");
        }
        else
        {
            Console.WriteLine($"    {"property",-25}  {"shape",-7}  {"target",-30}");
            foreach (var (_, propName, _, classification, linkedTargetShort) in raceLinkRows)
            {
                Console.WriteLine($"    {propName,-25}  {classification,-7}  {linkedTargetShort,-30}");
            }
        }

        // RACE anchor selection for Layer 1.P — pick 3 vanilla RACE records
        // with populated ActorEffect (the canonical list-of-FormLinks property
        // per PLAN § Phase 1 step 5). Phase 2's matrix substitutes these.
        if (haveSkyrimEsm)
        {
            Console.WriteLine();
            Console.WriteLine("  ── RACE anchor selection: vanilla Skyrim.esm RACE records with populated ActorEffect ──");
            try
            {
                using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
                var withSpells = srcMod.Races
                    .Where(r => r.ActorEffect != null && r.ActorEffect.Count > 0)
                    .OrderBy(r => r.FormKey.ID)
                    .Take(10)
                    .ToList();
                Console.WriteLine($"    found {withSpells.Count} qualifying RACE record(s) (showing first 10):");
                foreach (var r in withSpells)
                {
                    Console.WriteLine($"      Skyrim.esm:{r.FormKey.ID:X6}  {r.EditorID,-30}  ActorEffect.Count = {r.ActorEffect!.Count}");
                }
                if (withSpells.Count >= 3)
                {
                    Console.WriteLine($"    Phase 1 anchor candidates (first 3 with populated ActorEffect):");
                    foreach (var r in withSpells.Take(3))
                    {
                        Console.WriteLine($"      Skyrim.esm:{r.FormKey.ID:X6}  ({r.EditorID})");
                    }
                }
                else
                {
                    Console.WriteLine($"    *** WARN: <3 vanilla RACE records have populated ActorEffect; Phase 2 anchor pool may need a synthetic fixture");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    *** WARN: RACE anchor selection threw: {ex.Message}");
            }
        }

        // QUST anchor surfacing (Layer 1.P.batch.QUST cell — Phase 1 hand-back
        // checklist item). v2.9.1's anchor was Skyrim.esm:04C49D
        // (FollowerCommentary01); for v2.9.2's batch axis exercise we just
        // need 3+ QUST FormIDs; v2.9.1's anchors qualify.
        if (haveSkyrimEsm)
        {
            Console.WriteLine();
            Console.WriteLine("  ── QUST anchor selection (Layer 1.P.batch.QUST — 3+ vanilla QUST FormIDs) ──");
            try
            {
                using var srcMod = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsmForBatch7, SkyrimRelease.SkyrimSE);
                var firstThree = srcMod.Quests
                    .OrderBy(q => q.FormKey.ID)
                    .Take(10)
                    .ToList();
                Console.WriteLine($"    first 10 QUST records in Skyrim.esm (FormID order):");
                foreach (var q in firstThree)
                {
                    Console.WriteLine($"      Skyrim.esm:{q.FormKey.ID:X6}  {q.EditorID ?? "(null)"}");
                }
                Console.WriteLine($"    v2.9.1's anchor Skyrim.esm:04C49D (FollowerCommentary01) + 0E3145 (CR12) qualify;");
                Console.WriteLine($"    Phase 2 picks 3 from above OR re-uses v2.9.1's pair + first qualifying third.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    *** WARN: QUST anchor selection threw: {ex.Message}");
            }
        }

        // NPC_ Factions structure check (Scenario 3.2 precondition per Phase 1
        // hand-back checklist item). NPC_'s `Factions` list-of-RankPlacement
        // sub-structs each carry a `Faction` FormLink to FCTN. Scenario 3.2
        // uses `expand_links: ["Factions.Faction"]` (auto-traversal per Q1).
        Console.WriteLine();
        Console.WriteLine("  ── NPC_ Factions structure check (Scenario 3.2 precondition) ──");
        var npcGetter = typeof(INpcGetter);
        var factionsProp = npcGetter.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Equals("Factions", StringComparison.OrdinalIgnoreCase));
        if (factionsProp == null)
        {
            Console.WriteLine($"    *** UNEXPECTED: INpcGetter has no `Factions` property — Scenario 3.2 precondition fails");
            Console.WriteLine($"        Phase 3 falls back to Scenario 3.1 only (per MATRIX § Layer 3.2 conditional clause)");
            p1ReadSideFailures++;
        }
        else
        {
            Console.WriteLine($"    INpcGetter.Factions  declared type: {FriendlyTypeStatic(factionsProp.PropertyType)}");
            // Walk: Factions is IReadOnlyList<IRankPlacementGetter>; check the sub-struct's
            // FormLink-to-FCTN slot.
            Type? rankElt = null;
            foreach (var iface in factionsProp.PropertyType.GetInterfaces().Concat(new[] { factionsProp.PropertyType }))
            {
                if (!iface.IsGenericType) continue;
                var ifArgs = iface.GetGenericArguments();
                if (ifArgs.Length == 1 && ifArgs[0].Namespace?.StartsWith("Mutagen.Bethesda") == true)
                {
                    rankElt = ifArgs[0];
                    break;
                }
            }
            if (rankElt == null)
            {
                Console.WriteLine($"    *** WARN: couldn't extract Factions element type");
            }
            else
            {
                Console.WriteLine($"    Factions element type: {FriendlyTypeStatic(rankElt)}");
                // Find the FormLink sub-prop on the rank-placement struct.
                var rankProps = rankElt.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var formLinkSubProps = rankProps.Where(p => IsSingleFormLinkType(p.PropertyType)).ToList();
                if (formLinkSubProps.Count == 0)
                {
                    Console.WriteLine($"    *** UNEXPECTED: rank-placement element has no FormLink-typed sub-property");
                    Console.WriteLine($"        Scenario 3.2 (`Factions.Faction` expansion) precondition fails; Phase 3 skips Scenario 3.2");
                    p1ReadSideFailures++;
                }
                else
                {
                    foreach (var sp in formLinkSubProps)
                        Console.WriteLine($"      sub-property: {sp.Name,-15}  type: {FriendlyTypeStatic(sp.PropertyType)}");
                    var faction = formLinkSubProps.FirstOrDefault(p => p.Name.Equals("Faction", StringComparison.OrdinalIgnoreCase));
                    if (faction != null)
                    {
                        Console.WriteLine($"    canonical Scenario 3.2 expand_links path: \"Factions.Faction\" (auto-traversal per Q1)");
                    }
                    else
                    {
                        Console.WriteLine($"    *** WARN: no `Faction` sub-property — Scenario 3.2 path needs alternative slot name");
                    }
                }
            }
        }

        // Sweep summary table — counts.
        Console.WriteLine();
        Console.WriteLine("  ── Sweep summary ──");
        Console.WriteLine($"    Concrete getter interfaces scanned:                 {getterInterfaces.Count}");
        Console.WriteLine($"    Interfaces with FormLink-typed property(s):         {withFormLinks}");
        Console.WriteLine($"    Total FormLink-typed properties across all getters: {totalRows}");
        Console.WriteLine($"    RACE FormLink-typed properties:                     {raceLinkRows.Count}");
    }

    Console.WriteLine();
    Console.WriteLine("  ── Phase 1 perf-and-shape summary ──");
    Console.WriteLine($"    Subprocess startup median:                  {(startupMedian > 0 ? $"{startupMedian} ms (band 1200–1400 ms)" : "n/a")}");
    if (marginalTable.Count > 0)
    {
        var b1 = marginalTable.FirstOrDefault(x => x.N == 1);
        var bMax = marginalTable.OrderByDescending(x => x.N).FirstOrDefault();
        if (b1.N == 1 && bMax.N > 1)
        {
            double mm = (double)(bMax.Ms - b1.Ms) / (bMax.N - 1);
            Console.WriteLine($"    Per-record marginal at N={bMax.N}:                {mm:F2} ms (band 5–20 ms)");
        }
    }
    Console.WriteLine($"    Per-record full-detail payloads measured:   {payloadTable.Count} types");
    Console.WriteLine($"    Cross-product cliff (Q6 amendment timeout): {(crossProductCliff ? "*** CLIFF — escalate" : "no cliff")}");
}
Console.WriteLine($"=== v2.9.2 P1 read-side perf + shape sweep: {(p1ReadSideFailures == 0 ? "ALL PASS" : $"{p1ReadSideFailures} FAILURE(S)")} ===");

Console.WriteLine();
int totalFailures = auditFailures + effectsAuditFailures + inventoryFailures + p2aFailures + p2bFailures + p2cFailures + p2dFailures + p4InfoFailures + p1MultiCondFailures + p2QustFailures + p1ReadSideFailures;
if (totalFailures > 0)
{
    Console.WriteLine($"=== probe FAILED: {totalFailures} audit failure(s) ({auditFailures} v2.7.1 + {effectsAuditFailures} v2.8 P1 + {inventoryFailures} v2.9 P1 + {p2aFailures} v2.9 P2A + {p2bFailures} v2.9 P2B + {p2cFailures} v2.9 P2C + {p2dFailures} v2.9 P2D + {p4InfoFailures} v2.9 P4-INFO + {p1MultiCondFailures} v2.9.1 P1 multi-cond sweep + {p2QustFailures} v2.9.1 P2 quest-cond + {p1ReadSideFailures} v2.9.2 P1 read-side perf-and-shape) — reclassify in AUDIT/EFFECTS_AUDIT/CONDITIONS_AUDIT ===");
    Environment.Exit(1);
}
Console.WriteLine("=== probe complete ===");
