// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;
using Core.Runtime;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class RuntimeHelpDispatcherTests
{
    [Fact]
    public void ShouldBoldHeaders_ShouldSuppressAnsiForArbitraryWriter()
    {
        using var output = new StringWriter();

        Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(output));
    }

    [Fact]
    public void ShouldBoldHeaders_ShouldUseOutputPolicyForConsoleOut()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");

            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(Console.Out));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void ShouldBoldHeaders_ShouldHonorForceColorForArbitraryWriterOnlyThroughPolicyInput()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var originalForceColor = Environment.GetEnvironmentVariable("FORCE_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", null);
            Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

            Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(new StringWriter()));
            Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
            Environment.SetEnvironmentVariable("FORCE_COLOR", originalForceColor);
        }
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldPreferRootLeafOwner_WhenMultipleExtensionsShareVerb()
    {
        // Root-leaf owner sorts later by PackageId; without the root-leaf preference the wrong
        // extension would be chosen and `az config --help` would diverge from `az config` dispatch.
        var subOnly = MakeInfo("A.Sub", new[] { "config foo bar" });
        var rootOwner = MakeInfo("Z.Root", new[] { "config", "config show" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { subOnly, rootOwner },
            "config",
            out var resolved));
        Assert.Equal("Z.Root", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldTieBreakByPackageId_WhenNoRootLeafOwner()
    {
        var first = MakeInfo("A.Sub", new[] { "config foo" });
        var second = MakeInfo("Z.Sub", new[] { "config bar" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { second, first },
            "config",
            out var resolved));
        Assert.Equal("A.Sub", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldTieBreakByPackageId_WhenMultipleRootLeafOwners()
    {
        var first = MakeInfo("A.Root", new[] { "config", "config a" });
        var second = MakeInfo("Z.Root", new[] { "config", "config b" });

        Assert.True(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { second, first },
            "config",
            out var resolved));
        Assert.Equal("A.Root", resolved.PackageId);
    }

    [Fact]
    public void TryResolveExtensionCore_ShouldReturnFalse_WhenNoOwner()
    {
        var only = MakeInfo("A.Sub", new[] { "vm list" });

        Assert.False(RuntimeHelpDispatcher.TryResolveExtensionCore(
            new[] { only },
            "config",
            out _));
    }

    private static DiscoveredExtensionInfo MakeInfo(string packageId, IEnumerable<string> commandKeys)
    {
        var commands = commandKeys.ToDictionary(
            static k => k,
            static k => new CommandMetadata(
                Name: k,
                InvocationName: k,
                Aliases: [],
                FullName: k,
                AssemblyName: string.Empty,
                ProcessorType: typeof(object),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase);

        return new DiscoveredExtensionInfo(
            PackageId: packageId,
            ResolvedVersion: "1.0.0",
            Verbs: Array.Empty<string>(),
            Commands: commands,
            Location: null);
    }
}
