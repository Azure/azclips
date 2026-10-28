// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkSecurityGroupProcessor : ComputeProcessorBase<NetworkSecurityGroup, NetworkSecurityGroup>
{
    public string SubscriptionId { get; set; } = string.Empty;
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
    public Dictionary<string, string>? Tags { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{Name}?api-version=2025-05-01";

    protected override NetworkSecurityGroup? CreateRequestBody()
    {
        NetworkSecurityGroup body = new NetworkSecurityGroup
        {
            Location = Location,
            Tags = Tags,
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
        return body;
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



