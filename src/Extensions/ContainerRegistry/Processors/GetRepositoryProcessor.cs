// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a repository in a container registry.
/// CLI: az acr repository show --name myregistry --repository myrepo
/// PS:  Get-AzContainerRegistryRepository -RegistryName myregistry -Repository myrepo
/// </summary>
public class GetRepositoryProcessor : AcrProcessorBase<RepositoryAttributes>
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "Repository")]
    public string Repository { get; set; } = "";

    protected override Task<(HttpMethod Method, string Path, string Scope, HttpContent? Body)> CreateAcrRequest()
    {
        return Task.FromResult<(HttpMethod, string, string, HttpContent?)>(
            (HttpMethod.Get, $"/acr/v1/{Repository}", $"repository:{Repository}:metadata_read", null));
    }
}
