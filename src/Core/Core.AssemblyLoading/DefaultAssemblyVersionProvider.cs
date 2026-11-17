// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.AssemblyLoading;

/// <summary>
/// Resolves the authored product version from a loaded assembly.
/// </summary>
public sealed class DefaultAssemblyVersionProvider : IProductVersionProvider
{
    public string ProductVersion { get; } = GetVersion(Assembly.GetEntryAssembly(), "0.0.0");

    /// <summary>
    /// Returns the informational version without SemVer build metadata, or
    /// <paramref name="fallback"/> when no authored version is available.
    /// </summary>
    public static string GetVersion(Assembly? assembly, string fallback = "unknown")
    {
        if (assembly is null)
        {
            return fallback;
        }

        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var buildMetadataStart = informationalVersion.IndexOf('+');
            return buildMetadataStart < 0
                ? informationalVersion
                : informationalVersion[..buildMetadataStart];
        }

        return fallback;
    }
}
