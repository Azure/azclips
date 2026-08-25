// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageBlobProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public int NumResults { get; set; }
    public string[] Include { get; set; } = [];

    public override async Task ProcessAsync()
    {
        if (NumResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumResults), "NumResults cannot be negative.");
        }

        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            List<StorageBlob> blobs = [];
            string? marker = Marker;
            do
            {
                int? maxResults = NumResults == 0 ? null : Math.Min(5000, NumResults - blobs.Count);
                ListStorageBlobProcessor processor = CreateExtendedProcessor<ListStorageBlobProcessor>(p =>
                {
                    p.Url = StorageBlobCustomizationHelpers.BuildContainerUrl(AccountName, ContainerName);
                    p.Prefix = Prefix;
                    p.Marker = marker;
                    p.MaxResults = maxResults;
                    p.Include = Include;
                });
                using HttpResponseMessage response = await processor.SendAsync();
                XDocument document = await XDocument.LoadAsync(
                    await response.Content.ReadAsStreamAsync(Publisher.Token),
                    LoadOptions.None,
                    Publisher.Token);
                blobs.AddRange(document.Descendants()
                    .Where(element => element.Name.LocalName == "Blob")
                    .Select(ParseBlob));
                marker = Value(document.Root, "NextMarker");
            }
            while (!string.IsNullOrWhiteSpace(marker) && (NumResults == 0 || blobs.Count < NumResults));

            await WriteOutput(
                NumResults == 0 ? blobs.ToArray() : blobs.Take(NumResults).ToArray(),
                Publisher.Token);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private static StorageBlob ParseBlob(XElement element)
    {
        XElement? properties = Child(element, "Properties");
        XElement? metadata = Child(element, "Metadata");
        XElement? tags = Child(Child(element, "BlobTags"), "TagSet");
        return new StorageBlob(
            Value(element, "Name") ?? string.Empty,
            Value(properties, "BlobType"),
            ParseLong(Value(properties, "Content-Length")),
            Value(properties, "Content-Type"),
            Value(properties, "Etag"),
            Value(properties, "Last-Modified"),
            Value(properties, "AccessTier"),
            metadata?.Elements().ToDictionary(
                item => item.Name.LocalName,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ParseTags(tags));
    }

    private static IReadOnlyDictionary<string, string> ParseTags(XElement? tagSet) =>
        tagSet?.Elements()
            .Where(element => element.Name.LocalName == "Tag")
            .ToDictionary(
                tag => Value(tag, "Key") ?? string.Empty,
                tag => Value(tag, "Value") ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static XElement? Child(XElement? element, string name) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName == name);

    private static string? Value(XElement? element, string name) => Child(element, name)?.Value;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
}
