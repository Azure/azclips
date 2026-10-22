// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Nodes;
using Core.Agent.Tools.Resource.Cache;
using Core.Cli.Telemetry;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Core.Telemetry;
using Xunit;

namespace Cli.Tests;

// Agent tools that run in the CLI process, such as `az agent tool resource find`, record tool.* spans
// only through the Agent layer scope, never on the "CLI" source. These tests read a real config.json,
// run the real tool scope, and inspect the buffer that the detached uploader sends.
public sealed class CliTelemetryProviderTests : IDisposable
{
    private readonly DirectoryInfo _home = Directory.CreateTempSubdirectory("azclips-cli-telemetry-");

    private string TraceDirectory => Path.Combine(_home.FullName, "traces");

    public void Dispose() => _home.Delete(recursive: true);

    [Fact]
    public void AgentToolSpan_IsNotBufferedOnTheCliSource_WhenTelemetryIsEnabled()
    {
        var provider = CreateProvider("{}");
        try
        {
            RunFindResourcesTool();
        }
        finally
        {
            provider.TracerProvider.Dispose();
        }

        Assert.DoesNotContain("\"tool.find_resources\"", ReadBufferedTraces());
    }

    [Fact]
    public void AgentToolSpan_IsNotBuffered_WhenTelemetryIsDisabled()
    {
        CreateProvider("""{"telemetry":{"enabled":false}}""");

        RunFindResourcesTool();

        Assert.Empty(ReadBufferedTraces());
    }

    [Fact]
    public void UnreadableConfiguration_DoesNotThrow_AndBuffersNothing()
    {
        CreateProvider("""{"telemetry":""");

        RunFindResourcesTool();

        Assert.Empty(ReadBufferedTraces());
    }

    private CLITelemetryProvider CreateProvider(string configurationText)
    {
        var configurationPath = Path.Combine(_home.FullName, "config.json");
        File.WriteAllText(configurationPath, configurationText);
        var schema = new ConfigurationSchema();
        var configuration = new ConfigurationService(
            new FixedPathConfigurationProvider(configurationPath), schema, new DefaultConfigurationValidator(schema));

        return new CLITelemetryProvider(
            [
                new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()),
                new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()),
            ],
            new FileTelemetrySender(new FixedTraceDirectory(TraceDirectory)),
            configuration);
    }

    private static void RunFindResourcesTool()
    {
        // With the command's own root span off, the tool span starts as a root, as in the CLI.
        Activity.Current = null;
        using (ToolInvocationLog.Start("find_resources", "pattern=probe-rg kind=resourceGroup"))
        {
        }
    }

    private string ReadBufferedTraces() =>
        Directory.Exists(TraceDirectory)
            ? string.Concat(Directory.EnumerateFiles(TraceDirectory, "*", SearchOption.AllDirectories).Select(File.ReadAllText))
            : string.Empty;

    private sealed class FixedTraceDirectory(string path) : ITraceDirectoryProvider
    {
        public string GetTraceDirectory() => path;
    }

    private sealed class FixedPathConfigurationProvider(string path) : IConfigurationProvider
    {
        public string GetConfigFilePath() => path;

        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Only GetConfiguration reads are under test.");

        public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Only GetConfiguration reads are under test.");
    }
}
