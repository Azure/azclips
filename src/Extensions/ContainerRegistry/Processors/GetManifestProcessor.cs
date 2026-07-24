// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a manifest in a repository.
/// CLI: az acr manifest show --name myregistry --repository myrepo --reference sha256:abc123
/// PS:  Get-AzContainerRegistryManifest -RegistryName myregistry -RepositoryName myrepo -Reference sha256:abc123
/// </summary>
public class GetManifestProcessor : AcrProcessorBase<ManifestAttributes>
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The manifest reference (tag or digest).", SerializedName = "Reference")]
    public string Reference { get; set; } = "";

    protected override string GetScope() => $"repository:{RepositoryName}:metadata_read";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = GetLoginServer();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/{RepositoryName}/_manifests/{Reference}");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
