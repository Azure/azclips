// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Cli.Tests;

/// <summary>
/// Unit tests for <see cref="WellKnownResourceTypes"/>. Locks in the source-verified union of Azure
/// CLI and Azure PowerShell shortcut names and their public-cloud resource URLs, and asserts
/// case-insensitive resolution across both the CLI kebab-case and PowerShell PascalCase spellings.
/// </summary>
public class WellKnownResourceTypesTests
{
    [Theory]
    // CLI kebab-case canonical names (verified against azure-cli cloud_resource_types / cloud.py)
    [InlineData("arm", "https://management.core.windows.net/")]
    [InlineData("aad-graph", "https://graph.windows.net/")]
    [InlineData("ms-graph", "https://graph.microsoft.com/")]
    [InlineData("batch", "https://batch.core.windows.net/")]
    [InlineData("data-lake", "https://datalake.azure.net/")]
    [InlineData("oss-rdbms", "https://ossrdbms-aad.database.windows.net")]
    [InlineData("media", "https://rest.media.azure.net")]
    // PowerShell-derived names (verified against azure-powershell-common AzureEnvironmentConstants)
    [InlineData("key-vault", "https://vault.azure.net")]
    [InlineData("storage", "https://storage.azure.com/")]
    [InlineData("synapse", "https://dev.azuresynapse.net")]
    [InlineData("managed-hsm", "https://managedhsm.azure.net")]
    [InlineData("communication-email", "https://communication.azure.com")]
    [InlineData("attestation", "https://attest.azure.net")]
    [InlineData("app-configuration", "https://azconfig.io")]
    [InlineData("analysis-services", "https://region.asazure.windows.net")]
    [InlineData("operational-insights", "https://api.loganalytics.io")]
    public void TryResolve_CanonicalName_ReturnsExpectedResourceUrl(string name, string expected)
    {
        Assert.True(WellKnownResourceTypes.TryResolve(name, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("MSGraph", "https://graph.microsoft.com/")]
    [InlineData("KeyVault", "https://vault.azure.net")]
    [InlineData("ResourceManager", "https://management.core.windows.net/")]
    [InlineData("ManagedHsm", "https://managedhsm.azure.net")]
    [InlineData("OperationalInsights", "https://api.loganalytics.io")]
    [InlineData("AadGraph", "https://graph.windows.net/")]
    [InlineData("DataLake", "https://datalake.azure.net/")]
    [InlineData("OssRdbms", "https://ossrdbms-aad.database.windows.net")]
    [InlineData("CommunicationEmail", "https://communication.azure.com")]
    [InlineData("AppConfiguration", "https://azconfig.io")]
    [InlineData("AnalysisServices", "https://region.asazure.windows.net")]
    public void TryResolve_PowerShellPascalCaseAlias_ResolvesToSameResource(string alias, string expected)
    {
        Assert.True(WellKnownResourceTypes.TryResolve(alias, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("ARM")]
    [InlineData("Arm")]
    [InlineData("ms-GRAPH")]
    public void TryResolve_IsCaseInsensitive(string name)
    {
        Assert.True(WellKnownResourceTypes.TryResolve(name, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not-a-real-type")]
    public void TryResolve_UnknownOrEmpty_ReturnsFalse(string? name)
    {
        Assert.False(WellKnownResourceTypes.TryResolve(name, out var url));
        Assert.Equal(string.Empty, url);
    }
}
