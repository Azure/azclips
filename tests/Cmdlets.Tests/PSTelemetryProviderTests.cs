// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Management.Automation;

namespace Cmdlets.Tests;

public class PSTelemetryProviderTests
{
    [Fact]
    public void ActivityTags_ShouldContainPowerShellVersion()
    {
        var provider = new PSTelemetryProvider([], new StubTelemetrySender(), Mock.Of<IConfigurationService>());
        try
        {
            var tag = Assert.Single(provider.ActivityTags);
            Assert.Equal(TelemetryTags.PowerShellVersion, tag.Key);
            Assert.Equal(PSVersionInfo.PSVersion.ToString(), tag.Value);
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    [Fact]
    public void ShellType_ShouldBePwsh()
    {
        var provider = new PSTelemetryProvider([], new StubTelemetrySender(), Mock.Of<IConfigurationService>());
        try
        {
            Assert.Equal("pwsh", provider.ShellType);
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    [Fact]
    public void FailedHttpActivity_DirectExporterDoesNotReceiveStatusDescription()
    {
        var exported = new List<ExportedActivity>();
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(c => c.GetConfiguration<TelemetryOptions>())
            .Returns(new TelemetryOptions { Enabled = true });
        var provider = new PSTelemetryProvider(
            [
                new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()),
                new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()),
            ],
            new CapturingTelemetrySender(exported),
            configuration.Object);

        try
        {
            using var root = provider.ActivitySource.StartActivity("PowerShell.Activity.Root");
            root!.SetTag(TelemetryTags.ProductName, ClientType.PowerShell.ToString());
            using var httpSource = new ActivitySource("System.Net.Http");
            using (var http = httpSource.StartActivity(
                "System.Net.Http.HttpRequestOut",
                ActivityKind.Client))
            {
                http!.SetStatus(
                    ActivityStatusCode.Error,
                    "Request to https://private.example.test/tenant/resource failed.");
            }

            var exportedHttp = Assert.Single(exported, activity =>
                activity.Name == "System.Net.Http.HttpRequestOut");
            Assert.Equal(ActivityStatusCode.Error, exportedHttp.Status);
            Assert.Null(exportedHttp.StatusDescription);
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    private sealed class StubTelemetrySender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder)
        {
        }
    }

    private sealed class CapturingTelemetrySender(List<ExportedActivity> exported) : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder) =>
            builder.AddProcessor(new SimpleActivityExportProcessor(new SnapshotExporter(exported)));
    }

    private sealed class SnapshotExporter(List<ExportedActivity> exported) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                exported.Add(new ExportedActivity(
                    activity.OperationName,
                    activity.Status,
                    activity.StatusDescription));
            }

            return ExportResult.Success;
        }
    }

    private sealed record ExportedActivity(
        string Name,
        ActivityStatusCode Status,
        string? StatusDescription);
}
