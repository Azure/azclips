// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobContainerWorkflowBase : StorageDataPlaneProcessorBase
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    protected sealed override StorageService Service => StorageService.Blob;
    protected sealed override HttpMethod Method => HttpMethod.Get;
    protected sealed override string CreateRequestUri() =>
        throw new InvalidOperationException("Blob workflows execute through their atomic child processors.");

    protected StorageBlobContainer ResolveContainer(StorageBlobContainer? inputContainer = null, string? defaultContainer = null, string? defaultAccount = null)
    {
        if (Timeout is <= 0) throw new ArgumentException("Timeout must be positive.");
        var selected = inputContainer ?? ContainerObject;
        if (inputContainer is not null && ContainerObject is not null &&
            (inputContainer.Name != ContainerObject.Name || inputContainer.Context.StorageAccountName != ContainerObject.Context.StorageAccountName ||
             inputContainer.Context.Profile != ContainerObject.Context.Profile))
            throw new ArgumentException("The resource and container inputs refer to different contexts.");
        var context = selected?.Context ?? Context;
        if (context is null)
        {
            StorageContext.ValidateProfile(ResolvedProfile);
            context = new StorageContext(StorageAccountName ?? defaultAccount ?? throw new ArgumentException("Specify a Storage context or account name."), ResolvedProfile!);
        }
        StorageContext.ValidateProfile(context.Profile);
        if (Context is not null && (Context.StorageAccountName != context.StorageAccountName || Context.Profile != context.Profile) ||
            StorageAccountName is not null && StorageAccountName != context.StorageAccountName)
            throw new ArgumentException("The selected account/profile conflicts with the resource context.");
        string container = selected?.Name ?? Container ?? defaultContainer ?? throw new ArgumentException("Specify a container name or native container object.");
        if (Container is not null && Container != container)
            throw new ArgumentException("The container name conflicts with the native resource object.");
        var result = new StorageBlobContainer(container, context);
        _ = result.Uri;
        return result;
    }

    protected T CreateBlobProcessor<T>(StorageBlobContainer container, Action<T> configure)
        where T : StorageDataPlaneProcessorBase, new() =>
        CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = container.Context;
            processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
            configure(processor);
        });

    protected abstract Task ExecuteAsync();
    public sealed override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);
}

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
