# `az vm show` / `Get-AzVM` Parameter Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--ids`, `--include-user-data`/`-u`, `--show-details`/`-d` to `az vm show`, and `-Id`, `-UserData` to `Get-AzVM`, driven by a single shared orchestrator.

**Architecture:** One new `GetVMProcessor_Orchestrator` inherits the existing `GetVMProcessor` and backs all three invocations (`vm show`, `Get-AzVM_Get`, `Get-AzVM_Id`). Every new flag reduces to either a single ARM `$expand` value or a resource-id fan-out. Client-specific naming lives entirely in `ExtensionIndex` metadata, so there is no per-client branching in code.

**Tech Stack:** C# 12 / .NET (SDK pinned in `global.json`), xUnit, `Scenario.Tests` record/playback harness with a fake ARM `HttpMessageHandler`, PowerShell binary cmdlets.

**Design spec:** `docs/superpowers/specs/2026-09-23-vm-show-parameter-migration-design.md`

## Global Constraints

- Build with `dotnet build azclips.slnx`. Use the .NET SDK pinned in `global.json`.
- Nullable reference types and implicit usings are enabled. `System.Linq` is available without an explicit `using`.
- Compute API version is `2025-11-01`; Network API version is `2025-05-01`. Do not change either.
- Compute orchestrators live in `src/Extensions/Compute/Customization/` but declare the namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors` (see `StartVMProcessor_Orchestrator.cs`).
- Every file starts with the two-line copyright header used throughout the repo:
  ```csharp
  // Copyright (c) Microsoft Corporation.
  // Licensed under the MIT License.
  ```
- All Compute model classes end with `public override string ToString() => JsonNode.FromObject(this).ToString();` and the file imports `Microsoft.Azure.Azclips.ExtensionRuntime.Json`.
- `CliOutputFormatter` does **not** suppress nulls. Every newly added model property that is absent on a plain call MUST carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, or plain `az vm show` output changes.
- `tests/Cli.Tests/ExtensionIndexContractTests.cs` enforces three invariants on every `ExtensionIndex` entry. Violating any of them fails the test run:
  1. Each `ParameterMetadata`'s first argument must name a settable property on `ProcessorType`.
  2. `CommandMetadata.FullName` must equal `ProcessorType.FullName` exactly.
  3. Every alias listed in `ParameterMetadata.Aliases` must also appear in `CommandMetadata.ParameterAliases` mapped to that same flavored parameter name.
- Scenario tests must be annotated `[Collection("Scenario Tests")]` — `ScenarioHost` mutates process-wide state and cannot run in parallel.
- Do not modify `ComputeProcessorBase`, `ListVMProcessor`, or any existing `SessionRecords/*.json` recording.

---

### Task 1: `--include-user-data` / `-UserData`

Delivers the first vertical slice: the `Expand` seam on `GetVMProcessor`, the shared orchestrator, and the CLI + PowerShell wiring. Also builds the fake ARM backend that Tasks 2 and 3 reuse.

**Files:**
- Modify: `src/Extensions/Compute/Processors/GetVMProcessor.cs`
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs`
- Create: `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs`
- Modify: `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`
- Test: `tests/Scenario.Tests/FakeVmShowArmBackend.cs` (create)
- Test: `tests/Scenario.Tests/VmShowOrchestrationTests.cs` (create)

**Interfaces:**
- Consumes: `ComputeProcessorBase.CreateExtendedProcessor<T>`, `ComputeProcessorBase<TRequest,TResponse>.ProcessAsync`, `GetVMProcessor`.
- Produces:
  - `GetVMProcessor.Expand` — `public string? Expand { get; set; }`. Appended to the request URI as `&$expand={Expand}` when non-blank.
  - `VirtualMachineProperties.UserData` — `public string? UserData { get; set; }`.
  - `GetVMProcessor_Orchestrator` — `public class GetVMProcessor_Orchestrator : GetVMProcessor` with `public bool IncludeUserData { get; set; }` and `private string? ResolveExpand()`. Tasks 2 and 3 add `Ids` and `ShowDetails` to this same class.
  - `FakeVmShowArmBackend` — `internal sealed class FakeVmShowArmBackend : HttpMessageHandler`, constructor `(string subscriptionId)`, `public List<string> Requests { get; }` recording every absolute request URI, and public constants `NicName`, `PublicIpName`, `PrivateIp`, `PublicIp`, `MacAddress`, `UserDataValue`.

---

- [ ] **Step 1: Write the fake ARM backend**

This is a test double, so it is written in full up front and reused by Tasks 2 and 3.

Create `tests/Scenario.Tests/FakeVmShowArmBackend.cs`:

```csharp
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
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Scenario.Tests/VmShowOrchestrationTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives `vm show` and `Get-AzVM` through the real dispatcher, binder and engine against a fake
/// ARM, so the orchestrator's expand resolution, id fan-out and detail enrichment are asserted
/// without a recording or credentials.
/// </summary>
[Collection("Scenario Tests")]
public class VmShowOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-show-rg";
    private const string VmName = "scenario-show-vm";

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Boots a host wired to a fresh fake ARM. The recorder is required by the constructor, but the
    /// assertions read the backend's captured requests, which are not sanitised.
    /// </summary>
    private static ScenarioHost NewHost(ClientType clientType, out FakeVmShowArmBackend backend)
    {
        backend = new FakeVmShowArmBackend(SubscriptionId);
        return new ScenarioHost(
            clientType,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backend,
            extensions: [ExtensionUnderTest.Compute]);
    }

    [Fact]
    public void PlainShow_SendsNoExpand_AndReturnsTheVirtualMachine()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName);

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Null(vm.Properties.UserData);
        Assert.DoesNotContain(backend.Requests, uri => uri.Contains("$expand", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludeUserData_ExpandsUserData_AndSurfacesIt()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "--include-user-data");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(FakeVmShowArmBackend.UserDataValue, vm.Properties.UserData);
        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }

    [Fact]
    public void IncludeUserData_ShortAlias_BindsTheSameWay()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "-u");

        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }

    [Fact]
    public void GetAzVM_UserData_ExpandsUserData()
    {
        using var host = NewHost(ClientType.PowerShell, out var backend);

        var outputs = host.RunPowerShell("Get-AzVM_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["UserData"] = true,
        }, extension: "Compute");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(FakeVmShowArmBackend.UserDataValue, vm.Properties.UserData);
        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }
}
```

`VmId` is unused in Task 1 and is consumed by Task 2. If the compiler warns about it, leave it — Task 2 uses it three steps later.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`

Expected: compile error — `VirtualMachineProperties` has no `UserData`. Once the model lands in Step 4, the remaining failure is `Unable to find parameter: include-user-data`.

- [ ] **Step 4: Add `UserData` to the model**

In `src/Extensions/Compute/Models/VirtualMachine.cs`, inside `VirtualMachineProperties`, add immediately below the existing `ProvisioningState` property:

```csharp
    // Returned only when the request asks for $expand=userData.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserData { get; set; }
```

The file already imports `System.Text.Json.Serialization`, so no new `using` is needed.

- [ ] **Step 5: Add the `Expand` seam to `GetVMProcessor`**

Replace the contents of `src/Extensions/Compute/Processors/GetVMProcessor.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class GetVMProcessor : ComputeProcessorBase<object, VirtualMachine>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// ARM <c>$expand</c> value. Set by <c>GetVMProcessor_Orchestrator</c> from the user-data and
    /// show-details switches; deliberately not registered in <c>ExtensionIndex</c>, so no client
    /// can pass a raw expand string.
    /// </summary>
    public string? Expand { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        var uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";
        return string.IsNullOrWhiteSpace(Expand) ? uri : $"{uri}&$expand={Expand}";
    }
}
```

- [ ] **Step 6: Create the orchestrator**

Create `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared read orchestrator behind <c>az vm show</c>, <c>Get-AzVM_Get</c> and
    /// <c>Get-AzVM_Id</c>. It owns the client-facing switches and reduces them to a single ARM
    /// <c>$expand</c>; <c>ExtensionIndex</c> decides which of them each client exposes.
    /// </summary>
    public class GetVMProcessor_Orchestrator : GetVMProcessor
    {
        public bool IncludeUserData { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = ResolveExpand();
            await base.ProcessAsync();
        }

        /// <summary>The one place the switches turn into a request. Mirrors legacy `az vm show`.</summary>
        private string? ResolveExpand() => IncludeUserData ? "userData" : null;
    }
}
```

- [ ] **Step 7: Repoint the CLI and PowerShell entries**

In `src/Extensions/Compute/ExtensionIndex.cs`, replace the whole `s_getVM_PS` declaration with:

```csharp
        private static readonly CommandMetadata s_getVM_PS = new(
            Name: "GetVMProcessor_Orchestrator",
            InvocationName: "Get-AzVM_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["UserData"]          = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
```

and replace the whole `s_getVM_CLI` declaration with:

```csharp
        private static readonly CommandMetadata s_getVM_CLI = new(
            Name: "GetVMProcessor_Orchestrator",
            InvocationName: "vm show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]   = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]              = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"]    = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["include-user-data"] = new("IncludeUserData", "include-user-data", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: ["u"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["u"] = "include-user-data",
            }
        );
```

`name` and `resource-group` stay `IsRequired: true` here; Task 2 relaxes them when `--ids` arrives.

- [ ] **Step 8: Add `-UserData` to the cmdlet**

In `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`, add after the existing `Name` property:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

and inside `ProcessRecord`, immediately after the `if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))` block and before `var parameterSetName = ParameterSetName;`, add:

```csharp
        if (UserData.IsPresent)
        {
            parameters.Add(nameof(UserData), true);
        }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`
Expected: PASS, 4 tests.

- [ ] **Step 10: Run the guard suites**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~ExtensionIndexContractTests"`
Expected: PASS — proves `IncludeUserData` resolves on the orchestrator, `FullName` matches `ProcessorType.FullName`, and the `u` alias is in both tables.

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests"`
Expected: PASS — proves plain `vm show` and `Get-AzVM_Get` still replay against the committed recording unchanged.

- [ ] **Step 11: Commit**

```bash
git add src/Extensions/Compute/Processors/GetVMProcessor.cs src/Extensions/Compute/Models/VirtualMachine.cs src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/GetVMCmdlet.cs tests/Scenario.Tests/FakeVmShowArmBackend.cs tests/Scenario.Tests/VmShowOrchestrationTests.cs
git commit -m "[Compute] az vm show / Get-AzVM: add --include-user-data and -UserData"
```

---

### Task 2: `--ids` / `-Id`

Adds resource-id addressing. The id resolver is generalised here so Task 3's network lookups can reuse it.

**Files:**
- Modify: `src/Extensions/Compute/Customization/VirtualMachineIdResolver.cs`
- Modify: `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs`
- Modify: `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`
- Test: `tests/Scenario.Tests/VmShowOrchestrationTests.cs` (append tests)

**Interfaces:**
- Consumes: `GetVMProcessor_Orchestrator` and `GetVMProcessor.Expand` from Task 1; `FakeVmShowArmBackend` and the `NewHost` helper from Task 1.
- Produces:
  - `VirtualMachineIdResolver.ParseResourceId(string id, string provider, string resourceType, string displayName, string nameToken)` returning `(string SubscriptionId, string ResourceGroupName, string Name)`. Task 3 calls this for NIC and public-IP ids.
  - `GetVMProcessor_Orchestrator.Ids` — `public string[]? Ids { get; set; }`.
  - `GetVMProcessor_Orchestrator.EnsureAddressable()` — private guard, throws `InvalidOperationException`.

**Behaviour decisions carried from the spec (do not "improve" them):**
- `--ids` always writes an array, even for one id, matching `ShowIdentityProcessor_Orchestrator`.
- When `--ids` is supplied together with `--name`/`--resource-group`, `Ids` wins silently, matching `StartVMProcessor_Orchestrator`. Do not add a mutual-exclusion error.

---

- [ ] **Step 1: Write the failing tests**

Append these to the `VmShowOrchestrationTests` class in `tests/Scenario.Tests/VmShowOrchestrationTests.cs`:

```csharp
    [Fact]
    public void Ids_FetchEachVirtualMachine_AndWriteAnArray()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show", "--ids", VmId("vm-one"), VmId("vm-two"));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(new[] { "vm-one", "vm-two" }, vms.Select(vm => vm.Name).ToArray());
        Assert.Equal(2, backend.Requests.Count);
    }

    [Fact]
    public void SingleId_StillWritesAnArray()
    {
        using var host = NewHost(ClientType.CLI, out _);

        var outputs = host.RunCli("vm", "show", "--ids", VmId(VmName));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(VmName, Assert.Single(vms).Name);
    }

    [Fact]
    public void Ids_CarryTheExpandToEveryFetch()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show", "--ids", VmId("vm-one"), VmId("vm-two"), "--include-user-data");

        Assert.Equal(2, backend.Requests.Count);
        Assert.All(backend.Requests, uri => Assert.True(uri.Contains("$expand=userData", StringComparison.Ordinal), uri));
    }

    [Fact]
    public void Ids_WinOverNameAndResourceGroup()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--resource-group", "ignored-rg",
            "--name", "ignored-vm",
            "--ids", VmId("vm-one"));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal("vm-one", Assert.Single(vms).Name);
        Assert.All(backend.Requests, uri => Assert.False(uri.Contains("ignored-vm", StringComparison.Ordinal), uri));
    }

    [Fact]
    public void NoIdsAndNoName_FailsBeforeAnyRequest()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            host.RunCli("vm", "show", "--subscription-id", SubscriptionId));

        Assert.Contains("resource id", exception.Message);
        Assert.Empty(backend.Requests);
    }

    [Fact]
    public void MalformedId_IsRejectedBeforeAnyRequest()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        Assert.Throws<InvalidOperationException>(() =>
            host.RunCli("vm", "show", "--ids", "/subscriptions/abc/not-a-virtual-machine"));

        Assert.Empty(backend.Requests);
    }

    [Fact]
    public void GetAzVM_Id_ResolvesThroughTheSharedResolver()
    {
        using var host = NewHost(ClientType.PowerShell, out var backend);

        var outputs = host.RunPowerShell("Get-AzVM_Id", new Dictionary<string, object>
        {
            ["Id"] = new[] { VmId(VmName) },
        }, extension: "Compute");

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(VmName, Assert.Single(vms).Name);
        Assert.Single(backend.Requests);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`
Expected: the seven new tests FAIL with `Unable to find parameter: ids` (CLI) and a command-lookup failure for `Get-AzVM_Id` (PowerShell). The four Task 1 tests still PASS.

- [ ] **Step 3: Generalise the id resolver**

Replace the contents of `src/Extensions/Compute/Customization/VirtualMachineIdResolver.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Parses ARM resource ids shaped
    /// <c>/subscriptions/{s}/resourceGroups/{g}/providers/{provider}/{type}/{name}</c>. Shared by
    /// --ids / -Id addressing and by the network lookups behind --show-details, so all of them
    /// validate identically and fail with the same wording.
    /// </summary>
    internal static class VirtualMachineIdResolver
    {
        internal static (string SubscriptionId, string ResourceGroupName, string Name) ParseVirtualMachineResourceId(string id)
            => ParseResourceId(id, "Microsoft.Compute", "virtualMachines", "Virtual machine", "vmName");

        internal static (string SubscriptionId, string ResourceGroupName, string Name) ParseResourceId(
            string id, string provider, string resourceType, string displayName, string nameToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException($"{displayName} resource id cannot be empty.");

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals(provider, StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals(resourceType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid {displayName.ToLowerInvariant()} resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/{provider}/{resourceType}/{{{nameToken}}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }
    }
}
```

The `ParseVirtualMachineResourceId` signature, return shape, and both exception messages are unchanged, so `StartVMProcessor_Orchestrator`, `PowerOffVMProcessor_Orchestrator` and `DeallocateVMProcessor_Orchestrator` are unaffected.

- [ ] **Step 4: Add the fan-out to the orchestrator**

Replace the contents of `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs` with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared read orchestrator behind <c>az vm show</c>, <c>Get-AzVM_Get</c> and
    /// <c>Get-AzVM_Id</c>. It owns the client-facing switches and reduces them to a single ARM
    /// <c>$expand</c> plus, when addressing by resource id, one GET per id.
    /// <c>ExtensionIndex</c> decides which switches each client exposes.
    /// </summary>
    public class GetVMProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }
        public bool IncludeUserData { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = ResolveExpand();

            if (Ids is null || Ids.Length == 0)
            {
                EnsureAddressable();
                await base.ProcessAsync();
                return;
            }

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                // One GET per id, aggregated into a single array write — the shape
                // ShowIdentityProcessor_Orchestrator established for --ids.
                var results = new List<VirtualMachine>();
                foreach (var id in Ids)
                {
                    var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(id);
                    using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                    {
                        p.SubscriptionId = vmId.SubscriptionId;
                        p.ResourceGroupName = vmId.ResourceGroupName;
                        p.Name = vmId.Name;
                        p.Expand = Expand;
                    }).SendAsync();

                    if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is { } vm)
                    {
                        results.Add(vm);
                    }
                }

                await WriteOutput(results.ToArray());
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>The one place the switches turn into a request. Mirrors legacy `az vm show`.</summary>
        private string? ResolveExpand() => IncludeUserData ? "userData" : null;

        /// <summary>
        /// ParameterMetadata cannot express "required unless --ids", so without this guard the
        /// binder accepts an empty name and ARM answers a confusing 404 for a malformed URL.
        /// </summary>
        private void EnsureAddressable()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName) || string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException(
                    "Specify the virtual machine by both name and resource group, or address it by resource id.");
            }
        }
    }
}
```

- [ ] **Step 5: Wire `--ids` into the CLI entry**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_getVM_CLI`, change `name` and `resource-group` to `IsRequired: false` and add the `ids` entry, so the `Parameters` block reads:

```csharp
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]   = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]              = new("Name", "name", typeof(string), "The name of the virtual machine.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]    = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ids"]               = new("Ids", "ids", typeof(string[]), "One or more virtual machine resource IDs. Provide either --ids or --name and --resource-group.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include-user-data"] = new("IncludeUserData", "include-user-data", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: ["u"], AllowedValues: []),
            },
```

Leave the `ParameterAliases` block from Task 1 unchanged.

- [ ] **Step 6: Add the `Get-AzVM_Id` PowerShell entry**

In `src/Extensions/Compute/ExtensionIndex.cs`, add this declaration directly below `s_getVM_PS`:

```csharp
        private static readonly CommandMetadata s_getVMById_PS = new(
            Name: "GetVMProcessor_Orchestrator",
            InvocationName: "Get-AzVM_Id",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Id"]             = new("Ids", "Id", typeof(string[]), "The resource ID of the virtual machine. Provide either -Id or -Name and -ResourceGroupName.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["UserData"]       = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
```

`Id` maps to the `Ids` property exactly as `Stop-AzVM` already does. Then add this line to `s_psTable`, directly after the `Get-AzVM_Get` entry:

```csharp
            ["Get-AzVM_Id"]                = s_getVMById_PS,
```

- [ ] **Step 7: Add the `Id` parameter set to the cmdlet**

In `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`:

Add `[Parameter(ParameterSetName = "Id", Mandatory = false)]` to the existing `SubscriptionId` attribute list, so it reads:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";
```

Add the new parameter directly after the existing `Name` property:

```csharp
    [Parameter(ParameterSetName = "Id", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Id { get; set; } = "";
```

Extend the `UserData` switch added in Task 1 to the new set:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

And inside `ProcessRecord`, directly above the `if (UserData.IsPresent)` block added in Task 1, add:

```csharp
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Id)))
        {
            // The processor addresses VMs by id array; -Id is the single-target PowerShell spelling of it.
            parameters.Add(nameof(Id), new[] { Id });
        }
```

`commandName` is already built as `$"Get-AzVM_{parameterSetName}"`, so the new set routes to `Get-AzVM_Id` with no further change.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`
Expected: PASS, 11 tests.

- [ ] **Step 9: Run the guard suites**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~ExtensionIndexContractTests"`
Expected: PASS — proves `Ids` resolves from both the `ids` and `Id` spellings.

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests"`
Expected: PASS — the committed recording still replays.

- [ ] **Step 10: Commit**

```bash
git add src/Extensions/Compute/Customization/VirtualMachineIdResolver.cs src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/GetVMCmdlet.cs tests/Scenario.Tests/VmShowOrchestrationTests.cs
git commit -m "[Compute] az vm show / Get-AzVM: add --ids and -Id resource-id addressing"
```

---

### Task 3: `--show-details` / `-d`

The only flag that issues extra calls. Adds the public-IP processor, the model fields the enrichment writes, and the enrichment itself.

**Files:**
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs`
- Modify: `src/Extensions/Compute/Models/NetworkInterface.cs`
- Create: `src/Extensions/Compute/Models/PublicIPAddress.cs`
- Create: `src/Extensions/Compute/Processors/GetPublicIPAddressProcessor.cs`
- Modify: `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs`
- Test: `tests/Scenario.Tests/VmShowOrchestrationTests.cs` (append tests)

**Interfaces:**
- Consumes: `GetVMProcessor_Orchestrator` (`Ids`, `IncludeUserData`, `ResolveExpand`) from Tasks 1–2; `VirtualMachineIdResolver.ParseResourceId` from Task 2; the existing `GetNetworkInterfaceProcessor`.
- Produces:
  - `VirtualMachine.PowerState`, `.PublicIps`, `.Fqdns`, `.PrivateIps`, `.MacAddresses` — all `public string? { get; set; }`.
  - `VirtualMachineProperties.InstanceView` — `public VirtualMachineInstanceView? { get; set; }`.
  - `VirtualMachineInstanceView` — `public List<InstanceViewStatus> Statuses { get; set; } = [];`
  - `InstanceViewStatus` — `public string? Code { get; set; }`, `public string? DisplayStatus { get; set; }`.
  - `NetworkInterfaceProperties.MacAddress` — `public string? { get; set; }`.
  - `IPConfigurationProperties.PrivateIPAddress` — `public string? { get; set; }`; `.PublicIPAddress` — `public ResourceReference? { get; set; }`.
  - `ResourceReference` — `public string Id { get; set; }`.
  - `PublicIPAddress` / `PublicIPAddressProperties` / `PublicIPAddressDnsSettings` models.
  - `GetPublicIPAddressProcessor : ComputeProcessorBase<object, PublicIPAddress>` with `SubscriptionId`, `Name`, `ResourceGroupName`.
  - `GetVMProcessor_Orchestrator.ShowDetails` — `public bool { get; set; }`.

---

- [ ] **Step 1: Write the failing tests**

Append these to the `VmShowOrchestrationTests` class in `tests/Scenario.Tests/VmShowOrchestrationTests.cs`:

```csharp
    [Fact]
    public void ShowDetails_ExpandsInstanceView_AndFlattensNetworkAndPowerState()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "--show-details");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal("VM running", vm.PowerState);
        Assert.Equal(FakeVmShowArmBackend.PrivateIp, vm.PrivateIps);
        Assert.Equal(FakeVmShowArmBackend.PublicIp, vm.PublicIps);
        Assert.Equal(FakeVmShowArmBackend.MacAddress, vm.MacAddresses);
        Assert.Equal($"{FakeVmShowArmBackend.PublicIpName}.westus2.cloudapp.azure.com", vm.Fqdns);

        // The instance view is consumed for powerState, then dropped: legacy `del result['instanceView']`.
        Assert.Null(vm.Properties.InstanceView);

        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=instanceView", StringComparison.Ordinal));
        Assert.Contains(backend.Requests, uri => uri.Contains($"/networkInterfaces/{FakeVmShowArmBackend.NicName}", StringComparison.Ordinal));
        Assert.Contains(backend.Requests, uri => uri.Contains($"/publicIPAddresses/{FakeVmShowArmBackend.PublicIpName}", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowDetails_ShortAlias_BindsTheSameWay()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "-d");

        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=instanceView", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowDetailsWithUserData_ExpandsBoth()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "--show-details",
            "--include-user-data");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(FakeVmShowArmBackend.UserDataValue, vm.Properties.UserData);
        Assert.Equal("VM running", vm.PowerState);
        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=instanceView,userData", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowDetails_AppliesToEveryIdInAFanOut()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show", "--ids", VmId("vm-one"), VmId("vm-two"), "--show-details");

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(2, vms.Length);
        Assert.All(vms, vm => Assert.Equal("VM running", vm.PowerState));
        Assert.All(vms, vm => Assert.Null(vm.Properties.InstanceView));

        // Two VM GETs, two NIC GETs, two public-IP GETs.
        Assert.Equal(6, backend.Requests.Count);
    }

    [Fact]
    public void PlainShow_IssuesExactlyOneRequest()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName);

        Assert.Single(backend.Requests);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`
Expected: compile error — `VirtualMachine` has no `PowerState`. The 11 earlier tests are unaffected once the models land.

- [ ] **Step 3: Add the virtual-machine model fields**

In `src/Extensions/Compute/Models/VirtualMachine.cs`, replace the `VirtualMachine` class with:

```csharp
public class VirtualMachine
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public VirtualMachineProperties Properties { get; set; } = null!;

    // Synthetic fields computed by --show-details rather than returned by ARM. They sit outside
    // Properties so the ARM-sourced payload stays untouched, matching legacy `az vm show -d`.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PowerState { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdns { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddresses { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

Inside `VirtualMachineProperties`, add below the `UserData` property added in Task 1:

```csharp
    // Returned only with $expand=instanceView; cleared once --show-details has read the power state.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualMachineInstanceView? InstanceView { get; set; }
```

And append these two classes at the end of the same file:

```csharp
public class VirtualMachineInstanceView
{
    public List<InstanceViewStatus> Statuses { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class InstanceViewStatus
{
    public string? Code { get; set; }
    public string? DisplayStatus { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

- [ ] **Step 4: Add the network-interface model fields**

In `src/Extensions/Compute/Models/NetworkInterface.cs`, add the serialization import below the existing `using`:

```csharp
using System.Text.Json.Serialization;
```

Add to `NetworkInterfaceProperties`, above its `ToString`:

```csharp
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddress { get; set; }
```

Add to `IPConfigurationProperties`, above its `ToString`:

```csharp
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIPAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceReference? PublicIPAddress { get; set; }
```

And append this class at the end of the same file:

```csharp
/// <summary>An ARM sub-resource reference: the service returns only the id.</summary>
public class ResourceReference
{
    public string Id { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

- [ ] **Step 5: Add the public-IP model**

Create `src/Extensions/Compute/Models/PublicIPAddress.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class PublicIPAddress
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public PublicIPAddressProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressProperties
{
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

`JsonOptions` sets `PropertyNameCaseInsensitive = true`, so `IpAddress` binds ARM's `ipAddress`.

- [ ] **Step 6: Add the public-IP processor**

Create `src/Extensions/Compute/Processors/GetPublicIPAddressProcessor.cs`, mirroring `GetNetworkInterfaceProcessor`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class GetPublicIPAddressProcessor : ComputeProcessorBase<object, PublicIPAddress>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{Name}?api-version=2025-05-01";
}
```

- [ ] **Step 7: Add the enrichment to the orchestrator**

In `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`:

Add the property beside the existing ones:

```csharp
        public bool ShowDetails { get; set; }
```

Replace `ResolveExpand` with the full matrix:

```csharp
        /// <summary>
        /// The one place the switches turn into a request. Mirrors legacy `az vm show` exactly:
        /// --show-details needs the instance view for the power state, --include-user-data needs
        /// userData, and both together ask for one comma-joined expand.
        /// </summary>
        private string? ResolveExpand() => (ShowDetails, IncludeUserData) switch
        {
            (true, true) => "instanceView,userData",
            (true, false) => "instanceView",
            (false, true) => "userData",
            _ => null,
        };
```

Add this override so the single-VM path (which runs through `base.ProcessAsync`) is enriched before the base writes it:

```csharp
        /// <summary>
        /// The base pipeline deserializes then writes. This is the seam that lets --show-details
        /// decorate the model in between; returning false leaves the write to the base.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachine? body)
        {
            if (ShowDetails && body is not null)
            {
                await EnrichAsync(body);
            }
            return false;
        }
```

In the `Ids` loop, enrich each result — change the block that adds to `results` to:

```csharp
                    if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is { } vm)
                    {
                        if (ShowDetails)
                        {
                            await EnrichAsync(vm);
                        }
                        results.Add(vm);
                    }
```

And add the enrichment method itself:

```csharp
        /// <summary>
        /// Resolves every NIC and public IP the VM references, flattens them into the comma-joined
        /// summary fields legacy `az vm show -d` produces, then drops the instance view.
        /// </summary>
        private async Task EnrichAsync(VirtualMachine vm)
        {
            var privateIps = new List<string>();
            var publicIps = new List<string>();
            var fqdns = new List<string>();
            var macAddresses = new List<string>();

            foreach (var nicReference in vm.Properties?.NetworkProfile?.NetworkInterfaces ?? [])
            {
                var nicId = VirtualMachineIdResolver.ParseResourceId(
                    nicReference.Id, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");

                using HttpResponseMessage nicResponse = await CreateExtendedProcessor<GetNetworkInterfaceProcessor>(p =>
                {
                    p.SubscriptionId = nicId.SubscriptionId;
                    p.ResourceGroupName = nicId.ResourceGroupName;
                    p.Name = nicId.Name;
                }).SendAsync();

                if (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions) is not { } nic)
                {
                    continue;
                }

                Collect(macAddresses, nic.Properties?.MacAddress);

                foreach (var ipConfiguration in nic.Properties?.IPConfigurations ?? [])
                {
                    Collect(privateIps, ipConfiguration.Properties?.PrivateIPAddress);

                    if (ipConfiguration.Properties?.PublicIPAddress?.Id is not { } publicIpReference)
                    {
                        continue;
                    }

                    var publicIpId = VirtualMachineIdResolver.ParseResourceId(
                        publicIpReference, "Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");

                    using HttpResponseMessage publicIpResponse = await CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
                    {
                        p.SubscriptionId = publicIpId.SubscriptionId;
                        p.ResourceGroupName = publicIpId.ResourceGroupName;
                        p.Name = publicIpId.Name;
                    }).SendAsync();

                    if (await publicIpResponse.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions) is not { } publicIp)
                    {
                        continue;
                    }

                    Collect(publicIps, publicIp.Properties?.IpAddress);
                    Collect(fqdns, publicIp.Properties?.DnsSettings?.Fqdn);
                }
            }

            vm.PowerState = string.Join(",", (vm.Properties?.InstanceView?.Statuses ?? [])
                .Where(status => status.Code?.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(status => status.DisplayStatus)
                .Where(displayStatus => !string.IsNullOrWhiteSpace(displayStatus)));

            vm.PublicIps = string.Join(",", publicIps);
            vm.Fqdns = string.Join(",", fqdns);
            vm.PrivateIps = string.Join(",", privateIps);
            vm.MacAddresses = string.Join(",", macAddresses);

            // Legacy drops the instance view once the power state is flattened out of it.
            if (vm.Properties is not null)
            {
                vm.Properties.InstanceView = null;
            }

            // Blank entries are skipped so a VM without a public IP yields "" rather than ",".
            static void Collect(List<string> target, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }
```

- [ ] **Step 8: Expose `--show-details` on the CLI**

In `src/Extensions/Compute/ExtensionIndex.cs`, add to the `s_getVM_CLI` `Parameters` block, after `include-user-data`:

```csharp
                ["show-details"]      = new("ShowDetails", "show-details", typeof(bool), "Show public ip address, FQDN, and power states. Command will run slow.", IsRequired: false, Aliases: ["d"], AllowedValues: []),
```

and add the alias to the same entry's `ParameterAliases`:

```csharp
                ["d"] = "show-details",
```

Do **not** add `ShowDetails` to `s_getVM_PS` or `s_getVMById_PS`. PowerShell intentionally does not expose it — the spec dropped `-Status`, and there is no PowerShell spelling for the detail view.

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmShowOrchestrationTests"`
Expected: PASS, 16 tests.

- [ ] **Step 10: Run the guard suites and a full build**

Run: `dotnet build azclips.slnx`
Expected: build succeeds with no new warnings.

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~ExtensionIndexContractTests"`
Expected: PASS — proves `ShowDetails` resolves and the `d` alias is in both tables.

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj`
Expected: PASS — the whole scenario suite, including the untouched `VmLifecycleScenarioTests` recording.

- [ ] **Step 11: Commit**

```bash
git add src/Extensions/Compute/Models/VirtualMachine.cs src/Extensions/Compute/Models/NetworkInterface.cs src/Extensions/Compute/Models/PublicIPAddress.cs src/Extensions/Compute/Processors/GetPublicIPAddressProcessor.cs src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs tests/Scenario.Tests/VmShowOrchestrationTests.cs
git commit -m "[Compute] az vm show: add --show-details with NIC and public-IP enrichment"
```

---

### Task 4: Documentation and smoke scripts

**Files:**
- Modify: `src/CLI/Help/Docs/vm/show.md`
- Modify: `src/PS/Compute/help/Get-AzVM.md`
- Create: `tests/Scripts/CLI/VMShow.sh`
- Create: `tests/Scripts/PS/VMShow.ps1`

**Interfaces:**
- Consumes: every flag shipped in Tasks 1–3. Produces no code other tasks depend on.

---

- [ ] **Step 1: Document the CLI flags**

In `src/CLI/Help/Docs/vm/show.md`, append these three sections after the existing "Show using shell variables" example. Keep the existing `### <title>` + fenced-block structure — `Azclips.Help.Tool`'s `MarkdownStructureValidator` enforces it.

````markdown
### Show several virtual machines by resource ID

```bash
az vm show --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```

`--ids` replaces `--name`, `--resource-group` and `--subscription-id`, and accepts more than one id. It always returns an array, one entry per id.

### Include the user data

```bash
az vm show --name myvm --resource-group myrg --include-user-data
```

Adds `properties.userData` to the result. Equivalent short form: `-u`.

### Show public IPs, FQDNs and power state

```bash
az vm show --name myvm --resource-group myrg --show-details
```

Fetches each network interface and public IP address the VM references, then adds `powerState`, `publicIps`, `fqdns`, `privateIps` and `macAddresses` to the result. This issues extra requests, so it is slower than a plain show. Equivalent short form: `-d`.
````

- [ ] **Step 2: Document the PowerShell parameters**

In `src/PS/Compute/help/Get-AzVM.md`:

Add this third syntax block to the `## SYNTAX` section, after the existing `### Get` block:

````markdown
### Id

```
Get-AzVM -Id <string> [-SubscriptionId <string>] [-UserData] [-Profile <string>]
 [<CommonParameters>]
```
````

Add this example to the `## EXAMPLES` section:

````markdown
### Get a VM by resource ID

Retrieve a virtual machine addressed by its ARM resource ID.

```powershell
Get-AzVM -Id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```
````

The `## PARAMETERS` section is alphabetical, so insert `-Id` **before** `-Name`:

````markdown
### -Id

The resource ID of the virtual machine. Provide either -Id or -Name and -ResourceGroupName.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Id
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```
````

and add `-UserData` **after** `-SubscriptionId`, as the last parameter section before `### CommonParameters`:

````markdown
### -UserData

Include the user data properties in the query result.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Id
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

Finally, add `-SubscriptionId`'s `Id` parameter set to its existing `ParameterSets` list:

````markdown
- Name: Id
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
````

- [ ] **Step 3: Write the CLI smoke script**

Create `tests/Scripts/CLI/VMShow.sh`, modelled on `tests/Scripts/CLI/VMStop.sh`:

```bash
#!/usr/bin/env bash
# Smoke test for the vm show command through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG rule), then exercises every
# show workflow:
#   1. vm show                      -> plain GET
#   2. vm show --include-user-data  -> GET ...?$expand=userData
#   3. vm show -u                   -> same, short form
#   4. vm show --show-details       -> GET ...?$expand=instanceView + NIC + public IP GETs
#   5. vm show -d                   -> same, short form
#   6. vm show -d -u                -> GET ...?$expand=instanceView,userData
#   7. vm show --ids <id>           -> id-addressed show, returns an array
#
# NOTE: `vm create` in this repo does not allocate a public IP, so cases 4-6
# report empty publicIps and fqdns. privateIps, macAddresses and powerState are
# still populated, which is what this script verifies.
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Cleanup: the VM is always deleted on exit. The NIC/VNet/NSG and the resource
# group are left behind because this CLI has no delete command for them; remove
# them with the system Azure CLI:
#     az group delete --name "$RESOURCE_GROUP" --yes
# Set AZCLIPS_DELETE_GROUP=1 to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/CLI/VMShow.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmshow-rg-$SUFFIX}"
VM_NAME="azclips-vmshow-vm-$SUFFIX"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"

VM_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachines/$VM_NAME"

# Run unattended: disable the interactive agent so a command error surfaces a classic
# non-zero exit instead of handing off to the agent-mode TUI, which would block on input.
"$AZ" config set --key agent.enabled --value false

cleanup()
{
    echo "== cleanup: deleting VM $VM_NAME =="
    "$AZ" vm delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$VM_NAME" || true

    if [[ "${AZCLIPS_DELETE_GROUP:-0}" == "1" ]] && command -v az >/dev/null 2>&1
    then
        echo "== cleanup: deleting resource group $RESOURCE_GROUP =="
        az group delete --subscription "$SUBSCRIPTION_ID" --name "$RESOURCE_GROUP" --yes --no-wait || true
    else
        echo "== cleanup: remove leftover network resources with: az group delete --name $RESOURCE_GROUP --yes =="
    fi
}
trap cleanup EXIT

"$AZ" group create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION"

"$AZ" vm create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --location "$LOCATION" \
    --vm-size Standard_D2s_v3 \
    --admin-username Artemis \
    --admin-password "$ADMIN_PASSWORD" \
    --network-interface-name "azclips-vmshow-nic-$SUFFIX" \
    --virtual-network-name "azclips-vmshow-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmshow-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "azclips-vmshow-nsg-$SUFFIX"

echo "== 1. vm show (plain) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME"

echo "== 2. vm show --include-user-data =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --include-user-data

echo "== 3. vm show -u (short form) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -u

echo "== 4. vm show --show-details (expect powerState, privateIps, macAddresses) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --show-details

echo "== 5. vm show -d (short form) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -d

echo "== 6. vm show -d -u (both expands) =="
"$AZ" vm show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    -d -u

echo "== 7. vm show --ids (resource-id addressing, returns an array) =="
"$AZ" vm show --ids "$VM_ID"

echo "== vm show smoke test finished =="
```

- [ ] **Step 4: Write the PowerShell smoke script**

Create `tests/Scripts/PS/VMShow.ps1`, modelled on `tests/Scripts/PS/VMStop.ps1`:

```powershell
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for Get-AzVM through the built Compute module.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# Get-AzVM workflow:
#   1. Get-AzVM -ResourceGroupName -Name   -> plain GET
#   2. Get-AzVM -UserData                  -> GET ...?$expand=userData
#   3. Get-AzVM -Id                        -> id-addressed get
#   4. Get-AzVM -Id -UserData              -> id-addressed get with expand
#   5. Get-AzVM -ResourceGroupName         -> list in the resource group
#
# -ShowDetails has no PowerShell spelling: the design dropped -Status and did not
# add a PowerShell detail view, so the CLI's --show-details is CLI-only.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/PS/VMShow.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmshow-rg-$suffix" }
$vmName        = "azclips-vmshow-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

$vmId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Compute/virtualMachines/$vmName"

try
{
    New-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $resourceGroup `
        -Location $location

    New-AzVM `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $vmName `
        -Location $location `
        -VmSize Standard_D2s_v3 `
        -AdminUsername Artemis `
        -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
        -NetworkInterfaceName "azclips-vmshow-nic-$suffix" `
        -VirtualNetworkName "azclips-vmshow-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmshow-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -NetworkSecurityGroupName "azclips-vmshow-nsg-$suffix"

    Write-Host '== 1. Get-AzVM -ResourceGroupName -Name (plain) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName

    Write-Host '== 2. Get-AzVM -UserData =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName -UserData

    Write-Host '== 3. Get-AzVM -Id (resource-id addressing) =='
    Get-AzVM -Id $vmId

    Write-Host '== 4. Get-AzVM -Id -UserData =='
    Get-AzVM -Id $vmId -UserData

    Write-Host '== 5. Get-AzVM -ResourceGroupName (list) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup

    Write-Host '== Get-AzVM smoke test finished =='
}
finally
{
    Write-Host "== cleanup: removing VM $vmName =="
    Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName

    if ($env:AZCLIPS_DELETE_GROUP -eq '1' -and (Get-Command az -ErrorAction SilentlyContinue))
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        az group delete --subscription $subscriptionId --name $resourceGroup --yes --no-wait
    }
    else
    {
        Write-Host "== cleanup: remove leftover network resources with: az group delete --name $resourceGroup --yes =="
    }
}
```

- [ ] **Step 5: Verify the help tooling still accepts the docs**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj`
Expected: PASS — `MarkdownStructureValidatorTests` accepts the new sections.

- [ ] **Step 6: Run the full regression**

Run: `dotnet build azclips.slnx`
Expected: build succeeds, no new warnings.

Run each of these; all must PASS:
```
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj
```

- [ ] **Step 7: Commit**

```bash
git add src/CLI/Help/Docs/vm/show.md src/PS/Compute/help/Get-AzVM.md tests/Scripts/CLI/VMShow.sh tests/Scripts/PS/VMShow.ps1
git commit -m "[Compute] az vm show / Get-AzVM: document new parameters and add smoke scripts"
```

---

## Coverage map

Every requirement in the spec, and the task that implements it:

| Spec requirement | Task |
|---|---|
| `--ids` on `az vm show` | 2 |
| `--include-user-data` / `-u` | 1 |
| `--show-details` / `-d` | 3 |
| `-Id` on `Get-AzVM` (single string, wrapped as `string[]`) | 2 |
| `-UserData` on `Get-AzVM` | 1 |
| `Expand` seam on `GetVMProcessor`, not client-exposed | 1 |
| Generalised `ParseResourceId`, four call sites | 2 (resolver), 3 (NIC + public IP callers) |
| `GetPublicIPAddressProcessor` + `PublicIPAddress` model | 3 |
| `VirtualMachineProperties.UserData` | 1 |
| `VirtualMachineProperties.InstanceView` + five synthetic detail fields | 3 |
| `NetworkInterface` `MacAddress` / `PrivateIPAddress` / `PublicIPAddress` | 3 |
| `GetVMProcessor_Orchestrator` shared by CLI and both PS sets | 1 (created), 2 (`Get-AzVM_Id` added) |
| `ListVMProcessor` / `Get-AzVM_List` untouched | all — never edited |
| Plain output byte-identical (null-suppressing attributes) | 1, 3 |
| `--ids` always writes an array | 2 |
| `Ids` wins silently over name/resource-group | 2 |
| Guard when neither ids nor name/resource-group supplied | 2 |
| Enrichment failures propagate (no swallow) | 3 — no try/catch in `EnrichAsync` |
| Empty-string detail fields when there is no public IP | 3 — `Collect` skips blanks |
| API versions unchanged (`2025-11-01` / `2025-05-01`) | 1, 3 |
| Docs for both surfaces | 4 |
| Smoke scripts for both surfaces | 4 |
