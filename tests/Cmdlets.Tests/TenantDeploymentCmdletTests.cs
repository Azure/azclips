// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

public class TenantDeploymentCmdletTests
{
    [Fact]
    public void NameFromId_AcceptsTenantDeploymentId()
    {
        Assert.Equal(
            "deployment",
            NameFromId("/providers/Microsoft.Resources/deployments/deployment"));
    }

    [Theory]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Resources/deployments/deployment")]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Resources/deployments/deployment")]
    [InlineData("/providers/Microsoft.Authorization/deployments/deployment")]
    [InlineData("/providers/Microsoft.Resources/deployments")]
    public void NameFromId_RejectsNonTenantDeploymentIds(string id)
    {
        TargetInvocationException exception =
            Assert.Throws<TargetInvocationException>(() => NameFromId(id));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void NewCmdlet_DeclaresCreateAndWhatIfOutputTypes()
    {
        OutputTypeAttribute attribute = Assert.Single(
            typeof(NewTenantDeploymentCmdlet)
                .GetCustomAttributes<OutputTypeAttribute>());

        Assert.Equal(
            [
                "Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended",
                "Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult",
            ],
            attribute.Type.Select(type => type.Name));
    }

    private static string NameFromId(string id)
    {
        Type helpers = typeof(GetTenantDeploymentCmdlet).Assembly.GetType(
            "Microsoft.Azure.Azclips.Cmdlets.Resources.TenantDeploymentCmdletHelpers",
            throwOnError: true)!;
        MethodInfo method = helpers.GetMethod(
            "NameFromId",
            BindingFlags.Public | BindingFlags.Static)!;

        return (string)method.Invoke(null, [id])!;
    }
}
