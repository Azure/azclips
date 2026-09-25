// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli;
using Core.Configuration.Options;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

public class BundledExtensionLoaderTests
{
    [Fact]
    public void GetInstalledExtensions_ShouldEnumerateBundledExtensions_UnderDefaultRoot()
    {
        var suffix = UniqueSuffix();
        var extName = $"Extension_{suffix}";
        var loginName = $"Login_{suffix}";
        var configName = $"Config_{suffix}";
        try
        {
            CreatePackage(extName, realDll: false);
            CreatePackage(loginName, realDll: false);
            CreatePackage(configName, realDll: false);

            var installed = CreateLoader().GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == extName && x.Version == string.Empty);
            Assert.Contains(installed, x => x.Name == loginName && x.Version == string.Empty);
            Assert.Contains(installed, x => x.Name == configName && x.Version == string.Empty);
        }
        finally
        {
            SafeDeletePackage(extName);
            SafeDeletePackage(loginName);
            SafeDeletePackage(configName);
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldEnumeratePackagesUnderRoot_NotInBuiltInMap()
    {
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, realDll: false);

            var installed = CreateLoader().GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == name && x.Version == string.Empty);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldUseHighestNupkgVersion_WhenMultipleAvailable()
    {
        var name = $"Compute_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, realDll: false, nupkgVersions: ["0.1.1", "0.1.2"]);

            var installed = CreateLoader().GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == name && x.Version == "0.1.2");
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldFallBackToAssemblyInformationalVersion_WhenNupkgAbsent()
    {
        // Packaged-install shape: DLL present, no .nupkg. Loader reads AssemblyInformationalVersion
        // via MetadataLoadContext instead of returning empty. Fixes Azure/CLIPS#534.
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);

            var installed = CreateLoader().GetInstalledExtensions().ToArray();

            var entry = Assert.Single(installed, x => x.Name == name);
            Assert.False(string.IsNullOrEmpty(entry.Version));
            Assert.DoesNotContain('+', entry.Version);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldReturnEmptyVersion_WhenNupkgAbsentAndDllUnreadable()
    {
        // Neither .nupkg nor a loadable DLL — fallback returns empty (defensive posture).
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, realDll: false);

            var installed = CreateLoader().GetInstalledExtensions().ToArray();

