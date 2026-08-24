// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a tag from a repository.
/// CLI: az acr tag delete --name myregistry --repository myrepo --tag latest
/// PS:  Remove-AzContainerRegistryTag -RegistryName myregistry -RepositoryName myrepo -Tag latest
/// </summary>
public class RemoveTagProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The tag name to delete.", SerializedName = "Tag")]
    public string Tag { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(RegistryName);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"https://{loginServer}/acr/v1/{RepositoryName}/_tags/{Tag}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            await AcrTokenProvider.SetupAuthAsync(AuthHandler, JsonOptions,
                AcrTokenProvider.ResolveLoginServer(RegistryName), $"repository:{RepositoryName}:delete,metadata_read");
            using HttpResponseMessage response = await SendAsync();
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
