// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public static class HttpActivityEnricher
{
    private const string ArmClientRequestIdHeader = "x-ms-client-request-id";
    private const string MsalClientRequestIdHeader = "client-request-id";
    private static readonly ActivityListener s_exceptionSanitizer = CreateExceptionSanitizer();

    static HttpActivityEnricher() => _ = s_exceptionSanitizer;

    public static bool ShouldCollect(IConfigurationService configuration) =>
        ShouldCollect(configuration.GetConfiguration<TelemetryOptions>().Enabled, Activity.Current);

    internal static bool ShouldCollect(bool telemetryEnabled, Activity? current)
    {
        if (!telemetryEnabled)
            return false;

        return HasAzclipsInvocation(current);
    }

    private static bool HasAzclipsInvocation(Activity? current)
    {
        for (; current is not null; current = current.Parent)
        {
            if (current.GetTagItem(TelemetryTags.ProductName) is not null)
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

    private static ActivityListener CreateExceptionSanitizer()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "System.Net.Http",
            ExceptionRecorder = static (
                Activity activity,
                Exception exception,
                ref TagList tags) =>
            {
                if (!HasAzclipsInvocation(activity))
                    return;

                // .NET 10 records HTTP exceptions in the runtime before OpenTelemetry's
                // RecordException option is considered. Supplying these keys prevents
                // Activity.AddException from filling them with private error text.
                tags.Add("exception.message", null);
                tags.Add("exception.stacktrace", null);
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
