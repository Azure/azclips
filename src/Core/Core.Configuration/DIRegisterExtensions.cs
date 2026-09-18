// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Configuration;

public static class DIRegisterExtensions
{
    public static IServiceCollection RegisterConfigurationServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IConfigurationProvider, FileConfigurationProvider>()
            .AddSingleton<IConfigurationValidator, DefaultConfigurationValidator>()
            .AddSingleton<IConfigurationSchema, ConfigurationSchema>()
            .AddSingleton<ConfigurationService>()
            .AddSingleton<IConfigurationService>(services => services.GetRequiredService<ConfigurationService>())
            .AddSingleton<ISourceAwareConfigurationService>(services => services.GetRequiredService<ConfigurationService>());
    }
}
