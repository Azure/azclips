// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;

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
        services.AddSingleton<ITraceDirectoryProvider, DefaultTraceDirectoryProvider>();
        services.AddSingleton<ITelemetryPropertyFilter, DefaultTelemetryPropertyFilter>();
        services.AddSingleton<ITelemetryActivityNameFilter, DefaultTelemetryActivityNameFilter>();
        services.AddSingleton<BaseProcessor<Activity>, DefaultActivityNameFilterProcessor>();
        services.AddSingleton<BaseProcessor<Activity>, DefaultTelemetryPropertyFilterProcessor>();
        services.AddSingleton<ITelemetryRoutingContext, TelemetryRoutingContext>();
        return services;
    }
}
