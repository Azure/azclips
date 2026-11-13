// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Extensions.Accounts.Tests;

[Collection("Azclips Service Provider Tests")]
public class ProfileShowProcessorTests : IDisposable
{
    public void Dispose() => AzclipsServiceProvider.Dispose();

    private static ProfileShowProcessor CreateProcessor(string name, bool active)
    {
        var processor = new ProfileShowProcessor { Name = name, Active = active };
        typeof(ProfileProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, Mock.Of<IEventPublisher>());
        return processor;
    }

    // Strict: a profile lookup the test did not set up throws MockException instead of the expected error.
    private static void InitDi(Mock<IProfileService> profiles)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(profiles.Object));
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
    }

    [Fact]
    public async Task ProcessAsync_NameAndActive_ThrowsExactMessage()
    {
        InitDi(new Mock<IProfileService>(MockBehavior.Strict));

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => CreateProcessor("prod", active: true).ProcessAsync());

        Assert.Equal("Specify either a profile name or --active, not both.", ex.Message);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public async Task ProcessAsync_NoActiveProfile_ThrowsExactMessage(string name, bool active)
    {
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        profiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        InitDi(profiles);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() => CreateProcessor(name, active).ProcessAsync());

        Assert.Equal("No active profile is set.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_ProfileDoesNotExist_ThrowsExactMessage()
    {
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        profiles.Setup(p => p.GetProfileAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        InitDi(profiles);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() => CreateProcessor("missing", active: false).ProcessAsync());

        Assert.Equal("Profile 'missing' does not exist.", ex.Message);
    }
}