// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

/// <summary>
/// OS disk body rules of vm create. Zones and the end-to-end body are covered by the
/// <c>VmCreateOrchestrationTests</c> scenario tests.
/// </summary>
public class CreateOrUpdateVirtualMachinesProcessor_OrchestratorTests
{
    [Fact]
    public void ApplyOsDisk_OmitsOsDiskWhenNoStorageSkuOrSecurity()
    {
        var vm = ApplyOsDisk(new(), storageAccountType: null);

        Assert.Null(vm.Properties?.StorageProfile?.OsDisk);
    }

    [Fact]
    public void ApplyOsDisk_SetsOsDiskStorageAccountType()
    {
        var vm = ApplyOsDisk(new(), "Premium_LRS");

        Assert.Equal("FromImage", vm.OsDiskCreateOption);
        Assert.Equal("Premium_LRS", vm.ManagedDiskStorageAccountType);
        Assert.Null(vm.Properties!.StorageProfile!.OsDisk!.ManagedDisk!.SecurityProfile);
    }

    [Fact]
    public void ApplyOsDisk_SetsOsDiskDeleteOptionWithoutManagedDisk()
    {
        var vm = ApplyOsDisk(new() { OsDiskDeleteOption = "Delete" }, storageAccountType: null);

        Assert.Equal("FromImage", vm.OsDiskCreateOption);
        Assert.Equal("Delete", vm.OsDiskDeleteOption);
        Assert.Null(vm.Properties!.StorageProfile!.OsDisk!.ManagedDisk);
    }

    [Fact]
    public void ApplyOsDisk_CombinesOsDiskDeleteOptionWithStorageAccountType()
    {
        var vm = ApplyOsDisk(new() { OsDiskDeleteOption = "Detach" }, "Premium_LRS");

        Assert.Equal("Detach", vm.OsDiskDeleteOption);
        Assert.Equal("Premium_LRS", vm.ManagedDiskStorageAccountType);
    }

    [Fact]
    public void ApplyOsDisk_CombinesStorageAccountTypeWithDiskSecurity()
    {
        var vm = ApplyOsDisk(new(), "Premium_LRS", new ResolvedVmSecurityProfile { OsDiskSecurityEncryptionType = "VMGuestStateOnly" });

        Assert.Equal("Premium_LRS", vm.ManagedDiskStorageAccountType);
        Assert.Equal("VMGuestStateOnly", vm.SecurityProfileSecurityEncryptionType);
    }

    private static VirtualMachine ApplyOsDisk(CreateOrUpdateVirtualMachinesProcessor_Orchestrator processor, string? storageAccountType, ResolvedVmSecurityProfile? securityProfile = null)
    {
        var vm = new VirtualMachine();
        var method = typeof(CreateOrUpdateVirtualMachinesProcessor_Orchestrator).GetMethod("ApplyOsDisk", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(processor, [vm, storageAccountType, securityProfile ?? new ResolvedVmSecurityProfile()]);
        return vm;
    }
}
