# `az vm create --nsg` / `New-AzVM -SecurityGroupName` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `az vm create --network-security-group-name` with `--nsg` and `New-AzVM -NetworkSecurityGroupName` with `-SecurityGroupName`, with legacy Azure CLI semantics (`{vm}NSG` by default, reuse by name or resource ID, `""` for none). A new NSG gets legacy CLI's default rule for the image's OS, and the nine `--rule-*` / `-Rule*` parameters are removed.

**Architecture:** Both surfaces bind one orchestrator property, `NewVMProcessor_Orchestrator.NetworkSecurityGroup`. The NIC orchestrator resolves it, and the public IP, through a new shared `NewOrExistingResourceResolver`, NSG first. It looks up explicit names and IDs before anything is created, then creates the VNet, and the NSG and public IP when they're new, in parallel. A new NSG carries `NetworkSecurityGroupDefaultRule.For(osType)`. The VM orchestrator passes `Windows`, the OS of the only image vm create deploys.

**Tech Stack:** C# with nullable enabled on the .NET SDK pinned in `global.json`; xUnit scenario tests with record/playback against a fake ARM; PowerShell 7 with `Microsoft.PowerShell.PlatyPS` 1.0.1 for PowerShell help; bash and PowerShell smoke scripts.

**Spec:** `docs/superpowers/specs/2026-09-30-vm-create-nsg-design.md`

## Global Constraints

- Network api-version is `2025-05-01`; Compute api-version is `2025-11-01`.
- Default NSG name: the VM name followed by the literal `NSG` — `$"{Name}NSG"`. Default public IP name stays `$"{Name}PublicIP"`.
- Help text for both `--nsg` and `-SecurityGroupName`, verbatim: `Name of the network security group to create (default: '{vm}NSG') or of an existing one to attach. Can also reference an existing NSG by resource ID, or specify "" for none.`
- Missing resource ID errors, verbatim: `Network security group '<value>' was not found.` and, unchanged, `Public IP address '<value>' was not found.`
- Malformed NSG resource ID error, verbatim: `Invalid network security group resource id '<value>'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{networkSecurityGroupName}.`
- Default rule: OS type `Windows` in any casing gives name `rdp` and destination port `3389`; anything else gives `default-allow-ssh` and `22`. Both are `Inbound`, `Tcp`, `Allow`, priority `1000`, source address `*`, source port `*`, destination address `*`.
- The VM orchestrator passes the OS type `Windows`. There is no `--image` yet.
- Lookup order: the NSG, then the public IP, both before any `PUT`. Only a 404 means "create"; any other lookup failure surfaces.
- Do not modify `NewNetworkSecurityGroupProcessor` or the dormant `vm nsg create` metadata (`s_newNetworkSecurityGroup_CLI`).
- Compute orchestrators and helpers live under `src/Extensions/Compute/Customization/` but declare the `Microsoft.Azure.Azclips.Extensions.Compute.Processors` namespace.
- `src` builds with `TreatWarningsAsErrors=true`: product changes must add no warnings.
- Build with `dotnet build azclips.slnx`. If the Core.Agent npm restore fails, use the `building-the-solution` skill.
- Commit prefixes: `[Compute]` for product-code changes, `{Compute}` for test-only and docs-only changes. End every commit message with the trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
- **Expected red windows.** Every other test stays green after every task.
  - From Task 4 until Task 7, `VmCreateScenarioTests`, `VmLifecycleScenarioTests` and `VmCreateScriptScenarioTests` fail. Task 4 removes `--network-security-group-name`, which they pass. After Task 6 they fail playback on the `{vm}NSG` requests until Task 7 re-records.
  - From Task 4 until Task 5, the help validators (`build/build.ps1 -GenerateHelpArtifacts`, `build/Test-PSHelpMarkdown.ps1`) flag the removed `--network-security-group-name` / `-NetworkSecurityGroupName` in `vm/create.md` and `New-AzVM.md`.
  - The live smoke scripts under `tests/Scripts` pass removed parameters from Task 3 until Task 6 updates them. Don't run them live in between.

---

## File Structure

| File | Task | Responsibility |
|---|---|---|
| `tests/Scenario.Tests/FakeVmArmBackend.cs` | 1 | Fake ARM. Tracks NSGs and public IPs by ID, can be seeded with existing ones, and answers 404 for unknown ones. |
| `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | 1, 3, 4 | Fake-ARM orchestration tests: public IP modes (1), default rule (3), NSG modes (4). |
| `src/Extensions/Compute/Customization/NewOrExistingResourceResolver.cs` (new) | 2 | Legacy CLI's new / existing / none classification for the NSG and the public IP. |
| `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` | 2, 3, 4 | Resolves the NSG and public IP, creates what's new, then `PUT`s the NIC. |
| `src/Extensions/Compute/Customization/NetworkSecurityGroupDefaultRule.cs` (new) | 3 | Legacy CLI's default rule for an OS type. |
| `src/Extensions/Compute/Extensions.Compute.csproj` | 3 | `InternalsVisibleTo` the new unit-test project. |
| `tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`, `NetworkSecurityGroupDefaultRuleTests.cs` (new) | 3 | Unit tests for the default rule, including Linux, which no command reaches yet. |
| `azclips.slnx` | 3 | Registers the new test project. |
| `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` | 3, 4 | Image OS type (3); the NSG property and default name (4). |
| `src/Extensions/Compute/ExtensionIndex.cs` | 3, 4 | Drops `rule-*` / `Rule*` (3); registers `nsg` / `SecurityGroupName` (4). |
| `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` | 3, 4 | Drops `-Rule*` (3); `-SecurityGroupName` (4). |
| `src/Extensions/Compute/Models/NetworkInterface.cs` | 4 | Omits a null NSG reference from the NIC request. |
| `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` | 3, 4, 5 | Regenerated CLI export fixture. |
| `src/PS/Compute/help/New-AzVM.md` | 3, 5 | PowerShell help. |
| `src/CLI/Help/Docs/vm/create.md` | 5 | CLI help prose and examples. |
| `wiki/01-getting-started/powershell-usage.md` | 5 | The `New-AzVM` example. |
| `tests/Scenario.Tests/VmCreateScenarioTests.cs`, `VmLifecycleScenarioTests.cs`, `Scripts/vm-create.sh`, `Scripts/vm-create.ps1`, `Scripts/vm-create-module.ps1`, `README.md` | 6 | Move the live scenarios to the default NSG. |
| `tests/Benchmark/CLI/VirtualMachineBenchmarks.cs`, `tests/Benchmark/PS/VirtualMachineBenchmarks.cs` | 6 | Default NSG, same request count. |
| `tests/Scripts/CLI/VM{Create,Delete,List,Show,Stop}.sh`, `tests/Scripts/PS/VM{Create,Delete,List,Show,Stop}.ps1` | 6 | Rename to `--nsg` / `-SecurityGroupName`; drop the `--rule-*` / `-Rule*` arguments. |
| `tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json`, `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json` | 7 | Re-recorded live by the user. |

How the orchestration tests work: `ScenarioHost` runs real `vm create` / `New-AzVM` invocations through the dispatcher, binder and engine. A `RecordPlaybackInterceptor` in `Record` mode captures every request into a `SessionRecord`, and `FakeVmArmBackend` answers them. Each test then asserts on `record.Entries`: each entry's `Request.Method`, `Request.Uri` (full URI including `?api-version=`) and `Request.Body` (compact JSON). A processor exception, such as the resolver's "was not found", propagates out of `RunCli` / `RunPowerShell` unwrapped.

---

### Task 1: Pin the public IP modes before refactoring them

**Files:**
- Modify: `tests/Scenario.Tests/FakeVmArmBackend.cs` (whole file)
- Modify: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (two constants, four tests, four helpers)

**Interfaces:**
- Produces:
  - `FakeVmArmBackend(string subscriptionId, params string[] existingResourceIds)`. The IDs are NSG and public IP resource IDs that already exist. The fake builds NSG and public IP IDs from the request path.
  - In `VmCreateOrchestrationTests`:
    - `private static SessionRecord RecordCli(FakeVmArmBackend backend, string[] vmCreateArgs)`
    - `private static (SessionRecord Record, InvalidOperationException Error) RecordCliFailure(FakeVmArmBackend backend, string[] vmCreateArgs)`
    - `private static bool IsRequest(SessionRecordEntry entry, string method, string uriFragment)`
    - `private static string PutBody(SessionRecord record, string uriFragment)`
  - Constants `SharedPublicIpId` and `MissingPublicIpId`.

The public IP classification has no CI coverage today, and Task 2 refactors it. These tests pin today's behaviour first, so they **pass immediately**. If one fails, the test or the fake is wrong: fix that, not the product code.

- [ ] **Step 1: Replace `tests/Scenario.Tests/FakeVmArmBackend.cs`**

```csharp
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
```

The NSG branch now answers 404 for an unknown NSG `GET`. Nothing sends one until Task 4, so the existing tests are unaffected.

- [ ] **Step 2: Add the constants**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`, directly after `private const string Password = "PlaceholderP@ss1";`, add:

```csharp

    // A public IP the fake is seeded with, in the VM's resource group.
    private const string SharedPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/publicIPAddresses/shared-ip";

    // A well-formed id the fake has never heard of.
    private const string MissingPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip";
```

- [ ] **Step 3: Add the four public IP tests**

In the same file, insert these tests directly before `private static void AssertVm(IReadOnlyList<object> outputs)`:

```csharp
    [Fact]
    public void PublicIp_Omitted_CreatesTheDefaultWithoutALookup()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "GET", "/publicIPAddresses/"));
        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/scenario-vmPublicIP?"));
        Assert.Contains("/publicIPAddresses/scenario-vmPublicIP", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_EmptyString_AttachesNone()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", ""]);

        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/publicIPAddresses/"));
        Assert.DoesNotContain("publicIPAddress", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_ExistingName_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, SharedPublicIpId), [.. s_cliArgs, "--public-ip-address", "shared-ip"]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/publicIPAddresses/shared-ip?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/"));
        Assert.Contains(SharedPublicIpId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_MissingResourceId_FailsBeforeAnythingIsCreated()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", MissingPublicIpId]);

        Assert.Equal($"Public IP address '{MissingPublicIpId}' was not found.", error.Message);
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip?"));

        // Every network and VM request carries /providers/; the resource group create does not.
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

```

Each `?` in a URI fragment anchors the name to the end of the path, so `scenario-vmPublicIP?` can't match a longer name.

- [ ] **Step 4: Add the helpers**

In the same file, directly after the closing brace of `AssertVm`, add:

