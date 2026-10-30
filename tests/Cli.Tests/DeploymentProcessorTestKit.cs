// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using static Cli.Tests.ResourceGroupDeploymentCliTests;

namespace Cli.Tests;

/// <summary>Shared fake-ARM helpers for the per-operation deployment processor test suites.</summary>
internal static class DeploymentProcessorTestKit
{
    internal const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    internal const string DeploymentUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/test-rg/providers/Microsoft.Resources/deployments/dep";

    internal sealed record Sent(HttpMethod Method, string Uri, string? Body);

    internal static HttpResponseMessage Status(HttpStatusCode code, string? body = null) =>
        new(code)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"),
        };

    internal static (FakeHttpPipeline Pipeline, FakeOutputHandler Output, List<Sent> Sent) Fake(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var sent = new List<Sent>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            // ponytail: the orchestrators pre-download a TemplateUri template; answer the tests' fixed URI
            // with an empty template and keep it out of `sent`, which only tracks ARM calls.
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri == "https://example.com/t.json")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"parameters":{}}""") };
            }

            sent.Add(new Sent(request.Method, request.RequestUri!.AbsoluteUri, request.Content?.ReadAsStringAsync().Result));
            return responder(request);
        });
        return (pipeline, new FakeOutputHandler(), sent);
    }

    internal static T Configure<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output, Action<T> configure)
        where T : IProcessor
    {
        CreateProcessor(processor, pipeline, output);
        configure(processor);
        return processor;
    }
}
