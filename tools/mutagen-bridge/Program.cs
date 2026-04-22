// mutagen-bridge - Mutagen bridge for the mo2_mcp plugin
// Copyright (c) 2026 Aaronavich
// Licensed under the MIT License. See LICENSE for details.

using System.Text.Json;
using MutagenBridge;

// ── Entry Point ─────────────────────────────────────────────────────
// Reads a PatchRequest as JSON from stdin, processes it, writes
// a PatchResponse as JSON to stdout. Exit code 0 = success, 1 = error.

try
{
    var input = Console.In.ReadToEnd();

    if (string.IsNullOrWhiteSpace(input))
    {
        WriteError("No input received on stdin. Pipe a JSON request.");
        return 1;
    }

    var parseOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    // Peek at the command discriminator. Default "patch" for backward
    // compatibility with v1.2.0 payloads that lack the field.
    var envelope = JsonSerializer.Deserialize<RequestEnvelope>(input, parseOptions);
    var command = envelope?.Command ?? "patch";

    var outputOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    if (string.Equals(command, "read_record", StringComparison.OrdinalIgnoreCase))
    {
        var readRequest = JsonSerializer.Deserialize<ReadRequest>(input, parseOptions);
        if (readRequest == null)
        {
            WriteError("Failed to parse ReadRequest JSON.");
            return 1;
        }
        var reader = new RecordReader();
        var readResponse = reader.Read(readRequest);
        Console.Write(JsonSerializer.Serialize(readResponse, outputOptions));
        return readResponse.Success ? 0 : 1;
    }

    if (string.Equals(command, "read_records", StringComparison.OrdinalIgnoreCase))
    {
        var batchRequest = JsonSerializer.Deserialize<ReadBatchRequest>(input, parseOptions);
        if (batchRequest == null)
        {
            WriteError("Failed to parse ReadBatchRequest JSON.");
            return 1;
        }
        var reader = new RecordReader();
        var batchResponse = reader.ReadBatch(batchRequest);
        Console.Write(JsonSerializer.Serialize(batchResponse, outputOptions));
        return batchResponse.Success ? 0 : 1;
    }

    if (string.Equals(command, "fuz_info", StringComparison.OrdinalIgnoreCase))
    {
        var req = JsonSerializer.Deserialize<FuzInfoRequest>(input, parseOptions);
        if (req == null)
        {
            WriteError("Failed to parse FuzInfoRequest JSON.");
            return 1;
        }
        var resp = FuzCommands.Info(req);
        Console.Write(JsonSerializer.Serialize(resp, outputOptions));
        return resp.Success ? 0 : 1;
    }

    if (string.Equals(command, "fuz_extract", StringComparison.OrdinalIgnoreCase))
    {
        var req = JsonSerializer.Deserialize<FuzExtractRequest>(input, parseOptions);
        if (req == null)
        {
            WriteError("Failed to parse FuzExtractRequest JSON.");
            return 1;
        }
        var resp = FuzCommands.Extract(req);
        Console.Write(JsonSerializer.Serialize(resp, outputOptions));
        return resp.Success ? 0 : 1;
    }

    // Default: patch command
    var request = JsonSerializer.Deserialize<PatchRequest>(input, parseOptions);
    if (request == null)
    {
        WriteError("Failed to parse PatchRequest JSON.");
        return 1;
    }

    if (string.IsNullOrEmpty(request.OutputPath))
    {
        WriteError("output_path is required.");
        return 1;
    }

    if (request.Records.Count == 0)
    {
        WriteError("records list is empty.");
        return 1;
    }

    var engine = new PatchEngine();
    var response = engine.Process(request);

    Console.Write(JsonSerializer.Serialize(response, outputOptions));
    return response.Success ? 0 : 1;
}
catch (JsonException ex)
{
    WriteError($"JSON parse error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    WriteError($"Unhandled exception: {ex.Message}", ex.ToString());
    return 1;
}

static void WriteError(string message, string? detail = null)
{
    var response = new PatchResponse
    {
        Success = false,
        Error = message,
        ErrorDetail = detail,
    };
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
    Console.Write(JsonSerializer.Serialize(response, options));
}
