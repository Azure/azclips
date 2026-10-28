// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Options;
using Core.Extensions;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Extension Load Tracker Tests")]
public sealed class ExtensionSelectionPathOverrideTests
{
    private const string PackageIdPrefix = "Microsoft.Azure.Azclips.Extensions.";

    [Fact]
    public void CustomBundledAndInstalledDirs_ShouldFeedSelection_AndPreferHigherVersion()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var shortName = $"Compute_{suffix}";
        var packageId = PackageIdPrefix + shortName;

        var installedRoot = Path.Combine(Path.GetTempPath(), $"selection-override-{suffix}", "installed");
        var bundledPackageDir = Path.Combine(BundledExtensionLoader.GetDefaultRoot(), packageId);

        Directory.CreateDirectory(bundledPackageDir);
        Directory.CreateDirectory(installedRoot);

        try
        {
            File.WriteAllText(Path.Combine(bundledPackageDir, $"{packageId}.dll"), string.Empty);
            File.WriteAllText(Path.Combine(bundledPackageDir, $"{packageId}.1.2.0.nupkg"), string.Empty);

            var installedPackageDir = Path.Combine(installedRoot, packageId, "1.3.0");
            Directory.CreateDirectory(installedPackageDir);
            File.WriteAllText(Path.Combine(installedPackageDir, $"{packageId}.dll"), string.Empty);

            var configuration = new TestConfigurationService(installedRoot);
            var logging = new NullLoggingService();

            var bundledLoader = new BundledExtensionLoader(logging, configuration);
            var installedLoader = new InstalledExtensionLoader(logging, configuration);

            var bundledInstalled = Assert.Single(
                bundledLoader.GetInstalledExtensions(),
                x => x.Name == shortName);
            Assert.Equal("1.2.0", bundledInstalled.Version);

            var installedInstalled = Assert.Single(installedLoader.GetInstalledExtensions());
            Assert.Equal(shortName, installedInstalled.Name);
            Assert.Equal("1.3.0", installedInstalled.Version);

            var provider = new CLIAssemblyLoadingProvider(
                [bundledLoader, installedLoader],
                logging);
            var installed = provider.GetAllInstalledExtensions();

            Assert.NotNull(installed);
            Assert.Contains(installed!, item => item.Name == shortName && item.Version == "1.2.0");
            Assert.Contains(installed!, item => item.Name == shortName && item.Version == "1.3.0");

            var candidates = installed!
                .Where(item => item.Name == shortName)
                .Select(item => CreateInfo(packageId, item.Version, item.Version == "1.2.0" ? ["vm bundled"] : ["vm installed"]))
                .ToArray();

            var selection = ExtensionCandidateSelector.Select(candidates);

            Assert.Equal("1.3.0", selection.Selected.ResolvedVersion);
            Assert.Equal(ExtensionSelectionReason.HigherVersion, selection.Reason);
            Assert.Contains("vm installed", selection.Selected.Commands.Keys, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(bundledPackageDir);
            DirectoryUtils.TryDeleteDirectory(Path.GetDirectoryName(installedRoot)!);
        }
    }

    [Fact]
    public void GetAllInstalledExtensions_ShouldPropagateLoaderLocation_IntoInstalledExtensionRef()
    {
        // Both loaders' DLL paths must flow through the provider without being dropped or overwritten.
        var suffix = Guid.NewGuid().ToString("N");
        var bundledShortName = $"Login_{suffix}";
        var installedShortName = $"Resources_{suffix}";
        var bundledPackageId = PackageIdPrefix + bundledShortName;
        var installedPackageId = PackageIdPrefix + installedShortName;

        var installedRoot = Path.Combine(Path.GetTempPath(), $"location-flow-{suffix}", "installed");
        var bundledPackageDir = Path.Combine(BundledExtensionLoader.GetDefaultRoot(), bundledPackageId);

        Directory.CreateDirectory(bundledPackageDir);
        Directory.CreateDirectory(installedRoot);

        try
        {
            var expectedBundledDll = Path.Combine(bundledPackageDir, $"{bundledPackageId}.dll");
            File.WriteAllText(expectedBundledDll, string.Empty);

            var installedPackageDir = Path.Combine(installedRoot, installedPackageId, "2.0.0");
            Directory.CreateDirectory(installedPackageDir);
            var expectedInstalledDll = Path.Combine(installedPackageDir, $"{installedPackageId}.dll");
            File.WriteAllText(expectedInstalledDll, string.Empty);

            var configuration = new TestConfigurationService(installedRoot);
            var logging = new NullLoggingService();

            var provider = new CLIAssemblyLoadingProvider(
                [
                    new BundledExtensionLoader(logging, configuration),
                    new InstalledExtensionLoader(logging, configuration),
                ],
                logging);

            var installed = provider.GetAllInstalledExtensions();

            Assert.NotNull(installed);
            var login = Assert.Single(installed!, item => item.Name == bundledShortName);
            Assert.Equal(expectedBundledDll, login.Location);
            var resources = Assert.Single(installed!, item => item.Name == installedShortName);
            Assert.Equal(expectedInstalledDll, resources.Location);
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(bundledPackageDir);
            DirectoryUtils.TryDeleteDirectory(Path.GetDirectoryName(installedRoot)!);
        }
    }

