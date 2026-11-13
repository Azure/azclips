// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

internal static class PowerShellReleaseVersionResolver
{
    private const string AzRollupGuid = "7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5";
    private const string MissingRollupVersion = "0.0.0";

    internal static string GetReleaseVersion()
    {
        try
        {
            return GetReleaseVersion(
                Path.GetDirectoryName(typeof(ModuleInitializer).Assembly.Location)!,
                Environment.GetEnvironmentVariable("PSModulePath"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            return MissingRollupVersion;
        }
    }

    internal static string GetReleaseVersion(string assemblyDirectory, string? modulePath)
    {
        // The source and staged manifests are next to the dev build, while installed
        // modules are discovered through PSModulePath.
        var localManifests = new[]
        {
            Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "src", "PS", "Az", "Az.psd1")),
            Path.Combine(assemblyDirectory, "PS", "Az", "Az.psd1")
        };
        var localVersion = FindLatestVersion(localManifests);
        if (localVersion is not null)
            return localVersion.ToString();

        var roots = (modulePath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return FindLatestVersion(roots.SelectMany(GetInstalledManifests))?.ToString() ?? MissingRollupVersion;
    }

    private static IEnumerable<string> GetInstalledManifests(string root)
    {
        var azDirectory = Path.Combine(root, "Az");
        if (!Directory.Exists(azDirectory))
            yield break;

        yield return Path.Combine(azDirectory, "Az.psd1");
        foreach (var versionDirectory in Directory.EnumerateDirectories(azDirectory))
            yield return Path.Combine(versionDirectory, "Az.psd1");
    }

    internal static Version? FindLatestVersion(IEnumerable<string> manifestPaths)
    {
        Version? latest = null;
        foreach (var path in manifestPaths)
        {
            if (!File.Exists(path))
                continue;

            var manifest = File.ReadAllText(path);
            var guid = GetProperty(manifest, "GUID");
            if (!string.Equals(guid, AzRollupGuid, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = GetProperty(manifest, "ModuleVersion");
            if (!Version.TryParse(value, out var version))
                throw new InvalidDataException($"Az roll-up manifest '{path}' has an invalid ModuleVersion.");

            if (latest is null || version.CompareTo(latest) > 0)
                latest = version;
        }

        return latest;
    }

    private static string? GetProperty(string manifest, string property)
    {
        var match = Regex.Match(
            manifest,
            $@"^\s*{property}\s*=\s*['""](?<value>[^'""]+)['""]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline,
            TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups["value"].Value : null;
    }
}
