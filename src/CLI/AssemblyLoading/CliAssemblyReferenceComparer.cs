// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Breaks matching-pin and equivalent-version ties: Bundled before Installed, then root priority,
/// DLL path, and cache identity. Bundled roots themselves form an ordered fallback chain:
/// only the first available copy is exposed to selection.
/// </summary>
public sealed class CliAssemblyReferenceComparer : IComparer<AssemblyReference>
{
    public static CliAssemblyReferenceComparer Instance { get; } = new();

    public int Compare(AssemblyReference? x, AssemblyReference? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }
        if (x is null)
        {
            return -1;
        }
        if (y is null)
        {
            return 1;
        }
        if (x is ExtensionInstallation left && y is ExtensionInstallation right)
        {
            var comparison = left.SourcePriority.CompareTo(right.SourcePriority);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = left.RootPriority.CompareTo(right.RootPriority);
            if (comparison != 0)
            {
                return comparison;
            }
            var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            comparison = pathComparer.Compare(left.Location, right.Location);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        else if (x is ExtensionInstallation)
        {
            return -1;
        }
        else if (y is ExtensionInstallation)
        {
            return 1;
        }
        return StringComparer.Ordinal.Compare(x.CacheKey, y.CacheKey);
    }
}
