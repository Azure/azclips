// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobExistsProcessor_Orchestrator : StorageBlobProcessorBase<object, object>
{
    private bool _exists;

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;

    protected override Task BeforeProcessAsync()
    {
        if (ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required for authenticated Storage requests.");
        }
        return base.BeforeProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        GetStorageBlobPropertiesProcessor processor =
            CreateExtendedProcessor<GetStorageBlobPropertiesProcessor>(p =>
                p.Url = StorageBlobCustomizationHelpers.BuildBlobUrl(
                    AccountName,
                    ContainerName,
                    BlobName));
        HttpResponseMessage response = await processor.SendAsync(ensureSuccess: false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _exists = false;
            response.Content?.Dispose();
            response.Content = null;
            return response;
        }
        try
        {
            await response.EnsureSuccessAsync(Publisher);
            _exists = true;
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    protected override Task BeforeWriteOutputAsync(object? body, ref bool returnNow)
    {
        returnNow = true;
        return WriteOutput(_exists, Publisher.Token);
    }
}
