// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Record/playback scenarios for the virtual network commands: <c>network vnet create</c>,
/// <c>show</c>, <c>list</c> and <c>delete</c> on the CLI, and <c>New-AzVirtualNetwork</c>,
/// <c>Get-AzVirtualNetwork_Get</c>, <c>Get-AzVirtualNetwork_List</c> and
/// <c>Remove-AzVirtualNetwork</c> on PowerShell.
/// <para>
/// Each fact records once from the CLI and replays the same recording on both surfaces. The
/// PowerShell steps run the real cmdlet classes, so <c>-WhatIf</c>, <c>-Force</c>,
/// <c>-PassThru</c>, aliases and <c>ValidateSet</c> are exercised. The PowerShell-only steps
/// send no request, so one recording serves both surfaces.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class VirtualNetworkScenarioTests
{
    private const string ApiVersion = "2026-03-01";
    private const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    private const string DefaultLocation = "westus2";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>
    /// T1, verifying "T1 Virtual network lifecycle with create defaults": create fails before the
    /// resource group exists, then create with the defaults, show, list by resource group and by
    /// subscription, and delete. PowerShell also covers <c>-WhatIf</c>, the confirmation prompt,
    /// and <c>-Force -PassThru</c>.
    /// </summary>
    [Fact]
    public void VirtualNetworkLifecycleWithCreateDefaults()
    {
        const string GroupBase = "scenario-vnet-rg";
        const string VnetName = "vnet1";

        // The subscription-wide list records every virtual network in the recording subscription.
        // Keep only this scenario's. It runs after the default sanitisers, so the group name has
        // already lost the record run's suffix.
        var sanitizers = RecordingSanitizers.Default.With(
            [new VirtualNetworkListSanitizer($"/resourceGroups/{GroupBase}/providers/Microsoft.Network/virtualNetworks/{VnetName}")]);

        RunScenario(
            nameof(VirtualNetworkLifecycleWithCreateDefaults),
            [GroupBase],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1: create before the resource group exists.
                int before = interceptor.Sent.Count;
                var missing = Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", VnetName, "--subnet-name", "default"));
                Assert.Contains("ResourceGroupNotFound", missing.ToString());
                AssertOnlyResourceGroupGet(SentSince(interceptor, before), c, group);

                // Step 2.
                CreateGroupCli(host, c, group, DefaultLocation);

                // Step 3: create with the defaults.
                before = interceptor.Sent.Count;
                var created = SingleNetwork(host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", VnetName, "--subnet-name", "default"));
                var createRequests = SentSince(interceptor, before);
                AssertResourceGroupGetBeforePut(createRequests, c, group, VnetName);
                AssertCreatedWithDefaults(created);
                var body = PutBody(createRequests, c, group, VnetName);
                Assert.Equal("10.0.0.0/24", body["properties"]!["subnets"]![0]!["properties"]!["addressPrefix"]!.GetValue<string>());
                Assert.Null(body["properties"]!["subnets"]![0]!["properties"]!["addressPrefixes"]);
                Assert.Null(body["tags"]);
                Assert.Null(body["properties"]!["dhcpOptions"]);
                Assert.Null(body["properties"]!["bgpCommunities"]);
                Assert.Null(body["properties"]!["ddosProtectionPlan"]);
                Assert.Null(body["properties"]!["encryption"]);
                Assert.Null(body["extendedLocation"]);

                // Step 4: show.
                var shown = SingleNetwork(host.RunCli(
                    "network", "vnet", "show", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", VnetName));
                Assert.Equal(VnetName, shown.Name);
                Assert.Equal(VirtualNetworkType, shown.Type);
                if (c.Playback)
                {
                    Assert.Equal(created.Id, shown.Id);
                }

                // Step 5: list by resource group.
                before = interceptor.Sent.Count;
                var listed = Networks(host.RunCli(
                    "network", "vnet", "list", "--subscription-id", c.SubscriptionId, "--resource-group", group));
                Assert.Equal(VnetName, Assert.Single(listed).Name);
                AssertFirstRequest(SentSince(interceptor, before), "GET", ResourceGroupListPath(c, group));

                // Step 6: list by subscription, from the active profile's subscription.
                before = interceptor.Sent.Count;
                var all = Networks(host.RunCli("network", "vnet", "list"));
                Assert.Contains(all, vnet => vnet.Id == created.Id);
                Assert.All(all, vnet => Assert.Equal(VirtualNetworkType, vnet.Type));
                AssertFirstRequest(SentSince(interceptor, before), "GET", SubscriptionListPath(c));

                // Step 7: delete.
                Assert.Empty(host.RunCli(
                    "network", "vnet", "delete", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", VnetName));

                // Step 8: list by resource group after the delete.
                Assert.Empty(Networks(host.RunCli(
                    "network", "vnet", "list", "--subscription-id", c.SubscriptionId, "--resource-group", group)));

                // Step 9.
                DeleteGroupCli(host, c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1: create before the resource group exists.
                int before = interceptor.Sent.Count;
                var missing = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName, new() { ["SubnetName"] = "default" })));
                Assert.Contains("ResourceGroupNotFound", missing.ToString());
                AssertOnlyResourceGroupGet(SentSince(interceptor, before), c, group);

                // Step 2.
                CreateGroupPowerShell(host, c, group, DefaultLocation);

                // Step 3: -WhatIf stops before any request.
                before = interceptor.Sent.Count;
                Assert.Empty(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName, new() { ["SubnetName"] = "default", ["WhatIf"] = true })));
                Assert.Empty(SentSince(interceptor, before));

                // Step 4: create with the defaults.
                before = interceptor.Sent.Count;
                var created = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName, new() { ["SubnetName"] = "default" })));
                AssertResourceGroupGetBeforePut(SentSince(interceptor, before), c, group, VnetName);
                AssertCreatedWithDefaults(created);

                // Step 5: the Get set.
                var shown = SingleNetwork(host.RunPowerShellCmdlet<GetVirtualNetworkCmdlet>(VnetParameters(c, group, VnetName)));
                Assert.Equal(VnetName, shown.Name);
                Assert.Equal(VirtualNetworkType, shown.Type);

                // Step 6: the List set with a resource group writes each item, not the list.
                before = interceptor.Sent.Count;
                var listed = host.RunPowerShellCmdlet<GetVirtualNetworkCmdlet>(GroupParameters(c, group));
                Assert.Equal(VnetName, Assert.IsType<VirtualNetwork>(Assert.Single(listed)).Name);
                AssertFirstRequest(SentSince(interceptor, before), "GET", ResourceGroupListPath(c, group));

                // Step 7: the List set with no parameters.
                before = interceptor.Sent.Count;
                var all = host.RunPowerShellCmdlet<GetVirtualNetworkCmdlet>(new Dictionary<string, object>());
                var allNetworks = all.Select(item => Assert.IsType<VirtualNetwork>(item)).ToList();
                Assert.Contains(allNetworks, vnet => vnet.Id == created.Id);
                Assert.All(allNetworks, vnet => Assert.Equal(VirtualNetworkType, vnet.Type));
                AssertFirstRequest(SentSince(interceptor, before), "GET", SubscriptionListPath(c));

                // Step 8: remove -WhatIf.
                before = interceptor.Sent.Count;
                Assert.Empty(host.RunPowerShellCmdlet<RemoveVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName, new() { ["WhatIf"] = true })));
                AssertNoDelete(SentSince(interceptor, before));

                // Step 9: remove without -Force; the runspace has no UI to answer the prompt.
                before = interceptor.Sent.Count;
                Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<RemoveVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName)));
                AssertNoDelete(SentSince(interceptor, before));

                // Step 10: remove -Force -PassThru.
                before = interceptor.Sent.Count;
                var removed = host.RunPowerShellCmdlet<RemoveVirtualNetworkCmdlet>(
                    VnetParameters(c, group, VnetName, new() { ["Force"] = SwitchParameter.Present, ["PassThru"] = SwitchParameter.Present }));
                Assert.True(Assert.IsType<bool>(Assert.Single(removed)));
                Assert.Contains(SentSince(interceptor, before),
                    request => IsMethod(request, "DELETE") && PathAndQueryEquals(request, VnetPath(c, group, VnetName)));

                // Step 11: list by resource group after the remove.
                Assert.Empty(host.RunPowerShellCmdlet<GetVirtualNetworkCmdlet>(GroupParameters(c, group)));

                // Step 12.
                DeleteGroupPowerShell(host, c, group);
            },
            sanitizers);
    }

    /// <summary>
    /// T2, verifying "T2 Create with explicit inputs and inline subnet prefixes": every create
    /// input is sent, an explicit location skips the resource group lookup, and the inline subnet
    /// takes one prefix, the default prefix from a /27 address space, or several prefixes.
    /// </summary>
    [Fact]
    public void CreateWithExplicitInputsAndInlineSubnetPrefixes()
    {
        const string GroupBase = "scenario-vnet-inputs-rg";

        RunScenario(
            nameof(CreateWithExplicitInputsAndInlineSubnetPrefixes),
            [GroupBase],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1.
                CreateGroupCli(host, c, group, DefaultLocation);

                // Step 2: every input.
                int before = interceptor.Sent.Count;
                var inputs = SingleNetwork(host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet-inputs", "--location", DefaultLocation,
                    "--address-prefixes", "20.0.0.0/16", "10.0.0.0/16",
                    "--subnet-name", "subnet1", "--subnet-prefixes", "20.0.0.0/24",
                    "--dns-servers", "8.8.8.8",
                    "--tags", "env=scenario", "owner",
                    "--flowtimeout", "10",
                    "--bgp-community", "12076:20000",
                    "--ddos-protection", "false"));
                var requests = SentSince(interceptor, before);
                AssertPutFirstWithoutResourceGroupGet(requests, c, group, "vnet-inputs");
                var body = PutBody(requests, c, group, "vnet-inputs");
                Assert.Equal(DefaultLocation, body["location"]!.GetValue<string>());
                AssertExplicitInputs(inputs);
                Assert.Equal("20.0.0.0/24", body["properties"]!["subnets"]![0]!["properties"]!["addressPrefix"]!.GetValue<string>());
                Assert.Null(body["properties"]!["subnets"]![0]!["properties"]!["addressPrefixes"]);
                Assert.False(body["properties"]!["enableDdosProtection"]!.GetValue<bool>());

                // Step 3: the default subnet prefix keeps a mask longer than /24.
                before = interceptor.Sent.Count;
                var small = SingleNetwork(host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet-small", "--location", DefaultLocation,
                    "--address-prefixes", "10.2.0.0/27", "--subnet-name", "subnet1"));
                AssertSingleSubnet(small, "subnet1", "10.2.0.0/27");
                body = PutBody(SentSince(interceptor, before), c, group, "vnet-small");
                Assert.Equal("10.2.0.0/27", body["properties"]!["subnets"]![0]!["properties"]!["addressPrefix"]!.GetValue<string>());

                // Step 4: several subnet prefixes.
                before = interceptor.Sent.Count;
                var multi = SingleNetwork(host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet-multi", "--location", DefaultLocation,
                    "--address-prefixes", "10.1.0.0/16", "--subnet-name", "subnet1",
                    "--subnet-prefixes", "10.1.0.0/24", "10.1.1.0/24"));
                AssertSeveralSubnetPrefixesSent(PutBody(SentSince(interceptor, before), c, group, "vnet-multi"));
                AssertSeveralSubnetPrefixes(multi);

                // Step 5.
                Assert.Empty(DeleteVnetCli(host, c, group, "vnet-inputs"));

                // Steps 6 and 7.
                DeleteVnetCli(host, c, group, "vnet-small");
                DeleteVnetCli(host, c, group, "vnet-multi");

                // Step 8.
                DeleteGroupCli(host, c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1.
                CreateGroupPowerShell(host, c, group, DefaultLocation);

                // Step 2: every input.
                int before = interceptor.Sent.Count;
                var inputs = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet-inputs", new()
                    {
                        ["Location"] = DefaultLocation,
                        ["AddressPrefixes"] = new[] { "20.0.0.0/16", "10.0.0.0/16" },
                        ["SubnetName"] = "subnet1",
                        ["SubnetPrefixes"] = new[] { "20.0.0.0/24" },
                        ["DnsServers"] = new[] { "8.8.8.8" },
                        ["Tag"] = new Hashtable { ["env"] = "scenario", ["owner"] = "" },
                        ["FlowTimeoutInMinutes"] = 10,
                        ["BgpCommunity"] = "12076:20000",
                        ["EnableDdosProtection"] = new SwitchParameter(false),
                    })));
                var requests = SentSince(interceptor, before);
                AssertPutFirstWithoutResourceGroupGet(requests, c, group, "vnet-inputs");
                AssertExplicitInputs(inputs);
                Assert.False(PutBody(requests, c, group, "vnet-inputs")["properties"]!["enableDdosProtection"]!.GetValue<bool>());

                // Step 3.
                var small = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet-small", new()
                    {
                        ["Location"] = DefaultLocation,
                        ["AddressPrefixes"] = new[] { "10.2.0.0/27" },
                        ["SubnetName"] = "subnet1",
                    })));
                AssertSingleSubnet(small, "subnet1", "10.2.0.0/27");

                // Step 4.
                before = interceptor.Sent.Count;
                var multi = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet-multi", new()
                    {
                        ["Location"] = DefaultLocation,
                        ["AddressPrefixes"] = new[] { "10.1.0.0/16" },
                        ["SubnetName"] = "subnet1",
                        ["SubnetPrefixes"] = new[] { "10.1.0.0/24", "10.1.1.0/24" },
                    })));
                AssertSeveralSubnetPrefixesSent(PutBody(SentSince(interceptor, before), c, group, "vnet-multi"));
                AssertSeveralSubnetPrefixes(multi);

                // Step 5: -Force without -PassThru writes nothing.
                Assert.Empty(RemoveVnetPowerShell(host, c, group, "vnet-inputs"));

                // Steps 6 and 7.
                RemoveVnetPowerShell(host, c, group, "vnet-small");
                RemoveVnetPowerShell(host, c, group, "vnet-multi");

                // Step 8.
                DeleteGroupPowerShell(host, c, group);
            });
    }

    /// <summary>
    /// T3, verifying "T3 Encryption is sent only with EnableEncryption": the enforcement policy is
    /// sent only together with EnableEncryption, the service rejects encryption without a policy,
    /// and a policy outside the allowed values fails before any request.
    /// </summary>
    [Fact]
    public void EncryptionIsSentOnlyWithEnableEncryption()
    {
        const string GroupBase = "scenario-vnet-encryption-rg";

        RunScenario(
            nameof(EncryptionIsSentOnlyWithEnableEncryption),
            [GroupBase],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                string[] Create(string name, params string[] extra) =>
                [
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", name, .. extra,
                ];
                string[] WithSubnet(params string[] extra) =>
                    ["--address-prefixes", "10.0.0.0/16", "--subnet-name", "Mysubnet", "--subnet-prefixes", "10.0.0.0/24", .. extra];

                // Step 1.
                CreateGroupCli(host, c, group, DefaultLocation);

                // Step 2.
                int before = interceptor.Sent.Count;
                var vnet1 = SingleNetwork(host.RunCli(Create("MyVnet1",
                    WithSubnet("--enable-encryption", "true", "--encryption-enforcement-policy", "allowUnencrypted"))));
                var body = PutBody(SentSince(interceptor, before), c, group, "MyVnet1");
                Assert.True(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Equal("allowUnencrypted", body["properties"]!["encryption"]!["enforcement"]!.GetValue<string>());
                Assert.True(vnet1.EncryptionEnabled);
                Assert.Equal("AllowUnencrypted", vnet1.EncryptionEnforcement);

                // Step 3: the recording subscription lacks the AllowDropUnecryptedVnet feature, so the
                // service rejects the policy after the client sends it.
                before = interceptor.Sent.Count;
                var dropRejected = Assert.ThrowsAny<Exception>(() => host.RunCli(Create("MyVnet2",
                    WithSubnet("--enable-encryption", "true", "--encryption-enforcement-policy", "dropUnencrypted"))));
                Assert.Contains("CannotUseDropUnencryptedForVnet", dropRejected.ToString());
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet2");
                Assert.True(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Equal("dropUnencrypted", body["properties"]!["encryption"]!["enforcement"]!.GetValue<string>());

                // Step 4.
                before = interceptor.Sent.Count;
                var vnet3 = SingleNetwork(host.RunCli(Create("MyVnet3", WithSubnet("--enable-encryption", "false"))));
                Assert.Equal("Succeeded", vnet3.ProvisioningState);
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet3");
                Assert.False(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Null(body["properties"]!["encryption"]!["enforcement"]);

                // Step 5: encryption without a policy is rejected by the service.
                before = interceptor.Sent.Count;
                var rejected = Assert.ThrowsAny<Exception>(() => host.RunCli(Create("MyVnet4", WithSubnet("--enable-encryption", "true"))));
                Assert.Contains("MustSpecifyBothEncryptionIntentAndEnforcementPolicy", rejected.ToString());
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet4");
                Assert.True(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Null(body["properties"]!["encryption"]!["enforcement"]);

                // Step 6: a policy without EnableEncryption is not sent.
                before = interceptor.Sent.Count;
                var vnet5 = SingleNetwork(host.RunCli(Create("MyVnet5", "--encryption-enforcement-policy", "DropUnencrypted")));
                Assert.Equal("Succeeded", vnet5.ProvisioningState);
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet5");
                Assert.Null(body["properties"]!["encryption"]);
                Assert.Null(body["properties"]!["subnets"]);

                // Step 7: a value outside the allowed values.
                before = interceptor.Sent.Count;
                var invalid = Assert.ThrowsAny<Exception>(() => host.RunCli(Create("MyVnet6",
                    "--enable-encryption", "true", "--encryption-enforcement-policy", "Invalid")));
                Assert.Contains("not allowed", invalid.ToString());
                Assert.Empty(SentSince(interceptor, before));

                // Steps 8 to 11.
                DeleteVnetCli(host, c, group, "MyVnet1");
                DeleteVnetCli(host, c, group, "MyVnet3");
                DeleteVnetCli(host, c, group, "MyVnet5");

                // Step 12.
                DeleteGroupCli(host, c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                Dictionary<string, object> WithSubnet(Dictionary<string, object> extra)
                {
                    extra["AddressPrefixes"] = new[] { "10.0.0.0/16" };
                    extra["SubnetName"] = "Mysubnet";
                    extra["SubnetPrefixes"] = new[] { "10.0.0.0/24" };
                    return extra;
                }

                // Step 1.
                CreateGroupPowerShell(host, c, group, DefaultLocation);

                // Step 2.
                int before = interceptor.Sent.Count;
                var vnet1 = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "MyVnet1",
                    WithSubnet(new() { ["EnableEncryption"] = SwitchParameter.Present, ["EncryptionEnforcement"] = "allowUnencrypted" }))));
                var body = PutBody(SentSince(interceptor, before), c, group, "MyVnet1");
                Assert.True(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Equal("allowUnencrypted", body["properties"]!["encryption"]!["enforcement"]!.GetValue<string>());
                Assert.True(vnet1.EncryptionEnabled);
                Assert.Equal("AllowUnencrypted", vnet1.EncryptionEnforcement);

                // Step 3: the EncryptionEnforcementPolicy alias; the service rejects the policy.
                before = interceptor.Sent.Count;
                var dropRejected = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "MyVnet2",
                    WithSubnet(new() { ["EnableEncryption"] = SwitchParameter.Present, ["EncryptionEnforcementPolicy"] = "dropUnencrypted" }))));
                Assert.Contains("CannotUseDropUnencryptedForVnet", dropRejected.ToString());
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet2");
                Assert.Equal("dropUnencrypted", body["properties"]!["encryption"]!["enforcement"]!.GetValue<string>());

                // Step 4: -EnableEncryption:$false is bound and sent.
                before = interceptor.Sent.Count;
                var vnet3 = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "MyVnet3",
                    WithSubnet(new() { ["EnableEncryption"] = new SwitchParameter(false) }))));
                Assert.Equal("Succeeded", vnet3.ProvisioningState);
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet3");
                Assert.False(body["properties"]!["encryption"]!["enabled"]!.GetValue<bool>());
                Assert.Null(body["properties"]!["encryption"]!["enforcement"]);

                // Step 5.
                var rejected = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "MyVnet4",
                    WithSubnet(new() { ["EnableEncryption"] = SwitchParameter.Present }))));
                Assert.Contains("MustSpecifyBothEncryptionIntentAndEnforcementPolicy", rejected.ToString());

                // Step 6.
                before = interceptor.Sent.Count;
                var vnet5 = SingleNetwork(host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "MyVnet5",
                    new() { ["EncryptionEnforcement"] = "DropUnencrypted" })));
                Assert.Equal("Succeeded", vnet5.ProvisioningState);
                body = PutBody(SentSince(interceptor, before), c, group, "MyVnet5");
                Assert.Null(body["properties"]!["encryption"]);
                Assert.Null(body["properties"]!["subnets"]);

                // Step 7: the ValidateSet rejects the value at binding.
                before = interceptor.Sent.Count;
                var invalid = Assert.ThrowsAny<ParameterBindingException>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "MyVnet6",
                        new() { ["EnableEncryption"] = SwitchParameter.Present, ["EncryptionEnforcement"] = "Invalid" })));
                Assert.Equal("EncryptionEnforcement", invalid.ParameterName);
                Assert.Empty(SentSince(interceptor, before));

                // Steps 8 to 11.
                RemoveVnetPowerShell(host, c, group, "MyVnet1");
                RemoveVnetPowerShell(host, c, group, "MyVnet3");
                RemoveVnetPowerShell(host, c, group, "MyVnet5");

                // Step 12.
                DeleteGroupPowerShell(host, c, group);
            });
    }

    /// <summary>
    /// T4, verifying "T4 EdgeZone is sent as an extended location". The recording subscription has
    /// no edge-zone access, so the test checks the sent body and the UnsupportedEdgeZone error.
    /// </summary>
    [Fact]
    public void EdgeZoneIsSentAsExtendedLocation()
    {
        const string GroupBase = "scenario-vnet-edgezone-rg";
        const string Location = "westus";
        const string EdgeZone = "microsoftlosangeles1";

        RunScenario(
            nameof(EdgeZoneIsSentAsExtendedLocation),
            [GroupBase],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1.
                CreateGroupCli(host, c, group, Location);

                // Step 2.
                int before = interceptor.Sent.Count;
                var rejected = Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet1", "--location", Location,
                    "--edge-zone", EdgeZone, "--subnet-name", "subnet1"));
                Assert.Contains("UnsupportedEdgeZone", rejected.ToString());
                var body = PutBody(SentSince(interceptor, before), c, group, "vnet1");
                Assert.Equal(EdgeZone, body["extendedLocation"]!["name"]!.GetValue<string>());
                Assert.Equal("EdgeZone", body["extendedLocation"]!["type"]!.GetValue<string>());

                // Step 3.
                DeleteGroupCli(host, c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);

                // Step 1.
                CreateGroupPowerShell(host, c, group, Location);

                // Step 2.
                int before = interceptor.Sent.Count;
                var rejected = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(VnetParameters(c, group, "vnet1",
                    new() { ["Location"] = Location, ["EdgeZone"] = EdgeZone, ["SubnetName"] = "subnet1" })));
                Assert.Contains("UnsupportedEdgeZone", rejected.ToString());
                var body = PutBody(SentSince(interceptor, before), c, group, "vnet1");
                Assert.Equal(EdgeZone, body["extendedLocation"]!["name"]!.GetValue<string>());

                // Step 3.
                DeleteGroupPowerShell(host, c, group);
            });
    }

    /// <summary>
    /// T5, verifying "T5 DDoS protection plan by name or resource ID": a plan name is expanded to
    /// a resource ID in the virtual network's resource group, and a full resource ID is sent
    /// unchanged. Neither plan exists, so both creates fail at the service.
    /// </summary>
    /// <remarks>
    /// The service error for a nonexistent plan is not known before recording; pin the failure
    /// assertions to the recorded error code at review.
    /// </remarks>
    [Fact]
    public void DdosProtectionPlanByNameOrResourceId()
    {
        const string GroupBase = "scenario-vnet-ddos-rg";
        const string OtherGroupBase = "scenario-other-rg";
        const string PlanName = "scenario-missing-plan";

        static string PlanId(ScenarioContext c, string group) =>
            $"/subscriptions/{c.SubscriptionId}/resourceGroups/{group}/providers/Microsoft.Network/ddosProtectionPlans/{PlanName}";

        RunScenario(
            nameof(DdosProtectionPlanByNameOrResourceId),
            [GroupBase],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                string otherPlanId = PlanId(c, c.Group(OtherGroupBase));

                // Step 1.
                CreateGroupCli(host, c, group, DefaultLocation);

                // Step 2: by name.
                int before = interceptor.Sent.Count;
                Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet-ddos-name", "--ddos-protection-plan", PlanName));
                var body = PutBody(SentSince(interceptor, before), c, group, "vnet-ddos-name");
                if (c.Playback)
                {
                    Assert.Equal(PlanId(c, group), body["properties"]!["ddosProtectionPlan"]!["id"]!.GetValue<string>());
                }

                // Step 3: by resource ID.
                before = interceptor.Sent.Count;
                Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet-ddos-id", "--ddos-protection-plan", otherPlanId));
                body = PutBody(SentSince(interceptor, before), c, group, "vnet-ddos-id");
                if (c.Playback)
                {
                    Assert.Equal(otherPlanId, body["properties"]!["ddosProtectionPlan"]!["id"]!.GetValue<string>());
                }

                // Step 4.
                DeleteGroupCli(host, c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                string otherPlanId = PlanId(c, c.Group(OtherGroupBase));

                // Step 1.
                CreateGroupPowerShell(host, c, group, DefaultLocation);

                // Step 2: by name.
                int before = interceptor.Sent.Count;
                Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet-ddos-name", new() { ["DdosProtectionPlan"] = PlanName })));
                var body = PutBody(SentSince(interceptor, before), c, group, "vnet-ddos-name");
                if (c.Playback)
                {
                    Assert.Equal(PlanId(c, group), body["properties"]!["ddosProtectionPlan"]!["id"]!.GetValue<string>());
                }

                // Step 3: by resource ID, through the DdosProtectionPlanId alias.
                before = interceptor.Sent.Count;
                Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet-ddos-id", new() { ["DdosProtectionPlanId"] = otherPlanId })));
                body = PutBody(SentSince(interceptor, before), c, group, "vnet-ddos-id");
                if (c.Playback)
                {
                    Assert.Equal(otherPlanId, body["properties"]!["ddosProtectionPlan"]!["id"]!.GetValue<string>());
                }

                // Step 4.
                DeleteGroupPowerShell(host, c, group);
            });
    }

    /// <summary>
    /// T6, verifying "T6 Resource group location lookup failure stops the create": without a
    /// location, the failed resource group read ends the create before any PUT.
    /// </summary>
    [Fact]
    public void ResourceGroupLocationLookupFailureStopsCreate()
    {
        const string GroupBase = "scenario-vnet-missing-rg";

        RunScenario(
            nameof(ResourceGroupLocationLookupFailureStopsCreate),
            [],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                int before = interceptor.Sent.Count;
                var error = Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet1"));
                Assert.Contains("ResourceGroupNotFound", error.ToString());
                AssertOnlyResourceGroupGet(SentSince(interceptor, before), c, group);
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                int before = interceptor.Sent.Count;
                var error = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet1")));
                Assert.Contains("ResourceGroupNotFound", error.ToString());
                Assert.DoesNotContain(SentSince(interceptor, before), request => IsMethod(request, "PUT"));
            });
    }

    /// <summary>
    /// T7, verifying "T7 Explicit location is sent without a resource group lookup": with a
    /// location the PUT goes first, and the missing resource group is reported by the PUT.
    /// </summary>
    [Fact]
    public void ExplicitLocationIsSentWithoutResourceGroupLookup()
    {
        const string GroupBase = "scenario-vnet-missing-rg";

        RunScenario(
            nameof(ExplicitLocationIsSentWithoutResourceGroupLookup),
            [],
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                int before = interceptor.Sent.Count;
                var error = Assert.ThrowsAny<Exception>(() => host.RunCli(
                    "network", "vnet", "create", "--subscription-id", c.SubscriptionId,
                    "--resource-group", group, "--name", "vnet1", "--location", DefaultLocation));
                var requests = SentSince(interceptor, before);
                AssertPutFirstWithoutResourceGroupGet(requests, c, group, "vnet1");
                var body = PutBody(requests, c, group, "vnet1");
                Assert.Equal(DefaultLocation, body["location"]!.GetValue<string>());
                Assert.Equal(["10.0.0.0/16"], Strings(body["properties"]!["addressSpace"]!["addressPrefixes"]));
                Assert.Contains("ResourceGroupNotFound", error.ToString());
            },
            (host, interceptor, c) =>
            {
                string group = c.Group(GroupBase);
                int before = interceptor.Sent.Count;
                var error = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<NewVirtualNetworkCmdlet>(
                    VnetParameters(c, group, "vnet1", new() { ["Location"] = DefaultLocation })));
                AssertPutFirstWithoutResourceGroupGet(SentSince(interceptor, before), c, group, "vnet1");
                Assert.Contains("ResourceGroupNotFound", error.ToString());
            });
    }

    // ---- Scenario runner ----

    /// <summary>The run's subscription and mode, as each step sees them.</summary>
    private sealed record ScenarioContext(string SubscriptionId, RecorderMode Mode)
    {
        public bool Playback => Mode != RecorderMode.Record;

        public string Group(string baseName) => TestRun.ScopedName(baseName, Mode);
    }

    /// <summary>
    /// Records <paramref name="cli"/> from the CLI with live auth, or replays the recording through
    /// <paramref name="cli"/> and then <paramref name="powerShell"/>. In record mode the resource
    /// groups in <paramref name="groups"/> are deleted after the save, so the cleanup never lands
    /// in the recording.
    /// </summary>
    private static void RunScenario(
        string testName,
        string[] groups,
        Action<ScenarioHost, RecordPlaybackInterceptor, ScenarioContext> cli,
        Action<ScenarioHost, RecordPlaybackInterceptor, ScenarioContext> powerShell,
        RecordingSanitizers? sanitizers = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VirtualNetworkScenarioTests), testName, sanitizers: sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: s_extensions);
            var live = new ScenarioContext(host.LiveSubscriptionId(), recording.Mode);
            try
            {
                cli(host, interceptor, live);
                recording.Save();
            }
            finally
            {
                foreach (var group in groups)
                {
                    try { DeleteGroupCli(host, live, live.Group(group)); } catch { }
                }
            }
            return;
        }

        var playback = new ScenarioContext(RecordingSanitizers.PlaceholderGuid, recording.Mode);

        var cliInterceptor = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.CLI, cliInterceptor, extensions: s_extensions))
        {
            cli(host, cliInterceptor, playback);
        }

        var psInterceptor = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.PowerShell, psInterceptor, extensions: s_extensions))
        {
            powerShell(host, psInterceptor, playback);
        }
    }

    // ---- Commands ----

    private static void CreateGroupCli(ScenarioHost host, ScenarioContext c, string group, string location) =>
        host.RunCli("group", "create", "--subscription-id", c.SubscriptionId, "--name", group, "--location", location);

    private static void DeleteGroupCli(ScenarioHost host, ScenarioContext c, string group) =>
        host.RunCli("group", "delete", "--subscription-id", c.SubscriptionId, "--name", group);

    private static IReadOnlyList<object> DeleteVnetCli(ScenarioHost host, ScenarioContext c, string group, string name) =>
        host.RunCli("network", "vnet", "delete", "--subscription-id", c.SubscriptionId, "--resource-group", group, "--name", name);

    private static void CreateGroupPowerShell(ScenarioHost host, ScenarioContext c, string group, string location) =>
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = c.SubscriptionId,
            ["Name"] = group,
            ["Location"] = location,
        }, extension: "Resources");

    private static void DeleteGroupPowerShell(ScenarioHost host, ScenarioContext c, string group) =>
        host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = c.SubscriptionId,
            ["Name"] = group,
        }, extension: "Resources");

    private static IReadOnlyList<object> RemoveVnetPowerShell(ScenarioHost host, ScenarioContext c, string group, string name) =>
        host.RunPowerShellCmdlet<RemoveVirtualNetworkCmdlet>(VnetParameters(c, group, name, new() { ["Force"] = SwitchParameter.Present }));

    /// <summary>Cmdlet parameters naming one virtual network, plus <paramref name="extra"/>.</summary>
    private static Dictionary<string, object> VnetParameters(ScenarioContext c, string group, string name, Dictionary<string, object>? extra = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = c.SubscriptionId,
            ["ResourceGroupName"] = group,
            ["Name"] = name,
        };
        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                parameters[key] = value;
            }
        }
        return parameters;
    }

    private static Dictionary<string, object> GroupParameters(ScenarioContext c, string group) => new()
    {
        ["SubscriptionId"] = c.SubscriptionId,
        ["ResourceGroupName"] = group,
    };

    // ---- Requests ----

    private static string ResourceGroupPath(ScenarioContext c, string group) =>
        $"/subscriptions/{c.SubscriptionId}/resourceGroups/{group}";

    private static string VnetPath(ScenarioContext c, string group, string name) =>
        $"{ResourceGroupPath(c, group)}/providers/Microsoft.Network/virtualNetworks/{name}?api-version={ApiVersion}";

    private static string ResourceGroupListPath(ScenarioContext c, string group) =>
        $"{ResourceGroupPath(c, group)}/providers/Microsoft.Network/virtualNetworks?api-version={ApiVersion}";

    private static string SubscriptionListPath(ScenarioContext c) =>
        $"/subscriptions/{c.SubscriptionId}/providers/Microsoft.Network/virtualNetworks?api-version={ApiVersion}";

    private static List<CapturedHttpRequest> SentSince(RecordPlaybackInterceptor interceptor, int before) =>
        interceptor.Sent.Skip(before).ToList();

    private static bool IsMethod(CapturedHttpRequest request, string method) =>
        string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase);

    private static bool PathAndQueryEquals(CapturedHttpRequest request, string pathAndQuery) =>
        string.Equals(new Uri(request.Uri).PathAndQuery, pathAndQuery, StringComparison.OrdinalIgnoreCase);

    private static bool IsResourceGroupGet(CapturedHttpRequest request, ScenarioContext c, string group) =>
        IsMethod(request, "GET")
        && string.Equals(new Uri(request.Uri).AbsolutePath, ResourceGroupPath(c, group), StringComparison.OrdinalIgnoreCase);

    private static bool IsVnetPut(CapturedHttpRequest request, ScenarioContext c, string group, string name) =>
        IsMethod(request, "PUT") && PathAndQueryEquals(request, VnetPath(c, group, name));

    private static void AssertOnlyResourceGroupGet(List<CapturedHttpRequest> requests, ScenarioContext c, string group)
    {
        var request = Assert.Single(requests);
        Assert.True(IsResourceGroupGet(request, c, group), $"expected only GET {ResourceGroupPath(c, group)}, got {request.Method} {request.Uri}");
    }

    private static void AssertResourceGroupGetBeforePut(List<CapturedHttpRequest> requests, ScenarioContext c, string group, string name)
    {
        int get = requests.FindIndex(request => IsResourceGroupGet(request, c, group));
        int put = requests.FindIndex(request => IsVnetPut(request, c, group, name));
        Assert.True(get >= 0, $"no GET on resource group {group} was sent");
        Assert.True(put > get, $"the PUT to virtualNetworks/{name} was not sent after the resource group GET");
    }

    private static void AssertPutFirstWithoutResourceGroupGet(List<CapturedHttpRequest> requests, ScenarioContext c, string group, string name)
    {
        Assert.NotEmpty(requests);
        Assert.True(IsVnetPut(requests[0], c, group, name), $"expected the first request to be PUT {VnetPath(c, group, name)}, got {requests[0].Method} {requests[0].Uri}");
        Assert.DoesNotContain(requests, request => IsResourceGroupGet(request, c, group));
    }

    private static void AssertFirstRequest(List<CapturedHttpRequest> requests, string method, string pathAndQuery)
    {
        Assert.NotEmpty(requests);
        Assert.True(IsMethod(requests[0], method) && PathAndQueryEquals(requests[0], pathAndQuery),
            $"expected {method} {pathAndQuery}, got {requests[0].Method} {requests[0].Uri}");
    }

    private static void AssertNoDelete(List<CapturedHttpRequest> requests) =>
        Assert.DoesNotContain(requests, request => IsMethod(request, "DELETE"));

    /// <summary>The body of the one PUT to the virtual network among <paramref name="requests"/>.</summary>
    private static JsonNode PutBody(List<CapturedHttpRequest> requests, ScenarioContext c, string group, string name)
    {
        var put = Assert.Single(requests, request => IsVnetPut(request, c, group, name));
        Assert.NotNull(put.Body);
        return JsonNode.Parse(put.Body)!;
    }

    private static List<string> Strings(JsonNode? node) =>
        Assert.IsType<JsonArray>(node).Select(item => item!.GetValue<string>()).ToList();

    // ---- Output ----

    private static VirtualNetwork SingleNetwork(IReadOnlyList<object> output) =>
        Assert.IsType<VirtualNetwork>(Assert.Single(output));

    /// <summary>
    /// The virtual networks in <paramref name="output"/>. The CLI writes a list as one batch; the
    /// cmdlets write each item.
    /// </summary>
    private static List<VirtualNetwork> Networks(IReadOnlyList<object> output)
    {
        var networks = new List<VirtualNetwork>();
        foreach (var item in output)
        {
            switch (item)
            {
                case VirtualNetwork network:
                    networks.Add(network);
                    break;
                case IEnumerable<VirtualNetwork> batch:
                    networks.AddRange(batch);
                    break;
                default:
                    Assert.Fail($"unexpected output item {item.GetType()}");
                    break;
            }
        }
        return networks;
    }

    private static void AssertSameSet(IEnumerable<string> expected, IEnumerable<string>? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), actual.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static Subnet AssertSingleSubnet(VirtualNetwork network, string name, string addressPrefix)
    {
        Assert.NotNull(network.Subnets);
        var subnet = Assert.Single(network.Subnets);
        Assert.Equal(name, subnet.Name);
        Assert.Equal(addressPrefix, subnet.AddressPrefix);
        return subnet;
    }

    /// <summary>T1 create (CLI step 3, PowerShell step 4): the defaults the service returns.</summary>
    private static void AssertCreatedWithDefaults(VirtualNetwork created)
    {
        Assert.Equal(DefaultLocation, created.Location);
        Assert.Equal("Succeeded", created.ProvisioningState);
        Assert.Equal(["10.0.0.0/16"], created.AddressSpaceAddressPrefixes!);
        var subnet = AssertSingleSubnet(created, "default", "10.0.0.0/24");
        Assert.Equal("Disabled", subnet.PrivateEndpointNetworkPolicies);
        Assert.Equal("Disabled", created.PrivateEndpointVNetPolicies);
    }

    /// <summary>T2 step 2: every explicit input comes back on the created network.</summary>
    private static void AssertExplicitInputs(VirtualNetwork created)
    {
        Assert.Equal(DefaultLocation, created.Location);
        AssertSameSet(["20.0.0.0/16", "10.0.0.0/16"], created.AddressSpaceAddressPrefixes);
        AssertSingleSubnet(created, "subnet1", "20.0.0.0/24");
        Assert.Equal(["8.8.8.8"], created.DhcpOptionsDnsServers!);
        Assert.NotNull(created.Tags);
        Assert.Equal(2, created.Tags.Count);
        Assert.Equal("scenario", created.Tags["env"]);
        Assert.Equal("", created.Tags["owner"]);
        Assert.Equal(10, created.FlowTimeoutInMinutes);
        Assert.Equal("12076:20000", created.BgpCommunitiesVirtualNetworkCommunity);
        Assert.False(created.EnableDdosProtection);
    }

    /// <summary>T2 step 4: several subnet prefixes are sent as the plural property only.</summary>
    private static void AssertSeveralSubnetPrefixesSent(JsonNode body)
    {
        var subnet = body["properties"]!["subnets"]![0]!["properties"]!;
        Assert.Equal(["10.1.0.0/24", "10.1.1.0/24"], Strings(subnet["addressPrefixes"]));
        Assert.Null(subnet["addressPrefix"]);
    }

    /// <summary>T2 step 4: the created subnet holds both prefixes.</summary>
    private static void AssertSeveralSubnetPrefixes(VirtualNetwork created)
    {
        Assert.NotNull(created.Subnets);
        var subnet = Assert.Single(created.Subnets);
        Assert.Equal("subnet1", subnet.Name);
        AssertSameSet(["10.1.0.0/24", "10.1.1.0/24"], subnet.AddressPrefixes);
    }

    // ---- Sanitizer ----

    /// <summary>
    /// Trims a virtual network list response to the one network whose ID ends with
    /// <paramref name="keepIdSuffix"/>, so the subscription-wide list records no one else's
    /// networks. Only a JSON object with a <c>value</c> array of virtual networks is touched, so it
    /// is idempotent on playback, which re-sanitises the recorded side.
    /// </summary>
    private sealed class VirtualNetworkListSanitizer(string keepIdSuffix) : IRecordingSanitizer
    {
        private const string VirtualNetworkSegment = "/providers/Microsoft.Network/virtualNetworks/";

        public string Name => "virtual-network-list";

        public string Sanitize(string value)
        {
            if (!value.Contains("\"value\"", StringComparison.Ordinal))
            {
                return value;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(value) as JsonObject;
            }
            catch (JsonException)
            {
                return value;
            }

            if (root?["value"] is not JsonArray items)
            {
                return value;
            }

            var others = items
                .Where(item => item is JsonObject network
                               && network["id"] is JsonValue id
                               && id.TryGetValue<string>(out var resourceId)
                               && resourceId.Contains(VirtualNetworkSegment, StringComparison.OrdinalIgnoreCase)
                               && !resourceId.EndsWith(keepIdSuffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (others.Count == 0)
            {
                return value;
            }

            foreach (var item in others)
            {
                items.Remove(item);
            }

            return root.ToJsonString();
        }
    }
}
