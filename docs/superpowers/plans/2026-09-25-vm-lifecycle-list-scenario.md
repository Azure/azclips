# VM Lifecycle Scenario: List Steps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `VmLifecycleScenarioTests` to create, list, show, stop, deallocate, start, delete, covering the plain resource-group list and the `--show-details` / `-Status` detail paths on both surfaces.

**Architecture:** One existing xUnit scenario test gains two list steps straight after create, plus three assertion helpers. The CLI surface records a live session once; on playback both the CLI and PowerShell surfaces replay that one recording. The test change and the new recording land in a single commit, because the renamed test resolves to a recording file that only exists after a live record run.

**Tech Stack:** C# (nullable, implicit usings), xUnit, the repo's record/playback harness (`tests/Tests.Recording`, `ScenarioHost`), .NET SDK pinned in `global.json`.

**Design spec:** `docs/superpowers/specs/2026-09-25-vm-lifecycle-list-scenario-design.md`

## Global Constraints

- Only `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` and its recordings under `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/` change.
- **The test change and the new recording are committed together, in Task 3.** Tasks 1 and 2 do not commit: committing either half alone breaks CI playback.
- **Never stage or commit `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`.** It holds an unrelated uncommitted change that belongs to the user. Always `git add` explicit paths; never `git add -A` or `git add .`.
- **Agents never run record mode, sign in to Azure, or create Azure resources.** Task 2 is performed by a human.
- Build and test commands pass `-p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`; without it, a solution build fails with a TLS/SSL handshake error while one project downloads an npm package.
- Scenario values, all existing constants in the test: resource group `scenario-vm-lifecycle-rg`, VM `lifecycle-vm`, location `westus2`, subnet `10.0.0.0/24`, placeholder subscription `00000000-0000-0000-0000-000000000000`.
- The test method is renamed `CreateShowStopDeallocateStartDeleteVirtualMachine` → `CreateListShowStopDeallocateStartDeleteVirtualMachine`.
- CLI list steps use `--resource-group` (AzClips `vm list` has no `-g` alias) and the short alias `-d` for show details.
- The `-UserData` comment and the two regression-guard comments are used verbatim as given in Task 1.
- Out of scope: subscription-wide listing, list-mode `-UserData`, PowerShell parameter-set binding, resource-group cleanup inside the test, a `-g` alias on `vm list`.

---

### Task 1: Add the list steps and assertions to the lifecycle test

**Files:**
- Modify: `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`

**Interfaces:**
- Consumes (existing, unchanged): `ScenarioHost.RunCli(params string[] args) → IReadOnlyList<object>`; `ScenarioHost.RunPowerShell(string commandName, IDictionary<string, object> parameters, string? extension = null) → IReadOnlyList<object>`; `AssertVm(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)` in the same file; `VirtualMachine` model properties `PowerState`, `PublicIps`, `Fqdns`, `PrivateIps`, `MacAddresses`, `OsName`, `OsVersion`, `HyperVGeneration` (all `string?`), `MaintenanceRedeployStatus` (`MaintenanceRedeployStatus?`), `Properties.InstanceView` (`VirtualMachineInstanceView?`); PowerShell engine command `Get-AzVM_List` with parameters `SubscriptionId`, `ResourceGroupName`, `Status`.
- Produces: the renamed test method `CreateListShowStopDeallocateStartDeleteVirtualMachine`, which Task 2 records and Task 3 verifies.

This task follows TDD with the recording as the "implementation": the new test must fail on playback because its recording doesn't exist yet, and Task 2 supplies it.

- [ ] **Step 1: Update the class summary**

In `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`, replace:

```csharp
/// The virtual machine lifecycle: create, show, stop, deallocate, start, delete, driven through the
```

with:

```csharp
/// The virtual machine lifecycle: create, list, show, stop, deallocate, start, delete, driven through the
```

Leave the rest of the summary unchanged.

- [ ] **Step 2: Replace the test method**

Replace the entire existing `[Fact] public void CreateShowStopDeallocateStartDeleteVirtualMachine()` method, from `[Fact]` through its closing brace, with:

