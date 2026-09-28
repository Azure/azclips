// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    public class NewVMProcessor_Orchestrator : ComputeProcessorBase<VirtualMachine, VirtualMachine>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        [PopulatorCommand("az group list")]
        public string ResourceGroupName { get; set; } = string.Empty;

        public string VmSize { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public string AdminUsername { get; set; } = string.Empty;

        [ParameterGroup("Authentication Arguments")]
        public string AdminPassword { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string NetworkInterfaceName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string VirtualNetworkName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public string NetworkSecurityGroupName { get; set; } = string.Empty;

        [ParameterGroup("Network Arguments")]
        public List<string> SubnetNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<List<string>> SubnetAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> AddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDirections { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleProtocols { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<int> RulePriorities { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourceAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourcePortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationPortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleAccesses { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public bool AcceleratedNetworking { get; set; }

        /// <summary>
        /// <c>--public-ip-address</c> / <c>-PublicIpAddressName</c>, passed through to
        /// <see cref="NewNetworkInterfaceProcessor_Orchestrator.PublicIpAddress"/>. Null (omitted)
        /// creates or updates "{Name}PublicIP", legacy Azure CLI's default.
        /// </summary>
        [ParameterGroup("Network Arguments")]
        public string? PublicIpAddress { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
             NewNetworkInterfaceProcessor_Orchestrator nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>(p =>
            {
                p.SubscriptionId = SubscriptionId;
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
                p.AcceleratedNetworking = AcceleratedNetworking;
                p.PublicIpAddress = PublicIpAddress;
                p.DefaultPublicIpAddressName = $"{Name}PublicIP";
            });
            using HttpResponseMessage nicResponse = await nicProcessor.SendAsync();
            NetworkInterface nic = (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;

            NewVMProcessor vmProcessor = CreateExtendedProcessor<NewVMProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
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