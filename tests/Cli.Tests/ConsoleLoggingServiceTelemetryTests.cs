// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Runtime;

namespace Cli.Tests;

public class ConsoleLoggingServiceTelemetryTests
{
    [Fact]
    public void Debug_ShouldRecordOnlyStructuralTelemetryTags()
    {
        using var activity = new Activity("test").Start();
        var service = new ConsoleLoggingService();

        service.Debug("user@example.com");

        var telemetryEvent = Assert.Single(activity.Events);
        Assert.DoesNotContain(telemetryEvent.Tags, tag => tag.Key == "message");
        Assert.Contains(telemetryEvent.Tags, tag => tag is { Key: "level", Value: "Debug" });
        Assert.Contains(telemetryEvent.Tags, tag => tag is { Key: "logger", Value: nameof(ConsoleLoggingService) });
    }
}
