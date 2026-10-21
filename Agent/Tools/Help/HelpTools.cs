using System.ComponentModel;
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

    public HelpTools(string azExePath)
    {
        _azExePath = azExePath ?? throw new ArgumentNullException(nameof(azExePath));

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
        using var scope = ToolInvocationLog.Start("list_commands", "full catalog");
        return await ShellOutHelper.InvokeAzAsync(_azExePath, new[] { "agent", "tool", "help" }, ct).ConfigureAwait(false);
    }

    [Description("Return one command's full HelpCommand (parameters + examples + description).")]
    private async Task<string> GetCommandAsync(
        [Description("Command name. Pass cliName (e.g. 'vm create', 'group list') OR psName (e.g. 'New-AzVM', 'Get-AzResourceGroup'). Case-insensitive.")]
        string name,
        CancellationToken ct = default)
    {
        // See ListCommandsAsync — async so the scope covers the shell-out.
        using var scope = ToolInvocationLog.Start("get_command", $"name={name}");
        if (string.IsNullOrWhiteSpace(name))
        {
            scope.SetResult("error=missing_name");
            return "{\"error\":\"missing_name\",\"hint\":\"Pass cliName or psName.\"}";
        }
        return await ShellOutHelper.InvokeAzAsync(_azExePath, new[] { "agent", "tool", "help", "--name", name }, ct).ConfigureAwait(false);
    }
}
