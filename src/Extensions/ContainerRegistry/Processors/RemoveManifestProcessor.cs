// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a manifest from a repository.
/// CLI: az acr manifest delete --name myregistry --repository myrepo --reference sha256:abc123
/// PS:  Remove-AzContainerRegistryManifest -RegistryName myregistry -RepositoryName myrepo -Reference sha256:abc123
/// </summary>
public class RemoveManifestProcessor : AcrProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The manifest reference (tag or digest).", SerializedName = "Reference")]
    public string Reference { get; set; } = "";

    public override async Task ProcessAsync()
    {
        EnsureRuntimeContext();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(RegistryName);
            string scope = $"repository:{RepositoryName}:delete,metadata_read";

            using var response = await SendAcrRequestAsync(
                HttpMethod.Delete, loginServer, $"/acr/v1/{RepositoryName}/_manifests/{Reference}", scope);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
