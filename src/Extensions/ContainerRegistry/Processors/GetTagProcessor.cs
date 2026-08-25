// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a tag in a repository.
/// CLI: az acr tag show --name myregistry --repository myrepo --tag latest
/// PS:  Get-AzContainerRegistryTag -RegistryName myregistry -RepositoryName myrepo -Tag latest
/// </summary>
public class GetTagProcessor : AcrDataPlaneProcessorBase<TagAttributes>
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The tag name.", SerializedName = "Tag")]
    public string Tag { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/{RepositoryName}/_tags/{Tag}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await AcrTokenProvider.SetupAuthAsync(AuthHandler, JsonOptions,
            AcrTokenProvider.ResolveLoginServer(Endpoint), $"repository:{RepositoryName}:metadata_read");
        await base.ProcessAsync();
    }
}
