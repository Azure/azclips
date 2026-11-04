## Context

The active change `resources-deployments-tenant-scope` implements and registers the tenant deployment commands in `src/Extensions/Resources/ExtensionIndex.cs`:

- CLI: `deployment tenant create|validate|what-if|list|show|delete|cancel|export|wait`
- PowerShell: `New-AzTenantDeployment`, `Test-AzTenantDeployment`, `Get-AzTenantDeploymentWhatIfResult`, `Get-AzTenantDeployment_List`, `Get-AzTenantDeployment_Get`, `Stop-AzTenantDeployment`, `Remove-AzTenantDeployment`, `Save-AzTenantDeploymentTemplate`

Its behavior spec is `resources-deployments`. This change adds the paired test spec `resources-deployments-tests` and the tasks that write the scenarios.

The user kept two scenario tests, one per upstream azure-cli tenant test:

- T1 converts `DeploymentTestAtTenantScope.test_tenant_level_deployment`.
- T2 converts `DeploymentWhatIfAtTenantScopeTest.test_tenant_level_what_if`.

Both cases assert on the CLI and PowerShell surfaces, because each command they drive is reachable from both command tables. The PowerShell steps mirror the CLI steps; they are not a conversion of a separate upstream test.

The scenario harness already supports these commands, so this change needs no harness edit:

- `ExtensionUnderTest.ResourcesDeployments` routes every `deployment` command through its root leaf `""`.
- `ExtensionUnderTest.Resources` routes `group`, which T1's cleanup needs.
- `tests/Scenario.Tests/Scenario.Tests.csproj` already references `src/Extensions/Resources/Extensions.Resources.csproj`.

The existing Resources scenarios supply these precedents:

- `DeploymentOperationScenarioTests`:
  - record against real Azure with `useLiveAuth: true` on the CLI surface, then replay one recording on both surfaces;
  - the record-mode cleanup shape: a `workflowSucceeded` flag, `try`/`finally`, and a `try`/`catch` around the cleanup command that logs to `Console.Error`.
- `DeploymentGroupLifecycleScenarioTests`:
  - body-matched playback (`DefaultRequestMatcher(matchBody: true)`);
  - cleanup in a `finally` after `recording.Save()`;
  - the cancel race that fails asking for a re-record.
- `GroupReadScenarioTests`: trims a list response to the scenario's own resources before saving, and asserts with a name predicate in record mode.

## Goals / Non-Goals

**Goals:**
- Cover every tenant deployment command on both surfaces with the two upstream-derived cases. `deployment tenant wait` and `deployment tenant export` are CLI-only, and `Save-AzTenantDeploymentTemplate` is PowerShell-only.
- Keep the two cases independent. Each has its own test method, recording, fixtures, sanitizers, hosts and resource names, and each can be recorded and replayed alone.
- Do setup and cleanup only with first-class Azclips commands.

**Non-Goals:**
- `rest` / `Invoke-AzRestMethod`, anywhere in these scenarios.
- These upstream cases, which are not converted because the user kept only the two upstream azure-cli tenant tests:
  - Validation level: azure-cli `test_tenant_deployment_with_validation_level` and azure-powershell `Test-WhatIfProviderNoRbacAtTenantScope`.
  - Omitted name defaulting to one generated GUID.
  - The `ResourceIdOnly` what-if result format: azure-powershell `Test-WhatIfWithResourceIdOnlyAtTenantScope`.
  - Template Spec. This case is also `prerequisite-missing`: Azclips has no command that creates a Template Spec.
  - Bicep. Its playback also breaks: CI has no Bicep CLI, and the compiled template embeds the Bicep version.
  - azure-powershell `Test-DeploymentEndToEnd-TenantScope`, which T1's PowerShell steps already duplicate.
- The management group, role definition and policy parts of the upstream tenant template. They need `rest` or tenant-level authorization the scenarios do not assume.
- Behavior scenarios that neither case exercises. Decision 10 lists them.
- Product code, the `resources-deployments` behavior spec, and docs.

## Decisions

