// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Pins the resource-aware benchmark stub: (1) VNet path returns a body whose
/// properties.subnets[0].id is populated (the field the VM-create orchestrator reads),
/// (2) NSG/NIC paths echo an id, (3) VM path returns a terminal provisioningState,
/// (4) unmapped paths and the list path fall back to "{}", (5) every response carries
/// Retry-After: 0.
/// </summary>
public class StubHttpTerminateHandlerTests
{
    private const string Sub = "00000000-0000-0000-0000-000000000000";

    private static async Task<HttpResponseMessage> Send(string url)
    {
        var handler = new StubHttpTerminateHandler();
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        return await handler.SendAsync(request, null!, null!);
    }

    [Fact]
    public async Task VirtualNetworkPath_ReturnsSubnetWithId()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Network/virtualNetworks/vnet?api-version=2025-05-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var subnets = doc.RootElement.GetProperty("properties").GetProperty("subnets");
        Assert.True(subnets.GetArrayLength() >= 1);
        Assert.False(string.IsNullOrEmpty(subnets[0].GetProperty("id").GetString()));
    }

    [Fact]
    public async Task VirtualMachinePath_ReturnsTerminalProvisioningState()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Compute/virtualMachines/vm?api-version=2025-11-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var state = doc.RootElement.GetProperty("properties").GetProperty("provisioningState").GetString();
        Assert.Equal("Succeeded", state);
    }

    [Theory]
    [InlineData("Microsoft.Network/networkInterfaces/nic")]
    [InlineData("Microsoft.Network/networkSecurityGroups/nsg")]
    public async Task NicAndNsgPaths_EchoAnId(string resource)
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/{resource}?api-version=2025-05-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task ResourceGroupPath_ReturnsEmptyObject()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourcegroups/bench-rg?api-version=2025-04-01");
        Assert.Equal("{}", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task VirtualMachineListPath_ReturnsEmptyObject()
    {
        // No trailing slash after "virtualMachines" => a list, not a single VM.
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01");
        Assert.Equal("{}", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task EveryResponse_SetsRetryAfterZero()
    {
        var response = await Send("https://management.azure.com/anything");
        Assert.Equal("0", Assert.Single(response.Headers.GetValues("Retry-After")));
    }
}
