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
        request.Headers.Add("client-request-id", "msal-request-id");

        HttpActivityEnricher.Enrich(activity, request);

        Assert.Equal("http", activity.GetTagItem(TelemetryTags.EventName));
        Assert.Equal("msal-request-id", activity.GetTagItem(TelemetryTags.ClientRequestId));
    }

    [Fact]
    public void Enrich_ShouldPreferArmClientRequestId()
    {
        using var activity = new Activity("http").Start();
        using var request = new HttpRequestMessage();
        request.Headers.Add("x-ms-client-request-id", "arm-request-id");
        request.Headers.Add("client-request-id", "msal-request-id");

        HttpActivityEnricher.Enrich(activity, request);

        Assert.Equal("arm-request-id", activity.GetTagItem(TelemetryTags.ClientRequestId));
    }

    [Fact]
    public void ShouldCollect_RequiresLiveConsentAndActiveInvocation()
    {
        using var invocation = new Activity("CLI.Activity.Root").Start();
        invocation.SetTag(TelemetryTags.ClientType, "CLI");

        Assert.True(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, invocation));
        Assert.False(HttpActivityEnricher.ShouldCollect(telemetryEnabled: false, invocation));
        Assert.False(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, current: null));
    }

    [Fact]
    public void ShouldCollect_RecognizesInvocationAncestor()
    {
        using var invocation = new Activity("PowerShell.Activity.Root").Start();
        invocation.SetTag(TelemetryTags.ClientType, "PowerShell");
        using var child = new Activity("ProfileService.GetActiveProfileAsync").Start();

        Assert.True(HttpActivityEnricher.ShouldCollect(telemetryEnabled: true, child));
    }
}
