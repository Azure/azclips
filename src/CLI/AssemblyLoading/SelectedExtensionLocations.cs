// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using NuGet.Versioning;

namespace Core.Cli;

/// <summary>
/// Remembers which package copies routing selected: the package ids, in selection order, and
/// the DLL behind each one. The CLI loading provider loads that exact DLL instead of
/// re-searching loaders by name and version, and help and completions read the same selection.
/// CLI-only; Core still passes just a name and version.
/// </summary>
public sealed class SelectedExtensionLocations
{
    private readonly ConcurrentDictionary<string, (string Version, string Location)> _selected =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _packageIdsLock = new();
    private List<string> _packageIds = [];

    /// <summary>Package ids routing selected, one per package, in selection order.</summary>
    public IReadOnlyList<string> PackageIds
    {
        get
        {
            lock (_packageIdsLock)
            {
                return _packageIds.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_packageIdsLock)
        {
            _packageIds = [];
            _selected.Clear();
        }
    }

    public void Record(string packageIdOrName, string? version, string? location)
    {
        lock (_packageIdsLock)
        {
            _packageIds.Add(packageIdOrName);
        }

        if (location is not null)
        {
            _selected[Constants.StripKnownExtensionPrefix(packageIdOrName)] = (version ?? string.Empty, location);
        }
    }

    /// <summary>
    /// Returns the selected location when the request matches the selected version. An empty
    /// requested version matches, because routing already chose the copy for this extension.
    /// </summary>
    public bool TryGet(string extensionName, string? version, out string location)
    {
        location = string.Empty;
        if (!_selected.TryGetValue(Constants.StripKnownExtensionPrefix(extensionName), out var selected) ||
            !VersionsMatch(version, selected.Version))
        {
            return false;
        }

        location = selected.Location;
        return true;
    }

    private static bool VersionsMatch(string? requested, string selected)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return true;
        }

        if (NuGetVersion.TryParse(requested, out var left) && NuGetVersion.TryParse(selected, out var right))
        {
            return VersionComparer.VersionRelease.Equals(left, right);
        }

        return string.Equals(requested, selected, StringComparison.OrdinalIgnoreCase);
    }
}
