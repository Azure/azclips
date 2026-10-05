using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Service-level binding regressions for subscription profile defaults, client-facing diagnostics,
/// and parsing and validation failures that must precede default resolution.
/// </summary>
public class ProfileSubscriptionResolutionTests
{
    private const string ProfileSubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string ExplicitSubscriptionId = "22222222-2222-2222-2222-222222222222";

    [Theory]
    [InlineData(ClientType.CLI, "group create", "subscription-id")]
    [InlineData(ClientType.PowerShell, "New-AzResourceGroup", "SubscriptionId")]
    public void CreateResourceGroup_SubscriptionId_ShouldBeOptional(ClientType clientType, string command, string parameterKey)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata(command, clientType);

        Assert.False(
            metadata.Parameters[parameterKey].IsRequired,
            $"SubscriptionId should be optional for '{command}' so it defaults to the current subscription.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CliBinding_MissingSubscriptionDefault_ReportsCliFlag(bool hasProfile)
    {
        Profile? profile = hasProfile ? new Profile { Name = "without-subscription" } : null;
        var profileService = CreateProfileService(profile);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, CreateArguments(), metadata.Parameters, metadata.ParameterAliases, profile));

        Assert.Equal("Missing required argument '--subscription-id' and no value found in profile.", exception.Message);
        Assert.DoesNotContain("SubscriptionId", exception.Message);
        profileService.Verify(service => service.GetProfileValueByKey(profile, "SubscriptionId"), Times.Once);
    }

    [Fact]
    public void CliBinding_OmittedSubscription_UsesProfileAndBindsOtherArguments()
    {
        var profile = ProfileWithSub(ProfileSubscriptionId);
        var profileService = CreateProfileService(profile);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);

        var result = binder.BindArgumentsToProcessor(
            processor, CreateArguments(), metadata.Parameters, metadata.ParameterAliases, profile);

        Assert.Same(processor, result);
        Assert.Equal(ProfileSubscriptionId, processor.SubscriptionId);
        Assert.Equal("issue498-rg", processor.Name);
        Assert.Equal("westus", processor.Location);
        profileService.Verify(service => service.GetProfileValueByKey(profile, "SubscriptionId"), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CliBinding_ExplicitSubscription_DoesNotReadProfileDefault(bool hasProfile)
    {
        Profile? profile = hasProfile ? ProfileWithSub(ProfileSubscriptionId) : null;
        var profileService = CreateProfileService(profile);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);
        var args = CreateArguments();
        args["subscription-id"] = new List<List<string>> { new() { ExplicitSubscriptionId } };

        binder.BindArgumentsToProcessor(
            processor, args, metadata.Parameters, metadata.ParameterAliases, profile);

        Assert.Equal(ExplicitSubscriptionId, processor.SubscriptionId);
        Assert.Equal("issue498-rg", processor.Name);
        Assert.Equal("westus", processor.Location);
        profileService.Verify(
            service => service.GetProfileValueByKey(It.IsAny<Profile?>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("location")]
    public void CliBinding_MissingRequiredArgument_FailsBeforeProfileDefaults(string missingArgument)
    {
        var profileService = CreateProfileService(profile: null);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);
        var args = CreateArguments();
        args.Remove(missingArgument);

        var exception = Assert.Throws<UsageErrorException>(() => binder.BindArgumentsToProcessor(
            processor, args, metadata.Parameters, metadata.ParameterAliases, profile: null));

        Assert.Equal("UsageError", exception.Code);
        Assert.Equal(2, exception.ExitCode);
        Assert.True(exception.HasMissingRequiredParameters);
        Assert.Equal($"Invalid arguments: Required parameter '--{missingArgument}' is missing.", exception.Message);
        profileService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CliBinding_ValuelessSubscription_FailsBeforeValidationAndProfileDefaults(bool hasProfile, bool missingName)
    {
        Profile? profile = hasProfile ? ProfileWithSub(ProfileSubscriptionId) : null;
        var profileService = CreateProfileService(profile);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);
        var args = CreateArguments();
        args["subscription-id"] = new List<List<string>>();
        if (missingName)
        {
            args.Remove("name");
        }

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, args, metadata.Parameters, metadata.ParameterAliases, profile));

        Assert.Equal(
            "Failed to parse parameter 'subscription-id': Parameter 'subscription-id' requires a value.",
            exception.Message);
        profileService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultBinding_MissingSubscriptionDefault_PreservesInternalName(bool hasProfile)
    {
        Profile? profile = hasProfile ? new Profile { Name = "without-subscription" } : null;
        var profileService = CreateProfileService(profile);
        var metadata = CreateMetadata();
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = new ArgumentParsingService(new DefaultArgumentParsingProvider(), profileService.Object);
        var args = new Dictionary<string, object>
        {
            ["Name"] = "issue498-rg",
            ["Location"] = "westus",
        };

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, args, metadata.Parameters, metadata.ParameterAliases, profile));

        Assert.Equal("Missing required argument 'SubscriptionId' and no value found in profile.", exception.Message);
        profileService.Verify(service => service.GetProfileValueByKey(profile, "SubscriptionId"), Times.Once);
    }

    [Fact]
    public void CliBinding_MissingDefaultMetadata_FallsBackToInternalKey()
    {
        var profileService = CreateProfileService(profile: null);
        var metadata = CreateMetadata();
        var parameters = new Dictionary<string, ParameterMetadata>(metadata.Parameters);
        parameters.Remove("subscription-id");
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, CreateArguments(), parameters, metadata.ParameterAliases, profile: null));

        Assert.Equal("Missing required argument 'SubscriptionId' and no value found in profile.", exception.Message);
        profileService.Verify(service => service.GetProfileValueByKey(null, "SubscriptionId"), Times.Once);
    }

    [Fact]
    public void CliBinding_DefaultMetadataLookup_IgnoresInternalNameCase()
    {
        var profileService = CreateProfileService(profile: null);
        var metadata = CreateMetadata();
        var parameters = new Dictionary<string, ParameterMetadata>(metadata.Parameters);
        parameters["subscription-id"] = parameters["subscription-id"] with { Name = "subscriptionid" };
        var processor = (NewResourceGroupProcessor)metadata.GetInstance();
        var binder = CreateCliBinder(profileService.Object);

        var exception = Assert.Throws<ArgumentException>(() => binder.BindArgumentsToProcessor(
            processor, CreateArguments(), parameters, metadata.ParameterAliases, profile: null));

        Assert.Equal("Missing required argument '--subscription-id' and no value found in profile.", exception.Message);
        profileService.Verify(service => service.GetProfileValueByKey(null, "SubscriptionId"), Times.Once);
    }

    private static CommandMetadata CreateMetadata() =>
        new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("group create", ClientType.CLI);

    private static Dictionary<string, object> CreateArguments() => new()
    {
        ["name"] = new List<List<string>> { new() { "issue498-rg" } },
        ["location"] = new List<List<string>> { new() { "westus" } },
    };

    private static ArgumentParsingService CreateCliBinder(IProfileService profileService)
    {
        var logging = Mock.Of<ILoggingService>();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging), profileService);
    }

    private static Mock<IProfileService> CreateProfileService(Profile? profile)
    {
        var profileService = new Mock<IProfileService>(MockBehavior.Strict);
        profileService.Setup(service => service.GetProfileKeysForDefault()).Returns(["SubscriptionId"]);
        profileService.Setup(service => service.GetProfileValueByKey(
            It.Is<Profile?>(actual => ReferenceEquals(actual, profile)), "SubscriptionId"))
            .Returns(profile?.SubscriptionId);
        return profileService;
    }

    private static Profile ProfileWithSub(string sub) => new()
    {
        Name = "p",
        TenantId = "33333333-3333-3333-3333-333333333333",
        SubscriptionId = sub,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };
}
