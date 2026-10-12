// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Config;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for ExtensionDispatcher - the core router that loads extension assemblies
/// and dispatches commands to them. Help / version classification is covered by
/// <see cref="Parser.GlobalArgParserTests"/>; those shapes never reach this parser after CLIPS-519.
/// </summary>
public class ExtensionDispatcherTests
{

    [Fact]
    public void Parse_ShouldThrowArgumentException_WhenArgsEmpty()
    {
        // Bare `az` is dispatched by GlobalArgParser; reaching the dispatcher with no args is a programmer error.
        var dispatcher = new ExtensionDispatcher();

        Assert.Throws<ArgumentException>(() => dispatcher.Parse(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_ShouldThrowUsageError_WhenFirstTokenIsEmptyOrWhitespace(string token)
    {
        // Guards against IndexOutOfRangeException from `args[0][0]` on an empty/whitespace token.
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<UsageErrorException>(() => dispatcher.Parse([token]));
        Assert.Contains("Command-group token cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("help", "")]
    [InlineData("help", "   ")]
    public void Parse_ShouldNotThrowIndexOutOfRange_WhenHelpSubcommandFollowedByEmptyGroupToken(string firstToken, string secondToken)
    {
        // `az help ""` previously crashed inside the pre-CLIPS-519 help classifier. GlobalArgParser
        // now guards it (Help stays null, positional passes through); the dispatcher then treats
        // "help" as a normal verb and raises a descriptive not-found error.
        var dispatcher = new ExtensionDispatcher();

        var exception = Record.Exception(() => dispatcher.Parse([firstToken, secondToken]));
        Assert.NotNull(exception);
        Assert.IsNotType<IndexOutOfRangeException>(exception);
    }

    [Theory]
    [InlineData("version", "--definitely-not-real")]
    [InlineData("--version", "foo")]
    public void Parse_ShouldThrowUsageError_WhenIntrinsicVerbHasTrailingTokens(string verbToken, string trailing)
    {
        // `az version <junk>` / `az --version <junk>` are neither valid version requests nor unknown
        // command groups: `version` is a real intrinsic, the trailing token is just invalid. Report a
        // precise usage error (exit 2) rather than the misleading "command group not found". Bare
        // `az version` / `az --version` are dispatched by GlobalArgParser before the dispatcher runs.
        var dispatcher = new ExtensionDispatcher();

        var exception = Assert.Throws<UsageErrorException>(() => dispatcher.Parse([verbToken, trailing]));

        Assert.Equal("UsageError", exception.Code);
        Assert.Equal(2, exception.ExitCode);
        Assert.Contains("version", exception.Message, StringComparison.Ordinal);
        Assert.Contains(trailing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginHelpAdapter_ShouldProvideRuntimeCommandHelp()
    {
        var helpProvider = new ExtensionIndexHelpAdapter(
            new Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex().GetCommandTable(ClientType.CLI),
            "login");

        var groupHelp = helpProvider.GetGroupHelp(["login"]);
        var commandHelp = helpProvider.GetCommandHelp(["login"]);

        Assert.NotNull(groupHelp);
        Assert.NotNull(commandHelp);
        Assert.Equal("login", groupHelp!.GroupPath);
        Assert.Equal("login", commandHelp!.CommandPath);
    }

    // -------- Longest-prefix dispatch --------
    //
    // These tests build a dispatcher with a synthetic LeafIndex so we can exercise leaf-level
    // routing in isolation, independent of the on-disk registry.

    private static ExtensionDispatcher CreateLeafDispatcher(params (string Verb, string Path, string PackageId, bool IsBuiltIn, string Version)[] leaves)
    {
        // Post-CLIPS-622: every leaf routes through the registry; IsBuiltIn is retained for
        // caller readability only.
        var registry = new ExtensionRegistry();

        foreach (var leaf in leaves)
        {
            if (!registry.Extensions.TryGetValue(leaf.PackageId, out var entry))
            {
                entry = new ExtensionRegistryEntry
                {
                    Version = leaf.Version,
                    InstalledAt = DateTimeOffset.UtcNow,
                };
                registry.Extensions[leaf.PackageId] = entry;
            }
            entry.Contributions.Add(new Contribution { Verb = leaf.Verb, Path = leaf.Path });
        }

        var index = LeafIndex.Build(registry);
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        return new ExtensionDispatcher(resolver, () => index);
    }

    [Fact]
    public void Parse_ShouldRouteLeaf_ByLongestPrefixMatch()
    {
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Microsoft.Azure.Azclips.Extensions.Compute", false, "1.0.0"));

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
            ("vm", "create", "Pkg.A", false, "1.0.0"),
            ("vm", "create extra", "Pkg.A", false, "1.0.0"));

        var invocation = dispatcher.Parse(["vm", "create", "extra", "--name", "foo"]);

        Assert.Equal("vm create extra", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldBindResidualPositionalArgs_PastMatchedLeaf()
    {
        // Tokens past the longest matched leaf become positional
        // residual (parsed under the empty-string parameter key by ExtensionParser).
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Pkg.A", false, "1.0.0"));

        var invocation = dispatcher.Parse(["vm", "create", "myvm", "--size", "Standard_D2s_v3"]);

        Assert.Equal("vm create", invocation.CommandName);
        Assert.True(invocation.Parameters.ContainsKey(""));
        Assert.True(invocation.Parameters.ContainsKey("size"));
    }

    [Fact]
    public void Parse_ShouldRouteBuiltInRoot_ByVerb()
    {
        var dispatcher = CreateLeafDispatcher(
            ("login", "", "Microsoft.Azure.Azclips.Extensions.Accounts", true, string.Empty));

        var invocation = dispatcher.Parse(["login"]);

        Assert.Equal("Accounts", invocation.ExtensionName);
        Assert.Equal("login", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldComposeFullPath_FromBuiltInRootAndSubCommand()
    {
        // Built-ins contribute only (verb, ""). The dispatcher matches that, then prepends `verb`
        // to whatever ExtensionParser pulls out of the residual — yielding "login logout".
        var dispatcher = CreateLeafDispatcher(
            ("login", "", "Microsoft.Azure.Azclips.Extensions.Accounts", true, string.Empty));

        var invocation = dispatcher.Parse(["login", "logout"]);

        Assert.Equal("login logout", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldFallBackToVerbOnly_WhenNoLeafMatches_ButVerbIsKnown()
    {
        // Verb is known via the resolver's registry lookup but no leaf with that (verb, "") path
        // exists in the leaf index — exercises the verb-only fallback in the dispatcher.
        var registry = new ExtensionRegistry
        {
            Extensions =
            {
                ["Microsoft.Azure.Azclips.Extensions.Accounts"] = new ExtensionRegistryEntry
                {
                    Version = "0.0.0",
                    Verb = "login",
                },
            },
        };
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        // Empty leaf index — forces fallback.
        var emptyIndex = LeafIndex.Build(new ExtensionRegistry());
        var dispatcher = new ExtensionDispatcher(resolver, () => emptyIndex);

        var invocation = dispatcher.Parse(["login"]);

        Assert.Equal("Accounts", invocation.ExtensionName);
        Assert.Equal("login", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldThrow_WhenVerbIsUnknown()
    {
        // Truly unknown verb: not in LeafIndex, not in the resolver's built-in map, not in the
        // embedded hint table. Falls through to the trailing KeyNotFoundException, which the
        // host's outer catch converts to an AI-agent fallback attempt in Program.Main.
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Pkg.A", false, "1.0.0"));

        Assert.Throws<KeyNotFoundException>(() => dispatcher.Parse(["nonexistentverb"]));
    }

    [Fact]
    public void Parse_ShouldThrowCommandNotFound_WhenVerbKnown_ButLeafMissing_FlagVariant()
    {
        // Verb is discovered, leaf is not: `az vm --list` → CommandNotFound, not "install Compute".
        var dispatcher = CreateSplitDispatcher(
            leafIndexLeaves: [
                ("vm", "list",   "Microsoft.Azure.Azclips.Extensions.Compute"),
                ("vm", "create", "Microsoft.Azure.Azclips.Extensions.Compute"),
            ],
            resolverRegistry: new ExtensionRegistry());

        var ex = Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(["vm", "--list"]));

        Assert.Equal("CommandNotFound", ex.Code);
        Assert.Equal(4, ex.ExitCode);
        Assert.Equal("vm", ex.Verb);
        Assert.Empty(ex.Path);
        Assert.Contains("az vm --help", ex.Message, StringComparison.Ordinal);
        // `az vm` alone now renders group help, so the message must name the offending token
        // instead of claiming `az vm` is unrecognized.
        Assert.Contains("--list", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'az vm' is not a recognized command", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ShouldThrowCommandNotFound_WhenVerbKnown_ButLeafMissing_PositionalVariant()
    {
        // Same as flag variant but with a non-flag positional token: `az group foobar`.
        var dispatcher = CreateSplitDispatcher(
            leafIndexLeaves: [
                ("group", "list",   "Microsoft.Azure.Azclips.Extensions.Resources"),
                ("group", "create", "Microsoft.Azure.Azclips.Extensions.Resources"),
            ],
            resolverRegistry: new ExtensionRegistry());

        var ex = Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(["group", "foobar"]));

        Assert.Equal("CommandNotFound", ex.Code);
        Assert.Equal("group", ex.Verb);
        Assert.Equal(["foobar"], ex.Path);
        Assert.Contains("az group foobar", ex.Message, StringComparison.Ordinal);
        Assert.Contains("az group --help", ex.Message, StringComparison.Ordinal);
    }

    // A resolved built-in verb whose invocation lands on a command group (no runnable leaf) must
    // surface CommandNotFound with a `--help` hint, not be dispatched to the extension — otherwise
    // the extension throws a generic "not found" that falls through to the AI agent. Covers the
    // built-in path that #525 (verb-only fallback) does not reach because built-ins always resolve.

    [Theory]
    [InlineData(new[] { "agent" }, "agent", new string[0])]
    [InlineData(new[] { "agent", "tool" }, "agent", new[] { "tool" })]
    [InlineData(new[] { "agent", "tool", "resource" }, "agent", new[] { "tool", "resource" })]
    public void Parse_ShouldThrowCommandNotFound_WhenResolvedVerbLandsOnCommandGroup(
        string[] args, string expectedVerb, string[] expectedPath)
    {
        var registry = BuildAgentRegistry(["tool env", "tool resource find", "tool resource show"]);
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        var dispatcher = new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));

        var ex = Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(args));

        Assert.Equal("CommandNotFound", ex.Code);
        Assert.Equal(4, ex.ExitCode);
        Assert.Equal(expectedVerb, ex.Verb);
        Assert.Equal(expectedPath, ex.Path);
        Assert.Contains("az agent --help", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ShouldNameUnrecognizedArguments_WhenResolvedVerbLandsOnCommandGroup()
    {
        // The resolved-extension path throws from a different site than the verb-only fallback,
        // so it needs its own coverage that trailing flags survive into the message.
        var registry = BuildAgentRegistry(["tool env", "tool resource find"]);
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        var dispatcher = new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));

        var ex = Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(["agent", "--list"]));

        Assert.Empty(ex.Path);
        Assert.Contains("--list", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'az agent' is not a recognized command", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRouteRunnableLeaf_UnderCommandGroupVerb()
    {
        // The group gate must not swallow a valid leaf under the same verb.
        var registry = BuildAgentRegistry(["tool env", "tool resource find"]);
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        var dispatcher = new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));

        var invocation = dispatcher.Parse(["agent", "tool", "env"]);

        Assert.Equal("agent tool env", invocation.CommandName);
    }

    [Fact]
    public void Parse_ShouldRouteRunnableRootVerb_WhenBuiltInHasNoChildren()
    {
        // An extension whose only leaf is its root (login) stays runnable — the gate must not fire.
        var registry = new ExtensionRegistry
        {
            Extensions =
            {
                ["Microsoft.Azure.Azclips.Extensions.Accounts"] = new ExtensionRegistryEntry
                {
                    Version = "0.0.0",
                    Contributions = { new Contribution { Verb = "login", Path = "" } },
                },
            },
        };
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        var dispatcher = new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));

        var invocation = dispatcher.Parse(["login", "--subscription-id", "abc"]);

        Assert.Equal("login", invocation.CommandName);
    }

    private static ExtensionRegistry BuildAgentRegistry(string[] leafPaths)
    {
        var entry = new ExtensionRegistryEntry
        {
            Version = "0.0.0",
            Contributions = { new Contribution { Verb = "agent", Path = "" } },
        };
        foreach (var path in leafPaths)
        {
            entry.Contributions.Add(new Contribution { Verb = "agent", Path = path });
        }
        return new ExtensionRegistry
        {
            Extensions = { ["Microsoft.Azure.Azclips.Extensions.Agent"] = entry },
        };
    }

    [Fact]
    public void Parse_ShouldStillThrowExtensionNotInstalled_WhenVerbAbsentFromLeafIndex_ButHinted()
    {
        // Regression guard: genuinely-uninstalled verbs still get the "install this" hint.
        // Relies on "vm" being in the embedded extension-hints.json (see ExtensionHintTableEmbeddedTests).
        var dispatcher = CreateSplitDispatcher(
            leafIndexLeaves: [],
            resolverRegistry: new ExtensionRegistry());

        var ex = Assert.Throws<ExtensionNotInstalledException>(() => dispatcher.Parse(["vm", "list"]));

        Assert.Equal("ExtensionNotFound", ex.Code);
        Assert.Equal("vm", ex.Verb);
        Assert.Contains("Compute", ex.PackageId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a dispatcher whose LeafIndex and resolver can be seeded independently — necessary
    /// to reproduce the production divergence where discovery (LeafIndex) sees an installed
    /// extension but the file-registry-backed resolver doesn't.
    /// </summary>
    private static ExtensionDispatcher CreateSplitDispatcher(
        IReadOnlyList<(string Verb, string Path, string PackageId)> leafIndexLeaves,
        ExtensionRegistry resolverRegistry)
    {
        var leafRegistry = new ExtensionRegistry();
        foreach (var leaf in leafIndexLeaves)
        {
            if (!leafRegistry.Extensions.TryGetValue(leaf.PackageId, out var entry))
            {
                entry = new ExtensionRegistryEntry
                {
                    Version = "1.0.0",
                    InstalledAt = DateTimeOffset.UtcNow,
                };
                leafRegistry.Extensions[leaf.PackageId] = entry;
            }
            entry.Contributions.Add(new Contribution { Verb = leaf.Verb, Path = leaf.Path });
        }

        var index = LeafIndex.Build(leafRegistry);
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(resolverRegistry));
        return new ExtensionDispatcher(resolver, () => index);
    }

    [Fact]
    public void Parse_ShouldRouteLeafWithSubcommandPath_WithPositionalResidual()
    {
        // Regression: `az extension remove Resources` must resolve to (extension, remove) via
        // the extension's registered (verb, path) leaves and surface "Resources" as a positional
        // residual under the empty parameter key — not fall through to verb-only fallback and
        // yield the bogus command name "extension remove Resources".
        var registry = new ExtensionRegistry
        {
            Extensions =
            {
                ["Microsoft.Azure.Azclips.Extensions.Extension"] = new ExtensionRegistryEntry
                {
                    Version = "0.0.0",
                    Contributions =
                    {
                        new Contribution { Verb = "extension", Path = "" },
                        new Contribution { Verb = "extension", Path = "add" },
                        new Contribution { Verb = "extension", Path = "list" },
                        new Contribution { Verb = "extension", Path = "show" },
                        new Contribution { Verb = "extension", Path = "remove" },
                    },
                },
            },
        };
        var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
        var index = LeafIndex.Build(registry);
        var dispatcher = new ExtensionDispatcher(resolver, () => index);

        var invocation = dispatcher.Parse(["extension", "remove", "Resources"]);

        Assert.Equal("extension remove", invocation.CommandName);
        Assert.True(invocation.Parameters.ContainsKey(""));
        var positional = Assert.IsType<List<List<string>>>(invocation.Parameters[""]);
        Assert.Equal("Resources", positional[0][0]);
    }

    [Fact]
    public void Parse_ShouldCarrySelectedInstalledVersion_OnLeafMatch()
    {
        var dispatcher = CreateLeafDispatcher(
            ("vm", "create", "Microsoft.Azure.Azclips.Extensions.Compute", false, "1.2.3"));

        var invocation = dispatcher.Parse(["vm", "create"]);

        Assert.Equal("1.2.3", invocation.ModuleVersion);
    }

    // -------- Intermediate-group help path --------
    //
    // Regression: `az deployment group --help` is parsed with IsCommandHelp=true (the parser
    // flags any path with extra tokens past the verb), but the leaf is `deployment group create` —
    // so `deployment group` is an intermediate group, not a command. The help dispatcher must
    // fall back to GetGroupHelp on a GetCommandHelp miss instead of throwing
    // KeyNotFoundException, which used to bubble out as an unhandled exception.

    [Fact]
    public void ExtensionIndexHelpAdapter_ShouldReturnNullCommand_AndNonNullGroup_ForIntermediatePath()
    {
        // Companion contract: when the parser flags the path as command help but it actually
        // points at an intermediate group, GetCommandHelp returns null and GetGroupHelp returns
        // the group with the leaf as a sub-command. RuntimeHelpDispatcher relies on this to
        // recover from the parser's optimistic IsCommandHelp=true flag.
        var index = new StubExtensionIndex(cliLeaf: "deployment group create");
        var adapter = new ExtensionIndexHelpAdapter(index.GetCommandTable(ClientType.CLI), "deployment");

        var commandAtIntermediatePath = adapter.GetCommandHelp(["deployment", "group"]);
        var groupAtIntermediatePath = adapter.GetGroupHelp(["deployment", "group"]);

        Assert.Null(commandAtIntermediatePath);
        Assert.NotNull(groupAtIntermediatePath);
        Assert.Equal("deployment group", groupAtIntermediatePath!.GroupPath);
        var createCommand = Assert.Single(groupAtIntermediatePath.Commands);
        Assert.Equal("create", createCommand.Name);
    }

    // -------- Intermediate-group summary population --------

    [Fact]
    public void ExtensionIndexHelpAdapter_ShouldPopulateIntermediateGroupSummary_FromMarkdown()
    {
        // Relies on real corpus at src/CLI/Help/Docs/deployment/group/index.md.
        var index = new StubExtensionIndex(cliLeaf: "deployment group create");
        var adapter = new ExtensionIndexHelpAdapter(index.GetCommandTable(ClientType.CLI), "deployment");

        var groupHelp = adapter.GetGroupHelp(["deployment", "group"]);

        Assert.NotNull(groupHelp);
        Assert.False(
            string.IsNullOrWhiteSpace(groupHelp!.Summary),
            "Intermediate group summary must be sourced from deployment/group/index.md, not empty.");
    }

    [Fact]
    public void ExtensionIndexHelpAdapter_ShouldPopulateSubgroupRowSummary_FromMarkdown()
    {
        var index = new StubExtensionIndex(cliLeaf: "deployment group create");
        var adapter = new ExtensionIndexHelpAdapter(index.GetCommandTable(ClientType.CLI), "deployment");

        var parent = adapter.GetGroupHelp(["deployment"]);

        Assert.NotNull(parent);
        var groupRow = Assert.Single(parent!.Subgroups);
        Assert.Equal("group", groupRow.Name);
        Assert.False(
            string.IsNullOrWhiteSpace(groupRow.Summary),
            "Sub-group row must carry the summary from its own index.md.");
    }

    /// <summary>
    /// Minimal in-memory <see cref="IExtensionIndex"/> for adapter-level tests. Supports a single
    /// CLI leaf so we can model intermediate-group shapes (e.g. <c>deployment group create</c>)
    /// without pulling in a real extension assembly.
    /// </summary>
    private sealed class StubExtensionIndex(string cliLeaf) : IExtensionIndex
    {
        private static readonly CommandMetadata s_metadata = new(
            Name: "Stub",
            InvocationName: "Stub",
            Aliases: Array.Empty<string>(),
            FullName: "Stub.Processors.StubProcessor",
            AssemblyName: "Stub",
            ProcessorType: typeof(StubExtensionIndex),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        private readonly Dictionary<string, CommandMetadata> _table = new(StringComparer.OrdinalIgnoreCase)
        {
            [cliLeaf] = s_metadata,
        };

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
            => _table.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException(name);

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => _table;

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => _table.Values;

        public IEnumerable<string> GetCommandKeys(ClientType clientType) => _table.Keys;
    }
}
