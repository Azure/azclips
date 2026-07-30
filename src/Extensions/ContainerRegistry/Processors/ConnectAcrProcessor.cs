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
public class ConnectAcrProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "Name")]
    public string Name { get; set; } = "";

    public string UserName { get; private set; } = "";
    public string Password { get; private set; } = "";

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        /*
            from Yabo:
            TODO: 
            1) Calculate endpoint from registry name (Parameter: Name)
            2) For Yabo: Expose singleton registry from Engine
            3) init token cache (implemented by a separate class)
            4) Replicate https://github.com/Azure/azure-powershell/blob/main/src/ContainerRegistry/ContainerRegistry/Commands/ConnectAzureContainerRegistry.cs#L56-L80
        */
    }
}
