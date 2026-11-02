// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Moq;

namespace Cmdlets.Tests;

public class PSUserAgentHandlerTests
{
    [Fact]
    public async Task Handler_UsesAssemblyVersionAndPublicPreviewStage()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/");
        var next = new Mock<ISendAsync>();
        next.Setup(sender => sender.SendAsync(request, It.IsAny<IEventPublisher>()))
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        using var response = await new PSUserAgentHandler().SendAsync(
            request, Mock.Of<IEventPublisher>(), next.Object);

        var version = AssemblyVersionProvider.GetVersion(typeof(PSUserAgentHandler).Assembly, "0.0.0");
        Assert.Equal(
            $"AzurePowershell/{version} azclips/public-preview",
            request.Headers.UserAgent.ToString());
    }
}