### 1. One class, two independent methods, two recordings
Create `tests/Scenario.Tests/Resources/TenantDeploymentScenarioTests.cs` with these methods:

| Case | Method | Kind | Recording |
| --- | --- | --- | --- |
| T1 | `TenantDeploymentLifecycle` | `[Fact]` | `Resources/Records/TenantDeploymentScenarioTests/TenantDeploymentLifecycle.json` |
| T2 | `TenantDeploymentWhatIf` | `[Fact]` | `Resources/Records/TenantDeploymentScenarioTests/TenantDeploymentWhatIf.json` |

Each method opens its own `RecordingSession`, writes and deletes its own template temp file, builds its own sanitizer set, and creates its own hosts and interceptors. They share no state, setup or recording.

The class may hold shared constants and helper code, such as the template text and the private sanitizer classes. Nothing created at run time is shared. Either case can be recorded or replayed alone with its own `--filter`.

### 2. Record and playback shape
In record mode, each method runs the CLI surface with `useLiveAuth: true` and calls `recording.Save()`:

- T1 uses `extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments]`.
- T2 uses `extensions: [ExtensionUnderTest.ResourcesDeployments]`.

In playback, each method replays its CLI steps, then its PowerShell steps, each through its own `recording.CreateInterceptor()`.

Both use `DefaultRequestMatcher(matchBody: true)`, as `DeploymentGroupLifecycleScenarioTests` does. The request-content expectations are written for the CLI surface, so body matching is what holds PowerShell to the same template, parameters, mode and result format.

The alternative was path-only matching, which `DeploymentOperationScenarioTests` uses. It was rejected because PowerShell request content would then go unchecked.

### 3. Values and fixtures
Record mode run-scopes each `scenario-*` name with `TestRun.ScopedName`. The default `run-scoped-name` sanitizer strips the suffix again, so the recordings and playback only see base names.

| Placeholder | T1 lifecycle | T2 what-if |
| --- | --- | --- |
| `<subscription>` | `host.LiveSubscriptionId()` in record mode, `00000000-0000-0000-0000-000000000000` in playback | same |
| `<rg>` | `scenario-tenant-deployment-rg` | `scenario-tenant-what-if-rg` (never created) |
| `<deployment>` | `scenario-tenant-deployment` | `scenario-tenant-what-if` |
| `<deployment2>` | `scenario-tenant-deployment-cancel` | not used |
| `<location>` | `WestUS` | `WestUS` |

T2 uses its own nested resource-group name. That name never exists, so what-if always reports the group as `Create`, whatever T1 has done.

Each method writes `<tenant-template>` to its own temp file and deletes it in a `finally`. The template is reconstructed from this description, not copied from upstream:

- It uses the tenant deployment schema (`https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#`, `contentVersion` `1.0.0.0`).
- It declares string parameters `nestedSubId` and `nestedRG`.
- It has one resource, a `Microsoft.Resources/deployments` named `sub-nested`, with `subscriptionId` `[parameters('nestedSubId')]` and `location` `East US 2`.
- The nested deployment runs an inline `Incremental` subscription-schema template, which creates the resource group `[parameters('nestedRG')]` in `East US 2`.
- A trailing `//` comment follows the `resources` array, so T1 step 1 can show the comment is dropped from `properties.template`.
- It needs no management group and no tenant-level role or policy.

### 4. Setup and cleanup without `rest`
Neither case needs a setup resource: the nested deployment creates T1's resource group itself, and T2 creates nothing, so T2 has no cleanup.

**T1's normal flow.** CLI step 7 (`deployment tenant delete --name <deployment>`) and PowerShell step 7 (`Remove-AzTenantDeployment -Name <deployment>`) stay in the workflow with their expectations. They are part of the recording.

**T1's fallback cleanup.** Record mode ends with a `finally` that tries to delete everything the run may have created, whether the workflow passed or failed. It runs on the record-mode CLI host, uses only first-class Azclips commands, and goes in this fixed order:

1. `deployment tenant delete --name <deployment2>`
2. `deployment tenant delete --name <deployment>`
3. `group delete --subscription-id <subscription> --name <rg>`. This is the run-scoped group the nested `sub-nested` deployment created.