```csharp

    /// <summary>Runs group create then vm create against <paramref name="backend"/>, recording both.</summary>
    private static SessionRecord RecordCli(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(vmCreateArgs));
        }
        return record;
    }

    /// <summary>Like <see cref="RecordCli"/>, for a vm create that must fail; returns what it sent and the error.</summary>
    private static (SessionRecord Record, InvalidOperationException Error) RecordCliFailure(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        var error = Assert.Throws<InvalidOperationException>(() => host.RunCli(vmCreateArgs));
        return (record, error);
    }

    private static bool IsRequest(SessionRecordEntry entry, string method, string uriFragment)
        => entry.Request.Method == method && entry.Request.Uri.Contains(uriFragment);

    /// <summary>The body of the first PUT whose URI contains <paramref name="uriFragment"/>.</summary>
    private static string PutBody(SessionRecord record, string uriFragment)
        => record.Entries.First(e => IsRequest(e, "PUT", uriFragment)).Request.Body!;
```

`SessionRecord`, `SessionRecordEntry`, `RecordPlaybackInterceptor` and `RecorderMode` come from the file's existing `using Tests.Recording;`.

- [ ] **Step 5: Run the orchestration tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 9 passed, which is the 5 existing tests plus the 4 new ones.

- [ ] **Step 6: Commit**

```bash
git add tests/Scenario.Tests/FakeVmArmBackend.cs tests/Scenario.Tests/VmCreateOrchestrationTests.cs
git commit -m "{Compute} Pin vm create's public IP modes in the orchestration tests" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: Extract the new-or-existing resolver from the public IP code

**Files:**
- Create: `src/Extensions/Compute/Customization/NewOrExistingResourceResolver.cs`
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` (one call; delete `ClassifyPublicIpAddressAsync`)
- Test: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (Task 1's tests, **not edited**)

**Interfaces:**
- Consumes: `VirtualMachineIdResolver.ParseResourceId(string id, string provider, string resourceType, string displayName, string nameToken)`, which returns `(string SubscriptionId, string ResourceGroupName, string Name)`; the orchestrator's existing `FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)`, which returns `Task<string?>`.
- Produces:
  - `internal static class NewOrExistingResourceResolver` with:
    - `internal sealed record ResourceKind(string Provider, string ResourceType, string DisplayName, string NameToken)`
    - `internal static readonly ResourceKind NetworkSecurityGroup`
    - `internal static readonly ResourceKind PublicIpAddress`
    - `internal static Task<(string? ExistingId, string? NameToCreate)> ResolveAsync(string? value, string defaultName, string subscriptionId, string resourceGroupName, ResourceKind kind, Func<string, string, string, Task<string?>> findIdAsync)`

This is a behaviour-preserving refactor: the four public IP tests from Task 1 must stay green without edits.

- [ ] **Step 1: Create `src/Extensions/Compute/Customization/NewOrExistingResourceResolver.cs`**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Legacy Azure CLI's handling of a vm create argument that names a network resource to attach
/// or create (<c>--nsg</c>, <c>--public-ip-address</c>; <c>nsg_type</c> and
/// <c>public_ip_address_type</c> in azure-cli's <c>_validators.py</c>). Omitted means new with the
/// default name, <c>""</c> means none, a name is looked up in the VM's resource group and created
/// when missing, and a resource id must already exist.
/// </summary>
internal static class NewOrExistingResourceResolver
{
    /// <summary>What a resource id of this kind looks like, and how messages name it.</summary>
    internal sealed record ResourceKind(string Provider, string ResourceType, string DisplayName, string NameToken);

    internal static readonly ResourceKind NetworkSecurityGroup =
        new("Microsoft.Network", "networkSecurityGroups", "Network security group", "networkSecurityGroupName");

    internal static readonly ResourceKind PublicIpAddress =
        new("Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");

    /// <summary>
    /// Returns the id of an existing resource to attach, or the name of one to create; both are
    /// null when none is wanted. <paramref name="findIdAsync"/> takes a subscription, resource group
    /// and name, and returns the resource's id, or null when it does not exist.
    /// </summary>
    internal static async Task<(string? ExistingId, string? NameToCreate)> ResolveAsync(
        string? value,
        string defaultName,
        string subscriptionId,
        string resourceGroupName,
        ResourceKind kind,
        Func<string, string, string, Task<string?>> findIdAsync)
    {
        if (value is null)
        {
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                return (null, null);
            }
            return (null, defaultName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        if (value.Contains('/'))
        {
            var parsed = VirtualMachineIdResolver.ParseResourceId(
                value, kind.Provider, kind.ResourceType, kind.DisplayName, kind.NameToken);
            string existingId = await findIdAsync(parsed.SubscriptionId, parsed.ResourceGroupName, parsed.Name)
                ?? throw new InvalidOperationException($"{kind.DisplayName} '{value}' was not found.");
            return (existingId, null);
        }

        string? existingByName = await findIdAsync(subscriptionId, resourceGroupName, value);
        if (existingByName is not null)
        {
            return (existingByName, null);
        }
        return (null, value);
    }
}
```

The branches are those of today's `ClassifyPublicIpAddressAsync`, line for line. For `PublicIpAddress`, the `ParseResourceId` arguments and the "was not found" message produce exactly today's text.

- [ ] **Step 2: Call the resolver from the NIC orchestrator**

In `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`, replace:

```csharp
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await ClassifyPublicIpAddressAsync();
```

with:

```csharp
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            PublicIpAddress, DefaultPublicIpAddressName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.PublicIpAddress, FindPublicIpAddressIdAsync);
```

Keep the two comment lines above it.

- [ ] **Step 3: Delete `ClassifyPublicIpAddressAsync`**

In the same file, delete this whole method, including its doc comment and the blank line after it:

```csharp
    /// <summary>
    /// Determines the id of an existing public IP to attach, or the
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

```

`FindPublicIpAddressIdAsync` and `CreatePublicIpAddressAsync` stay as they are. `FindPublicIpAddressIdAsync` already has the `(string, string, string) -> Task<string?>` shape the resolver takes.

- [ ] **Step 4: Build the Compute extension**

Run: `dotnet build src/Extensions/Compute/Extensions.Compute.csproj`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 5: Run the orchestration tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 9 passed, unchanged from Task 1.

- [ ] **Step 6: Commit**

```bash
git add src/Extensions/Compute/Customization/NewOrExistingResourceResolver.cs src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs
git commit -m "[Compute] vm create: resolve the public IP through a shared new-or-existing resolver" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 3: Give a new NSG legacy CLI's default rule, and remove `--rule-*`

**Files:**
- Create: `src/Extensions/Compute/Customization/NetworkSecurityGroupDefaultRule.cs`
- Modify: `src/Extensions/Compute/Extensions.Compute.csproj` (`InternalsVisibleTo`)
- Create: `tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`
- Create: `tests/Extensions.Compute.Tests/NetworkSecurityGroupDefaultRuleTests.cs`
- Modify: `azclips.slnx` (one line)
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` (the nine `Rule*` properties give way to `OsType`; the NSG gets the default rule)
- Modify: `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` (drop the nine `Rule*` properties; pass the image OS type)
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (drop 27 entries)
- Modify: `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` (drop nine parameters and their forwarding)
- Modify: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (one test)
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
- Modify: `src/PS/Compute/help/New-AzVM.md` (SYNTAX; nine parameter blocks)

**Interfaces:**
- Consumes:
  - The `SecurityRule` model in `src/Extensions/Compute/Models/NetworkSecurityGroup.cs`: `string Name` and `SecurityRuleProperties Properties`.
  - `SecurityRuleProperties`: `string Protocol`, `SourcePortRange`, `DestinationPortRange`, `SourceAddressPrefix`, `DestinationAddressPrefix` and `Access`; `int Priority`; `string Direction`.
  - `NewNetworkSecurityGroupProcessor`'s parallel rule lists: `RuleNames`, `RuleDirections`, `RuleProtocols`, `RulePriorities` (`List<int>`), `RuleSourceAddressPrefixes`, `RuleSourcePortRanges`, `RuleDestinationPortRanges`, `RuleDestinationAddressPrefixes` and `RuleAccesses`. Index *i* of every list is rule *i*.
  - Task 1's helpers `RecordCli` and `PutBody`.
- Produces:
  - `internal static class NetworkSecurityGroupDefaultRule` with `internal static SecurityRule For(string osType)`.
  - `public string NewNetworkInterfaceProcessor_Orchestrator.OsType` (default `string.Empty`).
  - `NewVMProcessor_Orchestrator` passes `OsType = "Windows"` through `private const string ImageOsType`.
  - The nine `Rule*` properties are removed from both orchestrators. The matching parameters are removed from `vm create`, `New-AzVM` and the dormant `vm nic create-orchestrated` metadata.

- [ ] **Step 1: Create the unit-test project**

`Extensions.Compute` is delay-signed in Release, so its test friend must be too. This mirrors `tests/Extensions.Accounts.Tests/Extensions.Accounts.Tests.csproj`; `tests/Directory.Build.props` supplies the framework, xUnit and the implicit `using Xunit;`.

Create `tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <SignAssembly>true</SignAssembly>
    <DelaySign>true</DelaySign>
    <AssemblyOriginatorKeyFile>../../src/MSSharedLibKey.snk</AssemblyOriginatorKeyFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Extensions\Compute\Extensions.Compute.csproj" />
    <!-- The extension references these with ExcludeAssets=runtime, so the tests must bring them. -->
    <ProjectReference Include="..\..\src\Core\Core.Abstractions\Core.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\Extensions\ExtensionRuntime\ExtensionRuntime.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing unit tests**

Create `tests/Extensions.Compute.Tests/NetworkSecurityGroupDefaultRuleTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

/// <summary>
/// Unit tests for <c>NetworkSecurityGroupDefaultRule</c>: the inbound rule legacy Azure CLI gives a
/// network security group that vm create creates (azure-cli <c>custom.py</c> and
/// <c>_template_builder.py</c> <c>build_nsg_resource</c>). vm create only deploys Windows today, so
/// the Linux rule is pinned here rather than through a command.
/// </summary>
public class NetworkSecurityGroupDefaultRuleTests
{
    [Theory]
    [InlineData("Windows")]
    [InlineData("windows")]
    [InlineData("WINDOWS")]
    public void Windows_AllowsRdp(string osType)
    {
        SecurityRule rule = NetworkSecurityGroupDefaultRule.For(osType);

        Assert.Equal("rdp", rule.Name);
        Assert.Equal("3389", rule.Properties.DestinationPortRange);
        AssertInboundTcpAllowedFromAnywhere(rule.Properties);
    }

    [Theory]
    [InlineData("Linux")]
    [InlineData("linux")]
    public void Linux_AllowsSsh(string osType)
    {
        SecurityRule rule = NetworkSecurityGroupDefaultRule.For(osType);

        Assert.Equal("default-allow-ssh", rule.Name);
        Assert.Equal("22", rule.Properties.DestinationPortRange);
        AssertInboundTcpAllowedFromAnywhere(rule.Properties);
    }

    private static void AssertInboundTcpAllowedFromAnywhere(SecurityRuleProperties properties)
    {
        Assert.Equal("Inbound", properties.Direction);
        Assert.Equal("Tcp", properties.Protocol);
        Assert.Equal("Allow", properties.Access);
        Assert.Equal(1000, properties.Priority);
        Assert.Equal("*", properties.SourceAddressPrefix);
        Assert.Equal("*", properties.SourcePortRange);
        Assert.Equal("*", properties.DestinationAddressPrefix);
    }
}
```

- [ ] **Step 3: Register the project in the solution**

In `azclips.slnx`, directly after the line:

```xml
    <Project Path="tests/Extensions.Accounts.Tests/Extensions.Accounts.Tests.csproj" />
```

add:

```xml
    <Project Path="tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj" />
```

- [ ] **Step 4: Run the unit tests to see them fail**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`
Expected: the build fails with `error CS0103: The name 'NetworkSecurityGroupDefaultRule' does not exist in the current context`.

- [ ] **Step 5: Create `src/Extensions/Compute/Customization/NetworkSecurityGroupDefaultRule.cs`**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// The inbound rule legacy Azure CLI gives a network security group that vm create creates:
/// RDP for a Windows image and SSH for anything else (azure-cli <c>custom.py</c>, and
/// <c>build_nsg_resource</c> in <c>_template_builder.py</c>).
/// </summary>
internal static class NetworkSecurityGroupDefaultRule
{
    internal static SecurityRule For(string osType)
    {
        bool windows = string.Equals(osType, "Windows", StringComparison.OrdinalIgnoreCase);
        return new SecurityRule
        {
            Name = windows ? "rdp" : "default-allow-ssh",
            Properties = new SecurityRuleProperties
            {
                Protocol = "Tcp",
                SourcePortRange = "*",
                DestinationPortRange = windows ? "3389" : "22",
                SourceAddressPrefix = "*",
                DestinationAddressPrefix = "*",
                Access = "Allow",
                Priority = 1000,
                Direction = "Inbound",
            },
        };
    }
}
```

- [ ] **Step 6: Let the unit-test project see Compute's internals**

In `src/Extensions/Compute/Extensions.Compute.csproj`, directly after the line:

```xml
    <Import Project="$(MSBuildThisFileDirectory)..\..\..\Shared.props" />
```

add:

```xml

    <ItemGroup Condition="'$(Configuration)' == 'Release'">
        <InternalsVisibleTo Include="Extensions.Compute.Tests" Key="$(MSSharedLibPublicKey)" />
    </ItemGroup>
    <ItemGroup Condition="'$(Configuration)' != 'Release'">
        <InternalsVisibleTo Include="Extensions.Compute.Tests" />
    </ItemGroup>
```

It must come after the import, because `Shared.props` defines `$(MSSharedLibPublicKey)` for Release.

- [ ] **Step 7: Run the unit tests to see them pass**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`
Expected: 5 passed (three Windows cases, two Linux cases).

- [ ] **Step 8: Write the failing orchestration test**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`, insert this test directly before `private static void AssertVm(IReadOnlyList<object> outputs)`:

```csharp
    [Fact]
    public void NewNetworkSecurityGroup_GetsTheDefaultRdpRule()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        // vm create deploys a Windows image, so the new NSG gets legacy CLI's RDP rule and nothing else.
        Assert.Contains(
            "\"securityRules\":[{\"name\":\"rdp\",\"properties\":{\"protocol\":\"Tcp\",\"sourcePortRange\":\"*\",\"destinationPortRange\":\"3389\",\"sourceAddressPrefix\":\"*\",\"destinationAddressPrefix\":\"*\",\"access\":\"Allow\",\"priority\":1000,\"direction\":\"Inbound\"}}]",
            PutBody(record, "/networkSecurityGroups/"));
    }

```

The expected JSON follows `SecurityRuleProperties`' declaration order under the camel-case policy in `ComputeProcessorBase.JsonOptions`. The recorder keeps request bodies byte-for-byte; its sanitizers are regex replacements that don't touch this body.

- [ ] **Step 9: Run it to see it fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~NewNetworkSecurityGroup_GetsTheDefaultRdpRule`
Expected: FAIL with `Assert.Contains() Failure: Sub-string not found`. Today the NSG is `PUT` with `"securityRules":[]`.

- [ ] **Step 10: Build the NSG from the default rule in the NIC orchestrator**

In `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`:

(a) Delete these nine property lines:

```csharp
    public List<string> RuleNames { get; set; } = [];
    public List<string> RuleDirections { get; set; } = [];
    public List<string> RuleProtocols { get; set; } = [];
    public List<int> RulePriorities { get; set; } = [];
    public List<string> RuleSourceAddressPrefixes { get; set; } = [];
    public List<string> RuleSourcePortRanges { get; set; } = [];
    public List<string> RuleDestinationPortRanges { get; set; } = [];
    public List<string> RuleDestinationAddressPrefixes { get; set; } = [];
    public List<string> RuleAccesses { get; set; } = [];
```

(b) Directly after `public bool AcceleratedNetworking { get; set; }`, add:

```csharp

    /// <summary>OS type of the VM's image, which picks the default rule of a new NSG.</summary>
    public string OsType { get; set; } = string.Empty;
```

(c) Replace the NSG processor block:

```csharp
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
```

with:

```csharp
        SecurityRule defaultRule = NetworkSecurityGroupDefaultRule.For(OsType);
        NewNetworkSecurityGroupProcessor nsgProcessor = CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = NetworkSecurityGroupName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.RuleNames = [defaultRule.Name];
            p.RuleDirections = [defaultRule.Properties.Direction];
            p.RuleProtocols = [defaultRule.Properties.Protocol];
            p.RulePriorities = [defaultRule.Properties.Priority];
            p.RuleSourceAddressPrefixes = [defaultRule.Properties.SourceAddressPrefix];
            p.RuleSourcePortRanges = [defaultRule.Properties.SourcePortRange];
            p.RuleDestinationPortRanges = [defaultRule.Properties.DestinationPortRange];
            p.RuleDestinationAddressPrefixes = [defaultRule.Properties.DestinationAddressPrefix];
            p.RuleAccesses = [defaultRule.Properties.Access];
        });
