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
    private const string ValidSubscriptionId = "11111111-1111-1111-1111-111111111111";

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
                ["subscription-id"] = Value(ValidSubscriptionId),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "profile-sub" });

        Assert.True(processor.Identity);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.ClientId);
        Assert.Equal(ValidSubscriptionId, processor.SubscriptionId);
    }

    [Fact]
    public void CliBinding_IdentityWithoutSubscription_ShouldBindWhenNoProfileExists()
    {
        // az login --identity before any profile exists: the subscription is resolved after
        // authenticating, so binding must not demand one.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { ["identity"] = new List<List<string>> { new() } },
            metadata.Parameters,
            metadata.ParameterAliases,
            profile: null);

        Assert.True(processor.Identity);
        Assert.Equal(string.Empty, processor.SubscriptionId);
    }

    [Fact]
    public void CliBinding_WithoutSubscription_ShouldStillDefaultFromProfile()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { ["identity"] = new List<List<string>> { new() } },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "profile-sub" });

        Assert.Equal("profile-sub", processor.SubscriptionId);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("subscription")]
    public void CliBinding_SubscriptionSpellings_SetSubscriptionId(string flag)
    {
        // az login -s <sub> / az login --subscription <sub> written the Azure CLI v2 way.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [flag] = Value("sub-1") },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal("sub-1", processor.SubscriptionId);
    }

    [Theory]
    [InlineData("t")]
    [InlineData("tenant")]
    public void CliBinding_TenantSpellings_SetTenantId(string flag)
    {
        // az login -t <tenant> / az login --tenant <tenant> written the Azure CLI v2 way.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { [flag] = Value("tenant-1") },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "profile-sub" });

        Assert.Equal("tenant-1", processor.TenantId);
    }

    [Fact]
    public void CliBinding_ClassicAliasInvocation_BindsEndToEnd()
    {
        // az login -i -t <tenant> --subscription <sub> mixing the v2 short/long alias spellings.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["i"] = new List<List<string>> { new() },
                ["t"] = Value("tenant-1"),
                ["subscription"] = Value("sub-1"),
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.True(processor.Identity);
        Assert.Equal("tenant-1", processor.TenantId);
        Assert.Equal("sub-1", processor.SubscriptionId);
    }

    [Fact]
    public void CliMetadata_AdvertisesLegacyAliases()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);

        Assert.Contains("i", metadata.Parameters["identity"].Aliases);
    }

    [Fact]
    public void CliMetadata_AdvertisesSubscriptionAndTenantAliases()
    {
        // Parity with azure-cli v2: login accepts --subscription-id -s --subscription and
        // --tenant-id -t --tenant.
        var metadata = new ExtensionIndex().GetCommandMetadata("login", ClientType.CLI);

        Assert.Contains("s", metadata.Parameters["subscription-id"].Aliases);
        Assert.Contains("subscription", metadata.Parameters["subscription-id"].Aliases);
        Assert.Contains("t", metadata.Parameters["tenant-id"].Aliases);
        Assert.Contains("tenant", metadata.Parameters["tenant-id"].Aliases);

        Assert.Equal("subscription-id", metadata.ParameterAliases!["s"]);
        Assert.Equal("subscription-id", metadata.ParameterAliases!["subscription"]);
        Assert.Equal("tenant-id", metadata.ParameterAliases!["t"]);
        Assert.Equal("tenant-id", metadata.ParameterAliases!["tenant"]);
    }
}
