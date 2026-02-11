using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Authentication;
using Core.Implementation;
using Core.Runtime;
using Core.Runtime.Handler;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Extension methods for registering Cirrus services into an <see cref="IServiceCollection"/>.
/// Shared logic lives in <see cref="AddCirrusCore"/>; host-specific registrations in
/// <see cref="AddCirrusCli"/> and <see cref="AddCirrusPS"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers platform-agnostic core services (singletons) and per-invocation scoped services.
    /// Both CLI and PS hosts call this, then add their own host-specific registrations.
    /// </summary>
    public static IServiceCollection AddCirrusCore(this IServiceCollection services)
    {
        // --- Singletons: shared across all invocations ---
        services.AddSingleton<ILoggingService, ConsoleLoggingService>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<IAuthContextService, AuthContextService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();

        // Agent engine - handles all input via AI (Azure OpenAI)
        services.AddSingleton<AgentEngine>(sp =>
        {
            var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            return new AgentEngine(
                sp.GetRequiredService<IOutputAdapter>(),
                flavor);
        });

        // --- Scoped: new instance per command invocation ---

        // Auth handler: snapshot of current auth context at invocation time
        services.AddScoped<ICommandAuthHandler>(sp =>
        {
            var authContextService = sp.GetRequiredService<IAuthContextService>();
            var authService = sp.GetRequiredService<IAuthService>();
            AuthContext authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()
                ?? new AuthContext { SubscriptionId = "", TenantId = "" };

            return new CommandAuthHandler
            {
                AuthContext = new AuthContext
                {
                    SubscriptionId = authContext.SubscriptionId,
                    TenantId = authContext.TenantId
                },
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };
        });

        // Telemetry handler: wraps singleton ITelemetryService.StartActivity
        services.AddScoped<ICommandTelemetryHandler>(sp =>
        {
            var telemetryService = sp.GetRequiredService<ITelemetryService>();
            return new CommandTelemetryHandler
            {
                StartActivity = telemetryService.StartActivity
            };
        });

        // HTTP pipeline: created per invocation with auth handler prepended
        services.AddScoped<IRuntimeHttpPipeline>(sp =>
        {
            var authHandler = sp.GetRequiredService<ICommandAuthHandler>();
            IRuntimeHttpPipeline pipeline = new HttpPipeline(new HttpClientFactory());
            return pipeline.Prepend(authHandler.SendAsync);
        });

        return services;
    }

    /// <summary>
    /// Registers CLI-specific services. Call after <see cref="AddCirrusCore"/>.
    /// </summary>
    public static IServiceCollection AddCirrusCli(this IServiceCollection services)
    {
        // CLI writes to Console (used by AgentEngine)
        services.AddSingleton<IOutputAdapter, ConsoleOutputAdapter>();

        // ICommandOutputHandler is NOT registered in DI.
        // Each engine creates it per invocation wrapping the caller's output delegate,
        // then passes it as an extra parameter to ActivatorUtilities.CreateInstance.

        return services;
    }

    /// <summary>
    /// Registers PowerShell-specific services. Call after <see cref="AddCirrusCore"/>.
    /// <para>
    /// <b>ICommandOutputHandler is NOT registered here.</b> PS output must route to the
    /// calling cmdlet's $PSCmdlet.WriteObject, which changes per invocation. PSEngine
    /// creates it per invocation and passes it as an extra constructor parameter via
    /// ActivatorUtilities.CreateInstance.
    /// </para>
    /// </summary>
    public static IServiceCollection AddCirrusPS(this IServiceCollection services)
    {
        // PS-specific registrations go here.
        // ICommandOutputHandler is passed as an extra ctor param by PSEngine per invocation.

        return services;
    }
}
