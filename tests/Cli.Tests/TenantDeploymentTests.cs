// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

public class TenantDeploymentTests
{
    private static void Init(
        IProcessor processor,
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        IEventPublisher? publisher = null)
    {
        pipeline.PollingInterval = 0;
        Profile profile = new() { TenantId = "test-tenant", SubscriptionId = "test-subscription" };
        processor.Init(new ProcessorContext(
            pipeline,
            publisher ?? new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string RequestUri(IProcessor processor) =>
        (string)processor.GetType()
            .GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(processor, null)!;

    [Theory]
    [InlineData(typeof(CancelAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars/cancel?")]
    [InlineData(typeof(DeleteAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars?")]
    [InlineData(typeof(ExportTemplateAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars/exportTemplate?")]
    [InlineData(typeof(GetAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars?")]
    public void DeploymentName_IsEscapedAsSinglePathSegment(Type processorType, string expected)
    {
        var processor = (IProcessor)Activator.CreateInstance(processorType)!;
        processorType.GetProperty("DeploymentName")!.SetValue(processor, "name/with?reserved#chars");

        Assert.Contains(expected, RequestUri(processor));
    }

    [Fact]
    public void List_Filter_IsEncodedAsSingleQueryValue()
    {
        var processor = new ListAtTenantScopeDeploymentsProcessor
        {
            _filter = "provisioningState eq 'Succeeded' & name eq 'a/b'",
        };

        Assert.Contains(
            "$filter=provisioningState%20eq%20%27Succeeded%27%20%26%20name%20eq%20%27a%2Fb%27",
            RequestUri(processor));
    }

    [Fact]
    public void ExtensionIndex_ExposesRequestedTenantCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex();

        Assert.Equal(
            [
                "deployment tenant cancel",
                "deployment tenant create",
                "deployment tenant delete",
                "deployment tenant export",
                "deployment tenant list",
                "deployment tenant show",
                "deployment tenant validate",
                "deployment tenant wait",
                "deployment tenant what-if",
            ],
            index.GetCommandKeys(ClientType.CLI)
                .Where(key => key.StartsWith("deployment tenant ", StringComparison.Ordinal))
                .Order()
                .ToArray());

        CommandMetadata createPs =
            index.GetCommandMetadata("New-AzTenantDeployment", ClientType.PowerShell);
        Assert.Equal("ParametersFile", createPs.Parameters["TemplateParameterFile"].Name);
        Assert.Equal("ParametersUri", createPs.Parameters["TemplateParameterUri"].Name);
        Assert.Equal("ParametersObject", createPs.Parameters["TemplateParameterObject"].Name);
        Assert.Equal("Tags", createPs.Parameters["Tag"].Name);

        foreach (string command in new[]
        {
            "deployment tenant create",
            "deployment tenant delete",
            "deployment tenant validate",
            "deployment tenant what-if",
        })
        {
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
            Assert.DoesNotContain("no-wait", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-prompt", metadata.Parameters.Keys);
        }

        CommandMetadata whatIfMetadata =
            index.GetCommandMetadata("deployment tenant what-if", ClientType.CLI);
        Assert.DoesNotContain("no-pretty-print", whatIfMetadata.Parameters.Keys);
        Assert.DoesNotContain("no-color", whatIfMetadata.Parameters.Keys);

        Assert.Equal(
            [
                "Get-AzTenantDeployment_Get",
                "Get-AzTenantDeployment_List",
                "New-AzTenantDeployment",
                "Remove-AzTenantDeployment",
                "Stop-AzTenantDeployment",
                "Test-AzTenantDeployment",
            ],
            index.GetCommandKeys(ClientType.PowerShell)
                .Where(key => key.Contains("TenantDeployment", StringComparison.Ordinal))
                .Order()
                .ToArray());
    }

    [Fact]
    public async Task Create_ValidatesThenSendsScopedTenantBody()
    {
        List<(HttpMethod Method, Uri Uri, string? Body)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            return requests.Count == 1
                ? Json("""{ "name": "tenant-test" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var output = new FakeOutputHandler();
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
                ["contentVersion"] = "1.0.0.0",
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/deployments/tenant-test?api-version=2025-04-01",
            requests[1].Uri.ToString());

        JsonElement body = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
        Assert.Equal("Provider", body.GetProperty("properties").GetProperty("validationLevel").GetString());
    }

    [Fact]
    public async Task Create_DerivesDeploymentNameFromTemplateUriWhenNameOmitted()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count == 1
                ? Json("""{ "name": "azuredeploy" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Location = "eastus",
            TemplateUri = "https://example.com/templates/azuredeploy.json",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal("azuredeploy", processor.Name);
        Assert.Contains("/deployments/azuredeploy?", requests[1].Uri.ToString());
    }

    [Fact]
    public async Task Create_DerivesDeploymentNameFromTemplateSpecWhenNameOmitted()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count == 1
                ? Json("""{ "name": "my-spec" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Location = "eastus",
            TemplateSpecId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/templateSpecs/my-spec/versions/v1",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal("my-spec", processor.Name);
        Assert.Contains("/deployments/my-spec?", requests[1].Uri.ToString());
    }

    [Fact]
    public async Task Create_FallsBackToLiteralDeploymentNameWhenNoTemplateSourceGivesOne()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count == 1
                ? Json("""{ "name": "deployment1" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
                ["contentVersion"] = "1.0.0.0",
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal("deployment1", processor.Name);
        Assert.Contains("/deployments/deployment1?", requests[1].Uri.ToString());
    }

    [Theory]
    [InlineData("RequestContent", "RequestContent")]
    [InlineData("ResponseContent", "ResponseContent")]
    [InlineData("All", "RequestContent,ResponseContent")]
    [InlineData("None", null)]
    public async Task Create_NormalizesPowerShellDebugLevel(string input, string? expected)
    {
        List<string?> bodies = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return bodies.Count == 1
                ? Json("""{ "name": "tenant-test" }""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
            DeploymentDebugLogLevel = input,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement properties = JsonDocument.Parse(bodies[1]!).RootElement.GetProperty("properties");
        if (expected is null)
        {
            Assert.False(properties.TryGetProperty("debugSetting", out _));
        }
        else
        {
            Assert.Equal(
                expected,
                properties.GetProperty("debugSetting").GetProperty("detailLevel").GetString());
        }
    }

    [Fact]
    public async Task Create_PrefersAsyncOperationThenReadsFinalDeployment()
    {
        List<Uri> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count switch
            {
                1 => Json("""{ "name": "tenant-test" }"""),
                2 => WithHeaders(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    ("Azure-AsyncOperation", "https://management.azure.com/providers/Microsoft.Resources/operations/create-async"),
                    ("Location", "https://management.azure.com/providers/Microsoft.Resources/operations/create-location")),
                3 => Json("""{ "status": "Succeeded" }"""),
                4 => Json(
                    """{ "name": "tenant-test", "properties": { "provisioningState": "Succeeded" } }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(4, requests.Count);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/create-async",
            requests[2].ToString());
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/create-location",
            requests[3].ToString());
    }

    [Fact]
    public async Task List_UnwrapsTenantDeploymentPage()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""
            {
              "value": [
                { "name": "one", "properties": { "provisioningState": "Succeeded" } },
                { "name": "two", "properties": { "provisioningState": "Failed" } }
              ]
            }
            """));
        var output = new FakeOutputHandler();
        var processor = new ListAtTenantScopeDeploymentsProcessor();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var values = Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["one", "two"], values.Select(value => value.Name));
    }

    [Fact]
    public async Task Stop_DelegatesDirectlyToCancelWithoutPrecheck()
    {
        List<HttpMethod> methods = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            methods.Add(request.Method);
            return Json("{}");
        });
        var output = new FakeOutputHandler();
        var processor = new StopTenantDeploymentProcessor_Orchestrator { Name = "done" };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        // Matches `az deployment tenant cancel` / `Stop-AzTenantDeployment`, which forward
        // straight to ARM's cancel operation instead of first reading the deployment to reject
        // an already-terminal provisioningState locally.
        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Post, Assert.Single(methods));
    }

    [Fact]
    public async Task Stop_SuppressesCancelResponsePayload()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("{}"));
        var output = new FakeOutputHandler();
        var processor = new StopTenantDeploymentProcessor_Orchestrator { Name = "running" };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public void ConfirmWithWhatIf_TreatsPotentialChangesAsChanges()
    {
        MethodInfo method = typeof(NewTenantDeploymentProcessor_Orchestrator).GetMethod(
            "HasEffectiveChanges",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.True((bool)method.Invoke(
            null,
            [new[] { new WhatIfChange { ChangeType = "Create" } }])!);
        Assert.False((bool)method.Invoke(
            null,
            [
                new[]
                {
                    new WhatIfChange { ChangeType = "NoChange" },
                    new WhatIfChange { ChangeType = "Ignore" },
                },
            ])!);
    }

    [Theory]
    [InlineData("Failed", false)]
    [InlineData("Failed", true)]
    [InlineData("Canceled", false)]
    [InlineData("Canceled", true)]
    public async Task WhatIf_NonSucceededStatusWithoutErrorThrows(string status, bool createPath)
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json($$"""{ "status": "{{status}}" }"""));
        IProcessor processor = createPath
            ? new NewTenantDeploymentProcessor_Orchestrator
            {
                Name = "tenant-test",
                Location = "eastus",
                TemplateObject = new Dictionary<string, object>
                {
                    ["resources"] = Array.Empty<object>(),
                },
                WhatIf = true,
            }
            : new WhatIfTenantDeploymentProcessor_Orchestrator
            {
                Name = "tenant-test",
                Location = "eastus",
                TemplateObject = new Dictionary<string, object>
                {
                    ["resources"] = Array.Empty<object>(),
                },
            };
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync());

        Assert.Contains($"what-if {status.ToLowerInvariant()}", error.Message);
    }

    [Fact]
    public async Task WhatIf_FollowsGeneratedFinalLocation()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count switch
            {
                1 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    "https://management.azure.com/providers/Microsoft.Resources/operations/what-if-1"),
                2 => Json("""{ "status": "Succeeded", "properties": { "changes": [] } }"""),
                3 => Json("""{ "status": "Succeeded", "properties": { "changes": [] } }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var output = new FakeOutputHandler();
        var processor = new WhatIfAtTenantScopeDeploymentsProcessor
        {
            DeploymentName = "tenant-test",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal(HttpMethod.Get, requests[1].Method);
        Assert.Equal(HttpMethod.Get, requests[2].Method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/what-if-1",
            requests[1].Uri.ToString());
        Assert.Equal(requests[1].Uri, requests[2].Uri);
        Assert.Equal(
            "Succeeded",
            Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues)).Status);
    }

    [Fact]
    public async Task Validate_FollowsGeneratedFinalLocation()
    {
        List<Uri> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count switch
            {
                1 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    "https://management.azure.com/providers/Microsoft.Resources/operations/validate-1"),
                2 => Json("""{ "error": null }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var output = new FakeOutputHandler();
        var processor = new ValidateAtTenantScopeDeploymentsProcessor
        {
            DeploymentName = "tenant-test",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/validate-1",
            requests[1].ToString());
        Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
    }

    [Fact]
    public async Task TestValidation_ThrowsAndWritesNoSuccessOutput()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """
            {
              "error": {
                "code": "InvalidTemplate",
                "message": "The template is invalid."
              }
            }
            """,
            HttpStatusCode.BadRequest));
        var output = new FakeOutputHandler();
        var processor = new TestTenantDeploymentProcessor_Orchestrator
        {
            Name = "invalid",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, output);

        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("Azure-AsyncOperation")]
    [InlineData("Operation-Location")]
    [InlineData("Location")]
    public async Task Delete_WaitsOnAsyncHeaderWithoutWritingOperationPayload(string headerName)
    {
        List<Uri> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count switch
            {
                1 => WithHeaders(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    (headerName, "https://management.azure.com/providers/Microsoft.Resources/operations/delete-1")),
                2 => new HttpResponseMessage(HttpStatusCode.NoContent),
                3 => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var output = new FakeOutputHandler();
        var processor = new DeleteAtTenantScopeDeploymentsProcessor_Customization
        {
            DeploymentName = "tenant-test",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(headerName == "Location" ? 3 : 2, requests.Count);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/delete-1",
            requests[1].ToString());
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task WaitDeleted_TreatsNotFoundAsSuccess()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "error": { "code": "NotFound" } }""", HttpStatusCode.NotFound));
        var processor = new WaitTenantDeploymentProcessor
        {
            Name = "gone",
            Deleted = true,
            Interval = 1,
            Timeout = 1,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Wait_UsesSessionCancellationTokenDuringPollingDelay()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """{ "name": "running", "properties": { "provisioningState": "Running" } }"""));
        var processor = new WaitTenantDeploymentProcessor
        {
            Name = "running",
            Updated = true,
            Interval = 30,
            Timeout = 60,
        };
        Init(
            processor,
            pipeline,
            new FakeOutputHandler(),
            new FakeEventPublisher { Token = cancellation.Token });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());

        Assert.Equal(1, pipeline.SendCount);
    }

    private static HttpResponseMessage WithLocation(HttpResponseMessage response, string location)
    {
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static HttpResponseMessage WithHeaders(
        HttpResponseMessage response,
        params (string Name, string Value)[] headers)
    {
        foreach ((string name, string value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }
        return response;
    }
}
