// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

namespace Cmdlets.Tests;

/// <summary>
/// PowerShell binding tests for <c>Get-AzAccessToken</c>. The shared <see cref="GetAccessTokenProcessor"/>
/// exposes <c>Tenant</c>/<c>Plaintext</c> properties while the cmdlet surfaces
/// <c>-TenantId</c>/<c>-AsPlainText</c>; the metadata Name must map each PS parameter onto its
/// processor property, or binding throws at runtime. These exercise the real PS parsing/binding
/// pipeline, including the multi-valued <c>-Scope</c> and the <c>-AsPlainText</c> switch mapping onto
/// the <c>Plaintext</c> property.
/// </summary>
public class GetAccessTokenCmdletTests
{
    private static ArgumentParsingService PSBinder() =>
        new(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));

    [Fact]
    public void PSBinding_MapsTenantId_OntoProcessorProperty()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var processor = new GetAccessTokenProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ResourceUrl"] = "https://vault.azure.net",
                ["TenantId"] = "11111111-1111-1111-1111-111111111111",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("https://vault.azure.net", processor.Resource);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.Tenant);
    }

    [Fact]
    public void PSBinding_MapsMultipleScopes_OntoStringArray()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var processor = new GetAccessTokenProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Scope"] = new[] { "https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/Mail.Read" },
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(
            ["https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/Mail.Read"],
            processor.Scope!);
    }

    [Fact]
    public void PowerShellSurface_ExposesAsPlainTextMappedToPlaintextProperty()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var parameter = Assert.Single(metadata.Parameters.Values, p => p.Name == nameof(GetAccessTokenProcessor.Plaintext));
        Assert.Equal("AsPlainText", parameter.InvocationName);
    }

    // Azure PowerShell's Get-AzAccessToken accepts -Resource and -ResourceUri as aliases for
    // -ResourceUrl, so the PS binder resolves them via [Alias] on the cmdlet property. A missing
    // alias silently breaks migrated scripts at bind time, so the cmdlet carries both and the
    // command metadata mirrors them for generated help.
    [Fact]
    public void PowerShellSurface_ResourceUrl_ExposesClassicResourceAliases()
    {
        var alias = (AliasAttribute?)typeof(GetAccessTokenCmdlet)
            .GetProperty(nameof(GetAccessTokenCmdlet.ResourceUrl))!
            .GetCustomAttributes(typeof(AliasAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(alias);
        Assert.Contains("Resource", alias!.AliasNames);
        Assert.Contains("ResourceUri", alias.AliasNames);

        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var resourceParam = metadata.Parameters["ResourceUrl"];
        Assert.Contains("Resource", resourceParam.Aliases);
        Assert.Contains("ResourceUri", resourceParam.Aliases);

        // Mirror the aliases in the command-level alias map too, matching the sibling Connect-AzAccount surface.
        Assert.Equal("ResourceUrl", metadata.ParameterAliases["Resource"]);
        Assert.Equal("ResourceUrl", metadata.ParameterAliases["ResourceUri"]);
    }

    // Get-AzAccessToken parity: -ResourceTypeName accepts the PowerShell PascalCase resource-type names
    // via [ValidateSet] (suggest + validate, the repo's usual convention). This guard keeps the set in
    // lockstep with the resolver: every advertised name resolves, the set includes every PowerShell
    // spelling the resolver knows plus the ResourceManager alias, and it excludes the CLI-only shortcuts,
    // so adding/removing/mistyping a name fails CI.
    [Fact]
    public void PowerShellSurface_ResourceTypeName_ValidateSet_MatchesResolver()
    {
        var validateSet = (ValidateSetAttribute?)typeof(GetAccessTokenCmdlet)
            .GetProperty(nameof(GetAccessTokenCmdlet.ResourceTypeName))!
            .GetCustomAttributes(typeof(ValidateSetAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(validateSet);
        Assert.True(validateSet!.IgnoreCase, "IgnoreCase should be set so case variants (e.g. msgraph) bind.");

        // Every advertised name must resolve.
        foreach (var name in validateSet.ValidValues)
        {
            Assert.True(WellKnownResourceTypes.TryResolve(name, out _), $"ValidateSet name '{name}' should resolve.");
        }

        // The PowerShell set advertises every PascalCase spelling the resolver knows (completeness) plus
        // the ResourceManager alias, and excludes the CLI-only shortcuts (oss-rdbms / media), which have
        // no PowerShell spelling. This keeps Get-AzAccessToken aligned with Azure PowerShell's list.
        foreach (var psName in WellKnownResourceTypes.PowerShellNames)
        {
            Assert.Contains(psName, validateSet.ValidValues);
        }
        Assert.Contains("ResourceManager", validateSet.ValidValues);
        Assert.DoesNotContain("OssRdbms", validateSet.ValidValues);
        Assert.DoesNotContain("Media", validateSet.ValidValues);
    }
}
