// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli;
using Core.Cli.Parser;
using Core.Abstractions;
using Core.Configuration.Options;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Tests;

public sealed class CliExtensionServiceRegistrationTests
{
    private sealed class FakeStore : IExtensionRegistryStore
    {
        private readonly ExtensionRegistry _registry;
        public FakeStore(ExtensionRegistry registry) => _registry = registry;
        public ExtensionRegistry Load() => _registry;
        public void Save(ExtensionRegistry registry) { }
    }

    private sealed class StubConfigurationService(string installedRoot) : IConfigurationService
    {
        private readonly AssemblyLoadingOptions _options = new() { InstalledExtensionsRoot = installedRoot };

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
            => typeof(T) == typeof(AssemblyLoadingOptions)
                ? (T)(object)_options
                : defaultValue ?? new T();

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

    [Fact]
    public void AddCliExtensionDispatch_RegistersRegistryStore_AtConfigDerivedPath()
    {
        // Guards the one seat of truth for registry-path resolution: the DI factory in
        // CliExtensionServiceRegistration. A regression here would silently re-detach
        // the registry from assemblyLoading.installedExtensionsRoot (CLIPS-629).
        var customRoot = Path.Combine(Path.GetTempPath(), $"clips-629-{Guid.NewGuid():N}");

        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationService>(new StubConfigurationService(customRoot));
        services.AddCliExtensionDispatch();
        using var sp = services.BuildServiceProvider();

        var store = Assert.IsType<FileExtensionRegistryStore>(sp.GetRequiredService<IExtensionRegistryStore>());
        Assert.Equal(Path.Combine(customRoot, HomePaths.RegistryFileName), store.Path);
    }

    [Fact]
    public void Dispatcher_ResolvesVerb_FromInjectedRegistryStore()
    {
        var registry = new ExtensionRegistry();
        registry.Extensions["Microsoft.Azure.Azclips.Extensions.Widget"] = new ExtensionRegistryEntry
        {
            Verb = "widget",
            Version = "1.0.0",
            Contributions = { new Contribution { Verb = "widget", Path = "" } },
        };

        var services = new ServiceCollection();
        services.AddCliExtensionDispatch();
        // Last registration wins — mirrors the benchmark's override of the default file store.
        services.AddSingleton<IExtensionRegistryStore>(_ => new FakeStore(registry));

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();

        var invocation = dispatcher.Parse(new[] { "widget", "list" });

        // Resolution succeeded (no ExtensionNotInstalledException); package id stripped to short name.
        Assert.Equal("Widget", invocation.ModuleName);
    }

    [Fact]
    public void BuildContributionProjection_DerivesLeaves_FromDiscoveredCommands()
    {
        var metadata = new CommandMetadata(
            Name: "create",
            InvocationName: "vm create",
            Aliases: [],
            FullName: "vm create",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(object),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        var info = new DiscoveredExtensionInfo(
            PackageId: "Microsoft.Azure.Azclips.Extensions.Compute",
            ResolvedVersion: "1.0.0",
            Verbs: ["vm"],
            Commands: new Dictionary<string, CommandMetadata>
            {
                ["vm create"] = metadata,
                ["vm delete"] = metadata with { Name = "delete", InvocationName = "vm delete", FullName = "vm delete" },
            },
            Location: null);

        var projection = CliExtensionServiceRegistration.BuildContributionProjection(info);

        Assert.Contains(projection.Contributions, c => c.Verb == "vm" && c.Path == "create");
        Assert.Contains(projection.Contributions, c => c.Verb == "vm" && c.Path == "delete");
        Assert.DoesNotContain(projection.Contributions, c => c.Verb == "vm" && c.Path == string.Empty);
        Assert.Equal(new[] { "vm" }, projection.Namespaces);
    }

    [Fact]
    public void BuildContributionProjection_FallsBackToVerbRoots_WhenNoCommandsPresent()
    {
        var info = new DiscoveredExtensionInfo(
            PackageId: "Contoso.Foo",
            ResolvedVersion: "1.0.0",
            Verbs: ["foo"],
            Commands: new Dictionary<string, CommandMetadata>(),
            Location: null);

        var projection = CliExtensionServiceRegistration.BuildContributionProjection(info);

        var single = Assert.Single(projection.Contributions);
        Assert.Equal("foo", single.Verb);
        Assert.Equal(string.Empty, single.Path);
        Assert.Equal(new[] { "foo" }, projection.Namespaces);
    }

    [Fact]
    public void DiscoveryProjection_ShouldHardFail_WhenDifferentPackagesOwnSameLeaf()
    {
        var metadata = new CommandMetadata(
            Name: "create",
            InvocationName: "vm create",
            Aliases: [],
            FullName: "vm create",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(object),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        var discovered = new[]
        {
            new DiscoveredExtensionInfo(
                PackageId: "Microsoft.Azure.Azclips.Extensions.Compute",
                ResolvedVersion: "0.1.2",
                Verbs: ["vm"],
                Commands: new Dictionary<string, CommandMetadata>
                {
                    ["vm create"] = metadata,
                },
                Location: null),
            new DiscoveredExtensionInfo(
                PackageId: "Contoso.Azure.Azclips.Extensions.ComputeFork",
                ResolvedVersion: "1.0.0",
                Verbs: ["vm"],
                Commands: new Dictionary<string, CommandMetadata>
                {
                    ["vm create"] = metadata with
                    {
                        AssemblyName = "Contoso.Azure.Azclips.Extensions.ComputeFork",
                        FullName = "Contoso vm create",
                    },
                },
                Location: null),
        };

        var registry = new ExtensionRegistry();
        foreach (var info in discovered)
        {
            var projection = CliExtensionServiceRegistration.BuildContributionProjection(info);
            registry.Extensions[info.PackageId] = new ExtensionRegistryEntry
            {
                Version = info.ResolvedVersion,
                InstalledAt = DateTimeOffset.UtcNow,
                Contributions = projection.Contributions,
                Namespaces = projection.Namespaces,
            };
        }

        var ex = Assert.Throws<InvalidOperationException>(() => LeafIndex.Build(registry));
        Assert.Contains("vm", ex.Message, StringComparison.Ordinal);
        Assert.Contains("create", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Azure.Azclips.Extensions.Compute", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Contoso.Azure.Azclips.Extensions.ComputeFork", ex.Message, StringComparison.Ordinal);
    }
}
