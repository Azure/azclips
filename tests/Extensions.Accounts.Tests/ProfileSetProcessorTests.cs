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
    public void Dispose() => AzclipsServiceProvider.Dispose();

    [Fact]
    public async Task ProcessAsync_ProfileDoesNotExist_ThrowsProfileNotFound()
    {
        // Strict: setting the active profile (or any other call) after the failed lookup throws MockException.
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        profiles.Setup(p => p.GetProfileAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(profiles.Object));
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        var processor = new ProfileSetProcessor { Name = "missing" };
        typeof(ProfileProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, Mock.Of<IEventPublisher>());

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(processor.ProcessAsync);

        Assert.Equal("Profile 'missing' does not exist.", ex.Message);
    }
}