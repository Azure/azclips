// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

internal sealed record DataBoundaryRoutingPolicy
{
    internal static DataBoundaryRoutingPolicy Default { get; } = new()
    {
        TenantUnavailable = UploadRegion.ROW,
        AuthenticationUnavailable = UploadRegion.ROW,
        BoundaryUnavailable = UploadRegion.EU,
    };

    public required UploadRegion TenantUnavailable { get; init; }
    public required UploadRegion AuthenticationUnavailable { get; init; }
    public required UploadRegion BoundaryUnavailable { get; init; }

    internal UploadRegion Resolve(string? boundary, DataBoundaryOutcome? outcome)
    {
        if (DataBoundaryRouting.IsEu(boundary))
            return UploadRegion.EU;
        if (DataBoundaryRouting.IsConfirmedGlobal(boundary))
            return UploadRegion.ROW;

        return outcome switch
        {
            DataBoundaryOutcome.TenantUnavailable => TenantUnavailable,
            DataBoundaryOutcome.AuthenticationUnavailable => AuthenticationUnavailable,
            DataBoundaryOutcome.BoundaryUnavailable => BoundaryUnavailable,
            // Legacy records and invalid metadata do not establish a routing scenario.
            _ => UploadRegion.EU,
        };
    }
}
