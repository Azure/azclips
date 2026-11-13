using System.Text;
using System.Text.Json;

using Ace.Generator.Host;
using Ace.Generator.Model.Ir;

using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Contract tests for <see cref="HostRunner"/> — the out-of-process adapter that
/// backs the compiler's <c>csharp-generate</c> tool. Drives it in-memory (no
/// process spawn) to verify the NDJSON wire framing the CLI integration depends on.
/// </summary>
public sealed class HostRunnerTests
{
    private static TransformedIR Ir() => new()
    {
        SchemaVersion = "0.8.0",
        Target = Targets.For("Contoso.Clients"),
        Models =
        [
            new IRModel { Name = "Widget", Source = "Test", Id = new IRId { Module = "Test", Name = "Widget" } },
        ],
        Commands = [],
    };

    /// <summary>Serialize a request, run the host, and split stdout into frames.</summary>
    private static async Task<(int Code, List<JsonElement> Frames)> RunAsync(object request)
    {
        string json = JsonSerializer.Serialize(request);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var output = new MemoryStream();

        int code = await HostRunner.RunAsync(input, output);

        var frames = Encoding.UTF8.GetString(output.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();
        return (code, frames);
    }

    [Fact]
    public async Task Streams_a_file_frame_per_model_then_a_success_result()
    {
        (int code, List<JsonElement> frames) = await RunAsync(new { ir = Ir(), config = new { } });

        Assert.Equal(0, code);

        // A file frame was streamed for the Widget model — identified by content,
        // not a hardcoded path (naming is the generator's concern, pinned in its
        // own tests).
        Assert.Contains(frames, f =>
            f.GetProperty("kind").GetString() == "file" &&
            f.GetProperty("path").GetString()!.Length > 0 &&
            f.GetProperty("contents").GetString()!.Contains("class Widget"));

        // Terminal result frame, last, reporting success with no diagnostics.
        JsonElement result = frames[^1];
        Assert.Equal("result", result.GetProperty("kind").GetString());
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Empty(result.GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public async Task Takes_the_namespace_from_the_documents_target()
    {
        (int code, List<JsonElement> frames) = await RunAsync(new { ir = Ir(), config = new { } });

        Assert.Equal(0, code);
        JsonElement widget = frames.First(f =>
            f.GetProperty("kind").GetString() == "file" &&
            f.GetProperty("contents").GetString()!.Contains("class Widget"));
        Assert.Contains("namespace Contoso.Clients.Models", widget.GetProperty("contents").GetString());
    }

    [Fact]
    public async Task A_document_without_a_target_emits_an_error_result_and_no_files()
    {
        TransformedIR ir = Ir();
        ir.Target = null!;

        (int code, List<JsonElement> frames) = await RunAsync(new { ir, config = new { } });

        Assert.Equal(1, code);
        Assert.DoesNotContain(frames, f => f.GetProperty("kind").GetString() == "file");
        JsonElement result = frames[^1];
        Assert.Equal("result", result.GetProperty("kind").GetString());
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), d =>
            d.GetProperty("severity").GetString() == "error" &&
            d.GetProperty("code").GetString() == "ace-generator/missing-target");
    }

    [Fact]
    public async Task Invalid_server_reference_emits_an_error_result_without_the_processor()
    {
        TransformedIR ir = Ir();
        ir.Commands =
        [
            Commands.Atomic(
                "Get",
                new IRId { Module = "Test", Name = "missing" },
                new IRId { Module = "Test", Name = "get" }),
        ];

        (int code, List<JsonElement> frames) = await RunAsync(new { ir, config = new { } });

        Assert.Equal(1, code);
        JsonElement result = frames[^1];
        Assert.Equal("result", result.GetProperty("kind").GetString());
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), d =>
            d.GetProperty("severity").GetString() == "error" &&
            d.GetProperty("code").GetString() == "ace-generator/unknown-server-reference");
        Assert.DoesNotContain(frames, f =>
            f.GetProperty("kind").GetString() == "file" &&
            f.GetProperty("contents").GetString()!.Contains("class GetProcessor"));
    }

    [Fact]
    public async Task Emits_an_error_result_frame_on_invalid_request_json()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("{ not json"));
        using var output = new MemoryStream();

        int code = await HostRunner.RunAsync(input, output);

        Assert.Equal(1, code);
        JsonElement result = JsonDocument
            .Parse(Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1])
            .RootElement;
        Assert.Equal("result", result.GetProperty("kind").GetString());
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), d =>
            d.GetProperty("severity").GetString() == "error" &&
            d.GetProperty("code").GetString() == "ace-generator-host/unhandled");
    }
}
