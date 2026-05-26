// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Core.Telemetry;

namespace Core.Tests;

public class CoreTelemetryPropertiesTests
{
    [Fact]
    public void ApplyStaticProperties_ShouldNotThrow_WhenActivityIsNull()
    {
        var exception = Record.Exception(() => CoreTelemetryProperties.ApplyStaticProperties(null));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyStaticProperties_ShouldTagAllEssentialStaticTelemetryProperties_WhenActivityIsNotNull()
    {
        using var activity = new Activity("test").Start();

        CoreTelemetryProperties.ApplyStaticProperties(activity);

        Assert.Equal(CoreTelemetryProperties.Id, activity.GetTagItem(CoreTelemetryProperties.IdTag));
        Assert.Equal(CoreTelemetryProperties.IsMicrosoftInternal, activity.GetTagItem(CoreTelemetryProperties.MicrosoftInternalTag));
        Assert.Equal(CoreTelemetryProperties.ProductName, activity.GetTagItem(CoreTelemetryProperties.ProductNameTag));
        Assert.Equal(CoreTelemetryProperties.ProductVersion, activity.GetTagItem(CoreTelemetryProperties.ProductVersionTag));
        Assert.Equal(CoreTelemetryProperties.SchemaVersion, activity.GetTagItem(CoreTelemetryProperties.SchemaVersionTag));
        Assert.Equal(CoreTelemetryProperties.TelemetryVersion, activity.GetTagItem(CoreTelemetryProperties.TelemetryVersionTag));
    }

    [Fact]
    public void Id_ShouldBeStable_AcrossInvocations()
    {
        var first = CoreTelemetryProperties.Id;
        var second = CoreTelemetryProperties.Id;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Id_ShouldBeSha256HexString_64UppercaseHexChars()
    {
        var id = CoreTelemetryProperties.Id;

        Assert.Equal(64, id.Length);
        Assert.Matches(new Regex("^[0-9A-F]{64}$"), id);
    }

    // --- Static property contracts ---

    [Fact]
    public void ProductName_ShouldNotBeNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(CoreTelemetryProperties.ProductName));
    }

    [Fact]
    public void ProductVersion_ShouldNotBeNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(CoreTelemetryProperties.ProductVersion));
    }

    [Fact]
    public void SchemaVersion_ShouldNotBeNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(CoreTelemetryProperties.SchemaVersion));
    }

    [Fact]
    public void TelemetryVersion_ShouldNotBeNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(CoreTelemetryProperties.TelemetryVersion));
    }

    //TODO: write unit test for determining ismicrosoftinternal
    // [Fact]
    // public void IsMicrosoftInternal_ShouldDefaultToFalse_WhileResolverIsUnconfigured()
    // {
    //     // Change-detector for the TODO in ResolveIsMicrosoftInternal: when that
    //     // becomes config-driven, this test should be updated alongside the impl.
    //     Assert.False(CoreTelemetryProperties.IsMicrosoftInternal);
    // }
}
