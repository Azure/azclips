// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

public static class WellKnownEndpoints
{
    public const string AzureMonitorConnectionString =
        "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    public const string AriaConnectionString =
        "InstrumentationKey=1d28a0ed7ba8432a9cc5a3d5ba1dfcf3-1161b197-eef8-44f0-9805-70ed20d0d5cb-7060";

    public const string AriaEuConnectionString =
        "InstrumentationKey=1d28a0ed7ba8432a9cc5a3d5ba1dfcf3-1161b197-eef8-44f0-9805-70ed20d0d5cb-7060;IngestionEndpoint=https://eu-self.events.data.microsoft.com";

    /// <summary>
    /// Default local OTLP/gRPC endpoint for the Jaeger all-in-one dev stack
    /// (tools/observability/docker-compose.yml). Used when the resolved telemetry
    /// target is Local.
    /// </summary>
    public const string LocalOtlp = "http://localhost:4317";
}
