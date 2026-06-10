using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
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
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_ShouldThrowArgumentException_WhenFirstTokenIsEmptyOrWhitespace(string token)
    {
        // Guards against IndexOutOfRangeException from `args[0][0]` on an empty/whitespace token.
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<ArgumentException>(() => dispatcher.Parse([token]));
        Assert.Contains("Command-group token cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("help", "")]
    [InlineData("help", "   ")]
    public void Parse_ShouldNotThrowIndexOutOfRange_WhenHelpSubcommandFollowedByEmptyGroupToken(string firstToken, string secondToken)
    {
        // `azv2 help ""` previously crashed inside TryCreateHelpRequest at groupToken[0].
        // The guard now causes the help-request path to bail out; the dispatcher falls through
        // to the standard command path, which raises a descriptive error instead.
        var dispatcher = new ExtensionDispatcher();

        var exception = Record.Exception(() => dispatcher.Parse([firstToken, secondToken]));
        Assert.NotNull(exception);
        Assert.IsNotType<IndexOutOfRangeException>(exception);
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
    public void Parse_ShouldThrowRuntimeExtensionHelpException_WhenGroupHelpRequested()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["login", "--help"]));

        Assert.Equal("login", exception.Verb);
        Assert.False(exception.IsCommandHelp);
        Assert.Equal(["login"], exception.Path);
    }

    [Fact]
    public void Parse_ShouldThrowRuntimeExtensionHelpException_WhenHelpSubcommandUsedForCommand()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["help", "login", "show"]));

        Assert.Equal("login", exception.Verb);
        Assert.True(exception.IsCommandHelp);
        Assert.Equal(["login", "show"], exception.Path);
    }

    [Fact]
    public void Parse_ShouldShortCircuitToHelp_BeforeCommandExecutionPath()
    {
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<RuntimeExtensionHelpException>(() => dispatcher.Parse(["login", "--help", "--subscription-id", "missing"]));

        Assert.Equal("login", exception.Verb);
        Assert.False(exception.IsCommandHelp);
        Assert.Equal(["login"], exception.Path);
    }

    [Fact]
    public void LoginHelpAdapter_ShouldProvideRuntimeCommandHelp()
    {
        var helpProvider = new ExtensionIndexHelpAdapter(new Microsoft.Azure.Cirrus.Extensions.Login.ExtensionIndex(), "Login");

        var groupHelp = helpProvider.GetGroupHelp(["login"]);
        var commandHelp = helpProvider.GetCommandHelp(["login", "show"]);

        Assert.NotNull(groupHelp);
        Assert.NotNull(commandHelp);
        Assert.Equal("login", groupHelp!.GroupPath);
        Assert.Equal("login show", commandHelp!.CommandPath);
    }

    // -------- Longest-prefix dispatch --------
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
        // Multi-token leaf keys with positional residual binding.
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
        // Tokens past the longest matched leaf become positional
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
        // path exists in the leaf index — exercises the verb-only fallback.
        var registry = new ExtensionRegistry();
        var builtIns = new Dictionary<string, BuiltInExtension>(StringComparer.OrdinalIgnoreCase)
        {
            ["login"] = new("login", "Microsoft.Azure.Cirrus.Extensions.Login"),
        };
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry), builtIns);
        // Empty leaf index — forces fallback.
        var emptyIndex = LeafIndex.Build(new Dictionary<string, BuiltInExtension>(), new ExtensionRegistry());
        var dispatcher = new ExtensionDispatcher(resolver, () => emptyIndex);

        var invocation = dispatcher.Parse(["login"]);

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
