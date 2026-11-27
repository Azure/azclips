using System.Net;
using Core.Abstractions.Runtime;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class IdentityBenchmarkFixtureTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public async Task BenchmarkStub_IdentityDelete_ReturnsEmptyBody_OtherRequestsUnchanged()
    {
        var handler = new StubHttpTerminateHandler();
        var identityUri = $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/bench-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/bench-identity?api-version=2025-05-31-preview";

        // Matches the live recording: identity DELETE answers 200 with no body.
        using var delete = new HttpRequestMessage(HttpMethod.Delete, identityUri);
        using var deleteResponse = await handler.SendAsync(delete, Mock.Of<IEventPublisher>(), null!);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(string.Empty, await deleteResponse.Content.ReadAsStringAsync());

        // Only identity DELETE is special-cased; reads of the same path and other resource deletes keep the default body.
        using var get = new HttpRequestMessage(HttpMethod.Get, identityUri);
        using var getResponse = await handler.SendAsync(get, Mock.Of<IEventPublisher>(), null!);
        Assert.Equal("{}", await getResponse.Content.ReadAsStringAsync());

        using var otherDelete = new HttpRequestMessage(HttpMethod.Delete, $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/bench-rg?api-version=2021-04-01");
        using var otherDeleteResponse = await handler.SendAsync(otherDelete, Mock.Of<IEventPublisher>(), null!);
        Assert.Equal("{}", await otherDeleteResponse.Content.ReadAsStringAsync());
    }
}
