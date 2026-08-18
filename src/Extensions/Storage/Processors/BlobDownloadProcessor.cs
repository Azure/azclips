// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadProcessor : StorageBlobProcessorBase
{
    public override async Task ProcessAsync()
    {
        using HttpResponseMessage response = await SendAsync();
        await response.EnsureSuccessAsync(Publisher);
    }

    internal async Task<HttpResponseMessage> SendAsync()
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get);
        return await SendAsync(request);
    }
}