```csharp
    [Fact]
    public void CreateListShowStopDeallocateStartDeleteVirtualMachine()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmLifecycleScenarioTests), nameof(CreateListShowStopDeallocateStartDeleteVirtualMachine));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertVm(RunCreate(host, subscriptionId));
            AssertListed(RunList(host, subscriptionId, showDetails: false));
            AssertListedWithDetails(RunList(host, subscriptionId, showDetails: true));
            AssertVm(RunShow(host, subscriptionId));
            AssertActionCompleted(RunStop(host, subscriptionId));
            AssertActionCompleted(RunDeallocate(host, subscriptionId));
            AssertActionCompleted(RunStart(host, subscriptionId));
            AssertActionCompleted(RunDelete(host, subscriptionId));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertListed(RunList(cli, SubscriptionId, showDetails: false), SubscriptionId);
            AssertListedWithDetails(RunList(cli, SubscriptionId, showDetails: true), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertActionCompleted(RunStop(cli, SubscriptionId));
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId));
            AssertActionCompleted(RunStart(cli, SubscriptionId));
            AssertActionCompleted(RunDelete(cli, SubscriptionId));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId), extension: "Compute"), SubscriptionId);
            AssertListed(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: false), extension: "Compute"), SubscriptionId);

            // List-mode -UserData isn't covered: it needs a GET with $expand=userData, which only a
            // PowerShell call makes. Both surfaces replay this one CLI-recorded session, so nothing
            // records that request and its playback would fail.
            AssertListedWithStatus(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: true), extension: "Compute"), SubscriptionId);
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId), extension: "Compute"), SubscriptionId);

            // Stop-AzVM spans both REST operations the CLI splits across two commands, so these two
            // invocations replay the requests `vm stop` and `vm deallocate` recorded, in that order:
            // -StayProvisioned selects powerOff, and the default branch deallocates.
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, stayProvisioned: true), extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, stayProvisioned: false), extension: "Compute"));

            // -Id exercises the orchestrator's resource-id path; it resolves to the same
            // subscription/resource group/name, so it replays the request the CLI recorded.
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId) } }, extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", PsVmParameters(SubscriptionId), extension: "Compute"));
        }
    }
```

The stop, start and delete lines and their comments are unchanged from the current method; only the renamed method, the four list lines, and the `-UserData` comment are new.

- [ ] **Step 3: Add the `RunList` helper**

Immediately after the existing `RunCreate` method and before `RunShow`, add:

```csharp
    /// <summary>
    /// <c>vm list</c> scoped to the scenario's resource group. <paramref name="showDetails"/> passes the
    /// short alias <c>-d</c>, so the step also exercises the alias wiring.
    /// </summary>
    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId, bool showDetails)
    {
        var args = new List<string>
        {
            "vm", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
        };
        if (showDetails)
        {
            args.Add("-d");
        }
        return host.RunCli(args.ToArray());
    }
```

- [ ] **Step 4: Add the `PsListParameters` helper**

Immediately after the existing `PsVmParameters` method, add:

```csharp
    /// <summary>
    /// <c>Get-AzVM_List</c> scoped to the scenario's resource group; <paramref name="status"/> adds
    /// <c>-Status</c>.
    /// </summary>
    private static Dictionary<string, object> PsListParameters(string subscriptionId, bool status)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
        };
        if (status)
        {
            parameters["Status"] = true;
        }
        return parameters;
    }
```

- [ ] **Step 5: Add the three list assertion helpers**

Immediately after the existing `AssertVm` method and before the `<summary>` of `AssertActionCompleted`, add:

```csharp
    /// <summary>
    /// Every list step writes exactly one object, the <c>List&lt;VirtualMachine&gt;</c> the list
    /// orchestrator builds, holding the scenario's single VM. <c>Assert.IsType</c> is an exact match,
    /// so an array, another collection, or VMs written one at a time all fail. Returns the VM so the
    /// detail checks can build on it.
    /// </summary>
    private static VirtualMachine AssertListed(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vms = Assert.IsType<List<VirtualMachine>>(Assert.Single(outputs));
        AssertVm(vms, expectedSubscriptionId);
        return vms[0];
    }

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
        Assert.Null(vm.Properties.InstanceView);

        // Regression guard: can't fail with today's wiring. The CLI has no -Status, so vm list -d passes
        // includeStatusFields: false and must never surface the -Status-only fields.
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);
    }

    /// <summary>
    /// <c>Get-AzVM -Status</c>: the power state and the -Status-only fields come from the instance view.
    /// Of those fields only <c>HyperVGeneration</c> is asserted: the platform reports it, while the
    /// guest agent reports <c>OsName</c> and <c>OsVersion</c> and can lag on a freshly created VM.
    /// </summary>
    private static void AssertListedWithStatus(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.False(string.IsNullOrEmpty(vm.HyperVGeneration));
        Assert.Null(vm.Properties.InstanceView);

        // Regression guard: can't fail with today's wiring. PowerShell has no -ShowDetails, so -Status
        // must never run the NIC lookup that fills the network fields.
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
    }
```