```

`SecurityRule` resolves through the file's existing `using Microsoft.Azure.Azclips.Extensions.Compute.Models;`.

- [ ] **Step 11: Pass the image OS type from the VM orchestrator, and drop its `Rule*` properties**

In `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`:

(a) Directly after the class's opening brace (after `public class NewVMProcessor_Orchestrator : ComputeProcessorBase<VirtualMachine, VirtualMachine>` and `{`), add:

```csharp
        // The OS of the image vm create deploys: NewVMProcessor's default, Windows Server 2022.
        // --image will supply the chosen image's OS type instead.
        private const string ImageOsType = "Windows";

```

(b) Delete these nine properties, each with its attribute and the blank line after it:

```csharp
        [ParameterGroup("Network Arguments")]
        public List<string> RuleNames { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDirections { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleProtocols { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<int> RulePriorities { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourceAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleSourcePortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationPortRanges { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleDestinationAddressPrefixes { get; set; } = [];

        [ParameterGroup("Network Arguments")]
        public List<string> RuleAccesses { get; set; } = [];

```

(c) In `SendAsync`, delete these nine lines from the NIC orchestrator's configuration lambda:

```csharp
                p.RuleNames = RuleNames;
                p.RuleDirections = RuleDirections;
                p.RuleProtocols = RuleProtocols;
                p.RulePriorities = RulePriorities;
                p.RuleSourceAddressPrefixes = RuleSourceAddressPrefixes;
                p.RuleSourcePortRanges = RuleSourcePortRanges;
                p.RuleDestinationPortRanges = RuleDestinationPortRanges;
                p.RuleDestinationAddressPrefixes = RuleDestinationAddressPrefixes;
                p.RuleAccesses = RuleAccesses;
```

(d) In the same lambda, directly after `p.DefaultPublicIpAddressName = $"{Name}PublicIP";`, add:

```csharp
                p.OsType = ImageOsType;
```

- [ ] **Step 12: Remove the `rule-*` / `Rule*` parameters from the metadata**

In `src/Extensions/Compute/ExtensionIndex.cs`, delete these entries. Each is one line, and every other entry stays.

- In `s_newVMOrchestrator_PS` (`InvocationName: "New-AzVM"`), the entries keyed `["RuleNames"]`, `["RuleDirections"]`, `["RuleProtocols"]`, `["RulePriorities"]`, `["RuleSourceAddressPrefixes"]`, `["RuleSourcePortRanges"]`, `["RuleDestinationPortRanges"]`, `["RuleDestinationAddressPrefixes"]` and `["RuleAccesses"]`.
- In `s_newVMOrchestrator_CLI` (`InvocationName: "vm create"`), the entries keyed `["rule-names"]`, `["rule-directions"]`, `["rule-protocols"]`, `["rule-priorities"]`, `["rule-source-address-prefixes"]`, `["rule-source-port-ranges"]`, `["rule-destination-port-ranges"]`, `["rule-destination-address-prefixes"]` and `["rule-accesses"]`.
- In `s_newNetworkInterfaceOrchestrator_CLI` (`InvocationName: "vm nic create-orchestrated"`), the same nine `rule-*` keys. That dormant entry maps to the NIC orchestrator, whose `Rule*` properties no longer exist.

Leave `s_newNetworkSecurityGroup_CLI` (`InvocationName: "vm nsg create"`) untouched: it maps to `NewNetworkSecurityGroupProcessor`, which keeps its rule lists.

Check: `git grep -n -e '\["rule-' -e '\["Rule' -- src/Extensions/Compute/ExtensionIndex.cs`
Expected: exactly nine hits, all inside `s_newNetworkSecurityGroup_CLI`.

- [ ] **Step 13: Remove the `-Rule*` parameters from `New-AzVM`**

In `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`:

(a) Delete these nine parameters, each with its attribute and the blank line after it:

```csharp
    [Parameter(Mandatory = false)]
    public string[]? RuleNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDirections { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleProtocols { get; set; }

    [Parameter(Mandatory = false)]
    public int[]? RulePriorities { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourceAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourcePortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationPortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleAccesses { get; set; }

```

(b) In `ProcessRecord()`, delete these nine lines:

```csharp
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleNames)))                      { parameters.Add("RuleNames", RuleNames!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDirections)))                 { parameters.Add("RuleDirections", RuleDirections!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleProtocols)))                  { parameters.Add("RuleProtocols", RuleProtocols!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RulePriorities)))                 { parameters.Add("RulePriorities", RulePriorities!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourceAddressPrefixes)))      { parameters.Add("RuleSourceAddressPrefixes", RuleSourceAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourcePortRanges)))           { parameters.Add("RuleSourcePortRanges", RuleSourcePortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationPortRanges)))      { parameters.Add("RuleDestinationPortRanges", RuleDestinationPortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationAddressPrefixes))) { parameters.Add("RuleDestinationAddressPrefixes", RuleDestinationAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleAccesses)))                   { parameters.Add("RuleAccesses", RuleAccesses!); }
```

- [ ] **Step 14: Build the solution**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, with no new warnings. This also compiles `NewVMCmdlet`, which `Scenario.Tests` doesn't reference.

- [ ] **Step 15: Run the orchestration and unit tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 10 passed, including `NewNetworkSecurityGroup_GetsTheDefaultRdpRule`.

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`
Expected: 5 passed.

