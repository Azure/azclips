// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a manifest from a repository.
/// CLI: az acr manifest delete --name myregistry --repository myrepo --reference sha256:abc123
/// PS:  Remove-AzContainerRegistryManifest -RegistryName myregistry -RepositoryName myrepo -Reference sha256:abc123
/// </summary>
public class RemoveManifestProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The manifest reference (tag or digest).", SerializedName = "Reference")]
    public string Reference { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"https://{loginServer}/v2/{RepositoryName}/manifests/{Reference}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            await AcrTokenProvider.SetupAuthAsync(AuthHandler, JsonOptions,
                AcrTokenProvider.ResolveLoginServer(Endpoint), $"repository:{RepositoryName}:delete");
            using HttpResponseMessage response = await SendAsync();
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
