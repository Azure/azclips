# `az vm list` / `Get-AzVM` List Parameter Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `az vm list` and `Get-AzVM`'s list parameter sets the legacy listing parameters — subscription-wide listing, `--vmss`, `--show-details`, `-Name`, `-Location`, `-Status` and a working list-mode `-UserData` — behind one shared orchestrator.

**Architecture:** A new `ListVMProcessor_Orchestrator` owns every client-facing switch: it picks the ARM endpoint, builds the VMSS `$filter`, collects all pages, filters by exact name, fans out per VM for detail switches (capped at 100), then writes once. `ListVMProcessor` keeps only the HTTP shape. Detail enrichment is extracted out of `GetVMProcessor_Orchestrator` into a shared `VirtualMachineDetailEnricher` so `az vm show -d` and `az vm list -d` cannot drift.

**Tech Stack:** C# (nullable enabled, implicit usings), .NET SDK pinned in `global.json`, xUnit (not used here — see Testing Approach), PowerShell binary cmdlets, ARM REST API version `2025-11-01`.

**Design spec:** `docs/superpowers/specs/2026-09-25-vm-list-parameter-migration-design.md`

## Global Constraints

- Build with `dotnet build azclips.slnx` from the repository root. Use the .NET SDK pinned in `global.json`.
- C# with nullable enabled and implicit usings. Follow existing naming, namespace and folder conventions.
- Compute orchestrators live in the `Customization/` folder but declare the namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors`, wrapped in a block-bodied `namespace { }` declaration. The existing `GetVMProcessor_Orchestrator.cs` is the reference.
- Every new file starts with the two-line header: `// Copyright (c) Microsoft Corporation.` then `// Licensed under the MIT License.`
- ARM API version for all Compute virtual-machine calls is `2025-11-01`. Network calls use `2025-05-01`.
- The detail fan-out cap is exactly `100`, counted after name filtering, as a single budget regardless of how many detail switches are set.
- The unavailable-detail sentinel string is exactly `Info Not Available`.
- `az vm show`, `Get-AzVM_Get` and `Get-AzVM_Id` behaviour must not change.
- Do not register `Filter`, `Expand`, or any other orchestrator-internal property in `ExtensionIndex`. Clients must not be able to inject raw ARM query values.
- Wildcards are explicitly out of scope. `-Name` and `-ResourceGroupName` are exact matches only.

## Testing Approach — read this before Task 1

**This plan deliberately does not use TDD.** The user explicitly chose smoke-scripts-only coverage, matching what the preceding `vm show` migration shipped. There is no `tests/Extensions.Compute.Tests` project and you must not create one.

Verification for every code task is:

```bash
dotnet build azclips.slnx
```

Expected: `Build succeeded` with 0 errors. Warnings that already existed on `main` are acceptable; a new warning in a file you touched is not.

End-to-end verification happens once, in Task 9, via the smoke scripts. Those require a real Azure subscription and are run by a human, not by you.

---

### Task 1: Add the runtime plumbing — a collect-only pagination seam and a warning subscriber

Two small runtime prerequisites that later tasks depend on.

The existing pagination helper collects every page then writes immediately, leaving no seam to filter or enrich in between. Split it so the orchestrator can collect, transform, then write.

Separately, `Events.Warning` is declared in `Events.cs` but nothing subscribes to it, and `ILoggingService` exposes only `Info`, `Verbose`, `Debug` and `Error` — no `Warning`. Publishing `Events.Warning` today is a silent no-op, so the 100-VM cap notice in Task 5 would vanish. Rather than widen `ILoggingService` across Core (four files, for one message), subscribe the event to the existing `Info` sink with an explicit prefix.

**Files:**
- Modify: `src/Extensions/ExtensionRuntime/Pagination.cs`
- Modify: `src/Core/Core.Engine/Engine.Subscriptions.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Pagination.CollectWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions) → Task<List<TItem>>`. Note it takes **no** `OutputStreamDelegate` — that is the only signature difference from `WriteOutputWithPaginationAsync`. Also produces a working `Events.Warning` channel that Task 5 publishes to.

- [ ] **Step 1: Replace the body of `Pagination.cs`**

Replace the entire `Pagination` class with the following. The paging loop is moved verbatim into the new method; `WriteOutputWithPaginationAsync` keeps its exact existing signature so its seven callers need no change.

```csharp
public static class Pagination
{
    /// <summary>
    /// Follows every <c>nextLink</c> and returns the accumulated items without writing them.
    /// Use this when a caller needs to filter, sort or enrich the full result set before output;
    /// use <see cref="WriteOutputWithPaginationAsync"/> when the raw set should go straight out.
    /// </summary>
    public static async Task<List<TItem>> CollectWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
    {
        HttpRequestMessage request = await getRequest();
        int page = 1;
        List<TItem> batchedItems = new List<TItem>();
        try
        {
            while ( body is not null && pageItems(body) is {} items)
            {
                batchedItems.AddRange(items);
                if (nextLink(body) is not {} next || string.IsNullOrWhiteSpace(next))
                {
                    break;
                }
                body = default;
                request = request.CloneAndDispose(new Uri(next), HttpMethod.Get);
                await publisher.Publish(Events.PaginationNextPage, new EventData { Message = $"Fetching page {++page} ..." });
                using HttpResponseMessage response = await httpPipeline.SendAsync(request, publisher);
                await publisher.Publish(Events.TracingHttpResponseMessage, response);
                try
                {
                    await response.EnsureSuccessAsync(publisher);
                }
                catch
                {
                    throw;
                }
                string rawBody;
                if (response.Content is null) await publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync())) await publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                else
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<TBody>(rawBody, jsonOptions);
                    }
                    catch (JsonException)
                    {
                        await publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TBody).Name}. Raw body: {rawBody}");
                    }
                }
            }
            return batchedItems;
        }
        finally
        {
            request.Dispose();
        }
    }

    /// <summary>
    /// Follows every <c>nextLink</c> and writes the accumulated items as a single batch.
    /// </summary>
    public static async Task WriteOutputWithPaginationAsync<TBody, TItem>(this TBody? body, Func<TBody, IEnumerable<TItem>?> pageItems, Func<TBody, string?> nextLink, Func<Task<HttpRequestMessage>> getRequest, OutputStreamDelegate writeOutput, IEventPublisher publisher, IRuntimeHttpPipeline httpPipeline, JsonSerializerOptions jsonOptions)
    {
        List<TItem> batchedItems = await body.CollectWithPaginationAsync(pageItems, nextLink, getRequest, publisher, httpPipeline, jsonOptions);
        await writeOutput(batchedItems);
    }
}
```

