// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Runtime;

namespace Cli.Tests.ArgumentParsing;

public class PropertyParserTests
{
    private static PropertyParser CreateParser() => new(new ConsoleLoggingService());

    [Theory]
    [InlineData(typeof(int?), "42", 42)]
    [InlineData(typeof(bool?), "true", true)]
    public void ConvertToType_NullableValueType_UnwrapsAndConvertsUnderlyingType(Type nullableType, string value, object expected)
    {
        var parser = CreateParser();

        var result = parser.ConvertToType("property", value, nullableType);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToType_NullableInt_InvalidValue_ThrowsUsingUnderlyingTypeMessage()
    {
        // The error should mention the property, not the Nullable<T> wrapper, since the user only
        // ever sees the CLI flag name, not the underlying .NET type.
        var parser = CreateParser();

        var ex = Assert.Throws<PropertyParserException>(() => parser.ConvertToType("claims-matching-expression-version", "not-an-int", typeof(int?)));

        Assert.Contains("claims-matching-expression-version", ex.Message);
        Assert.Contains("integer", ex.Message);
    }

    [Fact]
    public void ConvertToType_NonNullableInt_StillConvertsNormally()
    {
        // Guards against the Nullable unwrap accidentally affecting non-nullable value types.
        var parser = CreateParser();

        var result = parser.ConvertToType("count", "7", typeof(int));

        Assert.Equal(7, result);
    }
}
