// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Reflection.Emit;
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetVersion_ShouldReturnFallback_WhenInformationalVersionIsAbsent(string? informationalVersion)
    {
        var assemblyName = new AssemblyName("VersionFallbackTest")
        {
            Version = new Version(1, 2, 3, 4),
        };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
                [informationalVersion]));
        }

        Assert.Equal("unknown", AssemblyVersionProvider.GetVersion(assembly));
        Assert.Equal("0.0.0", AssemblyVersionProvider.GetVersion(assembly, "0.0.0"));
    }
}
