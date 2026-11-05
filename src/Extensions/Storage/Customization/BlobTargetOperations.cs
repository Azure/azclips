// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class BlobTargetOperations
{

    internal static BlobTarget Resolve(IBlobTargetOptions options, StorageDataPlaneProcessorBase owner, string? defaultName = null, string? resourceUri = null, string? defaultAccount = null)
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
        var container = BlobContainerOperations.Resolve(options, owner, options.InputObject?.Container, uriContainer, uriAccount ?? defaultAccount);
        string name = options.InputObject?.Name ?? options.Blob ?? uriName ?? defaultName ?? throw new ArgumentException("Specify a blob name or native blob object.");
        if (uriAccount is not null && (uriAccount != container.Context.StorageAccountName || uriContainer != container.Name || uriName != name))
            throw new ArgumentException("The Blob URI conflicts with the selected resource or context.");
        if (options.Blob is not null && options.Blob != name || options.InputObject?.Snapshot is not null && options.Snapshot is not null && options.Snapshot != options.InputObject.Snapshot ||
            options.InputObject?.VersionId is not null && options.VersionId is not null && options.VersionId != options.InputObject.VersionId)
            throw new ArgumentException("The blob name or selector conflicts with the native blob object.");
        string? snapshot = options.Snapshot ?? options.InputObject?.Snapshot ?? uriSnapshot;
        string? version = options.VersionId ?? options.InputObject?.VersionId ?? uriVersion;
        if (uriSnapshot is not null && uriSnapshot != snapshot || uriVersion is not null && uriVersion != version)
            throw new ArgumentException("The Blob URI selector conflicts with the selected resource.");
        if (snapshot is not null && version is not null || snapshot == string.Empty || version == string.Empty)
            throw new ArgumentException("Specify a nonempty snapshot or version, not both.");
        var target = new BlobTarget(container, name, snapshot, version);
        _ = target.Url;
        return target;
    }

    internal static BlobProcessors.GetPropertiesBlobProcessor PropertiesProcessor<T>(T owner, BlobTarget target)
        where T : StorageDataPlaneProcessorBase, IBlobTargetOptions =>
        BlobContainerOperations.CreateProcessor<BlobProcessors.GetPropertiesBlobProcessor>(owner, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = owner.Timeout;
            processor.LeaseId = owner.LeaseId; processor.IfMatch = owner.IfMatch; processor.IfNoneMatch = owner.IfNoneMatch;
            processor.IfModifiedSince = Date(owner.IfModifiedSince); processor.IfUnmodifiedSince = Date(owner.IfUnmodifiedSince);
            processor.IfTags = owner.IfTags;
        });

    internal static async Task<StorageBlob> ReadPropertiesAsync<T>(T owner, BlobTarget target)
        where T : StorageDataPlaneProcessorBase, IBlobTargetOptions
    {
        using var response = await PropertiesProcessor(owner, target).SendAsync();
        return StorageBlob.FromHeaders(target.Container, target.Name, StorageResourceProperties.FromResponse(response), target.Snapshot, target.VersionId);
    }

    internal static string? Date(DateTimeOffset? value) => value?.ToString("R", CultureInfo.InvariantCulture);
    internal static bool IsMissingBlob(HttpRequestException exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "BlobNotFound";
}

internal sealed record BlobTarget(StorageBlobContainer Container, string Name, string? Snapshot, string? VersionId)
{
    internal string Url => Container.Context.GetBlobUri(Container.Name, Name).AbsoluteUri
        .WithQuery("snapshot", Snapshot).WithQuery("versionid", VersionId);
}