Leave the file's `using` block and namespace declaration exactly as they are.

- [ ] **Step 2: Wire up the `Events.Warning` channel**

In `src/Core/Core.Engine/Engine.Subscriptions.cs`, find the `Events.PaginationNextPage` subscriber:

```csharp
            subscriber.Subscribe(Events.PaginationNextPage, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info(evt.Message);
            });
```

Immediately after that block — and before the `return subscriber;` that follows it — add:

```csharp
            subscriber.Subscribe(Events.Warning, (data) =>
            {
                var evt = (EventData)data;
                loggingService.Info($"WARNING: {evt.Message}");
            });
```

`ILoggingService` has no `Warning` method, and adding one would mean touching `ILoggingService`, `ConsoleLoggingService`, `ConsoleSink` and `ScopedLoggingService`. Routing to `Info` with an explicit prefix keeps the change to one file. `Info` writes to stderr, so the notice will not corrupt piped JSON output.

- [ ] **Step 3: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Confirm the seven existing callers still compile unchanged**

Run: `git status --porcelain`
Expected: only `src/Extensions/ExtensionRuntime/Pagination.cs` and `src/Core/Core.Engine/Engine.Subscriptions.cs` are listed. If any of `ListResourceGroupProcessor.cs`, `ListVirtualNetworkProcessor.cs`, `ListDiskProcessor.cs`, `ListVMProcessor.cs`, `ListIdentityProcessor.cs`, `ListApplicationProcessor.cs`, `ListGroupMemberProcessor.cs`, `ListGroupProcessor.cs` or `ListUserProcessor.cs` needed editing, you changed the public signature — revert and fix.

- [ ] **Step 5: Commit**

```bash
git add src/Extensions/ExtensionRuntime/Pagination.cs src/Core/Core.Engine/Engine.Subscriptions.cs
git commit -m "{Runtime} Add a collect-only pagination seam and subscribe Events.Warning"
```

---

### Task 2: Extract shared virtual machine detail enrichment

`EnrichAsync` and `ResolveExpand` are private on `GetVMProcessor_Orchestrator`, and the list orchestrator needs both. The two orchestrators inherit different processors, so a shared base class is not available — extract to a static helper instead.

**Files:**
- Create: `src/Extensions/Compute/Customization/VirtualMachineDetailEnricher.cs`
- Modify: `src/Extensions/Compute/Processors/ComputeProcessorBase.cs`
- Modify: `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `VirtualMachineDetailEnricher.ResolveExpand(bool instanceView, bool userData) → string?`
  - `VirtualMachineDetailEnricher.ApplyNetworkAsync(ComputeProcessorBase owner, VirtualMachine vm) → Task`
  - `VirtualMachineDetailEnricher.ApplyInstanceView(VirtualMachine vm, bool includeStatusFields) → void`
  - `ComputeProcessorBase.CreateExtendedProcessor<T>` widened to `protected internal`.

Note `ApplyInstanceView` references `vm.OsName`, `vm.OsVersion`, `vm.HyperVGeneration` and `vm.MaintenanceRedeployStatus`, which Task 3 adds. **Do Task 3 before building this task, or do Tasks 2 and 3 together and build once.** The commit order below assumes you build after Task 3.

- [ ] **Step 1: Widen `CreateExtendedProcessor` so the static helper can compose child processors**

In `src/Extensions/Compute/Processors/ComputeProcessorBase.cs`, change exactly one word on the method declaration:

```csharp
protected internal T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : ComputeProcessorBase, new()
```

It currently reads `protected T CreateExtendedProcessor<T>(...)`. `protected internal` means "protected OR internal", so this only widens access; nothing that compiled before stops compiling. Leave the XML doc comment and the method body untouched.

- [ ] **Step 2: Create the enricher**

Create `src/Extensions/Compute/Customization/VirtualMachineDetailEnricher.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared detail enrichment behind <c>az vm show -d</c>, <c>az vm list -d</c> and
    /// <c>Get-AzVM -Status</c>. Two independent modes, so each caller pays only for what it exposes:
    /// <list type="bullet">
    /// <item><description><see cref="ApplyNetworkAsync"/> resolves NICs and public IPs — <c>--show-details</c> only.</description></item>
    /// <item><description><see cref="ApplyInstanceView"/> flattens the instance view — <c>--show-details</c> and <c>-Status</c>.</description></item>
    /// </list>
    /// Extracted from <c>GetVMProcessor_Orchestrator</c> so the CLI and PowerShell detail views
    /// cannot drift apart.
    /// </summary>
    internal static class VirtualMachineDetailEnricher
    {
        /// <summary>
        /// The one place detail switches turn into an ARM <c>$expand</c>. Mirrors legacy exactly:
        /// power-state views need the instance view, user data needs userData, and both together
        /// ask for one comma-joined expand rather than two round trips.
        /// </summary>
        internal static string? ResolveExpand(bool instanceView, bool userData) => (instanceView, userData) switch
        {
            (true, true) => "instanceView,userData",
            (true, false) => "instanceView",
            (false, true) => "userData",
            _ => null,
        };

        /// <summary>
        /// Resolves every NIC and public IP the VM references, flattening them into the comma-joined
        /// summary fields legacy <c>az vm show -d</c> produces. Blank entries are skipped so a VM
        /// without a public IP yields "" rather than ",".
        /// </summary>
        internal static async Task ApplyNetworkAsync(ComputeProcessorBase owner, VirtualMachine vm)
        {
            var privateIps = new List<string>();
            var publicIps = new List<string>();
            var fqdns = new List<string>();
            var macAddresses = new List<string>();

            foreach (var nicReference in vm.Properties?.NetworkProfile?.NetworkInterfaces ?? [])
            {
                var nicId = VirtualMachineIdResolver.ParseResourceId(
                    nicReference.Id, "Microsoft.Network", "networkInterfaces", "Network interface", "networkInterfaceName");

                using HttpResponseMessage nicResponse = await owner.CreateExtendedProcessor<GetNetworkInterfaceProcessor>(p =>
                {
                    p.SubscriptionId = nicId.SubscriptionId;
                    p.ResourceGroupName = nicId.ResourceGroupName;
                    p.Name = nicId.Name;
                }).SendAsync();

                if (await nicResponse.Content.ReadFromJsonAsync<NetworkInterface>(owner.JsonOptions) is not { } nic)
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

                    using HttpResponseMessage publicIpResponse = await owner.CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
                    {
                        p.SubscriptionId = publicIpId.SubscriptionId;
                        p.ResourceGroupName = publicIpId.ResourceGroupName;
                        p.Name = publicIpId.Name;
                    }).SendAsync();

                    if (await publicIpResponse.Content.ReadFromJsonAsync<PublicIPAddress>(owner.JsonOptions) is not { } publicIp)
                    {
                        continue;
                    }

                    Collect(publicIps, publicIp.Properties?.IpAddress);
                    Collect(fqdns, publicIp.Properties?.DnsSettings?.Fqdn);
                }
            }

            vm.PublicIps = string.Join(",", publicIps);
            vm.Fqdns = string.Join(",", fqdns);
            vm.PrivateIps = string.Join(",", privateIps);
            vm.MacAddresses = string.Join(",", macAddresses);

            static void Collect(List<string> target, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }

        /// <summary>
        /// Flattens the expanded instance view onto the model, then drops it — legacy does the same
        /// once the power state has been read out. <paramref name="includeStatusFields"/> adds the
        /// four fields only <c>Get-AzVM -Status</c> surfaces; <c>--show-details</c> passes false.
        /// </summary>
        internal static void ApplyInstanceView(VirtualMachine vm, bool includeStatusFields)
        {
            VirtualMachineInstanceView? instanceView = vm.Properties?.InstanceView;

            vm.PowerState = string.Join(",", (instanceView?.Statuses ?? [])
                .Where(status => status.Code?.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(status => status.DisplayStatus)
                .Where(displayStatus => !string.IsNullOrWhiteSpace(displayStatus)));

            if (includeStatusFields)
            {
                vm.OsName = instanceView?.OsName;
                vm.OsVersion = instanceView?.OsVersion;
                vm.HyperVGeneration = instanceView?.HyperVGeneration;
                vm.MaintenanceRedeployStatus = instanceView?.MaintenanceRedeployStatus;
            }

            if (vm.Properties is not null)
            {
                vm.Properties.InstanceView = null;
            }
        }
    }
}
```

- [ ] **Step 3: Point `GetVMProcessor_Orchestrator` at the enricher**

In `src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs`:

1. Delete the entire private `ResolveExpand()` method and its XML doc comment.
2. Delete the entire private `EnrichAsync(VirtualMachine vm)` method and its XML doc comment, including the local `static void Collect` function inside it.
3. Replace the `Expand = ResolveExpand();` line at the top of `ProcessAsync()` with:

```csharp
Expand = VirtualMachineDetailEnricher.ResolveExpand(ShowDetails, IncludeUserData);
```

4. Add this private method in place of the deleted `EnrichAsync`, preserving the original network-then-instance-view order:

```csharp
/// <summary>
/// `az vm show -d` wants the full network view plus the power state, and never the four
/// -Status-only fields, so it passes includeStatusFields: false.
/// </summary>
private async Task EnrichAsync(VirtualMachine vm)
{
    await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, vm);
    VirtualMachineDetailEnricher.ApplyInstanceView(vm, includeStatusFields: false);
}
```

5. Remove `using System.Net.Http.Json;` **only if** no other code in the file still uses `ReadFromJsonAsync`. The `Ids` fan-out in `ProcessAsync` does use it, so the using stays. Leave it.

The two call sites of `EnrichAsync` inside `ProcessAsync` and `BeforeWriteOutputAsyncAndReturn` are unchanged.

- [ ] **Step 4: Build (after completing Task 3)**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Extensions/Compute/Customization/VirtualMachineDetailEnricher.cs src/Extensions/Compute/Customization/GetVMProcessor_Orchestrator.cs src/Extensions/Compute/Processors/ComputeProcessorBase.cs
git commit -m "{Compute} Extract shared VM detail enrichment out of the show orchestrator"
```

