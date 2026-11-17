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

    [Fact]
    public void CreateRequestBody_OmitsOsDiskWhenNoStorageSkuOrSecurity()
    {
        var body = CreateRequestBody(CreateProcessor([]));

        Assert.Null(body!.Properties.StorageProfile.OSDisk);
    }

    [Fact]
    public void CreateRequestBody_SetsOsDiskStorageAccountType()
    {
        var processor = CreateProcessor([]);
        processor.OsDiskStorageAccountType = "Premium_LRS";

        var osDisk = CreateRequestBody(processor)!.Properties.StorageProfile.OSDisk;

        Assert.NotNull(osDisk);
        Assert.Equal("FromImage", osDisk!.CreateOption);
        Assert.Equal("Premium_LRS", osDisk.ManagedDisk!.StorageAccountType);
        Assert.Null(osDisk.ManagedDisk.SecurityProfile);
    }

    [Fact]
    public void CreateRequestBody_SetsOsDiskDeleteOptionWithoutManagedDisk()
    {
        var processor = CreateProcessor([]);
        processor.OsDiskDeleteOption = "Delete";

        var osDisk = CreateRequestBody(processor)!.Properties.StorageProfile.OSDisk;

        Assert.NotNull(osDisk);
        Assert.Equal("FromImage", osDisk!.CreateOption);
        Assert.Equal("Delete", osDisk.DeleteOption);
        Assert.Null(osDisk.ManagedDisk);
    }

    [Fact]
    public void CreateRequestBody_CombinesOsDiskDeleteOptionWithStorageAccountType()
    {
        var processor = CreateProcessor([]);
        processor.OsDiskDeleteOption = "Detach";
        processor.OsDiskStorageAccountType = "Premium_LRS";

        var osDisk = CreateRequestBody(processor)!.Properties.StorageProfile.OSDisk!;

        Assert.Equal("Detach", osDisk.DeleteOption);
        Assert.Equal("Premium_LRS", osDisk.ManagedDisk!.StorageAccountType);
    }

    [Fact]
    public void CreateRequestBody_CombinesStorageAccountTypeWithDiskSecurity()
    {
        var processor = CreateProcessor([]);
        processor.OsDiskStorageAccountType = "Premium_LRS";
        processor.OsDiskSecurityEncryptionType = "VMGuestStateOnly";

        var managedDisk = CreateRequestBody(processor)!.Properties.StorageProfile.OSDisk!.ManagedDisk!;

        Assert.Equal("Premium_LRS", managedDisk.StorageAccountType);
        Assert.Equal("VMGuestStateOnly", managedDisk.SecurityProfile!.SecurityEncryptionType);
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
