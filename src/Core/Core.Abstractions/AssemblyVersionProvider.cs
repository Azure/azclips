// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Abstractions;

/// <summary>
/// Resolves the authored product version from a loaded assembly.
/// </summary>
public static class AssemblyVersionProvider
{
    /// <summary>
    /// Returns the informational version without SemVer build metadata, falling back to the
    /// numeric assembly version and then <paramref name="fallback"/>.
    /// </summary>
    public static string GetVersion(Assembly? assembly, string fallback = "unknown")
    {
        if (assembly is null)
        {
            return fallback;
        }

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(version))
        {
            version = assembly.GetName().Version?.ToString();
        }

        if (string.IsNullOrEmpty(version))
        {
            return fallback;
        }

        var buildMetadataStart = version.IndexOf('+');
        return buildMetadataStart < 0 ? version : version[..buildMetadataStart];
    }
}
