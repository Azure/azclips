## 1. Scenario test cases

Carry out each task by following `Applying A Scenario-Test Change`, steps 1 and 2, in `.github/skills/azclips-scenario-test-converter/SKILL.md`. Write only `tests/Scenario.Tests/Resources/TenantDeploymentScenarioTests.cs`. `ScenarioHost.cs`, `Scenario.Tests.csproj`, product code, specs and docs stay unchanged, and no recording is written by hand.

The two test methods are independent (design Decision 1). Each opens its own recording, writes and deletes its own template file, builds its own sanitizers, and creates its own hosts. Neither depends on state, setup or a recording of the other. Shared constants and private helper or sanitizer classes in the file are allowed.

Assert on the models in `Microsoft.Azure.Azclips.Extensions.Resources.Models`: `DeploymentValidateResult`, `WhatIfOperationResult`, `DeploymentExtended`, `List<DeploymentExtended>` and `DeploymentTemplateSaveResult`. `deployment tenant export` outputs a `JsonElement`. Never use `rest` or `Invoke-AzRestMethod`.

`<tenant-template>` is the tenant-schema template of design Decision 3:

- parameters `nestedSubId` and `nestedRG`;
- one nested `Microsoft.Resources/deployments` named `sub-nested`, scoped to subscription `[parameters('nestedSubId')]` in `East US 2`, that creates resource group `[parameters('nestedRG')]` in `East US 2`;
- a trailing `//` comment after `resources`.

Each method writes it to its own temp file and deletes it in a `finally`.

