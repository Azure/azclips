// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a repository from a container registry.
/// CLI: az acr repository delete --name myregistry --repository myrepo
/// PS:  Remove-AzContainerRegistryRepository -RegistryName myregistry -Repository myrepo
/// </summary>
public class RemoveRepositoryProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the repository to delete.", SerializedName = "Repository")]
    public string Repository { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"https://{loginServer}/acr/v1/{Repository}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            using HttpResponseMessage response = await SendAsync();

            if (response.Content is not null)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var result = JsonSerializer.Deserialize<object>(body, JsonOptions);
                    if (result is not null)
                    {
                        await WriteOutput(result);
                    }
                }
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
