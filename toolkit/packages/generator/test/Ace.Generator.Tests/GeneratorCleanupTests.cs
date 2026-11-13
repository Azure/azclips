using Ace.Generator.Model;
using Ace.Generator.Model.Ir;

using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// The cleanup pass: each stage names the output scopes it owns, and they are
/// wiped via the remove handler <em>before</em> any file is written, so every
/// regeneration starts clean. Verifies the delegate is driven with the right
/// scopes in the right order — not which files ultimately land.
/// </summary>
public sealed class GeneratorCleanupTests
{
    private static TransformedIR Ir() => new()
    {

        SchemaVersion = "0.8.0",
        Target = Targets.For("Contoso"),
        Models = [new IRModel { Name = "Widget", Source = "T", Id = new IRId { Module = "T", Name = "Widget" } }],
        Servers =
        [
            new IRServerDefinition
            {
                Id = new IRId { Module = "T", Name = "server" },
                Endpoint = new IRServerLiteral { Url = "https://example.com" },
            },
        ],
        Operations = [new IROperation { Name = "get", Server = new IRId { Module = "T", Name = "server" } }],
        Commands = [],
    };

    [Fact]
    public async Task Cleanup_wipes_owned_scopes_before_any_write()
    {
        var events = new List<string>();

        ValueTask Write(string path, string contents, CancellationToken ct)
        {
            events.Add($"write:{path}");
            return ValueTask.CompletedTask;
        }

        ValueTask Remove(string scope, CancellationToken ct)
        {
            events.Add($"remove:{scope}");
            return ValueTask.CompletedTask;
        }

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            Ir(), config: null, Write, Remove);

        Assert.True(result.Success);

        // Model + processor generators own their directories; base/stub add nothing.
        List<string> removes = events
            .Where(e => e.StartsWith("remove:", StringComparison.Ordinal))
            .Select(e => e["remove:".Length..])
            .ToList();
        Assert.Contains("Models", removes);
        Assert.Contains("Processors", removes);

        // The whole cleanup pass precedes the whole produce pass.
        int lastRemove = events.FindLastIndex(e => e.StartsWith("remove:", StringComparison.Ordinal));
        int firstWrite = events.FindIndex(e => e.StartsWith("write:", StringComparison.Ordinal));
        Assert.True(firstWrite >= 0 && lastRemove < firstWrite, "every remove must precede every write");
    }

    [Fact]
    public async Task No_cleanup_when_no_remove_handler_is_given()
    {
        // Backward-compatible: with no remove handler, generation still succeeds
        // and simply performs no cleanup.
        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            Ir(),
            config: null,
            (_, _, _) => ValueTask.CompletedTask);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task A_document_without_a_target_wipes_nothing()
    {
        // Rejected before the cleanup pass: there is nowhere to generate into, so the
        // previous output must survive.
        var removes = new List<string>();
        TransformedIR ir = Ir();
        ir.Target = null!;

        GenerationResult result = await new CSharpClientGenerator().GenerateAsync(
            ir,
            config: null,
            (_, _, _) => ValueTask.CompletedTask,
            (scope, _) =>
            {
                removes.Add(scope);
                return ValueTask.CompletedTask;
            });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "ace-generator/missing-target");
        Assert.Empty(removes);
    }
}
