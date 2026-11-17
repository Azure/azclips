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
    public async Task ToolActivityKeepsItsParent_WithoutLoggingRegenerationPayloads()
    {
        using var fixture = new HelpCatalogFixture();
        using var trace = new AgentTraceCapture();
        using var parentScope = trace.Telemetry.StartIndependentRootActivity("direct-read-test-parent");
        var parent = parentScope?.Activity;
        Assert.NotNull(parent);
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromResult("{\"error\":\"shell_out_nonzero_exit\",\"stderr\":\"PRIVATE RESPONSE\"}"));
        var tools = new HelpTools(provider, trace.StartToolActivity);

        var result = await Task.Run(async () =>
            await tools.GetCommand.InvokeAsync(new AIFunctionArguments { ["name"] = "PRIVATE QUERY" }));

        HelpCatalogProviderTests.AssertError(result!.ToString()!, "help_catalog_regeneration_failed");
        var span = Assert.Single(trace.Stopped, activity => activity.OperationName == "tool.get_command");
        AgentTraceCapture.AssertParent(span, parent);
        Assert.Same(parent, Activity.Current);
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value?.ToString()?.Contains("PRIVATE", StringComparison.Ordinal) == true);
        Assert.Empty(span.Events);
    }
}
