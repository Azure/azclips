// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli;
using Core.Cli.Parser;
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
}
