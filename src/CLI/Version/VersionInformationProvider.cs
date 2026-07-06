// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;

namespace Core.Cli.Version;

/// <summary>
/// Gathers CLI version information from the running host: the <c>az</c> entry assembly,
/// the .NET runtime, and the configuration paths. Pure data collection with no rendering,
/// so both <c>az version</c> (JSON) and <c>az --version</c> (text) can share one source of truth.
/// </summary>
/// <remarks>
/// Sections are not wrapped defensively: if something goes wrong, exceptions bubble up to
/// diagnose initialization or configuration problems cleanly instead of masking them behind
/// an "unknown" placeholder.
/// </remarks>
internal static class VersionInformationProvider
{
    public static VersionInformation Gather(IConfigurationService configurationService)
    {
        ArgumentNullException.ThrowIfNull(configurationService);

        var entryAssembly = Assembly.GetEntryAssembly();

        return new VersionInformation
        {
            AzCli = GetAzCliVersion(entryAssembly),
            Runtime = GetRuntimeInformation(),
            Paths = GetConfigurationPaths(configurationService)
        };
    }

    private static string GetAzCliVersion(Assembly? entryAssembly)
    {
        if (entryAssembly == null)
        {
            return "unknown";
        }

        // Prefer the InformationalVersion (the value declared in the CLI csproj, e.g. "0.1.3") to
        // match how the CLI version is authored; fall back to the numeric AssemblyName.Version
        // (e.g. "0.1.3.0") only when the attribute is absent.
        var informationalVersion =
            entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            // The .NET SDK appends "+<source revision id>" (SemVer build metadata) to the authored
            // value; strip it so we report just the declared version (e.g. "0.1.3" instead of
            // "0.1.3+9f50d509..."). Any pre-release suffix before the '+' (e.g. "-beta") is preserved.
            var buildMetadataStart = informationalVersion.IndexOf('+');
            return buildMetadataStart < 0
                ? informationalVersion
                : informationalVersion[..buildMetadataStart];
        }

        return entryAssembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static RuntimeInfo GetRuntimeInformation()
    {
        return new RuntimeInfo
        {
            DotNet = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private static PathInfo GetConfigurationPaths(IConfigurationService configurationService)
    {
        // Install location never depends on config, so resolve it first and independently.
        var info = new PathInfo
        {
            InstallLocation = GetInstallLocation(),
            ConfigLocation = "unknown"
        };

        // Resolve from the configuration system so the reported paths match what the CLI actually
        // uses (config-driven, honoring user overrides).
        var configFilePath = configurationService.GetConfigFilePath();
        info.ConfigLocation = Path.GetDirectoryName(configFilePath) ?? configFilePath;

        return info;
    }

    // Report the app base directory — a stable, directory-like path that also works under single-file
    // publish (unlike Assembly.Location, which is empty there). Mirrors how the CLI derives its
    // install/base directory in AssemblyLoadingOptions. The trailing directory separator is trimmed so
    // the value reads like the Config path (e.g. "...\Core.Cli" instead of "...\Core.Cli\").
    private static string GetInstallLocation()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return string.IsNullOrEmpty(baseDirectory)
            ? "unknown"
            : Path.TrimEndingDirectorySeparator(baseDirectory);
    }
}
