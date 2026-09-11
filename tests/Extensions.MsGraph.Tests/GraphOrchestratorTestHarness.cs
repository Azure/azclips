// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Reflection-based helpers for injecting fake runtime state (pipeline, publisher, JSON options,
/// output delegate) into orchestrators (or any <see cref="GraphProcessorBase"/> subclass) without
/// having to write a Testable subclass per orchestrator or route through <c>Init</c> (which would
/// install a real auth override we don't need offline).
/// </summary>
internal static class GraphOrchestratorTestHarness
{
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo HttpPipelineField =
        typeof(GraphProcessorBase).GetField("HttpPipeline", Instance)!;

    private static readonly FieldInfo PublisherField =
        typeof(GraphProcessorBase).GetField("Publisher", Instance)!;

    private static readonly FieldInfo JsonOptionsField =
        typeof(GraphProcessorBase).GetField("JsonOptions", Instance)!;

    private static readonly FieldInfo WriteOutputField =
        typeof(GraphProcessorBase).GetField("WriteOutput", Instance)!;

    private static readonly MethodInfo SendAsyncMethod =
        typeof(GraphProcessorBase).GetMethod("SendAsync", Instance)!;

    /// <summary>
    /// Wires up the fake runtime context on the given orchestrator. Returns the orchestrator so
    /// callers can chain.
    /// </summary>
    public static T WithFakeContext<T>(
        this T processor,
        IRuntimeHttpPipeline pipeline,
        IEventPublisher publisher,
        JsonSerializerOptions? jsonOptions = null)
        where T : GraphProcessorBase
    {
        HttpPipelineField.SetValue(processor, pipeline);
        PublisherField.SetValue(processor, publisher);
        JsonOptionsField.SetValue(processor, jsonOptions ?? DefaultJsonOptions());
        WriteOutputField.SetValue(processor, (OutputStreamDelegate)((_, _) => Task.CompletedTask));
        return processor;
    }

    /// <summary>
    /// Invokes the base <c>SendAsync</c> path directly (bypassing <c>ProcessAsync</c>, which writes
    /// to output). Tests want to observe the outgoing HTTP requests captured by the fake pipeline;
    /// they don't need output serialization.
    /// </summary>
    public static Task<HttpResponseMessage> InvokeSendAsync(this GraphProcessorBase processor) =>
        (Task<HttpResponseMessage>)SendAsyncMethod.Invoke(processor, null)!;

    private static JsonSerializerOptions DefaultJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
