// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Runtime;
using Moq;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests.ArgumentParsing;

public class CliArgumentBindingTests
{
    [Theory]
    [InlineData("deployment group create", "resource-group", "test-rg", false)]
    [InlineData("deployment group what-if", "resource-group", "test-rg", false)]
    [InlineData("deployment sub create", "location", "westus", false)]
    [InlineData("deployment sub what-if", "location", "westus", false)]
    [InlineData("deployment sub validate", "location", "westus", false)]
    [InlineData("deployment sub create", "location", "westus", true)]
    [InlineData("deployment sub validate", "location", "westus", true)]
    public async Task DeploymentDefaults_BindAndReachArmWithExpectedNameAndMode(
        string command, string scopeFlag, string scopeValue, bool explicitOverrides)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata(command, ClientType.CLI);
        var (pipeline, output, sent) = Fake(_ => Status(System.Net.HttpStatusCode.OK,
            """{"name":"azuredeploy","status":"Succeeded","properties":{"provisioningState":"Succeeded","changes":[]}}"""));
        var processor = Configure(metadata.GetInstance(), pipeline, output, _ => { });
        var args = new List<string>
        {
            $"--{scopeFlag}", scopeValue, "--template-uri", "https://example.com/templates/azuredeploy.json"
        };
        if (explicitOverrides)
        {
            args.AddRange(["--name", "custom-deployment", "--mode", "Complete"]);
        }

        CreateBinder().BindArgumentsToProcessor(processor, Arguments(args.ToArray()),
            metadata, new Profile { SubscriptionId = SubscriptionId });
        await processor.ProcessAsync();

