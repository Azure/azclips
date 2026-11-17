// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Runtime.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Tests.Common.Stubs;

namespace Core.Telemetry.Tests;

public sealed class PersistenceTelemetryTests
{
    [Fact]
    public async Task BoundaryCache_UsesInjectedReadAndWriteApis_NotTheNativeCacheFile()
    {
        var root = NewRoot();
        try
        {
            Directory.CreateDirectory(root);
            var nativeCache = Path.Combine(root, HomePaths.DataBoundaryCacheFileName);
            File.WriteAllText(nativeCache, "{\"tenant\":\"Global\"}");
            var persistence = new StubPersistenceStore(root);
            persistence.Files[HomePaths.DataBoundaryCacheFileName] =
                Encoding.UTF8.GetBytes("{\"tenant\":\"EU\"}");
            var profiles = new StubProfileService(seedDefault: false);
            var resolver = new DataBoundaryResolver(persistence, profiles);

            var boundary = await resolver.ResolveAndCacheAsync("tenant",
                _ => throw new InvalidOperationException("The injected cache must prevent authentication."),
                TimeSpan.FromSeconds(1));
            Assert.Equal("EU", boundary);

            await new DataBoundaryCache(persistence).SetAsync("other-tenant", "global");

            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                persistence.Files[HomePaths.DataBoundaryCacheFileName])!;
            Assert.Equal("EU", saved["tenant"]);
            Assert.Equal("Global", saved["other-tenant"]);
            Assert.Equal("{\"tenant\":\"Global\"}", File.ReadAllText(nativeCache));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CachedStartup_UsesInjectedProfileServiceAndStore()
    {
        var persistence = new StubPersistenceStore(NewRoot());
        persistence.Files[HomePaths.DataBoundaryCacheFileName] =
            Encoding.UTF8.GetBytes("{\"selected-tenant\":\"Global\"}");
        var profiles = new StubProfileService(seedDefault: false);
        await profiles.SaveAndActivateProfileAsync(new Profile { Name = "selected", TenantId = "selected-tenant" });
        var resolver = new DataBoundaryResolver(persistence, profiles);

        var context = await CommandTelemetryContext.StandaloneAsync(true, resolver);

        Assert.Equal("selected-tenant", context.TenantId);
        Assert.Equal(UploadRegion.ROW, context.UploadRegion);
        Assert.False(Directory.Exists(persistence.RootDirectory));
    }

    [Fact]
    public async Task BoundaryCache_IsolatesStoreInstancesWithTheSameTenant()
    {
        var first = new StubPersistenceStore(NewRoot());
        var second = new StubPersistenceStore(NewRoot());
        first.Files[HomePaths.DataBoundaryCacheFileName] = Encoding.UTF8.GetBytes("{\"tenant\":\"EU\"}");
        second.Files[HomePaths.DataBoundaryCacheFileName] = Encoding.UTF8.GetBytes("{\"tenant\":\"Global\"}");

        Assert.Equal("EU", await new DataBoundaryCache(first).GetAsync("tenant"));
        Assert.Equal("Global", await new DataBoundaryCache(second).GetAsync("tenant"));
    }

    [Fact]
    public async Task BoundaryCache_ConcurrentWritersPreserveBothTenants()
    {
        var root = NewRoot();
        try
        {
            var store = new FilePersistenceStore(root);
            await Task.WhenAll(
                new DataBoundaryCache(store).SetAsync("first", "EU"),
                new DataBoundaryCache(store).SetAsync("second", "Global"));

            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                (await store.ReadAsync(HomePaths.DataBoundaryCacheFileName))!)!;
            Assert.Equal("EU", saved["first"]);
            Assert.Equal("Global", saved["second"]);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task BoundaryCache_WriteFailureIsReported_AndConfirmedValueRemainsInMemory()
    {
        var root = NewRoot();
        var previousError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            var store = new FailingWriteStore(root);
            var cache = new DataBoundaryCache(store);
            await cache.SetAsync("tenant", "EU");

            Assert.Equal("EU", await cache.GetAsync("tenant"));
            Assert.Empty(store.Files);
            Assert.Contains("[DataBoundaryCache] Cache write failed: IOException.", error.ToString());
        }
        finally
        {
            Console.SetError(previousError);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task BoundaryCache_CancellationPropagatesWithoutWriting()
    {
        var store = new StubPersistenceStore(NewRoot());
        var cache = new DataBoundaryCache(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.SetAsync("tenant", "EU", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetAsync("tenant", cancellation.Token));
        Assert.Empty(store.Files);
        Assert.False(Directory.Exists(store.RootDirectory));
    }

    [Fact]
    public async Task BoundaryCache_ReadFailureIsReportedAndTreatedAsACacheMiss()
    {
        var store = new FailingReadStore(NewRoot());
        var previousError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            Assert.Null(await new DataBoundaryCache(store).GetAsync("tenant"));
            Assert.Contains("[DataBoundaryCache] Cache read failed: IOException.", error.ToString());
            Assert.False(Directory.Exists(store.RootDirectory));
        }
        finally
        {
            Console.SetError(previousError);
        }
    }

    [Fact]
    public async Task BoundaryCache_PreservesBomEncodedLegacyCacheReads()
    {
        var store = new StubPersistenceStore(NewRoot());
        store.Files[HomePaths.DataBoundaryCacheFileName] =
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{\"tenant\":\"EU\"}")).ToArray();

        Assert.Equal("EU", await new DataBoundaryCache(store).GetAsync("tenant"));
    }

    [Fact]
    public void TraceWriterReservationsAndUploader_UseInjectedRoot()
    {
        var root = NewRoot();
        try
        {
            var services = new ServiceCollection();
            services.RegisterTelemetryServices();
            services.AddSingleton<IPersistenceStore>(new FilePersistenceStore(root));
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            using var container = services.BuildServiceProvider();
            var directory = container.GetRequiredService<ITraceDirectoryProvider>().GetTraceDirectory();
            var reservation = container.GetRequiredService<TelemetryUploadReservation>();
            Assert.Equal(Path.Combine(root, HomePaths.TraceDirectoryName), directory);

            var sourceName = nameof(TraceWriterReservationsAndUploader_UseInjectedRoot);
            using var source = new ActivitySource(sourceName);
            var builder = Sdk.CreateTracerProviderBuilder().AddSource(sourceName);
            container.GetRequiredService<ITelemetrySender>().ConfigureExporter(builder);
            using var provider = builder.Build();
            string invocationId;
            using (var activity = source.StartActivity("test-command"))
            {
                Assert.NotNull(activity);
                var context = TelemetryInvocationContext.Capture(activity, true);
                invocationId = context.InvocationId;
                Assert.True(reservation.Begin(context));
            }

            Assert.Single(Directory.GetFiles(directory, "traces-*.log"));
            Assert.Single(Directory.GetFiles(directory, "traces-hold-*.json"));
            var sink = new RecordingSink();
            var uploader = new TelemetryUpload(container.GetRequiredService<IPersistenceStore>(), sink);
            uploader.RunPending();
            Assert.Equal(0, sink.Count);

            provider.Dispose();
            container.Dispose();
            reservation.MarkReady(invocationId);
            uploader.RunPending();
            Assert.Equal(1, sink.Count);
            Assert.Empty(Directory.GetFiles(directory, "traces-*.log"));
            Assert.Empty(Directory.GetFiles(directory, "traces-hold-*.json"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void WriterOverride_DoesNotRedirectReservationsOrUploads()
    {
        var root = NewRoot();
        try
        {
            var persistence = new FilePersistenceStore(root);
            var writerRoot = Path.Combine(root, "writer");
            var services = new ServiceCollection();
            services.RegisterTelemetryServices();
            services.AddSingleton<IPersistenceStore>(persistence);
            services.AddSingleton<ITraceDirectoryProvider>(
                new DefaultTraceDirectoryProvider(new FilePersistenceStore(writerRoot)));
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            using var container = services.BuildServiceProvider();
            var reservation = container.GetRequiredService<TelemetryUploadReservation>();
            using var source = new ActivitySource(nameof(WriterOverride_DoesNotRedirectReservationsOrUploads));
            var builder = Sdk.CreateTracerProviderBuilder().AddSource(source.Name);
            container.GetRequiredService<ITelemetrySender>().ConfigureExporter(builder);
            using var provider = builder.Build();
            using (var activity = source.StartActivity("test-command"))
            {
                Assert.NotNull(activity);
                var context = TelemetryInvocationContext.Capture(activity, true);
                Assert.True(reservation.Begin(context));
                reservation.MarkReady(context.InvocationId);
            }

            var queue = Path.Combine(root, HomePaths.TraceDirectoryName);
            var writerDirectory = Path.Combine(writerRoot, HomePaths.TraceDirectoryName);
            Assert.Single(Directory.GetFiles(queue, "traces-hold-*.json"));
            Assert.Empty(Directory.GetFiles(queue, "traces-*.log"));
            Assert.Single(Directory.GetFiles(writerDirectory, "traces-*.log"));
            Assert.Empty(Directory.GetFiles(writerDirectory, "traces-hold-*.json"));

            var sink = new RecordingSink();
            new TelemetryUpload(persistence, sink).RunPending();

            Assert.Equal(0, sink.Count);
            Assert.Single(Directory.GetFiles(writerDirectory, "traces-*.log"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "telemetry-persistence-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class FailingWriteStore(string root) : StubPersistenceStore(root)
    {
        public override Task WriteAsync(
            string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated storage failure.");
    }

    private sealed class FailingReadStore(string root) : StubPersistenceStore(root)
    {
        public override Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated storage failure.");
    }

    private sealed class RecordingSink : ITraceExportSink
    {
        public int Count { get; private set; }

        public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries,
            UploadRegion? uploadRegion = null, TraceReplayProfile profile = TraceReplayProfile.Legacy,
            TraceReplayContext? replayContext = null)
        {
            Count += entries.Count;
            return true;
        }
    }
}
