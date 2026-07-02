// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Shared helpers for user-scoped extension storage paths.
/// Keeping these values centralized avoids scattered path-segment literals.
/// </summary>
public static class ExtensionHomePaths
{
    public const string DefaultHomeDirectoryName = ".azclips";
    public const string ExtensionsDirectoryName = "extensions";
    public const string RegistryFileName = "registry.json";

    public static string GetDefaultExtensionsRoot(string? userProfilePath = null)
    {
        var home = string.IsNullOrWhiteSpace(userProfilePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfilePath;

        return Path.Combine(home, DefaultHomeDirectoryName, ExtensionsDirectoryName);
    }

    public static string GetDefaultRegistryPath(string? userProfilePath = null)
        => Path.Combine(GetDefaultExtensionsRoot(userProfilePath), RegistryFileName);
}