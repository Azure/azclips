// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobTargetWorkflowBase : BlobContainerWorkflowBase
{
    public string? Blob { get; set; }
    public StorageBlob? InputObject { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }

    protected sealed record Target(StorageBlobContainer Container, string Name, string? Snapshot, string? VersionId)
    {
        internal string Url => Container.Context.GetBlobUri(Container.Name, Name).AbsoluteUri
            .WithQuery("snapshot", Snapshot).WithQuery("versionid", VersionId);
    }

    protected Target ResolveTarget(string? defaultName = null, string? resourceUri = null, string? defaultAccount = null)
    {
        string? uriAccount = null, uriContainer = null, uriName = null, uriSnapshot = null, uriVersion = null;
        if (resourceUri is not null)
        {
            if (!Uri.TryCreate(resourceUri, UriKind.Absolute, out var uri))
                throw new ArgumentException("Specify an absolute public-cloud Blob URI.");
            uriAccount = StorageContext.ValidatePublicUri(uri, StorageService.Blob);
            int separator = uri.AbsolutePath.IndexOf('/', 1);
            if (separator < 0 || separator == uri.AbsolutePath.Length - 1)
                throw new ArgumentException("The Blob URI must identify a container and blob.");
            uriContainer = Uri.UnescapeDataString(uri.AbsolutePath[1..separator]);
            uriName = Uri.UnescapeDataString(uri.AbsolutePath[(separator + 1)..]);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            foreach (string? key in query.AllKeys)
                if (key is null || !key.Equals("snapshot", StringComparison.OrdinalIgnoreCase) && !key.Equals("versionid", StringComparison.OrdinalIgnoreCase) ||
                    query.GetValues(key)?.Length != 1)
                    throw new ArgumentException("Blob resource URIs support one snapshot or version selector only.");
            uriSnapshot = query["snapshot"]; uriVersion = query["versionid"];
        }
        var container = ResolveContainer(InputObject?.Container, uriContainer, uriAccount ?? defaultAccount);
        string name = InputObject?.Name ?? Blob ?? uriName ?? defaultName ?? throw new ArgumentException("Specify a blob name or native blob object.");
        if (uriAccount is not null && (uriAccount != container.Context.StorageAccountName || uriContainer != container.Name || uriName != name))
            throw new ArgumentException("The Blob URI conflicts with the selected resource or context.");
        if (Blob is not null && Blob != name || InputObject?.Snapshot is not null && Snapshot is not null && Snapshot != InputObject.Snapshot ||
            InputObject?.VersionId is not null && VersionId is not null && VersionId != InputObject.VersionId)
            throw new ArgumentException("The blob name or selector conflicts with the native blob object.");
        string? snapshot = Snapshot ?? InputObject?.Snapshot ?? uriSnapshot;
        string? version = VersionId ?? InputObject?.VersionId ?? uriVersion;
        if (uriSnapshot is not null && uriSnapshot != snapshot || uriVersion is not null && uriVersion != version)
            throw new ArgumentException("The Blob URI selector conflicts with the selected resource.");
        if (snapshot is not null && version is not null || snapshot == string.Empty || version == string.Empty)
            throw new ArgumentException("Specify a nonempty snapshot or version, not both.");
        var target = new Target(container, name, snapshot, version);
        _ = target.Url;
        return target;
    }

    protected BlobProcessors.GetPropertiesBlobProcessor PropertiesProcessor(Target target) =>
        CreateBlobProcessor<BlobProcessors.GetPropertiesBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });

    protected async Task<StorageBlob> ReadPropertiesAsync(Target target)
    {
        using var response = await PropertiesProcessor(target).SendAsync();
        return StorageBlob.FromHeaders(target.Container, target.Name, StorageResourceProperties.FromResponse(response), target.Snapshot, target.VersionId);
    }

    protected static string? Date(DateTimeOffset? value) => value?.ToString("R", CultureInfo.InvariantCulture);
    protected static bool IsMissingBlob(HttpRequestException exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "BlobNotFound";
}
