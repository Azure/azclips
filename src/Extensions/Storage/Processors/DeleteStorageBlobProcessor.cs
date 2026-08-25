// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class DeleteStorageBlobProcessor : StorageBlobProcessorBase
{
    public string? SnapshotTime { get; set; }
    public string? VersionId { get; set; }
    public string? DeleteSnapshots { get; set; }
    public bool PermanentDelete { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public string? IfModifiedSince { get; set; }
    public string? IfUnmodifiedSince { get; set; }
    public string? TagCondition { get; set; }
    public bool PassThru { get; set; }

    public override async Task ProcessAsync()
    {
        Uri uri = BuildDeleteUri();
        using HttpRequestMessage request = CreateRequest(HttpMethod.Delete, uri);
        AddHeader(request, "x-ms-delete-snapshots", DeleteSnapshots);
        AddHeader(request, "x-ms-lease-id", LeaseId);
        AddHeader(request, "If-Match", IfMatch);
        AddHeader(request, "If-None-Match", IfNoneMatch);
        AddDateHeader(request, "If-Modified-Since", IfModifiedSince);
        AddDateHeader(request, "If-Unmodified-Since", IfUnmodifiedSince);
        AddHeader(request, "x-ms-if-tags", TagCondition);

        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);
        if (PassThru)
        {
            await WriteOutput(true, Publisher.Token);
        }
    }

    private Uri BuildDeleteUri()
    {
        if (!string.IsNullOrWhiteSpace(SnapshotTime) && !string.IsNullOrWhiteSpace(VersionId))
        {
            throw new ArgumentException("SnapshotTime and VersionId cannot be used together.");
        }
        if (!string.IsNullOrWhiteSpace(DeleteSnapshots)
            && (!string.IsNullOrWhiteSpace(SnapshotTime) || !string.IsNullOrWhiteSpace(VersionId)))
        {
            throw new ArgumentException("DeleteSnapshots cannot be used when deleting a snapshot or version.");
        }
        if (!string.IsNullOrWhiteSpace(DeleteSnapshots)
            && !DeleteSnapshots.Equals("include", StringComparison.OrdinalIgnoreCase)
            && !DeleteSnapshots.Equals("only", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("DeleteSnapshots must be include or only.", nameof(DeleteSnapshots));
        }

        var query = new List<string>();
        AddQuery("snapshot", SnapshotTime);
        AddQuery("versionid", VersionId);
        if (PermanentDelete)
        {
            query.Add("deletetype=Permanent");
        }
        return query.Count == 0
            ? BuildBlobUri()
            : new UriBuilder(BuildBlobUri()) { Query = string.Join("&", query) }.Uri;

        void AddQuery(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }
    }

    private static void AddHeader(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static void AddDateHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            throw new ArgumentException($"{name} must be a valid date.");
        }
        request.Headers.TryAddWithoutValidation(name, parsed.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture));
    }
}
