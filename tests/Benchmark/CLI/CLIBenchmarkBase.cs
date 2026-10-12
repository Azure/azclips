// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Benchmark.Telemetry;
using BenchmarkDotNet.Attributes;
using Core.Abstractions;
using Core.Cli;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Perfolizer.Mathematics.OutlierDetection;
using Tests.Common.Utilities;

namespace Benchmark.CLI;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
[WarmupCount(5000)]
[IterationCount(5000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class CLIBenchmarkBase : BenchmarkBase
{
    private static readonly ClientMetadata s_clientMetadata = new(
        ClientType.CLI,
        "04b07795-8ddb-461a-bbee-02f9e1bf7b46");

    private const string PackageIdPrefix = "Microsoft.Azure.Azclips.Extensions.";

    /// <summary>
    /// Short extension names (the segment after <see cref="PackageIdPrefix"/>) whose verbs this
    /// benchmark's CLI invocations need available. Defaults to the single extension named by the
    /// class's <c>[BenchmarkExtension]</c> attribute; override only in the rare case a benchmark
    /// needs additional extensions.
    /// </summary>
    /// <remarks>
    /// Every required extension is staged into the benchmark extensions home via
    /// <see cref="LocalExtensionStager"/> and loaded by <c>InstalledExtensionLoader</c>. Post-#622
    /// there is no Core/built-in distinction — dispatch is uniform across all verbs.
    /// </remarks>
    private IReadOnlyList<string> RequiredExtensions => new[] { ExtensionName };

    private string _extensionsRoot = null!;
    private StageTraceDirectoryProvider _traceDirectory = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon(s_clientMetadata);

        // Benchmark-scoped extensions home: a copy of the real ~/.azclips/extensions layout that the
        // resolver, reconciler, and InstalledExtensionLoader all agree on.
        _extensionsRoot = Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "extensions");
        DirectoryUtils.TryDeleteDirectory(_extensionsRoot);
        Directory.CreateDirectory(_extensionsRoot);

        // Fallback path for source build output.
        var releaseRoot = Path.Combine(RepoRoot, "artifacts", "Release");
        var stagedCount = 0;

        foreach (var shortName in RequiredExtensions)
        {
            var packageId = PackageIdPrefix + shortName;
            var sourceBuildDir = Path.Combine(releaseRoot, packageId);

            var version = ReadPackageVersion(sourceBuildDir, packageId);
            LocalExtensionStager.Stage(packageId, version, sourceBuildDir, _extensionsRoot);
            stagedCount++;
        }

        VerifyAuthFixture();

        Console.WriteLine(
            $"[Setup] CLI benchmarks ready (stub HTTP, real MSAL cache IO); staged {stagedCount} extension(s)");

        InitializeTelemetryCommon();

        _traceDirectory = new StageTraceDirectoryProvider(
            TelemetryArtifacts.WarmupDirectory,
            TelemetryArtifacts.ActualDirectory,
            Stage);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        SeedProfileStore();
        RegisterBenchmarkStubs();
        RegisterExtensionResolution();
        RegisterTelemetryCapture();
    }

    /// <summary>
    /// Registers the production <see cref="TelemetryService"/> and this benchmark's trace
    /// directories. Runs after <see cref="BenchmarkBase.RegisterBenchmarkStubs"/>, so DI last-wins
    /// semantics take the production service over <c>StubTelemetryService</c>.
    /// </summary>
    /// <remarks>
    /// Repeated every iteration because <c>[IterationCleanup]</c> disposes the container; the
    /// registered instance is the same throughout.
    /// </remarks>
    private void RegisterTelemetryCapture()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.AddSingleton<ITraceDirectoryProvider>(_traceDirectory);
        });
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupTelemetryCommon();
        CleanupGlobal();

        // Best-effort: InstalledExtensionLoader caches the extension ALC for the process
        // lifetime, so the staged DLL may still be locked here. A leftover home is harmless.
        DirectoryUtils.TryDeleteDirectory(
            Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"));
    }

    /// <summary>
    /// Runs the CLI in-process with <paramref name="args"/> and throws if it returns a
    /// non-zero exit code. Returns the (zero) exit code so a benchmark method can return it.
    /// </summary>
    protected static int RunCli(params string[] args)
    {
        var exitCode = Core.Cli.Program.Main(args).GetAwaiter().GetResult();
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"CLI exited with code {exitCode}");
        }
        return exitCode;
    }

    private void RegisterExtensionResolution()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            // Loaders read the staged versioned payload (<root>/<pkg>/<ver>/<dll>).
            services.RemoveAll<IExtensionLoader>();
            services.AddSingleton<IExtensionLoader>(sp => new BundledExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(_extensionsRoot)));
            services.AddSingleton<IExtensionLoader>(sp => new InstalledExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(_extensionsRoot)));

            // Resolver reads the staged registry (the layer that fails today).
            services.RemoveAll<IExtensionRegistryStore>();
            services.AddSingleton<IExtensionRegistryStore>(_ =>
                new FileExtensionRegistryStore(Path.Combine(_extensionsRoot, "registry.json")));
        });
    }

    // GeneratePackageOnBuild emits <packageId>.<version>.nupkg next to the dll. Parse the version
    // off the known package-id prefix (the id itself contains dots, so split-on-dot is wrong).
    private static string ReadPackageVersion(string sourceBuildDir, string packageId)    {
        if (!Directory.Exists(sourceBuildDir))
        {
            throw new DirectoryNotFoundException(
                $"Extension build output not found: '{sourceBuildDir}'. Run `dotnet build azclips.slnx -c Release`.");
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
            $"No '{packageId}.*.nupkg' found in '{sourceBuildDir}'. Run `dotnet build azclips.slnx -c Release`.");
    }

}
