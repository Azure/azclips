// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Deletes a repository from a container registry.
/// CLI: az acr repository delete --name myregistry --repository myrepo
/// PS:  Remove-AzContainerRegistryRepository -RegistryName myregistry -Repository myrepo
/// </summary>
public class RemoveRepositoryProcessor : AcrProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository to delete.", SerializedName = "Repository")]
    public string Repository { get; set; } = "";

    public override async Task ProcessAsync()
    {
        EnsureRuntimeContext();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(RegistryName);
            string scope = $"repository:{Repository}:delete,metadata_read";

            using var response = await SendAcrRequestAsync(
                HttpMethod.Delete, loginServer, $"/acr/v1/{Repository}", scope);

            // DELETE returns the list of deleted manifests/tags
            if (response.Content is not null)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<object>(body, JsonOptions);
                    if (result is not null)
                    {
                        await WriteOutput(result);
                    }
                }
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
