// spooky-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Text;

namespace SpookyBridge;

// ── FUZ Parser ──────────────────────────────────────────────────────
//
// Skyrim's .fuz format is a trivial wrapper that bundles a LIP sync track
// with an XWM audio stream. Layout:
//
//   bytes 0..3   ASCII "FUZE"
//   bytes 4..7   uint32 version (always 1)
//   bytes 8..11  uint32 lip_data_size
//   bytes 12..   lip_data bytes (lip_data_size of them)
//   then...      xwm audio bytes to EOF
//
// Spooky v1.11.1's audio parser rejects valid FUZes with "Not a valid FUZ
// file" even when the magic bytes check out — so we parse it ourselves.

public static class FuzCommands
{
    private const uint FuzMagic = 0x455A5546; // 'FUZE' little-endian
    private const uint ExpectedVersion = 1;

    public static FuzInfoResponse Info(FuzInfoRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FuzPath))
                return FuzInfoResponse.Fail("fuz_path is required.");

            if (!File.Exists(request.FuzPath))
                return FuzInfoResponse.Fail($"File not found: {request.FuzPath}");

            var fi = new FileInfo(request.FuzPath);
            using var fs = File.OpenRead(request.FuzPath);
            using var br = new BinaryReader(fs);

            if (fi.Length < 12)
                return FuzInfoResponse.Fail("File too small to be a FUZ (need at least 12 bytes).");

            var magicBytes = br.ReadBytes(4);
            var magicU32 = BitConverter.ToUInt32(magicBytes, 0);
            var magicAscii = Encoding.ASCII.GetString(magicBytes);
            if (magicU32 != FuzMagic)
            {
                return FuzInfoResponse.Fail(
                    $"Not a FUZ file. Magic is '{magicAscii}' (0x{magicU32:X8}), expected 'FUZE' (0x{FuzMagic:X8}).");
            }

            var version = br.ReadUInt32();
            var lipSize = br.ReadUInt32();
            var xwmSize = fi.Length - 12 - (long)lipSize;

            if (xwmSize < 0)
            {
                return FuzInfoResponse.Fail(
                    $"Invalid FUZ layout: lip_data_size ({lipSize}) exceeds remaining bytes ({fi.Length - 12}).");
            }

            return new FuzInfoResponse
            {
                Success = true,
                FuzPath = request.FuzPath.Replace("\\", "/"),
                Format = "FUZ",
                FileSize = fi.Length,
                Version = (int)version,
                LipSize = (long)lipSize,
                XwmSize = xwmSize,
                VersionSupported = version == ExpectedVersion,
            };
        }
        catch (Exception ex)
        {
            return FuzInfoResponse.Fail($"Unhandled error: {ex.Message}");
        }
    }

    public static FuzExtractResponse Extract(FuzExtractRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FuzPath))
                return FuzExtractResponse.Fail("fuz_path is required.");
            if (!File.Exists(request.FuzPath))
                return FuzExtractResponse.Fail($"File not found: {request.FuzPath}");
            if (string.IsNullOrEmpty(request.OutputDir))
                return FuzExtractResponse.Fail("output_dir is required.");

            using var fs = File.OpenRead(request.FuzPath);
            using var br = new BinaryReader(fs);
            var totalLen = fs.Length;

            if (totalLen < 12)
                return FuzExtractResponse.Fail("File too small to be a FUZ.");

            var magic = br.ReadUInt32();
            if (magic != FuzMagic)
                return FuzExtractResponse.Fail($"Not a FUZ file. Magic 0x{magic:X8}.");

            br.ReadUInt32(); // version, ignored (validated by Info if caller cares)
            var lipSize = br.ReadUInt32();
            var xwmSize = totalLen - 12 - (long)lipSize;
            if (xwmSize < 0)
                return FuzExtractResponse.Fail("Invalid FUZ layout: lip_data_size exceeds file bounds.");

            Directory.CreateDirectory(request.OutputDir);

            var basename = Path.GetFileNameWithoutExtension(request.FuzPath);
            var lipPath = Path.Combine(request.OutputDir, $"{basename}.lip");
            var xwmPath = Path.Combine(request.OutputDir, $"{basename}.xwm");

            if (lipSize > 0)
            {
                var lipData = br.ReadBytes((int)lipSize);
                File.WriteAllBytes(lipPath, lipData);
            }
            else
            {
                // Some FUZes have no LIP track; still create an empty .lip so
                // the caller gets a consistent pair of files on disk.
                File.WriteAllBytes(lipPath, Array.Empty<byte>());
            }

            // Stream the XWM in chunks so we don't balloon memory on long VO lines.
            using (var outXwm = File.Create(xwmPath))
            {
                fs.CopyTo(outXwm);
            }

            return new FuzExtractResponse
            {
                Success = true,
                FuzPath = request.FuzPath.Replace("\\", "/"),
                LipPath = lipPath.Replace("\\", "/"),
                XwmPath = xwmPath.Replace("\\", "/"),
                LipSize = (long)lipSize,
                XwmSize = xwmSize,
            };
        }
        catch (Exception ex)
        {
            return FuzExtractResponse.Fail($"Unhandled error: {ex.Message}");
        }
    }
}
