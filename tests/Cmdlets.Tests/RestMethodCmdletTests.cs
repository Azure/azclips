// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class RestMethodCmdletTests
{
    [Fact]
    public void JsonObject_ExposesNativeProperties_InPowerShellPipeline()
    {
        using var document = JsonDocument.Parse("""{"value":[{"name":"rg1","enabled":true}],"empty":null}""");
        var values = Invoke(document.RootElement, false, "$payload = @($input)[0]; $payload.value[0].name; $payload.value[0].enabled", out _);

        Assert.Equal("rg1", values[0].BaseObject);
        Assert.Equal(true, values[1].BaseObject);
    }

    [Theory]
    [InlineData("42", typeof(long), "42")]
    [InlineData("1.25", typeof(decimal), "1.25")]
    [InlineData("1e100", typeof(double), "1E+100")]
    [InlineData("true", typeof(bool), "True")]
    [InlineData("false", typeof(bool), "False")]
    [InlineData("\"hello\"", typeof(string), "hello")]
    public void JsonScalars_AreNativeValues(string body, Type type, string expected)
    {
        using var document = JsonDocument.Parse(body);
        var value = Assert.Single(Invoke(document.RootElement, false, null, out _)).BaseObject;

        Assert.IsType(type, value);
        Assert.Equal(expected, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void JsonArray_EnumeratesObjects_AndPreservesNestedArrays()
    {
        using var document = JsonDocument.Parse("""[{"items":[1,2]},{"items":[]} ]""");
        var values = Invoke(document.RootElement, false, null, out _);

        Assert.Equal(2, values.Length);
        Assert.Equal(new object?[] { 1L, 2L }, Assert.IsType<object?[]>(values[0].Properties["items"].Value));
        Assert.Empty(Assert.IsType<object?[]>(values[1].Properties["items"].Value));
    }

    [Fact]
    public void JsonNull_IsPowerShellNull()
    {
        using var document = JsonDocument.Parse("null");
        var values = Invoke(document.RootElement, false, "$null -eq @($input)[0]", out _);

        Assert.True(Assert.IsType<bool>(Assert.Single(values).BaseObject));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullResponse_BindsAndReachesEngine(bool fullResponse)
    {
        using var document = JsonDocument.Parse("""{"name":"rg1"}""");
        var output = new RestResponse
        {
            StatusCode = 200,
            Content = document.RootElement.GetRawText(),
            Json = document.RootElement.Clone()
        };
        var result = Assert.Single(Invoke(output, fullResponse, null, out var invocation));

        Assert.Equal(fullResponse, invocation.Parameters["FullResponse"]);
        if (fullResponse)
            Assert.Same(output, result.BaseObject);
        else
            Assert.Equal("rg1", result.Properties["name"].Value);
    }

    [Fact]
    public void RawContent_IsNotConverted()
    {
        Assert.Equal("plain text", Assert.Single(Invoke("plain text", false, null, out _)).BaseObject);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyBody_IsSuppressedUnlessFullResponse(bool fullResponse)
    {
        var response = new RestResponse { StatusCode = 204 };
        var values = Invoke(response, fullResponse, null, out _);

        if (fullResponse)
            Assert.Same(response, Assert.Single(values).BaseObject);
        else
            Assert.Empty(values);
    }

    private static PSObject[] Invoke(object output, bool fullResponse, string? pipelineScript, out CommandInvocation invocation)
    {
        var response = output as RestResponse ?? new RestResponse
        {
            StatusCode = 200,
            Content = output is JsonElement json ? json.GetRawText() : Assert.IsType<string>(output),
            Json = output is JsonElement element ? element.Clone() : null
        };
        AzclipsServiceProvider.Dispose();
        try
        {
            CommandInvocation? captured = null;
            var engine = new Mock<IEngine>();
            engine.Setup(value => value.OpenExecutionSession(
                    It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
                .Returns((CommandInvocation command, OutputStreams streams, CancellationToken token) =>
                {
                    captured = command;
                    var session = new Mock<ISession>();
                    session.Setup(value => value.Run()).Callback(() =>
                    {
                        var projection = new RestProcessor_Orchestrator { FullResponse = (bool)command.Parameters["FullResponse"] };
                        var projected = projection.GetOutputValue(response);
                        if (projected is not null)
                            streams.WriteOutput(projected, token).GetAwaiter().GetResult();
                    });
                    return session.Object;
                });
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(Mock.Of<ITelemetryService>());
            });
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "test-client"));

            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("Invoke-AzRestMethod", typeof(InvokeAzclipsRestMethodCmdlet), null));
            using var ps = PowerShell.Create(state);
            ps.AddCommand("Invoke-AzRestMethod").AddParameter("Uri", "/test").AddParameter("FullResponse", fullResponse);
            if (pipelineScript is not null)
                ps.AddScript(pipelineScript);
            var result = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            Assert.NotNull(captured);
            invocation = captured;
            return result.ToArray();
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }
}
