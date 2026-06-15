// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register telemetry-related services.
    /// </summary>
    public static IServiceCollection RegisterTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryProvider, DefaultTelemetryProvider>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        return services;
    }
}
