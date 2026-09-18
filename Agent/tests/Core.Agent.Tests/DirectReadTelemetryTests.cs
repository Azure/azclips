// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Agent.Tools.Help;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public sealed class DirectReadTelemetryTests
{
    [Fact]
    public async Task ToolActivityKeepsCliParentage_WithoutLoggingRegenerationPayloads()
    {
        using var fixture = new HelpCatalogFixture();
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "CLI",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = AgentTracing.Source.StartActivity("direct-read-test-parent");
        Assert.NotNull(parent);
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromResult("{\"error\":\"shell_out_nonzero_exit\",\"stderr\":\"PRIVATE RESPONSE\"}"));
        var tools = new HelpTools(provider);

        var result = await Task.Run(async () =>
            await tools.GetCommand.InvokeAsync(new AIFunctionArguments { ["name"] = "PRIVATE QUERY" }));

        HelpCatalogProviderTests.AssertError(result!.ToString()!, "help_catalog_regeneration_failed");
        var span = Assert.Single(stopped, activity => activity.OperationName == "tool.get_command");
        Assert.Equal("CLI", span.Source.Name);
        Assert.Equal(parent.TraceId, span.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Same(parent, Activity.Current);
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value?.ToString()?.Contains("PRIVATE", StringComparison.Ordinal) == true);
        Assert.Empty(span.Events);
    }
}
