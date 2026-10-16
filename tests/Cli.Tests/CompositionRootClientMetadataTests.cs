// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;

namespace Cli.Tests;

/// <summary>
/// Tests for the composition root (means the central place where dependencies are registered and wired up) of the CLI application.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class CompositionRootClientMetadataTests
{
    [Fact]
    public async Task Main_RegistersAzureCliApplicationId()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            ClientMetadata? metadata = null;
            AzclipsServiceProvider.PrependRegistration(services =>
                metadata = Assert.IsType<ClientMetadata>(
                    Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ClientMetadata))
                        .ImplementationInstance));

            var exitCode = await Core.Cli.Program.Main(["--help"]);

            Assert.Equal(0, exitCode);
            Assert.NotNull(metadata);
            Assert.Equal(ClientType.CLI, metadata.ClientType);
            Assert.Equal("04b07795-8ddb-461a-bbee-02f9e1bf7b46", metadata.FirstPartyApplicationId);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }
}
