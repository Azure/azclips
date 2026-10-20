using System.ComponentModel;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Shell;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Two AI agent tools that surface the Azure CLI help catalog to the LLM:
///   • list_commands — flat catalog of every Azure CLI command (cliName + psName + summary)
///   • get_command   — full HelpCommand for one command (parameters + examples + description)
///
/// Both shell out to <c>az agent tool help [--name &lt;x&gt;]</c>; the CLI processor
/// (<c>HelpProcessor</c>) is the single source of truth. Adding an extension via
/// <c>az extension add</c> shows up on the next call — no cached catalog to invalidate.
/// The tool contract stays "always return a JSON string", so this class simply
/// forwards stdout to the model.
/// </summary>
public sealed class HelpTools
{
    private readonly string _azExePath;
    private readonly Func<string, IDisposable?>? _activityCreator;

    public HelpTools(string azExePath, Func<string, IDisposable?>? activityCreator = null)
    {
        _azExePath = azExePath ?? throw new ArgumentNullException(nameof(azExePath));
        _activityCreator = activityCreator;

        ListCommands = AIFunctionFactory.Create(ListCommandsAsync, new AIFunctionFactoryOptions
        {
            Name = "list_commands",
            Description = "Return the full catalog of Azure CLI commands across all extensions. " +
                "Each entry has cliName (e.g. 'vm create', empty string when no CLI verb), " +
                "psName (e.g. 'New-AzVM', empty string when no PS cmdlet), verb, summary. " +
                "Use FIRST when you don't already know whether a command exists or what its exact name is. " +
                "After picking a candidate, call get_command for its parameters and examples. " +
                "Shells out to `az agent tool help` which enumerates every installed extension's command " +
                "manifests at runtime — expect ~100 ms and a few dozen KB depending on how many extensions " +
                "you have installed. No network call.",
        });

        GetCommand = AIFunctionFactory.Create(GetCommandAsync, new AIFunctionFactoryOptions
        {
            Name = "get_command",
            Description = "Return the full HelpCommand for one Azure CLI command: parameters (name/psName, type, required, description), " +
                "examples (cli[] and powerShell[]), description, summary. " +
                "Pass 'name' as the cliName (e.g. 'vm create') OR the psName (e.g. 'New-AzVM') — " +
                "lookup is case-insensitive and searches both surfaces. " +
                "Returns error=not_found (with a suggestions list of near-matching commands) when the command doesn't exist. " +
                "Call AFTER list_commands has confirmed the command exists.",
        });

        All = new[] { ListCommands, GetCommand };
    }

    public AIFunction ListCommands { get; }
    public AIFunction GetCommand { get; }
    public IReadOnlyList<AIFunction> All { get; }

    // Host-side entry to the same lookup the LLM's get_command uses. Lets the host
    // pre-fetch a failed command's help and inject it into context, so the agent
    // doesn't spend a turn looking up the command it was triggered by. Returns the
    // exact same JSON (help or not_found) the tool would return — both funnel
    // through the same shell-out call so they can't drift.
    public Task<string> LookupCommandAsync(string name, CancellationToken ct = default) =>
        GetCommandAsync(name, ct);

    // Host-side entry to the same catalog the LLM's list_commands returns. The plan
    // hook reads it to recognize azclips commands (PlanCommandNormalizer), so a new
    // command group or extension counts without a code change.
    public Task<string> ReadCatalogAsync(CancellationToken ct = default) =>
        ListCommandsAsync(ct);

    [Description("Return the full HelpIndex catalog.")]
    private async Task<string> ListCommandsAsync(CancellationToken ct = default)
    {
        // Async + await so ToolInvocationLog's scope stays alive for the entire
        // subprocess call — returning the Task without awaiting would dispose
        // the scope immediately, recording a near-zero duration.
        using var scope = ToolInvocationLog.Start("list_commands", "full catalog", _activityCreator);
        scope.RecordRequest("full catalog");
        var result = await ShellOutHelper.InvokeAzAsync(_azExePath, new[] { "agent", "tool", "help" }, ct).ConfigureAwait(false);
        scope.RecordResponse(IsError(result) ? "error" : "ok");
        return result;
    }

    [Description("Return one command's full HelpCommand (parameters + examples + description).")]
    private async Task<string> GetCommandAsync(
        [Description("Command name. Pass cliName (e.g. 'vm create', 'group list') OR psName (e.g. 'New-AzVM', 'Get-AzResourceGroup'). Case-insensitive.")]
        string name,
        CancellationToken ct = default)
    {
        // See ListCommandsAsync — async so the scope covers the shell-out.
        using var scope = ToolInvocationLog.Start("get_command", $"name={name}", _activityCreator);
        if (string.IsNullOrWhiteSpace(name))
        {
            scope.SetResult("error=missing_name");
            scope.RecordResponse("error");
            return "{\"error\":\"missing_name\",\"hint\":\"Pass cliName or psName.\"}";
        }
        var result = await ShellOutHelper.InvokeAzAsync(_azExePath, new[] { "agent", "tool", "help", "--name", name }, ct).ConfigureAwait(false);
        RecordLookup(scope, name, result);
        return result;
    }

    // Records the catalog's own name for a hit, never the model's input text.
    internal static void RecordLookup(ToolInvocationLog.Scope scope, string requested, string helpJson)
    {
        var (found, catalogName) = ReadCatalogName(requested, helpJson);
        scope.RecordRequest(catalogName ?? "(not found)");
        scope.RecordResponse(found ? "found" : IsError(helpJson) && !IsNotFound(helpJson) ? "error" : "not_found");
    }

    private static bool IsError(string json) => TryReadError(json) is not null;

    private static bool IsNotFound(string json) => TryReadError(json) == "not_found";

    private static string? TryReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? "error"
                : null;
        }
        catch (JsonException)
        {
            return "unreadable";
        }
    }

    private static (bool Found, string? Name) ReadCatalogName(string requested, string helpJson)
    {
        try
        {
            using var document = JsonDocument.Parse(helpJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
                return (false, null);
            var cliName = root.TryGetProperty("cliName", out var cli) ? cli.GetString() : null;
            var psName = root.TryGetProperty("psName", out var ps) ? ps.GetString() : null;
            var matched = string.Equals(psName, requested.Trim(), StringComparison.OrdinalIgnoreCase) ? psName : cliName;
            matched = string.IsNullOrEmpty(matched) ? (string.IsNullOrEmpty(cliName) ? psName : cliName) : matched;
            return string.IsNullOrEmpty(matched) ? (false, null) : (true, matched);
        }
        catch (JsonException)
        {
            return (false, null);
        }
    }
}