---

### Task 3: Add the `-Status` model fields and document every synthetic property

`-Status` surfaces four fields beyond what `--show-details` produces. They join the existing synthetic block on one model rather than spawning a second output type.

**Files:**
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `VirtualMachine.OsName`, `VirtualMachine.OsVersion`, `VirtualMachine.HyperVGeneration` (all `string?`), `VirtualMachine.MaintenanceRedeployStatus` (`MaintenanceRedeployStatus?`); `VirtualMachineInstanceView.OsName`, `.OsVersion`, `.HyperVGeneration` (all `string?`), `.MaintenanceRedeployStatus` (`MaintenanceRedeployStatus?`); new public class `MaintenanceRedeployStatus`.

- [ ] **Step 1: Replace the synthetic-field block on `VirtualMachine`**

Replace this block, which currently sits between the `Properties` property and `ToString()`:

```csharp
    // Synthetic fields computed by --show-details rather than returned by ARM. They sit outside
    // Properties so the ARM-sourced payload stays untouched.
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
```

with this. Each property names the exact command, cmdlet, parameter set and parameter that populates it, so the union model stays readable:

```csharp
    // Synthetic fields computed by the detail switches rather than returned by ARM. They sit
    // outside Properties so the ARM-sourced payload stays untouched, and every one is null unless
    // the command named in its comment asked for it.

    /// <summary>
    /// Power state, flattened out of the instance view.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListLocation</c>).
    /// Set to "Info Not Available" when a per-VM lookup failed during a list fan-out.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PowerState { get; set; }

    /// <summary>
    /// Comma-joined public IP addresses across every NIC.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: never populated — there is no PowerShell spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIps { get; set; }

    /// <summary>
    /// Comma-joined fully-qualified domain names across every public IP.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: never populated — there is no PowerShell spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdns { get; set; }

    /// <summary>
    /// Comma-joined private IP addresses across every NIC IP configuration.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: never populated — there is no PowerShell spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIps { get; set; }

    /// <summary>
    /// Comma-joined MAC addresses across every NIC.
    /// CLI: populated by <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: never populated — there is no PowerShell spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddresses { get; set; }

    /// <summary>
    /// Operating system name, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsName { get; set; }

    /// <summary>
    /// Operating system version, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }

    /// <summary>
    /// Hypervisor generation, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HyperVGeneration { get; set; }

    /// <summary>
    /// Maintenance and redeploy status, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaintenanceRedeployStatus? MaintenanceRedeployStatus { get; set; }
```

- [ ] **Step 2: Extend `VirtualMachineInstanceView` with the real ARM fields**

Replace the whole existing class:

```csharp
public class VirtualMachineInstanceView
{
    public List<InstanceViewStatus> Statuses { get; set; } = [];
}
```

with:

