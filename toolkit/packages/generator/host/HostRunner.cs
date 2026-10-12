using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Host;

/// <summary>
/// Drives one C# generation run against the compiler's <c>csharp-generate</c>
/// protocol: read a JSON <c>GenerateToolRequest</c> from <paramref name="input"/>,
/// run <see cref="CSharpClientGenerator"/>, and stream the reply to
/// <paramref name="output"/> as NDJSON — a <c>file</c> frame per generated file
/// (flushed as produced), then a terminal <c>result</c> frame.
/// </summary>
public static class HostRunner
{
    /// <summary>
    /// Run one generation, framing stdin/stdout per the tool protocol. Returns 0
    /// on success, 1 when the run reports an error (or fails to start).
    /// </summary>
    public static async Task<int> RunAsync(Stream input, Stream output)
    {
        var writer = new StreamWriter(output, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = false };
        try
        {
            string requestJson;
            using (var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                requestJson = await reader.ReadToEndAsync();
            }

            HostRequest request =
                JsonSerializer.Deserialize<HostRequest>(requestJson, IrJson.Options)
                ?? throw new JsonException("GenerateToolRequest deserialized to null.");

            var options = new GeneratorOptions { Config = request.Options.Config };

            // Stream each generated file to stdout the moment it is produced.
            async ValueTask WriteFileAsync(string relativePath, string contents, CancellationToken ct)
            {
                string line = JsonSerializer.Serialize(new FileFrame { Path = relativePath, Contents = contents });
                await writer.WriteLineAsync(line.AsMemory(), ct);
                await writer.FlushAsync(ct);
            }

            // Emit a remove frame for each owned scope (the cleanup pass runs before
            // produce, so these precede the file frames on stdout).
            async ValueTask RemoveScopeAsync(string relativePath, CancellationToken ct)
            {
                string line = JsonSerializer.Serialize(new RemoveFrame { Path = relativePath });
                await writer.WriteLineAsync(line.AsMemory(), ct);
                await writer.FlushAsync(ct);
            }

            GenerationResult result =
                await new CSharpClientGenerator().GenerateAsync(request.Ir, options, WriteFileAsync, RemoveScopeAsync);

            await WriteResultAsync(writer, result.Success, MapDiagnostics(result.Diagnostics));
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            // Emit a terminal result frame so the caller always learns why a run
            // ended, even if request parsing or generation threw mid-stream.
            await WriteResultAsync(
                writer,
                success: false,
                [new DiagnosticDto { Severity = "error", Code = "ace-generator-host/unhandled", Message = ex.Message }]);
            return 1;
        }
    }

    // Map generator diagnostics onto the compiler's two-level severity model. The
    // TypeScript CompilerDiagnostic has only error|warning; Info has no slot and
    // never affects success, so it is dropped.
    private static List<DiagnosticDto> MapDiagnostics(IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        var mapped = new List<DiagnosticDto>();
        foreach (GeneratorDiagnostic d in diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Info)
            {
                continue;
            }

            mapped.Add(new DiagnosticDto
            {
                Severity = d.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                Code = d.Code,
                Message = d.Message,
            });
        }

        return mapped;
    }

    private static async Task WriteResultAsync(StreamWriter writer, bool success, IReadOnlyList<DiagnosticDto> diagnostics)
    {
        var frame = new ResultFrame { Success = success, Diagnostics = diagnostics };
        await writer.WriteLineAsync(JsonSerializer.Serialize(frame).AsMemory());
        await writer.FlushAsync();
    }
}

/// <summary>stdin payload: the compiler's <c>GenerateToolRequest</c>.</summary>
internal sealed record HostRequest
{
    [JsonPropertyName("ir")]
    public TransformedIR Ir { get; init; } = new();

    [JsonPropertyName("options")]
    public HostOptions Options { get; init; } = new();
}

/// <summary>Host-resolved generation options carried in the request.</summary>
internal sealed record HostOptions
{
    [JsonPropertyName("config")]
    public JsonElement? Config { get; init; }
}

/// <summary>An NDJSON <c>file</c> frame: one generated file.</summary>
internal sealed record FileFrame
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "file";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("contents")]
    public string Contents { get; init; } = "";
}

/// <summary>An NDJSON <c>remove</c> frame: wipe one output scope before writing.</summary>
internal sealed record RemoveFrame
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "remove";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";
}

/// <summary>A diagnostic in the terminal <c>result</c> frame.</summary>
internal sealed record DiagnosticDto
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

/// <summary>The terminal NDJSON <c>result</c> frame.</summary>
internal sealed record ResultFrame
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "result";

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<DiagnosticDto> Diagnostics { get; init; } = [];
}
