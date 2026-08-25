// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;
using Core.Configuration.Options;

namespace Core.Cli.Version;

/// <summary>
/// Gathers CLI version information from the running host. Both <c>az version</c> (JSON) and <c>az --version</c> (text)
/// share one source of truth.
/// </summary>
/// <remarks>
/// Version data is gathered directly from host/runtime APIs; most failures are allowed to bubble up
/// to aid diagnosis, with a small number of fields falling back to "unknown" when the host does not
/// provide a value (e.g. no entry assembly or empty base directory).
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
        
        // Resolve from the configuration system 
        var configFilePath = configurationService.GetConfigFilePath();
        
        var info = new PathInfo
        {
            InstallLocation = GetInstallLocation(),
            ConfigLocation = Path.GetDirectoryName(configFilePath) ?? configFilePath,
            ExtensionsLocation = configurationService.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot
        };

        return info;
    }

    // Report the app base directory — a stable, directory-like path that also works under single-file
    // publish (unlike Assembly.Location, which is empty there).
    private static string GetInstallLocation()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return string.IsNullOrEmpty(baseDirectory)
            ? "unknown"
            : Path.TrimEndingDirectorySeparator(baseDirectory);
    }
}
