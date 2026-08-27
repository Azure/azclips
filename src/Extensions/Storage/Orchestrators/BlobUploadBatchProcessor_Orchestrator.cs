// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Enumeration;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobUploadBatchProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public string? Pattern { get; set; }
    public bool Overwrite { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        if (string.IsNullOrWhiteSpace(SourceDirectory))
        {
            throw new ArgumentException("SourceDirectory is required.", nameof(SourceDirectory));
        }

        string sourceRoot = Path.GetFullPath(SourceDirectory);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source directory '{sourceRoot}' does not exist.");
        }

        // ponytail: transfer sequentially; add bounded parallelism only if throughput becomes a problem.
        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).Order())
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath).Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(Pattern)
                && !FileSystemName.MatchesSimpleExpression(
                    Pattern,
                    relativePath,
                    ignoreCase: OperatingSystem.IsWindows()))
            {
                continue;
            }

            string prefix = DestinationPath?.Trim('/') ?? string.Empty;
            string blobName = string.IsNullOrEmpty(prefix)
                ? relativePath
                : $"{prefix}/{relativePath}";
            BlockBlobUploadProcessor_Orchestrator processor =
                CreateExtendedProcessor<BlockBlobUploadProcessor_Orchestrator>(p =>
                {
                    p.AccountName = AccountName;
                    p.ContainerName = ContainerName;
                    p.BlobName = blobName;
                    p.FilePath = sourcePath;
                    p.Overwrite = Overwrite;
                });
            await processor.ProcessAsync();
        }

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
