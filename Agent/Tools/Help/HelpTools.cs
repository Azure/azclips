using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Shell;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Two AI agent tools that surface the Azure CLI help catalog to the LLM:
///   • list_commands — flat catalog of every Azure CLI command (cliName + psName + summary)
///   • get_command   — ordered HelpCommand results for one or more command names
///
/// Both shell out to <c>az agent tool help [--name &lt;x&gt;]</c>; the CLI processor
/// (<c>HelpProcessor</c>) is the single source of truth. Adding an extension via
/// <c>az extension add</c> shows up on the next call — no cached catalog to invalidate.
/// The tool contract stays "always return a JSON string", so this class simply
/// forwards each command's JSON in the batched results array.
/// </summary>
public sealed class HelpTools
{
    private readonly string _azExePath;
    private readonly Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> _invokeAz;

    public HelpTools(string azExePath) : this(azExePath, ShellOutHelper.InvokeAzAsync)
    {
    }

    internal HelpTools(
        string azExePath,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> invokeAz)
    {
        _azExePath = azExePath ?? throw new ArgumentNullException(nameof(azExePath));
        _invokeAz = invokeAz ?? throw new ArgumentNullException(nameof(invokeAz));

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

        GetCommand = AIFunctionFactory.Create(GetCommandsAsync, new AIFunctionFactoryOptions
        {
            Name = "get_command",
            Description = "Return the full HelpCommand for one or more Azure CLI commands: parameters (name/psName, type, required, description), " +
                "examples (cli[] and powerShell[]), description, summary. " +
                "Pass 'names' as a list of cliName (e.g. 'vm create') or psName (e.g. 'New-AzVM') values; " +
                "lookup is case-insensitive and searches both surfaces. " +
                "Batch all needed commands in one call to avoid repeated tool round-trips. " +
                "Returns a results array in input order. Each entry is that command's HelpCommand, " +
                "or error=not_found (with suggestions) when the command doesn't exist. " +
                "Call AFTER list_commands has confirmed the commands exist.",
        });

        All = new[] { ListCommands, GetCommand };
    }

    public AIFunction ListCommands { get; }
    public AIFunction GetCommand { get; }
    public IReadOnlyList<AIFunction> All { get; }

    // Host-side entry to the same lookup the LLM's get_command uses. Lets the host
    // pre-fetch a failed command's help and inject it into context, so the agent
    // doesn't spend a turn looking up the command it was triggered by. Returns the
    // same single-command JSON as one batch entry, without the results wrapper.
    public async Task<string> LookupCommandAsync(string name, CancellationToken ct = default)
    {
        using var scope = ToolInvocationLog.Start("get_command", $"name={name}");
        var result = await LookupOneAsync(name, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
            scope.SetResult("error=missing_name");
        return result;
    }

    [Description("Return the full HelpIndex catalog.")]
    private async Task<string> ListCommandsAsync(CancellationToken ct = default)
    {
        // Async + await so ToolInvocationLog's scope stays alive for the entire
        // subprocess call — returning the Task without awaiting would dispose
        // the scope immediately, recording a near-zero duration.
        using var scope = ToolInvocationLog.Start("list_commands", "full catalog");
        return await _invokeAz(_azExePath, new[] { "agent", "tool", "help" }, ct).ConfigureAwait(false);
    }

    [Description("Return one or more commands' full HelpCommand as an ordered results array.")]
    private async Task<string> GetCommandsAsync(
        [Description("Command names to look up together. Each item is a cliName or psName. Pass all needed commands in this list to use one tool round-trip.")]
        IReadOnlyList<string> names,
        CancellationToken ct = default)
    {
        using var scope = ToolInvocationLog.Start("get_command", $"names={names?.Count ?? 0}");
        if (names is null || names.Count == 0)
        {
            scope.SetResult("error=missing_name");
            return "{\"error\":\"missing_name\",\"hint\":\"Pass one or more cliName/psName in names.\"}";
        }

        // Preserve each lookup's JSON, including structured per-command errors.
        var results = new StringBuilder("{\"results\":[");
        for (var i = 0; i < names.Count; i++)
        {
            if (ct.IsCancellationRequested)
                return CancelledBatch(scope);

            var result = await LookupOneAsync(names[i], ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
                return CancelledBatch(scope);

            if (i > 0) results.Append(',');
            results.Append(ValidateLookupResult(names[i], result));
        }
        results.Append("]}");
        scope.SetResult($"{names.Count} result(s)");
        return results.ToString();
    }

    private Task<string> LookupOneAsync(string name, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(name)
            ? Task.FromResult("{\"error\":\"missing_name\",\"hint\":\"Pass cliName or psName.\"}")
            : _invokeAz(_azExePath, new[] { "agent", "tool", "help", "--name", name }, ct);

    private static string ValidateLookupResult(string name, string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return result;
        }
        catch (JsonException)
        {
            // Invalid output belongs to this lookup, not the rest of the batch.
        }

        return JsonSerializer.Serialize(new
        {
            error = "invalid_help_response",
            name,
            hint = "The command help lookup did not return a JSON object.",
        });
    }

    private static string CancelledBatch(ToolInvocationLog.Scope scope)
    {
        scope.SetResult("error=shell_out_cancelled");
        return "{\"error\":\"shell_out_cancelled\"}";
    }
}
