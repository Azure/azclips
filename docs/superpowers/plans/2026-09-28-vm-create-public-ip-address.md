# `az vm create --public-ip-address` / `New-AzVM -PublicIpAddressName` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--public-ip-address` to `az vm create` and `-PublicIpAddressName` to `New-AzVM` with legacy Azure CLI semantics (a `{vm}PublicIP` public IP by default), and return the created VM enriched like `az vm show -d`.

**Architecture:** Both surfaces bind one orchestrator property, `NewVMProcessor_Orchestrator.PublicIpAddress`. The NIC orchestrator classifies it — looking up explicit names and ids first, then creating the VNet, NSG and (when needed) public IP in parallel — and attaches the public IP id to the NIC's primary IP configuration. After the VM is created, the VM orchestrator re-reads it with `$expand=instanceView` and runs the existing `VirtualMachineDetailEnricher`.

**Tech Stack:** C# with nullable enabled on the .NET SDK pinned in `global.json` (10.0.401); xUnit scenario tests with record/playback; PowerShell 7 with `Microsoft.PowerShell.PlatyPS` 1.0.1 for PowerShell help; bash smoke scripts.

**Spec:** `docs/superpowers/specs/2026-09-28-vm-create-public-ip-address-design.md`

## Global Constraints

- Network api-version is `2025-05-01`; Compute api-version is `2025-11-01`.
- Default public IP name: the VM name followed by the literal `PublicIP` — `$"{Name}PublicIP"`.
- A new public IP's request body is exactly `location`, `sku.name = "Standard"` and `properties.publicIPAllocationMethod = "Static"`. No DNS settings, zones, tags, IP version or idle timeout.
- Help text for both parameters, verbatim: `Name of the public IP address to create (default: '{vm}PublicIP') or of an existing one to attach. Can also reference an existing public IP by resource ID, or specify "" for none.`
- Missing resource id error, verbatim: `Public IP address '<value>' was not found.`
- Enrichment failure warning, verbatim: `Created virtual machine '<name>' but could not retrieve its details: <exception message>`
- **No new orchestration tests.** Do not edit `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`. `FakeVmArmBackend.cs` changes are maintenance only.
- Compute orchestrators live under `src/Extensions/Compute/Customization/` but declare the `Microsoft.Azure.Azclips.Extensions.Compute.Processors` namespace.
- Build with `dotnet build azclips.slnx`. If the Core.Agent npm restore fails, use the `building-the-solution` skill.
- Commit prefixes: `[Compute]` for customer-facing changes, `{Compute}` for test-only and docs-only changes.
- **Expected red window:** from Task 2 until Task 7, `VmCreateScenarioTests`, `VmLifecycleScenarioTests` and `VmCreateScriptScenarioTests` fail playback because their recordings predate the public IP. Every other test must stay green.

---

## File Structure

| File | Task | Responsibility |
|---|---|---|
| `src/Extensions/Compute/Models/PublicIPAddress.cs` | 1 | Public IP wire model; gains the create-body fields. |
| `src/Extensions/Compute/Processors/NewPublicIPAddressProcessor.cs` (new) | 1 | Atomic `PUT publicIPAddresses/{name}` long-running operation. Hidden. |
| `src/Extensions/Compute/Processors/ComputeProcessorBase.cs` | 2 | Gains `SendAllowingNotFoundAsync()` for existence probes. |
| `src/Extensions/Compute/Processors/NewNetworkInterfaceProcessor.cs` | 2 | Gains `PublicIpAddressId` for the primary IP configuration. |
| `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` | 2 | Classifies the public IP and orders lookups before creates. |
| `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` | 2, 3 | New `PublicIpAddress` property and default name (2); output enrichment (3). |
| `tests/Scenario.Tests/FakeVmArmBackend.cs` | 2 | Maintenance: serves public IP requests for the existing orchestration tests. |
| `src/Extensions/Compute/Models/VirtualMachine.cs` | 3 | Synthetic-field doc comments name `az vm create` / `New-AzVM`. |
| `src/Extensions/Compute/ExtensionIndex.cs` | 4 | Registers `public-ip-address` and `PublicIpAddressName`. |
| `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` | 4 | `-PublicIpAddressName` parameter and forwarding. |
| `src/CLI/Help/Docs/vm/create.md` | 4 | CLI help prose and examples. |
| `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` | 4 | Regenerated CLI export fixture. |
| `src/PS/Compute/help/New-AzVM.md` | 4 | PowerShell help. |
| `tests/Scenario.Tests/VmCreateScenarioTests.cs` | 5 | Asserts the enriched create output. |
| `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` | 5 | `vm list -d` now sees a public IP. |
| `tests/Scenario.Tests/README.md` | 5 | Describes what the vm create recording now holds. |
| `tests/Scripts/CLI/VMCreate.sh`, `tests/Scripts/PS/VMCreate.ps1` | 6 | Live smoke scripts for the public IP modes. |
| `tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json`, `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json` | 7 | Re-recorded live by the user. |

There is no Compute unit-test project and the user ruled out new orchestration tests, so Tasks 1–4 verify with the build, the existing `VmCreateOrchestrationTests`, the help validators and the CLI export fixture test. The enriched output and the public IP are asserted by the scenario tests in Task 5 once Task 7 re-records them.

---

### Task 1: Public IP create processor and model fields

**Files:**
- Modify: `src/Extensions/Compute/Models/PublicIPAddress.cs` (whole file)
- Create: `src/Extensions/Compute/Processors/NewPublicIPAddressProcessor.cs`

**Interfaces:**
- Consumes: `ComputeProcessorBase<TRequest, TResponse>` and `GetFirstHeader` (existing, `Microsoft.Azure.Azclips.ExtensionRuntime`).
- Produces:
  - `public class NewPublicIPAddressProcessor : ComputeProcessorBase<PublicIPAddress, PublicIPAddress>` with `string SubscriptionId`, `string Name`, `string Location`, `string ResourceGroupName` (all default `string.Empty`), `string Sku = "Standard"`, `string AllocationMethod = "Static"`, and `internal override Task<HttpResponseMessage> SendAsync()` running the long-running operation. The final response body is the public IP JSON, including `id`.
  - `PublicIPAddress.Location` (`string?`), `PublicIPAddress.Sku` (`PublicIPAddressSku?`), `PublicIPAddressSku.Name` (`string`), `PublicIPAddressProperties.PublicIPAllocationMethod` (`string?`).

- [ ] **Step 1: Replace `src/Extensions/Compute/Models/PublicIPAddress.cs`**

