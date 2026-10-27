// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public static class HttpActivityEnricher
{
    private const string ArmClientRequestIdHeader = "x-ms-client-request-id";
    private const string MsalClientRequestIdHeader = "client-request-id";

    public static bool ShouldCollect(IConfigurationService configuration) =>
        ShouldCollect(configuration.GetConfiguration<TelemetryOptions>().Enabled, Activity.Current);

    internal static bool ShouldCollect(bool telemetryEnabled, Activity? current)
    {
        if (!telemetryEnabled)
            return false;

        for (; current is not null; current = current.Parent)
        {
            if (current.GetTagItem(TelemetryTags.ClientType) is not null)
                return true;
        }

        return false;
    }

    public static void Enrich(Activity activity, HttpRequestMessage request)
    {
        activity.SetTag(TelemetryTags.EventName, activity.OperationName);

        if (request.Headers.TryGetValues(ArmClientRequestIdHeader, out var values) ||
            request.Headers.TryGetValues(MsalClientRequestIdHeader, out values))
        {
            if (Guid.TryParse(values.FirstOrDefault(), out var clientRequestId))
            {
                activity.SetTag(TelemetryTags.ClientRequestId, clientRequestId.ToString());
            }
        }
    }
}
