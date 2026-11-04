// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class TraceDirectoryProviderTests
{
    [Fact]
    public void GetTraceDirectory_ShouldReturnUserProfileTracesDirectory_WhenUsingDefaultProvider()
    {
        var previousHome = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, null);
        try
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                HomePaths.DefaultHomeDirectoryName,
                HomePaths.TraceDirectoryName);

            Assert.Equal(expected, new DefaultTraceDirectoryProvider().GetTraceDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previousHome);
        }
    }

    [Fact]
    public void StoragePaths_ShouldUseUserProfileHome_WhenOverrideIsUnset()
    {
        var previousHome = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, null);
        try
        {
            var userProfile = Path.Combine(Path.GetTempPath(), "azclips-user-profile");
            var home = Path.Combine(userProfile, HomePaths.DefaultHomeDirectoryName);

            Assert.Equal(Path.Combine(home, HomePaths.TraceDirectoryName),
                HomePaths.GetDefaultTraceDirectory(userProfile));
            Assert.Equal(Path.Combine(home, HomePaths.DataBoundaryCacheFileName),
                HomePaths.GetDefaultDataBoundaryCachePath(userProfile));
            Assert.Equal(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    HomePaths.DefaultHomeDirectoryName,
                    HomePaths.DataBoundaryCacheFileName),
                HomePaths.GetDefaultDataBoundaryCachePath());
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previousHome);
        }
    }

    [Fact]
    public void StoragePaths_ShouldHonorHomeOverride()
    {
        var previousHome = Environment.GetEnvironmentVariable(HomePaths.HomeOverrideVariable);
        var home = Path.Combine(Path.GetTempPath(), "azclips-storage-override");
        Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, home);
        try
        {
            Assert.Equal(Path.Combine(home, HomePaths.TraceDirectoryName),
                new DefaultTraceDirectoryProvider().GetTraceDirectory());
            Assert.Equal(Path.Combine(home, HomePaths.DataBoundaryCacheFileName),
                HomePaths.GetDefaultDataBoundaryCachePath());
            Assert.Equal(Path.Combine(home, HomePaths.TraceDirectoryName),
                HomePaths.GetDefaultTraceDirectory("ignored-user-profile"));
            Assert.Equal(Path.Combine(home, HomePaths.DataBoundaryCacheFileName),
                HomePaths.GetDefaultDataBoundaryCachePath("ignored-user-profile"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, previousHome);
        }
    }

    [Fact]
    public void RegisterTelemetryServices_ShouldRegisterDefaultProvider_WhenContainerIsBuilt()
    {
        var services = new ServiceCollection();
        services.RegisterTelemetryServices();

        using var container = services.BuildServiceProvider();
        var resolved = container.GetRequiredService<ITraceDirectoryProvider>();

        Assert.IsType<DefaultTraceDirectoryProvider>(resolved);
    }

    [Fact]
    public void FileTelemetrySender_ShouldUseOverriddenProvider_WhenOneIsRegisteredLast()
    {
        // A later provider registration must reach the sender DI builds; benchmarks override the
        // provider and leave the sender registration alone.
        var expected = Path.Combine(Path.GetTempPath(), "azclips-trace-directory-override");

        var services = new ServiceCollection();
        services.RegisterTelemetryServices();
        services.AddSingleton<ITraceDirectoryProvider>(new StubTraceDirectoryProvider(expected));
        services.AddSingleton<ITelemetrySender, FileTelemetrySender>();

        using var container = services.BuildServiceProvider();
        var sender = Assert.IsType<FileTelemetrySender>(container.GetRequiredService<ITelemetrySender>());

        // Builds through the real TracerProviderBuilder, so this covers the directory the exporter
        // is constructed with.
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("trace-directory-seam-test")
            .ConfigureExporterVia(sender)
            .Build();

        Assert.NotNull(provider);
        Assert.Equal(expected, container.GetRequiredService<ITraceDirectoryProvider>().GetTraceDirectory());
    }

    private sealed class StubTraceDirectoryProvider : ITraceDirectoryProvider
    {
        private readonly string _directory;

        internal StubTraceDirectoryProvider(string directory) => _directory = directory;

        public string GetTraceDirectory() => _directory;
    }
}

file static class TracerProviderBuilderTestExtensions
{
    internal static TracerProviderBuilder ConfigureExporterVia(
        this TracerProviderBuilder builder, ITelemetrySender sender)
    {
        sender.ConfigureExporter(builder);
        return builder;
    }
}
