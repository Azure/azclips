using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors
{
    public class NewVMProcessor_NICCombined : ComputeProcessorBase
    {
        public string SubscriptionId { get; set; } = string.Empty;
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

        public override async Task<HttpResponseMessage> SendAsyncLRO()
        {
             NewNetworkInterfaceProcessor_Combined nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Combined>(p =>
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
            });
            using HttpResponseMessage nicResponse = await nicProcessor.SendAsyncLRO();
            await EventListener.Signal(Events.TracingHttpResponseMessage, nicResponse);
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
            return await vmProcessor.SendAsyncLRO();
        }

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            SubscriptionId = SubscriptionId ?? AuthContext.SubscriptionId ?? throw new InvalidOperationException($"SubscriptionId must be provided either via property or auth context.");
            using HttpResponseMessage vmResponse = await SendAsyncLRO();
            await EventListener.Signal(Events.TracingHttpResponseMessage, vmResponse);
            VirtualMachine vm = (await vmResponse.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions))!;
            await WriteOutput(vm);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}