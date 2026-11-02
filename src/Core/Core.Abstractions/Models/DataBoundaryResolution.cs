// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public enum DataBoundaryOutcome
{
    Confirmed,
    TenantUnavailable,
    AuthenticationUnavailable,
    BoundaryUnavailable,
}

public sealed record DataBoundaryResolution(string? Boundary, DataBoundaryOutcome Outcome);
