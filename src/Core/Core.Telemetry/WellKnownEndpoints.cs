// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

public static class WellKnownEndpoints
{
    public const string AzureMonitorConnectionString =
        "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    public const string AriaConnectionString =
        "InstrumentationKey=ed2a860010bd41fd9879264d51e4e5c8-eb93fa08-79a9-48c9-b84b-9a55a6d403af-7285";

    public const string AriaEuConnectionString =
        "InstrumentationKey=ed2a860010bd41fd9879264d51e4e5c8-eb93fa08-79a9-48c9-b84b-9a55a6d403af-7285";

    /// <summary>
    /// EU-compliant OneCollector ingestion endpoint per the EU Data Boundary spec.
    /// OneCollector ignores IngestionEndpoint in connection strings, so the endpoint
    /// must be set explicitly via <c>TransportOptions.Endpoint</c>.
    /// </summary>
    public static readonly Uri AriaEuEndpoint =
        new("https://eu-mobile.events.data.microsoft.com/OneCollector/1.0/");

    /// <summary>
    /// Regional ("Rest of World", i.e. non-EU) OneCollector ingestion endpoint. Used instead
    /// of relying on OneCollector's default/global endpoint, because the global Collector can
    /// route traffic to any regional Collector/AEF instance — including the EU one — which can
    /// cause Geneva Delivery Service (GDS) to misroute non-EU data into the EU Kusto cluster.
    /// Pointing explicitly at this US/ROW regional endpoint avoids that ambiguity, the same way
    /// <see cref="AriaEuEndpoint"/> already does for EU. OneCollector ignores IngestionEndpoint
    /// in connection strings, so this must be set explicitly via <c>TransportOptions.Endpoint</c>.
    /// </summary>
    public static readonly Uri AriaRowEndpoint =
        new("https://us-mobile.events.data.microsoft.com/OneCollector/1.0/");

    /// <summary>
    /// Default local OTLP/gRPC endpoint for the Jaeger all-in-one dev stack
    /// (tools/observability/docker-compose.yml). Used when the resolved telemetry
    /// target is Local.
    /// </summary>
    public const string LocalOtlp = "http://localhost:4317";
}