Both deployments are deleted before the resource group. A tenant deployment record does not block deleting the group, but the fixed order means every run cleans up the same way.

The structure follows the `DeploymentOperationScenarioTests` shape, extended to several independent items and an aggregate error:

```text
bool workflowSucceeded = false;
try
{
    run CLI steps 1–11 and their record-mode assertions;
    recording.Save();
    workflowSucceeded = true;          // set only after the save
}
finally
{
    var failures = new List<Exception>();
    foreach (item in [<deployment2>, <deployment>, <rg>])      // fixed order
    {
        try { run the item's delete command on the CLI host; }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        { /* already gone: counts as cleaned up */ }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Cleanup of <item kind> '<item name>' failed: {e}");
            failures.Add(new InvalidOperationException("Cleanup of <item kind> '<item name>' failed.", e));
        }
    }
    if (workflowSucceeded && failures.Count > 0)
        throw new AggregateException("Record-mode cleanup failed; delete the named resources manually.", failures);
}
```

Rules:

- **Items are independent.** Each item has its own `try`/`catch`. A failure is collected, and the remaining items are still tried.
- **NotFound counts as cleaned up.** `EnsureSuccessAsync` throws `HttpRequestException` with `StatusCode` set. `HttpStatusCode.NotFound` counts as success. This covers:
  - `DeploymentNotFound` for `<deployment>`, which the normal flow already deleted at step 7;
  - `ResourceGroupNotFound` when the run failed before step 2 created the group.

  A delete that ARM answers with success for a missing resource also counts as cleaned up.
- **Failures are reported explicitly.** Every other failure is written to `Console.Error` with the item kind, the item name and the full exception.
- **A workflow failure is never masked.** The `finally` throws only when `workflowSucceeded` is true, which means no exception is propagating. When the workflow failed, including a failed `recording.Save()`, its original exception propagates unchanged, and cleanup failures appear only on `Console.Error`.
- **A failed cleanup is never silent.** When the workflow succeeded and any item failed, the test fails with the `AggregateException`, which names every failed item. The recording has already been saved and is valid; the failure tells the recorder which resources to delete by hand.
- **Cleanup is never in a recording.**
  - The `finally` runs after `recording.Save()`, and `RecordingSession.Save()` is the only writer. Nothing saves on dispose, so cleanup requests made after the save never reach the file.
  - When the workflow fails before the save, no file is written at all.
  - Playback runs no cleanup, and its recording holds no cleanup requests to replay.

### 5. Scenario-local sanitizers and list trimming
The converter's Target Locations allow edits only to the scenario file, so these sanitizers are private nested classes in `TenantDeploymentScenarioTests.cs`. They implement `IRecordingSanitizer`, and each method passes its own set through `OpenSession(..., sanitizers: ...)`.

- **Bare subscription GUID (T1 and T2).** The default `subscription-id` rule only rewrites a GUID that follows `/subscriptions/`. Both cases send the subscription as the bare `nestedSubId` value, and T1's later responses echo it in `properties.parameters`.
  - A scenario-local sanitizer rewrites that bare GUID to `00000000-0000-0000-0000-000000000000` wherever it appears.
  - It can key on the `nestedSubId` JSON value, or on the live subscription id once it is known. Either way, it must be in place before the first request is recorded, and it must leave the placeholder unchanged, so playback's re-sanitization is a no-op.
  - Playback then sends the placeholder in `nestedSubId`, and the body matches.
