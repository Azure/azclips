// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Management.Processors;

public abstract partial class StorageManagementProcessorBase : StorageDataPlaneProcessorBase
{
    protected override StorageService Service => StorageService.Management;
    protected virtual string Endpoint => "https://management.azure.com";
}

public abstract partial class StorageManagementProcessorBase<TRequest, TResponse> : StorageManagementProcessorBase
    where TRequest : class where TResponse : class
{
    protected override TRequest? CreateRequestBody() => default;
    partial void OverrideBeforeProcessBase();
    protected virtual Task BeforeProcessAsync()
    {
        OverrideBeforeProcessBase();
        return Task.CompletedTask;
    }

    protected virtual Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body) => Task.FromResult(false);
    public override Task ProcessAsync() => ProcessResponseAsync<TResponse>(BeforeProcessAsync, BeforeWriteOutputAsyncAndReturn);
}