- [ ] **Step 16: Regenerate the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`
Then run: `git --no-pager diff --stat tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/`
Expected: only `vm.json` changes. In its diff, the `vm create` entry loses the nine `--rule-*` parameter objects, from `--rule-accesses` through `--rule-source-port-ranges`, and nothing else changes. Any other drift means something else changed: stop and investigate.

- [ ] **Step 17: Run the CLI export fixture test**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --filter FullyQualifiedName~CliExportFixturesTests`
Expected: 3 passed (`_root_`, `login`, `vm`).

- [ ] **Step 18: Remove the `-Rule*` parameters from `src/PS/Compute/help/New-AzVM.md`**

(a) In SYNTAX, replace the syntax block's contents:

```text
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -VmSize <string>
 -AdminUsername <string> -AdminPassword <string> -NetworkInterfaceName <string>
 -VirtualNetworkName <string> -NetworkSecurityGroupName <string> [-SubscriptionId <string>]
 [-SubnetNames <string[]>] [-SubnetAddressPrefixes <string[][]>] [-AddressPrefixes <string[]>]
 [-RuleNames <string[]>] [-RuleDirections <string[]>] [-RuleProtocols <string[]>]
 [-RulePriorities <int[]>] [-RuleSourceAddressPrefixes <string[]>]
 [-RuleSourcePortRanges <string[]>] [-RuleDestinationPortRanges <string[]>]
 [-RuleDestinationAddressPrefixes <string[]>] [-RuleAccesses <string[]>] [-AcceleratedNetworking]
 [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

with:

```text
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -VmSize <string>
 -AdminUsername <string> -AdminPassword <string> -NetworkInterfaceName <string>
 -VirtualNetworkName <string> -NetworkSecurityGroupName <string> [-SubscriptionId <string>]
 [-SubnetNames <string[]>] [-SubnetAddressPrefixes <string[][]>] [-AddressPrefixes <string[]>]
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

(b) In PARAMETERS, delete everything from the line `### -RuleAccesses` up to, but not including, the line `### -SubnetAddressPrefixes`. That removes the nine contiguous blocks `-RuleAccesses`, `-RuleDestinationAddressPrefixes`, `-RuleDestinationPortRanges`, `-RuleDirections`, `-RuleNames`, `-RulePriorities`, `-RuleProtocols`, `-RuleSourceAddressPrefixes` and `-RuleSourcePortRanges`.

- [ ] **Step 19: Validate the PowerShell help against the built cmdlet**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records. If PlatyPS 1.0.1 is missing, run `pwsh -File build/Install-BuildDependencies.ps1` first.

If PSL14 reports drift for `New-AzVM.md`, because PlatyPS wants the SYNTAX or a parameter block laid out differently:
1. Run `pwsh -File build/New-PSHelpMarkdown.ps1 -Module Compute -Refresh`.
2. Keep only the `New-AzVM.md` change: `git checkout -- src/PS/Compute/help/Get-AzVM.md src/PS/Compute/help/Remove-AzVM.md src/PS/Compute/help/Start-AzVM.md src/PS/Compute/help/Stop-AzVM.md`.
3. Put back any prose that the refresh replaced with a `TODO -` marker.
4. Re-run `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute` and expect no records.

- [ ] **Step 20: Commit**

```bash
git add src/Extensions/Compute/Customization/NetworkSecurityGroupDefaultRule.cs src/Extensions/Compute/Extensions.Compute.csproj tests/Extensions.Compute.Tests azclips.slnx src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/NewVMCmdlet.cs tests/Scenario.Tests/VmCreateOrchestrationTests.cs tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json src/PS/Compute/help/New-AzVM.md
git commit -m "[Compute] vm create: give a new NSG legacy CLI's default rule and remove the --rule-* parameters" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 4: `--nsg` / `-SecurityGroupName` with legacy CLI semantics

**Files:**
- Modify: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (args, PowerShell parameters, four constants, ten tests, one helper)
- Modify: `src/Extensions/Compute/Models/NetworkInterface.cs` (one attribute)
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` (whole file)
- Modify: `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` (one property, one lambda line)
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (three entries)
- Modify: `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` (one parameter and its forwarding)
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`

**Interfaces:**
- Consumes:
  - `NewOrExistingResourceResolver.ResolveAsync(...)` and `NewOrExistingResourceResolver.NetworkSecurityGroup` / `.PublicIpAddress` (Task 2).
  - `NetworkSecurityGroupDefaultRule.For(string osType)` and `OsType` (Task 3).
  - `GetNetworkSecurityGroupProcessor` and `GetPublicIPAddressProcessor`, each with `SubscriptionId`, `ResourceGroupName` and `Name`.
  - The `ResourceReference` model (`src/Extensions/Compute/Models/NetworkInterface.cs`, `public string Id`).
  - `ComputeProcessorBase.SendAllowingNotFoundAsync()` and `CreateExtendedProcessor<T>(Action<T>? configure)`, where `T : ComputeProcessorBase, new()`.
  - Task 1's test helpers.
- Produces:
  - `public string? NewVMProcessor_Orchestrator.NetworkSecurityGroup`, marked `[ParameterGroup("Network Arguments")]`. It replaces `NetworkSecurityGroupName`.
  - `public string? NewNetworkInterfaceProcessor_Orchestrator.NetworkSecurityGroup` and `public string NewNetworkInterfaceProcessor_Orchestrator.DefaultNetworkSecurityGroupName` (default `string.Empty`). They replace `NetworkSecurityGroupName`.
  - CLI flag `--nsg` and PowerShell parameter `-SecurityGroupName`, both optional and bound to `NetworkSecurityGroup`. Omitting the parameter leaves the property null; `""` binds as `""`.

`NetworkSecurityGroup` becomes both a property name and, as before, a model type. That's legal C#: in type positions such as `NetworkSecurityGroup x = ...` and `ReadFromJsonAsync<NetworkSecurityGroup>`, name lookup only considers types, so it resolves to the model.

- [ ] **Step 1: Point the existing tests at `--nsg` / `SecurityGroupName`**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`:

(a) Replace the whole `s_cliArgs` field:

```csharp
    private static readonly string[] s_cliArgs =
    [
        "vm", "create",
        "--subscription-id", SubscriptionId,
        "--name", VmName,
        "--location", "westus2",
        "--resource-group", "scenario-vm-rg",
        "--size", "Standard_D2s_v3",
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--network-interface-name", "scenario-vm-nic",
        "--virtual-network-name", "scenario-vm-vnet",
        "--network-security-group-name", "scenario-vm-nsg",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];
```

with:

```csharp
    private static readonly string[] s_cliArgsWithoutNsg =
    [
        "vm", "create",
        "--subscription-id", SubscriptionId,
        "--name", VmName,
        "--location", "westus2",
        "--resource-group", "scenario-vm-rg",
        "--size", "Standard_D2s_v3",
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--network-interface-name", "scenario-vm-nic",
        "--virtual-network-name", "scenario-vm-vnet",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];

    // An explicit name the fake doesn't know: vm create looks it up, gets a 404, and creates it.
    private static readonly string[] s_cliArgs = [.. s_cliArgsWithoutNsg, "--nsg", "scenario-vm-nsg"];
```

`s_cliArgsWithoutNsg` must stay above `s_cliArgs`: static field initializers run in textual order.

(b) In `PsParameters()`, replace:

```csharp
        ["NetworkSecurityGroupName"] = "scenario-vm-nsg",
```

with:

```csharp
        ["SecurityGroupName"] = "scenario-vm-nsg",
```

- [ ] **Step 2: Write the failing NSG tests**

(a) Directly after the `MissingPublicIpId` constant, add:

```csharp

    // NSGs the fake is seeded with: one in the VM's resource group, one in another.
    private const string SharedNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg";
    private const string OtherGroupNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg";

    // A well-formed NSG id the fake has never heard of, and a public IP id passed where an NSG id belongs.
    private const string MissingNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/missing-nsg";
    private const string NotAnNsgId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/not-an-nsg";
```

(b) Insert these ten tests directly before `private static void AssertVm(IReadOnlyList<object> outputs)`:

