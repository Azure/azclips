// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Recording;

/// <summary>
/// Resolves the record/playback mode from <c>AZURE_TEST_MODE</c>, mirroring the Azure PowerShell
/// test framework. Unset (the CI/default) means Playback; see the project README for local use.
/// </summary>
public static class RecorderModeResolver
{
    /// <summary>The environment variable that selects the mode. Matches Azure PowerShell.</summary>
    public const string EnvironmentVariable = "AZURE_TEST_MODE";

    /// <summary>Reads <see cref="EnvironmentVariable"/> and resolves the mode (default Playback).</summary>
    public static RecorderMode FromEnvironment()
        => Parse(Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>
    /// Maps a value to a <see cref="RecorderMode"/>: null/blank means Playback, otherwise it must
    /// name a mode (case-insensitive). A typo throws instead of silently replaying.
    /// </summary>
    public static RecorderMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return RecorderMode.Playback;

        var trimmed = value.Trim();
        foreach (var mode in Enum.GetValues<RecorderMode>())
        {
            if (string.Equals(trimmed, mode.ToString(), StringComparison.OrdinalIgnoreCase)) return mode;
        }

        var valid = string.Join(", ", Enum.GetNames<RecorderMode>());
        throw new ArgumentException(
            $"'{value}' is not a valid {EnvironmentVariable} value. Expected one of: {valid} (unset means {nameof(RecorderMode.Playback)}).",
            nameof(value));
    }
}
