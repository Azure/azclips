// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for the <c>--custom</c> JMESPath validation in <see cref="JmesPathCondition"/>.</summary>
public class JmesPathConditionTests
{
    // The text after "is not valid JMESPath: " comes from the JmesPath.Net parser, so only our part is asserted.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("properties.[")]
    public void Validate_InvalidExpression_ThrowsInvalidInput(string expression)
    {
        var ex = Assert.Throws<InvalidInputException>(() => JmesPathCondition.Validate(expression));

        Assert.StartsWith($"--custom expression '{expression}' is not valid JMESPath: ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidExpression_DoesNotThrow()
    {
        Assert.Null(Record.Exception(() => JmesPathCondition.Validate("properties.provisioningState == 'Succeeded'")));
    }
}
