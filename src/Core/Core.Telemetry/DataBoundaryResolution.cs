// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

internal enum DataBoundaryOutcome
{
    Confirmed,
    TenantUnavailable,
    AuthenticationUnavailable,
    BoundaryUnavailable,
}

internal sealed record DataBoundaryResolution(string? Boundary, DataBoundaryOutcome Outcome);
