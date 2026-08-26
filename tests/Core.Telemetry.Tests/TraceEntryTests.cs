// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry.Tests;

public class TraceEntryTests
{
    [Fact]
    public void TryGetServiceName_ReturnsName_WhenResourceCarriesIt()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": "CLI" }""") };

        Assert.Equal("CLI", entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenResourceIsNotAnObject()
    {
        var entry = new TraceEntry { Resource = ParseJson("[]") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenPropertyIsMissing()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.version": "1.0" }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenNameIsEmpty()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": "" }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    [Fact]
    public void TryGetServiceName_ReturnsNull_WhenNameIsNotAString()
    {
        var entry = new TraceEntry { Resource = ParseJson("""{ "service.name": 42 }""") };

        Assert.Null(entry.TryGetServiceName());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
