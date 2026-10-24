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

    [Fact]
    public void GetVersion_ShouldReturnNumericAssemblyVersion_WhenInformationalVersionIsAbsent()
    {
        var assemblyName = new AssemblyName("VersionFallbackTest")
        {
            Version = new Version(1, 2, 3, 4),
        };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        Assert.Equal("1.2.3.4", AssemblyVersionProvider.GetVersion(assembly));
    }
}
