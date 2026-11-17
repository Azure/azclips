// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Web;
using Core.Abstractions.Runtime;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Moq helpers for a Graph <see cref="IRuntimeHttpPipeline"/>. Create the mock with
/// <see cref="MockBehavior.Strict"/> so any request a test did not set up throws <see cref="MockException"/>.
/// </summary>
internal static class GraphPipelineMockHelpers
{
    /// <summary>Answers every request whose path ends with <paramref name="pathSuffix"/> with a JSON body.</summary>
    public static void RespondTo(
        this Mock<IRuntimeHttpPipeline> mockPipeline,
        string pathSuffix,
        string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        mockPipeline.RespondTo(r => r.RequestUri!.AbsolutePath.EndsWith(pathSuffix, StringComparison.Ordinal), body, status);

    /// <summary>Answers every request matching <paramref name="match"/> with a JSON body.</summary>
    public static void RespondTo(
        this Mock<IRuntimeHttpPipeline> mockPipeline,
        Func<HttpRequestMessage, bool> match,
        string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        mockPipeline
            .Setup(p => p.SendAsync(It.Is<HttpRequestMessage>(r => match(r)), It.IsAny<IEventPublisher>()))
            .ReturnsAsync((HttpRequestMessage request, IEventPublisher _) => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });

    /// <summary>Answers every request whose full URL (including query) equals <paramref name="absoluteUri"/> with a JSON body.</summary>
    public static void RespondToUrl(
        this Mock<IRuntimeHttpPipeline> mockPipeline,
        string absoluteUri,
        string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        mockPipeline.RespondTo(r => r.RequestUri!.AbsoluteUri == absoluteUri, body, status);

    /// <summary>The requests sent through the pipeline, in order.</summary>
    public static IReadOnlyList<HttpRequestMessage> SentRequests(this Mock<IRuntimeHttpPipeline> mockPipeline) =>
        mockPipeline.Invocations
            .Where(i => i.Method.Name == nameof(IRuntimeHttpPipeline.SendAsync))
            .Select(i => (HttpRequestMessage)i.Arguments[0])
            .ToList();

    /// <summary>Each sent request as "METHOD url-without-query", in order.</summary>
    public static IEnumerable<string> RequestLines(this Mock<IRuntimeHttpPipeline> mockPipeline) =>
        mockPipeline.SentRequests().Select(r => $"{r.Method} {r.RequestUri!.GetLeftPart(UriPartial.Path)}");

    /// <summary>Each sent request as "METHOD full-url" (including query), in order.</summary>
    public static IEnumerable<string> FullRequestLines(this Mock<IRuntimeHttpPipeline> mockPipeline) =>
        mockPipeline.SentRequests().Select(r => $"{r.Method} {r.RequestUri!.AbsoluteUri}");

    /// <summary>The values of header <paramref name="name"/> on the request at <paramref name="index"/>; empty when absent.</summary>
    public static IEnumerable<string> HeaderValues(this Mock<IRuntimeHttpPipeline> mockPipeline, int index, string name) =>
        mockPipeline.SentRequests()[index].Headers.TryGetValues(name, out IEnumerable<string>? values) ? values : [];

    /// <summary>The decoded value of query parameter <paramref name="name"/> on the request at <paramref name="index"/>.</summary>
    public static string? QueryValue(this Mock<IRuntimeHttpPipeline> mockPipeline, int index, string name) =>
        HttpUtility.ParseQueryString(mockPipeline.SentRequests()[index].RequestUri!.Query)[name];
}
