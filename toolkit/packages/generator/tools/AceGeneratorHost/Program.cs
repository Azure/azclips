using System.Text.Json;
using System.Text.Json.Serialization;
using Ace.Generator;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Host;

/// <summary>
/// Process host for <see cref="CSharpClientGenerator"/>: reads a JSON request from stdin, runs the
/// generator, writes a JSON response to stdout.
/// </summary>
/// <remarks>
/// The backbone streams files as they are produced, but a process boundary cannot; the response
/// buffers them and the caller streams to its own sink. Diagnostics are returned in the response
/// rather than printed, so the caller's diagnostic channel stays the only one. stdout carries the
/// response and nothing else — anything unexpected goes to stderr.
/// </remarks>
internal static class Program
{
    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static async Task<int> Main()
    {
        string requestJson = await Console.In.ReadToEndAsync();

        Request? request;
        try
        {
            request = JsonSerializer.Deserialize<Request>(requestJson, IrJson.Options);
        }
        catch (JsonException error)
        {
            return await FailAsync($"request is not valid JSON: {error.Message}");
        }

        if (request?.Ir is null)
        {
            return await FailAsync("request is missing the 'ir' document.");
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        GenerationResult result;
        try
        {
            result = await new CSharpClientGenerator().GenerateAsync(
                request.Ir,
                new GeneratorOptions { RootNamespace = request.RootNamespace ?? "Generated" },
                (path, contents, _) =>
                {
                    files[path] = contents;
                    return ValueTask.CompletedTask;
                });
        }
        catch (Exception error)
        {
            return await FailAsync($"generation threw: {error.Message}");
        }

        await WriteAsync(
            new Response(
                files,
                [.. result.Diagnostics.Select(d => new Diagnostic(Severity(d.Severity), d.Code, d.Message))],
                result.Success));

        return result.Success ? 0 : 1;
    }

    private static async Task<int> FailAsync(string message)
    {
        await WriteAsync(
            new Response(
                new Dictionary<string, string>(StringComparer.Ordinal),
                [new Diagnostic("error", "ace-generator-host/bad-request", message)],
                Success: false));
        return 1;
    }

    private static async Task WriteAsync(Response response)
    {
        await Console.Out.WriteAsync(JsonSerializer.Serialize(response, ResponseOptions));
        await Console.Out.FlushAsync();
    }

    private static string Severity(DiagnosticSeverity severity) =>
        severity == DiagnosticSeverity.Error ? "error" : "warning";

    private sealed record Request
    {
        [JsonPropertyName("ir")]
        public IRDocument? Ir { get; init; }

        [JsonPropertyName("rootNamespace")]
        public string? RootNamespace { get; init; }
    }

    private sealed record Response(
        IReadOnlyDictionary<string, string> Files,
        IReadOnlyList<Diagnostic> Diagnostics,
        bool Success);

    private sealed record Diagnostic(string Severity, string Code, string Message);
}