`AssertVm(vms, expectedSubscriptionId)` compiles because `List<VirtualMachine>` converts to `IReadOnlyList<object>` (covariance).

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: `Build succeeded` with 0 errors and no warning pointing at `VmLifecycleScenarioTests.cs`.

- [ ] **Step 7: Run the scenario project in playback and confirm the expected failure**

Run, with `AZURE_TEST_MODE` unset:

```powershell
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: exactly one failure, `Scenario.Tests.VmLifecycleScenarioTests.CreateListShowStopDeallocateStartDeleteVirtualMachine`, with `System.IO.FileNotFoundException` for `...\SessionRecords\VmLifecycleScenarioTests\CreateListShowStopDeallocateStartDeleteVirtualMachine.json`. Every other scenario test passes. Any other failure means the edit broke something: stop and report.

- [ ] **Step 8: Stage the change without committing**

```powershell
git add tests/Scenario.Tests/VmLifecycleScenarioTests.cs
git --no-pager status --porcelain
```

Expected status: `M  tests/Scenario.Tests/VmLifecycleScenarioTests.cs` (staged). If the user's unrelated change to `src/PS/Compute/Cmdlets/GetVMCmdlet.cs` is still present, it shows as ` M` (unstaged) — leave it alone. **Do not commit** — see Global Constraints. Reviewers read this task's diff with `git --no-pager diff --cached`.

---

### Task 2: Record the scenario live

**Performed by a human, not an agent.** It needs an interactive Azure sign-in and creates billable resources for a few minutes.

**Files:**
- Create (written by record mode): `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`

**Interfaces:**
- Consumes: the staged test from Task 1.
- Produces: the new recording, which Task 3 verifies and commits.

Run every command from the repository root in PowerShell.

- [ ] **Step 1: Build, so the azclips CLI is current**

```powershell
dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

- [ ] **Step 2: Sign in with the azclips CLI**

Record mode reads the azclips profile store, not the classic Azure CLI's. Use your test tenant and subscription. The subscription needs the `Microsoft.Compute` and `Microsoft.Network` providers registered and quota for one `Standard_D2s_v3` in `westus2`.

```powershell
.\artifacts\Debug\Core.Cli\az.exe login --tenant-id <your-test-tenant> --subscription-id <your-test-subscription>
```

- [ ] **Step 3: Make sure the scenario resource group doesn't exist**

A leftover VM in the group would make the list return two VMs and fail the single-VM check.

```powershell
.\artifacts\Debug\Core.Cli\az.exe group exists --name scenario-vm-lifecycle-rg --subscription-id <your-test-subscription>
```

If it prints `true`, delete the group and wait for the command to finish:

```powershell
.\artifacts\Debug\Core.Cli\az.exe group delete --name scenario-vm-lifecycle-rg --subscription-id <your-test-subscription>
```

- [ ] **Step 4: Run the scenario in record mode**

```powershell
$env:AZURE_TEST_MODE = "Record"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests" -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
$env:AZURE_TEST_MODE = $null
```

Expected: 1 test passed, after several minutes. Record mode runs only the CLI surface and its assertions against live Azure, including the plain list and `-d` checks.

If it fails: at the list step with two VMs, clean the group (Step 3) and rerun; at any other assertion, stop and hand the full failure output back rather than editing assertions to make it pass.

- [ ] **Step 5: Delete the scenario resource group**

The test deletes the VM but not the NIC, virtual network, network security group, disk or group.

```powershell
.\artifacts\Debug\Core.Cli\az.exe group delete --name scenario-vm-lifecycle-rg --subscription-id <your-test-subscription>
```