```csharp
    [Fact]
    public void Nsg_Omitted_CreatesTheDefaultWithoutALookup()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgsWithoutNsg);

        // Every create is a long-running operation whose poll GETs the same URI after the PUT,
        // so a lookup would be a GET before the PUT.
        int create = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vmNSG?"));
        Assert.True(create >= 0, "vm create never created the default NSG.");
        Assert.DoesNotContain(record.Entries[..create], e => IsRequest(e, "GET", "/networkSecurityGroups/"));
        Assert.Contains("/networkSecurityGroups/scenario-vmNSG", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_NewName_IsLookedUpThenCreated()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        int lookup = record.Entries.FindIndex(e => IsRequest(e, "GET", "/networkSecurityGroups/scenario-vm-nsg?"));
        int create = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vm-nsg?"));
        Assert.True(lookup >= 0, "vm create never looked the NSG up.");
        Assert.Equal(404, record.Entries[lookup].Response.StatusCode);
        Assert.True(create > lookup, "vm create did not create the NSG after its lookup.");
        Assert.Contains("/networkSecurityGroups/scenario-vm-nsg", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_ExistingName_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, SharedNsgId), [.. s_cliArgsWithoutNsg, "--nsg", "shared-nsg"]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/networkSecurityGroups/shared-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(SharedNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_ResourceIdInAnotherGroup_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, OtherGroupNsgId), [.. s_cliArgsWithoutNsg, "--nsg", OtherGroupNsgId]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/shared-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(OtherGroupNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_MissingResourceId_FailsBeforeAnythingIsCreated()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", MissingNsgId]);

        Assert.Equal($"Network security group '{MissingNsgId}' was not found.", error.Message);
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/networkSecurityGroups/missing-nsg?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

    [Fact]
    public void Nsg_MalformedResourceId_FailsBeforeAnyRequest()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", NotAnNsgId]);

        Assert.Equal(
            $"Invalid network security group resource id '{NotAnNsgId}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Network/networkSecurityGroups/{{networkSecurityGroupName}}.",
            error.Message);

        // Only group create reached the fake.
        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/providers/"));
    }

    [Fact]
    public void Nsg_EmptyString_AttachesNone()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--nsg", ""]);

        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/networkSecurityGroups/"));
        Assert.DoesNotContain("networkSecurityGroup", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void Nsg_IsResolvedBeforeThePublicIp()
    {
        var (record, error) = RecordCliFailure(
            new FakeVmArmBackend(SubscriptionId),
            [.. s_cliArgsWithoutNsg, "--nsg", MissingNsgId, "--public-ip-address", MissingPublicIpId]);

        // Legacy CLI validates the NSG first, so its error wins and the public IP is never looked up.
        Assert.Equal($"Network security group '{MissingNsgId}' was not found.", error.Message);
        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/publicIPAddresses/"));
    }

    [Fact]
    public void PowerShell_SecurityGroupName_AttachesAnExistingNsg()
    {
        var parameters = PsParameters();
        parameters["SecurityGroupName"] = "shared-nsg";

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId, SharedNsgId), parameters);

        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.Contains(SharedNsgId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PowerShell_SecurityGroupNameOmitted_CreatesTheDefault()
    {
        var parameters = PsParameters();
        parameters.Remove("SecurityGroupName");

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId), parameters);

        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/scenario-vmNSG?"));
    }

```

The scenario host builds the invocation dictionary directly, the way `NewVMCmdlet` does, so the cmdlet's own `$null` handling (Step 7) isn't exercised here. Omitting the key is what the cmdlet sends for both an omitted parameter and `$null`.

(c) Directly after the `RecordCliFailure` helper, add:

```csharp

    /// <summary>Runs New-AzResourceGroup then New-AzVM against <paramref name="backend"/>, recording both.</summary>
    private static SessionRecord RecordPowerShell(FakeVmArmBackend backend, Dictionary<string, object> vmParameters)
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertVm(host.RunPowerShell("New-AzVM", vmParameters, extension: "Compute"));
        }
        return record;
    }
```

- [ ] **Step 3: Run the orchestration tests to see them fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: all 20 fail at argument binding. `--nsg` and `SecurityGroupName` aren't registered yet, and `--network-security-group-name` / `NetworkSecurityGroupName` are still required.

- [ ] **Step 4: Omit a null NSG reference from the NIC request**

In `src/Extensions/Compute/Models/NetworkInterface.cs`, replace:

```csharp
public class NetworkInterfaceProperties
{
    public NetworkSecurityGroup? NetworkSecurityGroup { get; set; }
```

with:

```csharp
public class NetworkInterfaceProperties
{
    // Omitted when null, so a NIC without an NSG sends no networkSecurityGroup rather than null.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NetworkSecurityGroup? NetworkSecurityGroup { get; set; }
```

The file already has `using System.Text.Json.Serialization;`. Reading NIC responses is unaffected.

