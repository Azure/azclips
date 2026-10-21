// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;

namespace Core.Cli.Recovery;

/// <summary>
/// One-time work on the failure path: removes released Agent keys and shows the recovery hint
/// to users who have not chosen a recovery mode. Progress is kept in the "recovery" section of
/// the Agent state file, outside config.json; losing it only repeats the work.
/// </summary>
internal static class RecoveryOnboarding
{
    internal const int StateVersion = 1;
    private const string SectionName = "recovery";
    internal const int MaxHints = 15;
    internal static readonly string[] RetiredKeys = ["agent.enabled", "agent.sessionStyle", "agent.builtin.endpoint"];

    internal const string ManualHint =
        "To get AI help by running az troubleshoot analyze after a failure, run: az config set --key agent.recovery --value manual";
    internal const string AutoHint =
        "Or, to start AI automatically after every failure, run: az config set --key agent.recovery --value auto";

    internal sealed class State
    {
        public bool RetiredKeysRemoved { get; set; }
        public int HintsShown { get; set; }
    }

    internal static string GetStatePath() => HomePaths.GetDefaultAgentStatePath();

    /// <summary>Never fails the command: any error leaves the state file unchanged.</summary>
    internal static async Task TryRunAsync(
        IConfigurationService config, TextWriter error, string statePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsync(config, error, statePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
    }

    internal static async Task RunAsync(
        IConfigurationService config, TextWriter error, string statePath, CancellationToken cancellationToken = default)
    {
        var state = ReadState(statePath);
        if (state.RetiredKeysRemoved && state.HintsShown >= MaxHints)
            return;

        var stored = await config.ListConfigurationAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var changed = false;

        if (!state.RetiredKeysRemoved)
        {
            foreach (var key in RetiredKeys.Where(stored.ContainsKey))
                await config.DeleteConfigurationAsync(key, force: true, cancellationToken).ConfigureAwait(false);
            state.RetiredKeysRemoved = true;
            changed = true;
        }

        if (state.HintsShown < MaxHints && !IsRecoveryChosen(stored))
        {
            error.WriteLine(ManualHint);
            error.WriteLine(AutoHint);
            state.HintsShown++;
            changed = true;
        }

        if (changed)
            WriteState(statePath, state);
    }

    private static bool IsRecoveryChosen(IReadOnlyDictionary<string, object?> stored) =>
        stored.ContainsKey("agent.recovery") ||
        Environment.GetEnvironmentVariable("AZCLIPS_AGENT_RECOVERY") is not null;

    private static State ReadState(string path)
    {
        var section = ReadDocument(path)?[SectionName] as JsonObject;
        return new State
        {
            RetiredKeysRemoved = TryGet<bool>(section, "retiredKeysRemoved"),
            HintsShown = TryGet<int>(section, "hintsShown"),
        };
    }

    private static T TryGet<T>(JsonObject? section, string name) =>
        section?[name] is JsonValue value && value.TryGetValue<T>(out var result) ? result : default!;

    private static JsonObject? ReadDocument(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    // Other sections in the file are kept as they are.
    private static void WriteState(string path, State state)
    {
        var document = ReadDocument(path) ?? [];
        document["version"] = StateVersion;
        document[SectionName] = new JsonObject
        {
            ["retiredKeysRemoved"] = state.RetiredKeysRemoved,
            ["hintsShown"] = state.HintsShown,
        };
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }
}
