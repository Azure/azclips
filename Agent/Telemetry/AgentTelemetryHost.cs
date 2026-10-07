// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Telemetry;

namespace Core.Agent.Telemetry;

internal sealed class AgentTelemetryHost
{
    private readonly Lazy<LayerTelemetryScope> _scope;

    internal AgentTelemetryHost(TelemetryLayerScopeFactory factory, IConfigurationService configuration)
    {
        _scope = new Lazy<LayerTelemetryScope>(() =>
        {
            var launch = ResolveLaunch(
                configuration.GetConfiguration<TelemetryOptions>().Enabled,
                Environment.GetEnvironmentVariable, TelemetryInvocationContext.StandaloneFromCache);
            if (launch.Diagnostic is { } diagnostic)
                Console.Error.WriteLine($"Agent telemetry: {diagnostic}");
            return factory.CreateScope("Agent", launch.Context);
        });
    }

    internal LayerTelemetryScope Scope => _scope.Value;

    internal static (TelemetryInvocationContext Context, string? Diagnostic) ResolveLaunch(
        bool enabled, Func<string, string?> read, Func<bool, TelemetryInvocationContext> standalone)
    {
        if (TelemetryLayerEnvironment.TryReadAgentLaunch(read, out var context, out var diagnostic))
            return (context! with { Enabled = enabled && context!.Enabled }, null);

        // Invalid launch metadata is not permission to undo a parent's opt-out. Standalone
        // routing only reads the existing profile/boundary cache; it never authenticates.
        var allowed = enabled && TelemetryLayerEnvironment.ParentAllowsTelemetry(read);
        return (standalone(allowed) with { Enabled = allowed }, diagnostic);
    }
}
