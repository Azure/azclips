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

    protected override string GetLoginServer() => ResolveLoginServer(Name);
    protected override string GetScope() => "registry:catalog:*";

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string loginServer = GetLoginServer();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{loginServer}/acr/v1/_catalog?n=1");
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            using HttpResponseMessage response = await SendAsync();
            string loginServer = GetLoginServer();
            await WriteOutput(new { LoginServer = loginServer, Authenticated = true });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
