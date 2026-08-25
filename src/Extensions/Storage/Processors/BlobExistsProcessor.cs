// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobExistsProcessor : StorageBlobProcessorBase
{
    public override async Task ProcessAsync()
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Head);
        using HttpResponseMessage response = await SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await WriteOutput(false, Publisher.Token);
            return;
        }

        await response.EnsureSuccessAsync(Publisher);
        await WriteOutput(true, Publisher.Token);
    }
}
