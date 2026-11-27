// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Moq;

namespace Core.ArgumentParsing.Tests;

/// <summary>
/// Tests for how <see cref="ArgumentParsingService"/> fills parameters from profile defaults
/// during binding. These rules are shared by every host, so they use the Core
/// <see cref="DefaultArgumentParsingProvider"/>.
/// </summary>
public class ArgumentParsingServiceProfileDefaultTests
{
    private static ParameterMetadata Meta(string processorProperty, string invocationName, Type type) =>
        new(processorProperty, invocationName, type, "test", IsRequired: false, [], AllowedValues: []);

    [Fact]
    public void BindArguments_ProfileDefaultDoesNotExcludeTheExplicitParameterSet()
    {
        var profile = new Profile { SubscriptionId = "profile-subscription" };
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetProfileKeysForDefault()).Returns(["SubscriptionId"]);
        profileService.Setup(service => service.GetProfileValueByKey(profile, "SubscriptionId"))
            .Returns(profile.SubscriptionId);
        var metadata = new CommandMetadata("Test", "Test-Command", [], typeof(TestProcessor).FullName!,
            typeof(TestProcessor).Assembly.GetName().Name!, typeof(TestProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = Meta("Name", "Name", typeof(string)),
                ["SubscriptionId"] = Meta("SubscriptionId", "SubscriptionId", typeof(string)),
            },
            new Dictionary<string, string>(),
            new Dictionary<string, ParameterSetMetadata>
            {
                ["ByName"] = new("ByName", Parameters: new Dictionary<string, ParameterReference>
                {
                    ["Name"] = new("Name", IsRequired: true),
                }),
            });
        var processor = new TestProcessor();

        new ArgumentParsingService(new DefaultArgumentParsingProvider(), profileService.Object)
            .BindArgumentsToProcessor(processor, new Dictionary<string, object> { ["Name"] = "example" },
                metadata, profile);

        Assert.Equal("example", processor.Name);
        Assert.Equal(profile.SubscriptionId, processor.SubscriptionId);
    }

    private sealed class TestProcessor : IProcessor
    {
        public string Name { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;

        public Task ProcessAsync() => Task.CompletedTask;
        public IProcessor Init(ProcessorContext context) => this;
    }
}
