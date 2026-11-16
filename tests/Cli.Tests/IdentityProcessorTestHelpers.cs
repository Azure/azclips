// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Cli.Tests;

/// <summary>
/// Shared offline setup for Managed Identity orchestrator unit tests.
/// </summary>
internal static class IdentityProcessorTestHelpers
{
    internal const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    internal const string MissingIdentityAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    /// <summary>Creates an initialized processor whose pipeline fails the test if any request is sent.</summary>
    internal static T CreateProcessor<T>(Profile? profile = null) where T : IProcessor, new()
        => CreateProcessor<T>(NoRequestPipeline(), profile);

    internal static T CreateProcessor<T>(IRuntimeHttpPipeline pipeline, Profile? profile = null) where T : IProcessor, new()
    {
        profile ??= new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new T();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            new FakeOutputHandler(),
            Profile: profile));
        return processor;
    }

    /// <summary>
    /// Any request fails the test with a different exception type, so a passing message assert
    /// also proves the validation ran before the network.
    /// </summary>
    internal static FakeHttpPipeline NoRequestPipeline() =>
        new((_, _) => throw new InvalidOperationException("Unexpected request."));

    /// <summary>Answers GETs with <paramref name="getBody"/>; any write (PUT/PATCH/DELETE) fails the test.</summary>
    internal static FakeHttpPipeline GetOnlyPipeline(string getBody) =>
        new((request, _) => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(getBody, Encoding.UTF8, "application/json") }
            : throw new InvalidOperationException("Unexpected request."));
}