        Assert.NotEmpty(sent);
        Assert.All(sent, request =>
        {
            Assert.Contains($"/subscriptions/{SubscriptionId}/", request.Uri);
            Assert.Contains($"/deployments/{(explicitOverrides ? "custom-deployment" : "azuredeploy")}", request.Uri);
        });
        var bodies = sent.Where(request => request.Body is not null).ToList();
        Assert.NotEmpty(bodies);
        Assert.All(bodies, request =>
        {
            using var body = JsonDocument.Parse(request.Body!);
            var properties = body.RootElement.GetProperty("properties");
            Assert.Equal(explicitOverrides ? "Complete" : "Incremental", properties.GetProperty("mode").GetString());
            Assert.Equal("https://example.com/templates/azuredeploy.json",
                properties.GetProperty("templateLink").GetProperty("uri").GetString());
        });
    }

    [Theory]
    [InlineData("deployment group create", "resource-group")]
    [InlineData("deployment group what-if", "resource-group")]
    [InlineData("deployment sub create", "location")]
    [InlineData("deployment sub what-if", "location")]
    public void DeploymentDefaults_DoNotRelaxRequiredScopeArguments(string command, string requiredFlag)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata(command, ClientType.CLI);

        var error = Assert.Throws<MissingRequiredArgumentException>(() =>
            CreateBinder().BindArgumentsToProcessor(metadata.GetInstance(),
                Arguments("--template-uri", "https://example.com/templates/azuredeploy.json"),
                metadata, new Profile { SubscriptionId = SubscriptionId }));

        Assert.Contains($"Required parameter '--{requiredFlag}' is missing.", error.Message);
        Assert.DoesNotContain("Required parameter '--name' is missing.", error.Message);
        Assert.DoesNotContain("Required parameter '--mode' is missing.", error.Message);
    }

    [Theory]
    [InlineData("group create", "location", false)]
    [InlineData("group create", "location", true)]
    [InlineData("network vnet show", "resource-group", false)]
    [InlineData("network vnet show", "resource-group", true)]
    public async Task MissingRequiredServiceArguments_StopBeforeArmRequest(
        string command, string secondRequiredFlag, bool explicitSubscription)
    {
        IExtensionIndex index = command == "group create"
            ? new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            : new Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex();
        var metadata = index.GetCommandMetadata(command, ClientType.CLI);
        var (pipeline, output, sent) = Fake(_ => Status(System.Net.HttpStatusCode.NotFound,
            """{"message":"No HTTP resource was found that matches the request URI."}"""));
        var processor = Configure(metadata.GetInstance(), pipeline, output, _ => { });
        var args = explicitSubscription
            ? Arguments("--subscription-id", SubscriptionId)
            : new Dictionary<string, object>();

        var error = await Record.ExceptionAsync(async () =>
        {
            CreateBinder().BindArgumentsToProcessor(
                processor, args, metadata, new Profile { SubscriptionId = SubscriptionId });
            await processor.ProcessAsync();
        });

        Assert.True(sent.Count == 0,
            $"Unexpected ARM requests: {string.Join(", ", sent.Select(request => $"{request.Method} {request.Uri}"))}; error: {error}");
        var missing = Assert.IsType<MissingRequiredArgumentException>(error);
        Assert.Contains("Required parameter '--name' is missing.", missing.Message);
        Assert.Contains($"Required parameter '--{secondRequiredFlag}' is missing.", missing.Message);
        Assert.Equal(2, missing.ExitCode);
        Assert.False(missing.AgentFallbackEligible);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingRequiredArguments_AreAggregatedAndRequestHelp(bool withParameterSet)
    {
        var processor = new TestProcessor();

        var error = Assert.Throws<MissingRequiredArgumentException>(() =>
            CreateBinder().BindArgumentsToProcessor(processor, new Dictionary<string, object>(),
                Metadata(withParameterSet), profile: null));

        Assert.Contains("Required parameter '--name' is missing.", error.Message);
        Assert.Contains("Required parameter '--resource-group' is missing.", error.Message);
        Assert.DoesNotContain("ResourceGroupName", error.Message);
        Assert.Equal(2, error.ExitCode);
        Assert.False(error.AgentFallbackEligible);
        var invocation = new CommandInvocation("Test", "test create", new Dictionary<string, object>());
        Assert.True(ErrorHelpPolicy.ShouldShowCommandHelp(error, invocation, errorStreamIsHumanReadable: true));
        Assert.False(ErrorHelpPolicy.ShouldShowCommandHelp(error, invocation, errorStreamIsHumanReadable: false));
        Assert.Equal(string.Empty, processor.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfileDefault_BindsOptionalSubscriptionWithoutChangingSetResolution(bool withParameterSet)
    {
        var processor = new TestProcessor();

        var result = CreateBinder().BindArgumentsToProcessor(processor,
            Arguments("--name", "example", "--resource-group", "group"),
            Metadata(withParameterSet),
            new Profile { SubscriptionId = "profile-subscription" });

        Assert.Same(processor, result);
        Assert.Equal("example", processor.Name);
        Assert.Equal("group", processor.ResourceGroupName);
        Assert.Equal("profile-subscription", processor.SubscriptionId);
    }

    [Fact]
    public void ProfileDefault_DoesNotChangeExistingExplicitInputSetRequirements()
    {
        var metadata = Metadata(withParameterSet: true);
        metadata.ParameterSets!["Default"].Parameters!["SubscriptionId"] = new("SubscriptionId", IsRequired: true);
        var error = Assert.Throws<UsageErrorException>(() =>
            CreateBinder().BindArgumentsToProcessor(new TestProcessor(),
                Arguments("--name", "example", "--resource-group", "group"),
                metadata,
                new Profile { SubscriptionId = "profile-subscription" }));

        Assert.Contains("Invalid parameter set", error.Message);
        Assert.False(error.HasMissingRequiredParameters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfileDefault_DoesNotSatisfyGloballyRequiredExplicitInput(bool withParameterSet)
    {
        var error = Assert.Throws<MissingRequiredArgumentException>(() =>
            CreateBinder().BindArgumentsToProcessor(new TestProcessor(),
                Arguments("--name", "example", "--resource-group", "group"),
                Metadata(withParameterSet, requiredSubscription: true),
                new Profile { SubscriptionId = "profile-subscription" }));

        Assert.Contains("Required parameter '--subscription-id' is missing.", error.Message);
    }

    [Theory]
    [InlineData("storage account create", "name", "resource-group")]
    [InlineData("msgraph group member check", "group", "member-id")]
    public void MissingRequiredSetArguments_UseCliDiagnosticsForRealCommands(
        string command, string firstRequiredFlag, string secondRequiredFlag)
    {
        IExtensionIndex index = command.StartsWith("storage", StringComparison.Ordinal)
            ? new Microsoft.Azure.Azclips.Extensions.Storage.ExtensionIndex()
            : new Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex();
        var metadata = index.GetCommandMetadata(command, ClientType.CLI);

        var error = Assert.Throws<MissingRequiredArgumentException>(() =>
            CreateBinder().BindArgumentsToProcessor(metadata.GetInstance(),
                new Dictionary<string, object>(), metadata, new Profile { SubscriptionId = SubscriptionId }));

        Assert.Contains($"Required parameter '--{firstRequiredFlag}' is missing.", error.Message);
        Assert.Contains($"Required parameter '--{secondRequiredFlag}' is missing.", error.Message);
        Assert.DoesNotContain("candidate:", error.Message);
        var invocation = new CommandInvocation("Test", command, new Dictionary<string, object>());
        Assert.True(ErrorHelpPolicy.ShouldShowCommandHelp(error, invocation, errorStreamIsHumanReadable: true));
        Assert.False(error.AgentFallbackEligible);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("profile-subscription")]
    public void ExplicitSubscriptionAndAlias_BindWithoutBeingOverwritten(string? profileSubscription)
    {
        var processor = new TestProcessor();
        var profile = profileSubscription is null ? null : new Profile { SubscriptionId = profileSubscription };

        CreateBinder().BindArgumentsToProcessor(processor,
            Arguments("-n", "example", "--resource-group", "group", "--subscription-id", "explicit-subscription"),
            Metadata(withParameterSet: true, requiredSubscription: true), profile);

        Assert.Equal("example", processor.Name);
        Assert.Equal("explicit-subscription", processor.SubscriptionId);
    }

    [Fact]
    public void MissingProfileDefault_RemainsAnOrdinaryUsageError()
    {
        var error = Assert.Throws<UsageErrorException>(() =>
            CreateBinder().BindArgumentsToProcessor(new TestProcessor(),
                Arguments("--name", "example", "--resource-group", "group"),
                Metadata(withParameterSet: false), profile: null));

        Assert.Contains("subscription-id is not provided from input and not present in profile either", error.Message);
        Assert.False(error.HasMissingRequiredParameters);
        Assert.False(error.AgentFallbackEligible);
    }

    [Fact]
    public void InvalidAllowedValue_RemainsAnOrdinaryUsageError()
    {
        var error = Assert.Throws<UsageErrorException>(() =>
            CreateBinder().BindArgumentsToProcessor(new TestProcessor(),
                Arguments("--name", "example", "--resource-group", "group", "--mode", "invalid"),
                Metadata(withParameterSet: true), new Profile { SubscriptionId = "subscription" }));

        Assert.Contains("not allowed", error.Message);
        Assert.False(error.HasMissingRequiredParameters);
    }

    [Fact]
    public void ExistingParameterSetConflict_RemainsAnOrdinaryUsageError()
    {
        var metadata = Metadata(withParameterSet: true);
        metadata.ParameterSets!["Default"].Parameters!.Remove("Mode");

        var error = Assert.Throws<UsageErrorException>(() =>
            CreateBinder().BindArgumentsToProcessor(new TestProcessor(),
                Arguments("--name", "example", "--resource-group", "group", "--mode", "valid"),
                metadata, new Profile { SubscriptionId = "subscription" }));

        Assert.Contains("Invalid parameter set", error.Message);
        Assert.False(error.HasMissingRequiredParameters);
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetProfileKeysForDefault()).Returns(["SubscriptionId"]);
        profileService.Setup(service => service.GetProfileValueByKey(It.IsAny<Profile?>(), "SubscriptionId"))
            .Returns((Profile? profile, string _) => profile?.SubscriptionId);
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging), profileService.Object);
    }

    private static CommandMetadata Metadata(bool withParameterSet, bool requiredSubscription = false)
    {
        var parameters = new Dictionary<string, ParameterMetadata>
        {
            ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Subscription.", requiredSubscription, [], []),
            ["name"] = new("Name", "name", typeof(string), "Name.", true, ["n"], []),
            ["resource-group"] = new("ResourceGroupName", "resource-group", typeof(string), "Group.", true, [], []),
            ["mode"] = new("Mode", "mode", typeof(string), "Mode.", false, [], ["valid"]),
        };
        var sets = withParameterSet
            ? new Dictionary<string, ParameterSetMetadata>
            {
                ["Default"] = new("Default", IsDefault: true,
                    Parameters: parameters.Values.ToDictionary(
                        parameter => parameter.Name,
                        parameter => new ParameterReference(parameter.Name, parameter.IsRequired))),
            }
            : null;
        return new CommandMetadata("Test", "test create", [], typeof(TestProcessor).FullName!,
            typeof(TestProcessor).Assembly.GetName().Name!, typeof(TestProcessor), parameters,
            new Dictionary<string, string> { ["n"] = "name" }, sets);
    }

    private static IDictionary<string, object> Arguments(params string[] args) =>
        new ExtensionParser().Parse(["test", .. args]).Item2;

    private sealed class TestProcessor : IProcessor
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string? Mode { get; set; }

        public Task ProcessAsync() => Task.CompletedTask;
        public IProcessor Init(ProcessorContext context) => this;
    }
}
