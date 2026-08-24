// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a manifest in a repository.
/// CLI: az acr manifest show --name myregistry --repository myrepo --reference sha256:abc123
/// PS:  Get-AzContainerRegistryManifest -RegistryName myregistry -RepositoryName myrepo -Reference sha256:abc123
/// </summary>
public class GetManifestProcessor : AcrDataPlaneProcessorBase<ManifestAttributes>
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The manifest reference (tag or digest).", SerializedName = "Reference")]
    public string Reference { get; set; } = "";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = AcrTokenProvider.ResolveLoginServer(RegistryName);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/{RepositoryName}/_manifests/{Reference}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await AcrTokenProvider.SetupAuthAsync(AuthHandler, JsonOptions,
            AcrTokenProvider.ResolveLoginServer(RegistryName), $"repository:{RepositoryName}:metadata_read");
        await base.ProcessAsync();
    }
}
