// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class BlobContainerOperations
{

    internal static StorageBlobContainer Resolve(IBlobContainerOptions options, StorageDataPlaneProcessorBase owner, StorageBlobContainer? inputContainer = null, string? defaultContainer = null, string? defaultAccount = null)
    {
        if (options.Timeout is <= 0) throw new ArgumentException("Timeout must be positive.");
        var selected = inputContainer ?? options.ContainerObject;
        if (inputContainer is not null && options.ContainerObject is not null &&
            (inputContainer.Name != options.ContainerObject.Name || inputContainer.Context.StorageAccountName != options.ContainerObject.Context.StorageAccountName ||
             inputContainer.Context.Profile != options.ContainerObject.Context.Profile))
            throw new ArgumentException("The resource and container inputs refer to different contexts.");
        var context = selected?.Context ?? options.Context;
        if (context is null)
        {
            var profile = owner.ResolvedProfile;
            StorageContext.ValidateProfile(profile);
            context = new StorageContext(options.StorageAccountName ?? defaultAccount ?? throw new ArgumentException("Specify a Storage context or account name."), profile!);
        }
        StorageContext.ValidateProfile(context.Profile);
        if (options.Context is not null && (options.Context.StorageAccountName != context.StorageAccountName || options.Context.Profile != context.Profile) ||
            options.StorageAccountName is not null && options.StorageAccountName != context.StorageAccountName)
            throw new ArgumentException("The selected account/profile conflicts with the resource context.");
        string container = selected?.Name ?? options.Container ?? defaultContainer ?? throw new ArgumentException("Specify a container name or native container object.");
        if (options.Container is not null && options.Container != container)
            throw new ArgumentException("The container name conflicts with the native resource object.");
        var result = new StorageBlobContainer(container, context);
        _ = result.Uri;
        return result;
    }

    internal static T CreateProcessor<T>(StorageDataPlaneProcessorBase owner, StorageBlobContainer container, Action<T> configure)
        where T : StorageDataPlaneProcessorBase, new() =>
        owner.CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = container.Context;
            processor.DeferFailureDiagnostics = true;
            configure(processor);
        });

}
