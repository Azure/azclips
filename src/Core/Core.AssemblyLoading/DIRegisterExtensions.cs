// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.AssemblyLoading;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register assembly loading-related services.
    /// </summary>
    public static IServiceCollection RegisterAssemblyLoadingServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IProductVersionProvider, DefaultAssemblyVersionProvider>();
        services.AddSingleton<IAssemblyLoadingProvider, DefaultAssemblyLoadingProvider>();
        services.AddSingleton<IAssemblyLoadingService, AssemblyLoadingService>();
        return services;
    }
}