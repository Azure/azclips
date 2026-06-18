// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Core.Abstractions;
using Core.Cli;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Perfolizer.Mathematics.OutlierDetection;
using Tests.Common.Utilities;

namespace Benchmark.CLI;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
// [MaxRelativeError(0.01)]
// [MaxIterationCount(1)]
[Outliers(OutlierMode.RemoveAll)]
[InProcess]
public abstract class CLIBenchmarkBase : BenchmarkBase
{
    private const string PackageIdPrefix = "Microsoft.Azure.Cirrus.Extensions.";

    /// <summary>
    /// Short extension names (the segment after <see cref="PackageIdPrefix"/>) whose verbs this
    /// benchmark's CLI invocations need staged. Override per benchmark class; empty stages nothing.
    /// </summary>
    protected virtual IReadOnlyList<string> RequiredExtensions => Array.Empty<string>();

    private string _extensionsRoot = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();

        // Benchmark-scoped extensions home: a copy of the real ~/.cirrus/extensions layout that the
        // resolver, reconciler, and MultiVersionExtensionLoader all agree on.
        _extensionsRoot = Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "extensions");
        TryDeleteDirectory(_extensionsRoot);
        Directory.CreateDirectory(_extensionsRoot);

        foreach (var shortName in RequiredExtensions)
        {
            var packageId = PackageIdPrefix + shortName;
            var sourceBuildDir = Path.Combine(RepoRoot, "artifacts", "Release", packageId);
            var version = ReadPackageVersion(sourceBuildDir, packageId);
            LocalExtensionStager.Stage(packageId, version, sourceBuildDir, _extensionsRoot);
        }

        Console.WriteLine($"[Setup] CLI benchmarks ready (stub HTTP); staged {RequiredExtensions.Count} extension(s)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
        RegisterExtensionResolution();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        // Best-effort: MultiVersionExtensionLoader caches the extension ALC for the process
        // lifetime, so the staged DLL may still be locked here. A leftover home is harmless.
        TryDeleteDirectory(Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"));
    }

    private void RegisterExtensionResolution()
    {
        var extensionsRoot = _extensionsRoot;

        CirrusServiceProvider.OverrideRegistration(services =>
        {
            // Loaders read the staged versioned payload (<root>/<pkg>/<ver>/<dll>).
            services.RemoveAll<IExtensionLoader>();
            services.AddSingleton<IExtensionLoader>(sp => new BuiltinExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(extensionsRoot)));
            services.AddSingleton<IExtensionLoader>(sp => new MultiVersionExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(extensionsRoot)));

            // Resolver reads the staged registry (the layer that fails today).
            services.RemoveAll<IExtensionRegistryStore>();
            services.AddSingleton<IExtensionRegistryStore>(_ => new ReconcilingRegistryStore(
                new FileExtensionRegistryStore(Path.Combine(extensionsRoot, "registry.json"))));
        });
    }

    // GeneratePackageOnBuild emits <packageId>.<version>.nupkg next to the dll. Parse the version
    // off the known package-id prefix (the id itself contains dots, so split-on-dot is wrong).
    private static string ReadPackageVersion(string sourceBuildDir, string packageId)
    {
        if (!Directory.Exists(sourceBuildDir))
        {
            throw new DirectoryNotFoundException(
                $"Extension build output not found: '{sourceBuildDir}'. Run `dotnet build cirrus.slnx -c Release`.");
        }

        var prefix = packageId + ".";
        foreach (var nupkg in Directory.EnumerateFiles(sourceBuildDir, packageId + ".*.nupkg"))
        {
            var name = Path.GetFileNameWithoutExtension(nupkg); // file name is "<packageId>.<version>"
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return name[prefix.Length..];
            }
        }
        throw new FileNotFoundException(
            $"No '{packageId}.*.nupkg' found in '{sourceBuildDir}'. Run `dotnet build cirrus.slnx -c Release`.");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}
