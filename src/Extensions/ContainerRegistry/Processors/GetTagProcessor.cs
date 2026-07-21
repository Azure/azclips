// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Gets attributes of a tag in a repository.
/// CLI: az acr tag show --name myregistry --repository myrepo --tag latest
/// PS:  Get-AzContainerRegistryTag -RegistryName myregistry -RepositoryName myrepo -Tag latest
/// </summary>
public class GetTagProcessor : AcrProcessorBase<TagAttributes>
{
    [ArgumentInfo(Required = true, Description = "The name of the repository.", SerializedName = "RepositoryName")]
    public string RepositoryName { get; set; } = "";

    [ArgumentInfo(Required = true, Description = "The tag name.", SerializedName = "Tag")]
    public string Tag { get; set; } = "";

    protected override Task<(HttpMethod Method, string Path, string Scope, HttpContent? Body)> CreateAcrRequest()
    {
        return Task.FromResult<(HttpMethod, string, string, HttpContent?)>(
            (HttpMethod.Get, $"/acr/v1/{RepositoryName}/_tags/{Tag}", $"repository:{RepositoryName}:metadata_read", null));
    }
}