```csharp
public class VirtualMachineInstanceView
{
    public List<InstanceViewStatus> Statuses { get; set; } = [];

    // Returned inside $expand=instanceView. Flattened onto VirtualMachine by -Status, then dropped.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HyperVGeneration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaintenanceRedeployStatus? MaintenanceRedeployStatus { get; set; }
}

/// <summary>
/// ARM's maintenance and redeploy status block, returned inside <c>$expand=instanceView</c>.
/// Surfaced by <c>Get-AzVM -Status</c> only.
/// </summary>
public class MaintenanceRedeployStatus
{
    public bool? IsCustomerInitiatedMaintenanceAllowed { get; set; }
    public string? PreMaintenanceWindowStartTime { get; set; }
    public string? PreMaintenanceWindowEndTime { get; set; }
    public string? MaintenanceWindowStartTime { get; set; }
    public string? MaintenanceWindowEndTime { get; set; }
    public string? LastOperationResultCode { get; set; }
    public string? LastOperationMessage { get; set; }
}
```

A property whose name matches its type name is legal C# and already used in this file — `public SecurityProfile? SecurityProfile { get; set; }` on `VirtualMachineProperties`. No alias is needed.

- [ ] **Step 3: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors. This build also covers Task 2.

- [ ] **Step 4: Commit**

```bash
git add src/Extensions/Compute/Models/VirtualMachine.cs
git commit -m "{Compute} Add -Status instance-view fields and document synthetic VM properties"
```

---

### Task 4: Teach `ListVMProcessor` all three list endpoints and `$filter`

**Files:**
- Modify: `src/Extensions/Compute/Processors/ListVMProcessor.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ListVMProcessor.Location` (`string`, default `string.Empty`) and `ListVMProcessor.Filter` (`string?`). `ResourceGroupName` stays `string` defaulting to `string.Empty` but is now optional in meaning — empty selects the subscription-wide endpoint.

- [ ] **Step 1: Replace the file**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists virtual machines from one of three ARM scopes, chosen by which properties are set:
/// location, then resource group, then subscription-wide. All three return the envelope
/// <c>{ "value": [...] }</c>; this processor deserializes it and writes the <c>value</c> array so
/// PowerShell enumerates individual VMs and the CLI serializes a JSON array.
/// <para>
/// This processor owns only the HTTP shape. Every client-facing switch — name filtering, VMSS
/// filtering, and the detail fan-out — lives on <c>ListVMProcessor_Orchestrator</c>.
/// </para>
/// </summary>
public class ListVMProcessor : ComputeProcessorBase<object, VirtualMachineListResult>
{
    private const string ApiVersion = "2025-11-01";

    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Empty selects the subscription-wide endpoint rather than the resource-group one.</summary>
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>When set, wins over <see cref="ResourceGroupName"/> and selects the by-location endpoint.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// ARM <c>$filter</c> value. Set by <c>ListVMProcessor_Orchestrator</c> from the VMSS switch;
    /// deliberately not registered in <c>ExtensionIndex</c>, so no client can pass a raw filter.
    /// </summary>
    public string? Filter { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        string uri = !string.IsNullOrWhiteSpace(Location)
            ? $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/locations/{Location}/virtualMachines?api-version={ApiVersion}"
            : !string.IsNullOrWhiteSpace(ResourceGroupName)
                ? $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version={ApiVersion}"
                : $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/virtualMachines?api-version={ApiVersion}";

        return string.IsNullOrWhiteSpace(Filter) ? uri : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
    }

