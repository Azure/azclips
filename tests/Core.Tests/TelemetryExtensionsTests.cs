// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using Tests.Common.Stubs;

namespace Core.Tests;

public class TelemetryExtensionsTests : IDisposable
{
    private readonly StubTelemetryService _service = new();
    private readonly ActivityListener _listener;
    private readonly List<Activity> _stoppedActivities = new();

    public TelemetryExtensionsTests()
    {
        _listener = new ActivityListener
        {
            // Filter by identity so this listener doesn't observe other tests' activities.
            ShouldListenTo = src => src == _service.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => _stoppedActivities.Add(a),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _service.Dispose();
    }

    // --- StartCLIActivity ---

    [Fact]
    public void StartCLIActivity_ShouldStartAzureCliMainServerActivity_WhenInvoked()
    {
        using var activity = _service.StartCLIActivity(Array.Empty<string>());

        Assert.NotNull(activity);
        Assert.Equal("AzureCLI.Main", activity!.OperationName);
        Assert.Equal(ActivityKind.Server, activity.Kind);
    }

    [Fact]
    public void StartCLIActivity_ShouldAppendPositionalArgsToCommand_WhenAllArgsAreSubcommands()
    {
        using var activity = _service.StartCLIActivity(new[] { "group", "list" });

        Assert.Equal("az group list", activity!.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void StartCLIActivity_ShouldNotRedact_WhenOptionHasNoFollowingValue()
    {
        using var activity = _service.StartCLIActivity(new[] { "-h" });

        Assert.Equal("az", activity!.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("-h", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void StartCLIActivity_ShouldRedactEachOptionValue_WhenMultipleOptionsWithValues()
    {
        using var activity = _service.StartCLIActivity(new[] { "group", "-n", "myrg", "-l", "eastus" });

        Assert.Equal("az group", activity!.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("-n *** -l ***", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void StartCLIActivity_ShouldStopAppendingToCommand_AfterFirstOptionEncountered()
    {
        using var activity = _service.StartCLIActivity(new[] { "group", "-n", "myrg", "trailingPositional" });

        Assert.Equal("az group", activity!.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("-n *** trailingPositional", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    // --- StartPowerShellActivity ---

    [Fact]
    public void StartPowerShellActivity_ShouldStartAzurePsCmdletServerActivity_WhenInvoked()
    {
        using var activity = _service.StartPowerShellActivity("Get-AzGroup", new Dictionary<string, object>());

        Assert.NotNull(activity);
        Assert.Equal("AzurePS.Cmdlet", activity!.OperationName);
        Assert.Equal(ActivityKind.Server, activity.Kind);
    }

    [Fact]
    public void StartPowerShellActivity_ShouldRedactEachParameter_WhenMultipleProvided()
    {
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "myrg",
            ["Location"] = "eastus",
        };

        using var activity = _service.StartPowerShellActivity("New-AzGroup", parameters);

        Assert.Equal("-Name *** -Location ***", activity!.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }

    [Fact]
    public void StartPowerShellActivity_ShouldNotThrow_WhenParametersIsNull()
    {
        using var activity = _service.StartPowerShellActivity("Get-AzGroup", null!);

        Assert.NotNull(activity);
        Assert.Equal("Get-AzGroup", activity!.GetTagItem(CoreTelemetryProperties.CommandTag));
        Assert.Equal("", activity.GetTagItem(CoreTelemetryProperties.ParameterTag));
    }
}
