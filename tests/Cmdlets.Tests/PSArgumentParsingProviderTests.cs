// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Moq;

namespace Cmdlets.Tests;

/// <summary>
/// Regression tests for <see cref="PSArgumentParsingProvider"/>. The parser bridges the
/// PowerShell cmdlet layer (whose <c>-ParameterName</c> matches the C# property on the cmdlet
/// class) and the processor layer (whose C# property can differ). The parser must route by
/// <see cref="ParameterMetadata.Name"/> so that the downstream reflection call
/// <c>processor.GetType().GetProperty(name)</c> targets the processor property, not the PS
/// parameter name. Missing-profile binding diagnostics must use the unprefixed invocation name.
/// </summary>
public class PSArgumentParsingProviderTests
{
    private static ParameterMetadata Meta(string processorProperty, string invocationName, Type type, string[]? aliases = null) =>
        new(processorProperty, invocationName, type, "test", IsRequired: false, aliases ?? [], AllowedValues: []);

    [Fact]
    public void ParseArguments_TranslatesKeyToProcessorPropertyName()
    {
        var provider = new PSArgumentParsingProvider();
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["PsParamName"] = Meta("ProcessorProperty", "PsParamName", typeof(string)),
        };
        var args = new Dictionary<string, object> { ["PsParamName"] = "value" };

        var result = provider.ParseArguments(args, parameters, parameterAliases: null);

        Assert.True(result.ContainsKey("ProcessorProperty"), "output key must be the processor property name");
        Assert.Equal("value", result["ProcessorProperty"]);
        Assert.False(result.ContainsKey("PsParamName"), "must not leak the PS-side parameter name to reflection");
    }

    [Fact]
    public void ParseArguments_LeavesKeyUnchangedWhenNameMatches()
    {
        // Existing cmdlets whose PS parameter name equals the processor property must keep working.
        var provider = new PSArgumentParsingProvider();
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["Filter"] = Meta("Filter", "Filter", typeof(string)),
        };
        var args = new Dictionary<string, object> { ["Filter"] = "expression" };

        var result = provider.ParseArguments(args, parameters, parameterAliases: null);

        Assert.True(result.ContainsKey("Filter"));
        Assert.Equal("expression", result["Filter"]);
    }

    [Fact]
    public void ParseArguments_PassesUnknownKeysThrough()
    {
        // Unknown keys are forwarded so downstream components can produce a clear error.
        var provider = new PSArgumentParsingProvider();
        var parameters = new Dictionary<string, ParameterMetadata>();
        var args = new Dictionary<string, object> { ["NotDeclared"] = "value" };

        var result = provider.ParseArguments(args, parameters, parameterAliases: null);

        Assert.True(result.ContainsKey("NotDeclared"));
        Assert.Equal("value", result["NotDeclared"]);
    }

    [Fact]
    public void ParseArguments_TranslatesArrayParametersToProcessorPropertyName()
    {
        var provider = new PSArgumentParsingProvider();
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["Select"] = Meta("Select", "Select", typeof(string[])),
        };
        var args = new Dictionary<string, object> { ["Select"] = new[] { "id", "displayName" } };

        var result = provider.ParseArguments(args, parameters, parameterAliases: null);

        Assert.True(result.ContainsKey("Select"));
        var value = Assert.IsType<string[]>(result["Select"]);
        Assert.Equal(new[] { "id", "displayName" }, value);
    }

    [Theory]
    [InlineData("SubscriptionId", false)]
    [InlineData("SubscriptionId", true)]
    [InlineData("Subscription", false)]
    [InlineData("Subscription", true)]
    public void BindArguments_MissingProfileDefault_UsesUnprefixedInvocationName(string invocationName, bool hasProfile)
    {
        Profile? profile = hasProfile ? new Profile { Name = "without-subscription" } : null;
        var profileService = new Mock<IProfileService>(MockBehavior.Strict);
        profileService.Setup(service => service.GetProfileKeysForDefault()).Returns(["SubscriptionId"]);
        profileService.Setup(service => service.GetProfileValueByKey(
            It.Is<Profile?>(actual => ReferenceEquals(actual, profile)), "SubscriptionId"))
            .Returns((string?)null);
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            [invocationName] = Meta(nameof(SubscriptionProcessor.SubscriptionId), invocationName, typeof(string)),
        };
        var processor = new SubscriptionProcessor();
        var binder = new ArgumentParsingService(new PSArgumentParsingProvider(), profileService.Object);

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, new Dictionary<string, object>(), parameters, parameterAliases: null, profile: profile));

        Assert.Equal($"Missing required argument '{invocationName}' and no value found in profile.", exception.Message);
        Assert.DoesNotContain("--", exception.Message);
        profileService.Verify(service => service.GetProfileValueByKey(profile, "SubscriptionId"), Times.Once);
    }

    private sealed class SubscriptionProcessor : IProcessor
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public IProcessor Init(ProcessorContext context) => this;

        public Task ProcessAsync() => Task.CompletedTask;
    }
}