`Id` and `Name` become null-omitted so the create body carries no `"id": null` / `"name": null`. Deserialization is unaffected.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class PublicIPAddress
{
    // Server-assigned: omitted from the create body, populated from ARM responses.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Name { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicIPAddressSku? Sku { get; set; }

    public PublicIPAddressProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressSku
{
    public string Name { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIPAllocationMethod { get; set; }

    // Absent until the address is allocated.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicIPAddressDnsSettings? DnsSettings { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressDnsSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

The camel-case naming policy in `ComputeProcessorBase.JsonOptions` serializes `PublicIPAllocationMethod` as `publicIPAllocationMethod`, which is ARM's spelling.

- [ ] **Step 2: Create `src/Extensions/Compute/Processors/NewPublicIPAddressProcessor.cs`**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewPublicIPAddressProcessor : ComputeProcessorBase<PublicIPAddress, PublicIPAddress>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Sku { get; set; } = "Standard";
    public string AllocationMethod { get; set; } = "Static";

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{Name}?api-version=2025-05-01";

    protected override PublicIPAddress? CreateRequestBody() => new PublicIPAddress
    {
        Location = Location,
        Sku = new PublicIPAddressSku { Name = Sku },
        Properties = new PublicIPAddressProperties
        {
            PublicIPAllocationMethod = AllocationMethod,
        },
    };

    // Wired like the NSG and NIC processors: poll Azure-AsyncOperation, then re-read the resource.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
```

- [ ] **Step 3: Build the Compute extension**

Run: `dotnet build src/Extensions/Compute/Extensions.Compute.csproj`
Expected: `Build succeeded` with no new warnings in the two files.

- [ ] **Step 4: Confirm the existing orchestration tests are unaffected**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 4 passed. Nothing calls the new processor yet.

- [ ] **Step 5: Commit**

```bash
git add src/Extensions/Compute/Models/PublicIPAddress.cs src/Extensions/Compute/Processors/NewPublicIPAddressProcessor.cs
git commit -m "[Compute] Add the public IP create processor"
```

### Task 2: Resolve and attach the public IP in the vm create orchestration

**Files:**
- Modify: `src/Extensions/Compute/Processors/ComputeProcessorBase.cs` (add `using System.Net;`; add a method after `SendAsyncLRO()`, which starts at line 147)
- Modify: `src/Extensions/Compute/Processors/NewNetworkInterfaceProcessor.cs:20` and `:70-71`
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` (whole file)
- Modify: `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` (one property, two lines in the NIC lambda)
- Modify: `tests/Scenario.Tests/FakeVmArmBackend.cs` (maintenance)
- Test: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (existing, **not edited**)

**Interfaces:**
- Consumes: `NewPublicIPAddressProcessor` (Task 1); `GetPublicIPAddressProcessor` (`SubscriptionId`, `ResourceGroupName`, `Name`); `VirtualMachineIdResolver.ParseResourceId(string id, string provider, string resourceType, string displayName, string nameToken)` returning `(string SubscriptionId, string ResourceGroupName, string Name)`.
- Produces:
  - `internal Task<HttpResponseMessage> ComputeProcessorBase.SendAllowingNotFoundAsync()` — returns 2xx and 404 responses; anything else fails exactly like `SendAsync()`.
  - `public string NewNetworkInterfaceProcessor.PublicIpAddressId` (default `string.Empty`).
  - `public string? NewNetworkInterfaceProcessor_Orchestrator.PublicIpAddress` and `public string NewNetworkInterfaceProcessor_Orchestrator.DefaultPublicIpAddressName` (default `string.Empty`).
  - `public string? NewVMProcessor_Orchestrator.PublicIpAddress`, marked `[ParameterGroup("Network Arguments")]`. Task 4 registers it.

- [ ] **Step 1: Add `SendAllowingNotFoundAsync()` to `ComputeProcessorBase`**

Add `using System.Net;` to the usings at the top of `src/Extensions/Compute/Processors/ComputeProcessorBase.cs`. Then, in the non-generic `ComputeProcessorBase` class, add this method directly after `SendAsyncLRO()`:

```csharp
        /// <summary>
        /// Existence probe for get-or-create flows. Sends the request like <see cref="SendAsync"/>, but a
        /// 404 is handed back to the caller instead of failing. <c>EnsureSuccessAsync</c> publishes the
        /// error before it throws, so catching a 404 afterwards would still print one.
        /// </summary>
        internal async Task<HttpResponseMessage> SendAllowingNotFoundAsync()
        {
            using HttpRequestMessage request = await CreateRequestMessage();
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return response;
            }

            try
            {
                await response.EnsureSuccessAsync(Publisher);
            }
            catch
            {
                response.Dispose();
                throw;
            }
            return response;
        }
```

This is the same sequence `HttpRuntime.SendAsync` runs, minus the 404, and the pattern `ExistsResourceGroupProcessor_Orchestrator` in the Resources extension already uses.

- [ ] **Step 2: Add `PublicIpAddressId` to `NewNetworkInterfaceProcessor`**

In `src/Extensions/Compute/Processors/NewNetworkInterfaceProcessor.cs`, add after `public bool AcceleratedNetworking { get; set; }` (line 20):

```csharp

    /// <summary>Resource id of the public IP for the primary IP configuration. Empty attaches none.</summary>
    public string PublicIpAddressId { get; set; } = string.Empty;
```

Then replace these two lines in the `IPConfigurationProperties` initializer (lines 70-71):

```csharp
                        PrivateIPAllocationMethod = "Dynamic",
                        Primary = index == 0
```

with:

```csharp
                        PrivateIPAllocationMethod = "Dynamic",
                        Primary = index == 0,
                        PublicIPAddress = index == 0 && !string.IsNullOrEmpty(PublicIpAddressId)
                            ? new ResourceReference { Id = PublicIpAddressId }
                            : null
```

`IPConfigurationProperties.PublicIPAddress` is already null-omitted, so a NIC without a public IP produces a request body that is byte-for-byte the same as today's.

- [ ] **Step 3: Replace `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor_Orchestrator : ComputeProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string NetworkSecurityGroupName { get; set; } = string.Empty;
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];
    public List<string> RuleNames { get; set; } = [];
    public List<string> RuleDirections { get; set; } = [];
    public List<string> RuleProtocols { get; set; } = [];
    public List<int> RulePriorities { get; set; } = [];
    public List<string> RuleSourceAddressPrefixes { get; set; } = [];
    public List<string> RuleSourcePortRanges { get; set; } = [];
    public List<string> RuleDestinationPortRanges { get; set; } = [];
    public List<string> RuleDestinationAddressPrefixes { get; set; } = [];
    public List<string> RuleAccesses { get; set; } = [];
    public bool AcceleratedNetworking { get; set; }

    /// <summary>
    /// The public IP to attach, with legacy <c>az vm create --public-ip-address</c> semantics: null
    /// creates or updates <see cref="DefaultPublicIpAddressName"/> without a lookup, empty or
    /// whitespace attaches none, a value containing '/' is the resource id of an existing public IP,
    /// and any other value is a name in <see cref="ResourceGroupName"/>, reused when it exists and
    /// created when it does not.
    /// </summary>
    public string? PublicIpAddress { get; set; }

    /// <summary>The public IP created when <see cref="PublicIpAddress"/> is null. Empty means none.</summary>
    public string DefaultPublicIpAddressName { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Explicit names and ids are looked up before anything is created, as legacy CLI checks
        // existence before it deploys, so a missing id fails without leaving a VNet or NSG behind.
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await ClassifyPublicIpAddressAsync();

        NewVirtualNetworkProcessor vnetProcessor = CreateExtendedProcessor<NewVirtualNetworkProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = VirtualNetworkName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetNames = SubnetNames;
            p.SubnetAddressPrefixes = SubnetAddressPrefixes;
            p.AddressPrefixes = AddressPrefixes;
        });

        NewNetworkSecurityGroupProcessor nsgProcessor = CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = NetworkSecurityGroupName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.RuleNames = RuleNames;
            p.RuleDirections = RuleDirections;
            p.RuleProtocols = RuleProtocols;
            p.RulePriorities = RulePriorities;
            p.RuleSourceAddressPrefixes = RuleSourceAddressPrefixes;
            p.RuleSourcePortRanges = RuleSourcePortRanges;
            p.RuleDestinationPortRanges = RuleDestinationPortRanges;
            p.RuleDestinationAddressPrefixes = RuleDestinationAddressPrefixes;
            p.RuleAccesses = RuleAccesses;
        });

        Task<HttpResponseMessage> vnetResponseTask = vnetProcessor.SendAsync();
        Task<HttpResponseMessage> nsgResponseTask = nsgProcessor.SendAsync();
        Task<string?> publicIpAddressIdTask = publicIpAddressNameToCreate is null
            ? Task.FromResult(existingPublicIpAddressId)
            : CreatePublicIpAddressAsync(publicIpAddressNameToCreate);
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        using HttpResponseMessage nsgResponse = await nsgResponseTask;
        string? publicIpAddressId = await publicIpAddressIdTask;
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;
        NetworkSecurityGroup nsg = (await nsgResponse.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;

        NewNetworkInterfaceProcessor nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = Name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetIds = vnet.Properties.Subnets.Select(subnet => subnet.Id).ToList();
            p.NetworkSecurityGroupId = nsg.Id!;
            p.AcceleratedNetworking = AcceleratedNetworking;
            p.PublicIpAddressId = publicIpAddressId ?? string.Empty;
        });
        return await nicProcessor.SendAsync();
    }

    /// <summary>
    /// Legacy CLI's <c>public_ip_address_type</c>: the id of an existing public IP to attach, or the
    /// name of one to create, or neither when no public IP is wanted.
    /// </summary>
    private async Task<(string? ExistingId, string? NameToCreate)> ClassifyPublicIpAddressAsync()
    {
        string? value = PublicIpAddress;

        if (value is null)
        {
            if (string.IsNullOrWhiteSpace(DefaultPublicIpAddressName))
            {
                return (null, null);
            }
            return (null, DefaultPublicIpAddressName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        if (value.Contains('/'))
        {
            var parsed = VirtualMachineIdResolver.ParseResourceId(
                value, "Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");
            string existingId = await FindPublicIpAddressIdAsync(parsed.SubscriptionId, parsed.ResourceGroupName, parsed.Name)
                ?? throw new InvalidOperationException($"Public IP address '{value}' was not found.");
            return (existingId, null);
        }

        string? existingByName = await FindPublicIpAddressIdAsync(SubscriptionId, ResourceGroupName, value);
        if (existingByName is not null)
        {
            return (existingByName, null);
        }
        return (null, value);
    }

    /// <summary>Returns the public IP's id, or null when ARM answers 404.</summary>
    private async Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.Name = name;
        }).SendAllowingNotFoundAsync();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }

    private async Task<string?> CreatePublicIpAddressAsync(string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<NewPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
        }).SendAsync();

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }
}
```

What changed from the old file: the two new properties, the classification call before the VNet and NSG are created, the public IP task that runs alongside them, `p.PublicIpAddressId`, and the three private helpers. The VNet and NSG lambdas are unchanged. The malformed-id message comes from `ParseResourceId`: `Invalid public ip address resource id '<id>'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{publicIpAddressName}.`

- [ ] **Step 4: Pass the public IP through `NewVMProcessor_Orchestrator`**

In `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`, add after the `AcceleratedNetworking` property:

```csharp

        /// <summary>
        /// <c>--public-ip-address</c> / <c>-PublicIpAddressName</c>, passed through to
        /// <see cref="NewNetworkInterfaceProcessor_Orchestrator.PublicIpAddress"/>. Null (omitted)
        /// creates or updates "{Name}PublicIP", legacy Azure CLI's default.
        /// </summary>
        [ParameterGroup("Network Arguments")]
        public string? PublicIpAddress { get; set; }
```

In `SendAsync()`, inside the `CreateExtendedProcessor<NewNetworkInterfaceProcessor_Orchestrator>` lambda, add after `p.AcceleratedNetworking = AcceleratedNetworking;`:

```csharp
                p.PublicIpAddress = PublicIpAddress;
                p.DefaultPublicIpAddressName = $"{Name}PublicIP";
```

- [ ] **Step 5: Run the existing orchestration tests to see them fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: FAIL (all 4 tests). The fake ARM throws `Fake ARM got an unexpected request: PUT https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/publicIPAddresses/scenario-vmPublicIP?api-version=2025-05-01`, because every vm create now makes its default public IP. The test output may show this as an assertion failure on an empty output list.

- [ ] **Step 6: Teach `FakeVmArmBackend` about public IPs (maintenance only)**

In `tests/Scenario.Tests/FakeVmArmBackend.cs`:

Extend the class summary with one sentence, so it reads:

```csharp
/// <summary>
/// Stands in for ARM behind the vm create orchestration. Responses carry the ids the
/// orchestrators read from each step, and the vm create is a real LRO with two polls.
/// Public IPs are remembered once PUT, so an unknown name answers 404.
/// </summary>
```

Add a field under `private int _vmPolls;`:

```csharp
    private readonly HashSet<string> _publicIps = new(StringComparer.OrdinalIgnoreCase);
```

Add this branch directly before the `if (path.Contains("/virtualNetworks/", ...))` branch:

```csharp
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
```

`203.0.113.10` is from the TEST-NET-3 documentation range.

- [ ] **Step 7: Run the orchestration tests to see them pass**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 4 passed.

- [ ] **Step 8: Run the rest of the scenario suite, minus the three recorded vm classes**

Run:

```powershell
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName!~VmCreateScenarioTests&FullyQualifiedName!~VmLifecycleScenarioTests&FullyQualifiedName!~VmCreateScriptScenarioTests"
```

Expected: all pass. `VmCreateScenarioTests`, `VmLifecycleScenarioTests` and `VmCreateScriptScenarioTests` now fail playback on the unrecorded public IP requests. That is the expected red window described in Global Constraints, closed by Task 7.

- [ ] **Step 9: Commit**

```bash
git add src/Extensions/Compute/Processors/ComputeProcessorBase.cs src/Extensions/Compute/Processors/NewNetworkInterfaceProcessor.cs src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs tests/Scenario.Tests/FakeVmArmBackend.cs
git commit -m "[Compute] vm create: create or attach a public IP with legacy CLI semantics"
```

### Task 3: Return the created VM enriched like `az vm show -d`

**Files:**
- Modify: `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` (add one method after `SendAsync()`)
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs:20-59` (doc comments only)

**Interfaces:**
- Consumes: `VirtualMachineDetailEnricher.ResolveExpand(bool instanceView, bool userData)`, `VirtualMachineDetailEnricher.ApplyNetworkAsync(ComputeProcessorBase owner, VirtualMachine vm)`, `VirtualMachineDetailEnricher.ApplyInstanceView(VirtualMachine vm, bool includeStatusFields)`, `GetVMProcessor.Expand`, and the `BeforeWriteOutputAsyncAndReturn(TResponse? body)` seam on `ComputeProcessorBase<TRequest, TResponse>`.
- Produces: `az vm create` / `New-AzVM` write one `VirtualMachine` with `PowerState`, `PublicIps`, `Fqdns`, `PrivateIps` and `MacAddresses` set and `Properties.InstanceView` null — the same shape `az vm show -d` writes. Task 5 asserts it.

- [ ] **Step 1: Add the enrichment override to `NewVMProcessor_Orchestrator`**

Add this method after `SendAsync()` in `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`. The file already has `using System.Net.Http.Json;`, `using Core.Abstractions;` and `using Core.Abstractions.Runtime;`, which cover `ReadFromJsonAsync`, `EventData` and `Events`.

```csharp

        /// <summary>
        /// Writes the created VM the way <c>az vm show -d</c> shows it, as legacy <c>az vm create</c>
        /// prints its public IP. The VM already exists by now, so a failed lookup only costs the
        /// detail fields: it is reported as a warning and the VM from the create is written instead.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachine? body)
        {
            if (body is null)
            {
                return false;
            }

            VirtualMachine output = body;
            try
            {
                using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                    p.Name = Name;
                    p.Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: false);
                }).SendAsync();

                VirtualMachine detailed = (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions))!;
                await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, detailed);
                VirtualMachineDetailEnricher.ApplyInstanceView(detailed, includeStatusFields: false);
                output = detailed;
            }
            catch (Exception exception)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"Created virtual machine '{Name}' but could not retrieve its details: {exception.Message}"
                });
            }

            await WriteOutput(output);
            return true;
        }
