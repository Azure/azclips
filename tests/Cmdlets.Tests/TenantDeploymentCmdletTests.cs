// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Collections;
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

    [Fact]
    public void NormalizeJsonInput_ConvertsNestedHashtables()
    {
        Hashtable input = new()
        {
            ["properties"] = new Hashtable
            {
                ["items"] = new object[]
                {
                    new Hashtable { ["name"] = "one" },
                },
            },
        };

        var normalized = Assert.IsType<Dictionary<string, object?>>(
            NormalizeJsonInput(input));
        var properties = Assert.IsType<Dictionary<string, object?>>(normalized["properties"]);
        var items = Assert.IsType<object?[]>(properties["items"]);
        var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(items));

        Assert.Equal("one", item["name"]);
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

    private static object? NormalizeJsonInput(object? value)
    {
        Type helpers = typeof(GetTenantDeploymentCmdlet).Assembly.GetType(
            "Microsoft.Azure.Azclips.Cmdlets.Resources.TenantDeploymentCmdletHelpers",
            throwOnError: true)!;
        MethodInfo method = helpers.GetMethod(
            "NormalizeJsonInput",
            BindingFlags.Public | BindingFlags.Static)!;

        return method.Invoke(null, [value]);
    }
}
