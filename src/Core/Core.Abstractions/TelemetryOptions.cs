// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed class TelemetryOptions
{
    public bool Enabled { get; set; } = true;

    public string? ZipkinEndpoint { get; set; }

    public string? AppInsightsInstrumentationKey { get; set; }
}