```

The write sits outside the `try`, so a failure while writing can never publish the warning and then write a second time. If the expanded `GET` itself fails, `EnsureSuccessAsync` prints its own error line before the warning. `az vm list -d` has the same behavior in `ListVMProcessor_Orchestrator.EnrichOneAsync`; keep it consistent here.

- [ ] **Step 2: Update the synthetic-field doc comments in `VirtualMachine.cs`**

In `src/Extensions/Compute/Models/VirtualMachine.cs`, replace the `PowerState` summary:

```csharp
    /// <summary>
    /// Power state, flattened out of the instance view.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// Set to "Info Not Available" when a per-VM lookup failed during a list fan-out.
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// Power state, flattened out of the instance view.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c>, and by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// Set to "Info Not Available" when a per-VM lookup failed during a list fan-out.
    /// </summary>
```

Then, in each of the four summaries for `PublicIps`, `Fqdns`, `PrivateIps` and `MacAddresses`, replace these two lines:

```csharp
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: never populated — there is no PowerShell spelling of --show-details.
```

with:

```csharp
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c> only — <c>Get-AzVM</c> has no spelling of --show-details.
```

Leave the first line of each summary (what the field holds) and the `OsName` / `OsVersion` / `HyperVGeneration` / `MaintenanceRedeployStatus` comments as they are.

- [ ] **Step 3: Build and run the orchestration tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 4 passed. `FakeVmArmBackend` already answers the expanded VM `GET` (its VM branch serves every non-PUT) and the NIC `GET`. Its NIC has no IP configurations, so no public IP `GET` is made, and `AssertVm` still sees one `VirtualMachine` named `scenario-vm` with provisioning state `Succeeded`. The enriched fields themselves are asserted in Task 5 against the live recording.

- [ ] **Step 4: Commit**

```bash
git add src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs src/Extensions/Compute/Models/VirtualMachine.cs
git commit -m "[Compute] vm create: return the VM with the vm show -d detail fields"
```

### Task 4: Expose `--public-ip-address` and `-PublicIpAddressName`, with help and fixture

**Files:**
- Modify: `src/Extensions/Compute/ExtensionIndex.cs:208` (PowerShell `s_newVMOrchestrator_PS`) and `:506` (CLI `s_newVMOrchestrator_CLI`)
- Modify: `src/PS/Compute/Cmdlets/NewVMCmdlet.cs:86` and `:126`
- Modify: `src/CLI/Help/Docs/vm/create.md` (whole file)
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
- Modify: `src/PS/Compute/help/New-AzVM.md`

**Interfaces:**
- Consumes: `NewVMProcessor_Orchestrator.PublicIpAddress` (Task 2). A `ParameterMetadata`'s first argument is the processor property name and its second is the name the user types.
- Produces: CLI flag `--public-ip-address` and PowerShell parameter `-PublicIpAddressName`, both bound to `PublicIpAddress`. Omitting the parameter leaves the property null; `""` binds as `""`. The CLI parser keeps empty tokens (`ExtensionParser`), and `PSArgumentParsingProvider` passes strings through unchanged.

- [ ] **Step 1: Register the PowerShell parameter**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_newVMOrchestrator_PS`, add after the `["AcceleratedNetworking"]` entry (line 208):

