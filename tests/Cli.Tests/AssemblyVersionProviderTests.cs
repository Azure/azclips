// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Cli.Tests;

public class AssemblyVersionProviderTests
{
    [Fact]
    public void GetVersion_ShouldReturnAuthoredVersionWithoutBuildMetadata()
    {
        var assembly = typeof(AssemblyVersionProviderTests).Assembly;
        var informationalVersion = assembly
            .GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType == typeof(AssemblyInformationalVersionAttribute))
            .ConstructorArguments[0]
            .Value!
            .ToString()!;

        var expected = informationalVersion.Split('+')[0];

        Assert.Equal(expected, AssemblyVersionProvider.GetVersion(assembly));
    }

    [Fact]
    public void GetVersion_ShouldReturnFallback_WhenAssemblyIsNull()
    {
        Assert.Equal("fallback", AssemblyVersionProvider.GetVersion(null, "fallback"));
    }
}
