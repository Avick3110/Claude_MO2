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

Console.WriteLine();
int totalFailures = auditFailures + effectsAuditFailures;
if (totalFailures > 0)
{
    Console.WriteLine($"=== probe FAILED: {totalFailures} audit failure(s) ({auditFailures} v2.7.1 + {effectsAuditFailures} v2.8 P1) — reclassify in AUDIT/EFFECTS_AUDIT ===");
    Environment.Exit(1);
}
Console.WriteLine("=== probe complete ===");
