// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

public abstract partial class FileProcessorBase : StorageDataPlaneProcessorBase
{
    public string Url { get; set; } = string.Empty;
    protected override StorageService Service => StorageService.Files;
    protected override string ResourceUrl => Url;
    protected virtual string Endpoint => new Uri(Url, UriKind.Absolute).GetLeftPart(UriPartial.Path);
}

public abstract partial class FileProcessorBase<TRequest, TResponse> : FileProcessorBase
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
