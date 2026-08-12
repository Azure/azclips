// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a repository in a container registry.
/// CLI: az acr repository show --name myregistry --repository myrepo
/// PS:  Get-AzContainerRegistryRepository -RegistryName myregistry -Repository myrepo
/// </summary>
public class GetRepositoryProcessor : AcrDataPlaneProcessorBase<RepositoryAttributes>
{
    public string Endpoint { get; set; } = string.Empty;
    
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "Repository")]
    public string Name { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{Endpoint}/acr/v1/{Name}?api-version=2021-07-01");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
