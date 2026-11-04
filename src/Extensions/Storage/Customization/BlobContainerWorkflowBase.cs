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
            processor.DeferFailureDiagnostics = true;
            configure(processor);
        });

    protected abstract Task ExecuteAsync();
    internal virtual Task ProcessNativeAsync() => ProcessAsync();
    public sealed override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);
}
