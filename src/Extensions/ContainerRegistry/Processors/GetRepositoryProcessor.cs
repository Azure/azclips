// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

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

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/{Repository}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await AcrTokenProvider.SetupAuthAsync(AuthHandler, JsonOptions,
            AcrTokenProvider.ResolveLoginServer(Endpoint), $"repository:{Repository}:metadata_read");
        await base.ProcessAsync();
    }
}
