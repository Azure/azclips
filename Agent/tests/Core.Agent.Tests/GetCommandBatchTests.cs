using System.Text.Json;
using Core.Agent.Plan;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

public sealed class GetCommandBatchTests
{
    [Fact]
    public void Schema_requires_an_array_of_command_names()
    {
        var schema = new HelpTools("unused").GetCommand.JsonSchema;
        var properties = schema.GetProperty("properties");
        var names = properties.GetProperty("names");

        Assert.False(properties.TryGetProperty("name", out _));
        Assert.Equal("array", names.GetProperty("type").GetString());
        Assert.Equal("string", names.GetProperty("items").GetProperty("type").GetString());
        Assert.Contains("names", schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Batch_preserves_order_duplicates_and_cli_arguments()
    {
        var calls = new List<string[]>();
        var tools = new HelpTools("fixture-az", (exe, args, _) =>
        {
            Assert.Equal("fixture-az", exe);
            calls.Add(args.ToArray());
            return Task.FromResult(JsonSerializer.Serialize(new { cliName = args[^1] }));
        });

        var result = await InvokeAsync(tools.GetCommand,
            new { names = new[] { "vm create", "group list", "vm create" } });

        Assert.Equal(new[] { "vm create", "group list", "vm create" },
            result.GetProperty("results").EnumerateArray().Select(item => item.GetProperty("cliName").GetString()));
        Assert.Equal(3, calls.Count);
        Assert.Equal(new[] { "agent", "tool", "help", "--name", "vm create" }, calls[0]);
        Assert.Equal(new[] { "agent", "tool", "help", "--name", "group list" }, calls[1]);
        Assert.Equal(calls[0], calls[2]);
    }

    [Fact]
    public async Task Single_name_is_wrapped_but_host_lookup_remains_unwrapped()
    {
        const string payload = """{ "cliName": "vm create", "parameters": [] }""";
        var tools = new HelpTools("fixture-az", (_, _, _) => Task.FromResult(payload));

        var batch = await InvokeAsync(tools.GetCommand, new { names = new[] { "vm create" } });
        var direct = await tools.LookupCommandAsync("vm create");
        var context = await FailedCommandHelp.BuildContextAsync(tools, "vm create --name example");

        Assert.Equal(1, batch.GetProperty("results").GetArrayLength());
        Assert.Equal(payload, batch.GetProperty("results")[0].GetRawText());
        Assert.Equal(payload, direct);
        Assert.NotNull(context);
        Assert.Contains(payload, context);
        Assert.DoesNotContain("\"results\"", context);
    }

    [Fact]
    public async Task Batch_preserves_per_item_errors_and_skips_blank_lookups()
    {
        var calls = new List<string>();
        var tools = new HelpTools("fixture-az", (_, args, _) =>
        {
            calls.Add(args[^1]);
            return Task.FromResult(args[^1] == "missing"
                ? """{"error":"not_found","suggestions":["vm create"]}"""
                : """{"cliName":"vm create","parameters":[]}""");
        });

        var result = await InvokeAsync(tools.GetCommand,
            new { names = new[] { "vm create", "missing", "  " } });
        var entries = result.GetProperty("results");

        Assert.Equal(3, entries.GetArrayLength());
        Assert.Equal("vm create", entries[0].GetProperty("cliName").GetString());
        Assert.Equal("not_found", entries[1].GetProperty("error").GetString());
        Assert.Equal("vm create", entries[1].GetProperty("suggestions")[0].GetString());
        Assert.Equal("missing_name", entries[2].GetProperty("error").GetString());
        Assert.Equal(new[] { "vm create", "missing" }, calls);
        Assert.Null(await FailedCommandHelp.BuildContextAsync(tools, "missing --name example"));
    }

    [Fact]
    public async Task Empty_names_returns_an_error_without_invoking_the_backend()
    {
        var tools = new HelpTools("fixture-az", (_, _, _) =>
            throw new InvalidOperationException("An empty batch must not invoke the backend."));

        var result = await InvokeAsync(tools.GetCommand, new { names = Array.Empty<string>() });

        Assert.Equal("missing_name", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Null_name_is_a_per_item_error_without_invoking_the_backend()
    {
        var tools = new HelpTools("fixture-az", (_, _, _) =>
            throw new InvalidOperationException("A null name must not invoke the backend."));

        var result = await InvokeAsync(tools.GetCommand, new { names = new string?[] { null } });

        Assert.Equal("missing_name", result.GetProperty("results")[0].GetProperty("error").GetString());
    }

    [Fact]
    public async Task Cancellation_stops_remaining_lookups_and_returns_an_error()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var tools = new HelpTools("fixture-az", (_, _, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            calls++;
            cancellation.Cancel();
            return Task.FromResult("""{"error":"shell_out_cancelled"}""");
        });

        var result = await InvokeAsync(tools.GetCommand,
            new { names = new[] { "vm create", "group list" } }, cancellation.Token);

        Assert.Equal(1, calls);
        Assert.Equal("shell_out_cancelled", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Precancelled_batch_does_not_invoke_the_backend()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var tools = new HelpTools("fixture-az", (_, _, _) =>
            throw new InvalidOperationException("A cancelled batch must not invoke the backend."));

        var result = await InvokeAsync(tools.GetCommand,
            new { names = new[] { "vm create" } }, cancellation.Token);

        Assert.Equal("shell_out_cancelled", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Default_constructor_keeps_the_real_shell_out_backend()
    {
        var executable = Path.Combine(Path.GetTempPath(), $"missing-az-{Guid.NewGuid():N}.exe");
        var result = await InvokeAsync(new HelpTools(executable).GetCommand,
            new { names = new[] { "vm create" } });

        var error = result.GetProperty("results")[0];
        Assert.Equal("shell_out_spawn_failed", error.GetProperty("error").GetString());
        Assert.Equal(executable, error.GetProperty("azExePath").GetString());
    }

    [Fact]
    public async Task List_commands_keeps_the_existing_catalog_contract()
    {
        const string catalog = """{"groups":[],"commands":[{"cliName":"vm create"}]}""";
        var tools = new HelpTools("fixture-az", (_, args, _) =>
        {
            Assert.Equal(new[] { "agent", "tool", "help" }, args);
            return Task.FromResult(catalog);
        });

        var result = await InvokeAsync(tools.ListCommands, new { });

        Assert.Equal(catalog, result.GetRawText());
        Assert.False(result.TryGetProperty("results", out _));
    }

    [Fact]
    public async Task Host_lookup_preserves_its_tool_timing_entry()
    {
        var name = $"timing-{Guid.NewGuid():N}";
        var tools = new HelpTools("fixture-az", (_, _, _) => Task.FromResult("""{"parameters":[]}"""));

        await tools.LookupCommandAsync(name);

        Assert.Single(ToolInvocationLog.Snapshot(),
            entry => entry.Name == "get_command" && entry.Args == $"name={name}");
    }

    private static async Task<JsonElement> InvokeAsync(
        AIFunction function, object arguments, CancellationToken cancellationToken = default)
    {
        var args = new AIFunctionArguments(
            JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!);
        var result = await function.InvokeAsync(args, cancellationToken);
        var json = JsonSerializer.SerializeToElement(result).GetString();
        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
