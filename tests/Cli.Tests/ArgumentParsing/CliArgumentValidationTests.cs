// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;

namespace Cli.Tests.ArgumentParsing;

public class CliArgumentValidationTests
{
    private static Core.Cli.CLIArgumentParsingProvider CreateProvider()
    {
        var loggingService = new ConsoleLoggingService();
        return new Core.Cli.CLIArgumentParsingProvider(new Core.Cli.PropertyParser(loggingService), loggingService);
    }

    [Fact]
    public void ValidateArguments_MissingRequired_ReportsCliFlagName()
    {
        var provider = CreateProvider();

        // Mirrors `az vm list`, whose required parameters are exposed as CLI flags.
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["SubscriptionId"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id.", true, [], []),
            ["ResourceGroupName"] = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", true, [], []),
        };

        var errors = provider.ValidateArguments(new Dictionary<string, object>(), parameters);

        Assert.NotNull(errors);
        Assert.Contains("Required parameter '--subscription-id' is missing.", errors!);
        Assert.Contains("Required parameter '--resource-group' is missing.", errors!);
        Assert.DoesNotContain(errors!, e => e.Contains("SubscriptionId"));
        Assert.DoesNotContain(errors!, e => e.Contains("ResourceGroupName"));
    }

    [Fact]
    public void ParseArguments_MissingScalarValue_ErrorUsesInvocationName()
    {
        // Guards that when a CLI flag name differs from its underlying processor property, the
        // error message references the flag the user typed, not the internal property name.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["long-flag"] = new("LongFlagProperty", "long-flag", typeof(string), "desc", false, [], []),
        };
        var args = new Dictionary<string, object>
        {
            ["long-flag"] = new List<List<string>>(),
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases: null));

        Assert.Contains("'long-flag'", ex.Message);
        Assert.DoesNotContain("LongFlagProperty", ex.Message);
    }

    [Fact]
    public void ParseArguments_ScalarConversionFailure_ErrorUsesInvocationName()
    {
        // Type-conversion failures inside PropertyParser must also surface the CLI flag name.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["numeric-flag"] = new("NumericFlagProperty", "numeric-flag", typeof(int), "desc", false, [], []),
        };
        var args = new Dictionary<string, object>
        {
            ["numeric-flag"] = new List<List<string>> { new() { "not-an-int" } },
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases: null));

        Assert.Contains("'numeric-flag'", ex.Message);
        Assert.DoesNotContain("NumericFlagProperty", ex.Message);
    }

    [Fact]
    public void ParseArguments_BindsPositionalArgs_ViaIndexedAliases()
    {
        // Mirrors `az config set core.output.color always`: the [""] alias binds position 0 to
        // Key (matching the existing single-positional convention, e.g. `profile show <name>`),
        // and the ["1"] alias binds position 1 to Value.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key.", true, [], []),
            ["value"] = new("Value", "value", typeof(string), "Configuration value.", true, [], []),
            ["force"] = new("Force", "force", typeof(bool), "Allow unknown keys.", false, [], []),
        };
        var parameterAliases = new Dictionary<string, string> { [""] = "key", ["1"] = "value" };
        var args = new Dictionary<string, object>
        {
            [string.Empty] = new List<List<string>> { new() { "core.output.color" }, new() { "always" } },
        };

        var parsed = provider.ParseArguments(args, parameters, parameterAliases);

        Assert.Equal("core.output.color", parsed["Key"]);
        Assert.Equal("always", parsed["Value"]);
        Assert.DoesNotContain("Force", parsed.Keys);
    }

    [Fact]
    public void ParseArguments_PositionalArgs_CoexistWithExplicitNamedFlag()
    {
        // `az config set core.output.color --value always`: the key is positional (position 0)
        // while the value is supplied explicitly via --value.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key.", true, [], []),
            ["value"] = new("Value", "value", typeof(string), "Configuration value.", true, [], []),
        };
        var parameterAliases = new Dictionary<string, string> { [""] = "key", ["1"] = "value" };
        var args = new Dictionary<string, object>
        {
            [string.Empty] = new List<List<string>> { new() { "core.output.color" } },
            ["value"] = new List<List<string>> { new() { "always" } },
        };

        var parsed = provider.ParseArguments(args, parameters, parameterAliases);

        Assert.Equal("core.output.color", parsed["Key"]);
        Assert.Equal("always", parsed["Value"]);
    }

    [Fact]
    public void ParseArguments_PositionalArgs_WithoutIndexedAlias_Throws()
    {
        // A second positional token with no corresponding ["1"] alias must be rejected rather than
        // silently dropped.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key.", true, [], []),
        };
        var parameterAliases = new Dictionary<string, string> { [""] = "key" };
        var args = new Dictionary<string, object>
        {
            [string.Empty] = new List<List<string>> { new() { "core.output.color" }, new() { "always" } },
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases));

        Assert.Contains("Unrecognized positional argument", ex.Message);
    }

    [Fact]
    public void ParseArguments_PositionalArg_ConflictingWithExplicitFlag_Throws()
    {
        // `az config set core.output.color --key foo --value bar`: the positional token and the
        // --key flag both target Key. This must be rejected deterministically rather than silently
        // depending on dictionary iteration order to decide which one "wins".
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key.", true, [], []),
            ["value"] = new("Value", "value", typeof(string), "Configuration value.", true, [], []),
        };
        var parameterAliases = new Dictionary<string, string> { [""] = "key", ["1"] = "value" };
        var args = new Dictionary<string, object>
        {
            ["key"] = new List<List<string>> { new() { "foo" } },
            ["value"] = new List<List<string>> { new() { "bar" } },
            [string.Empty] = new List<List<string>> { new() { "core.output.color" } },
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases));

        Assert.Contains("positionally and by flag", ex.Message);
    }

    [Fact]
    public void ParseArguments_IndexedPositionalAliasKey_NotAcceptedAsNamedFlag()
    {
        // Indexed ParameterAliases keys ("1", "2", ...) exist only to bind positional slots; a
        // user typing `--1 always` must not silently resolve as `--value always`. It should
        // surface the real problem (unknown parameter "1"), not succeed or produce a misleading
        // positional/flag conflict error.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["key"] = new("Key", "key", typeof(string), "Configuration key.", true, [], []),
            ["value"] = new("Value", "value", typeof(string), "Configuration value.", true, [], []),
        };
        var parameterAliases = new Dictionary<string, string> { [""] = "key", ["1"] = "value" };
        var args = new Dictionary<string, object>
        {
            ["1"] = new List<List<string>> { new() { "always" } },
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases));

        Assert.Contains("Unable to find parameter: 1", ex.Message);
    }

    [Fact]
    public void ParseArguments_MissingScalarValue_ErrorUsesShortAliasWhenUserTypedIt()
    {
        // When the user types a short alias (e.g. "-l" for "--long-flag"), the error should
        // reflect the alias the user typed, not the canonical form the alias resolves to.
        var provider = CreateProvider();

        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["long-flag"] = new("LongFlagProperty", "long-flag", typeof(string), "desc", true, ["l"], []),
        };
        var parameterAliases = new Dictionary<string, string> { ["l"] = "long-flag" };
        var args = new Dictionary<string, object>
        {
            ["l"] = new List<List<string>>(),
        };

        var ex = Assert.Throws<ArgumentException>(() => provider.ParseArguments(args, parameters, parameterAliases));

        Assert.Contains("'l'", ex.Message);
        Assert.DoesNotContain("'long-flag'", ex.Message);
        Assert.DoesNotContain("LongFlagProperty", ex.Message);
    }
}
