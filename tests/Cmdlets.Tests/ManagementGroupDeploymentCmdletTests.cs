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
public class ManagementGroupDeploymentCmdletTests
{
    [Theory]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), "Stop-AzManagementGroupDeployment")]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), "Remove-AzManagementGroupDeployment")]
    public void PassThru_WritesNothingWhenWhatIfSkipsTheCall(Type cmdlet, string name)
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            new ModuleInitializer().OnImport();
            var iss = InitialSessionState.CreateDefault2();
            iss.Commands.Add(new SessionStateCmdletEntry(name, cmdlet, null));
            using var ps = PowerShell.Create(iss);
            ps.AddCommand(name).AddParameter("ManagementGroupId", "mg").AddParameter("Name", "dep").AddParameter("PassThru").AddParameter("WhatIf");

            Assert.Empty(ps.Invoke());
            Assert.Empty(ps.Streams.Error);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet), ParameterAttribute.AllParameterSets, int.MinValue)]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet), ParameterAttribute.AllParameterSets, int.MinValue)]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), "GetByDeploymentName", 0)]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), "StopByDeploymentName", 0)]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), "RemoveByDeploymentName", 0)]
    public void ManagementGroupId_IsMandatoryLikeLegacy(Type cmdlet, string set, int position)
    {
        ParameterAttribute parameter = Assert.Single(cmdlet.GetProperty("ManagementGroupId")!.GetCustomAttributes<ParameterAttribute>());

        Assert.Equal((true, set, position), (parameter.Mandatory, parameter.ParameterSetName, parameter.Position));
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet), typeof(DeploymentExtended))]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet), typeof(DeploymentValidateResult))]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), typeof(DeploymentExtended))]
    public void Cmdlets_DeclareOutputTypes(Type cmdlet, Type expected)
    {
        Assert.Equal(expected, Assert.Single(Assert.Single(cmdlet.GetCustomAttributes<OutputTypeAttribute>()).Type).Type);
    }

    [Fact]
    public void TemplateCmdlets_UseResourceGroupDeploymentShape()
    {
        Assert.Empty(ParameterSets(typeof(NewManagementGroupDeploymentCmdlet)));
        Assert.Empty(ParameterSets(typeof(TestManagementGroupDeploymentCmdlet)));
        Assert.True(Assert.Single(typeof(NewManagementGroupDeploymentCmdlet).GetProperty("Name")!.GetCustomAttributes<ParameterAttribute>()).Mandatory);
        Assert.False(Assert.Single(typeof(TestManagementGroupDeploymentCmdlet).GetProperty("Name")!.GetCustomAttributes<ParameterAttribute>()).Mandatory);
        Assert.Null(typeof(NewManagementGroupDeploymentCmdlet).GetProperty("TemplateObject"));
        Assert.Equal(typeof(Hashtable), typeof(TestManagementGroupDeploymentCmdlet).GetProperty("TemplateObject")!.PropertyType);
        Assert.Equal(["TemplateParameterUri"], Aliases(typeof(TestManagementGroupDeploymentCmdlet), "ParametersUri"));
        Assert.Equal(["TemplateParameterFile"], Aliases(typeof(TestManagementGroupDeploymentCmdlet), "ParametersFile"));
        Assert.True(typeof(NewManagementGroupDeploymentCmdlet).GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
        Assert.False(typeof(TestManagementGroupDeploymentCmdlet).GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
    }
    [Theory]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), "GetByDeploymentName", new[] { "GetByDeploymentId", "GetByDeploymentName" })]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), "StopByDeploymentName", new[] { "StopByDeploymentId", "StopByDeploymentName", "StopByInputObject" })]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), "RemoveByDeploymentName", new[] { "RemoveByDeploymentId", "RemoveByDeploymentName", "RemoveByInputObject" })]
    public void NameCmdlets_UseLegacyParameterSetsAndAliases(Type cmdlet, string defaultSet, string[] sets)
    {
        Assert.Equal(defaultSet, cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName);
        Assert.Equal(sets, ParameterSets(cmdlet));
        Assert.Equal(["DeploymentName"], Aliases(cmdlet, "Name"));
        Assert.Equal(["DeploymentId", "ResourceId"], Aliases(cmdlet, "Id"));
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet))]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet))]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet))]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet))]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet))]
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
