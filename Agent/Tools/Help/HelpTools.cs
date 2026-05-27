using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Agent.Tools.Cache;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Help;

/// <summary>
/// Two AI agent tools that READ the embedded cirrus help corpus:
///   • list_commands — flat catalog of every cirrus command (cliName + psName + summary)
///   • get_command   — full HelpCommand for one verb (parameters + examples + description)
///
/// Data source is two static JSON files copied to output (HelpIndex.json / HelpManifest.json)
/// shaped after PR #226's HelpIndex / HelpManifest records. v2 will swap this for an
/// IHelpIndex runtime adapter once #226 merges; tool surface stays the same.
/// </summary>
public sealed class HelpTools
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly string _indexJson;
    private readonly Dictionary<string, JsonElement> _byCliName;
    private readonly Dictionary<string, JsonElement> _byPsName;

    public HelpTools()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Tools", "Help");
        var indexPath = Path.Combine(dir, "HelpIndex.json");
        var manifestPath = Path.Combine(dir, "HelpManifest.json");

        if (!File.Exists(indexPath) || !File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Help corpus missing. Expected: {indexPath} and {manifestPath}");
        }

        _indexJson = File.ReadAllText(indexPath);

        var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        _byCliName = new(StringComparer.OrdinalIgnoreCase);
        _byPsName = new(StringComparer.OrdinalIgnoreCase);

        foreach (var cmd in manifestDoc.RootElement.GetProperty("commands").EnumerateArray())
        {
            var clone = cmd.Clone();
            if (clone.TryGetProperty("cliName", out var cliEl) && cliEl.GetString() is { Length: > 0 } cliName)
                _byCliName[cliName] = clone;
            if (clone.TryGetProperty("psName", out var psEl) && psEl.GetString() is { Length: > 0 } psName)
                _byPsName[psName] = clone;
        }

        ListCommands = AIFunctionFactory.Create(ListCommandsImpl, new AIFunctionFactoryOptions
        {
            Name = "list_commands",
            Description = "Return the full catalog of cirrus commands across all extensions. " +
                "Each entry has cliName (e.g. 'vm create-combined', empty string when no CLI verb), " +
                "psName (e.g. 'New-VMCombined', empty string when no PS cmdlet), extension, group, summary. " +
                "Use FIRST when you don't already know whether a command exists or what its exact name is. " +
                "After picking a candidate, call get_command for its parameters and examples. " +
                "Cheap (~1 KB), no network call.",
        });

        GetCommand = AIFunctionFactory.Create(GetCommandImpl, new AIFunctionFactoryOptions
        {
            Name = "get_command",
            Description = "Return the full HelpCommand for one cirrus verb: parameters (cliName/psName, type, required, description), " +
                "examples (cli[] and powerShell[]), description, summary. " +
                "Pass 'name' as the cliName (e.g. 'vm create-combined') OR the psName (e.g. 'New-VMCombined') — " +
                "lookup is case-insensitive and searches both surfaces. " +
                "Returns error=not_found with suggestions when the verb doesn't exist. " +
                "Call AFTER list_commands has confirmed the verb exists.",
        });

        All = new[] { ListCommands, GetCommand };
    }

    public AIFunction ListCommands { get; }
    public AIFunction GetCommand { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Return the full HelpIndex catalog.")]
    private string ListCommandsImpl()
    {
        using var scope = ToolInvocationLog.Start("list_commands", "full catalog");
        var doc = JsonDocument.Parse(_indexJson);
        var commandCount = doc.RootElement.GetProperty("commands").GetArrayLength();
        var groupCount = doc.RootElement.GetProperty("groups").GetArrayLength();
        scope.SetResult($"groups={groupCount} commands={commandCount}");
        return _indexJson;
    }

    [Description("Return one command's full HelpCommand (parameters + examples + description).")]
    private string GetCommandImpl(
        [Description("Command name. Pass cliName (e.g. 'vm create-combined', 'group list') OR psName (e.g. 'New-VMCombined', 'Get-ResourceGroup_Get'). Case-insensitive.")]
        string name)
    {
        using var scope = ToolInvocationLog.Start("get_command", $"name={name}");

        if (string.IsNullOrWhiteSpace(name))
        {
            scope.SetResult("error=missing_name");
            return JsonSerializer.Serialize(new { error = "missing_name", hint = "Pass cliName or psName." }, Json);
        }

        if (_byCliName.TryGetValue(name, out var cliHit))
        {
            scope.SetResult($"cli hit: {name}");
            return cliHit.GetRawText();
        }
        if (_byPsName.TryGetValue(name, out var psHit))
        {
            scope.SetResult($"ps hit: {name}");
            return psHit.GetRawText();
        }

        var suggestions = _byCliName.Keys.Concat(_byPsName.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase)
                     || name.Contains(k, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToArray();
        scope.SetResult($"not_found, suggestions={suggestions.Length}");
        return JsonSerializer.Serialize(new
        {
            error = "not_found",
            name,
            hint = "Call list_commands to see the full catalog.",
            suggestions,
        }, Json);
    }
}
