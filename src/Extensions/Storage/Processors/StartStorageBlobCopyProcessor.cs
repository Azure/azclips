// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class StartStorageBlobCopyProcessor : StorageBlobProcessorBase
{
    public string SourceUri { get; set; } = string.Empty;

    public override async Task ProcessAsync()
    {
        if (SourceUri.Length > 2048
            || !Uri.TryCreate(SourceUri, UriKind.Absolute, out Uri? source)
            || (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("SourceUri must be an absolute HTTP or HTTPS URL up to 2048 characters.", nameof(SourceUri));
        }
        

        Uri destination = new UriBuilder(BuildBlobUri()) { Query = "comp=copy" }.Uri;
        using HttpRequestMessage request = CreateRequest(HttpMethod.Put, destination);
        request.Headers.TryAddWithoutValidation("x-ms-copy-source", source.AbsoluteUri);

        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);
        await WriteOutput(
            new StorageBlobCopyResult(
                BuildBlobUri().AbsoluteUri,
                Header(response, "x-ms-copy-id"),
                Header(response, "x-ms-copy-status"),
                response.Headers.ETag?.Tag,
                Header(response, "Last-Modified")),
            Publisher.Token);
    }
}
