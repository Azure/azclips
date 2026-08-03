// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;
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
        // Location must be the absolute DLL path, not just the package directory or null.
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

    [Fact]
    public void LoadExtension_ShouldReturnSameAssemblyInstance_OnRepeatedLoads()
    {
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root);

            var first = loader.LoadExtension("CacheProbe");
            var second = loader.LoadExtension("CacheProbe");

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldServeFromCache_WithoutRereadingDisk()
    {
        // Strongest available proof that the second load never touches the file: corrupt the DLL
        // between calls. A cache hit is unaffected; a re-read would throw BadImageFormatException.
        var root = CreateRoot();
        try
        {
            var dllPath = CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root);

            var first = loader.LoadExtension("CacheProbe");
            Corrupt(dllPath);
            var second = loader.LoadExtension("CacheProbe");

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldShareCache_AcrossLoaderInstances()
    {
        // The cache is static, not per-instance: a loader resolved from a second DI scope must not
        // re-load an extension another instance already materialised.
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");

            var first = CreateLoader(root).LoadExtension("CacheProbe");
            var second = CreateLoader(root).LoadExtension("CacheProbe");

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReuseIsolatedLoadContext_OnCacheHit()
    {
        // The cache stores the ExtensionLoadContext alongside the assembly precisely so the ALC
        // stays alive for later dependency resolution. Both loads must resolve to that same
        // isolated context — never the default one, which would defeat extension isolation.
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root);

            var first = loader.LoadExtension("CacheProbe");
            var second = loader.LoadExtension("CacheProbe");

            var firstContext = AssemblyLoadContext.GetLoadContext(first!);
            var secondContext = AssemblyLoadContext.GetLoadContext(second!);

            Assert.NotNull(firstContext);
            Assert.Same(firstContext, secondContext);
            Assert.NotSame(AssemblyLoadContext.Default, firstContext);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldKeyCacheByPath_NotByExtensionName()
    {
        // Same short name staged under two roots (the Debug/Release build-output shape) must yield
        // two independent cache entries. Keying by name would serve the wrong DLL.
        var root = CreateRoot();
        var rootA = Path.Combine(root, "a");
        var rootB = Path.Combine(root, "b");
        try
        {
            CreatePackage(rootA, "CacheProbe");
            CreatePackage(rootB, "CacheProbe");

            var fromA = CreateLoader(rootA).LoadExtension("CacheProbe");
            var fromB = CreateLoader(rootB).LoadExtension("CacheProbe");

            Assert.NotNull(fromA);
            Assert.NotNull(fromB);
            Assert.NotSame(fromA, fromB);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnCachedAssembly_ForVersionedAndUnversionedRequests()
    {
        // Both request shapes resolve to the same DLL path, so the versioned branch must land on
        // the same cache entry rather than loading a second copy.
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe", version: "1.2.3");
            var loader = CreateLoader(root);

            var unversioned = loader.LoadExtension("CacheProbe");
            var versioned = loader.LoadExtension("CacheProbe", "1.2.3");

            Assert.NotNull(unversioned);
            Assert.Same(unversioned, versioned);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public async Task LoadExtension_ShouldMaterializeOnce_WhenRequestedConcurrently()
    {
        // The Lazy<T> wrapper guarantees a single materialisation under contention; every racing
        // caller must observe the same assembly and the same load context.
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root);

            var results = await Task.WhenAll(
                Enumerable.Range(0, 16).Select(_ => Task.Run(() => loader.LoadExtension("CacheProbe"))));

            var expected = results[0];
            Assert.NotNull(expected);
            foreach (var result in results)
            {
                Assert.Same(expected, result);
                Assert.Same(AssemblyLoadContext.GetLoadContext(expected!), AssemblyLoadContext.GetLoadContext(result!));
            }
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnDistinctAssemblies_WhenCacheBypassed()
    {
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root, bypassExtensionCache: true);

            var first = loader.LoadExtension("CacheProbe");
            var second = loader.LoadExtension("CacheProbe");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.NotSame(AssemblyLoadContext.GetLoadContext(first!), AssemblyLoadContext.GetLoadContext(second!));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldRereadDisk_WhenCacheBypassed()
    {
        // Mirror of the cache-hit proof: with the bypass on, the second call must go back to the
        // file, so corrupting it surfaces as a load failure instead of a silent cache hit.
        var root = CreateRoot();
        try
        {
            var dllPath = CreatePackage(root, "CacheProbe");
            var loader = CreateLoader(root, bypassExtensionCache: true);

            Assert.NotNull(loader.LoadExtension("CacheProbe"));
            Corrupt(dllPath);

            Assert.Throws<BadImageFormatException>(() => loader.LoadExtension("CacheProbe"));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void LoadExtension_ShouldNotPopulateSharedCache_WhenCacheBypassed()
    {
        // A bypassed load must leave the shared cache untouched, otherwise a single `--no-cache`
        // load would poison later cached loads (and the first cached load would be a hit, not a
        // fresh materialisation).
        var root = CreateRoot();
        try
        {
            CreatePackage(root, "CacheProbe");

            var bypassed = CreateLoader(root, bypassExtensionCache: true).LoadExtension("CacheProbe");

            var cachingLoader = CreateLoader(root);
            var firstCached = cachingLoader.LoadExtension("CacheProbe");
            var secondCached = cachingLoader.LoadExtension("CacheProbe");

            Assert.NotNull(bypassed);
            Assert.NotNull(firstCached);
            Assert.NotSame(bypassed, firstCached);
            Assert.Same(firstCached, secondCached);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static BuiltinExtensionLoader CreateLoader(string root, bool bypassExtensionCache = false)
        => new(new NullLoggingService(), new TestConfigurationService(root, bypassExtensionCache));

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"az-ext-cache-{Guid.NewGuid():N}");

    /// <summary>
    /// Stages a loadable Bundled package under <paramref name="root"/> and returns the DLL path.
    /// The test assembly is copied in as the payload because it is the cheapest real assembly
    /// guaranteed to exist at test time — <c>LoadManagedAssembly</c> needs valid PE content.
    /// </summary>
    private static string CreatePackage(string root, string shortName, string? version = null)
    {
        var packageId = $"Microsoft.Azure.Azclips.Extensions.{shortName}";
        var packageDir = Path.Combine(root, packageId);
        Directory.CreateDirectory(packageDir);

        var dllPath = Path.Combine(packageDir, $"{packageId}.dll");
        File.Copy(typeof(BuiltinExtensionLoaderTests).Assembly.Location, dllPath, overwrite: true);

        if (!string.IsNullOrEmpty(version))
        {
            File.WriteAllText(Path.Combine(packageDir, $"{packageId}.{version}.nupkg"), string.Empty);
        }

        return dllPath;
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

    /// <summary>
    /// Replaces the staged DLL with non-PE content. Safe to do mid-test: the loader streams the
    /// file into memory and loads from that stream, so it never holds a handle on the file.
    /// </summary>
    private static void Corrupt(string dllPath)
        => File.WriteAllText(dllPath, "not a portable executable");

    private sealed class TestConfigurationService(string installationRoot, bool bypassExtensionCache = false) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new()
        {
            BuiltinInstallationDir = installationRoot,
            VersionedInstallationDir = installationRoot,
            BypassExtensionCache = bypassExtensionCache
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
