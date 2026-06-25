// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    public class NewVMProcessor_Orchestrator : ComputeProcessorBase<VirtualMachine>
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string VmSize { get; set; } = string.Empty;
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;

        public string NetworkInterfaceName { get; set; } = string.Empty;
        public string VirtualNetworkName { get; set; } = string.Empty;
        public string NetworkSecurityGroupName { get; set; } = string.Empty;
        public List<string> SubnetNames { get; set; } = [];
        public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
        public List<string> AddressPrefixes { get; set; } = [];
        public List<string> RuleNames { get; set; } = [];
        public List<string> RuleDirections { get; set; } = [];
        public List<string> RuleProtocols { get; set; } = [];
        public List<int> RulePriorities { get; set; } = [];
        public List<string> RuleSourceAddressPrefixes { get; set; } = [];
        public List<string> RuleSourcePortRanges { get; set; } = [];
        public List<string> RuleDestinationPortRanges { get; set; } = [];
        public List<string> RuleDestinationAddressPrefixes { get; set; } = [];
        public List<string> RuleAccesses { get; set; } = [];

        internal override async Task<HttpResponseMessage> SendAsync()
        {
             NewNetworkInterfaceProcessor_Orchestrator nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>(p =>
            {
                p.Name = NetworkInterfaceName;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VirtualNetworkName = VirtualNetworkName;
                p.NetworkSecurityGroupName = NetworkSecurityGroupName;
                p.SubnetNames = SubnetNames;
                p.SubnetAddressPrefixes = SubnetAddressPrefixes;
                p.AddressPrefixes = AddressPrefixes;
                p.RuleNames = RuleNames;
                p.RuleDirections = RuleDirections;
                p.RuleProtocols = RuleProtocols;
                p.RulePriorities = RulePriorities;
                p.RuleSourceAddressPrefixes = RuleSourceAddressPrefixes;
                p.RuleSourcePortRanges = RuleSourcePortRanges;
                p.RuleDestinationPortRanges = RuleDestinationPortRanges;
                p.RuleDestinationAddressPrefixes = RuleDestinationAddressPrefixes;
                p.RuleAccesses = RuleAccesses;
            });
            using HttpResponseMessage nicResponse = await nicProcessor.SendAsync();
            NetworkInterface nic = (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;

            NewVMProcessor vmProcessor = CreateExtendedProcessor<NewVMProcessor>(p =>
            {
                p.Name = Name;
                p.Location = Location;
                p.ResourceGroupName = ResourceGroupName;
                p.VmSize = VmSize;
                p.AdminUsername = AdminUsername;
                p.AdminPassword = AdminPassword;
                p.NetworkInterfaceId = nic.Id;
             });
            return await vmProcessor.SendAsync();
        }
    }
}