- [ ] **Step 5: Replace `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`**

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
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];
    public bool AcceleratedNetworking { get; set; }

    public string? NetworkSecurityGroup { get; set; }

    /// <summary>The NSG created when <see cref="NetworkSecurityGroup"/> is null. Empty means none.</summary>
    public string DefaultNetworkSecurityGroupName { get; set; } = string.Empty;

    /// <summary>OS type of the VM's image, which picks the default rule of a new NSG.</summary>
    public string OsType { get; set; } = string.Empty;

    public string? PublicIpAddress { get; set; }

    /// <summary>The public IP created when <see cref="PublicIpAddress"/> is null. Empty means none.</summary>
    public string DefaultPublicIpAddressName { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Explicit names and ids are looked up before anything is created, NSG first as legacy CLI
        // validates them, so a missing id fails without leaving a VNet, NSG or public IP behind.
        (string? existingNetworkSecurityGroupId, string? networkSecurityGroupNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            NetworkSecurityGroup, DefaultNetworkSecurityGroupName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.NetworkSecurityGroup, FindNetworkSecurityGroupIdAsync);
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            PublicIpAddress, DefaultPublicIpAddressName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.PublicIpAddress, FindPublicIpAddressIdAsync);

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

        Task<HttpResponseMessage> vnetResponseTask = vnetProcessor.SendAsync();
        Task<string?> networkSecurityGroupIdTask = networkSecurityGroupNameToCreate is null
            ? Task.FromResult(existingNetworkSecurityGroupId)
            : CreateNetworkSecurityGroupAsync(networkSecurityGroupNameToCreate);
        Task<string?> publicIpAddressIdTask = publicIpAddressNameToCreate is null
            ? Task.FromResult(existingPublicIpAddressId)
            : CreatePublicIpAddressAsync(publicIpAddressNameToCreate);
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        string? networkSecurityGroupId = await networkSecurityGroupIdTask;
        string? publicIpAddressId = await publicIpAddressIdTask;
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;

        NewNetworkInterfaceProcessor nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = Name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetIds = vnet.Properties.Subnets.Select(subnet => subnet.Id).ToList();
            p.NetworkSecurityGroupId = networkSecurityGroupId ?? string.Empty;
            p.AcceleratedNetworking = AcceleratedNetworking;
            p.PublicIpAddressId = publicIpAddressId ?? string.Empty;
        });
        return await nicProcessor.SendAsync();
    }

    private Task<string?> FindNetworkSecurityGroupIdAsync(string subscriptionId, string resourceGroupName, string name)
        => FindIdAsync<GetNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.Name = name;
        });

    private Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
        => FindIdAsync<GetPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.Name = name;
        });

    /// <summary>
    /// Reads the resource <typeparamref name="TGetProcessor"/> addresses and returns its id, or null
    /// when ARM answers 404. Any ARM resource's body carries its id, so it reads as a reference.
    /// </summary>
    private async Task<string?> FindIdAsync<TGetProcessor>(Action<TGetProcessor> configure)
        where TGetProcessor : ComputeProcessorBase, new()
    {
        using HttpResponseMessage response = await CreateExtendedProcessor(configure).SendAllowingNotFoundAsync();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        ResourceReference resource = (await response.Content.ReadFromJsonAsync<ResourceReference>(JsonOptions))!;
        return resource.Id;
    }

    private async Task<string?> CreateNetworkSecurityGroupAsync(string name)
    {
        SecurityRule defaultRule = NetworkSecurityGroupDefaultRule.For(OsType);
        using HttpResponseMessage response = await CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.RuleNames = [defaultRule.Name];
            p.RuleDirections = [defaultRule.Properties.Direction];
            p.RuleProtocols = [defaultRule.Properties.Protocol];
            p.RulePriorities = [defaultRule.Properties.Priority];
            p.RuleSourceAddressPrefixes = [defaultRule.Properties.SourceAddressPrefix];
            p.RuleSourcePortRanges = [defaultRule.Properties.SourcePortRange];
            p.RuleDestinationPortRanges = [defaultRule.Properties.DestinationPortRange];
            p.RuleDestinationAddressPrefixes = [defaultRule.Properties.DestinationAddressPrefix];
            p.RuleAccesses = [defaultRule.Properties.Access];
        }).SendAsync();

        NetworkSecurityGroup networkSecurityGroup = (await response.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;
        return networkSecurityGroup.Id;
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

What changed from Task 3's version:
- `NetworkSecurityGroupName` became `NetworkSecurityGroup` plus `DefaultNetworkSecurityGroupName`, resolved before the public IP.
- The NSG `PUT` moved into `CreateNetworkSecurityGroupAsync` and runs only for a new NSG.
- The NIC gets the existing or new NSG ID, or none.
- Both lookups now share `FindIdAsync<TGetProcessor>`. It reads the ID through the existing `ResourceReference` model (`{ Id }`), which any ARM resource body deserializes into. `FindNetworkSecurityGroupIdAsync` and `FindPublicIpAddressIdAsync` are thin wrappers that configure their Get processor, so the resolver still takes them as method groups.
- `CreatePublicIpAddressAsync` is unchanged, and so is the public IP's behaviour: Task 1's public IP tests pin it.

- [ ] **Step 6: Rename the property in the VM orchestrator and pass the default name**

In `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`:

(a) Replace:

```csharp
        [ParameterGroup("Network Arguments")]
        public string NetworkSecurityGroupName { get; set; } = string.Empty;
```

with:

```csharp
        [ParameterGroup("Network Arguments")]
        public string? NetworkSecurityGroup { get; set; }
```

(b) In `SendAsync`'s NIC orchestrator lambda, replace:

```csharp
                p.NetworkSecurityGroupName = NetworkSecurityGroupName;
```

with:

```csharp
                p.NetworkSecurityGroup = NetworkSecurityGroup;
                p.DefaultNetworkSecurityGroupName = $"{Name}NSG";
```

- [ ] **Step 7: Register `--nsg` and `-SecurityGroupName`**

In `src/Extensions/Compute/ExtensionIndex.cs`:

(a) In `s_newVMOrchestrator_PS` (`InvocationName: "New-AzVM"`), replace:

```csharp
                ["NetworkSecurityGroupName"]       = new("NetworkSecurityGroupName", "NetworkSecurityGroupName", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
```

with:

```csharp
                ["SecurityGroupName"]              = new("NetworkSecurityGroup", "SecurityGroupName", typeof(string), "Name of the network security group to create (default: '{vm}NSG') or of an existing one to attach. Can also reference an existing NSG by resource ID, or specify \"\" for none.", IsRequired: false, Aliases: [], AllowedValues: []),
```

(b) In `s_newVMOrchestrator_CLI` (`InvocationName: "vm create"`), replace its `["network-security-group-name"]` entry:

```csharp
                ["network-security-group-name"]      = new("NetworkSecurityGroupName", "network-security-group-name", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
```

with:

```csharp
                ["nsg"]                              = new("NetworkSecurityGroup", "nsg", typeof(string), "Name of the network security group to create (default: '{vm}NSG') or of an existing one to attach. Can also reference an existing NSG by resource ID, or specify \"\" for none.", IsRequired: false, Aliases: [], AllowedValues: []),
```

(c) The same `["network-security-group-name"]` line also appears in the dormant `s_newNetworkInterfaceOrchestrator_CLI` (`InvocationName: "vm nic create-orchestrated"`). There, change only the first argument, the processor property, from `"NetworkSecurityGroupName"` to `"NetworkSecurityGroup"`, so the dormant entry points at a property that exists:

```csharp
                ["network-security-group-name"]      = new("NetworkSecurityGroup", "network-security-group-name", typeof(string), "The network security group name to create.", IsRequired: true, Aliases: [], AllowedValues: []),
```

A `ParameterMetadata`'s first argument is the processor property name; its second is the name the user types.

- [ ] **Step 8: Replace `-NetworkSecurityGroupName` with `-SecurityGroupName` on `New-AzVM`**

In `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`:

(a) Replace:

```csharp
    [Parameter(Mandatory = true)]
    public string NetworkSecurityGroupName { get; set; } = "";
```

with:

```csharp
    [Parameter(Mandatory = false)]
    public string? SecurityGroupName { get; set; }
```

(b) In `ProcessRecord()`, delete this line from the `parameters` initializer:

```csharp
            { "NetworkSecurityGroupName", NetworkSecurityGroupName },
```

(c) Directly after the `AcceleratedNetworking` forwarding line:

```csharp
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AcceleratedNetworking)))          { parameters.Add("AcceleratedNetworking", AcceleratedNetworking.IsPresent); }
```

add:

```csharp
        // Omitted or $null keeps the default {vm}NSG; "" is forwarded because it means none.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SecurityGroupName)) && SecurityGroupName is not null) { parameters.Add("SecurityGroupName", SecurityGroupName); }
```

The dictionary key is the PowerShell name registered in Step 7, which the PowerShell table maps to `NetworkSecurityGroup`.

- [ ] **Step 9: Build the solution**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, with no new warnings.

- [ ] **Step 10: Run the orchestration and unit tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 20 passed.

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj`
Expected: 5 passed.

- [ ] **Step 11: Regenerate the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`
Then run: `git --no-pager diff tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
Expected: the `vm create` entry loses the `--network-security-group-name` parameter object. It gains this one between `--network-interface-name` and `--profile`:

```json
      {
        "name": "--nsg",
        "required": false,
        "short_summary": "Name of the network security group to create (default: \u0027{vm}NSG\u0027) or of an existing one to attach. Can also reference an existing NSG by resource ID, or specify \u0022\u0022 for none.",
        "long_summary": "",
        "group_name": "Network Arguments"
      },
```

The `long_summary` and examples still mention `--network-security-group-name`; they come from `vm/create.md`, which Task 5 rewrites. The exporter owns the escaping, so take its output as written. Any change outside `vm create` means something else drifted: stop and investigate.

- [ ] **Step 12: Run the CLI export fixture test**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --filter FullyQualifiedName~CliExportFixturesTests`
Expected: 3 passed.

- [ ] **Step 13: Commit**

```bash
git add tests/Scenario.Tests/VmCreateOrchestrationTests.cs src/Extensions/Compute/Models/NetworkInterface.cs src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/NewVMCmdlet.cs tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json
git commit -m "[Compute] Add az vm create --nsg and New-AzVM -SecurityGroupName with legacy CLI semantics" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

From this commit until Task 7, the three live-recording scenario classes are expected to fail; see Global Constraints.

### Task 5: Document `--nsg` and `-SecurityGroupName`

**Files:**
- Modify: `src/CLI/Help/Docs/vm/create.md` (whole file)
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
- Modify: `src/PS/Compute/help/New-AzVM.md` (SYNTAX, DESCRIPTION, EXAMPLES, PARAMETERS)
- Modify: `wiki/01-getting-started/powershell-usage.md` (one line)

**Interfaces:**
- Consumes: `--nsg` / `-SecurityGroupName` (Task 4); the default rule (Task 3).
- Produces: help that validates against the live metadata and cmdlet. Every CLI example passes every required flag, or the validator fails with `CLI_L4_MissingRequiredFlag`. Every PowerShell example assigns `$adminPassword` in its own fence (PSL5) and binds every mandatory parameter (PSL3).

- [ ] **Step 1: Replace `src/CLI/Help/Docs/vm/create.md`**

The first example's heading changes from "Create a Linux VM in East US" to "Create a VM in East US". vm create deploys Windows Server 2022, and the description now says the NSG rule follows the image's OS, so a "Linux" heading would promise SSH where users get RDP.

````markdown
---
summary: Create a virtual machine and its supporting network resources.
status: ga
schema: 1.0.0
online version:
---

Description: Provision an Azure virtual machine in the specified resource group along with the supporting virtual network, subnet, network security group, network interface, and public IP address in a single orchestrated call. The VM size, administrator credentials, and target location must all be provided. Resource names supplied for `--virtual-network-name` and `--network-interface-name` are created if they do not already exist in the resource group. By default a network security group named `{vm}NSG` is created, or updated if it already exists. A network security group the command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux. Pass `--nsg` a name to reuse the network security group of that name, unchanged, when it exists and create it when it does not, a resource ID to attach an existing network security group, or `""` to create none. By default a Standard, static public IP address named `{vm}PublicIP` is created, or updated if it already exists. Pass `--public-ip-address` a name to reuse the public IP address of that name when it exists and create it when it does not, a resource ID to attach an existing public IP address, or `""` to create none. The command returns the new virtual machine with the power state and the public and private IP addresses that `az vm show -d` reports.

### Create a VM in East US

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create using shell variables for credentials

```bash
az vm create --name $vmName --resource-group $rg --location eastus --size Standard_D2s_v5 --admin-username $adminUser --admin-password $adminPwd --network-interface-name $vmName-nic --virtual-network-name $vmName-vnet --subscription-id $subId
```

### Create a VM that uses an existing network security group

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --nsg mynsg
```

### Create a VM with a named public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --public-ip-address myvm-ip
```

### Create a VM without a public IP address

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --public-ip-address ""
```
````

The `Description:` paragraph stays on one line, as the file format requires.

- [ ] **Step 2: Build the solution**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`. The build copies the help markdown next to the CLI.

- [ ] **Step 3: Check the CLI help**

Run: `dotnet artifacts/Debug/Core.Cli/az.dll vm create --help`
Expected:
- `--nsg` is listed among the Network Arguments with the help text from Global Constraints.
- Neither `--network-security-group-name` nor any `--rule-*` flag appears.
- The description and all five examples render.

- [ ] **Step 4: Regenerate the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`
Then run: `git --no-pager diff tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
Expected: only the `vm create` entry's `long_summary` and `examples` change, matching Step 1. No example contains `--network-security-group-name`.

- [ ] **Step 5: Run the CLI export fixture test**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --filter FullyQualifiedName~CliExportFixturesTests`
Expected: 3 passed.

- [ ] **Step 6: Update `src/PS/Compute/help/New-AzVM.md`**

Make five edits.

(a) SYNTAX — replace the syntax block's contents:

```text
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -VmSize <string>
 -AdminUsername <string> -AdminPassword <string> -NetworkInterfaceName <string>
 -VirtualNetworkName <string> -NetworkSecurityGroupName <string> [-SubscriptionId <string>]
 [-SubnetNames <string[]>] [-SubnetAddressPrefixes <string[][]>] [-AddressPrefixes <string[]>]
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

with:

```text
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -VmSize <string>
 -AdminUsername <string> -AdminPassword <string> -NetworkInterfaceName <string>
 -VirtualNetworkName <string> [-SubscriptionId <string>] [-SecurityGroupName <string>]
 [-SubnetNames <string[]>] [-SubnetAddressPrefixes <string[][]>] [-AddressPrefixes <string[]>]
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

PlatyPS lists mandatory parameters first, then optional ones in declaration order. `-SecurityGroupName` is declared where `-NetworkSecurityGroupName` was, so it's the first optional parameter after `-SubscriptionId`.

(b) DESCRIPTION — replace:

```markdown
Creates a virtual network, network security group, public IP address, network interface, and virtual machine using the supplied names and parameters.
By default the public IP address is a Standard, static address named after the virtual machine with a `PublicIP` suffix (for example `myvmPublicIP`), created or, if it already exists, updated.
```

with:

```markdown
Creates a virtual network, network security group, public IP address, network interface, and virtual machine using the supplied names and parameters.
By default the network security group is named after the virtual machine with an `NSG` suffix (for example `myvmNSG`), created or, if it already exists, updated.
A network security group this command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux.
Use `-SecurityGroupName` to choose the network security group by name or resource ID, or pass an empty string to create none.
By default the public IP address is a Standard, static address named after the virtual machine with a `PublicIP` suffix (for example `myvmPublicIP`), created or, if it already exists, updated.
```

(c) EXAMPLES:

1. In the first example, `### Create a VM with its networking stack`, replace its first prose line:

   ```markdown
   Provision a `Standard_D2s_v3` virtual machine named `myvm` in `myrg` (East US) along with a fresh virtual network (`myvnet`), network security group (`mynsg`), and network interface (`mynic`).
   ```

   with:

   ```markdown
   Provision a `Standard_D2s_v3` virtual machine named `myvm` in `myrg` (East US) along with a fresh virtual network (`myvnet`), network interface (`mynic`), and network security group (`myvmNSG`, which allows inbound RDP).
   ```

2. In all three existing example commands, delete the text ` -NetworkSecurityGroupName mynsg`, including its leading space.

3. Directly after the closing fence of the first example, insert:

   ````markdown

   ### Create a VM that uses an existing network security group

   Provision the same stack, but attach the existing network security group `mynsg` in `myrg` instead of creating one.
   The network security group is attached as-is; its rules are not changed.

   ```powershell
   $adminPassword = 'P@ssw0rd-replace-me!'
   New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -SecurityGroupName mynsg
   ```
   ````

(d) PARAMETERS — delete everything from the line `### -NetworkSecurityGroupName` up to, but not including, the line `### -Profile`.

(e) PARAMETERS — insert this block directly before `### -SubnetAddressPrefixes`, which keeps the section alphabetical:

````markdown
### -SecurityGroupName

Name of the network security group to create (default: '{vm}NSG') or of an existing one to attach.
Can also reference an existing NSG by resource ID, or specify "" for none.

When omitted, a network security group named after the virtual machine with an `NSG` suffix is created, or updated if it already exists.
A name is looked up in the resource group: an existing network security group is attached as-is, otherwise one is created.
A resource ID must reference an existing network security group, which is attached as-is.
A network security group this command creates has one inbound rule for the image's operating system, allowing traffic from any source: `rdp` (TCP port 3389) for Windows or `default-allow-ssh` (TCP port 22) for Linux.

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

- [ ] **Step 7: Update the wiki example**

In `wiki/01-getting-started/powershell-usage.md`, replace:

```powershell
       -NetworkInterfaceName nic1 -VirtualNetworkName vnet1 -NetworkSecurityGroupName nsg1
```

with:

```powershell
       -NetworkInterfaceName nic1 -VirtualNetworkName vnet1 -SecurityGroupName nsg1
```

- [ ] **Step 8: Validate the PowerShell help against the built cmdlet**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records. If PSL14 reports drift for `New-AzVM.md`, follow the refresh procedure in Task 3 Step 19, then put back the prose from Step 6.

- [ ] **Step 9: Run the full help pipeline**

Run: `pwsh -File build/build.ps1 -GenerateHelpArtifacts`
Expected: exit code 0. This rebuilds the solution and validates every CLI and PowerShell example against the live metadata and cmdlets, including the new `--nsg` and `-SecurityGroupName` examples. It closes the help red window that Task 4 opened.

- [ ] **Step 10: Commit**

```bash
git add src/CLI/Help/Docs/vm/create.md tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json src/PS/Compute/help/New-AzVM.md wiki/01-getting-started/powershell-usage.md
git commit -m "{Compute} Document vm create --nsg and New-AzVM -SecurityGroupName" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 6: Move vm create's tests, scripts and benchmarks to `--nsg`

**Files:**
- Modify: `tests/Scenario.Tests/VmCreateScenarioTests.cs` (two lines)
- Modify: `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` (two lines)
- Modify: `tests/Scenario.Tests/Scripts/vm-create.sh`, `vm-create.ps1` and `vm-create-module.ps1` (one line each)
- Modify: `tests/Scenario.Tests/README.md` (one paragraph)
- Modify: `tests/Benchmark/CLI/VirtualMachineBenchmarks.cs` and `tests/Benchmark/PS/VirtualMachineBenchmarks.cs` (one argument each)
- Modify: `tests/Scripts/CLI/VMCreate.sh`, `VMDelete.sh`, `VMList.sh`, `VMShow.sh` and `VMStop.sh`
- Modify: `tests/Scripts/PS/VMCreate.ps1`, `VMDelete.ps1`, `VMList.ps1`, `VMShow.ps1` and `VMStop.ps1`

**Interfaces:**
- Consumes: `--nsg` / `-SecurityGroupName` and the default `{vm}NSG` with its `rdp` rule (Tasks 3 and 4).
- Produces: every caller of the removed parameters is updated.
  - The live scenarios and benchmarks take the default path, as the spec decided. That keeps the benchmarks' request count and moves the recordings to `{vm}NSG`.
  - The smoke scripts keep their explicit NSG names under `--nsg` / `-SecurityGroupName`.
  - `VMStop` and `VMDelete` drop the RDP rule they used to build; the default rule now provides it.

- [ ] **Step 1: Move the live vm create scenario to the default NSG**

In `tests/Scenario.Tests/VmCreateScenarioTests.cs`, delete this line from `CliArgs`:

```csharp
        "--network-security-group-name", "scenario-vm-nsg",
```

and this line from `PsParameters`:

```csharp
        ["NetworkSecurityGroupName"] = "scenario-vm-nsg",
```

- [ ] **Step 2: Move the lifecycle scenario to the default NSG**

In `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`, delete this line from `RunCreate`:

```csharp
            "--network-security-group-name", $"scenario-lifecycle-nsg-{suffix}",
```

and this line from `PsCreateParameters`:

```csharp
        ["NetworkSecurityGroupName"] = $"scenario-lifecycle-nsg-{suffix}",
```

The two VMs, `lifecycle-vm` and `lifecycle-vm-2`, now get `lifecycle-vmNSG` and `lifecycle-vm-2NSG`.

- [ ] **Step 3: Move the three replay scripts to the default NSG**

They replay the vm create recording, so they must send exactly what `VmCreateScenarioTests` sends.

In `tests/Scenario.Tests/Scripts/vm-create.sh`, replace:

```bash
  --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet --network-security-group-name scenario-vm-nsg \
```

with:

```bash
  --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet \
```

In `tests/Scenario.Tests/Scripts/vm-create.ps1`, replace:

```powershell
    --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet --network-security-group-name scenario-vm-nsg `
```

with:

```powershell
    --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet `
```

In `tests/Scenario.Tests/Scripts/vm-create-module.ps1`, replace:

```powershell
    -NetworkInterfaceName scenario-vm-nic -VirtualNetworkName scenario-vm-vnet -NetworkSecurityGroupName scenario-vm-nsg `
```

with:

```powershell
    -NetworkInterfaceName scenario-vm-nic -VirtualNetworkName scenario-vm-vnet `
```

Each replacement keeps the line's trailing continuation character.

- [ ] **Step 4: Update the vm create paragraph in `tests/Scenario.Tests/README.md`**

Replace:

```markdown
The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: vnet, nsg, the default public IP, nic, and the vm with its polling, then the detail
reads behind its output (the vm with `$expand=instanceView`, the nic, the public IP), twenty-odd
entries in one file. Before recording it live, the subscription needs the `Microsoft.Compute` and
`Microsoft.Network` providers registered and quota for one small vm and one Standard public IP.
The subscription comes from the signed-in profile. Afterwards delete the resource group the run
created (`scenario-vm-rg`), which removes the vm, its public IP and all its network resources with it.
```

with:

```markdown
The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: vnet, the default nsg (`scenario-vmNSG`, with its `rdp` rule), the default public
IP, nic, and the vm with its polling, then the detail reads behind its output (the vm with
`$expand=instanceView`, the nic, the public IP), twenty-odd entries in one file. Before recording
it live, the subscription needs the `Microsoft.Compute` and `Microsoft.Network` providers
registered and quota for one small vm and one Standard public IP. The subscription comes from the
signed-in profile. Afterwards delete the resource group the run created (`scenario-vm-rg`), which
removes the vm, its public IP, its nsg and all its other network resources with it.
```

- [ ] **Step 5: Move the benchmarks to the default NSG**

In `tests/Benchmark/CLI/VirtualMachineBenchmarks.cs`, replace:

```csharp
            "--virtual-network-name", $"bench-vnet-{n}",
            "--network-security-group-name", $"bench-nsg-{n}");
