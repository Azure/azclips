// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.Abstractions.Tests;

public class PowerShellContractsTests
{
    [Fact]
    public void ProcessorMetadata_CanDescribeMultipleCommandsAndParameterSets()
    {
        PSCommandAttribute[] commands = typeof(TestProcessor).GetCustomAttributes<PSCommandAttribute>().ToArray();
        PSParameterSetAttribute[] parameterSets = typeof(TestProcessor).GetCustomAttributes<PSParameterSetAttribute>().ToArray();

        Assert.Equal(2, commands.Length);
        Assert.Contains(commands, command => command.Verb == "Get" && command.Noun == "AzWidget");
        Assert.Contains(commands, command => command.Verb == "Invoke" && command.Noun == "AzWidgetAction");
        Assert.Equal(
            new[] { "Get", "GetViaIdentity", "Invoke" },
            parameterSets.Select(set => set.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(
            parameterSets.Where(set => set.Name is "Get" or "GetViaIdentity"),
            set => Assert.Equal("Get-AzWidget", set.CommandName));
        Assert.Contains(parameterSets, set => set.Name == "Invoke" && set.CommandName == "Invoke-AzWidgetAction");
    }

    [Fact]
    public void PropertyMetadata_CanDescribeBindingPerParameterSet()
    {
        PropertyInfo property = typeof(TestProcessor).GetProperty(nameof(TestProcessor.Name))!;
        PSParameterAttribute[] parameters = property.GetCustomAttributes<PSParameterAttribute>().ToArray();

        Assert.Equal(2, parameters.Length);
        Assert.All(parameters, parameter => Assert.Equal("Get-AzWidget", parameter.CommandName));
        Assert.Contains(parameters, parameter => parameter.ParameterSetName == "Get" && parameter.Mandatory);
        Assert.Contains(parameters, parameter => parameter.ParameterSetName == "GetViaIdentity" && !parameter.Mandatory);
    }

    [Fact]
    public void GeneratedParameterContract_HasProjectedUsageAndDefaults()
    {
        AttributeUsageAttribute usage = typeof(PSParameterAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        var parameter = new PSParameterAttribute("Get-AzWidget", "Get", "Name");

        Assert.Equal(AttributeTargets.Property, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
        Assert.Equal("Get-AzWidget", parameter.CommandName);
        Assert.Equal("Get", parameter.ParameterSetName);
        Assert.Equal("Name", parameter.Name);
        Assert.False(parameter.Mandatory);
        Assert.Equal(int.MinValue, parameter.Position);
        Assert.Empty(parameter.Aliases);
    }

    [Fact]
    public void GeneratedAuxiliaryContracts_HaveSyntheticAndRequiredProperties()
    {
        var completer = new PSCompleterAttribute("Get-AzWidget");
        PropertyInfo script = typeof(PSDefaultValueAttribute).GetProperty(nameof(PSDefaultValueAttribute.Script))!;

        Assert.Equal(string.Empty, completer.ParameterSetName);
        Assert.True(script.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() is not null);
    }

    [Fact]
    public void GeneratedCommandContract_PreservesProjectedTypesAndDefaults()
    {
        var command = new PSCommandAttribute("Get", "AzWidget", "Get");

        Assert.Equal(PSConfirmImpact.None, command.ConfirmImpact);
        Assert.Empty(command.Tags);
        Assert.Empty(command.OutputTypes);
        Assert.Equal(string.Empty, command.HttpPath);
        Assert.Equal(string.Empty, command.ApiVersion);
    }

    [Fact]
    public void Contracts_DoNotReferencePowerShellSdkTypes()
    {
        Type[] contractTypes =
        [
            typeof(PSCommandAttribute),
            typeof(PSParameterSetAttribute),
            typeof(PSParameterAttribute),
            typeof(PSCompleterAttribute),
            typeof(PSDefaultValueAttribute),
            typeof(PSModuleAttribute),
            typeof(PSIdentityAttribute),
            typeof(PSIdentityPropertyAttribute),
        ];

        Assert.DoesNotContain(contractTypes.SelectMany(GetContractTypes), type =>
            type.Namespace?.StartsWith("System.Management.Automation", StringComparison.Ordinal) == true);
    }

    public static IEnumerable<object[]> GeneratedContractUsages()
    {
        yield return new object[] { typeof(PSCommandAttribute), AttributeTargets.Class, true, false, () => new PSCommandAttribute("Get", "AzWidget", "Get") };
        yield return new object[] { typeof(PSParameterSetAttribute), AttributeTargets.Class, true, false, () => new PSParameterSetAttribute("Get-AzWidget", "Get") };
        yield return new object[] { typeof(PSParameterAttribute), AttributeTargets.Property, true, false, () => new PSParameterAttribute("Get-AzWidget", "Get", "Name") };
        yield return new object[] { typeof(PSCompleterAttribute), AttributeTargets.Class | AttributeTargets.Property, true, false, () => new PSCompleterAttribute("Get-AzWidget") };
        yield return new object[] { typeof(PSDefaultValueAttribute), AttributeTargets.Property, true, false, () => new PSDefaultValueAttribute("Get-AzWidget", "Get") { Script = "default" } };
        yield return new object[] { typeof(PSModuleAttribute), AttributeTargets.Assembly, true, true, () => new PSModuleAttribute("Widget", "Microsoft.Widget") };
        yield return new object[] { typeof(PSIdentityAttribute), AttributeTargets.Class, false, false, () => new PSIdentityAttribute("WidgetIdentity") };
        yield return new object[] { typeof(PSIdentityPropertyAttribute), AttributeTargets.Property, false, false, () => new PSIdentityPropertyAttribute("Name") };
    }

    [Theory]
    [MemberData(nameof(GeneratedContractUsages))]
    public void GeneratedContracts_HaveExpectedUsageAndConstructor(
        Type type, AttributeTargets targets, bool allowMultiple, bool inherited, Func<Attribute> factory)
    {
        AttributeUsageAttribute usage = type.GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.Equal(targets, usage.ValidOn);
        Assert.Equal(allowMultiple, usage.AllowMultiple);
        Assert.Equal(inherited, usage.Inherited);
        Assert.IsType(type, factory());
    }

    private static IEnumerable<Type> GetContractTypes(Type type) =>
        Flatten(type.GetProperties().Select(property => property.PropertyType).Append(type.BaseType ?? typeof(object)));

    private static IEnumerable<Type> Flatten(IEnumerable<Type> types)
    {
        foreach (Type type in types)
        {
            if (type.IsArray)
            {
                foreach (Type nested in Flatten([type.GetElementType()!]))
                {
                    yield return nested;
                }
            }
            else if (type.IsGenericType)
            {
                foreach (Type nested in Flatten(type.GetGenericArguments()))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return type;
            }
        }
    }

    [PSCommand("Get", "AzWidget", "Get", OutputTypes = [typeof(string)])]
    [PSCommand("Invoke", "AzWidgetAction", "Invoke")]
    [PSParameterSet("Get-AzWidget", "Get")]
    [PSParameterSet("Get-AzWidget", "GetViaIdentity")]
    [PSParameterSet("Invoke-AzWidgetAction", "Invoke")]
    private sealed class TestProcessor
    {
        [PSParameter("Get-AzWidget", "Get", "Name", Mandatory = true)]
        [PSParameter("Get-AzWidget", "GetViaIdentity", "Name")]
        public string Name { get; set; } = string.Empty;
    }
}