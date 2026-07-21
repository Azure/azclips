// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Validates connectivity and authentication to an Azure Container Registry.
/// CLI: az acr connect --name myregistry
/// PS:  Connect-AzContainerRegistry -Name myregistry
/// </summary>
public class ConnectAcrProcessor : AcrProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "Name")]
    public string Name { get; set; } = "";

    public override async Task ProcessAsync()
    {
        EnsureRuntimeContext();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(Name);
            // Validate by acquiring a token for catalog scope
            string scope = "registry:catalog:*";
            await AcquireAcrAccessTokenAsync(loginServer, scope);

            await WriteOutput(new { LoginServer = loginServer, Authenticated = true });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
