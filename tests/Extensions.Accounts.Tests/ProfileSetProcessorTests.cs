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
public class ProfileSetProcessorTests : IDisposable
{
    // Strict: setting the active profile (or any other call) after the failed lookup throws MockException.
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly IEventPublisher _mockPublisher = Mock.Of<IEventPublisher>();

    public void Dispose() => AzclipsServiceProvider.Dispose();

    [Fact]
    public async Task ProcessAsync_ProfileDoesNotExist_ThrowsProfileNotFound()
    {
        _mockProfiles.Setup(p => p.GetProfileAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        ProfileSetProcessor processor = CreateProcessor(name: "missing");

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Profile 'missing' does not exist.", Assert.IsType<AzclipsErrorException>(exception).Message);
    }

    private ProfileSetProcessor CreateProcessor(string name)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(_mockProfiles.Object));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        var processor = new ProfileSetProcessor { Name = name };
        typeof(ProfileProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, _mockPublisher);
        return processor;
    }
}
