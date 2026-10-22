// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;

namespace Cmdlets.Tests;

/// <summary>
/// Tests for the composition root (means the central place where dependencies are registered and wired up) of the PowerShell application.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class CompositionRootClientMetadataTests
{
    [Fact]
    public void OnImport_RegistersAzurePowerShellApplicationId()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            new ModuleInitializer().OnImport();

            var metadata = AzclipsServiceProvider.GetRequiredService<ClientMetadata>();
            Assert.Equal(ClientType.PowerShell, metadata.ClientType);
            Assert.Equal("1950a258-227b-4e31-a9cf-717495945fc2", metadata.FirstPartyApplicationId);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }
}