```csharp
                ["PublicIpAddressName"]            = new("PublicIpAddress", "PublicIpAddressName", typeof(string), "Name of the public IP address to create (default: '{vm}PublicIP') or of an existing one to attach. Can also reference an existing public IP by resource ID, or specify \"\" for none.", IsRequired: false, Aliases: [], AllowedValues: []),
```

- [ ] **Step 2: Register the CLI flag**

In the same file, inside `s_newVMOrchestrator_CLI`, add after the `["accelerated-networking"]` entry (line 506, one line lower once Step 1 is in):

```csharp
                ["public-ip-address"]                = new("PublicIpAddress", "public-ip-address", typeof(string), "Name of the public IP address to create (default: '{vm}PublicIP') or of an existing one to attach. Can also reference an existing public IP by resource ID, or specify \"\" for none.", IsRequired: false, Aliases: [], AllowedValues: []),
```

- [ ] **Step 3: Add `-PublicIpAddressName` to `NewVMCmdlet`**

In `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`, add after `public SwitchParameter AcceleratedNetworking { get; set; }` (line 86):

```csharp

    [Parameter(Mandatory = false)]
    public string? PublicIpAddressName { get; set; }
```

In `ProcessRecord()`, add after the `AcceleratedNetworking` forwarding line (line 126 before this step's first insertion; line 129 after it):

```csharp
        // Omitted or $null keeps the default public IP; "" is forwarded because it means none.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicIpAddressName)) && PublicIpAddressName is not null) { parameters.Add("PublicIpAddressName", PublicIpAddressName); }
```

The dictionary key is the PowerShell name registered in Step 1, not the property name. The PowerShell table maps it to `PublicIpAddress`, the same way `Get-AzVM`'s `UserData` maps to `IncludeUserData`.

- [ ] **Step 4: Replace `src/CLI/Help/Docs/vm/create.md`**

The two existing examples are unchanged; the description gains the public IP and two examples are appended. Every example must pass every required flag, or the help validator fails with `CLI_L4_MissingRequiredFlag`.

````markdown
---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, network interface, and public IP address in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. Resource names supplied for `--virtual-network-name`, `--network-security-group-name`, and `--network-interface-name` are created if they do not already exist in the resource group. By default a Standard, static public IP address named `{vm}PublicIP` is created, or updated if it already exists. Pass `--public-ip-address` a name to reuse the public IP address of that name when it exists and create it when it does not, a resource ID to attach an existing public IP address, or `""` to create none. The command returns the new virtual machine with the power state and the public and private IP addresses that `az vm show -d` reports.

### Create a Linux VM in East US

```bash
az vm create --name myvm --resource-group myrg --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --vm-size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --network-interface-name $vmName-nic --virtual-network-name $vmName-vnet --network-security-group-name $vmName-nsg --subscription-id $subId
```

### Create a VM with a named public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --public-ip-address myvm-ip
```

### Create a VM without a public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --vm-size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --network-security-group-name myvm-nsg --public-ip-address ""
```
````

The validator's tokenizer drops the empty `""` token, and a flag without a value is accepted as switch form, so the last example validates.

- [ ] **Step 5: Build the solution**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`.

- [ ] **Step 6: Check the CLI help**

Run: `dotnet artifacts/Debug/Core.Cli/az.dll vm create --help`
Expected: `--public-ip-address` is listed among the Network Arguments with the help text from Global Constraints, and the description and all four examples render.

- [ ] **Step 7: Regenerate the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`
Then run: `git --no-pager diff --stat tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/`
Expected: only `vm.json` changes. In `git --no-pager diff tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`, the `vm create` entry has the new `long_summary`, the two new examples, and a new parameter between `--profile` and `--resource-group`:

```json
      {
        "name": "--public-ip-address",
        "required": false,
        "short_summary": "Name of the public IP address to create (default: \u0027{vm}PublicIP\u0027) or of an existing one to attach. Can also reference an existing public IP by resource ID, or specify \u0022\u0022 for none.",
        "long_summary": "",
        "group_name": "Network Arguments"
      },
```

The exporter owns the escaping; take its output as written. Any change outside `vm create` means something else drifted — stop and investigate before continuing.

- [ ] **Step 8: Run the CLI export fixture test**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --filter FullyQualifiedName~CliExportFixturesTests`
Expected: 3 passed (`_root_`, `login`, `vm`).

- [ ] **Step 9: Update `src/PS/Compute/help/New-AzVM.md`**

Make six edits.

(a) SYNOPSIS — replace:

```markdown
Create a virtual machine together with its network interface, virtual network, and network security group in a single call.
```

with:

```markdown
Create a virtual machine together with its network interface, public IP address, virtual network, and network security group in a single call.
```

(b) SYNTAX — replace the last two lines of the syntax block:

```text
 [-RuleDestinationAddressPrefixes <string[]>] [-RuleAccesses <string[]>] [-AcceleratedNetworking]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

with:

```text
 [-RuleDestinationAddressPrefixes <string[]>] [-RuleAccesses <string[]>] [-AcceleratedNetworking]
 [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

(c) DESCRIPTION — replace:

```markdown
Provisions the full networking + VM stack as a single operation.
Creates a virtual network, network security group, network interface, and virtual machine using the supplied names and parameters.
Use this command when you want a one-shot VM deployment without composing the underlying resources individually.
```

with:

```markdown
Provisions the full networking + VM stack as a single operation.
Creates a virtual network, network security group, public IP address, network interface, and virtual machine using the supplied names and parameters.
By default the public IP address is a Standard, static address named after the virtual machine with a `PublicIP` suffix (for example `myvmPublicIP`), created or, if it already exists, updated.
Use `-PublicIpAddressName` to choose the public IP address by name or resource ID, or pass an empty string to create none.
Use this command when you want a one-shot VM deployment without composing the underlying resources individually.
```

(d) EXAMPLES — directly after the closing fence of the existing `### Create a VM with its networking stack` example, append:

````markdown

### Create a VM with a named public IP address

Provision the same stack, but name the public IP address `myvm-ip`.
If a public IP address named `myvm-ip` already exists in `myrg`, it is attached as-is instead of being created.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -NetworkSecurityGroupName mynsg -PublicIpAddressName myvm-ip
```

### Create a VM without a public IP address

Pass an empty string to `-PublicIpAddressName` to provision the stack without a public IP address.
The virtual machine is then reachable only from inside its virtual network.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -NetworkSecurityGroupName mynsg -PublicIpAddressName ''
```
````

Each fence assigns `$adminPassword` itself (PSL5) and binds every mandatory parameter (PSL3).

(e) PARAMETERS — insert this block directly before `### -ResourceGroupName`, which keeps the section alphabetical:

````markdown
### -PublicIpAddressName

Name of the public IP address to create (default: '{vm}PublicIP') or of an existing one to attach.
Can also reference an existing public IP by resource ID, or specify "" for none.

When omitted, a public IP address named after the virtual machine with a `PublicIP` suffix is created, or updated if it already exists.
A name is looked up in the resource group: an existing public IP address is attached as-is, otherwise one is created.
A resource ID must reference an existing public IP address, which is attached as-is.
New public IP addresses use the Standard SKU with static allocation, in the virtual machine's location.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

````

(f) OUTPUTS — replace:

```markdown
The newly-created `VirtualMachine` object, including its assigned ARM resource ID, provisioning state, size, and network configuration.
```

with:

```markdown
The newly-created `VirtualMachine` object, including its assigned ARM resource ID, provisioning state, size, and network configuration, plus the `PowerState`, `PublicIps`, `Fqdns`, `PrivateIps`, and `MacAddresses` detail fields.
`Fqdns` is empty because no DNS name label is set, and `PublicIps` is empty when `-PublicIpAddressName` is an empty string.
```

- [ ] **Step 10: Validate the PowerShell help against the built cmdlet**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records. If PlatyPS 1.0.1 is missing, run `pwsh -File build/Install-BuildDependencies.ps1` first.

If PSL14 reports drift for `New-AzVM.md` (PlatyPS wants the SYNTAX or parameter block laid out differently):
1. Run `pwsh -File build/New-PSHelpMarkdown.ps1 -Module Compute -Refresh`.
2. Keep only the `New-AzVM.md` change: `git checkout -- src/PS/Compute/help/Get-AzVM.md src/PS/Compute/help/Remove-AzVM.md src/PS/Compute/help/Start-AzVM.md src/PS/Compute/help/Stop-AzVM.md`.
3. Put back any prose from Step 9 that the refresh replaced with a `TODO -` marker.
4. Re-run `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute` and expect no records.

- [ ] **Step 11: Run the full help pipeline**

Run: `pwsh -File build/build.ps1 -GenerateHelpArtifacts`
Expected: exit code 0. This rebuilds the solution and validates every CLI and PowerShell example against the live metadata and cmdlets, including the four new examples.

- [ ] **Step 12: Re-run the orchestration tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 4 passed. The tests don't pass the new parameter, so they still take the default path.

- [ ] **Step 13: Commit**

```bash
git add src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/NewVMCmdlet.cs src/CLI/Help/Docs/vm/create.md tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json src/PS/Compute/help/New-AzVM.md
git commit -m "[Compute] Add az vm create --public-ip-address and New-AzVM -PublicIpAddressName"
```

### Task 5: Scenario test expectations for the default public IP

**Files:**
- Modify: `tests/Scenario.Tests/VmCreateScenarioTests.cs` (`AssertVm`)
- Modify: `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` (`AssertListedWithDetails` and its summary)
- Modify: `tests/Scenario.Tests/README.md:54-59`

**Interfaces:**
- Consumes: the enriched create output (Task 3) and the default public IP (Task 2), both as seen through recordings that Task 7 refreshes.
- Produces: assertions that fail against today's recordings and pass once Task 7 re-records.

- [ ] **Step 1: Assert the enriched create output in `VmCreateScenarioTests`**

In `tests/Scenario.Tests/VmCreateScenarioTests.cs`, replace `AssertVm`:

```csharp
    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
    }
```

with:

```csharp
    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);

        // vm create writes the az vm show -d view: the default public IP is attached, and no DNS
        // label is set, so Fqdns is an empty string rather than null.
        Assert.Equal("VM running", vm.PowerState);
        Assert.False(string.IsNullOrEmpty(vm.PublicIps));
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.Equal("", vm.Fqdns);
        Assert.Null(vm.Properties.InstanceView);
    }
```

The scenario's subnet is `10.0.0.0/24`, so the NIC's private IP starts with `10.0.0.`.

- [ ] **Step 2: Flip the public IP expectation in `VmLifecycleScenarioTests`**

In `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`, replace the summary and first lines of `AssertListedWithDetails`:

```csharp
    /// <summary>
    /// <c>vm list -d</c>: the power state comes from the instance view, and the network fields from
    /// the NIC lookup. <c>vm create</c> allocates no public IP, so <c>PublicIps</c> and <c>Fqdns</c>
    /// are empty strings, not null; null would mean the lookup never ran.
    /// </summary>
    private static void AssertListedWithDetails(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.Equal("", vm.PublicIps);
        Assert.Equal("", vm.Fqdns);
```

with:

```csharp
    /// <summary>
    /// <c>vm list -d</c>: the power state comes from the instance view, and the network fields from
    /// the NIC and public IP lookups. <c>vm create</c> attaches its default Standard public IP, so
    /// <c>PublicIps</c> is populated; it sets no DNS label, so <c>Fqdns</c> is an empty string, not
    /// null — null would mean the lookup never ran.
    /// </summary>
    private static void AssertListedWithDetails(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.False(string.IsNullOrEmpty(vm.PublicIps));
        Assert.Equal("", vm.Fqdns);
```

Leave the rest of the method, and `AssertListedWithStatus`, unchanged: `-Status` still never runs the network lookup, so its `PublicIps` stays null.

- [ ] **Step 3: Update the vm create paragraph in `tests/Scenario.Tests/README.md`**

Replace lines 54-59:

```markdown
The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: vnet, nsg, nic, and the vm with its polling, thirteen-odd entries in one file.
Before recording it live, the subscription needs the `Microsoft.Compute` and `Microsoft.Network`
providers registered and quota for one small vm. The subscription comes from the signed-in
profile. Afterwards delete the resource group the run created (`scenario-vm-rg`), which removes
the vm and all its network resources with it.
```

with:

```markdown
The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: vnet, nsg, the default public IP, nic, and the vm with its polling, then the detail
reads behind its output (the vm with `$expand=instanceView`, the nic, the public IP), twenty-odd
entries in one file. Before recording it live, the subscription needs the `Microsoft.Compute` and
`Microsoft.Network` providers registered and quota for one small vm and one Standard public IP.
The subscription comes from the signed-in profile. Afterwards delete the resource group the run
created (`scenario-vm-rg`), which removes the vm, its public IP and all its network resources with it.
```

- [ ] **Step 4: Confirm the two scenarios fail until they are re-recorded**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreateScenarioTests|FullyQualifiedName~VmLifecycleScenarioTests"`
Expected: FAIL. Playback finds no recorded entry for the `publicIPAddresses` requests. This is the red half of the cycle; Task 7 turns it green.

- [ ] **Step 5: Commit**

```bash
git add tests/Scenario.Tests/VmCreateScenarioTests.cs tests/Scenario.Tests/VmLifecycleScenarioTests.cs tests/Scenario.Tests/README.md
git commit -m "{Compute} Expect the default public IP and enriched output in the vm scenarios"
```

### Task 6: Cover the public IP modes in the create smoke scripts

**Files:**
- Modify: `tests/Scripts/CLI/VMCreate.sh` (whole file)
- Modify: `tests/Scripts/PS/VMCreate.ps1` (whole file)

**Interfaces:**
- Consumes: `--public-ip-address` / `-PublicIpAddressName` (Task 4); the enriched `publicIps` / `privateIps` output (Task 3); the missing-id error text (Task 2).
- Produces: two live smoke scripts. They follow the conventions of the newer `VMShow` / `VMList` scripts: `AZCLIPS_*` environment variables, their own resource group, and cleanup on exit. That replaces the hard-coded subscription and pre-existing resource group in the current `VMCreate` scripts. The create → show → delete → show flow the old scripts had is kept as case 5.

- [ ] **Step 1: Replace `tests/Scripts/CLI/VMCreate.sh`**

```bash
#!/usr/bin/env bash
# Smoke test for vm create's public IP workflows through the built CLI.
#
# Creates three VMs in one resource group, one per public IP mode, and checks the
# detail fields vm create returns:
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set
#   2. vm create --public-ip-address <new name>   -> creates that public IP; publicIps is set
#   3. vm create --public-ip-address ""           -> no public IP; publicIps is empty
#   4. vm create --public-ip-address <missing id> -> fails with "was not found" before
#                                                    anything is created
#   5. vm show / vm delete / vm show              -> the last show is expected to fail
#
# privateIps is checked alongside publicIps to prove the output was enriched: an
# empty publicIps with an empty privateIps means the detail lookup never ran.
#
# Reusing an existing public IP is not covered: an unattached public IP can't be
# created with this CLI.
#
# Prereqs: build the solution first with dotnet build, then sign in. The subscription
# needs quota for three Standard_D2s_v3 VMs and two Standard public IPs.
#
# Cleanup: on exit the resource group this script created is deleted, which removes
# the VMs, public IPs, NICs, VNets and NSGs with it. A group supplied through
# AZCLIPS_RESOURCE_GROUP may hold other resources, so the script only deletes its
# VMs there and leaves the rest to you.
#
# Usage: ./tests/Scripts/CLI/VMCreate.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmcreate-rg-$SUFFIX}"

# Only a group this script named itself is safe to delete.
OWNS_RESOURCE_GROUP=1
[[ -n "${AZCLIPS_RESOURCE_GROUP:-}" ]] && OWNS_RESOURCE_GROUP=0

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
DEFAULT_VM="vmc-def-$SUFFIX"
NAMED_VM="vmc-nam-$SUFFIX"
NONE_VM="vmc-non-$SUFFIX"
MISSING_VM="vmc-mis-$SUFFIX"
NAMED_PUBLIC_IP="azclips-vmcreate-pip-$SUFFIX"
MISSING_PUBLIC_IP_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$SUFFIX"

FAILURES=0

fail()
{
    echo "FAIL: $1"
    FAILURES=$((FAILURES + 1))
}

# Prints the value of a string field from a JSON document; empty when the field is absent.
json_field()
{
    sed -n "s/.*\"$1\": *\"\([^\"]*\)\".*/\1/p" <<< "$2" | head -n 1
}

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.enabled --value false

cleanup()
{
    if [[ "$OWNS_RESOURCE_GROUP" == "1" ]]
    then
        echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
        "$AZ" group delete --subscription-id "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" || true
    else
        for vm in "$DEFAULT_VM" "$NAMED_VM" "$NONE_VM"
        do
            echo "== cleanup: deleting VM $vm =="
            "$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$vm" || true
        done
        echo "== cleanup: kept resource group $RESOURCE_GROUP because AZCLIPS_RESOURCE_GROUP supplied it; remove its public IPs and network resources yourself =="
    fi
}
trap cleanup EXIT

# vm create with per-VM network resource names; extra arguments are appended.
create_vm()
{
    local name="$1"
    shift
    "$AZ" vm create \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$name" \
        --location "$LOCATION" \
        --vm-size Standard_D2s_v3 \
        --admin-username Artemis \
        --admin-password "$ADMIN_PASSWORD" \
        --network-interface-name "$name-nic" \
        --virtual-network-name "$name-vnet" \
        --address-prefixes 10.0.0.0/16 \
        --subnet-names "$name-subnet" \
        --subnet-address-prefixes 10.0.2.0/24 \
        --network-security-group-name "$name-nsg" \
        --output json \
        "$@"
}

# privateIps proves the detail lookup ran; then publicIps must be set ("yes") or empty ("no").
check_public_ip()
{
    local label="$1" output="$2" expect_public_ip="$3"
    local public_ips private_ips
    public_ips=$(json_field publicIps "$output")
    private_ips=$(json_field privateIps "$output")

    if [[ -z "$private_ips" ]]
    then
        fail "$label: privateIps is empty, so the output was not enriched"
    elif [[ "$expect_public_ip" == "yes" && -z "$public_ips" ]]
    then
        fail "$label: expected a public IP, got publicIps=''"
    elif [[ "$expect_public_ip" == "no" && -n "$public_ips" ]]
    then
        fail "$label: expected no public IP, got publicIps='$public_ips'"
    else
        echo "OK: $label: publicIps='$public_ips' privateIps='$private_ips'"
    fi
}

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes
else
    fail "default: vm create failed"
fi

echo "== 2. vm create --public-ip-address $NAMED_PUBLIC_IP (new name) =="
if output=$(create_vm "$NAMED_VM" --public-ip-address "$NAMED_PUBLIC_IP")
then
    check_public_ip "named" "$output" yes
else
    fail "named: vm create failed"
fi

echo "== 3. vm create --public-ip-address \"\" (no public IP) =="
if output=$(create_vm "$NONE_VM" --public-ip-address "")
then
    check_public_ip "none" "$output" no
else
    fail "none: vm create failed"
fi

echo "== 4. vm create --public-ip-address <missing resource id> (expect 'was not found') =="
if output=$(create_vm "$MISSING_VM" --public-ip-address "$MISSING_PUBLIC_IP_ID" 2>&1)
then
    echo "$output"
    fail "missing id: expected vm create to fail"
elif grep -qF "Public IP address '$MISSING_PUBLIC_IP_ID' was not found." <<< "$output"
then
    echo "OK: missing id: failed with the expected message"
else
    echo "$output"
    fail "missing id: vm create failed, but not with the expected message"
fi

echo "== 5. vm show, vm delete, vm show (the last show is expected to fail) =="
"$AZ" vm show --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" \
    || fail "vm show failed for $DEFAULT_VM"
"$AZ" vm delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" \
    || fail "vm delete failed for $DEFAULT_VM"
if "$AZ" vm show --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$DEFAULT_VM" >/dev/null 2>&1
then
    fail "vm show still finds $DEFAULT_VM after vm delete"
else
    echo "OK: $DEFAULT_VM no longer exists"
fi

if (( FAILURES > 0 ))
then
    echo "== vm create smoke test finished with $FAILURES failure(s) =="
    exit 1
fi
echo "== vm create smoke test finished =="
```

- [ ] **Step 2: Replace `tests/Scripts/PS/VMCreate.ps1`**

```powershell
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for New-AzVM's public IP workflows through the built Compute module.
#
# Creates three VMs in one resource group, one per public IP mode, and checks the
# detail fields New-AzVM returns:
#   1. New-AzVM                                    -> creates <vm>PublicIP; PublicIps is set
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName ''            -> no public IP; PublicIps is empty
#   4. New-AzVM -PublicIpAddressName <missing id>  -> fails with "was not found" before
#                                                     anything is created
#   5. Get-AzVM / Remove-AzVM / Get-AzVM           -> the last get is expected to fail
#
# PrivateIps is checked alongside PublicIps to prove the output was enriched: an
# empty PublicIps with an empty PrivateIps means the detail lookup never ran.
#
# Reusing an existing public IP is not covered: an unattached public IP can't be
# created with these modules.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
# The subscription needs quota for three Standard_D2s_v3 VMs and two Standard public IPs.
#
# Cleanup: on exit, whether the run passed or failed, the resource group this script
# created is deleted with Remove-AzResourceGroup, which removes the VMs, public IPs,
# NICs, VNets and NSGs with it. A group named through $env:AZCLIPS_RESOURCE_GROUP may
# already hold other resources, so the script only removes its VMs there.
#
# Usage: ./tests/Scripts/PS/VMCreate.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmcreate-rg-$suffix" }

# Azure caps the Windows computer name, derived from the VM name, at 15 characters.
$defaultVm         = "vmc-def-$suffix"
$namedVm           = "vmc-nam-$suffix"
$noneVm            = "vmc-non-$suffix"
$missingVm         = "vmc-mis-$suffix"
$namedPublicIp     = "azclips-vmcreate-pip-$suffix"
$missingPublicIpId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Network/publicIPAddresses/azclips-vmcreate-missing-pip-$suffix"

# Fail fast with an actionable message. Checking existence is not enough: the official
# Az modules (Az.Resources, Az.Compute) export the same cmdlet names and PowerShell
# auto-loads them on first use, so an un-imported azclips module silently resolves to
# the official one — which rejects -SubscriptionId and fails mid-script.
$requiredCommands = [ordered]@{
    'New-AzResourceGroup'    = 'Resources'
    'Remove-AzResourceGroup' = 'Resources'
    'New-AzVM'               = 'Compute'
    'Get-AzVM'               = 'Compute'
    'Remove-AzVM'            = 'Compute'
}

foreach ($requiredCommand in $requiredCommands.Keys)
{
    $expectedModule = $requiredCommands[$requiredCommand]
    $resolved = Get-Command $requiredCommand -ErrorAction SilentlyContinue

    if (-not $resolved)
    {
        throw "$requiredCommand is not available. Import the azclips modules first:`n" +
              "    Import-Module ./src/PS/Accounts/Accounts.psd1`n" +
              "    Import-Module ./src/PS/Resources/Resources.psd1`n" +
              "    Import-Module ./src/PS/Compute/Compute.psd1"
    }

    if ($resolved.ModuleName -ne $expectedModule)
    {
        throw "$requiredCommand resolves to module '$($resolved.ModuleName)' " +
              "(version $($resolved.Version)), not the azclips '$expectedModule' module.`n" +
              "The official Az modules shadow azclips unless you import it first:`n" +
              "    Import-Module ./src/PS/$expectedModule/$expectedModule.psd1`n" +
              "If the official Az module is already loaded in this session, start a fresh " +
              "pwsh session and import the azclips modules before running this script."
    }
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message)
{
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
}

