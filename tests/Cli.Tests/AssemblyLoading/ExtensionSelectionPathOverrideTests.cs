// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Options;
using Core.Extensions;
using Tests.Common.Utilities;

namespace Cli.Tests;

public sealed class ExtensionSelectionPathOverrideTests
{
    [Fact]
    public void CustomBuiltinAndVersionedDirs_ShouldFeedSelection_AndPreferHigherVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"selection-override-{Guid.NewGuid():N}");
        var builtinRoot = Path.Combine(root, "builtin");
        var versionedRoot = Path.Combine(root, "versioned");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";

        Directory.CreateDirectory(builtinRoot);
        Directory.CreateDirectory(versionedRoot);

        try
        {
            var builtinPackageDir = Path.Combine(builtinRoot, packageId);
            Directory.CreateDirectory(builtinPackageDir);
            File.WriteAllText(Path.Combine(builtinPackageDir, "Microsoft.Azure.Azclips.Extensions.Compute.dll"), string.Empty);
            File.WriteAllText(Path.Combine(builtinPackageDir, "Microsoft.Azure.Azclips.Extensions.Compute.1.2.0.nupkg"), string.Empty);

            var versionedPackageDir = Path.Combine(versionedRoot, packageId, "1.3.0");
            Directory.CreateDirectory(versionedPackageDir);

            var configuration = new TestConfigurationService(builtinRoot, versionedRoot);
            var logging = new NullLoggingService();

            var builtinLoader = new BuiltinExtensionLoader(logging, configuration);
            var versionedLoader = new MultiVersionExtensionLoader(logging, configuration);

            var builtinInstalled = Assert.Single(builtinLoader.GetInstalledExtensions());
            Assert.Equal("Compute", builtinInstalled.Name);
            Assert.Equal("1.2.0", builtinInstalled.Version);

            var versionedInstalled = Assert.Single(versionedLoader.GetInstalledExtensions());
            Assert.Equal("Compute", versionedInstalled.Name);
            Assert.Equal("1.3.0", versionedInstalled.Version);

            var provider = new CLIAssemblyLoadingProvider(
                [builtinLoader, versionedLoader],
                logging,
                new InMemoryRegistryStore(new ExtensionRegistry()));
            var installed = provider.GetAllInstalledExtensions();

            Assert.NotNull(installed);
            Assert.Contains(installed!, item => item.Name == "Compute" && item.Version == "1.2.0");
            Assert.Contains(installed!, item => item.Name == "Compute" && item.Version == "1.3.0");

            var candidates = installed!
                .Where(item => item.Name == "Compute")
                .Select(item => CreateInfo(packageId, item.Version, item.Version == "1.2.0" ? ["vm builtin"] : ["vm versioned"]))
                .ToArray();

            var selection = ExtensionCandidateSelector.Select(candidates);

            Assert.Equal("1.3.0", selection.Selected.ResolvedVersion);
            Assert.Equal(ExtensionSelectionReason.HigherVersion, selection.Reason);
            Assert.Contains("vm versioned", selection.Selected.Commands.Keys, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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

        return new DiscoveredExtensionInfo(packageId, version, verbs, commands);
    }

    private sealed class TestConfigurationService(string builtinRoot, string versionedRoot) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new()
        {
            BuiltinInstallationDir = builtinRoot,
            VersionedInstallationDir = versionedRoot,
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