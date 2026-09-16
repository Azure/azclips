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
    public void CliBinding_BindsResourceTypeAndTenant_ToProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("account get-access-token", ClientType.CLI);
        var processor = new GetAccessTokenProcessor();

        CliBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["resource-type"] = Value("key-vault"),
                ["tenant-id"] = Value("11111111-1111-1111-1111-111111111111"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("key-vault", processor.ResourceType);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.Tenant);
    }

    [Theory]
    [InlineData("t")]
    [InlineData("tenant")]
    public void CliBinding_ClassicTenantSpellings_BindToTenant(string flag)
    {
        // Classic `az account get-access-token --tenant <id>` / `-t <id>` must keep working.
        var metadata = new ExtensionIndex().GetCommandMetadata("account get-access-token", ClientType.CLI);
        var processor = new GetAccessTokenProcessor();

        CliBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                [flag] = Value("11111111-1111-1111-1111-111111111111"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.Tenant);
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
