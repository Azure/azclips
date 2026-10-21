// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class TenantDeploymentCmdletTests
{
    [Theory]
    [InlineData(typeof(StopTenantDeploymentCmdlet), "Stop-AzTenantDeployment")]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), "Remove-AzTenantDeployment")]
    public void PassThru_WritesNothingWhenWhatIfSkipsTheCall(Type cmdlet, string name)
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            new ModuleInitializer().OnImport();
            var iss = InitialSessionState.CreateDefault2();
            iss.Commands.Add(new SessionStateCmdletEntry(name, cmdlet, null));
            using var ps = PowerShell.Create(iss);
            ps.AddCommand(name).AddParameter("Name", "dep").AddParameter("PassThru").AddParameter("WhatIf");

            Assert.Empty(ps.Invoke());
            Assert.Empty(ps.Streams.Error);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    [Theory]
    [InlineData(typeof(GetTenantDeploymentCmdlet), "GetByDeploymentName", false)]
    [InlineData(typeof(StopTenantDeploymentCmdlet), "StopByDeploymentName", true)]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), "RemoveByDeploymentName", true)]
    public void Name_IsFirstPositionLikeLegacy(Type cmdlet, string set, bool mandatory)
    {
        ParameterAttribute parameter = Assert.Single(cmdlet.GetProperty("Name")!.GetCustomAttributes<ParameterAttribute>());

        Assert.Equal((mandatory, set, 0), (parameter.Mandatory, parameter.ParameterSetName, parameter.Position));
    }

    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet), typeof(DeploymentExtended))]
    [InlineData(typeof(TestTenantDeploymentCmdlet), typeof(DeploymentValidateResult))]
    [InlineData(typeof(StopTenantDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(GetTenantDeploymentCmdlet), typeof(DeploymentExtended))]
    public void Cmdlets_DeclareOutputTypes(Type cmdlet, Type expected)
    {
        Assert.Equal(expected, Assert.Single(Assert.Single(cmdlet.GetCustomAttributes<OutputTypeAttribute>()).Type).Type);
    }

    [Fact]
    public void TemplateCmdlets_UseResourceGroupDeploymentShape()
    {
        Assert.Empty(ParameterSets(typeof(NewTenantDeploymentCmdlet)));
        Assert.Empty(ParameterSets(typeof(TestTenantDeploymentCmdlet)));
        Assert.True(Assert.Single(typeof(NewTenantDeploymentCmdlet).GetProperty("Name")!.GetCustomAttributes<ParameterAttribute>()).Mandatory);
        Assert.False(Assert.Single(typeof(TestTenantDeploymentCmdlet).GetProperty("Name")!.GetCustomAttributes<ParameterAttribute>()).Mandatory);
        Assert.Null(typeof(NewTenantDeploymentCmdlet).GetProperty("TemplateObject"));
        Assert.Equal(typeof(Hashtable), typeof(TestTenantDeploymentCmdlet).GetProperty("TemplateObject")!.PropertyType);
        Assert.Equal(["TemplateParameterUri"], Aliases(typeof(TestTenantDeploymentCmdlet), "ParametersUri"));
        Assert.Equal(["TemplateParameterFile"], Aliases(typeof(TestTenantDeploymentCmdlet), "ParametersFile"));
        Assert.True(typeof(NewTenantDeploymentCmdlet).GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
        Assert.False(typeof(TestTenantDeploymentCmdlet).GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
    }
    [Theory]
    [InlineData(typeof(GetTenantDeploymentCmdlet), "GetByDeploymentName", new[] { "GetByDeploymentId", "GetByDeploymentName" })]
    [InlineData(typeof(StopTenantDeploymentCmdlet), "StopByDeploymentName", new[] { "StopByDeploymentId", "StopByDeploymentName", "StopByInputObject" })]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), "RemoveByDeploymentName", new[] { "RemoveByDeploymentId", "RemoveByDeploymentName", "RemoveByInputObject" })]
    public void NameCmdlets_UseLegacyParameterSetsAndAliases(Type cmdlet, string defaultSet, string[] sets)
    {
        Assert.Equal(defaultSet, cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName);
        Assert.Equal(sets, ParameterSets(cmdlet));
        Assert.Equal(["DeploymentName"], Aliases(cmdlet, "Name"));
        Assert.Equal(["DeploymentId", "ResourceId"], Aliases(cmdlet, "Id"));
    }

    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet))]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet))]
    [InlineData(typeof(StopTenantDeploymentCmdlet))]
    [InlineData(typeof(TestTenantDeploymentCmdlet))]
    [InlineData(typeof(GetTenantDeploymentCmdlet))]
    public void AsJob_IsNotExposed(Type cmdlet)
    {
        Assert.Null(cmdlet.GetProperty("AsJob"));
    }

    private static string[] ParameterSets(Type cmdlet) =>
        cmdlet.GetProperties()
            .SelectMany(p => p.GetCustomAttributes<ParameterAttribute>())
            .Select(p => p.ParameterSetName)
            .Where(name => name != ParameterAttribute.AllParameterSets)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> Aliases(Type cmdlet, string property) =>
        cmdlet.GetProperty(property)!.GetCustomAttribute<AliasAttribute>()!.AliasNames;
}
