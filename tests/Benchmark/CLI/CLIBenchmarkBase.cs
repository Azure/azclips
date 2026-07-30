// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
[WarmupCount(0)]
[IterationCount(1)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class CLIBenchmarkBase : BenchmarkBase
{
    private const string PackageIdPrefix = "Microsoft.Azure.Azclips.Extensions.";

    /// <summary>
    /// Package ids of the Core built-in extensions, derived from <see cref="BuiltInExtensions.Map"/>.
    /// </summary>
    private static readonly HashSet<string> s_builtInPackageIds =
        new(BuiltInExtensions.Map.Values.Select(builtIn => builtIn.PackageId), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Short extension names (the segment after <see cref="PackageIdPrefix"/>) whose verbs this
    /// benchmark's CLI invocations need available. Defaults to the single extension named by the
    /// class's <c>[BenchmarkExtension]</c> attribute; override only in the rare case a benchmark
    /// needs additional extensions.
    /// </summary>
    /// <remarks>
    /// Each required extension is set up in one of two ways, based on <see cref="s_builtInPackageIds"/>:
    /// <list type="bullet">
    ///   <item><b>Installed</b> — copied to the benchmark extensions home, added to <c>registry.json</c>, and loaded by <c>MultiVersionExtensionLoader</c>.</item>
    ///   <item><b>Core built-in</b> — kept in <c>artifacts/Release</c> and loaded by <c>BuiltinExtensionLoader</c>; built-ins are not added to the registry.</item>
    /// </list>
    /// </remarks>
    private IReadOnlyList<string> RequiredExtensions => new[] { ExtensionName };

    private string _extensionsRoot = null!;
    private string _builtinInstallationDir = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();

        // Benchmark-scoped extensions home: a copy of the real ~/.azclips/extensions layout that the
        // resolver, reconciler, and MultiVersionExtensionLoader all agree on.
        _extensionsRoot = Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "extensions");
        TryDeleteDirectory(_extensionsRoot);
        Directory.CreateDirectory(_extensionsRoot);

        // Fallback path for Core built-in extensions: the Release output of the build
        var releaseRoot = Path.Combine(RepoRoot, "artifacts", "Release");
        var stagedCount = 0;
        var builtInCount = 0;

        foreach (var shortName in RequiredExtensions)
        {
            var packageId = PackageIdPrefix + shortName;
            var sourceBuildDir = Path.Combine(releaseRoot, packageId);

            // Core built-ins can't be registry-staged: ExtensionAddPlanner rejects a root leaf on
            // a built-in verb, mirroring `extension add`. Verify the payload is on disk and let
            // BuiltinExtensionLoader serve it from the Release output instead.
            if (s_builtInPackageIds.Contains(packageId))
            {
                EnsureBuiltInPayload(sourceBuildDir, packageId);
                builtInCount++;
                continue;
            }

            var version = ReadPackageVersion(sourceBuildDir, packageId);
            LocalExtensionStager.Stage(packageId, version, sourceBuildDir, _extensionsRoot);
            stagedCount++;
        }

        // Only redirect the built-in loader when a built-in is actually in play; otherwise keep the
        // staged home as the first candidate root so the installed-extension path is unchanged.
        _builtinInstallationDir = builtInCount > 0 ? releaseRoot : _extensionsRoot;

        Console.WriteLine(
            $"[Setup] CLI benchmarks ready (stub HTTP); staged {stagedCount} installed, {builtInCount} built-in extension(s)");
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
            services.AddSingleton<IExtensionLoader>(sp => new BuiltinExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(_builtinInstallationDir, _extensionsRoot)));
            services.AddSingleton<IExtensionLoader>(sp => new MultiVersionExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(_builtinInstallationDir, _extensionsRoot)));

            // Resolver reads the staged registry (the layer that fails today).
            services.RemoveAll<IExtensionRegistryStore>();
            services.AddSingleton<IExtensionRegistryStore>(_ =>
                new FileExtensionRegistryStore(Path.Combine(_extensionsRoot, "registry.json")));
        });
    }

    /// <summary>
    /// Fail fast if a Core built-in's Release output is missing.
    /// </summary>
    private static void EnsureBuiltInPayload(string sourceBuildDir, string packageId)
    {
        if (!Directory.Exists(sourceBuildDir))
        {
            throw new DirectoryNotFoundException(
                $"Built-in extension build output not found: '{sourceBuildDir}'. Run `dotnet build azclips.slnx -c Release`.");
        }

        // Matches what BuiltinExtensionLoader probes for; no .nupkg is needed because dispatch
        // resolves built-ins with a null version and the loader takes its unversioned branch.
        var assemblyPath = Path.Combine(sourceBuildDir, packageId + ".dll");
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Built-in extension assembly not found: '{assemblyPath}'. Run `dotnet build azclips.slnx -c Release`.");
        }
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
