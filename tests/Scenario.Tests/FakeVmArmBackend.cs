using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind the vm create orchestration. Responses carry the ids the
/// orchestrators read from each step, and the vm create is a real LRO with two polls.
/// NSGs and public IPs exist once PUT, or when their ids are passed in as already existing;
/// a GET of any other answers 404. Their ids come from the request path, so resources in
/// other resource groups resolve too.
/// </summary>
internal sealed class FakeVmArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private int _vmPolls;
    private readonly HashSet<string> _existingIds;

    /// <param name="existingResourceIds">NSG and public IP ids that exist before vm create runs.</param>
    public FakeVmArmBackend(string subscriptionId, params string[] existingResourceIds)
    {
        _subscriptionId = subscriptionId;
        _existingIds = new HashSet<string>(existingResourceIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Answers the VM read that follows a successful create (its <c>$expand=instanceView</c> GET)
    /// with a 500, after the VM itself was created.
    /// </summary>
    public bool FailVmDetailRead { get; init; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!.AbsoluteUri;
        var path = request.RequestUri!.AbsolutePath;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/resourcegroups/", StringComparison.OrdinalIgnoreCase) && !path.Contains("/providers/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new
            {
                id = $"/subscriptions/{_subscriptionId}/resourceGroups/{name}",
                name,
                type = "Microsoft.Resources/resourceGroups",
                location = "westus2",
                properties = new { provisioningState = "Succeeded" },
            }, HttpStatusCode.Created));
        }

        if (path.Contains("/operations/vm-create", StringComparison.OrdinalIgnoreCase))
        {
            _vmPolls++;
            return Task.FromResult(Json(new { status = _vmPolls < 2 ? "InProgress" : "Succeeded" }, HttpStatusCode.OK));
        }

        if (path.Contains("/publicIPAddresses/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(TrackedResource(
                request, path, name, "Microsoft.Network/publicIPAddresses",
                sku: new { name = "Standard" },
                properties: new { provisioningState = "Succeeded", publicIPAllocationMethod = "Static", ipAddress = "203.0.113.10" }));
        }

        if (path.Contains("/virtualNetworks/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/virtualNetworks", name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    subnets = new[] { new { id = Id("Microsoft.Network/virtualNetworks", name) + "/subnets/default", name = "default" } },
                },
            }, HttpStatusCode.OK));
        }

        if (path.Contains("/networkSecurityGroups/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(TrackedResource(
                request, path, name, "Microsoft.Network/networkSecurityGroups",
                sku: null,
                properties: new { provisioningState = "Succeeded" }));
        }

        if (path.Contains("/networkInterfaces/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/networkInterfaces", name),
                name,
                location = "westus2",
                properties = new { provisioningState = "Succeeded" },
            }, HttpStatusCode.OK));
        }

        if (path.Contains("/virtualMachines/", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Method == HttpMethod.Put)
            {
                var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
                accepted.Headers.TryAddWithoutValidation(
                    "Azure-AsyncOperation",
                    $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Compute/locations/westus2/operations/vm-create?api-version=2025-11-01");
                return Task.FromResult(accepted);
            }

            if (FailVmDetailRead && Uri.UnescapeDataString(request.RequestUri!.Query).Contains("$expand=instanceView", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(new
                {
                    error = new { code = "InternalServerError", message = "An internal server error occurred." },
                }, HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Compute/virtualMachines", name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                    storageProfile = new { imageReference = new { publisher = "MicrosoftWindowsServer", offer = "WindowsServer", sku = "2022-datacenter-azure-edition", version = "latest" } },
                    osProfile = new { computerName = name, adminUsername = "azadmin", windowsConfiguration = new { } },
                    networkProfile = new { networkInterfaces = new[] { new { id = Id("Microsoft.Network/networkInterfaces", "scenario-vm-nic") } } },
                },
            }, HttpStatusCode.OK));
        }

        throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {uri}");
    }

    /// <summary>An NSG or public IP: a PUT creates it, and a GET of one that doesn't exist answers 404.</summary>
    private HttpResponseMessage TrackedResource(HttpRequestMessage request, string path, string name, string type, object? sku, object properties)
    {
        var id = path.TrimEnd('/');

        // Requests fan out in parallel, so guard the set.
        lock (_existingIds)
        {
            if (request.Method == HttpMethod.Put)
            {
                _existingIds.Add(id);
            }
            else if (!_existingIds.Contains(id))
            {
                return Json(new
                {
                    error = new { code = "ResourceNotFound", message = $"The Resource '{type}/{name}' was not found." },
                }, HttpStatusCode.NotFound);
            }
        }

        return Json(new { id, name, location = "westus2", sku, properties }, HttpStatusCode.OK);
    }

    private string Id(string provider, string name)
        => $"/subscriptions/{_subscriptionId}/resourceGroups/scenario-vm-rg/providers/{provider}/{name}";

    // Retry-After 1 keeps recording against this fake quick (the engine falls back to 15s)
    // while still storing a nonzero value for the playback neutralization test to override.
    private static HttpResponseMessage Json(object body, HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "1");
        return response;
    }
}