    private string? nextLink(VirtualMachineListResult body) => body.NextLink;
    private VirtualMachine[]? pageItems(VirtualMachineListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachineListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
```

`nextLink` and `pageItems` keep their existing lowercase names so the orchestrator's override stays diff-friendly against the original file.

- [ ] **Step 2: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Extensions/Compute/Processors/ListVMProcessor.cs
git commit -m "{Compute} Teach ListVMProcessor the subscription, resource-group and location scopes"
```

---

### Task 5: Add the list orchestrator

**Files:**
- Create: `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs`

**Interfaces:**
- Consumes: `Pagination.CollectWithPaginationAsync` (Task 1); `VirtualMachineDetailEnricher.ResolveExpand`, `.ApplyNetworkAsync`, `.ApplyInstanceView` (Task 2); `VirtualMachine.PowerState` and friends (Task 3); `ListVMProcessor.Location`, `.Filter` (Task 4); the existing `VirtualMachineIdResolver.ParseResourceId` and `.ParseVirtualMachineResourceId`; the existing `GetVMProcessor.Expand`.
- Produces: `ListVMProcessor_Orchestrator` with public properties `Name` (`string?`), `Vmss` (`string?`), `ShowDetails` (`bool`), `Status` (`bool`), `IncludeUserData` (`bool`). Task 6 binds these names in `ExtensionIndex`.

- [ ] **Step 1: Create the file**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Shared list orchestrator behind <c>az vm list</c>, <c>Get-AzVM_List</c> and
    /// <c>Get-AzVM_ListLocation</c>. It owns every client-facing switch and reduces them to an
    /// endpoint choice, an optional ARM <c>$filter</c>, an exact-match name filter, and a capped
    /// per-VM fan-out for the detail switches. <c>ExtensionIndex</c> decides which switches each
    /// client exposes.
    /// </summary>
    public class ListVMProcessor_Orchestrator : ListVMProcessor
    {
        /// <summary>Legacy PowerShell caps the -Status fan-out here to avoid throttling; we cap every detail switch.</summary>
        private const int MaxDetailFanOut = 100;

        /// <summary>Legacy's sentinel for a VM whose detail lookup failed.</summary>
        private const string InfoNotAvailable = "Info Not Available";

        /// <summary>Exact-match name filter, applied client-side. Wildcards are deliberately not supported.</summary>
        public string? Name { get; set; }

        /// <summary>VMSS resource id or VMSS name. A name requires a resource group.</summary>
        public string? Vmss { get; set; }

        public bool ShowDetails { get; set; }
        public bool Status { get; set; }
        public bool IncludeUserData { get; set; }

        /// <summary>True when at least one switch needs a per-VM GET.</summary>
        private bool NeedsDetail => ShowDetails || Status || IncludeUserData;

        /// <summary>True when a switch needs the instance view expanded.</summary>
        private bool NeedsInstanceView => ShowDetails || Status;

        public override async Task ProcessAsync()
        {
            ResolveVmssFilter();

            // ParameterMetadata cannot express "this set means list unless both are given", and
            // PowerShell may bind a concrete resource-group-plus-name pair to either the Get or the
            // List set. Branching on the values, exactly as legacy does, makes the outcome identical
            // whichever set won.
            if (string.IsNullOrWhiteSpace(Location)
                && !string.IsNullOrWhiteSpace(ResourceGroupName)
                && !string.IsNullOrWhiteSpace(Name))
            {
                await ListSingleAsync();
                return;
            }

            await base.ProcessAsync();
        }

        /// <summary>
        /// The base pipeline deserializes the first page then hands it here. This is the seam where
        /// the remaining pages are collected, the name filter is applied, and the detail fan-out
        /// runs — all before a single write. Returning true tells the base not to write again.
        /// </summary>
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachineListResult? body)
        {
            List<VirtualMachine> virtualMachines = await body.CollectWithPaginationAsync(
                pageBody => pageBody.Value,
                pageBody => pageBody.NextLink,
                CreateRequestMessage,
                Publisher,
                HttpPipeline,
                JsonOptions);

            if (!string.IsNullOrWhiteSpace(Name))
            {
                virtualMachines = virtualMachines
                    .Where(virtualMachine => string.Equals(virtualMachine.Name, Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            await EnrichAllAsync(virtualMachines);
            await WriteOutput(virtualMachines);
            return true;
        }

        /// <summary>
        /// Turns the VMSS switch into the ARM <c>$filter</c> legacy Azure CLI emits. The value is
        /// quoted: the REST spec's description drops the opening quote but keeps the closing one,
        /// so it is an unbalanced documentation typo rather than a different grammar.
        /// </summary>
        private void ResolveVmssFilter()
        {
            if (string.IsNullOrWhiteSpace(Vmss))
            {
                return;
            }

            string vmssId;

            if (Vmss.Contains('/'))
            {
                var parsed = VirtualMachineIdResolver.ParseResourceId(
                    Vmss, "Microsoft.Compute", "virtualMachineScaleSets", "Virtual machine scale set", "vmScaleSetName");

                vmssId = Vmss.Trim();

                // Legacy takes the resource group from the id when it was not supplied separately.
                if (string.IsNullOrWhiteSpace(ResourceGroupName))
                {
                    ResourceGroupName = parsed.ResourceGroupName;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ResourceGroupName))
                {
                    throw new InvalidOperationException(
                        "Specify the resource group when listing virtual machine instances by scale set name, or pass the scale set resource id instead.");
                }

                vmssId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{Vmss}";
            }

            Filter = $"'virtualMachineScaleSet/id' eq '{vmssId}'";
        }

        /// <summary>
        /// Insurance path for a concrete resource group plus name. Writes a one-element collection
        /// rather than a bare object, because this is still a list command.
        /// </summary>
        private async Task ListSingleAsync()
        {
            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                var results = new List<VirtualMachine>();

                using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                    p.Name = Name!;
                    p.Expand = NeedsDetail ? VirtualMachineDetailEnricher.ResolveExpand(NeedsInstanceView, IncludeUserData) : null;
                }).SendAsync();

                if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is { } virtualMachine)
                {
                    await ApplyDetailAsync(virtualMachine);
                    results.Add(virtualMachine);
                }

                await WriteOutput(results);
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// No list endpoint can expand userData, and none returns NIC or public-IP data, so every
        /// detail switch costs one GET per VM. Legacy accepts that cost and so do we; the cap is
        /// what keeps a subscription-wide listing from melting.
        /// </summary>
        private async Task EnrichAllAsync(List<VirtualMachine> virtualMachines)
        {
            if (!NeedsDetail || virtualMachines.Count == 0)
            {
                return;
            }

            string? expand = VirtualMachineDetailEnricher.ResolveExpand(NeedsInstanceView, IncludeUserData);
            int budget = Math.Min(virtualMachines.Count, MaxDetailFanOut);

            for (int index = 0; index < budget; index++)
            {
                virtualMachines[index] = await EnrichOneAsync(virtualMachines[index], expand);
            }

            if (virtualMachines.Count > MaxDetailFanOut)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"There are more than {MaxDetailFanOut} virtual machines in the result. Only the details of {MaxDetailFanOut} are shown to avoid throttling. Narrow the result with a resource group or a name to get details for the rest."
                });
            }
        }

        /// <summary>
        /// Re-fetches one VM with the resolved expand and returns the detailed model. A failure is
        /// swallowed the way legacy swallows it — one deleted or erroring VM must not fail the whole
        /// listing — but unlike legacy it is published as a warning so the cause is traceable.
        /// </summary>
        private async Task<VirtualMachine> EnrichOneAsync(VirtualMachine virtualMachine, string? expand)
        {
            try
            {
                var vmId = VirtualMachineIdResolver.ParseVirtualMachineResourceId(virtualMachine.Id);

                using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                {
                    p.SubscriptionId = vmId.SubscriptionId;
                    p.ResourceGroupName = vmId.ResourceGroupName;
                    p.Name = vmId.Name;
                    p.Expand = expand;
                }).SendAsync();

                if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is not { } detailed)
                {
                    virtualMachine.PowerState = InfoNotAvailable;
                    return virtualMachine;
                }

                await ApplyDetailAsync(detailed);
                return detailed;
            }
            catch (Exception exception)
            {
                await Publisher.Publish(Events.Warning, new EventData
                {
                    Message = $"Could not retrieve details for virtual machine '{virtualMachine.Name}': {exception.Message}"
                });

                virtualMachine.PowerState = InfoNotAvailable;
                return virtualMachine;
            }
        }

        /// <summary>
        /// Network enrichment is --show-details only; instance-view flattening serves both switches,
        /// and only -Status asks for the four extra fields.
        /// </summary>
        private async Task ApplyDetailAsync(VirtualMachine virtualMachine)
        {
            if (ShowDetails)
            {
                await VirtualMachineDetailEnricher.ApplyNetworkAsync(this, virtualMachine);
            }

            if (NeedsInstanceView)
            {
                VirtualMachineDetailEnricher.ApplyInstanceView(virtualMachine, includeStatusFields: Status);
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

If the compiler reports that `EventData` or `Events` is not found, check the `using` block — `Events` lives in `Core.Abstractions.Runtime` and `EventData` in `Core.Abstractions`. Both are already in the using list above.

- [ ] **Step 3: Commit**

```bash
git add src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs
git commit -m "{Compute} Add the shared VM list orchestrator"
```

---

### Task 6: Wire the new commands into `ExtensionIndex`

**Files:**
- Modify: `src/Extensions/Compute/ExtensionIndex.cs`

**Interfaces:**
- Consumes: `ListVMProcessor_Orchestrator` and its five switch properties (Task 5).
- Produces: CLI command `vm list` and PowerShell commands `Get-AzVM_List` and `Get-AzVM_ListLocation`, all bound to `ListVMProcessor_Orchestrator`. Task 7's cmdlet emits exactly these two PowerShell command names.

Remember: `ParameterMetadata`'s first argument is the **processor property name**, the second is the **client-facing name**. That is how one orchestrator serves two spellings.

- [ ] **Step 1: Replace `s_listVM_PS`**

Find `private static readonly CommandMetadata s_listVM_PS = new(` and replace the whole declaration through its closing `);` with:

```csharp
        private static readonly CommandMetadata s_listVM_PS = new(
            Name: "ListVMProcessor_Orchestrator",
            InvocationName: "Get-AzVM_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name. Omit to list every virtual machine in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "Return only the virtual machine with this exact name. Wildcards are not supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Status"]            = new("Status", "Status", typeof(bool), "Retrieve the power state, OS name, OS version, hypervisor generation and maintenance status of each virtual machine. Command will run slow.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UserData"]          = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listVMByLocation_PS = new(
            Name: "ListVMProcessor_Orchestrator",
            InvocationName: "Get-AzVM_ListLocation",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Location"]       = new("Location", "Location", typeof(string), "List every virtual machine in this location.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Status"]         = new("Status", "Status", typeof(bool), "Retrieve the power state, OS name, OS version, hypervisor generation and maintenance status of each virtual machine. Command will run slow.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UserData"]       = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
```

- [ ] **Step 2: Replace `s_listVM_CLI`**

Find `private static readonly CommandMetadata s_listVM_CLI = new(` and replace the whole declaration through its closing `);` with:

```csharp
        private static readonly CommandMetadata s_listVM_CLI = new(
            Name: "ListVMProcessor_Orchestrator",
            InvocationName: "vm list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.ListVMProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.ListVMProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Omit to list every virtual machine in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["vmss"]            = new("Vmss", "vmss", typeof(string), "List VM instances in a specific VMSS. Please specify the VMSS id or VMSS name.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["show-details"]    = new("ShowDetails", "show-details", typeof(bool), "Show public ip address, FQDN, and power states. Command will run slow.", IsRequired: false, Aliases: ["d"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["d"] = "show-details",
            }
        );
```

- [ ] **Step 3: Register the new PowerShell command in the dispatch table**

In `s_psTable`, immediately after the `["Get-AzVM_List"] = s_listVM_PS,` line, add:

```csharp
            ["Get-AzVM_ListLocation"]      = s_listVMByLocation_PS,
```

`s_cliTable` needs no change — `["vm list"] = s_listVM_CLI` already points at the replaced metadata.

- [ ] **Step 4: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Verify the CLI help reflects the new surface**

Run: `dotnet build azclips.slnx` then `./artifacts/Debug/Core.Cli/az vm list --help`
Expected: the output lists `--resource-group` as optional and shows `--vmss` and `--show-details`/`-d`. On Windows use `.\artifacts\Debug\Core.Cli\az.exe`.

- [ ] **Step 6: Commit**

```bash
git add src/Extensions/Compute/ExtensionIndex.cs
git commit -m "{Compute} Wire az vm list and Get-AzVM list sets to the list orchestrator"
```

---

### Task 7: Extend the `Get-AzVM` cmdlet

**Files:**
- Modify: `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`

**Interfaces:**
- Consumes: the PowerShell command names `Get-AzVM_List` and `Get-AzVM_ListLocation` registered in Task 6. The cmdlet builds its command name as `$"Get-AzVM_{ParameterSetName}"`, so the parameter set names **must** be exactly `List` and `ListLocation`.
- Produces: nothing for later tasks.

- [ ] **Step 1: Relax `ResourceGroupName` and add `Name` to the `List` set**

Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    public string? Name { get; set; }
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "ListLocation", Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter Status { get; set; }
```

- [ ] **Step 2: Add `ListLocation` to `SubscriptionId` and `UserData`**

Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";
```

and replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

- [ ] **Step 3: Bind the two new parameters in `ProcessRecord`**

In `ProcessRecord`, immediately after the `if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))` block, add:

```csharp
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Location)))
        {
            parameters.Add("Location", Location);
        }
```

and immediately after the `if (UserData.IsPresent)` block, add:

```csharp
        if (Status.IsPresent)
        {
            parameters.Add(nameof(Status), true);
        }
```

The existing `$"Get-AzVM_{parameterSetName}"` line then produces `Get-AzVM_List` or `Get-AzVM_ListLocation` with no further change.

- [ ] **Step 4: Build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Verify parameter-set resolution by hand**

This is the risk the design flagged: relaxing `List` makes a concrete resource-group-plus-name pair bindable by both `List` and `Get`.

Run:

```powershell
Import-Module ./src/PS/Compute/Compute.psd1
(Get-Command Get-AzVM).ParameterSets | Select-Object Name, IsDefault
```

Expected: four sets — `List` (IsDefault `True`), `Get`, `Id`, `ListLocation`.

Then confirm the binder does not report ambiguity:

```powershell
Get-AzVM -ResourceGroupName rg -Name vm -WhatIf
```

Expected: no `AmbiguousParameterSet` error. An authentication or network error is fine — you are only testing binding. If PowerShell does report ambiguity, the orchestrator's value-based routing already makes the outcome identical either way, so record the observed set in the commit message and continue.

- [ ] **Step 6: Commit**

```bash
git add src/PS/Compute/Cmdlets/GetVMCmdlet.cs
git commit -m "{Compute} Add -Name, -Location, -Status and list-mode -UserData to Get-AzVM"
```

---

### Task 8: Update the command documentation

**Files:**
- Modify: `src/CLI/Help/Docs/vm/list.md`
- Modify: `src/PS/Compute/help/Get-AzVM.md`

**Interfaces:**
- Consumes: the final parameter surface from Tasks 6 and 7.
- Produces: nothing.

- [ ] **Step 1: Replace `src/CLI/Help/Docs/vm/list.md`**

```markdown
---
summary: List virtual machines.
status: ga
schema: 1.0.0
online version:
---

Description: Return a list of virtual machines, including each machine's name, size, location, and provisioning state. Omit `--resource-group` to list every virtual machine in the subscription. Use this command for inventory, audits, or before bulk operations such as start, stop, or delete.

### List all VMs in the subscription

```bash
az vm list --subscription-id 00000000-0000-0000-0000-000000000000
```

### List VMs in a resource group

```bash
az vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### List VM instances in a scale set by name

```bash
az vm list --resource-group myrg --vmss myvmss
```

### List VM instances in a scale set by resource id

```bash
az vm list --vmss /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachineScaleSets/myvmss
```

### List VMs with public IP addresses, FQDNs and power states

Runs one extra request per virtual machine, so it is slow. Details are fetched for at most 100 virtual machines.

```bash
az vm list --resource-group myrg -d
```

### List using shell variables

```bash
az vm list --resource-group $rg --subscription-id $subId
```
```

- [ ] **Step 2: Update `src/PS/Compute/help/Get-AzVM.md` — syntax blocks**

Replace the `### List (Default)` syntax block:

```
### List (Default)

```
Get-AzVM -ResourceGroupName <string> [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```
```

with:

```
### List (Default)

```
Get-AzVM [-ResourceGroupName <string>] [-Name <string>] [-SubscriptionId <string>] [-Status]
 [-UserData] [-Profile <string>] [<CommonParameters>]
```

### ListLocation

```
Get-AzVM -Location <string> [-SubscriptionId <string>] [-Status] [-UserData]
 [-Profile <string>] [<CommonParameters>]
```
```

- [ ] **Step 3: Update the DESCRIPTION section**

Replace the `## DESCRIPTION` paragraph with:

```
Retrieves details of virtual machines in a subscription.
Run without parameters to list every virtual machine in the subscription, or supply `-ResourceGroupName` to list the virtual machines in one resource group.
Supply `-ResourceGroupName` together with `-Name` to retrieve a single virtual machine, or `-Name` alone to find a virtual machine by exact name across the subscription. Wildcards are not supported.
Use `-Location` to list every virtual machine in a location.
Alternatively, address a single virtual machine directly by its ARM resource ID with `-Id`.
```

- [ ] **Step 4: Add examples**

Immediately after the `### List all VMs in a resource group` example block, insert:

````
### List all VMs in the subscription

Return every virtual machine the subscription contains.

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000
```

### Find a VM by name across the subscription

Return the virtual machine named `myvm`, whichever resource group it lives in.

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -Name myvm
```

### List all VMs in a location

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -Location eastus
```

### List VMs with their power state

Runs one extra request per virtual machine, so it is slow. Details are fetched for at most 100 virtual machines.

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myrg -Status
```
````

- [ ] **Step 5: Update the PARAMETERS section**

Change `-Name`'s `ParameterSets` block from listing only `Get` to:

```yaml
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
```

Change `-ResourceGroupName`'s `List` entry from `IsRequired: true` to `IsRequired: false`.

Append this entry to `-SubscriptionId`'s `ParameterSets` list and to `-Profile`'s (`-Profile` currently uses a single `(All)` entry — leave that one as it is, it already covers every set):

```yaml
- Name: ListLocation
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
```

Append these two entries to `-UserData`'s `ParameterSets` list, which currently holds only `Get` and `Id`:

```yaml
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ListLocation
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
```

Insert two new parameter sections in alphabetical position — `-Location` after `-Id`, and `-Status` after `-Profile`:

````
### -Location

List every virtual machine in this location.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ListLocation
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```
````

````
### -Status

Retrieve the power state, OS name, OS version, hypervisor generation and maintenance status of each virtual machine. Command will run slow. Details are retrieved for at most 100 virtual machines.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ListLocation
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

- [ ] **Step 6: Commit**

```bash
git add src/CLI/Help/Docs/vm/list.md src/PS/Compute/help/Get-AzVM.md
git commit -m "{Compute} Document the new az vm list and Get-AzVM list parameters"
```

---

### Task 9: Add the smoke scripts

**Files:**
- Create: `tests/Scripts/CLI/VMList.sh`
- Create: `tests/Scripts/PS/VMList.ps1`
- Modify: `tests/Scripts/PS/VMShow.ps1`

**Interfaces:**
- Consumes: the finished command surface.
- Produces: nothing.

These scripts need a real subscription and are run by a human. Your job is to write them correctly, not to execute them.

- [ ] **Step 1: Create `tests/Scripts/CLI/VMList.sh`**

```bash
#!/usr/bin/env bash
# Smoke test for the vm list command through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# list workflow:
#   1. vm list -g <rg>          -> resource-group scoped list
#   2. vm list                  -> subscription-wide list
#   3. vm list -g <rg> -d       -> per-VM fan-out for power state and IPs
#   4. vm list --vmss <name>    -> $filter by scale set name
#   5. vm list --vmss <id>      -> $filter by scale set resource id
#   6. vm list --vmss <name>    -> without -g, expected to fail with a usage error
#
# Cases 4 and 5 return an empty array unless the resource group actually holds a
# scale set; that is a successful result for this script, which is checking that
# the filter is accepted rather than that it matches anything.
#
# NOTE: `vm create` in this repo does not allocate a public IP, so case 3
# reports empty publicIps and fqdns. privateIps, macAddresses and powerState are
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
# Usage: ./tests/Scripts/CLI/VMList.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?Set AZCLIPS_SUBSCRIPTION_ID before running}"
LOCATION="${AZCLIPS_LOCATION:-eastus}"
SUFFIX="$$"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-vmlist-rg-$SUFFIX}"
VM_NAME="vmlst-vm-$SUFFIX"
VMSS_NAME="azclips-vmlist-vmss-$SUFFIX"
ADMIN_PASSWORD="${AZCLIPS_VM_ADMIN_PASSWORD:?Set AZCLIPS_VM_ADMIN_PASSWORD before running}"

VMSS_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Compute/virtualMachineScaleSets/$VMSS_NAME"

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
    --network-interface-name "azclips-vmlist-nic-$SUFFIX" \
    --virtual-network-name "azclips-vmlist-vnet-$SUFFIX" \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names "azclips-vmlist-subnet-$SUFFIX" \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name "azclips-vmlist-nsg-$SUFFIX"

echo "== 1. vm list --resource-group (scoped list) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

echo "== 2. vm list (subscription-wide) =="
"$AZ" vm list --subscription-id "$SUBSCRIPTION_ID"

echo "== 3. vm list -d (expect powerState, privateIps, macAddresses) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    -d

echo "== 4. vm list --vmss by name (expect an empty array, not an error) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --vmss "$VMSS_NAME"

echo "== 5. vm list --vmss by resource id (expect an empty array, not an error) =="
"$AZ" vm list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --vmss "$VMSS_ID"

echo "== 6. vm list --vmss by name without --resource-group (expect a usage error) =="
if "$AZ" vm list --subscription-id "$SUBSCRIPTION_ID" --vmss "$VMSS_NAME"
then
    echo "FAIL: expected a usage error when --vmss is a name and --resource-group is missing"
    exit 1
else
    echo "OK: command failed as expected"
fi

echo "== vm list smoke test finished =="
```

- [ ] **Step 2: Create `tests/Scripts/PS/VMList.ps1`**

Reuse the module-resolution guard from `VMShow.ps1` verbatim — it exists because the official Az modules shadow the azclips ones and would otherwise fail confusingly mid-script.

```powershell
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for Get-AzVM's list parameter sets through the built Compute module.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG), then exercises every
# list workflow:
#   1. Get-AzVM -ResourceGroupName        -> resource-group scoped list
#   2. Get-AzVM                           -> subscription-wide list
#   3. Get-AzVM -Name                     -> subscription-wide list, exact name filter
#   4. Get-AzVM -Location                 -> location scoped list
#   5. Get-AzVM -ResourceGroupName -Status -> per-VM fan-out for power state and OS fields
#   6. Get-AzVM -ResourceGroupName -UserData -> per-VM fan-out for user data
#
# -Name takes an exact match only; wildcards are deliberately unsupported.
#
# Prereqs: build the solution with dotnet build, then import the modules:
#     Import-Module ./src/PS/Accounts/Accounts.psd1
#     Import-Module ./src/PS/Resources/Resources.psd1
#     Import-Module ./src/PS/Compute/Compute.psd1
#     Connect-AzAccount
#
# Cleanup: the VM is always removed on exit. The NIC/VNet/NSG and the resource
# group are left behind because this module has no cmdlet for them; remove them
# with the system Azure CLI:
#     az group delete --name <group> --yes
# Set $env:AZCLIPS_DELETE_GROUP = '1' to have this script attempt that automatically.
#
# Usage: ./tests/Scripts/PS/VMList.ps1

$ErrorActionPreference = 'Stop'

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId))
{
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location      = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$suffix        = $PID
$resourceGroup = if ($env:AZCLIPS_RESOURCE_GROUP) { $env:AZCLIPS_RESOURCE_GROUP } else { "azclips-vmlist-rg-$suffix" }
$vmName        = "vmlst-vm-$suffix"

if (-not $env:AZCLIPS_VM_ADMIN_PASSWORD)
{
    throw 'Set AZCLIPS_VM_ADMIN_PASSWORD before running.'
}

# Fail fast with an actionable message. Checking existence is not enough: the official
# Az modules (Az.Resources, Az.Compute) export the same cmdlet names and PowerShell
# auto-loads them on first use, so an un-imported azclips module silently resolves to
# the official one — which rejects -SubscriptionId and fails mid-script.
$requiredCommands = [ordered]@{
    'New-AzResourceGroup' = 'Resources'
    'New-AzVM'            = 'Compute'
    'Get-AzVM'            = 'Compute'
    'Remove-AzVM'         = 'Compute'
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

# Guards the cleanup block: without it, a failure before the VM exists triggers a
# Remove-AzVM that 404s and masks the real error.
$vmCreated = $false

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
        -NetworkInterfaceName "azclips-vmlist-nic-$suffix" `
        -VirtualNetworkName "azclips-vmlist-vnet-$suffix" `
        -AddressPrefixes "10.0.0.0/16" `
        -SubnetNames "azclips-vmlist-subnet-$suffix" `
        -SubnetAddressPrefixes "10.0.2.0/24" `
        -NetworkSecurityGroupName "azclips-vmlist-nsg-$suffix"

    $vmCreated = $true

    Write-Host '== 1. Get-AzVM -ResourceGroupName (scoped list) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup

    Write-Host '== 2. Get-AzVM (subscription-wide list) =='
    Get-AzVM -SubscriptionId $subscriptionId

    Write-Host '== 3. Get-AzVM -Name (exact name filter across the subscription) =='
    Get-AzVM -SubscriptionId $subscriptionId -Name $vmName

    Write-Host '== 4. Get-AzVM -Location (location scoped list) =='
    Get-AzVM -SubscriptionId $subscriptionId -Location $location

    Write-Host '== 5. Get-AzVM -Status (expect powerState and osName) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Status

    Write-Host '== 6. Get-AzVM -UserData (list mode) =='
    Get-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -UserData

    Write-Host '== Get-AzVM list smoke test finished =='
}
finally
{
    if ($vmCreated)
    {
        Write-Host "== cleanup: removing VM $vmName =="
        Remove-AzVM -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $vmName
    }
    else
    {
        Write-Host '== cleanup: skipped, the VM was never created =='
    }

    if ($vmCreated -and $env:AZCLIPS_DELETE_GROUP -eq '1' -and (Get-Command az -ErrorAction SilentlyContinue))
    {
        Write-Host "== cleanup: deleting resource group $resourceGroup =="
        az group delete --subscription $subscriptionId --name $resourceGroup --yes --no-wait
    }
    elseif ($vmCreated)
    {
        Write-Host "== cleanup: remove leftover network resources with: az group delete --name $resourceGroup --yes =="
    }
}
```

- [ ] **Step 3: Fix the now-stale comment in `tests/Scripts/PS/VMShow.ps1`**

That script claims PowerShell has no detail view. `-Status` now exists, so replace:

```
# -ShowDetails has no PowerShell spelling: the design dropped -Status and did not
# add a PowerShell detail view, so the CLI's --show-details is CLI-only.
```

with:

```
# --show-details stays CLI-only. PowerShell's detail view is -Status, which applies
# to the list parameter sets only and is covered by VMList.ps1.
```

- [ ] **Step 4: Mark the shell script executable**

```bash
git update-index --add --chmod=+x tests/Scripts/CLI/VMList.sh
```

- [ ] **Step 5: Final build**

Run: `dotnet build azclips.slnx`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add tests/Scripts/CLI/VMList.sh tests/Scripts/PS/VMList.ps1 tests/Scripts/PS/VMShow.ps1
git commit -m "{Compute} Add az vm list and Get-AzVM list smoke scripts"
```

---

## Post-implementation verification

Run by a human with a live subscription:

```bash
export AZCLIPS_SUBSCRIPTION_ID=<subscription>
export AZCLIPS_VM_ADMIN_PASSWORD=<password>
./tests/Scripts/CLI/VMList.sh
```

```powershell
$env:AZCLIPS_SUBSCRIPTION_ID = '<subscription>'
$env:AZCLIPS_VM_ADMIN_PASSWORD = '<password>'
./tests/Scripts/PS/VMList.ps1
```

Also re-run `./tests/Scripts/CLI/VMShow.sh` and `./tests/Scripts/PS/VMShow.ps1` to confirm the enricher extraction left `vm show` untouched.

Two things to watch for, both flagged as risks in the design:

1. **VMSS `$filter` quoting.** Cases 4 and 5 of the CLI script must return an empty array rather than an ARM `400`. A `400` means ARM wants the unquoted form; drop the inner quotes in `ResolveVmssFilter` and re-run.
2. **PowerShell parameter-set binding.** `Get-AzVM -ResourceGroupName rg -Name vm` must return one VM and must not report `AmbiguousParameterSet`.
