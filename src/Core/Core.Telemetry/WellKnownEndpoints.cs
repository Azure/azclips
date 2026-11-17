// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Telemetry;

public static class WellKnownEndpoints
{
    private const string AriaDevelopmentConnectionString =
        "InstrumentationKey=ed2a860010bd41fd9879264d51e4e5c8-eb93fa08-79a9-48c9-b84b-9a55a6d403af-7285";

    public static string AriaConnectionString { get; } = ResolveAriaConnectionString();

    /// <summary>
    /// EU collector, set through TransportOptions.Endpoint because OneCollector ignores
    /// IngestionEndpoint in connection strings.
    /// </summary>
    public static readonly Uri AriaEuEndpoint =
        new("https://eu-mobile.events.data.microsoft.com/OneCollector/1.0/");

    /// <summary>
    /// Explicit US/ROW collector; OneCollector's default collector may route through EU.
    /// Set through TransportOptions.Endpoint, not the connection string.
    /// </summary>
    public static readonly Uri AriaRowEndpoint =
        new("https://us-mobile.events.data.microsoft.com/OneCollector/1.0/");

    /// <summary>
    /// Default local OTLP/gRPC endpoint for the Jaeger all-in-one dev stack
    /// (tools/observability/docker-compose.yml). Used when the resolved telemetry
    /// target is Local.
    /// </summary>
    public const string LocalOtlp = "http://localhost:4317";

    private static string ResolveAriaConnectionString()
    {
        var value = typeof(WellKnownEndpoints).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "AriaConnectionString")
            ?.Value;

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : AriaDevelopmentConnectionString;
    }
}
