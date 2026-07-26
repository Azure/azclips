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
