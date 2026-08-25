// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Lists manifests in a repository with full pagination.
/// CLI: az acr manifest list --registry myregistry --repository myrepo
/// PS:  Get-AzContainerRegistryManifest -RegistryName myregistry -RepositoryName myrepo
/// </summary>
public class ListManifestProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = AcrTokenProvider.ResolveLoginServer(Endpoint);

            List<string> pages = await SendAsyncPaged(loginServer, $"/acr/v1/{RepositoryName}/_manifests");

            var allManifests = new List<ManifestAttributeBase>();
            string? registry = null;
            string? imageName = null;

            foreach (string page in pages)
            {
                var result = JsonSerializer.Deserialize<ManifestList>(page, JsonOptions);
                if (result is not null)
                {
                    registry ??= result.Registry;
                    imageName ??= result.ImageName;
                    if (result.Manifests is not null)
                    {
                        allManifests.AddRange(result.Manifests);
                    }
                }
            }

            await WriteOutput(new ManifestList
            {
                Registry = registry,
                ImageName = imageName,
                Manifests = allManifests.ToArray()
            });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
