// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Parser;
using Core.Cli.Version;
using Core.Abstractions;
using Core.Extensions;
using Xunit;

namespace Cli.Tests.Parser;

/// <summary>
/// Tests for <see cref="GlobalArgParser.Parse"/>. Every case in the CLIPS-519 precedence matrix
/// (see <c>scratch/CLIPS-519-TEMP-PR-PLAN.md</c>) is asserted here — the parser must preserve
/// existing <c>ExtensionDispatcher</c> behavior byte-for-byte for these shapes.
/// </summary>
public class GlobalArgParserTests
{
    // ─── Bare invocation ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyArgs_YieldsBareRootHelp()
    {
        var result = GlobalArgParser.Parse(Array.Empty<string>());

        Assert.True(result.IsBareInvocation);
        Assert.NotNull(result.Help);
        Assert.Empty(result.Help!.Path);
        Assert.Null(result.Version);
        Assert.Empty(result.Positional);
        Assert.False(result.Debug);
        Assert.Null(result.Output);
        Assert.False(result.NoCache);
    }

    // ─── Help family ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-help")]
    [InlineData("-?")]
    [InlineData("--?")]
    [InlineData("?")]
    [InlineData("help")]
    [InlineData("--HELP")]
    public void Parse_TopLevelHelpToken_YieldsRootHelp_WithoutBareFlag(string token)
    {
        // These are user-typed help; welcome banner must NOT fire (only bare `az` gets the banner).
        var result = GlobalArgParser.Parse(new[] { token });

        Assert.False(result.IsBareInvocation);
        Assert.NotNull(result.Help);
        Assert.Empty(result.Help!.Path);
        Assert.Null(result.Version);
    }

