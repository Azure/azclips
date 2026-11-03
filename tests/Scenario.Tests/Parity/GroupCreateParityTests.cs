using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// <c>az group create</c> and <c>New-AzResourceGroup</c> resolve to the same
/// <c>NewResourceGroupProcessor</c>, so for one recording to serve both surfaces the HTTP
/// they emit must be identical. These tests drive both surfaces subcutaneously (the real
/// dispatcher/binder for the CLI, the real cmdlet invocation shape and PS binder for
/// PowerShell), capture the traffic at the pipeline's tracing seam, and diff it.
/// No Azure or OpenAI credentials are used.
/// </summary>
[Collection("Scenario Tests")]
public class GroupCreateParityTests
{
    // Fixed identities from Tests.Common stubs (StubProfileService / StubAuthService).
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "azclips-scenario-rg";
    private const string Location = "westus2";

    private static readonly string s_expectedUri =
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}?api-version=2025-04-01";

    /// <summary>
    /// Canned ARM answer for the PUT, shaped like a real 201 from
    /// Microsoft.Resources/resourceGroups. The parity tests only compare what the surfaces
    /// send, so a canned answer is enough; no recording involved.
    /// </summary>
    private static HttpResponseMessage ArmResponder(HttpRequestMessage request)
    {
        var body =
            $"{{\"id\":\"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}\"," +
            $"\"name\":\"{ResourceGroupName}\",\"type\":\"Microsoft.Resources/resourceGroups\"," +
            $"\"location\":\"{Location}\",\"properties\":{{\"provisioningState\":\"Succeeded\"}}}}";
        return new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static IReadOnlyList<object> RunCliGroupCreate(ScenarioHost host) =>
        host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", SubscriptionId);

    private static IReadOnlyList<object> RunPsNewResourceGroup(ScenarioHost host) =>
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

    [Fact]
    public void Cli_GroupCreate_EmitsExpectedArmRequest()
    {
        var http = new CapturingTracingInterceptor(ArmResponder);
        using var host = new ScenarioHost(ClientType.CLI, http);

        var outputs = RunCliGroupCreate(host);

        var request = Assert.Single(http.Captured);
        AssertGroupCreatePut(request);

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }

    [Fact]
    public void PowerShell_NewResourceGroup_EmitsExpectedArmRequest()
    {
        var http = new CapturingTracingInterceptor(ArmResponder);
        using var host = new ScenarioHost(ClientType.PowerShell, http);

        var outputs = RunPsNewResourceGroup(host);

        var request = Assert.Single(http.Captured);
        AssertGroupCreatePut(request);

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }

    [Fact]
    public void GroupCreate_CliAndPowerShell_EmitIdenticalHttpRequests()
    {
        CapturedHttpRequest cliRequest;
        CapturedHttpRequest psRequest;
        object cliOutput;
        object psOutput;

        var cliHttp = new CapturingTracingInterceptor(ArmResponder);
        using (var cli = new ScenarioHost(ClientType.CLI, cliHttp))
        {
            cliOutput = Assert.Single(RunCliGroupCreate(cli));
            cliRequest = Assert.Single(cliHttp.Captured);
        }

        var psHttp = new CapturingTracingInterceptor(ArmResponder);
        using (var ps = new ScenarioHost(ClientType.PowerShell, psHttp))
        {
            psOutput = Assert.Single(RunPsNewResourceGroup(ps));
            psRequest = Assert.Single(psHttp.Captured);
        }

        AssertGroupCreatePut(cliRequest);
        AssertGroupCreatePut(psRequest);
        Assert.NotEqual(ClientRequestId(cliRequest), ClientRequestId(psRequest));

        var cliHeaders = cliRequest.Headers
            .Where(header => !header.Key.Equals("x-ms-client-request-id", StringComparison.OrdinalIgnoreCase))
            .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .Select(header => $"{header.Key}: {string.Join(", ", header.Value)}");
        var psHeaders = psRequest.Headers
            .Where(header => !header.Key.Equals("x-ms-client-request-id", StringComparison.OrdinalIgnoreCase))
            .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .Select(header => $"{header.Key}: {string.Join(", ", header.Value)}");
        Assert.Equal(cliRequest.Method, psRequest.Method);
        Assert.Equal(cliRequest.Uri, psRequest.Uri);
        Assert.Equal(cliHeaders, psHeaders);
        Assert.Equal(cliRequest.Body, psRequest.Body);

        // Same request + same recording must also yield the same object on both surfaces.
        Assert.Equal(JsonSerializer.Serialize(cliOutput), JsonSerializer.Serialize(psOutput));
    }

    private static void AssertGroupCreatePut(CapturedHttpRequest request)
    {
        Assert.Equal("PUT", request.Method);
        Assert.Equal(s_expectedUri, request.Uri);

        Assert.True(request.Headers.ContainsKey("Authorization"),
            "The capture seam sits after the auth step, so the request must carry a bearer token.");
        Assert.True(Guid.TryParse(ClientRequestId(request), out _));

        Assert.NotNull(request.Body);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(ResourceGroupName, body.RootElement.GetProperty("name").GetString());
        Assert.Equal(Location, body.RootElement.GetProperty("location").GetString());
    }

    private static string ClientRequestId(CapturedHttpRequest request)
    {
        Assert.True(request.Headers.TryGetValue("x-ms-client-request-id", out var values));
        return Assert.Single(values!);
    }
}
