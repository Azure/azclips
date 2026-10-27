// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class HttpActivityEnricherTests
{
    [Fact]
    public void Enrich_ShouldUseMsalClientRequestId()
    {
        using var activity = new Activity("http").Start();
        using var request = new HttpRequestMessage();
        const string requestId = "11111111-1111-1111-1111-111111111111";
        request.Headers.Add("client-request-id", requestId);

        HttpActivityEnricher.Enrich(activity, request);

        Assert.Equal("http", activity.GetTagItem(TelemetryTags.EventName));
        Assert.Equal(requestId, activity.GetTagItem(TelemetryTags.ClientRequestId));
    }

    [Fact]
    public void Enrich_ShouldPreferArmClientRequestId()
    {
        using var activity = new Activity("http").Start();
        using var request = new HttpRequestMessage();
        const string armRequestId = "11111111-1111-1111-1111-111111111111";
        const string msalRequestId = "22222222-2222-2222-2222-222222222222";
        request.Headers.Add("x-ms-client-request-id", armRequestId);
        request.Headers.Add("client-request-id", msalRequestId);

        HttpActivityEnricher.Enrich(activity, request);

        Assert.Equal(armRequestId, activity.GetTagItem(TelemetryTags.ClientRequestId));
    }

    [Fact]
    public void ShouldCollect_RequiresLiveConsentAndActiveInvocation()
    {
        using var invocation = new Activity("CLI.Activity.Root").Start();
        invocation.SetTag(TelemetryTags.ProductName, "CLI");

        Assert.True(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, invocation));
        Assert.False(HttpActivityEnricher.ShouldCollect(telemetryEnabled: false, invocation));
        Assert.False(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, current: null));
    }

    [Fact]
    public void ShouldCollect_RecognizesInvocationAncestor()
    {
        using var invocation = new Activity("PowerShell.Activity.Root").Start();
        invocation.SetTag(TelemetryTags.ProductName, "PowerShell");
        using var child = new Activity("ProfileService.GetActiveProfileAsync").Start();

        Assert.True(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, child));
    }

    [Fact]
    public void ExceptionSanitizer_DoesNotModifyUnrelatedHttpActivities()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("System.Net.Http");
        var previous = Activity.Current;
        Activity.Current = null;

        try
        {
            using var activity = source.StartActivity("System.Net.Http.HttpRequestOut");
            activity!.AddException(new InvalidOperationException("unrelated private detail"));

            var exceptionEvent = Assert.Single(activity.Events);
            Assert.Contains(
                exceptionEvent.Tags,
                tag => tag.Key == "exception.message"
                    && Equals(tag.Value, "unrelated private detail"));
        }
        finally
        {
            Activity.Current = previous;
        }
    }
}
