// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Cli.Tests;

public class FederatedCredentialValidatorTests
{
    [Theory]
    [InlineData(1, null)]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    [InlineData(null, "claims['sub'] matches 'repo:*'")]
    public void ValidateClaimsMatchingExpression_HalfSpecified_ThrowsInvalidInput(int? version, string? value)
    {
        Exception? exception = Record.Exception(() => FederatedCredentialValidator.ValidateClaimsMatchingExpression(version, value));

        Assert.Equal(
            "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(1, "claims['sub'] matches 'repo:*'")]
    public void ValidateClaimsMatchingExpression_BothOrNeither_DoesNotThrow(int? version, string? value)
    {
        Exception? exception = Record.Exception(() => FederatedCredentialValidator.ValidateClaimsMatchingExpression(version, value));

        Assert.Null(exception);
    }
}
