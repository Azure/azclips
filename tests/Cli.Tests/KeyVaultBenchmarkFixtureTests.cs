using System.Text.Json;
using Core.Abstractions.Runtime;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

public class KeyVaultBenchmarkFixtureTests
{
    [Theory]
    [InlineData("secrets")]
    [InlineData("certificates")]
    public async Task ListsHaveTwoDistinctPagesAndTerminate(string resource)
    {
        var handler = new StubHttpTerminateHandler();
        var publisher = Mock.Of<IEventPublisher>();
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, $"https://bench-vault.vault.azure.net/{resource}?api-version=2025-07-01&maxresults=1");
        using var firstResponse = await handler.SendAsync(firstRequest, publisher, null!);
        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        string firstId = first.RootElement.GetProperty("value")[0].GetProperty("id").GetString()!;
        var next = new Uri(first.RootElement.GetProperty("nextLink").GetString()!);
        Assert.Equal(firstRequest.RequestUri!.Host, next.Host);

        using var nextRequest = new HttpRequestMessage(HttpMethod.Get, next);
        using var nextResponse = await handler.SendAsync(nextRequest, publisher, null!);
        using var second = JsonDocument.Parse(await nextResponse.Content.ReadAsStringAsync());
        Assert.Single(second.RootElement.GetProperty("value").EnumerateArray());
        Assert.NotEqual(firstId, second.RootElement.GetProperty("value")[0].GetProperty("id").GetString());
        Assert.False(second.RootElement.TryGetProperty("nextLink", out _));
        Assert.Same(nextRequest, nextResponse.RequestMessage);
    }

    [Fact]
    public async Task CertificateCreationHasACompletedPendingResponse()
    {
        var handler = new StubHttpTerminateHandler();
        foreach (var (path, expected) in new[] { ("create", "inProgress"), ("pending", "completed") })
        {
            using var request = new HttpRequestMessage(path == "create" ? HttpMethod.Post : HttpMethod.Get,
                $"https://bench-vault.vault.azure.net/certificates/bench-certificate/{path}?api-version=2025-07-01");
            using var response = await handler.SendAsync(request, Mock.Of<IEventPublisher>(), null!);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected, body.RootElement.GetProperty("status").GetString());
            Assert.Equal(TimeSpan.Zero, response.Headers.RetryAfter!.Delta);
        }
    }
}