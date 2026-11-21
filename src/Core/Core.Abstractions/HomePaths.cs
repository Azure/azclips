// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Shared helpers for user-scoped home paths under the Azclips root.
/// Keeping these values centralized avoids scattered path-segment literals.
/// </summary>
public static class HomePaths
{
    public const string DefaultHomeDirectoryName = ".azclips";

    /// <summary>
    /// Points every azclips home path (config, extensions, profiles, auth context, traces, survey) at one
    /// directory instead of the per-user defaults. Scenario tests use it to isolate a run from the
    /// developer's real state; unset means the user-profile Azclips home.
    /// </summary>
    public const string HomeOverrideVariable = "AZCLIPS_HOME";

    private static string? OverrideRoot
        => Environment.GetEnvironmentVariable(HomeOverrideVariable) is { Length: > 0 } value ? value : null;
    public const string ExtensionsDirectoryName = "extensions";
    public const string RegistryFileName = "registry.json";
    public const string ConfigFileName = "config.json";
    public const string TraceDirectoryName = "traces";
    public const string AgentDirectoryName = "agent";
    public const string AgentArmDirectoryName = "arm";
    public const string AgentCopilotDirectoryName = "copilot";
    public const string AgentEvaluationDirectoryName = "evaluation";
    public const string AgentFailureHistoryDirectoryName = "failure-history";
    public const string AgentHelpDirectoryName = "help";
    // Agent JSONL traces; not the telemetry upload queue (TraceDirectoryName, "traces").
    public const string AgentTraceDirectoryName = "trace";
    public const string AgentStateFileName = "state.json";
    public const string ProfilesFileName = "profiles.json";
    public const string ActiveProfileFileName = "active-profile";
    public const string DataBoundaryCacheFileName = "data-boundary-cache.json";
    public const string SurveyFileName = "azclips-survey.json";

    public static string GetDefaultSurveyFilePath(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), SurveyFileName);

    public static string GetDefaultHomeRoot(string? userProfilePath = null)
    {
        if (OverrideRoot is { } overridden)
        {
            return overridden;
        }

        var home = string.IsNullOrWhiteSpace(userProfilePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfilePath;

        return Path.Combine(home, DefaultHomeDirectoryName);
    }

    public static string GetDefaultExtensionsRoot(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), ExtensionsDirectoryName);

    public static string GetDefaultRegistryPath(string? userProfilePath = null)
        => Path.Combine(GetDefaultExtensionsRoot(userProfilePath), RegistryFileName);

    public static string GetDefaultConfigFilePath(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), ConfigFileName);

    public static string GetDefaultLocalAppDataRoot(string? localApplicationDataPath = null)
    {
        if (OverrideRoot is { } overridden)
        {
            return overridden;
        }

        var local = string.IsNullOrWhiteSpace(localApplicationDataPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localApplicationDataPath;

        return Path.Combine(local, DefaultHomeDirectoryName);
    }

    public static string GetDefaultTraceDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), TraceDirectoryName);

    public static string GetDefaultDataBoundaryCachePath(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), DataBoundaryCacheFileName);

    /// <summary>
    /// Root for program data owned by the Agent, under <see cref="GetDefaultHomeRoot"/>.
    /// Everything here is rebuilt or expired by the Agent itself.
    /// </summary>
    public static string GetDefaultAgentRoot(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), AgentDirectoryName);

    public static string GetDefaultArmCacheDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentArmDirectoryName);

    public static string GetDefaultAgentCopilotDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentCopilotDirectoryName);

    public static string GetDefaultAgentEvaluationDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentEvaluationDirectoryName);

    public static string GetDefaultFailureHistoryDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentFailureHistoryDirectoryName);

    public static string GetDefaultAgentHelpDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentHelpDirectoryName);

    public static string GetDefaultAgentTraceDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentTraceDirectoryName);

    public static string GetDefaultAgentStatePath(string? userProfilePath = null)
        => Path.Combine(GetDefaultAgentRoot(userProfilePath), AgentStateFileName);
}
