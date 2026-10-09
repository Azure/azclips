// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using static Cli.Tests.ResourceGroupDeploymentCliTests;

namespace Cli.Tests;

/// <summary>Shared fake-ARM helpers for the per-operation deployment processor test suites.</summary>
internal static class DeploymentProcessorTestKit
{
    internal const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    internal const string DeploymentUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/test-rg/providers/Microsoft.Resources/deployments/dep";
    internal const string SubscriptionDeploymentsUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments";
    internal const string SucceededSubscriptionDeployment =
        """{"id":"/subscriptions/x/providers/Microsoft.Resources/deployments/dep","name":"dep","properties":{"provisioningState":"Succeeded"}}""";

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

    /// <summary>Asserts a subscription-scope command is wired to <typeparamref name="TProcessor"/> and exposes no resource group parameter.</summary>
    internal static void AssertSubscriptionCommand<TProcessor>(string command, ClientType client)
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata(command, client);

        Assert.Equal(typeof(TProcessor), metadata.ProcessorType);
        Assert.DoesNotContain("ResourceGroupName", metadata.Parameters.Keys);
    }
}
