using System.ComponentModel;
using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Core.Cli.Help;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Two AI agent tools that surface the Azure CLI help catalog to the LLM:
///   • list_commands — flat catalog of every Azure CLI command (cliName + psName + summary)
///   • get_command   — full HelpCommand for one command (parameters + examples + description)
///
/// Both read the same versioned catalog as <c>az agent tool help</c>. The CLI
/// generates missing/stale artifacts; warm reads do not start a subprocess.
/// </summary>
public sealed class HelpTools
{
    private readonly HelpCatalogProvider _catalog;
    private readonly Func<string, IDisposable?>? _activityCreator;

    public HelpTools(string azExePath, IConfigurationService configuration, Func<string, IDisposable?>? activityCreator = null)
        : this(new HelpCatalogProvider(azExePath, configuration), activityCreator)
    {
    }

    public HelpTools(HelpCatalogProvider catalog, Func<string, IDisposable?>? activityCreator = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _activityCreator = activityCreator;

        ListCommands = AIFunctionFactory.Create(ListCommandsAsync, new AIFunctionFactoryOptions
        {
            Name = "list_commands",
            Description = "Return the full catalog of Azure CLI commands across all extensions. " +
                "Each entry has cliName (e.g. 'vm create', empty string when no CLI verb), " +
                "psName (e.g. 'New-AzVM', empty string when no PS cmdlet), verb, summary. " +
                "Use FIRST when you don't already know whether a command exists or what its exact name is. " +
                "After picking a candidate, call get_command for its parameters and examples. " +
                "Reads the local help catalog, regenerating it when installed extensions change. No network call.",
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
    // through the same provider so they can't drift.
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
        // catalog read — returning the Task without awaiting would dispose
        // the scope immediately, recording a near-zero duration.
        using var scope = ToolInvocationLog.Start("list_commands", "full catalog", _activityCreator);
        scope.RecordRequest("full catalog");
        var result = await _catalog.ListAsync(ct).ConfigureAwait(false);
        scope.RecordResponse(result.Kind == HelpLookupKind.Error ? "error" : "ok");
        return result.Json;
    }

    [Description("Return one command's full HelpCommand (parameters + examples + description).")]
    private async Task<string> GetCommandAsync(
        [Description("Command name. Pass cliName (e.g. 'vm create', 'group list') OR psName (e.g. 'New-AzVM', 'Get-AzResourceGroup'). Case-insensitive.")]
        string name,
        CancellationToken ct = default)
    {
        // See ListCommandsAsync — async so the scope covers the catalog read.
        using var scope = ToolInvocationLog.Start("get_command", $"name={name}", _activityCreator);
        // Records the catalog name only on a hit; never the requested text.
        scope.RecordRequest("(not found)");
        if (string.IsNullOrWhiteSpace(name))
        {
            scope.SetResult("error=missing_name");
            scope.RecordResponse("error");
            return "{\"error\":\"missing_name\",\"hint\":\"Pass cliName or psName.\"}";
        }
        var result = await _catalog.GetAsync(name, ct).ConfigureAwait(false);
        var catalogName = result.Command is null ? null : CatalogName(result.Command, name);
        if (catalogName is not null)
            scope.RecordRequest(catalogName);
        scope.RecordResponse(catalogName is not null ? "found" : result.Kind == HelpLookupKind.Error ? "error" : "not_found");
        return result.Json;
    }

    // The catalog's own name in the form that was requested; never the requested text.
    private static string? CatalogName(HelpCommand command, string requested)
    {
        var matched = string.Equals(command.PsName, requested.Trim(), StringComparison.OrdinalIgnoreCase)
            ? command.PsName
            : command.CliName;
        matched = string.IsNullOrEmpty(matched)
            ? (string.IsNullOrEmpty(command.CliName) ? command.PsName : command.CliName)
            : matched;
        return string.IsNullOrEmpty(matched) ? null : matched;
    }
}