    [Fact]
    public void GetAllInstalledExtensions_ShouldReportEveryInstalledVersion_SoAPinCanSelectItsOwnDll()
    {
        // The Installed loader reports every version with its own DLL; the selector, not the
        // loader, applies the pin. The selected copy's Location is the one execution loads.
        var installedRoot = Path.Combine(Path.GetTempPath(), $"pin-location-{Guid.NewGuid():N}", "installed");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        Directory.CreateDirectory(installedRoot);

        try
        {
            foreach (var version in new[] { "1.2.0", "1.3.0" })
            {
                var versionDir = Path.Combine(installedRoot, packageId, version);
                Directory.CreateDirectory(versionDir);
                File.WriteAllText(Path.Combine(versionDir, "Microsoft.Azure.Azclips.Extensions.Compute.dll"), string.Empty);
            }

            var configuration = new TestConfigurationService(installedRoot);
            var logging = new NullLoggingService();
            var provider = new CLIAssemblyLoadingProvider(
                [new InstalledExtensionLoader(logging, configuration)],
                logging);

            var installed = provider.GetAllInstalledExtensions()!;

            Assert.Equal(["1.3.0", "1.2.0"], installed.Select(item => item.Version));
            string ExpectedDll(string version) =>
                Path.Combine(installedRoot, packageId, version, "Microsoft.Azure.Azclips.Extensions.Compute.dll");
            Assert.All(installed, item => Assert.Equal(ExpectedDll(item.Version), item.Location));

            var pin = new ExtensionRegistryEntry { Version = "1.2.0", Pinned = true, InstalledAt = DateTimeOffset.UtcNow };
            var selection = ExtensionCandidateSelector.Select(
                installed.Select(item => new ExtensionRoutingInfo(packageId, item.Version, [], [], item.Location)).ToArray(),
                pin);

            Assert.Equal(ExtensionSelectionReason.Pinned, selection.Reason);
            Assert.Equal(ExpectedDll("1.2.0"), selection.Selected.Location);
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(Path.GetDirectoryName(installedRoot)!);
        }
    }

    [Fact]
    public void InstalledExtensionLoader_GetInstalledExtensions_ShouldSkipVersionDirWithoutDll()
    {
        // Corrupt/partial install: version subdir exists but no DLL. Enumeration must skip it —
        // dispatch couldn't load it anyway, so surfacing it in `az extension list` misleads users.
        var installedRoot = Path.Combine(Path.GetTempPath(), $"missing-dll-{Guid.NewGuid():N}", "installed");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var versionDir = Path.Combine(installedRoot, packageId, "1.0.0");
        Directory.CreateDirectory(versionDir);

        try
        {
            var configuration = new TestConfigurationService(installedRoot);
            var loader = new InstalledExtensionLoader(new NullLoggingService(), configuration);

            Assert.Empty(loader.GetInstalledExtensions());
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(Path.GetDirectoryName(installedRoot)!);
        }
    }

    private static DiscoveredExtensionInfo CreateInfo(string packageId, string version, IEnumerable<string> commandNames)
    {
        var commands = commandNames.ToDictionary(
            command => command,
            command => new CommandMetadata(
                Name: command,
                InvocationName: command,
                Aliases: [],
                FullName: command,
                AssemblyName: packageId,
                ProcessorType: typeof(object),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase);

        var verbs = commandNames
            .Select(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DiscoveredExtensionInfo(packageId, version, verbs, commands, Location: null);
    }

    private sealed class TestConfigurationService(string installedRoot) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new()
        {
            InstalledExtensionsRoot = installedRoot,
        };

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(AssemblyLoadingOptions))
            {
                return (T)(object)_options;
            }

            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public string GetConfigFilePath() => string.Empty;

        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }

    private sealed class NullLoggingService : ILoggingService
    {
        public void Log(string message) { }
        public void Debug(string message) { }
        public void Error(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Warn(string message) { }
    }
}
