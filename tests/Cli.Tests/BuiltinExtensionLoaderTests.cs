using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Options;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

public class BuiltinExtensionLoaderTests
{
    [Fact]
    public void GetInstalledExtensions_ShouldEnumerateBuiltinExtensionsFromConfiguredRoot()
    {
        var repoRoot = RepoLocator.GetRepoRoot();
        var installationRoot = Path.Combine(repoRoot, "artifacts", "Debug");
        var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(installationRoot));

        var installed = loader.GetInstalledExtensions().ToArray();

        Assert.Contains(installed, x => x.Name == "Extension" && x.Version == string.Empty);
        Assert.Contains(installed, x => x.Name == "Login" && x.Version == string.Empty);
        Assert.Contains(installed, x => x.Name == "Config" && x.Version == string.Empty);
    }

    [Fact]
    public void GetInstalledExtensions_ShouldEnumerateInstallRootPackages_NotInBuiltInMap()
    {
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "Microsoft.Azure.Cirrus.Extensions.Foo");
        Directory.CreateDirectory(packageDir);

        try
        {
            File.WriteAllText(Path.Combine(packageDir, "Microsoft.Azure.Cirrus.Extensions.Foo.dll"), string.Empty);
            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var installed = loader.GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == "Foo" && x.Version == string.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class TestConfigurationService(string installationRoot) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new()
        {
            BuiltinInstallationDir = installationRoot,
            VersionedInstallationDir = installationRoot,
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
