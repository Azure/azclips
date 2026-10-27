// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;

namespace Core.Cli.Recovery;

/// <summary>
/// Shows the recovery hint on the failure path to users who have not chosen a recovery mode.
/// The hint count is kept in the "recovery" section of the Agent state file, outside config.json;
/// losing it only repeats the hint.
/// </summary>
internal static class RecoveryOnboarding
{
    internal const int StateVersion = 1;
    private const string SectionName = "recovery";
    internal const int MaxHints = 15;

    internal const string ManualHint =
        "To get AI help by running az troubleshoot analyze after a failure, run: az config set --key agent.recovery.trigger --value manual";
    internal const string AutoHint =
        "Or, to start AI automatically after every failure, run: az config set --key agent.recovery.trigger --value auto";

    internal sealed class State
    {
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
        if (state.HintsShown >= MaxHints)
            return;

        var stored = await config.ListConfigurationAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (IsRecoveryChosen(stored))
            return;

        error.WriteLine(ManualHint);
        error.WriteLine(AutoHint);
        state.HintsShown++;
        WriteState(statePath, state);
    }

    private static bool IsRecoveryChosen(IReadOnlyDictionary<string, object?> stored) =>
        stored.ContainsKey("agent.recovery.trigger");

    private static State ReadState(string path)
    {
        var section = FileLock.ReadJson(path)[SectionName] as JsonObject;
        return new State
        {
            HintsShown = TryGet<int>(section, "hintsShown"),
        };
    }

    private static T TryGet<T>(JsonObject? section, string name) =>
        section?[name] is JsonValue value && value.TryGetValue<T>(out var result) ? result : default!;

    // Other sections in the file are kept as they are.
    private static void WriteState(string path, State state) =>
        FileLock.UpdateJson(path, document =>
        {
            document["version"] = StateVersion;
            document[SectionName] = new JsonObject
            {
                ["hintsShown"] = state.HintsShown,
            };
        }, TimeSpan.FromSeconds(1));
}
