// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Xunit;

namespace Cli.Tests;

public class InvalidInputErrorTests
{
    [Fact]
    public void JsonUpdateHelper_MalformedSetExpression_ThrowsInvalidInput()
    {
        var target = new JsonObject();

        var ex = Assert.Throws<InvalidInputException>(() =>
            JsonUpdateHelper.ApplyGenericUpdate(target, ["novalue"], null, null, forceString: false));

        Assert.Equal("InvalidInput", ex.Code);
        Assert.Equal("Invalid --set expression 'novalue'. Expected PATH=VALUE.", ex.Message);
    }

    [Fact]
    public void ManagedIdentityCredential_SystemAssignedWithId_ThrowsInvalidInput()
    {
        var ex = Assert.Throws<InvalidInputException>(() =>
            new ManagedIdentityCredential(ManagedIdentityIdType.SystemAssigned, "some-id"));

        Assert.Equal("InvalidInput", ex.Code);
    }

    [Fact]
    public void InvalidInput_UsesCode_InvalidInput_AndExitCode2()
    {
        var ex = AzclipsErrorException.InvalidInput("Invalid --set expression 'x'. Expected PATH=VALUE.");

        Assert.Equal("InvalidInput", ex.Code);
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void InvalidInput_ReturnsSubtype_AndIsAgentEligible()
    {
        var ex = AzclipsErrorException.InvalidInput("bad value");

        Assert.IsType<InvalidInputException>(ex);
        Assert.True(ex.AgentFallbackEligible);
    }

    [Fact]
    public void InvalidInput_PreservesInnerException()
    {
        var inner = new FormatException("boom");

        var ex = AzclipsErrorException.InvalidInput("bad value", innerException: inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void OtherTypedErrors_AreNotAgentEligible()
    {
        Assert.False(AzclipsErrorException.UsageError("bad usage").AgentFallbackEligible);
        Assert.False(AzclipsErrorException.ProfileNotFound("missing").AgentFallbackEligible);
        Assert.False(AzclipsErrorException.RegistryInvalid("corrupt").AgentFallbackEligible);
    }
}
