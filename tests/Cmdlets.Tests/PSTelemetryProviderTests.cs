// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;
using Moq;
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

    private sealed class StubTelemetrySender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder)
        {
        }
    }
}
