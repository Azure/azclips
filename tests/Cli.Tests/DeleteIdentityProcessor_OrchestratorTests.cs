// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// ManagedIdentityResolverTests covers every input case; these prove the orchestrator calls each check before any request.
public class DeleteIdentityProcessor_OrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_MissingNameAndResourceGroup_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<DeleteIdentityProcessor_Orchestrator>(profile: null);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(MissingIdentityAddressMessage, ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceId_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<DeleteIdentityProcessor_Orchestrator>(profile: null);
        processor.Ids = ["not-a-resource-id"];

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(
            "Invalid managed identity resource id 'not-a-resource-id'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{resourceName}.",
            ex.Message);
    }}
