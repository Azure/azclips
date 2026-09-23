// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Scenario.Tests;

/// <summary>
/// Stands in for ARM behind `vm show`. Serves the virtual machine, its NIC, and the NIC's public
/// IP, and honours $expand so the instanceView / userData paths can be asserted without a
/// recording. Every request URI is captured in <see cref="Requests"/>.
/// </summary>
internal sealed class FakeVmShowArmBackend : HttpMessageHandler
{
    public const string NicName = "scenario-show-nic";
    public const string PublicIpName = "scenario-show-pip";
    public const string PrivateIp = "10.0.0.4";
    public const string PublicIp = "20.0.0.4";
    public const string MacAddress = "00-0D-3A-11-22-33";
    public const string UserDataValue = "dGVzdC11c2VyLWRhdGE=";

    private readonly string _subscriptionId;

    public FakeVmShowArmBackend(string subscriptionId) => _subscriptionId = subscriptionId;

    /// <summary>Absolute URI of every request the orchestration issued, in order.</summary>
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.AbsoluteUri);

        var path = request.RequestUri!.AbsolutePath;
        var query = request.RequestUri!.Query;
        var name = path.TrimEnd('/').Split('/')[^1];

        if (path.Contains("/publicIPAddresses/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/publicIPAddresses", name),
                name,
                location = "westus2",
                properties = new
                {
                    ipAddress = PublicIp,
                    dnsSettings = new { fqdn = $"{name}.westus2.cloudapp.azure.com" },
                },
            }));
        }

        if (path.Contains("/networkInterfaces/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Network/networkInterfaces", name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    macAddress = MacAddress,
                    ipConfigurations = new[]
                    {
                        new
                        {
                            name = "ipconfig1",
                            properties = new
                            {
                                privateIPAddress = PrivateIp,
                                publicIPAddress = new { id = Id("Microsoft.Network/publicIPAddresses", PublicIpName) },
                            },
                        },
                    },
                },
            }));
        }

        if (path.Contains("/virtualMachines/", StringComparison.OrdinalIgnoreCase))
        {
            // A dictionary rather than an anonymous type: the expanded members are conditional.
            var properties = new Dictionary<string, object?>
            {
                ["provisioningState"] = "Succeeded",
                ["hardwareProfile"] = new { vmSize = "Standard_D2s_v3" },
                ["storageProfile"] = new { imageReference = new { publisher = "Canonical", offer = "0001-com-ubuntu-server-jammy", sku = "22_04-lts-gen2", version = "latest" } },
                ["osProfile"] = new { computerName = name, adminUsername = "azadmin" },
                ["networkProfile"] = new { networkInterfaces = new[] { new { id = Id("Microsoft.Network/networkInterfaces", NicName) } } },
            };

            if (query.Contains("instanceView", StringComparison.OrdinalIgnoreCase))
            {
                properties["instanceView"] = new
                {
                    statuses = new[]
                    {
                        new { code = "ProvisioningState/succeeded", displayStatus = "Provisioning succeeded" },
                        new { code = "PowerState/running", displayStatus = "VM running" },
                    },
                };
            }

            if (query.Contains("userData", StringComparison.OrdinalIgnoreCase))
            {
                properties["userData"] = UserDataValue;
            }

            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Compute/virtualMachines", name),
                name,
                location = "westus2",
                properties,
            }));
        }

        throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
    }

    private string Id(string provider, string name)
        => $"/subscriptions/{_subscriptionId}/resourceGroups/scenario-show-rg/providers/{provider}/{name}";

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };
}
