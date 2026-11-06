// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

public class TenantDeploymentCmdletTests
{
    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet))]
    [InlineData(typeof(TestTenantDeploymentCmdlet))]
    [InlineData(typeof(GetTenantDeploymentCmdlet))]
    [InlineData(typeof(GetTenantDeploymentWhatIfResultCmdlet))]
    [InlineData(typeof(StopTenantDeploymentCmdlet))]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet))]
    [InlineData(typeof(SaveTenantDeploymentTemplateCmdlet))]
    public void Name_BindsCanonicalNameAndDeploymentNameAlias(Type cmdlet)
    {
        var metadata = new CommandMetadata(cmdlet);
        string parameterBlock = ProxyCommand.GetCmdletBindingAttribute(metadata)
            + "param(" + ProxyCommand.GetParamBlock(metadata) + ")";
        string location = typeof(TenantDeploymentTemplateCmdletBase).IsAssignableFrom(cmdlet)
            ? " -Location eastus"
            : string.Empty;

        foreach (string parameter in new[] { "Name", "DeploymentName" })
        {
            var initialState = InitialSessionState.Create();
            initialState.LanguageMode = PSLanguageMode.FullLanguage;
            using var ps = PowerShell.Create(initialState);
            ps.AddScript($"function Target {{ {parameterBlock} process {{ $PSBoundParameters }} }}; Target -{parameter} tenant-deployment{location}");

            var output = ps.Invoke();
            Assert.Empty(ps.Streams.Error);
            var bound = Assert.IsAssignableFrom<IDictionary>(Assert.Single(output).BaseObject);
            Assert.Equal("tenant-deployment", bound["Name"]);
            Assert.False(bound.Contains("DeploymentName"));
        }
    }
}
