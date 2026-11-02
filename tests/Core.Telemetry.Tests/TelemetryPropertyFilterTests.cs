// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class TelemetryPropertyFilterTests
{
    private readonly ITelemetryPropertyFilter _filter = new DefaultTelemetryPropertyFilter();

    public static TheoryData<string> AllowedTelemetryTags() => new()
    {
        TelemetryTags.InstallationId,
        TelemetryTags.ProductName,
        TelemetryTags.ProductVersion,
        TelemetryTags.SchemaVersion,
        TelemetryTags.TelemetryVersion,
        TelemetryTags.CommandOutputType,
        TelemetryTags.ExceptionType,
        TelemetryTags.OperatingSystemType,
        TelemetryTags.OperatingSystemVersion,
        TelemetryTags.DistributionName,
        TelemetryTags.DistributionId,
        TelemetryTags.ShellType,
        TelemetryTags.CommandName,
        TelemetryTags.CommandExtensionName,
        TelemetryTags.CommandExtensionVersion,
        TelemetryTags.CommandParameters,
        TelemetryTags.SubscriptionId,
        TelemetryTags.TenantId,
        TelemetryTags.EntraObjectId,
        TelemetryTags.AuthType,
        TelemetryTags.AuthSuccess,
        TelemetryTags.AuthStatusCode,
        TelemetryTags.AuthDurationTotalMs,
        TelemetryTags.ClientRequestId,
        TelemetryTags.Mode,
        TelemetryTags.ImportKind,
        // AI error-recovery tags. These ride the properties.* channel rather than
        // AllowedKeys; a tag outside both is stripped silently at export.
        TelemetryTags.CommandTraceId,
        TelemetryTags.CommandSpanId,
        TelemetryTags.RecoverySessionStyle,
        TelemetryTags.RecoveryProvider,
        TelemetryTags.RecoveryExitCode,
        TelemetryTags.RecoveryReprompts,
        TelemetryTags.AgentModel,
        TelemetryTags.ProcessName,
        TelemetryTags.RecoveryOutcome,
        TelemetryTags.RecoverySkipReason,
    };

    [Theory]
    [MemberData(nameof(AllowedTelemetryTags))]
    public void IsAllowed_AllTelemetryTagsConstants_AreAllowed(string key)
    {
        Assert.True(_filter.IsAllowed(key));
    }

    [Theory]
    [InlineData("server.port")]
    [InlineData("http.request.method")]
    [InlineData("http.response.status_code")]
    [InlineData("network.protocol.version")]
    [InlineData("otel.status_code")]
    public void IsAllowed_KnownSafeHttpAndOtelKeys_AreAllowed(string key)
    {
        Assert.True(_filter.IsAllowed(key));
    }

    [Theory]
    [InlineData("http.url")]                 // can encode subscription IDs / resource paths
    [InlineData("url.full")]                 // OTel semantic-conv alternative
    [InlineData("server.address")]           // can expose private or internal hostnames
    [InlineData("http.target")]              // path / query
    [InlineData("exception.message")]        // can echo PII from caller-supplied input
    [InlineData("exception.stacktrace")]     // includes formatted messages and inner exceptions
    [InlineData("otel.status_description")]  // operator-supplied text
    [InlineData("resourceGroupName")]
    [InlineData("module.name")]
    [InlineData("module.version")]
    [InlineData("eventName")]
    [InlineData("UnknownNewTag")]
    [InlineData("anything.else")]
    public void IsAllowed_KeysNotInAllowlist_AreDropped(string key)
    {
        Assert.False(_filter.IsAllowed(key));
    }

    [Fact]
    public void IsAllowed_PropertiesPrefix_IsAllowed()
    {
        Assert.True(_filter.IsAllowed(TelemetryTags.PropertyTagPrefix + "foo"));
        Assert.True(_filter.IsAllowed(TelemetryTags.PropertyTagPrefix + "deeply.nested.value"));
    }

    [Fact]
    public void IsAllowed_IsCaseInsensitive()
    {
        Assert.True(_filter.IsAllowed("HTTP.REQUEST.METHOD"));
        Assert.True(_filter.IsAllowed("Http.Request.Method"));
        Assert.True(_filter.IsAllowed(TelemetryTags.CommandName.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsAllowed_NullOrEmpty_IsFalse(string? key)
    {
        Assert.False(_filter.IsAllowed(key));
    }

    [Fact]
    public void Allowlist_ExposesEveryAllowedKey()
    {
        // Spot-check that the exposed read-only view is consistent with IsAllowed
        // for the verbatim keys (the properties.* prefix is handled separately).
        var allowlist = DefaultTelemetryPropertyFilter.Allowlist;
        Assert.NotEmpty(allowlist);
        foreach (var key in allowlist)
        {
            Assert.True(_filter.IsAllowed(key), $"allowlist key '{key}' should pass IsAllowed");
        }
    }

    [Fact]
    public void IsAllowed_EveryEmittedTelemetryTagsConstant_IsAdmitted()
    {
        var stripped = typeof(TelemetryTags)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Where(field => field.Name is not (
                nameof(TelemetryTags.PropertyTagPrefix) or
                nameof(TelemetryTags.DataBoundary) or nameof(TelemetryTags.DataBoundaryOutcome)))
            .Select(field => new { field.Name, Value = (string)field.GetRawConstantValue()! })
            .Where(tag => !_filter.IsAllowed(tag.Value))
            .ToArray();

        Assert.Empty(stripped);
        Assert.False(_filter.IsAllowed(TelemetryTags.DataBoundary));
        Assert.False(_filter.IsAllowed(TelemetryTags.DataBoundaryOutcome));
    }
}
