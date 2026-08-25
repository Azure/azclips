// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageBlobProcessor : StorageBlobProcessorBase
{
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public int? NumResults { get; set; }
    public string[] Include { get; set; } = [];

    public override async Task ProcessAsync()
    {
        if (NumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumResults), "NumResults must be greater than zero.");
        }

        var blobs = new List<StorageBlob>();
        string? marker = Marker;
        do
        {
            using HttpRequestMessage request = CreateContainerRequest(HttpMethod.Get, BuildQuery(marker, blobs.Count));
            using HttpResponseMessage response = await SendAsync(request);
            await response.EnsureSuccessAsync(Publisher);

            XDocument document = await XDocument.LoadAsync(
                await response.Content.ReadAsStreamAsync(Publisher.Token),
                LoadOptions.None,
                Publisher.Token);
            blobs.AddRange(document.Descendants().Where(element => element.Name.LocalName == "Blob").Select(ParseBlob));
            marker = Value(document.Root, "NextMarker");
        }
        while (!string.IsNullOrWhiteSpace(marker) && (!NumResults.HasValue || blobs.Count < NumResults.Value));

        await WriteOutput(
            NumResults.HasValue ? blobs.Take(NumResults.Value).ToArray() : blobs.ToArray(),
            Publisher.Token);
    }

    private string BuildQuery(string? marker, int count)
    {
        var values = new List<string> { "restype=container", "comp=list" };
        Add("prefix", Prefix);
        Add("marker", marker);
        if (NumResults.HasValue)
        {
            Add("maxresults", Math.Min(5000, NumResults.Value - count).ToString(CultureInfo.InvariantCulture));
        }
        if (Include.Length > 0)
        {
            Add("include", string.Join(",", Include));
        }
        return string.Join("&", values);

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add($"{name}={Uri.EscapeDataString(value)}");
            }
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
                ?? new Dictionary<string, string>(),
            ParseTags(tags));
    }

    private static IReadOnlyDictionary<string, string> ParseTags(XElement? tagSet) =>
        tagSet?.Elements()
            .Where(element => element.Name.LocalName == "Tag")
            .ToDictionary(
                tag => Value(tag, "Key") ?? string.Empty,
                tag => Value(tag, "Value") ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>();

    private static XElement? Child(XElement? element, string name) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName == name);

    private static string? Value(XElement? element, string name) => Child(element, name)?.Value;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
}
