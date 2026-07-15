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
}