```

with:

```csharp
            "--virtual-network-name", $"bench-vnet-{n}");
```

In `tests/Benchmark/PS/VirtualMachineBenchmarks.cs`, replace:

```csharp
            ("VirtualNetworkName", $"bench-vnet-{n}"),
            ("NetworkSecurityGroupName", $"bench-nsg-{n}"));
```

with:

```csharp
            ("VirtualNetworkName", $"bench-vnet-{n}"));
```

The default path `PUT`s the NSG without a lookup, so each create sends the same number of requests as before.

- [ ] **Step 6: Update the CLI smoke scripts**

In `tests/Scripts/CLI/VMCreate.sh`, replace:

```bash
        --network-security-group-name "$name-nsg" \
```

with:

```bash
        --nsg "$name-nsg" \
```

In `tests/Scripts/CLI/VMList.sh`, replace:

```bash
    --network-security-group-name "azclips-vmlist-nsg-$SUFFIX"
```

with:

```bash
    --nsg "azclips-vmlist-nsg-$SUFFIX"
```

In `tests/Scripts/CLI/VMShow.sh`, replace:

```bash
    --network-security-group-name "azclips-vmshow-nsg-$SUFFIX"
```

with:

```bash
    --nsg "azclips-vmshow-nsg-$SUFFIX"
```

In `tests/Scripts/CLI/VMStop.sh`, replace:

```bash
    --network-security-group-name "azclips-vmstop-nsg-$SUFFIX" \
    --rule-names rdp \
    --rule-protocols tcp \
    --rule-directions Inbound \
    --rule-priorities 1001 \
    --rule-source-address-prefixes "*" \
    --rule-source-port-ranges "*" \
    --rule-destination-port-ranges 3389 \
    --rule-destination-address-prefixes "*" \
    --rule-accesses Allow
```

with:

```bash
    --nsg "azclips-vmstop-nsg-$SUFFIX"
```

In `tests/Scripts/CLI/VMDelete.sh`, replace:

```bash
        --network-security-group-name "${PREFIX}-nsg-$suffix" \
        --rule-names rdp \
        --rule-protocols tcp \
        --rule-directions Inbound \
        --rule-priorities 1001 \
        --rule-source-address-prefixes "*" \
        --rule-source-port-ranges "*" \
        --rule-destination-port-ranges 3389 \
        --rule-destination-address-prefixes "*" \
        --rule-accesses Allow
```

with:

```bash
        --nsg "${PREFIX}-nsg-$suffix"
```

The new last argument has no trailing `\`: it ends the command.

- [ ] **Step 7: Update the PowerShell smoke scripts**

In `tests/Scripts/PS/VMCreate.ps1`, replace the splatted hashtable:

```powershell
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
```

with:

```powershell
    $parameters = @{
        SubscriptionId        = $subscriptionId
        ResourceGroupName     = $resourceGroup
        Name                  = $Name
        Location              = $location
        VmSize                = 'Standard_D2s_v3'
        AdminUsername         = 'Artemis'
        AdminPassword         = $env:AZCLIPS_VM_ADMIN_PASSWORD
        NetworkInterfaceName  = "$Name-nic"
        VirtualNetworkName    = "$Name-vnet"
        AddressPrefixes       = '10.0.0.0/16'
        SubnetNames           = "$Name-subnet"
        SubnetAddressPrefixes = '10.0.2.0/24'
        SecurityGroupName     = "$Name-nsg"
    }
```

In `tests/Scripts/PS/VMList.ps1`, replace:

```powershell
        -NetworkSecurityGroupName "azclips-vmlist-nsg-$suffix"
```

with:

```powershell
        -SecurityGroupName "azclips-vmlist-nsg-$suffix"
```

In `tests/Scripts/PS/VMShow.ps1`, replace:

```powershell
        -NetworkSecurityGroupName "azclips-vmshow-nsg-$suffix"
```

with:

```powershell
        -SecurityGroupName "azclips-vmshow-nsg-$suffix"
```

In `tests/Scripts/PS/VMStop.ps1`, replace:

```powershell
        -NetworkSecurityGroupName "azclips-vmstop-nsg-$suffix" `
        -RuleNames rdp `
        -RuleProtocols tcp `
        -RuleDirections Inbound `
        -RulePriorities 1001 `
        -RuleSourceAddressPrefixes * `
        -RuleSourcePortRanges * `
        -RuleDestinationPortRanges 3389 `
        -RuleDestinationAddressPrefixes * `
        -RuleAccesses Allow
```

with:

```powershell
        -SecurityGroupName "azclips-vmstop-nsg-$suffix"
```

In `tests/Scripts/PS/VMDelete.ps1`, replace:

