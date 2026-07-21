// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a tag from a repository.
/// CLI: az acr tag delete --name myregistry --repository myrepo --tag latest
/// PS:  Remove-AzContainerRegistryTag -RegistryName myregistry -RepositoryName myrepo -Tag latest
/// </summary>
public class RemoveTagProcessor : AcrProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The tag name to delete.", SerializedName = "Tag")]
    public string Tag { get; set; } = "";

    public override async Task ProcessAsync()
    {
        EnsureRuntimeContext();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(RegistryName);
            string scope = $"repository:{RepositoryName}:delete,metadata_read";

            using var response = await SendAcrRequestAsync(
                HttpMethod.Delete, loginServer, $"/acr/v1/{RepositoryName}/_tags/{Tag}", scope);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
