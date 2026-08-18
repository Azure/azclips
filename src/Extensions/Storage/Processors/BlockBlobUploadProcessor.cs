// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlockBlobUploadProcessor : StorageBlobProcessorBase
{
    public Stream Content { get; set; } = Stream.Null;
    public long ContentLength { get; set; }
    public bool Overwrite { get; set; }

    public override async Task ProcessAsync()
    {
        using HttpResponseMessage response = await SendAsync();
        await response.EnsureSuccessAsync(Publisher);
    }

    internal async Task<HttpResponseMessage> SendAsync()
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Put);
        request.Options.Set(RuntimeHttpRequestOptions.DisableRetry, true);
        request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        if (!Overwrite)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        }
        request.Content = new StreamContent(Content);
        request.Content.Headers.ContentLength = ContentLength;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await SendAsync(request);
    }
}
