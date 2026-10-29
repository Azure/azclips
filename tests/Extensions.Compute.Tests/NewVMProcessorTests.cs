// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class NewVMProcessorTests
{
    [Fact]
    public void CreateRequestBody_OmitsZonesWhenNoneProvided()
    {
        var processor = CreateProcessor([]);
        var body = CreateRequestBody(processor);

        Assert.NotNull(body);
        Assert.Null(body!.Zones);
    }

    [Fact]
    public void CreateRequestBody_UsesProvidedZonesArray()
    {
        var processor = CreateProcessor(["1"]);
        var body = CreateRequestBody(processor);

        Assert.NotNull(body);
        Assert.Equal(["1"], body!.Zones);
    }

    private static NewVMProcessor CreateProcessor(List<string> zones) =>
        new()
        {
            SubscriptionId = "sub",
            Name = "vm",
            Location = "eastus",
            ResourceGroupName = "rg",
            VmSize = "Standard_D2s_v5",
            AdminUsername = "azureuser",
            AdminPassword = "placeholder-password",
            NetworkInterfaceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic",
            Zones = zones
        };

    private static VirtualMachine? CreateRequestBody(NewVMProcessor processor)
    {
        var method = typeof(NewVMProcessor).GetMethod("CreateRequestBody", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<VirtualMachine?>(method!.Invoke(processor, null));
    }
}
