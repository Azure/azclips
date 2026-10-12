// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsStorage;

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
        var processor = new ListStorageAccountProcessor_Customized();

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
        var processor = new ListStorageAccountProcessor_Customized();

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

    // ── Closed value sets ──

    [Fact]
    public void NewCmdlet_ValidateSets_MatchTheValuesTheCliEnforces()
    {
        // The PowerShell binder never reads AllowedValues, so [ValidateSet] is what rejects a bad
        // value before a request is sent. Holding it to the index keeps both surfaces accepting the
        // same values. Tri-state flags are bool? on the cmdlet, so only string properties count.
        var stringProperties = typeof(NewStorageAccountCmdlet).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .ToDictionary(p => p.Name);
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzStorageAccount", ClientType.PowerShell)!;
        var closed = metadata.Parameters.Values
            .Where(p => p.AllowedValues.Length > 0 && stringProperties.ContainsKey(p.Name))
            .ToDictionary(p => p.Name, p => p.AllowedValues);
        var declared = stringProperties.Values
            .Select(p => (p.Name, Set: p.GetCustomAttribute<ValidateSetAttribute>()))
            .Where(p => p.Set is not null)
            .ToDictionary(p => p.Name, p => p.Set!);

        Assert.NotEmpty(closed);
        Assert.Equal(closed.Keys.Order(), declared.Keys.Order());
        foreach (var (name, values) in closed)
        {
            Assert.Equal(values, declared[name].ValidValues);
            Assert.True(declared[name].IgnoreCase, $"-{name} must match case-insensitively, as the CLI does.");
        }
    }
}
