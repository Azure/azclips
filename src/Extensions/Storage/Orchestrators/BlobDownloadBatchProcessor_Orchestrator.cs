// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Enumeration;
using System.Xml.Linq;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadBatchProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string DestinationDirectory { get; set; } = string.Empty;
    public string? Pattern { get; set; }
    public bool Overwrite { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        if (string.IsNullOrWhiteSpace(DestinationDirectory))
        {
            throw new ArgumentException("DestinationDirectory is required.", nameof(DestinationDirectory));
        }

        string destinationRoot = Path.GetFullPath(DestinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        string destinationPrefix = destinationRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        // ponytail: transfer sequentially; add bounded parallelism only if throughput becomes a problem.
        await foreach (string blobName in ListBlobNamesAsync())
        {
            if (!string.IsNullOrWhiteSpace(Pattern)
                && !FileSystemName.MatchesSimpleExpression(Pattern, blobName))
            {
                continue;
            }

            string destinationPath = Path.GetFullPath(
                Path.Combine(destinationRoot, blobName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(
                    destinationPrefix,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Blob name '{blobName}' resolves outside the destination directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            BlobDownloadProcessor_Orchestrator processor =
                CreateExtendedProcessor<BlobDownloadProcessor_Orchestrator>(p =>
                {
                    p.AccountName = AccountName;
                    p.ContainerName = ContainerName;
                    p.BlobName = blobName;
                    p.FilePath = destinationPath;
                    p.Overwrite = Overwrite;
                });
            await processor.ProcessAsync();
        }

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }

    private async IAsyncEnumerable<string> ListBlobNamesAsync()
    {
        string? marker = null;
        do
        {
            string query = "restype=container&comp=list";
            if (!string.IsNullOrEmpty(marker))
            {
                query += $"&marker={Uri.EscapeDataString(marker)}";
            }

            using HttpRequestMessage request = CreateContainerRequest(HttpMethod.Get, query);
            using HttpResponseMessage response = await SendAsync(request);
            await response.EnsureSuccessAsync(Publisher);
            await using Stream content = await response.Content.ReadAsStreamAsync(Publisher.Token);
            XDocument document = await XDocument.LoadAsync(content, LoadOptions.None, Publisher.Token);

            foreach (XElement blob in document.Descendants().Where(e => e.Name.LocalName == "Blob"))
            {
                XElement? name = blob.Elements().FirstOrDefault(e => e.Name.LocalName == "Name");
                if (!string.IsNullOrEmpty(name?.Value))
                {
                    yield return name.Value;
                }
            }

            marker = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "NextMarker")?.Value;
        }
        while (!string.IsNullOrEmpty(marker));
    }
}
