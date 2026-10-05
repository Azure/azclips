using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind the vm create orchestration. Responses carry the ids the
/// orchestrators read from each step, and the vm create is a real LRO with two polls.
/// Public IPs are remembered once PUT, so an unknown name answers 404.
/// </summary>
internal sealed class FakeVmArmBackend : HttpMessageHandler
{
    private readonly string _subscriptionId;
    private int _vmPolls;
    private readonly HashSet<string> _publicIps = new(StringComparer.OrdinalIgnoreCase);

    public FakeVmArmBackend(string subscriptionId)
    {
        _subscriptionId = subscriptionId;
    }

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

        if (path.Contains("/artifactTypes/vmImage/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/versions", StringComparison.OrdinalIgnoreCase))
        {
            var imageVersion = path.Contains("/publishers/MicrosoftWindowsServer/", StringComparison.OrdinalIgnoreCase)
                ? "2022.0.202409010"
                : path.Contains("/publishers/Contoso/", StringComparison.OrdinalIgnoreCase)
                ? "with-plan"
                : path.Contains("/publishers/VersionTest/", StringComparison.OrdinalIgnoreCase)
                ? "9.0.0"
                : "22.04.202409010";
            var operatingSystem = path.Contains("/publishers/MicrosoftWindowsServer/", StringComparison.OrdinalIgnoreCase)
                ? "Windows"
                : "Linux";
            var hasPlan = path.Contains("/publishers/Contoso/", StringComparison.OrdinalIgnoreCase);

            object[] versions = path.Contains("/publishers/VersionTest/", StringComparison.OrdinalIgnoreCase)
                ? new object[]
                {
                    new
                    {
                        name = "9.0.0",
                        properties = new
                        {
                            osDiskImage = new { operatingSystem },
                            plan = (object?)null
                        }
                    },
                    new
                    {
                        name = "10.0.0",
                        properties = new
                        {
                            osDiskImage = new { operatingSystem },
                            plan = (object?)null
                        }
                    }
                }
                : new object[]
                {
                    new
                    {
                        name = imageVersion,
                        properties = new
                        {
                            osDiskImage = new { operatingSystem },
                            plan = hasPlan ? new { name = "plan-name", product = "plan-product", publisher = "plan-publisher" } : null
                        }
                    }
                };

            return Task.FromResult(Json(versions, HttpStatusCode.OK));
        }

        if (path.Contains("/artifactTypes/vmImage/", StringComparison.OrdinalIgnoreCase)
            && path.Contains("/versions/", StringComparison.OrdinalIgnoreCase))
        {
            var operatingSystem = path.Contains("/publishers/MicrosoftWindowsServer/", StringComparison.OrdinalIgnoreCase)
                ? "Windows"
                : "Linux";
            var hasPlan = path.Contains("/publishers/Contoso/", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(Json(new
            {
                name,
                properties = new
                {
                    hyperVGeneration = path.Contains("/publishers/TrustedLaunchTest/", StringComparison.OrdinalIgnoreCase) ? "V2" : null,
                    features = path.Contains("/publishers/TrustedLaunchTest/", StringComparison.OrdinalIgnoreCase)
                        ? new[] { new { name = "SecurityType", value = "TrustedLaunch" } }
                        : Array.Empty<object>(),
                    osDiskImage = new { operatingSystem },
                    plan = hasPlan ? new { name = "plan-name", product = "plan-product", publisher = "plan-publisher" } : null
                }
            }, HttpStatusCode.OK));
        }

        if (path.Contains("/publicIPAddresses/", StringComparison.OrdinalIgnoreCase))
        {
            // Requests fan out in parallel, so guard the set.
            lock (_publicIps)
            {
                if (request.Method == HttpMethod.Put)
                {
                    _publicIps.Add(name);
                }
                else if (!_publicIps.Contains(name))
                {
                    return Task.FromResult(Json(new
                    {
                        error = new { code = "ResourceNotFound", message = $"The Resource 'Microsoft.Network/publicIPAddresses/{name}' under resource group 'scenario-vm-rg' was not found." },
                    }, HttpStatusCode.NotFound));
                }
            }

            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/publicIPAddresses", name),
                name,
                location = "westus2",
                sku = new { name = "Standard" },
                properties = new { provisioningState = "Succeeded", publicIPAllocationMethod = "Static", ipAddress = "203.0.113.10" },
            }, HttpStatusCode.OK));
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
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/networkSecurityGroups", name),
                name,
                location = "westus2",
                properties = new { provisioningState = "Succeeded" },
            }, HttpStatusCode.OK));
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