    [Fact]
    public void Parse_HelpSubcommandWithGroup_YieldsGroupHelpPath()
    {
        var result = GlobalArgParser.Parse(new[] { "help", "group" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group" }, result.Help!.Path);
    }

    [Fact]
    public void Parse_HelpSubcommandWithGroupAndCommand_YieldsFullPath()
    {
        var result = GlobalArgParser.Parse(new[] { "help", "group", "cmd" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group", "cmd" }, result.Help!.Path);
    }

    [Fact]
    public void Parse_HelpSubcommandStopsAtFirstFlag()
    {
        var result = GlobalArgParser.Parse(new[] { "help", "group", "cmd", "--foo", "bar" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group", "cmd" }, result.Help!.Path);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("-?")]
    [InlineData("--?")]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-help")]
    public void Parse_HelpSubcommandSkipsHelpTokensInsteadOfTreatingThemAsPath(string helpToken)
    {
        // Pre-refactor `StripHelpFlags` removed all help tokens (`?`, `-?`, `-h`, `--help`) from the
        // tail of an `az help ...` subcommand invocation. `az help group ?` must yield group help
        // for "group", not a bogus command-help request for "group ?".
        var result = GlobalArgParser.Parse(new[] { "help", "group", helpToken });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group" }, result.Help!.Path);
    }

    [Fact]
    public void Parse_TrailingHelpFlagOnGroup_YieldsGroupHelpPath()
    {
        var result = GlobalArgParser.Parse(new[] { "group", "--help" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group" }, result.Help!.Path);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("-help")]
    [InlineData("-?")]
    [InlineData("--?")]
    public void Parse_TrailingHelpFlagAliasOnGroup_YieldsGroupHelpPath(string helpToken)
    {
        var result = GlobalArgParser.Parse(new[] { "group", helpToken });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group" }, result.Help!.Path);
    }

    [Fact]
    public void Parse_TrailingHelpFlagOnCommand_YieldsCommandHelpPath()
    {
        var result = GlobalArgParser.Parse(new[] { "group", "cmd", "--help" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group", "cmd" }, result.Help!.Path);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-help")]
    [InlineData("-?")]
    [InlineData("--?")]
    public void Parse_HelpFlagAfterCommandOption_YieldsCommandHelpPath(string helpToken)
    {
        var result = GlobalArgParser.Parse(new[] { "group", "list", "--subscription-id", "abc", helpToken });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "group", "list" }, result.Help!.Path);
    }

    [Fact]
    public void Parse_HelpAliasAsParameterValue_DoesNotYieldCommandHelp()
    {
        // `?` is a declared bare alias, so this only passes while the post-flag guard holds.
        var result = GlobalArgParser.Parse(new[] { "group", "cmd", "--name", "?" });

        Assert.Null(result.Help);
        Assert.Equal(new[] { "group", "cmd", "--name", "?" }, result.Positional);
    }

    [Fact]
    public void Parse_HelpFlagAtArgsZeroWithTrailingTokens_DoesNotYieldHelp()
    {
        // Pre-refactor behavior: `az --help group` was NOT recognized as help (the dispatcher's
        // TryCreateHelpRequest required either args[0]=="help" or a help flag at index >= 1). It
        // fell through and errored as an unknown verb '--help'. Preserve.
        var result = GlobalArgParser.Parse(new[] { "--help", "group" });

        Assert.Null(result.Help);
        Assert.Equal(new[] { "--help", "group" }, result.Positional);
    }

    [Fact]
    public void Parse_HelpSubcommandWithEmptyGroupToken_FallsThroughWithoutCrash()
    {
        // `az help ""` previously guarded against IndexOutOfRangeException in TryCreateHelpRequest;
        // the parser must not fabricate a help request for it. Positional is preserved so the
        // dispatcher's own ArgumentException guard fires downstream.
        var result = GlobalArgParser.Parse(new[] { "help", "" });

        Assert.Null(result.Help);
        Assert.Equal(new[] { "help", "" }, result.Positional);
    }

    // ─── Version family ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_VersionVerb_YieldsJson()
    {
        var result = GlobalArgParser.Parse(new[] { "version" });

        Assert.NotNull(result.Version);
        Assert.Equal(VersionOutputFormat.Json, result.Version!.Format);
        Assert.Null(result.Help);
    }

    [Fact]
    public void Parse_VersionFlag_YieldsText()
    {
        var result = GlobalArgParser.Parse(new[] { "--version" });

        Assert.NotNull(result.Version);
        Assert.Equal(VersionOutputFormat.Text, result.Version!.Format);
    }

    [Theory]
    [InlineData("VERSION", "json")]
    [InlineData("--VERSION", "text")]
    public void Parse_VersionTokenIsCaseInsensitive(string token, string expected)
    {
        var result = GlobalArgParser.Parse(new[] { token });

        Assert.NotNull(result.Version);
        Assert.Equal(expected, result.Version!.Format.ToString().ToLowerInvariant());
    }

    [Theory]
    [InlineData("version", "foo")]
    [InlineData("--version", "foo")]
    public void Parse_VersionWithTrailingTokens_DoesNotDispatchVersion(string versionToken, string trailing)
    {
        // `az version foo` / `az --version foo` fall through to normal dispatch so the intrinsic
        // usage error ("Unrecognized arguments for 'version'") can fire — matching the
        // ExtensionDispatcherTests case that guards this shape.
        var result = GlobalArgParser.Parse(new[] { versionToken, trailing });

        Assert.Null(result.Version);
        Assert.Equal(new[] { versionToken, trailing }, result.Positional);
    }

    [Fact]
    public void Parse_HelpAndVersionPresent_HelpWins()
    {
        // `az version --help` carries both the version verb and a help flag. Pre-refactor the
        // dispatcher checked help before version; the parser must preserve that precedence.
        var result = GlobalArgParser.Parse(new[] { "version", "--help" });

        Assert.NotNull(result.Help);
        Assert.Equal(new[] { "version" }, result.Help!.Path);
        Assert.Null(result.Version);
    }

    // ─── --debug ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--debug", "group", "cmd")]
    [InlineData("group", "--debug", "cmd")]
    [InlineData("group", "cmd", "--debug")]
    [InlineData("group", "cmd", "--DEBUG")]
    public void Parse_DebugFlag_IsPositionInsensitiveAndCaseInsensitive(string a, string b, string c)
    {
        var result = GlobalArgParser.Parse(new[] { a, b, c });

        Assert.True(result.Debug);
        Assert.Equal(new[] { "group", "cmd" }, result.Positional);
    }

    [Fact]
    public void Parse_DebugFlagOnly_YieldsBareHelpButNotBareInvocation()
    {
        // `az --debug` alone → pre-refactor: ParseDebugFlag strips, dispatcher.Parse([]) throws
        // DispatchHelpException → root help renders, but no banner (isBareInvocation was captured
        // pre-strip and returned false). Preserve.
        var result = GlobalArgParser.Parse(new[] { "--debug" });

        Assert.False(result.IsBareInvocation);
        Assert.True(result.Debug);
        Assert.NotNull(result.Help);
        Assert.Empty(result.Help!.Path);
    }

    // ─── --output ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--output", "json", "json")]
    [InlineData("--output", "table", "table")]
    [InlineData("--OUTPUT", "JSON", "json")]
    public void Parse_OutputFlag_SpaceSeparated_ResolvesFormat(string flag, string value, string expected)
    {
        var result = GlobalArgParser.Parse(new[] { flag, value });

        Assert.Equal(expected, result.Output?.ToString().ToLowerInvariant());
        Assert.Empty(result.Positional);
    }

    [Theory]
    [InlineData("--output=json", "json")]
    [InlineData("--output=yaml", "yaml")]
    [InlineData("--Output=Json", "json")]
    public void Parse_OutputFlag_EqualsForm_ResolvesFormat(string token, string expected)
    {
        var result = GlobalArgParser.Parse(new[] { token });

        Assert.Equal(expected, result.Output?.ToString().ToLowerInvariant());
        Assert.Empty(result.Positional);
    }

    [Fact]
    public void Parse_OutputFlag_StripsSpaceSeparatedValueFromPositional()
    {
        var result = GlobalArgParser.Parse(new[] { "group", "--output", "json", "cmd" });

        Assert.Equal(OutputFormat.Json, result.Output);
        Assert.Equal(new[] { "group", "cmd" }, result.Positional);
    }

    [Theory]
    [InlineData("jsonc")]
    [InlineData("table")]
    [InlineData("tsv")]
    [InlineData("yaml")]
    [InlineData("yamlc")]
    public void Parse_OutputFlag_AcceptsAllRealFormats(string value)
    {
        var result = GlobalArgParser.Parse(new[] { "--output", value });

        Assert.Equal(value, result.Output?.ToString().ToLowerInvariant());
    }

    [Fact]
    public void Parse_OutputFlagWithUnknownValue_ThrowsUsageError()
    {
        // Q1 decision: strict enum. Pre-refactor silently treated unknown values as "not json"
        // (== plain). The parser now surfaces the typo instead of hiding it.
        var ex = Assert.Throws<AzclipsErrorException>(
            () => GlobalArgParser.Parse(new[] { "--output", "xml" }));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("--output", ex.Message);
        Assert.Contains("xml", ex.Message);
    }

    [Fact]
    public void Parse_OutputEqualsUnknownValue_ThrowsUsageError()
    {
        var ex = Assert.Throws<AzclipsErrorException>(
            () => GlobalArgParser.Parse(new[] { "--output=xml" }));

        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Parse_OutputFlagWithPlainValue_ThrowsUsageError()
    {
        // `plain` was retired: it used to double as both the unspecified default and a real
        // value, which is exactly the conflation this change resolves. No longer valid.
        var ex = Assert.Throws<AzclipsErrorException>(
            () => GlobalArgParser.Parse(new[] { "--output", "plain" }));

        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Parse_OutputFlagAtEndWithNoValue_ThrowsUsageError()
    {
        // Pre-refactor: silently left in args (no strip, no error). Q1 direction: raise, same as
        // an unknown value — both are user typos.
        var ex = Assert.Throws<AzclipsErrorException>(
            () => GlobalArgParser.Parse(new[] { "group", "--output" }));

        Assert.Equal(2, ex.ExitCode);
    }

    // ─── --query ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--query", "[].name", "[].name")]
    [InlineData("--QUERY", "[].name", "[].name")]
    public void Parse_QueryFlag_SpaceSeparated_ResolvesExpression(string flag, string value, string expected)
    {
        var result = GlobalArgParser.Parse(new[] { flag, value });

        Assert.Equal(expected, result.Query);
        Assert.Empty(result.Positional);
    }

    [Theory]
    [InlineData("--query=[].name", "[].name")]
    [InlineData("--Query=[?contains(name, 'a')].name", "[?contains(name, 'a')].name")]
    public void Parse_QueryFlag_EqualsForm_ResolvesExpression(string token, string expected)
    {
        var result = GlobalArgParser.Parse(new[] { token });

        Assert.Equal(expected, result.Query);
        Assert.Empty(result.Positional);
    }

    [Fact]
    public void Parse_QueryFlagAtEndWithNoValue_ThrowsUsageError()
    {
        var ex = Assert.Throws<AzclipsErrorException>(
            () => GlobalArgParser.Parse(new[] { "group", "--query" }));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("--query", ex.Message);
    }

    // ─── --no-cache ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--no-cache", "group", "cmd")]
    [InlineData("group", "--no-cache", "cmd")]
    [InlineData("group", "cmd", "--NO-CACHE")]
    public void Parse_NoCacheFlag_IsPositionInsensitiveAndCaseInsensitive(string a, string b, string c)
    {
        var result = GlobalArgParser.Parse(new[] { a, b, c });

        Assert.True(result.NoCache);
        Assert.Equal(new[] { "group", "cmd" }, result.Positional);
    }

    [Fact]
    public void Parse_NoCacheFlag_DoesNotMutateEnvironment()
    {
        // The parser is pure; the AZCLIPS_NO_EXTENSION_CACHE mutation lives at the call site in
        // Program.Main. This test guards the invariant so future changes don't reintroduce hidden
        // coupling.
        var before = Environment.GetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE");
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE", null);

            var result = GlobalArgParser.Parse(new[] { "--no-cache", "group" });

            Assert.True(result.NoCache);
            Assert.Null(Environment.GetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE", before);
        }
    }

    // ─── Combined ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AllGlobalFlagsTogether_AllSetAndStripped()
    {
        var result = GlobalArgParser.Parse(new[]
        {
            "--output", "json", "--debug", "--no-cache", "group", "cmd",
        });

        Assert.True(result.Debug);
        Assert.Equal(OutputFormat.Json, result.Output);
        Assert.True(result.NoCache);
        Assert.Equal(new[] { "group", "cmd" }, result.Positional);
        Assert.Null(result.Help);
        Assert.Null(result.Version);
        Assert.False(result.IsBareInvocation);
    }

    [Fact]
    public void Parse_NormalCommand_LeavesPositionalIntact()
    {
        var result = GlobalArgParser.Parse(new[] { "group", "cmd", "--name", "foo" });

        Assert.Equal(new[] { "group", "cmd", "--name", "foo" }, result.Positional);
        Assert.Null(result.Help);
        Assert.Null(result.Version);
    }
}
