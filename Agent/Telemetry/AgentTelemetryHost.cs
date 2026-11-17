// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Telemetry;

namespace Core.Agent.Telemetry;

internal sealed class AgentTelemetryHost
{
    private readonly Lazy<Task<LayerTelemetryScope>> _scope;

    internal AgentTelemetryHost(
        TelemetryLayerScopeFactory factory, IConfigurationService configuration, DataBoundaryResolver resolver)
    {
        _scope = new Lazy<Task<LayerTelemetryScope>>(async () =>
        {
            var launch = await ResolveLaunchAsync(
                configuration.GetConfiguration<TelemetryOptions>().Enabled,
                Environment.GetEnvironmentVariable,
                enabled => CommandTelemetryContext.StandaloneAsync(enabled, resolver)).ConfigureAwait(false);
            if (launch.Diagnostic is { } diagnostic)
                Console.Error.WriteLine($"Agent telemetry: {diagnostic}");
            return factory.CreateScope("Agent", launch.Context, launch.FileId);
        });
    }

    internal LayerTelemetryScope Scope =>
        _scope.IsValueCreated && _scope.Value.IsCompletedSuccessfully
            ? _scope.Value.Result
            : throw new InvalidOperationException("Agent telemetry scope has not been initialized.");

    internal Task<LayerTelemetryScope> GetScopeAsync() => _scope.Value;

    internal static async Task<(TelemetryInvocationContext Context, string? FileId, string? Diagnostic)> ResolveLaunchAsync(
        bool enabled, Func<string, string?> read, Func<bool, Task<TelemetryInvocationContext>> standalone)
    {
        if (TelemetryLayerEnvironment.TryReadAgentLaunch(read, out var context, out var fileId, out var diagnostic))
            return (context! with { Enabled = enabled && context!.Enabled }, fileId, null);

        // Invalid launch metadata is not permission to undo a parent's opt-out. Standalone
        // routing only reads the existing profile/boundary cache; it never authenticates.
        var allowed = enabled && TelemetryLayerEnvironment.ParentAllowsTelemetry(read);
        return ((await standalone(allowed).ConfigureAwait(false)) with { Enabled = allowed }, null, diagnostic);
    }
}
