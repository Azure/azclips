// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Core.Runtime.Recovery;
using Core.Runtime.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Runtime;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register runtime-related services.
    /// </summary>
    public static IServiceCollection RegisterRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton<IPersistenceStore, FilePersistenceStore>();
        // Output abstraction - allows supporting both Console (CLI) and PowerShell hosts
        // Original design used Console.WriteLine directly; this abstraction enables
        // future PowerShell output formatting without changing engine logic
        services.AddSingleton<IOutputAdapter, ConsoleOutputAdapter>();
        services.AddSingleton<ILoggingService, ScopedLoggingService>();
        services.AddSingleton<IHttpTerminateHandler, DefaultHttpTerminateHandler>();
        services.AddSingleton<ITracingInterceptor, DefaultTracingInterceptor>();
        services.AddSingleton<IUserAgentHandler, DefaultUserAgentHandler>();
        services.AddSingleton<IClientRequestIdHandler, ClientRequestIdHandler>();
        services.AddSingleton<IHttpPipelineService, HttpPipelineService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IFailureHistoryStore>(provider =>
            new EncryptedFailureHistoryStore(
                provider.GetRequiredService<IFailureHistoryKeyStore>(), logger: provider.GetService<ILoggingService>()));
        services.AddKeyedSingleton<IConfigurationChangeHandler, RecoveryConfigurationChangeHandler>(
            RecoveryConfigurationChangeHandler.TriggerKey);
        return services;
    }
}