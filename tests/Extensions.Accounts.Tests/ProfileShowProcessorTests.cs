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
    // Strict: a profile lookup the test did not set up throws MockException instead of the expected error.
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly IEventPublisher _mockPublisher = Mock.Of<IEventPublisher>();

    public void Dispose() => AzclipsServiceProvider.Dispose();

    [Fact]
    public async Task ProcessAsync_NameAndActive_ThrowsInvalidInput()
    {
        ProfileShowProcessor processor = CreateProcessor(name: "prod", active: true);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Specify either a profile name or --active, not both.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public async Task ProcessAsync_NoActiveProfile_ThrowsNotSignedIn(string name, bool active)
    {
        _mockProfiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        ProfileShowProcessor processor = CreateProcessor(name: name, active: active);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("No active profile is set.", Assert.IsType<AzclipsErrorException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_ProfileDoesNotExist_ThrowsProfileNotFound()
    {
        _mockProfiles.Setup(p => p.GetProfileAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        ProfileShowProcessor processor = CreateProcessor(name: "missing", active: false);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Profile 'missing' does not exist.", Assert.IsType<AzclipsErrorException>(exception).Message);
    }

    private ProfileShowProcessor CreateProcessor(string name, bool active)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(_mockProfiles.Object));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        var processor = new ProfileShowProcessor { Name = name, Active = active };
        typeof(ProfileProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, _mockPublisher);
        return processor;
    }
}
