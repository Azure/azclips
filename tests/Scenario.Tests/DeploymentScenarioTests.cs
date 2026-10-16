using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Offline regression coverage for the commands that share <c>DeploymentOrchestratorBase</c>
/// (create, what-if, validate). Each scenario runs through the real CLI dispatcher or the
/// cmdlet invocation shape, captures the ARM request at the tracing seam and asserts its URI
/// and body. A canned ARM responder stands in for the network; no credentials are used.
/// </summary>
[Collection("Scenario Tests")]
public sealed class DeploymentScenarioTests : IDisposable
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "azclips-scenario-rg";
    private const string DeploymentsUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/providers/Microsoft.Resources/deployments/";

    private static readonly ExtensionUnderTest s_deployment = ExtensionUnderTest.Resources with
    {
        Verb = "deployment",
        Leaves = ["group create", "group what-if", "group validate"],
    };

    private readonly string _templateFile;

    public DeploymentScenarioTests()
    {
        _templateFile = Path.Combine(Path.GetTempPath(), $"azclips-deploy-{Guid.NewGuid():N}.json");
        File.WriteAllText(_templateFile, """
            {
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "p1": { "type": "string" },
                "count": { "type": "int" },
                "withDefault": { "type": "string", "defaultValue": "d" }
              },
              "resources": []
            }
            """);
    }

    public void Dispose() => File.Delete(_templateFile);

    // Every deployment endpoint answers a successful terminal result, so LRO polling ends at once.
    private static HttpResponseMessage ArmResponder(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = path.EndsWith("/whatIf", StringComparison.OrdinalIgnoreCase)
            ? """{"status":"Succeeded","changes":[]}"""
            : """{"id":"/subscriptions/x/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep","name":"dep","properties":{"provisioningState":"Succeeded"}}""";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed record Run(string[] Cli, string Ps, Dictionary<string, object> PsParameters);

    private static IReadOnlyList<CapturedHttpRequest> Execute(ClientType surface, Run run)
    {
        var http = new CapturingTracingInterceptor(ArmResponder);
        using var host = new ScenarioHost(surface, http, extensions: s_deployment);
        if (surface == ClientType.CLI)
        {
            host.RunCli([.. run.Cli, "--subscription-id", SubscriptionId]);
        }
        else
        {
            host.RunPowerShell(run.Ps, new Dictionary<string, object>(run.PsParameters) { ["SubscriptionId"] = SubscriptionId });
        }
        return http.Captured;
    }

    private Run Create(params string[] parameterTokens) => Create(rollbackOnError: true, parameterTokens);

    private Run Create(bool rollbackOnError, params string[] parameterTokens) => new(
        ["deployment", "group", "create", "--resource-group", ResourceGroupName, "--name", "dep",
         "--template-file", _templateFile, "--mode", "Complete",
         .. (rollbackOnError ? new[] { "--rollback-on-error" } : []),
         .. parameterTokens.SelectMany(t => new[] { "--parameter", t })],
        "New-AzResourceGroupDeployment",
        new()
        {
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = "dep",
            ["TemplateFile"] = _templateFile,
            ["Mode"] = "Complete",
            ["RollbackOnError"] = rollbackOnError,
            ["Parameter"] = parameterTokens,
        });

    private static Run WithValidationLevel(Run run, string level) => new(
        [.. run.Cli, "--validation-level", level],
        run.Ps,
        new(run.PsParameters) { ["ValidationLevel"] = level });

    private static readonly Run s_whatIf = new(
        ["deployment", "group", "what-if", "--resource-group", ResourceGroupName, "--name", "dep",
         "--template-uri", "https://example.com/t.json", "--query-string", "?sig=abc",
         "--parameters-uri", "https://example.com/p.json", "--result-format", "ResourceIdOnly"],
        "New-AzResourceGroupDeploymentWhatIf",
        new()
        {
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = "dep",
            ["TemplateUri"] = "https://example.com/t.json",
            ["QueryString"] = "?sig=abc",
            ["ParametersUri"] = "https://example.com/p.json",
            ["ResultFormat"] = "ResourceIdOnly",
        });

    private Run Validate() => new(
        ["deployment", "group", "validate", "--resource-group", ResourceGroupName,
         "--template-file", _templateFile, "--parameter", "p1=v1", "--parameter", "count=3"],
        "Test-AzResourceGroupDeployment",
        new()
        {
            ["ResourceGroupName"] = ResourceGroupName,
            ["TemplateFile"] = _templateFile,
            ["Parameter"] = new[] { "p1=v1", "count=3" },
        });

    private static JsonElement Properties(CapturedHttpRequest request)
    {
        Assert.NotNull(request.Body);
        return JsonDocument.Parse(request.Body).RootElement.GetProperty("properties").Clone();
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void Create_OfflinePolling_CompletesWithoutDefaultDelay(ClientType surface)
    {
        var stopwatch = Stopwatch.StartNew();

        var requests = Execute(surface, Create("p1=v1", "count=3"));

        stopwatch.Stop();
        Assert.Equal(new[] { "POST", "PUT", "GET", "GET" }, requests.Select(r => r.Method));
        Assert.Equal(DeploymentsUri + "dep/validate?api-version=2025-04-01", requests[0].Uri);
        Assert.All(requests.Skip(1), r => Assert.Equal(DeploymentsUri + "dep?api-version=2025-04-01", r.Uri));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Offline deployment polling took {stopwatch.Elapsed}; expected no 15-second polling delay.");
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void Create_SendsPutWithInlineTemplateParametersAndRollback(ClientType surface)
    {
        var request = Assert.Single(Execute(surface, Create("p1=v1", "count=3")), r => r.Method == "PUT");

        Assert.Equal(DeploymentsUri + "dep?api-version=2025-04-01", request.Uri);
        var properties = Properties(request);
        Assert.Equal("Complete", properties.GetProperty("mode").GetString());
        Assert.True(properties.GetProperty("template").TryGetProperty("resources", out _));
        Assert.Equal("v1", properties.GetProperty("parameters").GetProperty("p1").GetProperty("value").GetString());
        Assert.Equal(3, properties.GetProperty("parameters").GetProperty("count").GetProperty("value").GetInt32());
        Assert.Equal("LastSuccessful", properties.GetProperty("onErrorDeployment").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void WhatIf_SendsPostWithTemplateLinkQueryStringAndResultFormat(ClientType surface)
    {
        var request = Execute(surface, s_whatIf)[0];

        Assert.Equal("POST", request.Method);
        Assert.Equal(DeploymentsUri + "dep/whatIf?api-version=2025-04-01", request.Uri);
        var properties = Properties(request);
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        var templateLink = properties.GetProperty("templateLink");
        Assert.Equal("https://example.com/t.json", templateLink.GetProperty("uri").GetString());
        Assert.Equal("sig=abc", templateLink.GetProperty("queryString").GetString());
        Assert.Equal("https://example.com/p.json", properties.GetProperty("parametersLink").GetProperty("uri").GetString());
        Assert.False(properties.TryGetProperty("template", out var template) && template.ValueKind != JsonValueKind.Null);
        Assert.Equal("ResourceIdOnly", properties.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void Validate_WithoutName_PostsToGeneratedDeploymentName(ClientType surface)
    {
        var request = Execute(surface, Validate())[0];

        Assert.Equal("POST", request.Method);
        Assert.StartsWith(DeploymentsUri, request.Uri);
        Assert.EndsWith("/validate?api-version=2025-04-01", request.Uri);
        var name = request.Uri[DeploymentsUri.Length..^"/validate?api-version=2025-04-01".Length];
        Assert.True(Guid.TryParse(name, out _), $"Expected a generated GUID deployment name, got '{name}'.");
        Assert.Equal("v1", Properties(request).GetProperty("parameters").GetProperty("p1").GetProperty("value").GetString());
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void Create_MissingTemplateParameter_FailsBeforeAnyRequest(ClientType surface)
    {
        var http = new CapturingTracingInterceptor(ArmResponder);
        using var host = new ScenarioHost(surface, http, extensions: s_deployment);
        var run = Create("p1=v1");

        var error = Assert.ThrowsAny<ArgumentException>(() =>
        {
            if (surface == ClientType.CLI)
            {
                host.RunCli([.. run.Cli, "--subscription-id", SubscriptionId]);
            }
            else
            {
                host.RunPowerShell(run.Ps, new Dictionary<string, object>(run.PsParameters) { ["SubscriptionId"] = SubscriptionId });
            }
        });

        Assert.Contains("count", error.Message);
        Assert.Empty(http.Captured);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void Create_WithoutRollback_SendsNoOnErrorDeployment(ClientType surface)
    {
        var request = Assert.Single(Execute(surface, Create(false, "p1=v1", "count=3")), r => r.Method == "PUT");

        Assert.False(Properties(request).TryGetProperty("onErrorDeployment", out var onError) && onError.ValueKind != JsonValueKind.Null);
    }

    [Theory]
    [InlineData(ClientType.CLI, "create")]
    [InlineData(ClientType.PowerShell, "create")]
    [InlineData(ClientType.CLI, "what-if")]
    [InlineData(ClientType.PowerShell, "what-if")]
    public void CreateAndWhatIf_PassValidationLevel(ClientType surface, string command)
    {
        var run = WithValidationLevel(command == "create" ? Create("p1=v1", "count=3") : s_whatIf, "Provider");

        var request = Execute(surface, run)[0];

        Assert.Equal("Provider", Properties(request).GetProperty("validationLevel").GetString());
    }

    [Fact]
    public void Create_CliAndPowerShell_EmitIdenticalRequests()
    {
        var run = Create("p1=v1", "count=3");

        var cli = Assert.Single(Execute(ClientType.CLI, run), r => r.Method == "PUT");
        var ps = Assert.Single(Execute(ClientType.PowerShell, run), r => r.Method == "PUT");

        Assert.Equal(cli.ToString(), ps.ToString());
    }
}