- **Tenant id (T1 and T2).** The default set already rewrites `/tenants/<guid>`, `"tenantId": "<guid>"` and authority URLs. After recording, review each file for any other tenant id form, and extend the scenario-local sanitizer if one appears. `devops/scripts/ci/scan_recordings.py` must report nothing for the new recordings.
- **Tenant list trimming (T1 only).** `GET /providers/Microsoft.Resources/deployments` returns every tenant-root deployment in the recorder's tenant. A scenario-local sanitizer, modeled on `ResourceGroupListSanitizer`, drops `value` entries whose `id` does not end with `/providers/Microsoft.Resources/deployments/<deployment>` or `/<deployment2>`.
  - It keys on this run's scoped names: `TestRun.ScopedName(..., mode)`, with the mode resolved through `RecorderModeResolver` before `OpenSession`.
  - It runs before the default `run-scoped-name` rewrite. Build the set as `new RecordingSanitizers([trim, ..RecordingSanitizers.Default.Sanitizers], RecordingSanitizers.Default.RemovedHeaders)`. Otherwise a leftover `scenario-tenant-deployment-t<other run>` would collapse onto the base name and survive the trim.
  - In playback, scoped names equal base names, so the trim stays idempotent.
- **Record-mode list assertions (T1).** Sanitizers only change what is written, so the live CLI output in record mode is untrimmed. Record mode asserts that exactly one entry is named the scoped `<deployment>`, using `Assert.Single(list, predicate)` as `GroupReadScenarioTests` does. Playback asserts that the whole trimmed list is exactly that one deployment.

### 6. Where request and response expectations are checked
Request expectations come from `RecordPlaybackInterceptor.Sent`, which holds every request in send order, in memory and unsanitized. Each step reads only the requests it sent, by taking the count before and after the step, as `SentCount` and `AssertGetsSent` do in `DeploymentOperationScenarioTests`.

Tenant request paths carry no subscription, so paths are asserted in both modes. Body values are compared against the mode's own values: the live subscription and scoped names in record mode, and the placeholder and base names in playback.

T1 step 10's response check ("only the last response has `provisioningState` `Canceled`") reads the response bodies of the session-record entries that step produced in record mode or consumed in playback.

### 7. Wait and PowerShell polling (T1)
`deployment tenant wait` sleeps `--interval` seconds between GETs in both modes. That is its own delay, not the LRO fallback that the offline host zeroes, so T1 passes `--interval 1`.

PowerShell has no wait command. PowerShell step 10 calls `Get-AzTenantDeployment_Get` repeatedly, with a bounded attempt count, until `provisioningState` is `Canceled`. Playback serves the recorded GETs in order, so it consumes exactly the GETs CLI step 10 recorded, and step 11 then consumes the final show.

### 8. Cancel race (T1)
The second deployment only creates an already-existing resource group, so ARM may finish it before the cancel lands. As in `DeploymentGroupLifecycleScenarioTests`, step 9 catches `HttpRequestException` containing `DeploymentCannotBeCancelled` and fails with a message asking for a re-record. Step 8's `Accepted` or `Running` assertion fails the same way if the PUT already returned a terminal state.

### 9. PowerShell Save path (T1)
`RunPowerShell` builds the `CommandInvocation` directly from the `ExtensionIndex` names `DeploymentName`, `Path` and `Force`. It bypasses the cmdlet, which would resolve `Path` against the session's location. So PowerShell step 6 does the following:

1. Creates a unique absolute temp folder, `Path.Combine(Path.GetTempPath(), "azclips-tenant-export-<guid>")`.
2. Passes it as `Path`.
3. Asserts that `DeploymentTemplateSaveResult.Path` equals `Path.Combine(folder, "<deployment>.json")`.
4. Reads and checks the file.
5. Deletes the folder in a `finally`.

### 10. Where each case sits in the test spec, and coverage
Each case is one scenario under the one behavior requirement it is filed against:

- T1 is filed under "Create validates, then deploys and waits". Its upstream test centers on create, and T1 asserts validate-before-PUT with identical bodies.
- T2 is filed under "What-if a deployment".

Commands each case exercises (17 tenant commands in scope):

| Command | T1 lifecycle | T2 what-if |
| --- | --- | --- |
| `deployment tenant validate` / `Test-AzTenantDeployment` | yes | — |
| `deployment tenant create` / `New-AzTenantDeployment` (sync and `--no-wait` / `-NoWait`) | yes | — |
| `deployment tenant list` / `Get-AzTenantDeployment_List` (with and without filter) | yes | — |
| `deployment tenant show` / `Get-AzTenantDeployment_Get` | yes | — |
| `deployment tenant export` (CLI only) | yes | — |
| `Save-AzTenantDeploymentTemplate` (PowerShell only) | yes | — |
| `deployment tenant delete` / `Remove-AzTenantDeployment` | yes | — |
| `deployment tenant cancel` / `Stop-AzTenantDeployment` | yes | — |
| `deployment tenant wait` (CLI only) | yes | — |
| `deployment tenant what-if` / `Get-AzTenantDeploymentWhatIfResult` | — | yes |

