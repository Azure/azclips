// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Core.Telemetry.Tests;

/// <summary>
/// Regression coverage for the mechanism <see cref="Core.Engine.Engine"/>'s
/// <c>ResolveUploadRegionAsync</c> and <see cref="TelemetryRoutingResolver"/> rely on to avoid
/// exporting the token-acquisition/ARM-lookup spans before the data boundary they discover is
/// known: wrapping the resolution in <see cref="SuppressInstrumentationScope"/>.
/// </summary>
/// <remarks>
/// Uses a <see cref="TracerProvider"/> built the same way production code builds one
/// (<c>Sdk.CreateTracerProviderBuilder().AddSource(...).Build()</c>) rather than a manually
/// attached <see cref="ActivityListener"/> — a raw listener's own <c>Sample</c> callback does
/// not consult <see cref="Sdk.SuppressInstrumentation"/> at all, so it would not actually
/// exercise the mechanism being tested here (unlike the TracerProvider-driven sampling path,
/// which is what <c>AddHttpClientInstrumentation()</c> and <c>TelemetryService.StartActivity</c>
/// both go through in production).
/// </remarks>
public class DataBoundaryResolutionSuppressesTelemetryTests
{
    [Fact]
    public void StartActivity_ReturnsNull_WhileSuppressInstrumentationScopeIsActive()
    {
        var sourceName = nameof(StartActivity_ReturnsNull_WhileSuppressInstrumentationScopeIsActive);
        using var source = new ActivitySource(sourceName);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .Build()!;

        using (var before = source.StartActivity("before-suppression"))
        {
            Assert.NotNull(before);
        }

        using (SuppressInstrumentationScope.Begin())
        {
            using var suppressed = source.StartActivity("during-suppression");
            Assert.Null(suppressed);
        }

        using (var after = source.StartActivity("after-suppression"))
        {
            Assert.NotNull(after);
        }
    }
}
