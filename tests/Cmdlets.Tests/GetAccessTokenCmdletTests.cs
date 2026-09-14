// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            processor.Scope);
    }

    [Fact]
    public void PowerShellSurface_ExposesAsPlainTextMappedToPlaintextProperty()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var parameter = Assert.Single(metadata.Parameters.Values, p => p.Name == nameof(GetAccessTokenProcessor.Plaintext));
        Assert.Equal("AsPlainText", parameter.InvocationName);
    }
}
