// coverage-smoke — Tier D / Phase 1 + Tier C / Phase 2 inline smoke harness for v2.7.1.
//
// Tier D (Phase 1) — silent-failure detection:
//   1. Picking a real Container and Armor record from Skyrim.esm.
//   2. Posting a deliberately-unsupported (operator, record-type) request
//      and asserting the bridge returns the structured `unmatched_operators`
//      error with the override rolled back.
//   3. Posting a supported request against the same record and asserting
//      success (no Tier D regression on legitimate combos).
//   4. Posting `remove_conditions` on Armor (which has no Conditions
//      property) to confirm the v2.7.1 alignment of ApplyRemoveConditions
//      with ApplyAddConditions — both now throw rather than silently
//      returning 0.
//
// Tier C (Phase 2) — bracket-indexer dict mutation:
//   5. Posting `set_fields: { "Starting[Health]": 250 }` on a vanilla race
//      and reading back the output ESP via Mutagen to confirm Health == 250
//      while Magicka/Stamina are preserved (bracket-indexer write semantics).
//   6. Posting `set_fields: { "Regen": { "Health": X, "Magicka": Y } }` on
//      the same race and reading back to confirm both keys updated while the
//      un-named third key is preserved (whole-dict merge semantics, not replace).
//   7. Posting `set_fields: { "Starting[Bogus]": 100 }` and asserting the
//      bridge surfaces the Enum.Parse failure as a per-record error with the
//      override rolled back. (Goes through ProcessOverride's general catch arm,
//      not Tier D's UnsupportedOperatorException.)
//
// Run from the repo root:
//   dotnet run -c Release --project tools/coverage-smoke
//
// Sibling of tools/race-probe/. Stays in the repo as a regression check
// for Tier D semantics (mods-key present iff handler-matched-and-ran)
// AND Tier C semantics (bracket-indexer dict mutation, merge-not-replace).

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
