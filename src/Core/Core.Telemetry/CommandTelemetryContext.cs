// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public sealed class CommandTelemetryContext(IConfigurationService configuration) : ICommandTelemetryContext
{
    private Activity? _root;

    public void BindCommand(Activity? root) => _root = root;

    public TelemetryInvocationContext Capture(string? command = null) =>
        TelemetryInvocationContext.Capture(
            _root, configuration.GetConfiguration<TelemetryOptions>().Enabled, command);

    public static TelemetryInvocationContext Standalone(bool enabled)
    {
        var tenant = DataBoundaryResolver.GetActiveTenantId();
        var boundary = tenant is null ? null : DataBoundaryCache.Get(tenant);
        return new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled,
            TenantId: tenant, DataBoundary: boundary);
    }
}
