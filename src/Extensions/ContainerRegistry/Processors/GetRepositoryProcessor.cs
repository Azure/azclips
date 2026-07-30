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
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "Repository")]
    public string Repository { get; set; } = "";

    protected override string GetScope() => $"repository:{Repository}:metadata_read";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = GetLoginServer();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/{Repository}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
