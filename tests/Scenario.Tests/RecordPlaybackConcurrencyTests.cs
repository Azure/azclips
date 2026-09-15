using System.Net;
using System.Text;
using Core.Abstractions.Runtime;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Orchestrators fan requests out in parallel (vm create fires vnet + nsg at once), and one
/// interceptor instance sees all of them. These tests drive the step from many threads to hold
/// the recorder to that: no lost entries in record mode, each entry consumed exactly once in
/// playback.
/// </summary>
public class RecordPlaybackConcurrencyTests
{
    private const int Parallelism = 32;

    private sealed class EchoNext : ISendAsync
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"uri\":\"{request.RequestUri}\"}}", Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Record_KeepsEveryEntry_UnderParallelRequests()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        var next = new EchoNext();

        await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(i => Task.Run(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://management.azure.com/things/{i}");
            using var response = await recorder.SendAsync(request, null!, next);
        })));

        Assert.Equal(Parallelism, record.Entries.Count);
        Assert.Equal(Parallelism, recorder.Sent.Count);
        Assert.Equal(
            Enumerable.Range(0, Parallelism).Select(i => $"https://management.azure.com/things/{i}").Order(),
            record.Entries.Select(e => e.Request.Uri).Order());
    }

    [Fact]
    public async Task Playback_ConsumesEachEntryOnce_UnderParallelRequests()
    {
        // Identical URIs with distinct bodies: correct consumption serves every body exactly once.
        var record = new SessionRecord();
        for (int i = 0; i < Parallelism; i++)
        {
            record.Entries.Add(new SessionRecordEntry
            {
                Request = new RecordedRequest { Method = "GET", Uri = "https://management.azure.com/poll" },
                Response = new RecordedResponse { StatusCode = 200, Body = $"{{\"n\":{i}}}" },
            });
        }
        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record);

        var bodies = await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(_ => Task.Run(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/poll");
            using var response = await playback.SendAsync(request, null!, null!);
            return await response.Content.ReadAsStringAsync();
        })));

        Assert.Equal(
            Enumerable.Range(0, Parallelism).Select(i => $"{{\"n\":{i}}}").Order(),
            bodies.Order());

        // The record is exhausted, so one more request must fail rather than replay a duplicate.
        using var extra = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/poll");
        await Assert.ThrowsAsync<InvalidOperationException>(() => playback.SendAsync(extra, null!, null!));
    }
}
