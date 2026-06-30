// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli;
using Core.Cli.Parser;
using Core.Abstractions;
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

    [Fact]
    public void Dispatcher_ResolvesVerb_FromInjectedRegistryStore()
    {
        var registry = new ExtensionRegistry();
        registry.Extensions["Microsoft.Azure.Cirrus.Extensions.Widget"] = new ExtensionRegistryEntry
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
            });

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
            Commands: new Dictionary<string, CommandMetadata>());

        var projection = CliExtensionServiceRegistration.BuildContributionProjection(info);

        var single = Assert.Single(projection.Contributions);
        Assert.Equal("foo", single.Verb);
        Assert.Equal(string.Empty, single.Path);
        Assert.Equal(new[] { "foo" }, projection.Namespaces);
    }
}
