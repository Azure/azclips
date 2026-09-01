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
        "InstrumentationKey=ed2a860010bd41fd9879264d51e4e5c8-eb93fa08-79a9-48c9-b84b-9a55a6d403af-7285;IngestionEndpoint=https://eu-self.events.data.microsoft.com";

    /// <summary>
    /// Default local OTLP/gRPC endpoint for the Jaeger all-in-one dev stack
    /// (tools/observability/docker-compose.yml). Used when the resolved telemetry
    /// target is Local.
    /// </summary>
    public const string LocalOtlp = "http://localhost:4317";
}