            Assert.Contains(installed, x => x.Name == name && x.Version == string.Empty);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void GetInstalledExtensions_ShouldReportAbsoluteDllLocation_ForBundledPackage()
    {
        // Location must be the absolute DLL path, not just the package directory or null.
        var name = $"Compute_{UniqueSuffix()}";
        try
        {
            var expectedDllPath = CreatePackage(name, realDll: false);

            var installed = Assert.Single(
                CreateLoader().GetInstalledExtensions(),
                x => x.Name == name);

            Assert.Equal(expectedDllPath, installed.Location);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // LoadExtension — versioned-load behavior (fix for Azure/CLIPS#522).
    //
    // Prior behavior: BundledExtensionLoader.LoadExtension short-circuited to null on any non-empty
    // version, forcing the caller to fall through to InstalledExtensionLoader — which only knows
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
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, nupkgVersions: ["1.2.3"]);

            var assembly = CreateLoader().LoadExtension(name, "1.2.3");

            Assert.NotNull(assembly);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnNull_WhenVersionDoesNotMatchNupkg()
    {
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, nupkgVersions: ["1.2.3"]);

            var assembly = CreateLoader().LoadExtension(name, "9.9.9");

            Assert.Null(assembly);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnNull_WhenVersionedButNoNupkg()
    {
        // Package folder has a DLL but no .nupkg (the packaged-install shape). A versioned request
        // cannot confirm identity here, so the loader must defer to the next one in the chain.
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);

            var assembly = CreateLoader().LoadExtension(name, "1.2.3");

            Assert.Null(assembly);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnAssembly_WhenUnversionedRequest()
    {
        // Regression guard on the pre-existing unversioned load path.
        var name = $"Foo_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);

            var assembly = CreateLoader().LoadExtension(name);

            Assert.NotNull(assembly);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // LoadExtension — process-wide assembly cache.
    //
    // The loader keeps a static ConcurrentDictionary of
    // extensionPath -> Lazy<(ExtensionLoadContext, Assembly)> so a Bundled extension is read off
    // disk and materialised into an AssemblyLoadContext at most once per process. Repeat loads must
    // hand back the very same Assembly instance — a second load would otherwise produce a distinct
    // Type identity for every extension type and re-pay the file read plus ALC construction cost.
    //
    // AssemblyLoadingOptions.BypassExtensionCache (the --no-cache flag / AZCLIPS_NO_EXTENSION_CACHE
    // env var) is the benchmark/test escape hatch and must force a cold load every time, never
    // reading from nor writing to the shared cache.
    //
    // Each test stages its package under a unique short name derived from a Guid, so the
    // process-wide cache — which is keyed by absolute path and intentionally has no reset hook —
    // is always cold for that key.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LoadExtension_ShouldReturnSameAssemblyInstance_OnRepeatedLoads()
    {
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);
            var loader = CreateLoader();

            var first = loader.LoadExtension(name);
            var second = loader.LoadExtension(name);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldServeFromCache_WithoutRereadingDisk()
    {
        // Strongest available proof that the second load never touches the file: corrupt the DLL
        // between calls. A cache hit is unaffected; a re-read would throw BadImageFormatException.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            var dllPath = CreatePackage(name);
            var loader = CreateLoader();

            var first = loader.LoadExtension(name);
            Corrupt(dllPath);
            var second = loader.LoadExtension(name);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldShareCache_AcrossLoaderInstances()
    {
        // The cache is static, not per-instance: a loader resolved from a second DI scope must not
        // re-load an extension another instance already materialised.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);

            var first = CreateLoader().LoadExtension(name);
            var second = CreateLoader().LoadExtension(name);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReuseIsolatedLoadContext_OnCacheHit()
    {
        // The cache stores the ExtensionLoadContext alongside the assembly precisely so the ALC
        // stays alive for later dependency resolution. Both loads must resolve to that same
        // isolated context — never the default one, which would defeat extension isolation.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);
            var loader = CreateLoader();

            var first = loader.LoadExtension(name);
            var second = loader.LoadExtension(name);

            var firstContext = AssemblyLoadContext.GetLoadContext(first!);
            var secondContext = AssemblyLoadContext.GetLoadContext(second!);

            Assert.NotNull(firstContext);
            Assert.Same(firstContext, secondContext);
            Assert.NotSame(AssemblyLoadContext.Default, firstContext);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldProduceIndependentCacheEntries_ForDifferentPackages()
    {
        // Two distinct packages must resolve to two distinct cache entries; a regression that
        // collapsed all entries onto a shared key would return the same assembly for both.
        var suffix = UniqueSuffix();
        var nameA = $"CacheProbeA_{suffix}";
        var nameB = $"CacheProbeB_{suffix}";
        try
        {
            CreatePackage(nameA);
            CreatePackage(nameB);
            var loader = CreateLoader();

            var fromA = loader.LoadExtension(nameA);
            var fromB = loader.LoadExtension(nameB);

            Assert.NotNull(fromA);
            Assert.NotNull(fromB);
            Assert.NotSame(fromA, fromB);
        }
        finally
        {
            SafeDeletePackage(nameA);
            SafeDeletePackage(nameB);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnCachedAssembly_ForVersionedAndUnversionedRequests()
    {
        // Both request shapes resolve to the same DLL path, so the versioned branch must land on
        // the same cache entry rather than loading a second copy.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name, nupkgVersions: ["1.2.3"]);
            var loader = CreateLoader();

            var unversioned = loader.LoadExtension(name);
            var versioned = loader.LoadExtension(name, "1.2.3");

            Assert.NotNull(unversioned);
            Assert.Same(unversioned, versioned);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public async Task LoadExtension_ShouldMaterializeOnce_WhenRequestedConcurrently()
    {
        // The Lazy<T> wrapper guarantees a single materialisation under contention; every racing
        // caller must observe the same assembly and the same load context.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);
            var loader = CreateLoader();

            var results = await Task.WhenAll(
                Enumerable.Range(0, 16).Select(_ => Task.Run(() => loader.LoadExtension(name))));

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
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldReturnDistinctAssemblies_WhenCacheBypassed()
    {
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);
            var loader = CreateLoader(bypassExtensionCache: true);

            var first = loader.LoadExtension(name);
            var second = loader.LoadExtension(name);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.NotSame(AssemblyLoadContext.GetLoadContext(first!), AssemblyLoadContext.GetLoadContext(second!));
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldRereadDisk_WhenCacheBypassed()
    {
        // Mirror of the cache-hit proof: with the bypass on, the second call must go back to the
        // file, so corrupting it surfaces as a load failure instead of a silent cache hit.
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            var dllPath = CreatePackage(name);
            var loader = CreateLoader(bypassExtensionCache: true);

            Assert.NotNull(loader.LoadExtension(name));
            Corrupt(dllPath);

            Assert.Throws<BadImageFormatException>(() => loader.LoadExtension(name));
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    [Fact]
    public void LoadExtension_ShouldNotPopulateSharedCache_WhenCacheBypassed()
    {
        // A bypassed load must leave the shared cache untouched, otherwise a single `--no-cache`
        // load would poison later cached loads (and the first cached load would be a hit, not a
        // fresh materialisation).
        var name = $"CacheProbe_{UniqueSuffix()}";
        try
        {
            CreatePackage(name);

            var bypassed = CreateLoader(bypassExtensionCache: true).LoadExtension(name);

            var cachingLoader = CreateLoader();
            var firstCached = cachingLoader.LoadExtension(name);
            var secondCached = cachingLoader.LoadExtension(name);

            Assert.NotNull(bypassed);
            Assert.NotNull(firstCached);
            Assert.NotSame(bypassed, firstCached);
            Assert.Same(firstCached, secondCached);
        }
        finally
        {
            SafeDeletePackage(name);
        }
    }

    private static BundledExtensionLoader CreateLoader(bool bypassExtensionCache = false)
        => new(new NullLoggingService(), new TestConfigurationService(bypassExtensionCache));

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Stages a Bundled package at
    /// <c>&lt;<see cref="BundledExtensionLoader.GetDefaultRoot"/>&gt;/Microsoft.Azure.Azclips.Extensions.&lt;shortName&gt;/</c>
    /// and returns the absolute path of its primary DLL. Tests pass a Guid-suffixed name so the
    /// staging path is unique per test, keeping the loader's process-wide cache cold.
    /// </summary>
    private static string CreatePackage(
        string shortName,
        bool realDll = true,
        params string[] nupkgVersions)
    {
        var packageId = $"Microsoft.Azure.Azclips.Extensions.{shortName}";
        var packageDir = Path.Combine(BundledExtensionLoader.GetDefaultRoot(), packageId);
        Directory.CreateDirectory(packageDir);

        var dllPath = Path.Combine(packageDir, $"{packageId}.dll");
        if (realDll)
        {
            File.Copy(typeof(BundledExtensionLoaderTests).Assembly.Location, dllPath, overwrite: true);
        }
        else
        {
            File.WriteAllText(dllPath, string.Empty);
        }

        foreach (var version in nupkgVersions)
        {
            File.WriteAllText(Path.Combine(packageDir, $"{packageId}.{version}.nupkg"), string.Empty);
        }

        return dllPath;
    }

    /// <summary>
    /// Replaces the staged DLL with non-PE content. Safe to do mid-test: the loader streams the
    /// file into memory and loads from that stream, so it never holds a handle on the file.
    /// </summary>
    private static void Corrupt(string dllPath)
        => File.WriteAllText(dllPath, "not a portable executable");

    /// <summary>
    /// Best-effort delete of the staging dir for a single package (not the shared root).
    /// </summary>
    private static void SafeDeletePackage(string shortName)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var packageDir = Path.Combine(
            BundledExtensionLoader.GetDefaultRoot(),
            $"Microsoft.Azure.Azclips.Extensions.{shortName}");

        // Non-fatal if it fails: leftover files at the bin/Debug dir are harmless, and the
        // extension ALC may still hold the DLL for the process lifetime.
        DirectoryUtils.TryDeleteDirectory(packageDir);
    }

    private sealed class TestConfigurationService(bool bypassExtensionCache = false) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new()
        {
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
