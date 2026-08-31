// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class TraceDirectoryProviderTests
{
    [Fact]
    public void GetTraceDirectory_ShouldReturnLocalAppDataTracesDirectory_WhenUsingDefaultProvider()
    {
        // Pins the concrete path, so a change to where production telemetry lands fails here.
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".azclips",
            "traces");

        Assert.Equal(expected, new DefaultTraceDirectoryProvider().GetTraceDirectory());
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
