// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class NewNetworkSecurityGroupProcessor : ComputeProcessorBase<NetworkSecurityGroup>
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> RuleNames { get; set; } = [];
    public List<string> RuleDirections { get; set; } = [];
    public List<string> RuleProtocols { get; set; } = [];
    public List<int> RulePriorities { get; set; } = [];
    public List<string> RuleSourceAddressPrefixes { get; set; } = [];
    public List<string> RuleSourcePortRanges { get; set; } = [];
    public List<string> RuleDestinationPortRanges { get; set; } = [];
    public List<string> RuleDestinationAddressPrefixes { get; set; } = [];
    public List<string> RuleAccesses { get; set; } = [];

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{Name}?api-version=2025-05-01";
        NetworkSecurityGroup body = new NetworkSecurityGroup
        {
            Location = Location,
            Properties = new NetworkSecurityGroupProperties
            {
                FlushConnection = false,
                SecurityRules = RuleNames.Select((name, index) => new SecurityRule
                {
                    Name = name,
                    Properties = new SecurityRuleProperties
                    {
                        Direction = RuleDirections.ElementAtOrDefault(index) ?? string.Empty,
                        Protocol = RuleProtocols.ElementAtOrDefault(index) ?? string.Empty,
                        Priority = RulePriorities.ElementAtOrDefault(index),
                        SourceAddressPrefix = RuleSourceAddressPrefixes.ElementAtOrDefault(index) ?? string.Empty,
                        SourcePortRange = RuleSourcePortRanges.ElementAtOrDefault(index) ?? string.Empty,
                        DestinationPortRange = RuleDestinationPortRanges.ElementAtOrDefault(index) ?? string.Empty,
                        DestinationAddressPrefix = RuleDestinationAddressPrefixes.ElementAtOrDefault(index) ?? string.Empty,
                        Access = RuleAccesses.ElementAtOrDefault(index) ?? string.Empty
                    }
                }).ToList()
            }
        };
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    /*
        Yabo:
        swagger says:         
        "x-ms-long-running-operation-options": {
          "final-state-via": "azure-async-operation",
          "final-state-schema": "#/definitions/Common.VirtualNetwork"
        }
        but got nothing, fallback to no finaluri
    */
    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}



