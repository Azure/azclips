// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;

namespace Cli.Tests;

public class MsalAuthTelemetryTests
{
    [Fact]
    public void SetAuthenticationSuccessTags_RecordsOutcomeAndDurations()
    {
        using var activity = new Activity("auth").Start();

        MsalAuthService.SetAuthenticationSuccessTags(activity, 125, 8);

        Assert.Equal(true, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(125L, activity.GetTagItem(TelemetryTags.AuthDurationInHttpMs));
        Assert.Equal(8L, activity.GetTagItem(TelemetryTags.AuthDurationInCacheMs));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthStatusCode));
    }

    [Fact]
    public void SetAuthenticationFailureTags_RecordsMsalServiceStatusCode()
    {
        using var activity = new Activity("auth").Start();
        var exception = new MsalServiceException("service_error", "message", 429);

        MsalAuthService.SetAuthenticationFailureTags(activity, exception);

        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Equal(429, activity.GetTagItem(TelemetryTags.AuthStatusCode));
        Assert.Equal(
            typeof(MsalServiceException).FullName,
            activity.GetTagItem("exception.type"));
    }

    [Fact]
    public void SetAuthenticationFailureTags_OmitsStatusCodeWhenUnavailable()
    {
        using var activity = new Activity("auth").Start();

        MsalAuthService.SetAuthenticationFailureTags(activity, new InvalidOperationException());

        Assert.Equal(false, activity.GetTagItem(TelemetryTags.AuthSuccess));
        Assert.Null(activity.GetTagItem(TelemetryTags.AuthStatusCode));
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            activity.GetTagItem("exception.type"));
    }
}
