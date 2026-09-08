// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Tests;

/// <summary>
/// Azure CLI compatibility for `az login`: the legacy flag spellings must keep binding to the
/// Cirrus parameters so existing managed-identity scripts run unchanged.
/// </summary>
public class LoginCompatibilityAliasTests
{
    private static IArgumentParsingService CreateBinder() =>
        new ServiceCollection()
            .AddSingleton<ILoggingService, ConsoleLoggingService>()
            .AddSingleton<PropertyParser>()
            .AddSingleton<IProfileService, ProfileService>()
            .RegisterArgumentParsingServices()
            .AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>()
            .BuildServiceProvider()
            .GetRequiredService<IArgumentParsingService>();

    private static List<List<string>> Value(string value) => [[value]];

    [Theory]
    [InlineData("i")]
    [InlineData("identity")]
    public void CliBinding_IdentitySpellings_SetIdentitySwitch(string flag)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [flag] = new List<List<string>> { new() } },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "profile-sub" });

        Assert.True(processor.Identity);
    }

    [Fact]
    public void CliBinding_LegacyManagedIdentityInvocation_BindsEndToEnd()
    {
        // az login -i --client-id <guid> --subscription-id <sub> written the Azure CLI way.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["i"] = new List<List<string>> { new() },
                ["client-id"] = Value("11111111-1111-1111-1111-111111111111"),
                ["subscription-id"] = Value("sub-1"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "profile-sub" });

        Assert.True(processor.Identity);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.ClientId);
        Assert.Equal("sub-1", processor.SubscriptionId);
    }

    [Fact]
    public void CliMetadata_AdvertisesLegacyAliases()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);

        Assert.Contains("i", metadata.Parameters["identity"].Aliases);
    }

    [Fact]
    public void CliMetadata_DoesNotAliasSubscription()
    {
        // Cirrus exposes only --subscription-id; no other command aliases it, so login must not
        // either. This diverges from azure-cli's login, which defines --subscription/-s.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);

        Assert.Empty(metadata.Parameters["subscription-id"].Aliases);
        Assert.False(metadata.ParameterAliases!.ContainsKey("s"));
        Assert.False(metadata.ParameterAliases!.ContainsKey("subscription"));
    }
}
