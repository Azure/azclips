// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Abstractions.Runtime;
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
    //

    [Fact]
    public void ApplyClientType_ShouldTagClientType_WhenActivityIsNotNull()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyClientType(ClientType.CLI);

        Assert.Equal(ClientType.CLI, activity.GetTagItem(CoreTelemetryProperties.ClientTypeTag));
    }

    [Fact]
    public void ApplyClientType_ShouldReturnSameActivity_WhenChainedFluently()
    {
        using var activity = new Activity("test").Start();

        var returned = activity.ApplyClientType(ClientType.PowerShell);

        Assert.Same(activity, returned);
    }

    [Fact]
    public void ApplyClientType_ShouldNotThrow_WhenActivityIsNull()
    {
        var exception = Record.Exception(() => ((Activity?)null).ApplyClientType(ClientType.CLI));

        Assert.Null(exception);
    }

    // --- ApplyModuleInfo ---

    [Fact]
    public void ApplyModuleInfo_ShouldTagBothNameAndVersion_WhenBothProvided()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyModuleInfo("Resources", "1.2.3");

        Assert.Equal("Resources", activity.GetTagItem(CoreTelemetryProperties.ModuleNameTag));
        Assert.Equal("1.2.3", activity.GetTagItem(CoreTelemetryProperties.ModuleVersionTag));
    }

    [Fact]
    public void ApplyModuleInfo_ShouldSkipName_WhenNameIsNull()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyModuleInfo(null, "1.2.3");

        Assert.Null(activity.GetTagItem(CoreTelemetryProperties.ModuleNameTag));
        Assert.Equal("1.2.3", activity.GetTagItem(CoreTelemetryProperties.ModuleVersionTag));
    }

    [Fact]
    public void ApplyModuleInfo_ShouldSkipName_WhenNameIsEmpty()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyModuleInfo(string.Empty, "1.2.3");

        Assert.Null(activity.GetTagItem(CoreTelemetryProperties.ModuleNameTag));
        Assert.Equal("1.2.3", activity.GetTagItem(CoreTelemetryProperties.ModuleVersionTag));
    }

    [Fact]
    public void ApplyModuleInfo_ShouldSkipVersion_WhenVersionIsNullOrEmpty()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyModuleInfo("Resources", null);

        Assert.Equal("Resources", activity.GetTagItem(CoreTelemetryProperties.ModuleNameTag));
        Assert.Null(activity.GetTagItem(CoreTelemetryProperties.ModuleVersionTag));
    }

    [Fact]
    public void ApplyModuleInfo_ShouldNotThrow_WhenActivityIsNull()
    {
        var exception = Record.Exception(() => ((Activity?)null).ApplyModuleInfo("Resources", "1.2.3"));

        Assert.Null(exception);
    }

    // --- ApplyTotalDuration ---

    [Fact]
    public void ApplyTotalDuration_ShouldTagDurationInMilliseconds_WhenInvoked()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyTotalDuration(TimeSpan.FromMilliseconds(1234));

        Assert.Equal(1234L, activity.GetTagItem(CoreTelemetryProperties.TotalDurationTag));
    }

    [Fact]
    public void ApplyTotalDuration_ShouldTruncateFractionalMilliseconds_WhenDurationHasSubmillisecondComponent()
    {
        using var activity = new Activity("test").Start();

        // 100.7ms — truncates to 100, not rounds to 101.
        activity.ApplyTotalDuration(TimeSpan.FromTicks(1_007_000));

        Assert.Equal(100L, activity.GetTagItem(CoreTelemetryProperties.TotalDurationTag));
    }

    [Fact]
    public void ApplyTotalDuration_ShouldNotThrow_WhenActivityIsNull()
    {
        var exception = Record.Exception(() => ((Activity?)null).ApplyTotalDuration(TimeSpan.FromSeconds(1)));

        Assert.Null(exception);
    }

    // --- ApplyCommandLine ---

    [Fact]
    public void ApplyCommandLine_ShouldTagBothCommandAndParameter_WhenInvoked()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyCommandLine("az group list", "-l ***");

        Assert.Equal("az group list", activity.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("-l ***", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void ApplyCommandLine_ShouldTagEmptyValues_WhenInvokedWithEmptyStrings()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyCommandLine(string.Empty, string.Empty);

        // Even empty values are written — consumers see a stable schema regardless of input.
        Assert.Equal(string.Empty, activity.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal(string.Empty, activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void ApplyCommandLine_ShouldNotThrow_WhenActivityIsNull()
    {
        var exception = Record.Exception(() => ((Activity?)null).ApplyCommandLine("az", ""));

        Assert.Null(exception);
    }

    // --- ApplyEvent ---

    [Fact]
    public void ApplyEvent_ShouldReturnNull_WhenActivityIsNull()
    {
        Activity? activity = null;

        var result = activity.ApplyEvent(Events.SessionCreated);

        Assert.Null(result);
    }

    [Fact]
    public void ApplyEvent_ShouldNotThrow_WhenActivityIsNull()
    {
        Activity? activity = null;

        var exception = Record.Exception(() => activity.ApplyEvent(Events.SessionCreated));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyEvent_ShouldApplyEventTag_WithProductNamePrefix_WhenActivityIsNotNull()
    {
        using var activity = new Activity("test").Start();
        const string eventName = Events.SessionCreated;
        var expected = $"{CoreTelemetryProperties.ProductName}.{eventName}";

        activity.ApplyEvent(eventName);

        var tagValue = activity.GetTagItem(CoreTelemetryProperties.EventNameTag);
        Assert.Equal(expected, tagValue);
    }

    [Fact]
    public void ApplyEvent_ShouldReturnSameActivityInstance_WhenActivityIsNotNull()
    {
        using var activity = new Activity("test").Start();

        var result = activity.ApplyEvent("evt");

        Assert.Same(activity, result);
    }

    [Fact]
    public void ApplyEvent_ShouldOverwriteEventNameTag_WhenCalledTwice()
    {
        using var activity = new Activity("test").Start();

        activity.ApplyEvent("First");
        activity.ApplyEvent("Second");

        var tagValue = activity.GetTagItem(CoreTelemetryProperties.EventNameTag);
        Assert.Equal($"{CoreTelemetryProperties.ProductName}.Second", tagValue);
    }

}