- [x] 1.1 **T1**: verifies scenario "T1 Tenant deployment lifecycle: validate, create, list, show, export, delete, then cancel and wait on a second deployment" of `resources-deployments-tests`, under requirement "Create validates, then deploys and waits".
  - Create `tests/Scenario.Tests/Resources/TenantDeploymentScenarioTests.cs`:
    - Class `TenantDeploymentScenarioTests`, `[Collection("Scenario Tests")]`, namespace `Scenario.Tests`.
    - A class summary that states what each method needs to record:
      - T1: `Microsoft.Resources/deployments/write` at tenant root (`/`), and resource-group create rights in the subscription.
      - T2: what-if permission at `/`.
    - `[Fact] TenantDeploymentLifecycle`, with a comment naming T1 and the scenario above.
  - Open this method's own recording: `SessionRecordFiles.OpenSession(typeof(TenantDeploymentScenarioTests), nameof(TenantDeploymentLifecycle), new DefaultRequestMatcher(matchBody: true), sanitizers)`.
    - `sanitizers` holds, in order, the tenant-list trim (keyed on this run's `<deployment>` and `<deployment2>`), the bare-subscription rewrite, and `RecordingSanitizers.Default.Sanitizers` (design Decision 5).
    - Resolve the mode first with `RecorderModeResolver`.
  - Values (design Decision 3):
    - `<subscription>` = `host.LiveSubscriptionId()` in record mode, `00000000-0000-0000-0000-000000000000` in playback.
    - `<rg>` = `TestRun.ScopedName("scenario-tenant-deployment-rg", mode)`.
    - `<deployment>` = `TestRun.ScopedName("scenario-tenant-deployment", mode)`.
    - `<deployment2>` = `TestRun.ScopedName("scenario-tenant-deployment-cancel", mode)`.
    - Location `WestUS`.
  - Record mode, using the `DeploymentOperationScenarioTests` precedent shape (a `workflowSucceeded` flag, `try`/`finally`, and a `try`/`catch` around each cleanup item that logs to `Console.Error`), extended as design Decision 4 describes:
    - Create `ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments])`.
    - In the `try`, run CLI steps 1–11, call `recording.Save()`, and only then set `workflowSucceeded = true`.
    - In the `finally`, run fallback cleanup steps 12–14 in that order on the same CLI host, after the save, so the recording never holds them. For each item:
      - Wrap it in its own `try`/`catch`, so a failure never stops the next item from being tried.
      - Treat `HttpRequestException` with `StatusCode == HttpStatusCode.NotFound` as cleaned up. This covers `<deployment>` already deleted at step 7, and `<rg>` never created because the run failed before step 2.
      - For any other exception, write the item kind, the item name and the full exception to `Console.Error`, and collect it.
    - After all three items, if `workflowSucceeded` is true and any item failed, throw an `AggregateException` naming every failed item.
    - When the workflow failed, throw nothing from the `finally`. The original exception propagates unchanged, and cleanup failures appear only on `Console.Error`.
  - Playback: run CLI steps 1–11 on a CLI host with its own interceptor, then PowerShell steps 1–11 on a PowerShell host with another interceptor. Run no cleanup.
  - CLI steps:
    1. `deployment tenant validate --name <deployment> --location WestUS --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
       - expect: outputs one DeploymentValidateResult whose error is null
       - expect: the result's properties.provisioningState is Succeeded
       - expect: the request is POST /providers/Microsoft.Resources/deployments/<deployment>/validate?api-version=2025-04-01
       - expect: the request body's location is WestUS and properties.mode is Incremental
       - expect: the request body's properties.template equals the JSON content of <tenant-template> with its trailing // comment dropped, and the body has no properties.templateLink
       - expect: the request body's properties.parameters is {"nestedSubId":{"value":"<subscription>"},"nestedRG":{"value":"<rg>"}} and the body has no properties.parametersLink
    2. `deployment tenant create --name <deployment> --location WestUS --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
       - expect: outputs one DeploymentExtended whose name is <deployment>; properties.provisioningState Succeeded; properties.mode Incremental
       - expect: the command sends POST .../<deployment>/validate?api-version=2025-04-01 before PUT .../<deployment>?api-version=2025-04-01, and the validate and PUT request bodies are identical
    3. `deployment tenant list`
       - expect: outputs one List<DeploymentExtended> holding exactly one deployment named <deployment> (after trimming to this scenario)
       - expect: the request is GET /providers/Microsoft.Resources/deployments?api-version=2025-04-01 with no $filter and no $top
    4. `deployment tenant list --filter "provisioningState eq 'Succeeded'"`
       - expect: holds exactly one deployment named <deployment> (after trimming); the request carries $filter set to the URL-escaped expression and no $top
    5. `deployment tenant show --name <deployment>`
       - expect: one DeploymentExtended named <deployment>, provisioningState Succeeded
    6. `deployment tenant export --name <deployment>`
       - expect: outputs one JSON value that is the template itself: it has $schema, a resources array, and no top-level template property
       - expect: its resources array has 1 entry (the nested deployment)
       - expect: the request is POST /providers/Microsoft.Resources/deployments/<deployment>/exportTemplate?api-version=2025-04-01
    7. `deployment tenant delete --name <deployment>`
       - expect: outputs nothing; the request is DELETE /providers/Microsoft.Resources/deployments/<deployment>?api-version=2025-04-01
    8. `deployment tenant create --name <deployment2> --location WestUS --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg> --no-wait`
       - expect: outputs one DeploymentExtended named <deployment2> whose provisioningState is Accepted or Running
       - expect: the command sends POST .../<deployment2>/validate, then PUT .../<deployment2>; the next recorded request after that PUT is step 9's cancel (nothing polled)
    9. `deployment tenant cancel --name <deployment2>`
       - expect: outputs nothing; the request is POST .../<deployment2>/cancel?api-version=2025-04-01
    10. `deployment tenant wait --name <deployment2> --custom "properties.provisioningState=='Canceled'" --interval 1`
        - expect: outputs nothing; every request is GET .../<deployment2>?api-version=2025-04-01 and only the last response has provisioningState Canceled
    11. `deployment tenant show --name <deployment2>`
        - expect: provisioningState is Canceled
    12. (record mode only, in the `finally` after the save) `deployment tenant delete --name <deployment2>`
    13. (record mode only, in the `finally` after the save) `deployment tenant delete --name <deployment>`. Normally NotFound, because step 7 already deleted it; that counts as cleaned up.
    14. (record mode only, in the `finally` after the save) `group delete --subscription-id <subscription> --name <rg>`, the run-scoped group the nested deployment created.
  - PowerShell steps run in playback only, through `RunPowerShell` with the `ExtensionIndex` parameter names shown:
    1. `Test-AzTenantDeployment -Name <deployment> -Location WestUS -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
       - expect: one DeploymentValidateResult, error null, provisioningState Succeeded
    2. `New-AzTenantDeployment -Name <deployment> -Location WestUS -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
       - expect: one DeploymentExtended named <deployment>, provisioningState Succeeded, mode Incremental
    3. `Get-AzTenantDeployment_List`
       - expect: exactly one deployment named <deployment> (after trimming)
    4. `Get-AzTenantDeployment_List -Filter "provisioningState eq 'Succeeded'"`
       - expect: exactly one deployment named <deployment> (after trimming)
    5. `Get-AzTenantDeployment_Get -Name <deployment>`
       - expect: one DeploymentExtended named <deployment>, provisioningState Succeeded
    6. `Save-AzTenantDeploymentTemplate -DeploymentName <deployment> -Path <temp folder> -Force`
       - expect: outputs one DeploymentTemplateSaveResult whose Path is <temp folder>/<deployment>.json; the file holds indented JSON of the template with $schema and a resources array (test deletes it afterwards)
       - expect: the file's resources array has 1 entry (the nested deployment)
    7. `Remove-AzTenantDeployment -Name <deployment>`
       - expect: outputs nothing
    8. `New-AzTenantDeployment -Name <deployment2> -Location WestUS -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>" -NoWait`
       - expect: one DeploymentExtended named <deployment2>, provisioningState Accepted or Running
    9. `Stop-AzTenantDeployment -Name <deployment2>`
       - expect: outputs nothing
    10. `Get-AzTenantDeployment_Get -Name <deployment2>`, repeated until provisioningState is Canceled (replays the GETs CLI step 10 recorded)
        - expect: the last output's provisioningState is Canceled
    11. `Get-AzTenantDeployment_Get -Name <deployment2>`
        - expect: provisioningState is Canceled
  - Review gaps to handle in the code (design Decisions 5–9):
    - **Request checks.** Take request expectations from `RecordPlaybackInterceptor.Sent`, sliced per step. Compare body values against the mode's own subscription and names. Take step 10's response check from the session-record entries that step produced or consumed.
    - **Sanitizers.** The bare-subscription sanitizer rewrites the GUID passed as `nestedSubId` in request and response bodies. Check for any tenant id the default set misses, and extend the scenario-local sanitizer if one appears.
    - **List trimming.** The list trim keeps only this run's `<deployment>` and `<deployment2>`, and runs before the default `run-scoped-name` rewrite. In record mode, assert the list with a name predicate (`Assert.Single(list, d => d.Name == <deployment>)`). In playback, assert the trimmed list is exactly that deployment.
    - **Cancel race.** In step 9, catch `HttpRequestException` containing `DeploymentCannotBeCancelled` and fail with a message asking for a re-record. Do the same in step 8 if `provisioningState` is already terminal.
    - **PowerShell polling.** PowerShell step 10 polls with a bounded attempt count.
    - **Save path.** PowerShell step 6 passes an absolute, existing, unique temp folder as `Path`. It asserts `Path` equals `Path.Combine(folder, "<deployment>.json")`, and deletes the folder in a `finally`.
  - Reference: azure-cli `test_resource.py` `DeploymentTestAtTenantScope.test_tenant_level_deployment`.

- [x] 1.2 **T2**: verifies scenario "T2 Tenant deployment what-if with the default result format" of `resources-deployments-tests`, under requirement "What-if a deployment".
  - In `TenantDeploymentScenarioTests`, add `[Fact] TenantDeploymentWhatIf`, with a comment naming T2 and the scenario above. It is independent of `TenantDeploymentLifecycle`.
  - Open this method's own recording: `SessionRecordFiles.OpenSession(typeof(TenantDeploymentScenarioTests), nameof(TenantDeploymentWhatIf), new DefaultRequestMatcher(matchBody: true), sanitizers)`.
    - `sanitizers` holds the bare-subscription rewrite, then `RecordingSanitizers.Default.Sanitizers` (design Decision 5).
    - It needs no list trim, because T2 sends no list request.
  - Values (design Decision 3):
    - `<subscription>` = `host.LiveSubscriptionId()` in record mode, `00000000-0000-0000-0000-000000000000` in playback.
    - `<rg>` = `TestRun.ScopedName("scenario-tenant-what-if-rg", mode)`. This resource group is never created.
    - `<deployment>` = `TestRun.ScopedName("scenario-tenant-what-if", mode)`.
    - Location `WestUS`.
    - This method writes its own `<tenant-template>` temp file.
  - Record mode: create `ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: [ExtensionUnderTest.ResourcesDeployments])`, run CLI step 1, then `recording.Save()`. There is no cleanup, because what-if creates nothing.
  - Playback: run CLI step 1 on a CLI host with its own interceptor, then PowerShell step 1 on a PowerShell host with another interceptor.
  - CLI steps:
    1. `deployment tenant what-if --name <deployment> --location WestUS --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
       - expect: outputs one WhatIfOperationResult whose status is Succeeded and whose error is null
       - expect: properties.changes is not empty and each has changeType Create
       - expect: the request is POST /providers/Microsoft.Resources/deployments/<deployment>/whatIf?api-version=2025-04-01
       - expect: the request body's properties.whatIfSettings.resultFormat is FullResourcePayloads and properties.mode is Incremental
  - PowerShell steps, playback only:
    1. `Get-AzTenantDeploymentWhatIfResult -Name <deployment> -Location WestUS -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
       - expect: one WhatIfOperationResult, status Succeeded, error null; changes not empty, each changeType Create
  - Review gaps to handle in the code:
    - Take the request expectations from `RecordPlaybackInterceptor.Sent`, and compare body values against the mode's own subscription and names.
    - The bare-subscription sanitizer rewrites the `nestedSubId` GUID. Check for any tenant id the default set misses.
  - Reference: azure-cli `test_resource.py` `DeploymentWhatIfAtTenantScopeTest.test_tenant_level_what_if`.

## 2. Build, review and record

These tasks are steps 3–5 of `Applying A Scenario-Test Change`. Do not report the change ready to archive until task 2.3 is done or the user declines recording.

- [x] 2.1 Step 3, build and replay:
  - Build `tests/Scenario.Tests`.
  - With `AZURE_TEST_MODE` unset, run `dotnet test tests/Scenario.Tests --filter FullyQualifiedName~TenantDeploymentScenarioTests`.
  - Both methods fail until they are recorded. Report each as `recording-required`, and never hand-write, hand-edit or hand-sanitise a recording to make them pass.
  - Confirm with `git status` that the only new or changed file is `tests/Scenario.Tests/Resources/TenantDeploymentScenarioTests.cs`.
- [x] 2.2 Step 4, stop for the user's review of the scenario code with the converter's `Apply Report`:
  - `harness_changes`: none.
  - `coverage_result`: 17 commands in scope (9 CLI, 8 PowerShell), all covered by upstream conversion, none still uncovered.
  - `recordings.new`: `Resources/Records/TenantDeploymentScenarioTests/TenantDeploymentLifecycle.json` and `Resources/Records/TenantDeploymentScenarioTests/TenantDeploymentWhatIf.json`.
  - `record_filter`: `FullyQualifiedName~TenantDeploymentScenarioTests.TenantDeploymentLifecycle` and `FullyQualifiedName~TenantDeploymentScenarioTests.TenantDeploymentWhatIf`, one per run.
  - `live_effects`: T1 creates two tenant-root deployments and one resource group. It deletes `<deployment>` in the workflow, then deletes `<deployment2>`, `<deployment>` (normally already gone) and `<rg>` in the record-mode `finally`. T2 creates nothing.
  - `playback_result`.

  Apply the corrections the user asks for and show the result again, repeating until the user confirms.
- [ ] 2.3 Step 5, record only after that confirmation:
  1. Ask whether to record, naming each scenario's live effects and the permissions it needs (design Risks).
  2. Record only the scenarios the user approves, one `--filter` at a time, following `Record vs. playback` in `tests/Scenario.Tests/README.md`. Confirm the user is signed in with the azclips CLI first.
  3. If T1 fails with `DeploymentCannotBeCancelled` or an already-terminal no-wait create, record T1 again. If T1 fails only with the cleanup `AggregateException`, the saved recording is valid: delete the resources it names by hand instead of re-recording.
  4. Review each rewritten recording:
     - The T1 tenant list responses hold only this scenario's deployments.
     - No live subscription GUID appears, in paths or bare.
     - No tenant id appears.
     - `python3 devops/scripts/ci/scan_recordings.py --base origin/main` reports nothing for the new files.
  5. Re-run playback for each recorded method on its own, then for the whole class.
  6. Report the recordings for the user to review and commit. Also report any `scenario-tenant-*` deployment or resource group the record run left behind (from the `Console.Error` cleanup lines or the cleanup `AggregateException`) that still needs manual cleanup.
