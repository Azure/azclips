// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli.Parser;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

/// <summary>
/// Registers verb-to-package resolution as DI services so hosts (and tests/benchmarks) can supply a
/// scoped registry store instead of the hard-coded ~/.cirrus default.
/// </summary>
public static class CliExtensionServiceRegistration
{
    public static IServiceCollection AddCliExtensionDispatch(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionRegistryStore>(
            _ => new ReconcilingRegistryStore(new FileExtensionRegistryStore()));

        services.AddSingleton<IExtensionResolver>(
            sp => new ExtensionResolver(sp.GetRequiredService<IExtensionRegistryStore>()));

        services.AddSingleton(sp => new ExtensionDispatcher(
            sp.GetRequiredService<IExtensionResolver>(),
            () => LeafIndex.Build(
                BuiltInExtensions.Map,
                sp.GetRequiredService<IExtensionRegistryStore>().Load())));

        return services;
    }
}
