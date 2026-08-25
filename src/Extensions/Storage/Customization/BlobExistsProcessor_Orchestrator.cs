// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobExistsProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            GetStorageBlobPropertiesProcessor processor =
                CreateExtendedProcessor<GetStorageBlobPropertiesProcessor>(p =>
                    p.Url = StorageBlobCustomizationHelpers.BuildBlobUrl(
                        AccountName,
                        ContainerName,
                        BlobName));
            try
            {
                using HttpResponseMessage response = await processor.SendAsync();
                await WriteOutput(true, Publisher.Token);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                await WriteOutput(false, Publisher.Token);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
