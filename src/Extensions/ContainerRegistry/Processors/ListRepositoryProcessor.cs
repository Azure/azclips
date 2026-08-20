// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Lists repositories in a container registry with full pagination.
/// CLI: az acr repository list --registry myregistry
/// PS:  Get-AzContainerRegistryRepository -RegistryName myregistry
/// </summary>
public class ListRepositoryProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    protected override string GetLoginServer() => ResolveLoginServer(RegistryName);
    protected override string GetScope() => "registry:catalog:*";

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = GetLoginServer();
            string scope = GetScope();

            List<string> pages = await SendAsyncPaged(loginServer, "/acr/v1/_catalog", scope);

            var allRepositories = new List<string>();
            foreach (string page in pages)
            {
                var result = JsonSerializer.Deserialize<RepositoryList>(page, JsonOptions);
                if (result?.Repositories is not null)
                {
                    allRepositories.AddRange(result.Repositories);
                }
            }

            await WriteOutput(new RepositoryList { Repositories = allRepositories.ToArray() });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
