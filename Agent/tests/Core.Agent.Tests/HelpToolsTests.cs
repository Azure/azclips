using System.Text.Json;
using Core.Agent.Tools.Help;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="HelpTools"/>. The agent's behavior depends on
/// the JSON shape these tools return (list_commands → catalog with cliName/psName;
/// get_command → full HelpCommand or error=not_found). We assert on that JSON
/// contract — NOT on internal field formatting (anti-pattern B).
///
/// Also acts as a regression guard for cirrus-anti-patterns §A: HelpIndex.json
/// and HelpManifest.json must stay in sync (same command count). If a future
/// edit adds a command to one file but forgets the other, this fails loudly.
/// </summary>
public class HelpToolsTests
{
    private static async Task<JsonElement> InvokeAsync(AIFunction fn, object? args = null)
    {
        var argDict = args is null
            ? new AIFunctionArguments()
            : new AIFunctionArguments(
                JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(args))!);
        var result = await fn.InvokeAsync(argDict);
        var json = result switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString()!,
            JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(result),
        };
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task ListCommands_ReturnsFullCatalog_IncludingVmCreate()
    {
        var tools = new HelpTools();

        var root = await InvokeAsync(tools.ListCommands);

        var commands = root.GetProperty("commands");
        Assert.True(commands.GetArrayLength() >= 18,
            $"expected ≥18 commands, got {commands.GetArrayLength()}");

        var vmCreate = commands.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("cliName").GetString() == "vm create");
        Assert.NotEqual(JsonValueKind.Undefined, vmCreate.ValueKind);
        Assert.Equal("New-VM", vmCreate.GetProperty("psName").GetString());
    }

    [Fact]
    public async Task GetCommand_ByCliName_ReturnsEntry()
    {
        var tools = new HelpTools();

        var root = await InvokeAsync(tools.GetCommand, new { name = "vm create" });

        Assert.False(root.TryGetProperty("error", out _));
        Assert.Equal("vm create", root.GetProperty("cliName").GetString());
        Assert.Equal("New-VM", root.GetProperty("psName").GetString());
    }

    [Fact]
    public async Task GetCommand_ByPsName_ReturnsSameEntryAsCliLookup()
    {
        var tools = new HelpTools();

        var byCli = await InvokeAsync(tools.GetCommand, new { name = "vm create" });
        var byPs  = await InvokeAsync(tools.GetCommand, new { name = "New-VM" });

        Assert.Equal(byCli.GetProperty("name").GetString(), byPs.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCommand_IsCaseInsensitive()
    {
        var tools = new HelpTools();

        var lower = await InvokeAsync(tools.GetCommand, new { name = "vm create" });
        var mixed = await InvokeAsync(tools.GetCommand, new { name = "VM Create" });
        var psUpper = await InvokeAsync(tools.GetCommand, new { name = "NEW-VM" });

        Assert.Equal(lower.GetProperty("name").GetString(), mixed.GetProperty("name").GetString());
        Assert.Equal(lower.GetProperty("name").GetString(), psUpper.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCommand_UnknownName_ReturnsNotFoundWithSuggestions()
    {
        var tools = new HelpTools();

        var root = await InvokeAsync(tools.GetCommand, new { name = "vm" });

        Assert.Equal("not_found", root.GetProperty("error").GetString());
        var suggestions = root.GetProperty("suggestions");
        Assert.True(suggestions.GetArrayLength() > 0,
            "substring match on 'vm' must surface vm-related verbs");
        var names = suggestions.EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("vm create", names);
    }

    [Fact]
    public async Task GetCommand_MissingName_ReturnsMissingNameError()
    {
        var tools = new HelpTools();

        var root = await InvokeAsync(tools.GetCommand, new { name = "" });

        Assert.Equal("missing_name", root.GetProperty("error").GetString());
    }

    [Fact]
    public async Task IndexAndManifest_CommandCountsMatch()
    {
        var tools = new HelpTools();
        var index = await InvokeAsync(tools.ListCommands);
        var indexCount = index.GetProperty("commands").GetArrayLength();

        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Tools", "Help", "HelpManifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifestCount = manifest.RootElement.GetProperty("commands").GetArrayLength();

        Assert.Equal(indexCount, manifestCount);
    }

    [Fact]
    public async Task EveryIndexCommand_IsResolvableViaGetCommand()
    {
        var tools = new HelpTools();
        var index = await InvokeAsync(tools.ListCommands);

        foreach (var cmd in index.GetProperty("commands").EnumerateArray())
        {
            var cliName = cmd.GetProperty("cliName").GetString();
            var psName  = cmd.GetProperty("psName").GetString();
            var lookup  = !string.IsNullOrEmpty(cliName) ? cliName : psName;
            Assert.False(string.IsNullOrEmpty(lookup),
                $"command {cmd.GetProperty("name").GetString()} has neither cliName nor psName");

            var hit = await InvokeAsync(tools.GetCommand, new { name = lookup });
            Assert.False(hit.TryGetProperty("error", out _),
                $"index lists '{lookup}' but get_command returned not_found");
        }
    }

    [Fact]
    public void BuildFailedCommandContext_InjectsHelp_OnRealCommand()
    {
        var ctx = ChatAgent.BuildFailedCommandContext(new HelpTools(), "vm create -g rg -n x");
        Assert.NotNull(ctx);
        Assert.Contains("vm create", ctx!);
    }

    [Fact]
    public void BuildFailedCommandContext_ReturnsNull_OnNotFound()
    {
        // A multi-word command whose trailing positional arg gets swept into the
        // extracted verb resolves to not_found — we must NOT inject "not_found"
        // (it would mislead the agent into thinking its real command doesn't exist).
        var ctx = ChatAgent.BuildFailedCommandContext(new HelpTools(), "vm create totally-not-a-real-verb");
        Assert.Null(ctx);
    }

    [Fact]
    public void BuildFailedCommandContext_ReturnsNull_OnNaturalLanguage()
    {
        Assert.Null(ChatAgent.BuildFailedCommandContext(new HelpTools(), "帮我创建一个虚拟机"));
    }
}
