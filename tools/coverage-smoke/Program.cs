// coverage-smoke — inline smoke harness for v2.7.1 covering Tiers D, C, and A.
//
// Tier D (Phase 1) — silent-failure detection (Tests 1-3):
//   1. add_perks on Container — asserts unmatched_operators error + rollback.
//   2. set_fields(Weight) on Container — asserts success (no Tier D regression).
//   3. remove_conditions on Armor — asserts ApplyRemoveConditions throws on
//      missing Conditions property (v2.7.1 alignment with ApplyAddConditions).
//
// Tier C (Phase 2) — bracket-indexer dict mutation (Tests 4-6):
//   4. set_fields(Starting[Health]=250) on RACE — bracket-indexer write,
//      readback confirms Magicka/Stamina preserved.
//   5. set_fields(Regen={Health,Magicka}) on RACE — whole-dict merge,
//      readback confirms Stamina preserved (not replaced).
//   6. set_fields(Starting[Bogus]=100) on RACE — Enum.Parse failure surfaces
//      as per-record error with rollback (general catch arm, not Tier D).
//
// Tier A (Phase 3) — comprehensive operator wire-ups (Tests 7-22):
//   7-12.  add_keywords on RACE/FURN/ACTI/LCTN/SPEL/MGEF.
//   13-18. remove_keywords on the same six record types.
//   19-20. add_spells / remove_spells on RACE.
//   21-22. add_items on LeveledNpc / LeveledSpell.
//   Each test picks a representative source record, exercises the operator
//   via the bridge, and reads back the output ESP via Mutagen to confirm
//   the mutation actually landed (not just that the bridge claimed success).
//
// Run from the repo root:
//   dotnet run -c Release --project tools/coverage-smoke
//
// Sibling of tools/race-probe/. Stays in the repo as a regression check for
// Tier D semantics (mods-key present iff handler-matched-and-ran), Tier C
// semantics (bracket-indexer + whole-dict merge), and Tier A wire-up coverage
// for the 16 (operator, record-type) pairs newly supported in v2.7.1.

using System.Diagnostics;
using System.Text.Json;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

const string SkyrimEsm = @"E:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Skyrim.esm";

if (!File.Exists(SkyrimEsm))
{
    Console.Error.WriteLine($"FAIL: Skyrim.esm not found at {SkyrimEsm}");
    return 2;
}

// Resolve mutagen-bridge.exe relative to this project: ../mutagen-bridge/bin/Release/net8.0/mutagen-bridge.exe
var thisDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
var bridgeExe = Path.GetFullPath(Path.Combine(thisDir,
    "..", "..", "..", "..", "mutagen-bridge", "bin", "Release", "net8.0", "mutagen-bridge.exe"));

if (!File.Exists(bridgeExe))
{
    Console.Error.WriteLine($"FAIL: mutagen-bridge.exe not found at {bridgeExe}");
    Console.Error.WriteLine("Run `dotnet build -c Release` in tools/mutagen-bridge first.");
    return 2;
}

Console.WriteLine($"bridge: {bridgeExe}");
Console.WriteLine($"source: {SkyrimEsm}");
Console.WriteLine();

using var source = SkyrimMod.CreateFromBinaryOverlay(SkyrimEsm, SkyrimRelease.SkyrimSE);

var firstContainer = source.Containers.FirstOrDefault()
    ?? throw new InvalidOperationException("No Container in Skyrim.esm");
var firstArmor = source.Armors.FirstOrDefault()
    ?? throw new InvalidOperationException("No Armor in Skyrim.esm");

// Pick a race with populated Starting + Regen so the merge/preservation
// assertions in tests 5 + 6 are meaningful. Playable races (NordRace,
// ImperialRace, etc.) have all three BasicStat keys populated; creature
// races may have empty dicts.
var pickedRace = source.Races.FirstOrDefault(r =>
    r.Starting != null && r.Starting.Count >= 3 &&
    r.Regen    != null && r.Regen.Count    >= 3)
    ?? throw new InvalidOperationException("No Race with populated Starting+Regen in Skyrim.esm");

Console.WriteLine($"CONT: {firstContainer.FormKey} ({firstContainer.EditorID})");
Console.WriteLine($"ARMO: {firstArmor.FormKey} ({firstArmor.EditorID})");
Console.WriteLine($"RACE: {pickedRace.FormKey} ({pickedRace.EditorID})");
Console.WriteLine($"  Starting: Health={pickedRace.Starting![BasicStat.Health]}, " +
                  $"Magicka={pickedRace.Starting[BasicStat.Magicka]}, " +
                  $"Stamina={pickedRace.Starting[BasicStat.Stamina]}");
