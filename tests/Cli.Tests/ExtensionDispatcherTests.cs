using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Extensions;
using Microsoft.Azure.Cirrus.Extensions.Config;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for ExtensionDispatcher - the core router that loads extension assemblies
/// and dispatches commands to them.
/// </summary>
public class ExtensionDispatcherTests
{

    [Fact]
    public void Parse_ShouldThrowArgumentException_WhenArgsEmpty()
    {
        // Arrange
        var dispatcher = new ExtensionDispatcher();
        var emptyArgs = Array.Empty<string>();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => dispatcher.Parse(emptyArgs));
        Assert.Contains("Arguments cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("?")]
    [InlineData("-?")]
    public void Parse_ShouldThrowDispatchHelpException_WhenHelpFlagProvided(string helpFlag)
    {
        // Arrange
        var dispatcher = new ExtensionDispatcher();
        var args = new[] { helpFlag };
        
        // Act & Assert
        Assert.Throws<DispatchHelpException>(() => dispatcher.Parse(args));
    }

    [Fact]
    public void ConfigExtensionIndex_ShouldExposeTranslatedPowerShellParameters()
    {
        var extensionIndex = new ExtensionIndex();

        var getMetadata = extensionIndex.GetCommandMetadata("Get-CirrusConfig", ClientType.PowerShell);
        Assert.Contains("Json", getMetadata.Parameters.Keys);

        var listMetadata = extensionIndex.GetCommandMetadata("Get-CirrusConfigList", ClientType.PowerShell);
        Assert.Contains("All", listMetadata.Parameters.Keys);
        Assert.Contains("Json", listMetadata.Parameters.Keys);
    }

    // -------- Shape B (Phase 7) longest-prefix dispatch --------
    //
    // These tests build a dispatcher with a synthetic LeafIndex so we can exercise leaf-level
    // routing in isolation, independent of the on-disk registry.

    private static ExtensionDispatcher CreateLeafDispatcher(params (string Verb, string Path, string PackageId, bool IsBuiltIn)[] leaves)
    {
        var registry = new ExtensionRegistry();
        var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase);

        foreach (var leaf in leaves)
        {
            if (leaf.IsBuiltIn)
            {
                builtIns[leaf.Verb] = new BuiltInExtension(leaf.Verb, leaf.PackageId);
            }
            else
            {
                if (!registry.Extensions.TryGetValue(leaf.PackageId, out var entry))
                {
                    entry = new ExtensionRegistryEntry
                    {
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                    };
                    registry.Extensions[leaf.PackageId] = entry;
                }
                entry.Contributions.Add(new Contribution { Verb = leaf.Verb, Path = leaf.Path });
            }
        }

        var index = LeafIndex.Build(builtIns, registry);
        // Resolver is not consulted on exact leaf hits; supply a permissive built-in-only resolver
        // so the verb-only fallback path still works in tests that exercise it.
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry), builtIns);
        return new ExtensionDispatcher(resolver, () => index);
    }

    [Fact]
    public void Parse_ShouldRouteLeaf_ByLongestPrefixMatch()
    {
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Microsoft.Azure.Cirrus.Extensions.Compute", false));

        var invocation = dispatcher.Parse(["vm", "create", "--name", "foo"]);

        Assert.Equal("Compute", invocation.ExtensionName);
        Assert.Equal("vm create", invocation.CommandName);
        Assert.True(invocation.Parameters.ContainsKey("name"));
    }

    [Fact]
    public void Parse_ShouldPreferLongerLeaf_OverShorterMatch()
    {
        // Closes Shape A deferred #17: multi-token leaf keys with positional residual binding.
        // Both ("vm", "create") and ("vm", "create extra") are registered; the longer wins.
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Pkg.A", false),
            ("vm", "create extra", "Pkg.A", false));

        var invocation = dispatcher.Parse(["vm", "create", "extra", "--name", "foo"]);

        Assert.Equal("vm create extra", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldBindResidualPositionalArgs_PastMatchedLeaf()
    {
        // Closes Shape A deferred #17: tokens past the longest matched leaf become positional
        // residual (parsed under the empty-string parameter key by ExtensionParser).
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Pkg.A", false));

        var invocation = dispatcher.Parse(["vm", "create", "myvm", "--size", "Standard_D2s_v3"]);

        Assert.Equal("vm create", invocation.CommandName);
        Assert.True(invocation.Parameters.ContainsKey(""));
        Assert.True(invocation.Parameters.ContainsKey("size"));
    }

    [Fact]
    public void Parse_ShouldRouteBuiltInRoot_ByVerb()
    {
        var dispatcher = CreateLeafDispatcher(
            ("login", "", "Microsoft.Azure.Cirrus.Extensions.Login", true));

        var invocation = dispatcher.Parse(["login"]);

        Assert.Equal("Login", invocation.ExtensionName);
        Assert.Equal("login", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldComposeFullPath_FromBuiltInRootAndSubCommand()
    {
        // Built-ins contribute only (verb, ""). The dispatcher matches that, then prepends `verb`
        // to whatever ExtensionParser pulls out of the residual — yielding "login logout".
        var dispatcher = CreateLeafDispatcher(
            ("login", "", "Microsoft.Azure.Cirrus.Extensions.Login", true));

        var invocation = dispatcher.Parse(["login", "logout"]);

        Assert.Equal("login logout", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldFallBackToVerbOnly_WhenNoLeafMatches_ButVerbIsKnown()
    {
        // Verb is registered via the resolver (built-in seed) but no leaf with that (verb, "")
        // path exists in the leaf index either — exercises the fallback path used by group help.
        var registry = new ExtensionRegistry();
        var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
        {
            ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
        };
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry), builtIns);
        // Empty leaf index — forces fallback.
        var emptyIndex = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), new ExtensionRegistry());
        var dispatcher = new ExtensionDispatcher(resolver, () => emptyIndex);

        var invocation = dispatcher.Parse(["login", "--help"]);

        Assert.Equal("Login", invocation.ExtensionName);
        Assert.Equal("login", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldThrow_WhenVerbIsUnknown()
    {
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Pkg.A", false));

        Assert.Throws<KeyNotFoundException>(() => dispatcher.Parse(["nonexistentverb"]));
    }

    [Fact]
    public void Parse_ShouldRouteBuiltInLeaf_WithPositionalResidual()
    {
        // Regression: `azv2 extension remove Resources` must resolve to (extension, remove) via
        // the built-in's seeded Leaves and surface "Resources" as a positional residual under the
        // empty parameter key — not fall through to verb-only fallback and yield the bogus
        // command name "extension remove Resources". See BuiltInExtensions.Map.
        var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
        {
            ["extension"] = new("extension", "Microsoft.Azure.Cirrus.Extensions.Extension")
            {
                Leaves = new[] { "add", "list", "show", "remove" },
            },
        };
        var registry = new ExtensionRegistry();
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry), builtIns);
        var index = LeafIndex.Build(builtIns, registry);
        var dispatcher = new ExtensionDispatcher(resolver, () => index);

        var invocation = dispatcher.Parse(["extension", "remove", "Resources"]);

        Assert.Equal("extension remove", invocation.CommandName);
        Assert.True(invocation.Parameters.ContainsKey(""));
        var positional = Assert.IsType<List<List<string>>>(invocation.Parameters[""]);
        Assert.Equal("Resources", positional[0][0]);
    }

    private sealed class InMemoryRegistryStore(ExtensionRegistry registry) : IExtensionRegistryStore
    {
        public ExtensionRegistry Load() => registry;
        public void Save(ExtensionRegistry registry) { /* no-op for tests */ }
    }
}
