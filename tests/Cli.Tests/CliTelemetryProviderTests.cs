// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Core.Cli.Telemetry;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
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

    [Fact]
    public async Task FailedHttpActivity_DirectExporterDoesNotReceiveExceptionDetails()
    {
        var exported = new List<ExportedActivity>();
        var provider = CreateProvider("{}", new CapturingTelemetrySender(exported));
        try
        {
            using var root = provider.ActivitySource.StartActivity("CLI.Activity.Root");
            root!.SetTag(TelemetryTags.ClientType, ClientType.CLI.ToString());
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var acceptTask = listener.AcceptTcpClientAsync();
            var requestTask = client.GetAsync($"http://127.0.0.1:{endpoint.Port}/private/path");
            using var connection = await acceptTask;
            listener.Stop();
            connection.Close();

            await Assert.ThrowsAsync<HttpRequestException>(() => requestTask);

            var http = Assert.Single(exported, activity =>
                activity.Name == "System.Net.Http.HttpRequestOut");
            Assert.Equal(ActivityStatusCode.Error, http.Status);
            Assert.Null(http.StatusDescription);
            var eventTags = http.Events.SelectMany(activityEvent => activityEvent.Tags).ToArray();
            Assert.DoesNotContain(
                eventTags,
                tag => (tag.Key is "exception.message" or "exception.stacktrace")
                    && tag.Value is not null);
            Assert.DoesNotContain(
                eventTags,
                tag => tag.Value?.ToString()?.Contains("/private/path", StringComparison.Ordinal) == true);
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    private CLITelemetryProvider CreateProvider(
        string configurationText,
        ITelemetrySender? sender = null)
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
            sender ?? new FileTelemetrySender(new FixedTraceDirectory(TraceDirectory)),
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
                    activity.StatusDescription,
                    [.. activity.Events]));
            }

            return ExportResult.Success;
        }
    }

    private sealed record ExportedActivity(
        string Name,
        ActivityStatusCode Status,
        string? StatusDescription,
        ActivityEvent[] Events);
}