That is 9 CLI keys and 8 PowerShell keys. Every one is covered, and no command is left without a scenario.

Other `resources-deployments` requirements the cases also exercise:

- **T1:**
  - Deployment template source: Local JSON template file is inlined.
  - Deployment parameter sources: KEY=VALUE parameter value; Parameter values merge in order over the file.
  - Tenant deployment location and Incremental mode.
  - Create without waiting.
  - Validate a deployment.
  - List tenant deployments: List request, Filter is sent URL-escaped, No filter sends no $filter, $top is never sent.
  - Show a deployment by name.
  - Delete a deployment by name.
  - Cancel a deployment by name.
  - Export only the deployment template.
  - Save a deployment template to a file: Path that is an existing folder, Written file and output.
  - Wait for a deployment condition: Custom condition reaches Canceled.
- **T2:**
  - Deployment template source: Local JSON template file is inlined.
  - Deployment parameter sources: KEY=VALUE parameter value.
  - Tenant deployment location and Incremental mode.
  - What-if a deployment: What-if request and polling, Result format defaults to full resource payloads.

No scenario exercises these any longer:

- `deployment tenant what-if --result-format` / `Get-AzTenantDeploymentWhatIfResult -ResultFormat`. Only the default `FullResourcePayloads` path runs. This leaves "Result format is matched without regard to case" and the `ResourceIdOnly` output shape untested.
- `--validation-level` / `-ValidationLevel` on validate, what-if and create.
- An omitted `--name` / `-Name`, the generated-GUID name. The requirement "Optional deployment name defaults to a GUID" has no scenario test.
- "Deployment parameter sources: No parameter source".
- Template URI, query string, Template Spec, Bicep, parameter files and URIs, missing-file errors, validation and what-if failures, the other `wait` conditions, timeouts, Save's default path and overwrite refusal, and list pagination. No upstream test was converted for these.

## Risks / Trade-offs

- [Recording needs tenant-root deployment rights] → T1 needs `Microsoft.Resources/deployments/write` at `/` and resource-group create in the subscription. T2 needs permission to run what-if at `/`. Record only with an account that has them, and say so when asking whether to record.
- [Cancel race in T1] → The test fails with a re-record message. Re-running the T1 record may take more than one attempt.
- [A T1 record run leaves tenant deployments or the resource group behind] → Every T1 record run, whether it passes or fails, tries the fallback cleanup of `<deployment2>`, `<deployment>` and `<rg>` (Decision 4). Something stays behind only when its own delete fails. That failure is written to `Console.Error`, fails the test when the workflow passed, and appears in the apply report's `live_effects` for manual `deployment tenant delete` / `group delete`.
- [A cleanup failure fails a run whose recording is good] → This is deliberate, so a leak is never silent. The saved recording is still valid. Delete the named resources, then review and commit the recording; no re-record is needed.
- [Other people's data in tenant list responses] → T1's scenario-local trim, plus a review of the recording before commit.
- [Bare subscription or tenant ids in bodies] → The scenario-local sanitizer in both cases, plus `scan_recordings.py`.
- [What-if output varies across re-records] → Assertions avoid exact change counts. They check only "not empty" and each change's `changeType`.
- [Body-matched playback is brittle] → Any change to request-body serialization needs a re-record. This is accepted, as for `DeploymentGroupLifecycleScenarioTests`.
- [Playback time for wait] → `--interval 1` keeps it near one second per recorded GET.

## Informational note

The `resources-deployments` behavior spec states that `properties.validationLevel` is sent only for what-if. Validate and create also send it in the implementation. No case in this change asserts it any longer, so this does not block the change. It remains a possible clarification for the behavior spec owner.