# New-AzVM with per-VM network resource names; $Extra is splatted on top.
function New-SmokeVM([string]$Name, [hashtable]$Extra = @{})
{
    $parameters = @{
        SubscriptionId           = $subscriptionId
        ResourceGroupName        = $resourceGroup
        Name                     = $Name
        Location                 = $location
        VmSize                   = 'Standard_D2s_v3'
        AdminUsername            = 'Artemis'
        AdminPassword            = $env:AZCLIPS_VM_ADMIN_PASSWORD
        NetworkInterfaceName     = "$Name-nic"
        VirtualNetworkName       = "$Name-vnet"
        AddressPrefixes          = '10.0.0.0/16'
        SubnetNames              = "$Name-subnet"
        SubnetAddressPrefixes    = '10.0.2.0/24'
        NetworkSecurityGroupName = "$Name-nsg"
    }
    New-AzVM @parameters @Extra
}

# PrivateIps proves the detail lookup ran; then PublicIps must be set or empty as expected.
function Test-PublicIp([string]$Label, $Vm, [bool]$ExpectPublicIp)
{
    if ([string]::IsNullOrEmpty($Vm.PrivateIps))
    {
        Add-Failure "${Label}: PrivateIps is empty, so the output was not enriched"
    }
    elseif ($ExpectPublicIp -and [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected a public IP, got PublicIps=''"
    }
    elseif (-not $ExpectPublicIp -and -not [string]::IsNullOrEmpty($Vm.PublicIps))
    {
        Add-Failure "${Label}: expected no public IP, got PublicIps='$($Vm.PublicIps)'"
    }
    else
    {
        Write-Host "OK: ${Label}: PublicIps='$($Vm.PublicIps)' PrivateIps='$($Vm.PrivateIps)'"
    }
}

# Guard the cleanup block: each resource is only deleted once it exists.
$groupCreated = $false
$createdVms   = [System.Collections.Generic.List[string]]::new()

# Only a group this script named itself is safe to delete. One supplied through
# AZCLIPS_RESOURCE_GROUP may already hold other resources.
$ownsResourceGroup = -not $env:AZCLIPS_RESOURCE_GROUP

try
{
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Location $location
    $groupCreated = $true

    Write-Host "== 1. New-AzVM (default public IP ${defaultVm}PublicIP) =="
    $createdVms.Add($defaultVm)
    Test-PublicIp -Label 'default' -Vm (New-SmokeVM -Name $defaultVm) -ExpectPublicIp $true

    Write-Host "== 2. New-AzVM -PublicIpAddressName $namedPublicIp (new name) =="
    $createdVms.Add($namedVm)
    Test-PublicIp -Label 'named' -Vm (New-SmokeVM -Name $namedVm -Extra @{ PublicIpAddressName = $namedPublicIp }) -ExpectPublicIp $true

    Write-Host "== 3. New-AzVM -PublicIpAddressName '' (no public IP) =="
    $createdVms.Add($noneVm)
    Test-PublicIp -Label 'none' -Vm (New-SmokeVM -Name $noneVm -Extra @{ PublicIpAddressName = '' }) -ExpectPublicIp $false

    Write-Host "== 4. New-AzVM -PublicIpAddressName <missing resource id> (expect 'was not found') =="
    $expected = "Public IP address '$missingPublicIpId' was not found."
    try
    {
        $null = New-SmokeVM -Name $missingVm -Extra @{ PublicIpAddressName = $missingPublicIpId }
        Add-Failure 'missing id: expected New-AzVM to fail'
    }
    catch
    {
        if ("$_".Contains($expected) -or $_.Exception.Message.Contains($expected))
        {
            Write-Host 'OK: missing id: failed with the expected message'
        }
        else
        {
            Add-Failure "missing id: New-AzVM failed, but not with the expected message: $_"
        }
    }

    Write-Host '== 5. Get-AzVM, Remove-AzVM, Get-AzVM (the last get is expected to fail) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
    Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm -Force
    $null = $createdVms.Remove($defaultVm)
    try
    {
        $null = Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $defaultVm
        Add-Failure "Get-AzVM still finds $defaultVm after Remove-AzVM"
    }
    catch
    {
        Write-Host "OK: $defaultVm no longer exists"
    }

    if ($failures.Count -gt 0)
    {
        throw "New-AzVM smoke test finished with $($failures.Count) failure(s)."
    }
    Write-Host '== New-AzVM smoke test finished =='
}
finally
{
    # Cleanup failures are warnings, not throws: a throw here would replace the exception
    # that ended the run.
    if ($groupCreated -and $ownsResourceGroup)
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        try
        {
            $null = Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroup -Force
        }
        catch
        {
            Write-Warning ("Could not delete resource group ${resourceGroup}. Remove it with " +
                "'az group delete --name $resourceGroup --yes'. Error: $($_.Exception.Message)")
        }
    }
    elseif ($groupCreated)
    {
        foreach ($vm in $createdVms)
        {
            Write-Host "== cleanup: removing VM $vm =="
            try
            {
                Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vm -Force
            }
            catch
            {
                Write-Warning "Could not remove VM ${vm}. Error: $($_.Exception.Message)"
            }
        }
        Write-Host "== cleanup: kept resource group $resourceGroup because AZCLIPS_RESOURCE_GROUP supplied it; remove its public IPs and network resources yourself =="
    }
}
```

- [ ] **Step 3: Syntax-check both scripts**

Run (Git Bash or any bash): `bash -n tests/Scripts/CLI/VMCreate.sh`
Expected: no output, exit code 0.

Run (PowerShell):

```powershell
$errors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path tests/Scripts/PS/VMCreate.ps1).Path, [ref]$null, [ref]$errors)
$errors.Count
```

Expected: `0`.

Running them live needs credentials and quota. That happens in Task 7.

- [ ] **Step 4: Commit**

```bash
git add tests/Scripts/CLI/VMCreate.sh tests/Scripts/PS/VMCreate.ps1
git commit -m "{Compute} Cover the public IP modes in the vm create smoke scripts"
```

### Task 7: Re-record the live scenarios (user, with credentials)

**This task is run by the user.** It creates real Azure resources (VMs and Standard public IPs) and needs a signed-in azclips profile. An agent must not run it without the user's explicit go-ahead.

**Files:**
- Re-record: `tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json`
- Re-record: `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`

**Interfaces:**
- Consumes: Tasks 1–5 committed and built.
- Produces: recordings that close the red window. `VmCreateScriptScenarioTests` replays the vm create recording through its three scripts, so it goes green too.

- [ ] **Step 1: Build (Debug)**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`.

