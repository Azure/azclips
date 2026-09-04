// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Login;
using Microsoft.Azure.Azclips.Extensions.Login;
using Microsoft.Azure.Azclips.Extensions.Login.Processors;

namespace Cmdlets.Tests;

/// <summary>
/// PowerShell binding tests for <c>Get-AzAccessToken</c>. The shared <see cref="GetAccessTokenProcessor"/>
/// exposes <c>Tenant</c>/<c>Subscription</c> properties while the cmdlet surfaces <c>-TenantId</c>/
/// <c>-SubscriptionId</c>; the metadata Name must map the PS parameter onto the processor property, or
/// binding throws at runtime. These exercise the real PS parsing/binding pipeline, including the
/// multi-valued <c>-Scope</c>, and assert the PowerShell surface omits the CLI-only <c>-Plaintext</c>
/// flag (PowerShell opts into plaintext via the AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN env var).
/// </summary>
public class GetAccessTokenCmdletTests
{
    private static ArgumentParsingService PSBinder() =>
        new(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));

    [Fact]
    public void PSBinding_MapsTenantIdAndSubscriptionId_OntoProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        var processor = new GetAccessTokenProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ResourceUrl"] = "https://vault.azure.net",
                ["TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["SubscriptionId"] = "sub-1",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("https://vault.azure.net", processor.Resource);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.Tenant);
        Assert.Equal("sub-1", processor.Subscription);
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
    public void PowerShellSurface_DoesNotExposePlaintextFlag()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        Assert.DoesNotContain(metadata.Parameters.Values, p => p.Name == nameof(GetAccessTokenProcessor.Plaintext));
    }
}