Console.WriteLine($"  Regen:    Health={pickedRace.Regen![BasicStat.Health]}, " +
                  $"Magicka={pickedRace.Regen[BasicStat.Magicka]}, " +
                  $"Stamina={pickedRace.Regen[BasicStat.Stamina]}");
Console.WriteLine();

float origStaminaStarting = pickedRace.Starting[BasicStat.Stamina];
float origMagickaStarting = pickedRace.Starting[BasicStat.Magicka];
float origStaminaRegen    = pickedRace.Regen[BasicStat.Stamina];

var outDir = Path.Combine(Path.GetTempPath(), "coverage-smoke");
Directory.CreateDirectory(outDir);

var loadOrderListings = new[]
{
    new { mod_key = "Skyrim.esm", path = SkyrimEsm, enabled = true }
};

int failures = 0;

// ── Test 1: failing case — add_perks on Container (Tier D should error) ──
{
    var outPath = Path.Combine(outDir, "test1-tier-d-failing.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstContainer.FormKey),
                source_path = SkyrimEsm,
                add_perks = new[] { "Skyrim.esm:000F2CB6" }, // Light Armor perk; valid FormKey, but irrelevant — request is unsupported on CONT
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 1: add_perks on CONT (expected: Tier D unmatched-operator error) ──");
    Console.WriteLine($"  exit code: {exit}");
    Console.WriteLine($"  response:");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;

    bool ok = true;
    if (!root.GetProperty("success").GetBoolean() == false) { Console.WriteLine("  FAIL: success should be false"); ok = false; }
    if (root.GetProperty("failed_count").GetInt32() != 1) { Console.WriteLine("  FAIL: failed_count should be 1"); ok = false; }
    if (root.GetProperty("records_written").GetInt32() != 0) { Console.WriteLine("  FAIL: records_written should be 0 (override rolled back)"); ok = false; }

    var details = root.GetProperty("details");
    if (details.GetArrayLength() != 1) { Console.WriteLine("  FAIL: details should have 1 entry"); ok = false; }
    else
    {
        var d0 = details[0];
        var recordType = d0.TryGetProperty("record_type", out var rt) ? rt.GetString() : null;
        if (recordType != "CONT") { Console.WriteLine($"  FAIL: record_type expected CONT, got {recordType}"); ok = false; }

        if (!d0.TryGetProperty("unmatched_operators", out var unmatched))
        {
            Console.WriteLine("  FAIL: unmatched_operators field missing");
            ok = false;
        }
        else
        {
            var ops = unmatched.EnumerateArray().Select(e => e.GetString()).ToList();
            if (ops.Count != 1 || ops[0] != "add_perks")
            {
                Console.WriteLine($"  FAIL: unmatched_operators should be [\"add_perks\"], got [{string.Join(", ", ops)}]");
                ok = false;
            }
        }

        if (!d0.TryGetProperty("error", out var err) || string.IsNullOrEmpty(err.GetString()))
        {
            Console.WriteLine("  FAIL: error field should be set");
            ok = false;
        }
    }

    // Also: output ESP must NOT exist (override rolled back, no records → bridge errors before write).
    if (File.Exists(outPath))
    {
        Console.WriteLine($"  FAIL: output ESP should not have been written (was rolled back)");
        ok = false;
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 2: passing case — set_fields on Container (supported, Tier D should NOT error) ──
{
    var outPath = Path.Combine(outDir, "test2-tier-d-passing.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstContainer.FormKey),
                source_path = SkyrimEsm,
                set_fields = new Dictionary<string, object> {
                    // Container has a Weight property (float) directly accessible by reflection.
                    { "Weight", 99.5f }
                },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 2: set_fields(Weight) on CONT (expected: success) ──");
    Console.WriteLine($"  exit code: {exit}");
    Console.WriteLine($"  response:");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;

    bool ok = true;
    if (root.GetProperty("success").GetBoolean() != true) { Console.WriteLine("  FAIL: success should be true"); ok = false; }
    if (root.GetProperty("records_written").GetInt32() != 1) { Console.WriteLine("  FAIL: records_written should be 1"); ok = false; }

    var details = root.GetProperty("details");
    if (details.GetArrayLength() == 1)
    {
        var d0 = details[0];
        if (d0.TryGetProperty("unmatched_operators", out _))
        {
            Console.WriteLine("  FAIL: unmatched_operators must be absent on success");
            ok = false;
        }
        if (d0.TryGetProperty("modifications", out var mods)
            && mods.TryGetProperty("fields_set", out var fs)
            && fs.GetInt32() == 1)
        {
            // expected
        }
        else
        {
            Console.WriteLine("  FAIL: modifications.fields_set should be 1");
            ok = false;
        }
    }
    else { Console.WriteLine("  FAIL: details should have 1 entry"); ok = false; }

    if (!File.Exists(outPath))
    {
        Console.WriteLine("  FAIL: output ESP should have been written");
        ok = false;
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 3: aligned-throw case — remove_conditions on Armor (no Conditions property) ──
//
// Pre-v2.7.1: ApplyRemoveConditions silently returned 0 → mods["conditions_removed"]
// would not have been written under the old conditional pattern, but the body in
// ApplyModifications writes it unconditionally on the supported path. After the
// v2.7.1 alignment, ApplyRemoveConditions throws InvalidOperationException
// ("does not support conditions") on missing Conditions property → the existing
// ProcessOverride catch-all rolls back the override and surfaces the error message.
//
// Tier D's UnsupportedOperatorException path does NOT fire here — the helper
// itself throws first, so this exercises the OTHER catch arm.
{
    var outPath = Path.Combine(outDir, "test3-conditions-aligned-throw.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstArmor.FormKey),
                source_path = SkyrimEsm,
                remove_conditions = new[]
                {
                    new { index = 0, function = (string?)null }
                },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 3: remove_conditions on ARMO (expected: aligned-throw error) ──");
    Console.WriteLine($"  exit code: {exit}");
    Console.WriteLine($"  response:");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;

    bool ok = true;
    if (root.GetProperty("success").GetBoolean() != false) { Console.WriteLine("  FAIL: success should be false"); ok = false; }

    var details = root.GetProperty("details");
    if (details.GetArrayLength() == 1)
    {
        var d0 = details[0];
        if (!d0.TryGetProperty("error", out var err) || string.IsNullOrEmpty(err.GetString()))
        {
            Console.WriteLine("  FAIL: error field should be set");
            ok = false;
        }
        else if (!err.GetString()!.Contains("does not support conditions"))
        {
            Console.WriteLine($"  FAIL: error should mention 'does not support conditions', got: {err.GetString()}");
            ok = false;
        }
    }
    else { Console.WriteLine("  FAIL: details should have 1 entry"); ok = false; }

    if (File.Exists(outPath))
    {
        Console.WriteLine($"  FAIL: output ESP should not have been written");
        ok = false;
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 4: Tier C bracket-indexer write — Starting[Health] = 250 on a Race ──
//
// Confirms: bridge accepts `Starting[Health]: 250.0` syntax in set_fields,
// writes the override ESP, and read-back through Mutagen shows
// Starting[Health] == 250 with Magicka/Stamina preserved from the source.
{
    var outPath = Path.Combine(outDir, "test4-tier-c-bracket-indexer.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(pickedRace.FormKey),
                source_path = SkyrimEsm,
                set_fields = new Dictionary<string, object> {
                    { "Starting[Health]", 250.0f }
                },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 4: set_fields(Starting[Health]=250) on RACE (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    bool ok = doc.RootElement.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
    else
    {
        // Read back the override via Mutagen.
        using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
        var overridden = outMod.Races.FirstOrDefault(r => r.FormKey == pickedRace.FormKey);
        if (overridden == null) { Console.WriteLine("  FAIL: race override not found in output ESP"); ok = false; }
        else
        {
            var s = overridden.Starting;
            if (s == null) { Console.WriteLine("  FAIL: Starting null on readback"); ok = false; }
            else
            {
                if (s[BasicStat.Health] != 250.0f)
                {
                    Console.WriteLine($"  FAIL: Starting[Health] expected 250, got {s[BasicStat.Health]}");
                    ok = false;
                }
                if (s[BasicStat.Magicka] != origMagickaStarting)
                {
                    Console.WriteLine($"  FAIL: Starting[Magicka] expected {origMagickaStarting} (preserved), got {s[BasicStat.Magicka]}");
                    ok = false;
                }
                if (s[BasicStat.Stamina] != origStaminaStarting)
                {
                    Console.WriteLine($"  FAIL: Starting[Stamina] expected {origStaminaStarting} (preserved), got {s[BasicStat.Stamina]}");
                    ok = false;
                }
                if (ok) Console.WriteLine($"  readback: Starting={{Health={s[BasicStat.Health]}, Magicka={s[BasicStat.Magicka]}, Stamina={s[BasicStat.Stamina]}}}");
            }
        }
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 5: Tier C whole-dict merge — Regen: { Health, Magicka } on a Race ──
//
// Confirms: bridge accepts a JSON object value for a dict-typed property,
// and that the per-key merge preserves the un-named third key (Stamina)
// rather than replacing the whole dict.
{
    var outPath = Path.Combine(outDir, "test5-tier-c-whole-dict-merge.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(pickedRace.FormKey),
                source_path = SkyrimEsm,
                set_fields = new Dictionary<string, object> {
                    { "Regen", new Dictionary<string, float> { { "Health", 1.5f }, { "Magicka", 2.5f } } }
                },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 5: set_fields(Regen={Health,Magicka}) on RACE (expected: merge, Stamina preserved) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    bool ok = doc.RootElement.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
    else
    {
        using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
        var overridden = outMod.Races.FirstOrDefault(r => r.FormKey == pickedRace.FormKey);
        if (overridden == null) { Console.WriteLine("  FAIL: race override not found in output ESP"); ok = false; }
        else
        {
            var r = overridden.Regen;
            if (r == null) { Console.WriteLine("  FAIL: Regen null on readback"); ok = false; }
            else
            {
                if (r[BasicStat.Health] != 1.5f)
                {
                    Console.WriteLine($"  FAIL: Regen[Health] expected 1.5, got {r[BasicStat.Health]}");
                    ok = false;
                }
                if (r[BasicStat.Magicka] != 2.5f)
                {
                    Console.WriteLine($"  FAIL: Regen[Magicka] expected 2.5, got {r[BasicStat.Magicka]}");
                    ok = false;
                }
                if (r[BasicStat.Stamina] != origStaminaRegen)
                {
                    Console.WriteLine($"  FAIL: Regen[Stamina] expected {origStaminaRegen} (preserved by merge), got {r[BasicStat.Stamina]}");
                    ok = false;
                }
                if (ok) Console.WriteLine($"  readback: Regen={{Health={r[BasicStat.Health]}, Magicka={r[BasicStat.Magicka]}, Stamina={r[BasicStat.Stamina]}}}");
            }
        }
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 6: Tier C unparseable enum — Starting[Bogus] should error and roll back ──
//
// Goes through ProcessOverride's general catch arm (Enum.Parse throws
// ArgumentException). NOT Tier D's UnsupportedOperatorException — set_fields
// always matches a handler, so the failure is in value conversion, not
// operator dispatch.
{
    var outPath = Path.Combine(outDir, "test6-tier-c-unparseable-enum.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(pickedRace.FormKey),
                source_path = SkyrimEsm,
                set_fields = new Dictionary<string, object> {
                    { "Starting[Bogus]", 100.0f }
                },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, stderr, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 6: set_fields(Starting[Bogus]=100) on RACE (expected: error + rollback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;

    bool ok = true;
    if (root.GetProperty("success").GetBoolean() != false) { Console.WriteLine("  FAIL: success should be false"); ok = false; }

    var details = root.GetProperty("details");
    if (details.GetArrayLength() == 1)
    {
        var d0 = details[0];
        if (!d0.TryGetProperty("error", out var err) || string.IsNullOrEmpty(err.GetString()))
        {
            Console.WriteLine("  FAIL: error field should be set");
            ok = false;
        }
        else if (!err.GetString()!.Contains("Bogus", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  FAIL: error should mention 'Bogus', got: {err.GetString()}");
            ok = false;
        }
        // unmatched_operators must be absent — set_fields IS dispatched, the failure is in conversion.
        if (d0.TryGetProperty("unmatched_operators", out _))
        {
            Console.WriteLine("  FAIL: unmatched_operators should be absent (set_fields IS a matched handler)");
            ok = false;
        }
    }
    else { Console.WriteLine("  FAIL: details should have 1 entry"); ok = false; }

    if (File.Exists(outPath))
    {
        Console.WriteLine($"  FAIL: output ESP should not have been written");
        ok = false;
    }

    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ═══════════════════════════════════════════════════════════════════════════
// Phase 3 / Tier A wire-ups — 16 (operator, record-type) pairs
// ═══════════════════════════════════════════════════════════════════════════

// Pick representative records — six keyword-carrying types where Keywords is
// already populated, so the same record exercises both ADD (fresh kw not in
// the list) and REMOVE (existing kw from the list) paths.
var firstRaceWithKw = source.Races.FirstOrDefault(r => r.Keywords != null && r.Keywords.Count >= 1)
    ?? throw new InvalidOperationException("No Race with Keywords in Skyrim.esm");
var firstFurnitureWithKw = source.Furniture.FirstOrDefault(f => f.Keywords != null && f.Keywords.Count >= 1)
    ?? throw new InvalidOperationException("No Furniture with Keywords in Skyrim.esm");
var firstActivatorWithKw = source.Activators.FirstOrDefault(a => a.Keywords != null && a.Keywords.Count >= 1)
    ?? throw new InvalidOperationException("No Activator with Keywords in Skyrim.esm");
var firstLocationWithKw = source.Locations.FirstOrDefault(l => l.Keywords != null && l.Keywords.Count >= 1)
    ?? throw new InvalidOperationException("No Location with Keywords in Skyrim.esm");
// Vanilla Skyrim.esm has zero SPEL records with populated Keywords (mod
// overhauls like Requiem add them, but the base game leaves the slot empty).
// Fall back to the first SPEL — Test 11 (ADD) still verifies wire-up by
// adding a fresh kw; Test 17 (REMOVE) becomes a Tier D wire-up check
// (remove dispatch ran, mods key written) with keywords_removed=0.
var firstSpellWithKw = source.Spells.FirstOrDefault(s => s.Keywords != null && s.Keywords.Count >= 1)
    ?? source.Spells.FirstOrDefault()
    ?? throw new InvalidOperationException("No Spell in Skyrim.esm");
bool spellHasExistingKw = firstSpellWithKw.Keywords != null && firstSpellWithKw.Keywords.Count >= 1;
var firstMagicEffectWithKw = source.MagicEffects.FirstOrDefault(m => m.Keywords != null && m.Keywords.Count >= 1)
    ?? throw new InvalidOperationException("No MagicEffect with Keywords in Skyrim.esm");

// Race with ActorEffect populated for spells tests (19-20).
var firstRaceWithSpells = source.Races.FirstOrDefault(r => r.ActorEffect != null && r.ActorEffect.Count >= 1)
    ?? throw new InvalidOperationException("No Race with ActorEffect in Skyrim.esm");

// First leveled-list records for add_items tests (21-22).
var firstLvln = source.LeveledNpcs.FirstOrDefault()
    ?? throw new InvalidOperationException("No LeveledNpc in Skyrim.esm");
var firstLvsp = source.LeveledSpells.FirstOrDefault()
    ?? throw new InvalidOperationException("No LeveledSpell in Skyrim.esm");

// Fresh references for the leveled-list tests: NPC for LVLN, Spell for LVSP.
var freshNpcRef = source.Npcs.First().FormKey;
var freshSpellRef = source.Spells.First().FormKey;

// Pick a fresh keyword from Skyrim.esm not currently on the given list.
FormKey FreshKwFor(IReadOnlyList<IFormLinkGetter<IKeywordGetter>>? existing)
{
    var existingFks = new HashSet<FormKey>(existing?.Select(k => k.FormKey) ?? Enumerable.Empty<FormKey>());
    var fresh = source.Keywords.FirstOrDefault(k => !existingFks.Contains(k.FormKey))
        ?? throw new InvalidOperationException("No fresh keyword available in Skyrim.esm");
    return fresh.FormKey;
}

// Pick a fresh spell not currently on the given ActorEffect list.
FormKey FreshSpellFor(IReadOnlyList<IFormLinkGetter<ISpellRecordGetter>>? existing)
{
    var existingFks = new HashSet<FormKey>(existing?.Select(s => s.FormKey) ?? Enumerable.Empty<FormKey>());
    var fresh = source.Spells.FirstOrDefault(s => !existingFks.Contains(s.FormKey))
        ?? throw new InvalidOperationException("No fresh spell available in Skyrim.esm");
    return fresh.FormKey;
}

Console.WriteLine($"RACE-kw:    {firstRaceWithKw.FormKey} ({firstRaceWithKw.EditorID}, Keywords.Count={firstRaceWithKw.Keywords!.Count})");
Console.WriteLine($"FURN-kw:    {firstFurnitureWithKw.FormKey} ({firstFurnitureWithKw.EditorID}, Keywords.Count={firstFurnitureWithKw.Keywords!.Count})");
Console.WriteLine($"ACTI-kw:    {firstActivatorWithKw.FormKey} ({firstActivatorWithKw.EditorID}, Keywords.Count={firstActivatorWithKw.Keywords!.Count})");
Console.WriteLine($"LCTN-kw:    {firstLocationWithKw.FormKey} ({firstLocationWithKw.EditorID}, Keywords.Count={firstLocationWithKw.Keywords!.Count})");
Console.WriteLine($"SPEL-kw:    {firstSpellWithKw.FormKey} ({firstSpellWithKw.EditorID}, Keywords.Count={(firstSpellWithKw.Keywords?.Count ?? 0)}, hasExistingKw={spellHasExistingKw})");
Console.WriteLine($"MGEF-kw:    {firstMagicEffectWithKw.FormKey} ({firstMagicEffectWithKw.EditorID}, Keywords.Count={firstMagicEffectWithKw.Keywords!.Count})");
Console.WriteLine($"RACE-spell: {firstRaceWithSpells.FormKey} ({firstRaceWithSpells.EditorID}, ActorEffect.Count={firstRaceWithSpells.ActorEffect!.Count})");
Console.WriteLine($"LVLN:       {firstLvln.FormKey} ({firstLvln.EditorID})");
Console.WriteLine($"LVSP:       {firstLvsp.FormKey} ({firstLvsp.EditorID})");
Console.WriteLine();

// Helper: run an add_keywords test and verify the keyword landed in the output ESP.
// Captures bridgeExe / SkyrimEsm / loadOrderListings / outDir from outer scope.
int KwAddTest(int testNum, string recordTypeLabel, FormKey targetFk, FormKey freshKwFk,
              Func<ISkyrimModGetter, IReadOnlyList<IFormLinkGetter<IKeywordGetter>>?> readKwFromOutput)
{
    var outPath = Path.Combine(outDir, $"test{testNum:D2}-add-kw-{recordTypeLabel.ToLower()}.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(targetFk),
                source_path = SkyrimEsm,
                add_keywords = new[] { FormatFormKey(freshKwFk) },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine($"── Test {testNum}: add_keywords on {recordTypeLabel} (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("keywords_added", out var added)
            || added.GetInt32() != 1)
        {
            Console.WriteLine("  FAIL: modifications.keywords_added should be 1");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var kwList = readKwFromOutput(outMod);
            if (kwList == null) { Console.WriteLine($"  FAIL: {recordTypeLabel} override Keywords missing on readback"); ok = false; }
            else if (!kwList.Any(k => k.FormKey == freshKwFk))
            {
                Console.WriteLine($"  FAIL: fresh keyword {freshKwFk} not present after add (got {kwList.Count} keywords)");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: Keywords contains {freshKwFk} ({kwList.Count} total)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    Console.WriteLine();
    return ok ? 0 : 1;
}

// Helper: run a remove_keywords test and verify the keyword is gone from the output ESP.
// expectedRemoved=1 (default) is the normal case: the picked record carries the kw and
// removal lands. expectedRemoved=0 covers the "no existing kw to remove" wire-up check —
// Tier D contract still passes (handler ran, mods key written, count=0); the readback is
// trivially satisfied since the kw was never present.
int KwRemoveTest(int testNum, string recordTypeLabel, FormKey targetFk, FormKey existingKwFk,
                 Func<ISkyrimModGetter, IReadOnlyList<IFormLinkGetter<IKeywordGetter>>?> readKwFromOutput,
                 int expectedRemoved = 1)
{
    var outPath = Path.Combine(outDir, $"test{testNum:D2}-remove-kw-{recordTypeLabel.ToLower()}.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(targetFk),
                source_path = SkyrimEsm,
                remove_keywords = new[] { FormatFormKey(existingKwFk) },
            }
        },
        load_order = new
        {
            game_release = "SkyrimSE",
            listings = loadOrderListings,
        }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine($"── Test {testNum}: remove_keywords on {recordTypeLabel} (expected: success + readback, removeCount={expectedRemoved}) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("keywords_removed", out var removed)
            || removed.GetInt32() != expectedRemoved)
        {
            Console.WriteLine($"  FAIL: modifications.keywords_removed should be {expectedRemoved}");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var kwList = readKwFromOutput(outMod);
            if (kwList == null) { Console.WriteLine($"  FAIL: {recordTypeLabel} override Keywords missing on readback"); ok = false; }
            else if (kwList.Any(k => k.FormKey == existingKwFk))
            {
                Console.WriteLine($"  FAIL: removed keyword {existingKwFk} still present after remove");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: Keywords no longer contains {existingKwFk} ({kwList.Count} remaining)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    Console.WriteLine();
    return ok ? 0 : 1;
}

// ── Tests 7-12: add_keywords on RACE/FURN/ACTI/LCTN/SPEL/MGEF ──
failures += KwAddTest(7,  "RACE", firstRaceWithKw.FormKey,        FreshKwFor(firstRaceWithKw.Keywords),
    om => om.Races.FirstOrDefault(r => r.FormKey == firstRaceWithKw.FormKey)?.Keywords);
failures += KwAddTest(8,  "FURN", firstFurnitureWithKw.FormKey,   FreshKwFor(firstFurnitureWithKw.Keywords),
    om => om.Furniture.FirstOrDefault(f => f.FormKey == firstFurnitureWithKw.FormKey)?.Keywords);
failures += KwAddTest(9,  "ACTI", firstActivatorWithKw.FormKey,   FreshKwFor(firstActivatorWithKw.Keywords),
    om => om.Activators.FirstOrDefault(a => a.FormKey == firstActivatorWithKw.FormKey)?.Keywords);
failures += KwAddTest(10, "LCTN", firstLocationWithKw.FormKey,    FreshKwFor(firstLocationWithKw.Keywords),
    om => om.Locations.FirstOrDefault(l => l.FormKey == firstLocationWithKw.FormKey)?.Keywords);
failures += KwAddTest(11, "SPEL", firstSpellWithKw.FormKey,       FreshKwFor(firstSpellWithKw.Keywords),
    om => om.Spells.FirstOrDefault(s => s.FormKey == firstSpellWithKw.FormKey)?.Keywords);
failures += KwAddTest(12, "MGEF", firstMagicEffectWithKw.FormKey, FreshKwFor(firstMagicEffectWithKw.Keywords),
    om => om.MagicEffects.FirstOrDefault(m => m.FormKey == firstMagicEffectWithKw.FormKey)?.Keywords);

// ── Tests 13-18: remove_keywords on RACE/FURN/ACTI/LCTN/SPEL/MGEF ──
failures += KwRemoveTest(13, "RACE", firstRaceWithKw.FormKey,        firstRaceWithKw.Keywords![0].FormKey,
    om => om.Races.FirstOrDefault(r => r.FormKey == firstRaceWithKw.FormKey)?.Keywords);
failures += KwRemoveTest(14, "FURN", firstFurnitureWithKw.FormKey,   firstFurnitureWithKw.Keywords![0].FormKey,
    om => om.Furniture.FirstOrDefault(f => f.FormKey == firstFurnitureWithKw.FormKey)?.Keywords);
failures += KwRemoveTest(15, "ACTI", firstActivatorWithKw.FormKey,   firstActivatorWithKw.Keywords![0].FormKey,
    om => om.Activators.FirstOrDefault(a => a.FormKey == firstActivatorWithKw.FormKey)?.Keywords);
failures += KwRemoveTest(16, "LCTN", firstLocationWithKw.FormKey,    firstLocationWithKw.Keywords![0].FormKey,
    om => om.Locations.FirstOrDefault(l => l.FormKey == firstLocationWithKw.FormKey)?.Keywords);
failures += KwRemoveTest(17, "SPEL", firstSpellWithKw.FormKey,
    spellHasExistingKw ? firstSpellWithKw.Keywords![0].FormKey : source.Keywords.First().FormKey,
    om => om.Spells.FirstOrDefault(s => s.FormKey == firstSpellWithKw.FormKey)?.Keywords,
    expectedRemoved: spellHasExistingKw ? 1 : 0);
failures += KwRemoveTest(18, "MGEF", firstMagicEffectWithKw.FormKey, firstMagicEffectWithKw.Keywords![0].FormKey,
    om => om.MagicEffects.FirstOrDefault(m => m.FormKey == firstMagicEffectWithKw.FormKey)?.Keywords);

// ── Test 19: add_spells on RACE ──
{
    var freshSpell = FreshSpellFor(firstRaceWithSpells.ActorEffect);
    var outPath = Path.Combine(outDir, "test19-add-spells-race.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstRaceWithSpells.FormKey),
                source_path = SkyrimEsm,
                add_spells = new[] { FormatFormKey(freshSpell) },
            }
        },
        load_order = new { game_release = "SkyrimSE", listings = loadOrderListings }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 19: add_spells on RACE (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("spells_added", out var added)
            || added.GetInt32() != 1)
        {
            Console.WriteLine("  FAIL: modifications.spells_added should be 1");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var overridden = outMod.Races.FirstOrDefault(r => r.FormKey == firstRaceWithSpells.FormKey);
            if (overridden?.ActorEffect == null) { Console.WriteLine("  FAIL: race override or ActorEffect missing"); ok = false; }
            else if (!overridden.ActorEffect.Any(s => s.FormKey == freshSpell))
            {
                Console.WriteLine($"  FAIL: fresh spell {freshSpell} not present after add (got {overridden.ActorEffect.Count} spells)");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: ActorEffect contains {freshSpell} ({overridden.ActorEffect.Count} total)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 20: remove_spells on RACE ──
{
    var existingSpell = firstRaceWithSpells.ActorEffect![0].FormKey;
    var outPath = Path.Combine(outDir, "test20-remove-spells-race.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstRaceWithSpells.FormKey),
                source_path = SkyrimEsm,
                remove_spells = new[] { FormatFormKey(existingSpell) },
            }
        },
        load_order = new { game_release = "SkyrimSE", listings = loadOrderListings }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 20: remove_spells on RACE (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("spells_removed", out var removed)
            || removed.GetInt32() != 1)
        {
            Console.WriteLine("  FAIL: modifications.spells_removed should be 1");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var overridden = outMod.Races.FirstOrDefault(r => r.FormKey == firstRaceWithSpells.FormKey);
            if (overridden?.ActorEffect == null) { Console.WriteLine("  FAIL: race override or ActorEffect missing"); ok = false; }
            else if (overridden.ActorEffect.Any(s => s.FormKey == existingSpell))
            {
                Console.WriteLine($"  FAIL: removed spell {existingSpell} still present after remove");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: ActorEffect no longer contains {existingSpell} ({overridden.ActorEffect.Count} remaining)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 21: add_items on LeveledNpc ──
{
    var outPath = Path.Combine(outDir, "test21-add-items-lvln.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstLvln.FormKey),
                source_path = SkyrimEsm,
                add_items = new[] { new { reference = FormatFormKey(freshNpcRef), level = (short)1, count = (short)1 } },
            }
        },
        load_order = new { game_release = "SkyrimSE", listings = loadOrderListings }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 21: add_items on LVLN (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("items_added", out var added)
            || added.GetInt32() != 1)
        {
            Console.WriteLine("  FAIL: modifications.items_added should be 1");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var overridden = outMod.LeveledNpcs.FirstOrDefault(l => l.FormKey == firstLvln.FormKey);
            if (overridden?.Entries == null) { Console.WriteLine("  FAIL: LVLN override or Entries missing"); ok = false; }
            else if (!overridden.Entries.Any(e => e.Data?.Reference.FormKey == freshNpcRef))
            {
                Console.WriteLine($"  FAIL: fresh NPC ref {freshNpcRef} not present in Entries (got {overridden.Entries.Count} entries)");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: Entries contains ref {freshNpcRef} ({overridden.Entries.Count} total)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

// ── Test 22: add_items on LeveledSpell ──
{
    var outPath = Path.Combine(outDir, "test22-add-items-lvsp.esp");
    if (File.Exists(outPath)) File.Delete(outPath);

    var req = new
    {
        command = "patch",
        output_path = outPath,
        esl_flag = false,
        author = "coverage-smoke",
        records = new[]
        {
            new
            {
                op = "override",
                formid = FormatFormKey(firstLvsp.FormKey),
                source_path = SkyrimEsm,
                add_items = new[] { new { reference = FormatFormKey(freshSpellRef), level = (short)1, count = (short)1 } },
            }
        },
        load_order = new { game_release = "SkyrimSE", listings = loadOrderListings }
    };

    var (stdout, _, exit) = RunBridge(bridgeExe, JsonSerializer.Serialize(req));
    Console.WriteLine("── Test 22: add_items on LVSP (expected: success + readback) ──");
    Console.WriteLine($"  exit code: {exit}");
    foreach (var line in stdout.Split('\n')) Console.WriteLine($"    {line.TrimEnd('\r')}");

    using var doc = JsonDocument.Parse(stdout);
    var root = doc.RootElement;
    bool ok = root.GetProperty("success").GetBoolean();
    if (!ok) Console.WriteLine("  FAIL: bridge response was not success");
    else
    {
        var d0 = root.GetProperty("details")[0];
        if (!d0.TryGetProperty("modifications", out var mods)
            || !mods.TryGetProperty("items_added", out var added)
            || added.GetInt32() != 1)
        {
            Console.WriteLine("  FAIL: modifications.items_added should be 1");
            ok = false;
        }
        if (!File.Exists(outPath)) { Console.WriteLine("  FAIL: output ESP missing"); ok = false; }
        else if (ok)
        {
            using var outMod = SkyrimMod.CreateFromBinaryOverlay(outPath, SkyrimRelease.SkyrimSE);
            var overridden = outMod.LeveledSpells.FirstOrDefault(l => l.FormKey == firstLvsp.FormKey);
            if (overridden?.Entries == null) { Console.WriteLine("  FAIL: LVSP override or Entries missing"); ok = false; }
            else if (!overridden.Entries.Any(e => e.Data?.Reference.FormKey == freshSpellRef))
            {
                Console.WriteLine($"  FAIL: fresh spell ref {freshSpellRef} not present in Entries (got {overridden.Entries.Count} entries)");
                ok = false;
            }
            else
            {
                Console.WriteLine($"  readback: Entries contains ref {freshSpellRef} ({overridden.Entries.Count} total)");
            }
        }
    }
    Console.WriteLine(ok ? "  PASS" : "  FAIL");
    if (!ok) failures++;
    Console.WriteLine();
}

Console.WriteLine($"=== smoke complete: {(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)")} ===");
return failures == 0 ? 0 : 1;


// Format a Mutagen FormKey in the bridge's expected "PluginName:HexID" shape.
// Mutagen's own FormKey.ToString() produces "HexID:PluginName" (reversed) — the
// bridge's FormIdHelper.Parse can't parse that, so we build the bridge form directly.
static string FormatFormKey(FormKey fk) => $"{fk.ModKey.FileName}:{fk.ID:X6}";

static (string stdout, string stderr, int exit) RunBridge(string exe, string stdinJson)
{
    var psi = new ProcessStartInfo(exe)
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    var p = Process.Start(psi)!;
    p.StandardInput.Write(stdinJson);
    p.StandardInput.Close();
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (stdout, stderr, p.ExitCode);
}