- [ ] **Step 6: Confirm the recording was written**

```powershell
Test-Path tests\Scenario.Tests\SessionRecords\VmLifecycleScenarioTests\CreateListShowStopDeallocateStartDeleteVirtualMachine.json
```

Expected: `True`. Hand back to the agent for Task 3.

---

### Task 3: Verify the recording and commit

**Files:**
- Verify: `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`
- Delete: `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateShowStopDeallocateStartDeleteVirtualMachine.json`
- Commit: the above plus `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`

**Interfaces:**
- Consumes: the staged test from Task 1 and the recording from Task 2.
- Produces: one commit containing the test change, the new recording, and the removal of the old recording.

- [ ] **Step 1: Run the scenario in playback**

With `AZURE_TEST_MODE` unset:

```powershell
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests" -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: 1 test passed. This is the first run of the PowerShell surface, including its `-Status` checks.

**If it fails only at `Assert.False(string.IsNullOrEmpty(vm.HyperVGeneration))` in `AssertListedWithStatus`:** first confirm the recorded `$expand=instanceView` response body contains no `hyperVGeneration`. If it doesn't, the recording is correct and the assumption was wrong, so per the spec delete that one assertion line and change the helper's `<summary>` to:

```csharp
    /// <summary>
    /// <c>Get-AzVM -Status</c>: the power state comes from the instance view. The -Status-only fields
    /// aren't asserted: this recording's instance view omits <c>hyperVGeneration</c>, and the guest agent
    /// reports <c>OsName</c> and <c>OsVersion</c>, which can lag on a freshly created VM.
    /// </summary>
```

Then rerun this step, keep the recording, and record the change in your report. Do not re-record. Any other failure: stop and report.

- [ ] **Step 2: Check the recording holds the four new requests**

```powershell
$path = 'tests\Scenario.Tests\SessionRecords\VmLifecycleScenarioTests\CreateListShowStopDeallocateStartDeleteVirtualMachine.json'
$entries = @((Get-Content $path -Raw | ConvertFrom-Json).entries)
$isGet = { param($e) $e.request.method -eq 'GET' }
$list = @($entries | Where-Object { (& $isGet $_) -and ($_.request.uri -split '\?')[0] -like '*/resourceGroups/scenario-vm-lifecycle-rg/providers/Microsoft.Compute/virtualMachines' })
$iv   = @($entries | Where-Object { (& $isGet $_) -and ($_.request.uri -split '\?')[0] -like '*/virtualMachines/lifecycle-vm' -and $_.request.uri -match '(\$|%24)expand=instanceView' })
"list GETs:         $($list.Count) (expect 2)"
"instanceView GETs: $($iv.Count) (expect 1)"
if ($iv.Count -eq 1) {
    $ivIndex = [array]::IndexOf($entries, $iv[0])
    $nicAfter = @($entries[($ivIndex + 1)..($entries.Count - 1)] | Where-Object { (& $isGet $_) -and ($_.request.uri -split '\?')[0] -like '*/networkInterfaces/scenario-lifecycle-nic' })
    "NIC GETs after it: $($nicAfter.Count) (expect 1)"
}
```

Expected: `2`, `1`, `1`. Any other count: stop and report the full request list.

- [ ] **Step 3: Run the recording secret scan**

```powershell
python3 devops/scripts/ci/scan_recordings.py --all
```

Expected: no findings. Any finding: stop and report file, line and rule; do not commit.

- [ ] **Step 4: Remove the old recording**

```powershell
git rm tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateShowStopDeallocateStartDeleteVirtualMachine.json
```

- [ ] **Step 5: Run the whole scenario project**

```powershell
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
```

Expected: 0 failed; the total test count is unchanged from before Task 1.

- [ ] **Step 6: Commit the test, the new recording and the removal together**

```powershell
git add tests/Scenario.Tests/VmLifecycleScenarioTests.cs tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json
git commit -m "{Tests} Add list steps to the VM lifecycle scenario"
git --no-pager show --stat --oneline HEAD
git --no-pager status --porcelain
```

Expected: the commit shows exactly three files — the test (modified), the new recording (added), the old recording (deleted). `git status` shows nothing else from this plan; if the user's unrelated `GetVMCmdlet.cs` change is still present, it remains ` M` (unstaged) and untouched.