```powershell
        -NetworkSecurityGroupName "$Prefix-nsg-$Suffix" `
        -RuleNames rdp `
        -RuleProtocols tcp `
        -RuleDirections Inbound `
        -RulePriorities 1001 `
        -RuleSourceAddressPrefixes * `
        -RuleSourcePortRanges * `
        -RuleDestinationPortRanges 3389 `
        -RuleDestinationAddressPrefixes * `
        -RuleAccesses Allow
```

with:

```powershell
        -SecurityGroupName "$Prefix-nsg-$Suffix"
```

The new last argument has no trailing backtick: it ends the command.

- [ ] **Step 8: Syntax-check every edited script**

Run the bash parse check with Git's bash on Windows:

```bash
for f in tests/Scripts/CLI/VMCreate.sh tests/Scripts/CLI/VMDelete.sh tests/Scripts/CLI/VMList.sh tests/Scripts/CLI/VMShow.sh tests/Scripts/CLI/VMStop.sh tests/Scenario.Tests/Scripts/vm-create.sh; do bash -n "$f" || echo "SYNTAX ERROR: $f"; done
```

Expected: no output.

Then run the PowerShell parse check:

```powershell
Get-ChildItem tests/Scripts/PS/VM*.ps1, tests/Scenario.Tests/Scripts/vm-create*.ps1 | ForEach-Object {
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$errors) | Out-Null
    if ($errors) { "$($_.Name): $($errors -join '; ')" }
}
```

Expected: no output.

- [ ] **Step 9: Confirm no caller still uses a removed parameter**

Run: `git grep -n -e "network-security-group-name" -e "NetworkSecurityGroupName" -e "--rule-" -e "-Rule[A-Z]" -- src tests wiki tools ":!*.json"`
Expected: exactly one hit, which is intentional: the dormant `vm nic create-orchestrated` entry `["network-security-group-name"]` in `src/Extensions/Compute/ExtensionIndex.cs`. The dormant `vm nsg create` entries are keyed `"rule-names"` and so on, without the leading `--`, and don't match.

Any other hit is a missed caller: fix it.

- [ ] **Step 10: Build and run the tests that must stay green**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, including the benchmark project.

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter FullyQualifiedName~VmCreateOrchestrationTests`
Expected: 20 passed.

- [ ] **Step 11: Confirm the live scenarios now wait on a re-recording**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreateScenarioTests|FullyQualifiedName~VmLifecycleScenarioTests|FullyQualifiedName~VmCreateScriptScenarioTests"`
Expected: FAIL. Playback reports an unmatched `PUT https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/networkSecurityGroups/scenario-vmNSG?api-version=2025-05-01` (or `lifecycle-vmNSG` for the lifecycle scenario). The recordings predate the default NSG; Task 7 re-records them.

- [ ] **Step 12: Commit**

```bash
git add tests/Scenario.Tests/VmCreateScenarioTests.cs tests/Scenario.Tests/VmLifecycleScenarioTests.cs tests/Scenario.Tests/Scripts tests/Scenario.Tests/README.md tests/Benchmark/CLI/VirtualMachineBenchmarks.cs tests/Benchmark/PS/VirtualMachineBenchmarks.cs tests/Scripts/CLI tests/Scripts/PS
git commit -m "{Compute} Move vm create's scenario tests, scripts and benchmarks to --nsg" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 7: Re-record the live scenarios (user, with credentials)

**This task is run by the user.** It creates real Azure resources (VMs, NSGs and Standard public IPs) and needs a signed-in azclips profile. An agent must not run it without the user's explicit go-ahead.

**Files:**
- Re-record: `tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json`
- Re-record: `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`

**Interfaces:**
- Consumes: Tasks 1–6 committed and built.
- Produces: recordings that close the scenario red window. `VmCreateScriptScenarioTests` replays the vm create recording through its three scripts, so it goes green too.

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

`vm delete` leaves NSGs, public IPs, NICs and VNets behind, as legacy does, so delete the whole groups:

```powershell
dotnet artifacts/Debug/Core.Cli/az.dll group delete --name scenario-vm-rg
dotnet artifacts/Debug/Core.Cli/az.dll group delete --name scenario-vm-lifecycle-rg
```

- [ ] **Step 5: Inspect the vm create recording**

```powershell
$j = Get-Content tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json -Raw | ConvertFrom-Json
$j.entries | ForEach-Object { "{0} {1} -> {2}" -f $_.Request.Method, $_.Request.Uri, $_.Response.StatusCode }
($j.entries | Where-Object { $_.Request.Method -eq 'PUT' -and $_.Request.Uri -match 'networkSecurityGroups' }).Request.Body
($j.entries | Where-Object { $_.Request.Method -eq 'PUT' -and $_.Request.Uri -match 'networkInterfaces' }).Request.Body
```

Expected:
- **NSG `PUT`:** a `PUT .../networkSecurityGroups/scenario-vmNSG?api-version=2025-05-01` with **no** `GET` of that NSG before it, because the default name is not looked up. Its body's `securityRules` holds exactly the `rdp` rule: TCP, destination port `3389`, priority `1000`, inbound, allow, from `*`.
- **NIC `PUT`:** the body contains `"networkSecurityGroup":{"id":"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/networkSecurityGroups/scenario-vmNSG"`. It still attaches `scenario-vmPublicIP` on `ipconfig0`.
- **Sanitising:** the subscription ID is the placeholder, and the password appears only as `[redacted]`.

The lifecycle recording has the same shape for `lifecycle-vmNSG` and `lifecycle-vm-2NSG`.

- [ ] **Step 6: Scan the recordings for secrets**

Run: `python devops/scripts/ci/scan_recordings.py --base origin/main` (use `python3` where that is the interpreter's name)
Expected: no findings.

- [ ] **Step 7: Replay the whole scenario suite**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj`
Expected: all pass. That includes:
- `VmCreateScenarioTests`, on both surfaces.
- `VmLifecycleScenarioTests`.
- `VmCreateScriptScenarioTests`. Its bash case skips without a usable bash, and its module case needs the Debug artifacts.

- [ ] **Step 8: Run the smoke scripts live (recommended)**

`VMStop` used to build its own RDP rule and now relies on the default one, so run it along with `VMCreate`:

```bash
AZCLIPS_SUBSCRIPTION_ID=<test-subscription> AZCLIPS_VM_ADMIN_PASSWORD='<strong-password>' ./tests/Scripts/CLI/VMCreate.sh
AZCLIPS_SUBSCRIPTION_ID=<test-subscription> AZCLIPS_VM_ADMIN_PASSWORD='<strong-password>' ./tests/Scripts/CLI/VMStop.sh
```

```powershell
$env:AZCLIPS_SUBSCRIPTION_ID = '<test-subscription>'
$env:AZCLIPS_VM_ADMIN_PASSWORD = '<strong-password>'
Import-Module ./src/PS/Accounts/Accounts.psd1
Import-Module ./src/PS/Resources/Resources.psd1
Import-Module ./src/PS/Compute/Compute.psd1
Connect-AzAccount
./tests/Scripts/PS/VMCreate.ps1
./tests/Scripts/PS/VMStop.ps1
```

Expected: each script reports every case as passing and cleans up after itself, as its header describes.

- [ ] **Step 9: Commit the recordings**

```bash
git add tests/Scenario.Tests/SessionRecords/VmCreateScenarioTests/CreatesVirtualMachine.json tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json
git commit -m "{Compute} Re-record the vm create and lifecycle scenarios with the default NSG" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
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
dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj
dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj
```

Expected: all pass.

- [ ] **Step 3: Build and test in Release**

The ADO pipeline runs `dotnet test azclips.slnx -c Release`, where Compute and its test friend are delay-signed.

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj -c Release`
Expected: 5 passed. A `CS0122` ("inaccessible due to its protection level") means the Release `InternalsVisibleTo` key and the test project's signing disagree; compare with `Extensions.Accounts`.

- [ ] **Step 4: Validate help once more**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records.

- [ ] **Step 5: Smoke-run the benchmarks**

Use the `validate-benchmarks` skill.
Expected: the `vm create` / `New-AzVM` benchmarks complete. The stub answers the NSG `PUT` with `{}`, which carries no ID. So, exactly as before this change, the NIC is sent without an NSG reference.

- [ ] **Step 6: Review the branch's scope**

Run: `git --no-pager diff --stat jaysontaimicrosoft-vm-create-public-ip-address...HEAD`. This worktree was created from that branch; substitute your base if it differs.
Expected: only the files in the File Structure table, the two recordings, and the spec and plan under `docs/superpowers/`.

---

## Spec coverage

| Spec requirement | Task |
|---|---|
| `--nsg` / `-SecurityGroupName` replace the old names, optional, bound to `NetworkSecurityGroup`, help text | 4 |
| Behaviour table: omitted → `PUT {vm}NSG` without lookup; `""`/whitespace → none; name → look up, then reuse or create; ID → look up, reuse or fail | 2 (resolver), 4 |
| Explicit names and IDs looked up before anything is created, NSG before public IP; then VNet, NSG and public IP in parallel | 4 |
| New NSG gets legacy CLI's default rule for the image's OS; `Windows` → `rdp`/3389, else `default-allow-ssh`/22 | 3 |
| OS type comes from the VM's image — `Windows` today | 3 |
| Nine `--rule-*` / `-Rule*` parameters removed, including from the dormant `vm nic create-orchestrated` metadata | 3 |
| Shared new-or-existing resolver, public IP behaviour and wording unchanged | 1 (pins), 2 |
| Malformed ID message, missing ID message, 404-only special-casing | 2, 4 |
| `""` sends no `networkSecurityGroup` (`NetworkInterface.cs` omits null) | 4 |
| PowerShell `$null` treated as omitted | 4 (cmdlet forwarding) |
| Dormant `vm nic create-orchestrated` re-points to `NetworkSecurityGroup`; `vm nsg create` and `NewNetworkSecurityGroupProcessor` untouched | 3, 4 |
| `InternalsVisibleTo` + `tests/Extensions.Compute.Tests` in the `Extensions.Accounts.Tests` shape; Linux rule unit-tested | 3 |
| CLI help, PowerShell help, CLI export fixture, wiki | 3, 4, 5 |
| Fake-ARM orchestration tests for every NSG mode, the default rule, PowerShell binding, and public IP regressions | 1, 3, 4 |
| `FakeVmArmBackend`: NSG memory, seeding, IDs from the request path | 1 |
| Live scenarios on the default path, re-recorded; replay scripts follow | 6, 7 |
| Benchmarks on the default path | 6, 8 |
| Smoke scripts renamed; `VMStop` / `VMDelete` drop their rule arguments | 6 |
| `vm delete` / `Remove-AzVM` unchanged | — (no task touches them) |
