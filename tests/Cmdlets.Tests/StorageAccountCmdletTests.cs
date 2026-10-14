// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsStorage;

using System.Collections;
using System.Management.Automation;
using System.Reflection;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using GetStorageAccountCmdlet = CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage.GetStorageAccountCmdlet;
using NewStorageAccountCmdlet = CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage.NewStorageAccountCmdlet;
using RemoveStorageAccountCmdlet = CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage.RemoveStorageAccountCmdlet;
using StorageCmdletArgumentConverter = CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage.StorageCmdletArgumentConverter;

namespace Cmdlets.Tests;

/// <summary>
/// Binding and declaration tests for the storage account cmdlets.
/// <para>
/// The pointed one is the parameter-set coupling: Get-AzStorageAccount resolves which processor
/// runs by appending its active parameter set name to the command name, so the set names in the
/// cmdlet and the keys in the extension index are one contract held together only by strings.
/// Renaming a set compiles cleanly and then fails at runtime, and nothing else in the build
/// catches it.
/// </para>
/// </summary>
public class StorageAccountCmdletTests
{
    private static ArgumentParsingService Service() =>
        new(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));

    private static IEnumerable<string> ParameterSetsOf<T>(string propertyName) =>
        typeof(T).GetProperty(propertyName)!
            .GetCustomAttributes<ParameterAttribute>()
            .Select(a => a.ParameterSetName);

    // ── The parameter-set to command-name contract ──

    [Theory]
    [InlineData("AccountName")]
    [InlineData("ResourceGroup")]
    public void EveryGetParameterSet_HasAMatchingCommandInTheIndex(string parameterSetName)
    {
        // ProcessRecord builds $"Get-AzStorageAccount_{ParameterSetName}", so a set without a
        // matching index entry throws KeyNotFoundException the first time it is used.
        var metadata = new ExtensionIndex().GetCommandMetadata($"Get-AzStorageAccount_{parameterSetName}", ClientType.PowerShell);

        Assert.NotNull(metadata);
    }

    [Fact]
    public void GetCmdlet_DeclaresExactlyTheParameterSetsTheIndexServes()
    {
        // Guards the other direction: a set added to the cmdlet without an index entry.
        var declared = typeof(GetStorageAccountCmdlet)
            .GetProperties()
            .SelectMany(p => p.GetCustomAttributes<ParameterAttribute>())
            .Select(a => a.ParameterSetName)
            .Where(n => n != ParameterAttribute.AllParameterSets)
            .Distinct()
            .OrderBy(n => n);

        Assert.Equal(["AccountName", "ResourceGroup"], declared);
    }

    [Fact]
    public void GetCmdlet_DefaultsToTheResourceGroupSet()
    {
        // With no arguments the cmdlet must list rather than fail asking for an account name.
        var attribute = typeof(GetStorageAccountCmdlet).GetCustomAttribute<CmdletAttribute>()!;

        Assert.Equal("ResourceGroup", attribute.DefaultParameterSetName);
    }

    // ── Binding onto the processors ──

    [Fact]
    public void GetByAccountName_BindsOntoTheShowProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzStorageAccount_AccountName", ClientType.PowerShell);
        var processor = new ShowStorageAccountProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubscriptionId"] = "sub-1",
            ["ResourceGroupName"] = "my-rg",
            ["Name"] = "mystorageaccount",
            ["Expand"] = "geoReplicationStats",
        };

        Service().BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile());

        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("my-rg", processor.ResourceGroupName);
        // The PowerShell parameter is Name; the processor property it feeds is AccountName.
        Assert.Equal("mystorageaccount", processor.AccountName);
        Assert.Equal("geoReplicationStats", processor.Expand);
    }

    [Fact]
    public void GetByResourceGroup_BindsOntoTheListCustomization()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzStorageAccount_ResourceGroup", ClientType.PowerShell);
        var processor = new ListStorageAccountProcessor_Orchestrator();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubscriptionId"] = "sub-1",
            ["ResourceGroupName"] = "my-rg",
        };

        Service().BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile());

        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("my-rg", processor.ResourceGroupName);
    }

    [Fact]
    public void GetByResourceGroup_WithoutAResourceGroup_LeavesTheSubscriptionBranchSelected()
    {
        // Omitting the group is what selects the subscription-wide operation, so the bound
        // processor must be left with an empty ResourceGroupName rather than a default.
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzStorageAccount_ResourceGroup", ClientType.PowerShell);
        var processor = new ListStorageAccountProcessor_Orchestrator();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubscriptionId"] = "sub-1",
        };

        Service().BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile());

        Assert.Equal(string.Empty, processor.ResourceGroupName);
    }

    [Fact]
    public void Remove_BindsOntoTheDeleteProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Remove-AzStorageAccount", ClientType.PowerShell);
        var processor = new DeleteStorageAccountProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubscriptionId"] = "sub-1",
            ["ResourceGroupName"] = "my-rg",
            ["Name"] = "mystorageaccount",
        };

        Service().BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile());

        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("my-rg", processor.ResourceGroupName);
        Assert.Equal("mystorageaccount", processor.AccountName);
    }

    // ── Declarations ──

    [Fact]
    public void GetCmdlet_IncludeGeoReplicationStats_IsScopedToTheAccountNameSet()
    {
        // The expansion applies to a single account, so offering it on the list set would accept
        // an argument the command cannot honour.
        Assert.Equal(["AccountName"], ParameterSetsOf<GetStorageAccountCmdlet>(nameof(GetStorageAccountCmdlet.IncludeGeoReplicationStats)));
    }

    [Fact]
    public void GetCmdlet_ResourceGroupName_IsMandatoryOnlyWhenGettingOneAccount()
    {
        var byName = typeof(GetStorageAccountCmdlet).GetProperty(nameof(GetStorageAccountCmdlet.ResourceGroupName))!
            .GetCustomAttributes<ParameterAttribute>()
            .ToDictionary(a => a.ParameterSetName, a => a.Mandatory);

        Assert.True(byName["AccountName"]);
        Assert.False(byName["ResourceGroup"]);
    }

    [Theory]
    [InlineData(typeof(NewStorageAccountCmdlet), ConfirmImpact.Medium)]
    [InlineData(typeof(RemoveStorageAccountCmdlet), ConfirmImpact.High)]
    public void MutatingCmdlets_SupportShouldProcess(Type cmdletType, ConfirmImpact expectedImpact)
    {
        var attribute = cmdletType.GetCustomAttribute<CmdletAttribute>()!;

        Assert.True(attribute.SupportsShouldProcess);
        Assert.Equal(expectedImpact, attribute.ConfirmImpact);
    }

    [Fact]
    public void GetCmdlet_DoesNotSupportShouldProcess()
    {
        // Reading changes nothing, so -WhatIf and -Confirm would be noise.
        var attribute = typeof(GetStorageAccountCmdlet).GetCustomAttribute<CmdletAttribute>()!;

        Assert.False(attribute.SupportsShouldProcess);
    }

    [Fact]
    public void RemoveCmdlet_DeclaresAnOptionalForceSwitch()
    {
        // Az.Storage scripts delete unattended with -Force; without the parameter they fail at binding.
        var property = typeof(RemoveStorageAccountCmdlet).GetProperty("Force");

        Assert.NotNull(property);
        Assert.Equal(typeof(SwitchParameter), property!.PropertyType);
        Assert.All(property.GetCustomAttributes<ParameterAttribute>(), a => Assert.False(a.Mandatory));
    }

    [Theory]
    [InlineData(typeof(NewStorageAccountCmdlet))]
    [InlineData(typeof(GetStorageAccountCmdlet))]
    public void AccountReturningCmdlets_DeclareTheStorageAccountOutputType(Type cmdletType)
    {
        // Generated help and pipeline tooling read the type from here; without it New- advertised System.Object.
        var outputType = cmdletType.GetCustomAttribute<OutputTypeAttribute>();

        Assert.NotNull(outputType);
        Assert.Equal(["Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageAccount"], outputType.Type.Select(type => type.Name));
    }

    // ── Az.Storage compatibility ──
    //
    // The cmdlets keep Az.Storage's parameter names and types so existing scripts bind, while the
    // processors keep the CLI names. BuildInvocationParameters is the only bridge between the two,
    // so these tests drive it into the real binder rather than handing the engine internal names.

    [Theory]
    [InlineData(typeof(NewStorageAccountCmdlet))]
    [InlineData(typeof(GetStorageAccountCmdlet))]
    [InlineData(typeof(RemoveStorageAccountCmdlet))]
    public void Cmdlets_AcceptTheAzStorageAccountNameAliases(Type cmdletType)
    {
        var alias = cmdletType.GetProperty("Name")!.GetCustomAttribute<AliasAttribute>();

        Assert.NotNull(alias);
        Assert.Equal(["StorageAccountName", "AccountName"], alias.AliasNames);
    }

    [Theory]
    [InlineData("ResourceGroupName", 0)]
    [InlineData("Name", 1)]
    [InlineData("SkuName", 2)]
    [InlineData("Location", 3)]
    public void NewCmdlet_PositionalParameters_MatchAzStorage(string propertyName, int position)
    {
        var parameter = typeof(NewStorageAccountCmdlet).GetProperty(propertyName)!.GetCustomAttribute<ParameterAttribute>()!;

        Assert.Equal(position, parameter.Position);
        Assert.True(parameter.ValueFromPipelineByPropertyName);
    }

    [Theory]
    [InlineData("Tag", typeof(Hashtable))]
    [InlineData("EnableHttpsTrafficOnly", typeof(bool?))]
    [InlineData("EnableHierarchicalNamespace", typeof(bool?))]
    [InlineData("EnableLargeFileShare", typeof(SwitchParameter))]
    [InlineData("AssignIdentity", typeof(SwitchParameter))]
    [InlineData("UserAssignedIdentityId", typeof(string))]
    [InlineData("Zone", typeof(string[]))]
    [InlineData("SasExpirationPeriod", typeof(TimeSpan?))]
    [InlineData("KeyExpirationPeriodInDay", typeof(int?))]
    [InlineData("EnableAccountLevelImmutability", typeof(SwitchParameter))]
    [InlineData("ImmutabilityPeriod", typeof(int?))]
    [InlineData("RequireInfrastructureEncryption", typeof(SwitchParameter))]
    [InlineData("KeyVaultUri", typeof(string))]
    [InlineData("ActiveDirectoryAccountType", typeof(string))]
    [InlineData("EnableAzureActiveDirectoryKerberosForFile", typeof(bool?))]
    public void NewCmdlet_UsesTheAzStorageParameterNamesAndTypes(string propertyName, Type expectedType)
    {
        var property = typeof(NewStorageAccountCmdlet).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedType, property.PropertyType);
    }

    [Fact]
    public void NewCmdlet_KindIsOptionalAndDefaultsToStorageV2()
    {
        var cmdlet = new NewStorageAccountCmdlet();
        var parameter = typeof(NewStorageAccountCmdlet).GetProperty("Kind")!.GetCustomAttribute<ParameterAttribute>()!;

        Assert.False(parameter.Mandatory);
        Assert.Equal("StorageV2", cmdlet.Kind);
    }

    [Fact]
    public void NewCmdlet_InvocationParameters_BindOntoTheCreateCustomization()
    {
        var cmdlet = new NewStorageAccountCmdlet
        {
            ResourceGroupName = "my-rg",
            Name = "mystorageaccount",
            Location = "eastus",
            SkuName = "Standard_LRS",
            Tag = new Hashtable { ["env"] = "dev" },
            EnableHttpsTrafficOnly = true,
            EnableHierarchicalNamespace = false,
            EnableLargeFileShare = true,
            AssignIdentity = true,
            Zone = ["1"],
            SasExpirationPeriod = TimeSpan.FromDays(1),
            KeyExpirationPeriodInDay = 30,
            EnableAccountLevelImmutability = true,
            ImmutabilityPeriod = 7,
            RequireInfrastructureEncryption = true,
            KeyVaultUri = "https://myvault.vault.azure.net/",
            ActiveDirectoryAccountType = "Computer",
        };
        var bound = new Dictionary<string, object>
        {
            ["ResourceGroupName"] = cmdlet.ResourceGroupName,
            ["Name"] = cmdlet.Name,
            ["Location"] = cmdlet.Location,
            ["SkuName"] = cmdlet.SkuName,
            ["Tag"] = cmdlet.Tag,
            ["EnableHttpsTrafficOnly"] = true,
            ["EnableHierarchicalNamespace"] = false,
            ["EnableLargeFileShare"] = cmdlet.EnableLargeFileShare,
            ["AssignIdentity"] = cmdlet.AssignIdentity,
            ["Zone"] = cmdlet.Zone,
            ["SasExpirationPeriod"] = cmdlet.SasExpirationPeriod,
            ["KeyExpirationPeriodInDay"] = 30,
            ["EnableAccountLevelImmutability"] = cmdlet.EnableAccountLevelImmutability,
            ["ImmutabilityPeriod"] = 7,
            ["RequireInfrastructureEncryption"] = cmdlet.RequireInfrastructureEncryption,
            ["KeyVaultUri"] = cmdlet.KeyVaultUri,
            ["ActiveDirectoryAccountType"] = cmdlet.ActiveDirectoryAccountType,
        };
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzStorageAccount", ClientType.PowerShell)!;

        var args = cmdlet.BuildInvocationParameters(bound);

        // Every key the cmdlet sends must be one the index declares, or the engine drops it.
        Assert.All(args.Keys, key => Assert.Contains(key, metadata.Parameters.Keys));

        var processor = new NewStorageAccountProcessor_Orchestrator();
        // SubscriptionId is left to the profile, as it is when -SubscriptionId isn't passed.
        Service().BindArgumentsToProcessor(processor, new Dictionary<string, object>(args, StringComparer.OrdinalIgnoreCase), metadata.Parameters, metadata.ParameterAliases, new Profile { SubscriptionId = "sub-1" });

        Assert.Equal("mystorageaccount", processor.AccountName);
        Assert.Equal("StorageV2", processor.Kind);
        Assert.Equal(["env=dev"], processor.Tags);
        Assert.Equal("true", processor.SupportsHttpsTrafficOnly, ignoreCase: true);
        Assert.Equal("false", processor.IsHnsEnabled, ignoreCase: true);
        Assert.Equal("Enabled", processor.LargeFileSharesState);
        Assert.Equal("SystemAssigned", processor.IdentityType);
        Assert.Equal(["1"], processor.Zones);
        Assert.Equal("1.00:00:00", processor.SasExpirationPeriod);
        Assert.Equal("30", processor.KeyExpirationPeriodInDays);
        Assert.Equal("true", processor.EnableAlw, ignoreCase: true);
        Assert.Equal("7", processor.ImmutabilityPeriodSinceCreationInDays);
        Assert.Equal("true", processor.RequireInfrastructureEncryption, ignoreCase: true);
        Assert.Equal("https://myvault.vault.azure.net/", processor.EncryptionKeyVault);
        Assert.Equal("Computer", processor.AccountType);
    }

    [Fact]
    public void NewCmdlet_UnboundParameters_AreNotSent()
    {
        // Unset options must be omitted so the service default applies; only the required set and
        // Kind, which has a default like Az.Storage's, go out.
        var cmdlet = new NewStorageAccountCmdlet { ResourceGroupName = "my-rg", Name = "acct", Location = "eastus", SkuName = "Standard_LRS" };

        var args = cmdlet.BuildInvocationParameters(new Dictionary<string, object>());

        Assert.Equal(["AccountName", "Kind", "Location", "ResourceGroupName", "SkuName"], args.Keys.Order());
    }

    [Fact]
    public void NewCmdlet_ExplicitIdentityType_WinsOverAssignIdentity()
    {
        var cmdlet = new NewStorageAccountCmdlet { IdentityType = "SystemAssignedUserAssigned", AssignIdentity = true };

        var args = cmdlet.BuildInvocationParameters(new Dictionary<string, object> { ["IdentityType"] = cmdlet.IdentityType, ["AssignIdentity"] = cmdlet.AssignIdentity });

        Assert.Equal("SystemAssigned,UserAssigned", args["IdentityType"]);
    }

    [Fact]
    public void ArgumentConverter_TagNamesContainingEquals_AreRejected()
    {
        // The processor splits each pair on its first '=', so such a name would be silently cut short.
        var error = Assert.Throws<InvalidInputException>(() => StorageCmdletArgumentConverter.ToTagArguments(new Hashtable { ["a=b"] = "c" }));

        Assert.Contains("a=b", error.Message);
    }

    [Theory]
    [InlineData(1, 0, "1.00:00:00")]
    [InlineData(0, 12, "0.12:00:00")]
    [InlineData(400, 6, "400.06:00:00")]
    public void ArgumentConverter_SasExpirationPeriod_UsesArmsDayFirstFormat(int days, int hours, string expected)
    {
        Assert.Equal(expected, StorageCmdletArgumentConverter.ToSasExpirationPeriod(new TimeSpan(days, hours, 0, 0)));
    }

    // ── Closed value sets ──

    [Fact]
    public void NewCmdlet_ValidateSets_AcceptExactlyTheValuesTheCliEnforces()
    {
        // The PowerShell binder never reads AllowedValues, so [ValidateSet] is what rejects a bad
        // value before a request is sent. Holding it to the index keeps both surfaces accepting the
        // same values. ActiveDirectoryAccountType feeds the processor's AccountType, and IdentityType
        // takes Az.Storage's SystemAssignedUserAssigned, which the cmdlet rewrites for ARM.
        var processorNames = new Dictionary<string, string> { ["ActiveDirectoryAccountType"] = "AccountType" };
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzStorageAccount", ClientType.PowerShell)!;
        var declared = typeof(NewStorageAccountCmdlet).GetProperties()
            .Select(p => (p.Name, Set: p.GetCustomAttribute<ValidateSetAttribute>()))
            .Where(p => p.Set is not null)
            .ToList();

        Assert.NotEmpty(declared);
        foreach (var (name, set) in declared)
        {
            Assert.True(set!.IgnoreCase, $"-{name} must match case-insensitively, as the CLI does.");
            var accepted = set.ValidValues.Select(StorageCmdletArgumentConverter.ToIdentityType);
            Assert.Equal(metadata.Parameters[processorNames.GetValueOrDefault(name, name)].AllowedValues.Order(), accepted.Order());
        }

        // The reverse: every closed string option is guarded, bar tri-state flags (bool? on the
        // cmdlet) and large file shares, which Az.Storage exposes as the -EnableLargeFileShare switch.
        var guarded = declared.Select(p => processorNames.GetValueOrDefault(p.Name, p.Name)).ToHashSet();
        var unguarded = metadata.Parameters.Values
            .Where(p => p.AllowedValues.Length > 0 && !p.AllowedValues.Order().SequenceEqual(["false", "true"]))
            .Select(p => p.Name)
            .Where(n => !guarded.Contains(n) && n != "LargeFileSharesState");
        Assert.Empty(unguarded);
    }
}
