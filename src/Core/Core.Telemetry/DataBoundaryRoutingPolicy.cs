// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

internal enum RegionalDestination
{
    Eu,
    Row,
}

internal sealed record DataBoundaryRoutingPolicy
{
    internal static DataBoundaryRoutingPolicy Default { get; } = new()
    {
        TenantUnavailable = RegionalDestination.Row,
        AuthenticationUnavailable = RegionalDestination.Row,
        BoundaryUnavailable = RegionalDestination.Eu,
    };

    public required RegionalDestination TenantUnavailable { get; init; }
    public required RegionalDestination AuthenticationUnavailable { get; init; }
    public required RegionalDestination BoundaryUnavailable { get; init; }

    internal RegionalDestination Resolve(string? boundary, DataBoundaryOutcome? outcome)
    {
        if (DataBoundaryRouting.IsEu(boundary))
            return RegionalDestination.Eu;
        if (DataBoundaryRouting.IsConfirmedGlobal(boundary))
            return RegionalDestination.Row;

        return outcome switch
        {
            DataBoundaryOutcome.TenantUnavailable => TenantUnavailable,
            DataBoundaryOutcome.AuthenticationUnavailable => AuthenticationUnavailable,
            DataBoundaryOutcome.BoundaryUnavailable => BoundaryUnavailable,
            // Legacy records and invalid metadata do not establish a routing scenario.
            _ => RegionalDestination.Eu,
        };
    }
}
