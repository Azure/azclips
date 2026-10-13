// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Tests.Common.Utilities;

namespace Core.Telemetry.Tests;

public class FileExporterBatchTests
{
    private const string SourceName = "FileExporterBatchTests";

    /// <summary>
    /// A <see cref="BatchActivityExportProcessor"/> hands the exporter a <see cref="Batch{T}"/>
    /// backed by a circular buffer, which dequeues as it is enumerated. An exporter that walks it
    /// more than once therefore loses whatever the earlier pass consumed.
    /// </summary>
    [Fact]
    public void Export_ShouldWriteEveryActivity_WhenTheBatchProcessorDeliversThemTogether()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "azclips-file-exporter-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var source = new ActivitySource(SourceName);

            using (var provider = Sdk.CreateTracerProviderBuilder()
                       .AddSource(SourceName)
                       .SetSampler(new AlwaysOnSampler())
                       .AddProcessor(new BatchActivityExportProcessor(new FileExporter(directory)))
                       .Build())
            {
                // Siblings under one root, so all four land in a single batch. The first to end is
                // the one a double enumeration drops.
                using var root = source.StartActivity("root");
                source.StartActivity("first")?.Dispose();
                source.StartActivity("second")?.Dispose();
                source.StartActivity("third")?.Dispose();
            }

            var names = Directory
                .GetFiles(directory, TraceFileStore.PendingFilePattern)
                .SelectMany(file => TraceFileFormat.Parse(File.ReadAllText(file)))
                .Select(entry => entry.OperationName)
                .ToList();

            Assert.Equal(
                new[] { "first", "root", "second", "third" },
                names.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void Export_ShouldPromoteEntraObjectId_FromParentChain()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "azclips-file-exporter-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var source = new ActivitySource(SourceName);

            using (var provider = Sdk.CreateTracerProviderBuilder()
                       .AddSource(SourceName)
                       .SetSampler(new AlwaysOnSampler())
                       .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(directory)))
                       .Build())
            {
                using var root = source.StartActivity("root");
                Assert.NotNull(root);
                root.SetTag(TelemetryTags.EntraObjectId, "object-1");

                using var child = source.StartActivity("child");
                Assert.NotNull(child);
            }

            var childEntry = Directory
                .GetFiles(directory, TraceFileStore.PendingFilePattern)
                .SelectMany(file => TraceFileFormat.Parse(File.ReadAllText(file)))
                .Single(entry => entry.OperationName == "child");

            Assert.Equal("object-1", childEntry.EntraObjectId);
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(directory);
        }
    }
}
