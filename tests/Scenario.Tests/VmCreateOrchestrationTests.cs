using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Proves the vm create orchestration records and replays against a fake ARM: chained LRO
/// creates, vnet and nsg in parallel, downstream requests built from upstream response ids.
/// </summary>
[Collection("Scenario Tests")]
public class VmCreateOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string VmName = "scenario-vm";
    private const string Password = "PlaceholderP@ss1";
    private const string PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8g";
    private const string Image = "MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest";
    private const string LinuxImage = "Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest";

    // A public IP the fake is seeded with, in the VM's resource group.
    private const string SharedPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/publicIPAddresses/shared-ip";

    // A well-formed id the fake has never heard of.
    private const string MissingPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip";

    // NSGs the fake is seeded with: one in the VM's resource group, one in another.
    private const string SharedNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg";
    private const string OtherGroupNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg";

    // A well-formed NSG id the fake has never heard of, and a public IP id passed where an NSG id belongs.
    private const string MissingNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/missing-nsg";
    private const string NotAnNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/not-an-nsg";
    private const string NotADiskEncryptionSetId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/not-a-des";

    private static readonly string[] s_groupArgs =
    [
        "group", "create",
        "--subscription-id", SubscriptionId,
        "--name", "scenario-vm-rg",
        "--location", "westus2",
    ];

    private static Dictionary<string, object> PsGroupParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["Name"] = "scenario-vm-rg",
        ["Location"] = "westus2",
    };

    private static readonly string[] s_cliArgsWithoutNsg =
    [
        "vm", "create",
        "--subscription-id", SubscriptionId,
        "--name", VmName,
        "--location", "westus2",
        "--resource-group", "scenario-vm-rg",
        "--size", "Standard_D2s_v3",
        "--image", Image,
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--network-interface-name", "scenario-vm-nic",
        "--virtual-network-name", "scenario-vm-vnet",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];

    // An explicit name the fake doesn't know: vm create looks it up, gets a 404, and creates it.
    private static readonly string[] s_cliArgs = [.. s_cliArgsWithoutNsg, "--nsg", "scenario-vm-nsg"];

    private static Dictionary<string, object> PsParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["Name"] = VmName,
        ["Location"] = "westus2",
        ["ResourceGroupName"] = "scenario-vm-rg",
        ["VmSize"] = "Standard_D2s_v3",
        ["Image"] = Image,
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["NetworkInterfaceName"] = "scenario-vm-nic",
        ["VirtualNetworkName"] = "scenario-vm-vnet",
        ["SecurityGroupName"] = "scenario-vm-nsg",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static SessionRecord RecordFromCli()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        AssertVm(host.RunCli(s_cliArgs));
        return record;
    }

    [Fact]
    public void Size_BindsVmSize()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"vmSize\":\"Standard_D2s_v3\"", vmBody);
    }

    [Fact]
    public void SshKeyValues_BindLinuxConfigurationPublicKeys()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli([.. ArgsWithImage(LinuxImage), "--ssh-key-values", PublicKey]));
        }

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"linuxConfiguration\"", vmBody);
        Assert.Contains("\"disablePasswordAuthentication\":false", vmBody);
        Assert.Contains("\"path\":\"/home/azadmin/.ssh/authorized_keys\"", vmBody);
        Assert.Contains($"\"keyData\":\"{PublicKey}\"", vmBody);
        Assert.DoesNotContain("windowsConfiguration", vmBody);
    }

    [Fact]
    public void SshKeyValues_CanOmitAdminPassword()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(ArgsWithImage(LinuxImage).Where(arg => arg != "--admin-password" && arg != Password).Append("--ssh-key-values").Append(PublicKey).ToArray()));
        }

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"disablePasswordAuthentication\":true", vmBody);
        Assert.DoesNotContain("adminPassword", vmBody);
    }

    [Fact]
    public void Create_RequiresPasswordOrSshKey_BeforeCreatingNetworkResources()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli(s_cliArgs.Where(arg => arg != "--admin-password" && arg != Password).ToArray()));

        Assert.Contains("Specify an administrator password or SSH public key values.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void Create_RequiresAdminPasswordForPowerShell_BeforeCreatingNetworkResources()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
        var parameters = PsParameters();
        parameters["AdminPassword"] = "";
        var error = Assert.Throws<UsageErrorException>(() => host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));

        Assert.Contains("Specify an administrator password or SSH public key values.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SshKeyValues_RequireLinuxImage_BeforeCreatingNetworkResources()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli([.. s_cliArgs, "--ssh-key-values", PublicKey]));

        Assert.Contains("SSH public keys require a Linux marketplace image.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SshKeyValues_WhitespaceKey_FailsBeforeCreatingNetworkResources()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli([.. ArgsWithImage(LinuxImage), "--ssh-key-values", "  "]));

        Assert.Contains("SSH public key value cannot be empty.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void Image_Urn_BindsMarketplaceImageReference()
    {
        var vmBody = RecordVmBody(ArgsWithImage("Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest"));

        Assert.Contains("\"publisher\":\"Canonical\"", vmBody);
        Assert.Contains("\"offer\":\"0001-com-ubuntu-server-jammy\"", vmBody);
        Assert.Contains("\"sku\":\"22_04-lts-gen2\"", vmBody);
        Assert.Contains("\"version\":\"22.04.202409010\"", vmBody);
        Assert.DoesNotContain("windowsConfiguration", vmBody);
    }

    [Fact]
    public void Image_Urn_BindsMarketplacePlanWhenImageHasPlan()
    {
        var vmBody = RecordVmBody(ArgsWithImage("Contoso:offer:sku:with-plan"));

        Assert.Contains("\"plan\":{\"name\":\"plan-name\",\"product\":\"plan-product\",\"publisher\":\"plan-publisher\"}", vmBody);
    }

    [Fact]
    public void Image_LatestVersion_UsesNumericOrdering()
    {
        var vmBody = RecordVmBody(ArgsWithImage("VersionTest:offer:sku:latest"));

        Assert.Contains("\"version\":\"10.0.0\"", vmBody);
    }

    [Fact]
    public void Image_TrustedLaunchFeature_PreservesTrustedLaunchDefaults()
    {
        var vmBody = RecordVmBody(ArgsWithImage("TrustedLaunchTest:offer:sku:1.0.0"));

        Assert.Contains("\"securityType\":\"TrustedLaunch\"", vmBody);
        Assert.Contains("\"secureBootEnabled\":true", vmBody);
        Assert.Contains("\"vTpmEnabled\":true", vmBody);
    }

    [Fact]
    public void SecurityType_Standard_OverridesTrustedLaunchDefaults()
    {
        var vmBody = RecordVmBody([.. s_cliArgs, "--security-type", "Standard"]);

        Assert.Contains("\"securityType\":\"Standard\"", vmBody);
        Assert.DoesNotContain("uefiSettings", vmBody);
        Assert.DoesNotContain("secureBootEnabled", vmBody);
        Assert.DoesNotContain("vTpmEnabled", vmBody);
    }

    [Fact]
    public void SecurityType_TrustedLaunch_DefaultsUefiSettings()
    {
        var vmBody = RecordVmBody([.. ArgsWithImage("Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest"), "--security-type", "TrustedLaunch"]);

        Assert.Contains("\"securityType\":\"TrustedLaunch\"", vmBody);
        Assert.Contains("\"secureBootEnabled\":true", vmBody);
        Assert.Contains("\"vTpmEnabled\":true", vmBody);
    }

    [Fact]
    public void SecurityType_ConfidentialVM_RequiresOsDiskSecurityEncryptionType()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli([.. s_cliArgs, "--security-type", "ConfidentialVM"]));

        Assert.Contains("OS disk security encryption type is required", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SecurityType_ConfidentialVM_BindsOsDiskSecurityEncryptionType()
    {
        var vmBody = RecordVmBody([.. s_cliArgs, "--security-type", "ConfidentialVM", "--os-disk-security-encryption-type", "VMGuestStateOnly"]);

        Assert.Contains("\"securityType\":\"ConfidentialVM\"", vmBody);
        Assert.Contains("\"secureBootEnabled\":true", vmBody);
        Assert.Contains("\"vTpmEnabled\":true", vmBody);
        Assert.Contains("\"osDisk\":{\"createOption\":\"FromImage\"", vmBody);
        Assert.Contains("\"securityEncryptionType\":\"VMGuestStateOnly\"", vmBody);
    }

    [Fact]
    public void SecurityType_ConfidentialVM_BindsDiskEncryptionSet()
    {
        var vmBody = RecordVmBody([.. s_cliArgs, "--security-type", "ConfidentialVM", "--os-disk-security-encryption-type", "DiskWithVMGuestState", "--os-disk-secure-vm-disk-encryption-set", "mydes"]);

        Assert.Contains("\"securityEncryptionType\":\"DiskWithVMGuestState\"", vmBody);
        Assert.Contains("\"diskEncryptionSet\":{\"id\":\"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Compute/diskEncryptionSets/mydes\"}", vmBody);
    }

    [Fact]
    public void OsDiskSecurityEncryptionType_RequiresConfidentialVM()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli([.. s_cliArgs, "--os-disk-security-encryption-type", "VMGuestStateOnly"]));

        Assert.Contains("can only be specified when security type is ConfidentialVM", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void OsDiskSecureVMDiskEncryptionSet_RequiresDiskWithVMGuestState()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli([.. s_cliArgs, "--security-type", "ConfidentialVM", "--os-disk-security-encryption-type", "VMGuestStateOnly", "--os-disk-secure-vm-disk-encryption-set", "mydes"]));

        Assert.Contains("can only be specified when OS disk security encryption type is DiskWithVMGuestState", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void OsDiskSecureVMDiskEncryptionSet_InvalidResourceId_FailsBeforeCreatingNetworkResources()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli(
            [.. s_cliArgs,
                "--security-type", "ConfidentialVM",
                "--os-disk-security-encryption-type", "DiskWithVMGuestState",
                "--os-disk-secure-vm-disk-encryption-set", NotADiskEncryptionSetId]));

        Assert.Contains("Invalid disk encryption set resource id", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SecurityType_PowerShell_BindsStandard()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
        var parameters = PsParameters();
        parameters["SecurityType"] = "Standard";
        AssertVm(host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"securityType\":\"Standard\"", vmBody);
        Assert.DoesNotContain("uefiSettings", vmBody);
    }

    [Fact]
    public void SecurityType_PowerShell_BindsConfidentialVMOsDiskSecurityEncryptionType()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
        var parameters = PsParameters();
        parameters["SecurityType"] = "ConfidentialVM";
        parameters["OSDiskSecurityEncryptionType"] = "VMGuestStateOnly";
        AssertVm(host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"securityType\":\"ConfidentialVM\"", vmBody);
        Assert.Contains("\"securityEncryptionType\":\"VMGuestStateOnly\"", vmBody);
    }

    [Theory]
    [InlineData("Ubuntu2204")]
    [InlineData("custom-image")]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/images-rg/providers/Microsoft.Compute/images/custom-image")]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/images-rg/providers/Microsoft.Compute/galleries/gallery/images/image/versions/1.0.0")]
    [InlineData("/SharedGalleries/galleryName/Images/imageName/Versions/1.0.0")]
    [InlineData("/CommunityGalleries/galleryName/Images/imageName/Versions/1.0.0")]
    [InlineData("https://storage.blob.core.windows.net/vhds/source.vhd")]
    public void Image_NonUrnForms_FailBeforeCreatingNetworkResources(string image)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        var error = Assert.Throws<UsageErrorException>(() => host.RunCli(ArgsWithImage(image)));

        Assert.Contains("Only marketplace image URNs", error.Message);

        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Image_EmptyOrWhitespace_FailsBeforeCreatingNetworkResources(string image)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunCli(s_groupArgs);
        Assert.Throws<UsageErrorException>(() => host.RunCli(ArgsWithImage(image)));

        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Image_EmptyOrWhitespace_FailsBeforeCreatingNetworkResources_PowerShell(string image)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

        host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
        var parameters = PsParameters();
        parameters["Image"] = image;
        Assert.Throws<UsageErrorException>(() => host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));

        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SshKeyValues_ReadPublicKeyFiles_AndTrimContent()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pub");
        File.WriteAllText(keyPath, $"{PublicKey}{Environment.NewLine}");

        try
        {
            var record = new SessionRecord();
            var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
            using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
            {
                host.RunCli(s_groupArgs);
                AssertVm(host.RunCli([.. ArgsWithImage(LinuxImage), "--ssh-key-values", keyPath]));
            }

            var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
            Assert.Contains($"\"keyData\":\"{PublicKey}\"", vmBody);
            Assert.DoesNotContain("\\n", vmBody);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    [Fact]
    public void SshKeyValues_PassThroughMissingPublicKeyFilesAsInlineValues()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli([.. ArgsWithImage(LinuxImage), "--ssh-key-values", "missing-key.pub"]));
        }

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"keyData\":\"missing-key.pub\"", vmBody);
    }

    [Fact]
    public void Record_CapturesTheWholeChain_Sanitised()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }

        var uris = record.Entries.Select(e => e.Request.Uri).ToList();
        Assert.Contains(uris, u => u.Contains("/virtualNetworks/scenario-vm-vnet"));
        Assert.Contains(uris, u => u.Contains("/networkSecurityGroups/scenario-vm-nsg"));
        Assert.Contains(uris, u => u.Contains("/networkInterfaces/scenario-vm-nic"));
        Assert.Contains(uris, u => u.Contains("/virtualMachines/scenario-vm"));
        Assert.Contains(uris, u => u.Contains("/operations/vm-create"));

        // Downstream requests must embed ids from upstream responses.
        var nicBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/")).Request.Body!;
        Assert.Contains("/subnets/default", nicBody);
        Assert.Contains("/networkSecurityGroups/scenario-vm-nsg", nicBody);
        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("/networkInterfaces/scenario-vm-nic", vmBody);

        // Sanitised: the password really went over the wire, but never into the record.
        Assert.Contains(recorder.Sent, r => r.Body is not null && r.Body.Contains(Password));
        Assert.DoesNotContain(Password, vmBody);
        Assert.Contains("[redacted]", vmBody);
    }

    [Fact]
    public void Playback_ReplaysBothSurfaces_FromOneRecording_Fast()
    {
        var record = RecordFromCli();

        // Playback stamps Retry-After to zero, so both replays must beat one recorded interval.
        var clock = Stopwatch.StartNew();

        using (var cli = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            cli.RunCli(s_groupArgs);
            AssertVm(cli.RunCli(s_cliArgs));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsParameters(), extension: "Compute"));
        }

        clock.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5),
            $"Playback took {clock.Elapsed}; recorded Retry-After values are being honored instead of neutralized.");
    }

    [Fact]
    public void Playback_ServesConcurrentVnetAndNsg_RegardlessOfRecordedOrder()
    {
        // vnet and nsg run in parallel, so their recorded order is racy. Prove the swap replays.
        var record = RecordFromCli();

        int vnetIndex = record.Entries.FindIndex(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        int nsgIndex = record.Entries.FindIndex(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        (record.Entries[vnetIndex], record.Entries[nsgIndex]) = (record.Entries[nsgIndex], record.Entries[vnetIndex]);

        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        AssertVm(host.RunCli(s_cliArgs));
    }

    [Fact]
    public void AcceleratedNetworking_IsOffByDefault_AndOptIn()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":false", NicBody(record));

        record = new SessionRecord();
        recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli([.. s_cliArgs, "--accelerated-networking", "true"]));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":true", NicBody(record));

        record = new SessionRecord();
        recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            var parameters = PsParameters();
            parameters["AcceleratedNetworking"] = true;
            AssertVm(host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":true", NicBody(record));

        static string NicBody(SessionRecord record)
            => record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/")).Request.Body!;
    }

    [Fact]
    public void ExistingNics_CliAttachesMultipleNicsWithoutCreatingNetworkResources()
    {
        string secondNicId = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/networkInterfaces/shared-nic";
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", "primary-nic", secondNicId, "--nic-delete-option", "Delete"));

        AssertNoNetworkDependencyWrites(record);
        var networkInterfaces = VmNetworkInterfaces(record);
        Assert.Equal(2, networkInterfaces.GetArrayLength());
        Assert.EndsWith("/networkInterfaces/primary-nic", networkInterfaces[0].GetProperty("id").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(networkInterfaces[0].GetProperty("properties").GetProperty("primary").GetBoolean());
        Assert.Equal("Delete", networkInterfaces[0].GetProperty("properties").GetProperty("deleteOption").GetString());
        Assert.Equal(secondNicId, networkInterfaces[1].GetProperty("id").GetString());
        Assert.False(networkInterfaces[1].GetProperty("properties").GetProperty("primary").GetBoolean());
        Assert.Equal("Delete", networkInterfaces[1].GetProperty("properties").GetProperty("deleteOption").GetString());
    }

    [Fact]
    public void ExistingNics_CliSupportsPerNicDeleteOptions()
    {
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs(
                "--nics", "primary-nic", "secondary-nic",
                "--nic-delete-option", "primary-nic=Delete", "secondary-nic=Detach"));

        var networkInterfaces = VmNetworkInterfaces(record);
        Assert.Equal("Delete", networkInterfaces[0].GetProperty("properties").GetProperty("deleteOption").GetString());
        Assert.Equal("Detach", networkInterfaces[1].GetProperty("properties").GetProperty("deleteOption").GetString());
    }

    [Fact]
    public void ExistingNics_PowerShellAppliesOneDeleteOptionToAllNics()
    {
        var parameters = PsParameters();
        RemoveNewNicParameters(parameters);
        parameters["NetworkInterfaceIds"] = new List<string> { "primary-nic", "secondary-nic" };
        parameters["NetworkInterfaceDeleteOption"] = "Detach";

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId), parameters);

        AssertNoNetworkDependencyWrites(record);
        var networkInterfaces = VmNetworkInterfaces(record);
        Assert.Equal(2, networkInterfaces.GetArrayLength());
        Assert.All(
            networkInterfaces.EnumerateArray(),
            nic => Assert.Equal("Detach", nic.GetProperty("properties").GetProperty("deleteOption").GetString()));
    }

    [Fact]
    public void ExistingNics_RejectNetworkCreationArgumentsBeforeCreatingResources()
    {
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", "primary-nic", "--subnet", "default"));

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("--subnet", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/providers/"));
    }

    [Fact]
    public void ExistingNics_RejectUnknownDeleteOptionTarget()
    {
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", "primary-nic", "--nic-delete-option", "other-nic=Delete"));

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("other-nic", error.Message);
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/virtualMachines/"));
    }

    [Fact]
    public void ExistingNics_DeleteOption_MatchesByTerminalName()
    {
        string nicId1 = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/networkInterfaces/primary-nic";
        string nicId2 = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/networkInterfaces/secondary-nic";
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", nicId1, nicId2, "--nic-delete-option", "primary-nic=Delete", "secondary-nic=Detach"));

        var networkInterfaces = VmNetworkInterfaces(record);
        Assert.Equal(2, networkInterfaces.GetArrayLength());
        Assert.Equal("Delete", networkInterfaces[0].GetProperty("properties").GetProperty("deleteOption").GetString());
        Assert.Equal("Detach", networkInterfaces[1].GetProperty("properties").GetProperty("deleteOption").GetString());
    }

    [Fact]
    public void ExistingNics_DeleteOption_RejectsAmbiguousTerminalName()
    {
        string nicId1 = $"/subscriptions/{SubscriptionId}/resourceGroups/rg1/providers/Microsoft.Network/networkInterfaces/shared-nic";
        string nicId2 = $"/subscriptions/{SubscriptionId}/resourceGroups/rg2/providers/Microsoft.Network/networkInterfaces/shared-nic";
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", nicId1, nicId2, "--nic-delete-option", "shared-nic=Delete"));

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("ambiguous", error.Message);
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/virtualMachines/"));
    }

    [Fact]
    public void ExistingNics_DeleteOption_RejectsDuplicateOptionForSameNic()
    {
        string nicId = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/networkInterfaces/primary-nic";
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            ExistingNicCliArgs("--nics", nicId, "--nic-delete-option", $"{nicId}=Delete", "primary-nic=Detach"));

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("specified more than once", error.Message);
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/virtualMachines/"));
    }

    [Fact]
    public void CreatedNic_SerializesDeleteOption()
    {
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId),
            [.. s_cliArgs, "--nic-delete-option", "Delete"]);

        var networkInterface = Assert.Single(VmNetworkInterfaces(record).EnumerateArray());
        Assert.Equal("Delete", networkInterface.GetProperty("properties").GetProperty("deleteOption").GetString());
        Assert.False(networkInterface.GetProperty("properties").TryGetProperty("primary", out _));
    }

    [Fact]
    public void SubnetResourceId_BypassesVirtualNetworkCreation()
    {
        string subnetId = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/virtualNetworks/shared-vnet/subnets/app";
        string[] args = RemoveOption(s_cliArgs, "--subnet-names");
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId, subnetId),
            [.. args, "--subnet", subnetId]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/subnets/app?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/virtualNetworks/"));
        Assert.Contains(subnetId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void SubnetResourceId_NotFound_FailsBeforeCreatingNetworkResources()
    {
        string subnetId = $"/subscriptions/{SubscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Network/virtualNetworks/shared-vnet/subnets/app";
        string[] args = RemoveOption(s_cliArgs, "--subnet-names");
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            [.. args, "--subnet", subnetId]);

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains($"Subnet '{subnetId}' was not found.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/publicIPAddresses/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/"));
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SubnetAndSubnetNames_Conflict_ReportsBothClientSurfaces()
    {
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            [.. s_cliArgs, "--subnet", "custom"]);

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("Specify either --subnet or --subnet-names (-SubnetName or -SubnetNames), not both.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void VirtualNetworkName_MissingWhenSubnetIsName_ReportsBothClientSurfaces()
    {
        string[] args = RemoveOption(s_cliArgs, "--subnet-names");
        string[] argsWithoutVnet = RemoveOption(args, "--virtual-network-name");
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            [.. argsWithoutVnet, "--subnet", "custom"]);

        Assert.IsType<UsageErrorException>(error);
        Assert.Contains("Specify --virtual-network-name (-VirtualNetworkName) when --subnet (-SubnetName) is a subnet name.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/"));
    }

    [Fact]
    public void SubnetName_SelectsExistingSubnetWithoutUpdatingVirtualNetwork()
    {
        string[] args = RemoveOption(s_cliArgs, "--subnet-names");
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId),
            [.. args, "--subnet", "default"]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/virtualNetworks/scenario-vm-vnet?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/virtualNetworks/"));
        Assert.Contains("/subnets/default", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_Omitted_CreatesTheDefaultWithoutALookup()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        var publicIpPutIndex = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/publicIPAddresses/scenario-vmPublicIP?"));
        Assert.True(publicIpPutIndex >= 0);
        Assert.DoesNotContain(record.Entries[..publicIpPutIndex], e => IsRequest(e, "GET", "/publicIPAddresses/"));
        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/scenario-vmPublicIP?"));
        Assert.Contains("/publicIPAddresses/scenario-vmPublicIP", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_EmptyString_AttachesNone()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", ""]);

        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/publicIPAddresses/"));
        Assert.DoesNotContain("publicIPAddress", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_ExistingName_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, SharedPublicIpId), [.. s_cliArgs, "--public-ip-address", "shared-ip"]);

        var publicIpGetIndex = record.Entries.FindIndex(e => IsRequest(e, "GET", "/publicIPAddresses/shared-ip?"));
        Assert.True(publicIpGetIndex >= 0);
        Assert.DoesNotContain(record.Entries[..publicIpGetIndex], e => IsRequest(e, "PUT", "/providers/"));
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/publicIPAddresses/shared-ip?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/"));
        Assert.Contains(SharedPublicIpId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_MissingResourceId_FailsBeforeAnythingIsCreated()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", MissingPublicIpId]);

        Assert.IsType<UsageErrorException>(error);
        Assert.Equal($"Public IP address '{MissingPublicIpId}' was not found.", error.Message);
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip?"));

        // Every network and VM request carries /providers/; the resource group create does not.
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

    [Fact]
    public void NewNetworkSecurityGroup_GetsTheDefaultRdpRule()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        // vm create deploys a Windows image, so the new NSG gets legacy CLI's RDP rule and nothing else.
        Assert.Contains(
            "\"securityRules\":[{\"name\":\"rdp\",\"properties\":{\"protocol\":\"Tcp\",\"sourcePortRange\":\"*\",\"destinationPortRange\":\"3389\",\"sourceAddressPrefix\":\"*\",\"destinationAddressPrefix\":\"*\",\"access\":\"Allow\",\"priority\":1000,\"direction\":\"Inbound\"}}]",
            PutBody(record, "/networkSecurityGroups/"));
    }

    [Fact]
    public void NewNetworkSecurityGroup_ForALinuxImage_GetsTheDefaultSshRule()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), ArgsWithImage(LinuxImage));

        // The image's metadata says Linux, so the new NSG gets legacy CLI's SSH rule instead of RDP.
        Assert.Contains(
            "\"securityRules\":[{\"name\":\"default-allow-ssh\",\"properties\":{\"protocol\":\"Tcp\",\"sourcePortRange\":\"*\",\"destinationPortRange\":\"22\",\"sourceAddressPrefix\":\"*\",\"destinationAddressPrefix\":\"*\",\"access\":\"Allow\",\"priority\":1000,\"direction\":\"Inbound\"}}]",
            PutBody(record, "/networkSecurityGroups/"));
    }

    [Fact]
    public void Nsg_Omitted_CreatesTheDefaultWithoutALookup()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgsWithoutNsg);

        // Every create is a long-running operation whose poll GETs the same URI after the PUT,
        // so a lookup would be a GET before the PUT.
        int create = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vmNSG?"));
        Assert.True(create >= 0, "vm create never created the default NSG.");
        Assert.DoesNotContain(record.Entries[..create], e => IsRequest(e, "GET", "/networkSecurityGroups/"));
        Assert.Contains("/networkSecurityGroups/scenario-vmNSG", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_NewName_IsLookedUpThenCreated()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        int lookup = record.Entries.FindIndex(e => IsRequest(e, "GET", "/networkSecurityGroups/scenario-vm-nsg?"));
        int create = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vm-nsg?"));
        Assert.True(lookup >= 0, "vm create never looked the NSG up.");
        Assert.Equal(404, record.Entries[lookup].Response.StatusCode);
        Assert.True(create > lookup, "vm create did not create the NSG after its lookup.");
        Assert.Contains("/networkSecurityGroups/scenario-vm-nsg", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_ExistingName_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, SharedNsgId), [.. s_cliArgsWithoutNsg, "--nsg", "shared-nsg"]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/networkSecurityGroups/shared-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(SharedNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_ResourceIdInAnotherGroup_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, OtherGroupNsgId), [.. s_cliArgsWithoutNsg, "--nsg", OtherGroupNsgId]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(OtherGroupNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_MissingResourceId_FailsBeforeAnythingIsCreated()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", MissingNsgId]);

        Assert.IsType<UsageErrorException>(error);
        Assert.Equal($"Network security group '{MissingNsgId}' was not found.", error.Message);
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/missing-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

    [Fact]
    public void Nsg_MalformedResourceId_FailsBeforeAnyNetworkRequest()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", NotAnNsgId]);

        Assert.Equal(
            $"Invalid network security group resource id '{NotAnNsgId}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Network/networkSecurityGroups/{{networkSecurityGroupName}}.",
            error.Message);

        // As in legacy CLI, only the image is resolved first; no network resource is read or created.
        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/providers/Microsoft.Network/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

    [Fact]
    public void Nsg_EmptyString_AttachesNone()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", ""]);

        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain("networkSecurityGroup", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_IsResolvedBeforeThePublicIp()
    {
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            [.. s_cliArgsWithoutNsg, "--nsg", MissingNsgId, "--public-ip-address", MissingPublicIpId]);

        // Legacy CLI validates the NSG first, so its error wins and the public IP is never looked up.
        Assert.IsType<UsageErrorException>(error);
        Assert.Equal($"Network security group '{MissingNsgId}' was not found.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/publicIPAddresses/"));
    }

    [Fact]
    public void PowerShell_SecurityGroupName_AttachesAnExistingNsg()
    {
        var parameters = PsParameters();
        parameters["SecurityGroupName"] = "shared-nsg";

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId, SharedNsgId), parameters);

        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(SharedNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PowerShell_SecurityGroupNameOmitted_CreatesTheDefault()
    {
        var parameters = PsParameters();
        parameters.Remove("SecurityGroupName");

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId), parameters);

        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vmNSG?"));
    }

    [Fact]
    public void FailedDetailRead_FailsVmCreate_AfterTheVmIsCreated()
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), new FakeVmArmBackend(SubscriptionId) { FailVmDetailRead = true }, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);

        // As in legacy CLI, a failed read after the create fails the command rather than
        // writing the VM without its details, even though the VM now exists.
        var error = Assert.Throws<HttpRequestException>(() => host.RunCli(s_cliArgs));
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/virtualMachines/scenario-vm?"));
    }

    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal("westus2", vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
    }

    /// <summary>Runs group create then vm create against <paramref name="backend"/>, recording both.</summary>
    private static SessionRecord RecordCli(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(vmCreateArgs));
        }
        return record;
    }

    /// <summary>Like <see cref="RecordCli"/>, for a vm create that must fail; returns what it sent and the error.</summary>
    private static (SessionRecord Record, Exception Error) RecordCliFailure(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli(vmCreateArgs));
        return (record, error);
    }

    /// <summary>Runs New-AzResourceGroup then New-AzVM against <paramref name="backend"/>, recording both.</summary>
    private static SessionRecord RecordPowerShell(FakeVmArmBackend backend, Dictionary<string, object> vmParameters)
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertVm(host.RunPowerShell("New-AzVM", vmParameters, extension: "Compute"));
        }
        return record;
    }

    private static bool IsRequest(SessionRecordEntry entry, string method, string uriFragment)
        => entry.Request.Method == method && entry.Request.Uri.Contains(uriFragment);

    private static JsonElement VmNetworkInterfaces(SessionRecord record)
        => JsonDocument.Parse(PutBody(record, "/virtualMachines/"))
            .RootElement.GetProperty("properties").GetProperty("networkProfile").GetProperty("networkInterfaces");

    private static void AssertNoNetworkDependencyWrites(SessionRecord record)
    {
        Assert.DoesNotContain(record.Entries, e =>
            e.Request.Method == "PUT"
            && (e.Request.Uri.Contains("/virtualNetworks/")
                || e.Request.Uri.Contains("/networkSecurityGroups/")
                || e.Request.Uri.Contains("/publicIPAddresses/")
                || e.Request.Uri.Contains("/networkInterfaces/")));
    }

    private static string[] ExistingNicCliArgs(params string[] networkArgs) =>
    [
        "vm", "create",
        "--subscription-id", SubscriptionId,
        "--name", VmName,
        "--location", "westus2",
        "--resource-group", "scenario-vm-rg",
        "--size", "Standard_D2s_v3",
        "--image", Image,
        "--admin-username", "azadmin",
        "--admin-password", Password,
        .. networkArgs,
    ];

    private static void RemoveNewNicParameters(Dictionary<string, object> parameters)
    {
        foreach (string name in new[]
        {
            "NetworkInterfaceName",
            "VirtualNetworkName",
            "SecurityGroupName",
            "SubnetNames",
            "SubnetAddressPrefixes",
            "AddressPrefixes",
        })
        {
            parameters.Remove(name);
        }
    }

    private static string[] RemoveOption(string[] args, string option)
    {
        int index = Array.IndexOf(args, option);
        Assert.True(index >= 0, $"Option '{option}' was not found.");
        return [.. args[..index], .. args[(index + 2)..]];
    }

    /// <summary>The body of the first PUT whose URI contains <paramref name="uriFragment"/>.</summary>
    private static string PutBody(SessionRecord record, string uriFragment)
        => record.Entries.First(e => IsRequest(e, "PUT", uriFragment)).Request.Body!;

    private static string RecordVmBody(string[] args)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(args));
        }

        return record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
    }

    private static string[] ArgsWithImage(string image)
    {
        var args = s_cliArgs.ToArray();
        var imageIndex = Array.IndexOf(args, "--image");
        args[imageIndex + 1] = image;
        return args;
    }
}