- [ ] **Step 2: Sign in with the azclips CLI**

Record mode reads the azclips profile store, not the classic Azure CLI's. Use a test subscription that has the `Microsoft.Compute` and `Microsoft.Network` providers registered, plus quota for two small VMs and two Standard public IPs.

```powershell
dotnet artifacts/Debug/Core.Cli/az.dll login --tenant-id <test-tenant> --subscription-id <test-subscription>
```

- [ ] **Step 3: Record both scenarios**

```powershell
$env:AZURE_TEST_MODE = "Record"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateScenarioTests
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmLifecycleScenarioTests
$env:AZURE_TEST_MODE = $null
```

Expected: both pass live and rewrite their recording files.

- [ ] **Step 4: Delete the resource groups the recordings created**

`vm delete` leaves public IPs, NICs, VNets and NSGs behind (as legacy does), so delete the groups:

```powershell
dotnet artifacts/Debug/Core.Cli/az.dll group delete --name scenario-vm-rg
dotnet artifacts/Debug/Core.Cli/az.dll group delete --name scenario-vm-lifecycle-rg
```

- [ ] **Step 5: Inspect the vm create recording**

```powershell
$j = Get-Content tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json -Raw | ConvertFrom-Json
$j.entries | ForEach-Object { "{0} {1} -> {2}" -f $_.Request.Method, $_.Request.Uri, $_.Response.StatusCode }
($j.entries | Where-Object { $_.Request.Method -eq 'PUT' -and $_.Request.Uri -match 'publicIPAddresses' }).Request.Body
($j.entries | Where-Object { $_.Request.Method -eq 'PUT' -and $_.Request.Uri -match 'networkInterfaces' }).Request.Body
```

