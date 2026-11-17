// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cmdlets.Tests;

/// <summary>
/// Regression test closing out a review comment that assumed PowerShell binds through
/// <c>DefaultArgumentParsingProvider</c> (a passthrough) and would therefore fail to resolve
/// <c>-ObjectId</c> / <c>-ObjectType</c> on <c>New-AzRoleAssignment</c> to the orchestrator's
/// <see cref="NewRoleAssignmentProcessor_Orchestrator.AssigneeObjectId"/> /
/// <see cref="NewRoleAssignmentProcessor_Orchestrator.PrincipalType"/> properties. PowerShell
/// actually binds through <see cref="PSArgumentParsingProvider"/> (swapped in by
/// <c>ModuleInitializer.OnImport()</c> before any command runs), which does perform that
/// translation via <see cref="ParameterMetadata.Name"/>. This test exercises the real
/// <c>New-AzRoleAssignment</c> metadata from <see cref="ExtensionIndex"/> through the real
/// provider, rather than synthetic metadata, so this exact class of regression cannot recur
/// silently.
/// </summary>
public class RoleAssignmentPSArgumentParsingProviderTests
{
    [Fact]
    public void ParseArguments_TranslatesObjectIdAndObjectType_ToProcessorPropertyNames()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("New-AzRoleAssignment", ClientType.PowerShell);
        var provider = new PSArgumentParsingProvider();

        var args = new Dictionary<string, object>
        {
            ["ObjectId"] = "11111111-2222-3333-4444-555555555555",
            ["RoleDefinitionId"] = "b24988ac-6180-42a0-ab88-20f7382dd24c",
            ["ObjectType"] = "ServicePrincipal",
        };

        IDictionary<string, (string, object)> result = provider.ParseArguments(args, metadata.Parameters, metadata.ParameterAliases);

        Assert.True(result.ContainsKey("AssigneeObjectId"), "ObjectId must translate to the processor's AssigneeObjectId property");
        Assert.Equal("11111111-2222-3333-4444-555555555555", result["AssigneeObjectId"].Item2);

        Assert.True(result.ContainsKey("PrincipalType"), "ObjectType must translate to the processor's PrincipalType property");
        Assert.Equal("ServicePrincipal", result["PrincipalType"].Item2);

        Assert.True(result.ContainsKey("RoleDefinitionId"));

        // The raw PS-side names must not leak through to reflection-based property binding.
        Assert.False(result.ContainsKey("ObjectId"));
        Assert.False(result.ContainsKey("ObjectType"));
    }
}
