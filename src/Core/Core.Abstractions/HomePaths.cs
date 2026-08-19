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
    public const string ExtensionsDirectoryName = "extensions";
    public const string RegistryFileName = "registry.json";
    public const string ConfigFileName = "config.json";
    public const string TraceDirectoryName = "trace";
    public const string TelemetryTraceDirectoryName = "traces";
    public const string ArmCacheDirectoryName = "arm-cache";
    public const string AuthContextFileName = "context.json";

    public static string GetDefaultHomeRoot(string? userProfilePath = null)
    {
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

    public static string GetDefaultTraceDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), TraceDirectoryName);

    public static string GetDefaultTraceConfigValue()
        => $"~/{DefaultHomeDirectoryName}/{TraceDirectoryName}";

    public static string GetDefaultArmCacheDirectory(string? userProfilePath = null)
        => Path.Combine(GetDefaultHomeRoot(userProfilePath), ArmCacheDirectoryName);

    public static string GetDefaultLocalAppDataRoot(string? localApplicationDataPath = null)
    {
        var local = string.IsNullOrWhiteSpace(localApplicationDataPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localApplicationDataPath;

        return Path.Combine(local, DefaultHomeDirectoryName);
    }

    public static string GetDefaultAuthContextFilePath(string? localApplicationDataPath = null)
        => Path.Combine(GetDefaultLocalAppDataRoot(localApplicationDataPath), AuthContextFileName);

    /// <summary>
    /// Where telemetry spans are buffered before a background process uploads them:
    /// <c>{LocalApplicationData}/.azclips/traces</c>.
    /// Distinct from <see cref="GetDefaultTraceDirectory"/>, which is the user-facing
    /// diagnostic trace directory under the profile root — different root, different
    /// purpose, confusingly similar name.
    /// </summary>
    public static string GetDefaultTelemetryTraceDirectory(string? localApplicationDataPath = null)
        => Path.Combine(GetDefaultLocalAppDataRoot(localApplicationDataPath), TelemetryTraceDirectoryName);
}