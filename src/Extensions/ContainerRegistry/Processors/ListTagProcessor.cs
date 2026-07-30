// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Lists tags in a repository with full pagination.
/// CLI: az acr tag list --registry myregistry --repository myrepo
/// PS:  Get-AzContainerRegistryTag -RegistryName myregistry -RepositoryName myrepo
/// </summary>
public class ListTagProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(RegistryName);
            string scope = $"repository:{RepositoryName}:metadata_read";

            List<string> pages = await SendAsyncPaged(loginServer, $"/acr/v1/{RepositoryName}/_tags", scope);

            var allTags = new List<TagAttributeBase>();
            string? registry = null;
            string? imageName = null;

            foreach (string page in pages)
            {
                var result = JsonSerializer.Deserialize<TagList>(page, JsonOptions);
                if (result is not null)
                {
                    registry ??= result.Registry;
                    imageName ??= result.ImageName;
                    if (result.Tags is not null)
                    {
                        allTags.AddRange(result.Tags);
                    }
                }
            }

            await WriteOutput(new TagList
            {
                Registry = registry,
                ImageName = imageName,
                Tags = allTags.ToArray()
            });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