Expected:
- A `PUT .../publicIPAddresses/scenario-vmPublicIP?api-version=2025-05-01` with **no** `GET` of that public IP before it (the default name is not looked up), and body `{"location":"westus2","sku":{"name":"Standard"},"properties":{"publicIPAllocationMethod":"Static"}}`.
- The NIC `PUT` body contains `"publicIPAddress":{"id":"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/publicIPAddresses/scenario-vmPublicIP"}` on `ipconfig0`.
- After the VM's polling: `GET .../virtualMachines/scenario-vm?api-version=2025-11-01&$expand=instanceView`, then a `GET` of the NIC and a `GET` of `scenario-vmPublicIP`.
- The subscription id is the placeholder, and the password appears only as `[redacted]`.

- [ ] **Step 6: Scan the recordings for secrets**

Run: `python devops/scripts/ci/scan_recordings.py --base origin/main` (use `python3` where that is the interpreter's name)
Expected: no findings. The public IP address in the recording is not a credential, and neither the sanitizers nor the scanner treat it as one.

- [ ] **Step 7: Replay the whole scenario suite**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj`
Expected: all pass. That includes `VmCreateScenarioTests` (both surfaces), `VmLifecycleScenarioTests`, and `VmCreateScriptScenarioTests`, whose bash case skips without a usable bash and whose module case needs the Debug artifacts.

- [ ] **Step 8: Run the smoke scripts live (recommended)**

```bash
AZCLIPS_SUBSCRIPTION_ID=<test-subscription> AZCLIPS_VM_ADMIN_PASSWORD='<strong-password>' ./tests/Scripts/CLI/VMCreate.sh
```

```powershell
$env:AZCLIPS_SUBSCRIPTION_ID = '<test-subscription>'
$env:AZCLIPS_VM_ADMIN_PASSWORD = '<strong-password>'
Import-Module ./src/PS/Accounts/Accounts.psd1
Import-Module ./src/PS/Resources/Resources.psd1
Import-Module ./src/PS/Compute/Compute.psd1
Connect-AzAccount
./tests/Scripts/PS/VMCreate.ps1
```

Expected: each script prints an `OK:` line for cases 1–5, ends with `smoke test finished`, and deletes its resource group on exit.

- [ ] **Step 9: Commit the recordings**

```bash
git add tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json
git commit -m "{Compute} Re-record the vm create and lifecycle scenarios with the default public IP"
```

---

### Task 8: Final verification

**Files:** none changed. If a step fails, fix it in the task that owns the file.

- [ ] **Step 1: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, with no new warnings in the files this plan touched.

- [ ] **Step 2: Run the affected test suites**

```powershell
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj
dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj
```

Expected: all pass.

- [ ] **Step 3: Validate help once more**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records.

- [ ] **Step 4: Smoke-run the benchmarks**

Use the `validate-benchmarks` skill. Expected: the `vm create` / `New-AzVM` benchmarks complete. The stub returns `{}` for public IP paths, which carries no `provisioningState`, so the public IP's long-running operation ends on its first poll and no id is attached.

- [ ] **Step 5: Review the branch's scope**

Run: `git --no-pager diff --stat jaysontaimicrosoft-vm-list-params...HEAD` (this worktree was created from that branch; substitute your base if it differs).
Expected: only the files in the File Structure table, the two recordings, and the spec and plan under `docs/superpowers/`.

---

## Spec coverage

| Spec requirement | Task |
|---|---|
| `--public-ip-address` / `-PublicIpAddressName` bound to `PublicIpAddress`, help text | 4 |
| Behaviour table: omitted → PUT `{vm}PublicIP` without lookup; `""`/whitespace → none; name → look up, then reuse or create; id → look up, reuse or fail | 2 |
| Look up explicit names and ids before anything is created; then VNet, NSG and public IP in parallel | 2 |
| New public IP: location, Standard, Static; primary ipconfig only | 1, 2 |
| Output enriched like `az vm show -d` on both surfaces | 3 |
| Enrichment failure → warning and un-enriched VM | 3 |
| Malformed id message, missing id message, 404-only special-casing | 2 |
| PowerShell `$null` treated as omitted | 4 |
| `VirtualMachine` synthetic-field docs | 3 |
| CLI help, PowerShell help, CLI export fixture | 4 |
| Scenario recordings re-recorded; script scenarios replay them | 5, 7 |
| Smoke scripts: default, new name, `""`, missing id | 6, 7 |
| `FakeVmArmBackend` maintenance, no new orchestration tests | 2 |
| Benchmark stub unchanged | 8 |
| `vm delete` / `Remove-AzVM` unchanged | — (no task touches them) |
