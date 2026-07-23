// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Options;
using Xunit;

namespace Cli.Tests;

public class BuiltinExtensionLoaderTests
{
    [Fact]
    public void GetInstalledExtensions_ShouldEnumerateBuiltinExtensionsFromConfiguredRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var extensionPackage = Path.Combine(root, "Microsoft.Azure.Azclips.Extensions.Extension");
        var loginPackage = Path.Combine(root, "Microsoft.Azure.Azclips.Extensions.Login");
        var configPackage = Path.Combine(root, "Microsoft.Azure.Azclips.Extensions.Config");

        Directory.CreateDirectory(extensionPackage);
        Directory.CreateDirectory(loginPackage);
        Directory.CreateDirectory(configPackage);

        try
        {
            File.WriteAllText(Path.Combine(extensionPackage, "Microsoft.Azure.Azclips.Extensions.Extension.dll"), string.Empty);
            File.WriteAllText(Path.Combine(loginPackage, "Microsoft.Azure.Azclips.Extensions.Login.dll"), string.Empty);
            File.WriteAllText(Path.Combine(configPackage, "Microsoft.Azure.Azclips.Extensions.Config.dll"), string.Empty);

            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));
            var installed = loader.GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == "Extension" && x.Version == string.Empty);
            Assert.Contains(installed, x => x.Name == "Login" && x.Version == string.Empty);
            Assert.Contains(installed, x => x.Name == "Config" && x.Version == string.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldEnumerateInstallRootPackages_NotInBuiltInMap()
    {
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "Microsoft.Azure.Azclips.Extensions.Foo");
        Directory.CreateDirectory(packageDir);

        try
        {
            File.WriteAllText(Path.Combine(packageDir, "Microsoft.Azure.Azclips.Extensions.Foo.dll"), string.Empty);
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

    [Fact]
    public void GetInstalledExtensions_ShouldUseHighestNupkgVersion_WhenAvailableInInstallRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var packageDir = Path.Combine(root, packageId);
        Directory.CreateDirectory(packageDir);

        try
        {
            File.WriteAllText(Path.Combine(packageDir, "Microsoft.Azure.Azclips.Extensions.Compute.dll"), string.Empty);
            File.WriteAllText(Path.Combine(packageDir, "Microsoft.Azure.Azclips.Extensions.Compute.0.1.1.nupkg"), string.Empty);
            File.WriteAllText(Path.Combine(packageDir, "Microsoft.Azure.Azclips.Extensions.Compute.0.1.2.nupkg"), string.Empty);

            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var installed = loader.GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == "Compute" && x.Version == "0.1.2");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldReportAbsoluteDllLocation_ForBundledPackage()
    {
        // CLIPS#478: `az extension list` surfaces the on-disk `Location` of each discovered
        // extension. Verify the built-in loader emits the actual DLL path (not just the
        // package directory or a null placeholder).
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var packageDir = Path.Combine(root, packageId);
        var expectedDllPath = Path.Combine(packageDir, "Microsoft.Azure.Azclips.Extensions.Compute.dll");
        Directory.CreateDirectory(packageDir);

        try
        {
            File.WriteAllText(expectedDllPath, string.Empty);

            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var installed = Assert.Single(loader.GetInstalledExtensions());

            Assert.Equal("Compute", installed.Name);
            Assert.Equal(expectedDllPath, installed.Location);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // LoadExtension — versioned-load behavior (fix for Azure/CLIPS#522).
    //
    // Prior behavior: BuiltinExtensionLoader.LoadExtension short-circuited to null on any non-empty
    // version, forcing the caller to fall through to MultiVersionExtensionLoader — which only knows
    // about ~/.azclips/extensions/. That broke local Debug/Release dispatch because discovery
    // stamps a real version onto Bundled candidates (from the on-disk .nupkg), so dispatch requested
    // a versioned load the built-in loader had refused to serve.
    //
    // Fix: honor a versioned request when the on-disk .nupkg in the package folder reports the
    // matching version. Empty/mismatched/absent versions still route to the next loader.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LoadExtension_ShouldReturnAssembly_WhenVersionMatchesNupkg()
    {
        var (root, packageDir) = CreateFakePackage("Foo", "1.2.3", includeNupkg: true, includeRealDll: true);
        try
        {
            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var assembly = loader.LoadExtension("Foo", "1.2.3");

            Assert.NotNull(assembly);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnNull_WhenVersionDoesNotMatchNupkg()
    {
        var (root, packageDir) = CreateFakePackage("Foo", "1.2.3", includeNupkg: true, includeRealDll: true);
        try
        {
            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var assembly = loader.LoadExtension("Foo", "9.9.9");

            Assert.Null(assembly);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnNull_WhenVersionedButNoNupkg()
    {
        // Package folder has a DLL but no .nupkg (the packaged-install shape). A versioned request
        // cannot confirm identity here, so the loader must defer to the next one in the chain.
        var (root, packageDir) = CreateFakePackage("Foo", version: null, includeNupkg: false, includeRealDll: true);
        try
        {
            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var assembly = loader.LoadExtension("Foo", "1.2.3");

            Assert.Null(assembly);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnAssembly_WhenUnversionedRequest()
    {
        // Regression guard on the pre-existing unversioned load path.
        var (root, packageDir) = CreateFakePackage("Foo", version: null, includeNupkg: false, includeRealDll: true);
        try
        {
            var loader = new BuiltinExtensionLoader(new NullLoggingService(), new TestConfigurationService(root));

            var assembly = loader.LoadExtension("Foo");

            Assert.NotNull(assembly);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static (string Root, string PackageDir) CreateFakePackage(
        string shortName,
        string? version,
        bool includeNupkg,
        bool includeRealDll)
    {
        var root = Path.Combine(Path.GetTempPath(), $"builtin-loader-{Guid.NewGuid():N}");
        var packageId = $"Microsoft.Azure.Azclips.Extensions.{shortName}";
        var packageDir = Path.Combine(root, packageId);
        Directory.CreateDirectory(packageDir);

        var dllPath = Path.Combine(packageDir, $"{packageId}.dll");
        if (includeRealDll)
        {
            // Copy a real, load-able assembly so ExtensionLoadContext.LoadManagedAssembly succeeds.
            // The test assembly is the cheapest one that is guaranteed to exist at test time.
            var sourceAssembly = typeof(BuiltinExtensionLoaderTests).Assembly.Location;
            File.Copy(sourceAssembly, dllPath, overwrite: true);
        }
        else
        {
            File.WriteAllText(dllPath, string.Empty);
        }

        if (includeNupkg && !string.IsNullOrEmpty(version))
        {
            File.WriteAllText(
                Path.Combine(packageDir, $"{packageId}.{version}.nupkg"),
                string.Empty);
        }

        return (root, packageDir);
    }

    private static void SafeDelete(string root)
    {
        // Assembly load may hold a lock on the DLL until the ALC is collected. Best-effort delete.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (Directory.Exists(root))
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Non-fatal: temp cleanup will handle it eventually.
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
