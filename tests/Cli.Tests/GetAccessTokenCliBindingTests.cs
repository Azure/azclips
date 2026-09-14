// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

namespace Cli.Tests;

/// <summary>
/// CLI binding tests for <c>account get-access-token</c>. The engine binds parsed arguments onto the
/// processor by each parameter's metadata <see cref="ParameterMetadata.Name"/>, so a mismatch between
/// a registered Name and a processor property compiles fine but throws at runtime. These exercise the
/// real CLI parsing/binding pipeline to guard that, including the multi-valued <c>--scope</c>.
/// </summary>
public class GetAccessTokenCliBindingTests
{
    private static ArgumentParsingService CliBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService()));
    }

    private static List<List<string>> Value(params string[] values) => [[.. values]];

    [Fact]
    public void CliBinding_BindsResourceTypeTenantAndSubscription_ToProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("account get-access-token", ClientType.CLI);
        var processor = new GetAccessTokenProcessor();

        CliBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["resource-type"] = Value("key-vault"),
                ["tenant-id"] = Value("11111111-1111-1111-1111-111111111111"),
                ["subscription-id"] = Value("sub-1"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("key-vault", processor.ResourceType);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.Tenant);
        Assert.Equal("sub-1", processor.Subscription);
    }

    [Fact]
    public void CliBinding_BindsMultipleScopes_ToStringArray()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("account get-access-token", ClientType.CLI);
        var processor = new GetAccessTokenProcessor();

        CliBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["scope"] = Value("https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/Mail.Read"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(
            ["https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/Mail.Read"],
            processor.Scope);
    }
}
