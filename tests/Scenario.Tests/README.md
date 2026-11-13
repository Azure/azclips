# Scenario tests

Scenario tests exercise a command end to end, through the real dispatcher, argument binders, and
engine pipeline, while the HTTP traffic to Azure is served from a recorded file instead of the
network. They run with no credentials and gate every PR.

## Record vs. playback

The mode is chosen by a single environment variable, `AZURE_TEST_MODE`, mirroring the Azure
PowerShell test framework:

| `AZURE_TEST_MODE` | Behaviour | Credentials |
| ----------------- | --------- | ----------- |
| unset (or `Playback`) | Replay the committed recording; the network is never touched. | none |
| `Record` | Run against the live pipeline and rewrite the recording file. | yes |

An unrecognised value (a typo like `Recrod`) fails fast rather than silently replaying.

### CI

CI leaves `AZURE_TEST_MODE` unset, so every scenario replays its committed recording with no
credentials and no network. Record mode is never enabled in a PR run.

### Locally

Normal runs need nothing; the default is playback:

```
dotnet test tests/Scenario.Tests
```

Offline `ScenarioHost` runs automatically disable the fallback LRO polling delay through the
temporary internal test hook `TEMP_AZCLIPS_PLAYBACK_TEST_POLLING_INTERVAL=0` (seconds). The host restores
the previous environment value on disposal and clears the hook for live recording, preserving the
normal polling interval. Explicit server `Retry-After` delays still take precedence. This is not a
supported product setting; developers and CI do not need to set it.
Live/record scenarios opt into `useLiveAuth: true`, so the host clears the hook and retains the
15-second fallback. Tests recording against fake backends without live auth still use zero delay.
The fake VM backend omits delay headers; the playback-neutralization test adds a nonzero
`Retry-After` to its recording only after capture, so recording does not incur that wait.

To refresh a recording, sign in with the azclips CLI (not the classic Azure CLI; record mode reads
the azclips profile store) and run the single scenario in record mode. The subscription comes from
the signed-in profile; the sanitisers replace it with a placeholder before the file is written.

```
# PowerShell
az login --tenant-id <test-tenant> --subscription-id <test-subscription>
$env:AZURE_TEST_MODE = "Record"
dotnet test tests/Scenario.Tests --filter FullyQualifiedName~GroupCreateScenarioTests
$env:AZURE_TEST_MODE = $null
```

```
# bash
AZURE_TEST_MODE=Record dotnet test tests/Scenario.Tests --filter FullyQualifiedName~GroupCreateScenarioTests
```

The recording under `<Module>/Records/<TestClass>/<TestName>.json`, next to the test that owns
it, is rewritten; review it (no live ids, tokens, or passwords should appear, the sanitisers
scrub them) and commit it. Record from a single surface: the recording is shared by both the CLI
and PowerShell runs on playback. The folder comes from the test's own source location, so moving
a test moves where its recording lands; test class names must stay unique across modules
(`SessionRecordsTests` enforces this), or two tests would overwrite each other's recording.

Record runs name their resource groups with a run-scoped suffix, `scenario-test-rg-t3f8a21c9`,
so simultaneous runs cannot collide, and delete the group when the run ends, pass or fail. The
sanitisers strip the suffix before the file is written, so recordings and playback only ever see
the base name. The id comes from `AZCLIPS_TEST_RUN_ID` when set, otherwise it is generated per
process; everything a test creates matches `scenario-*-t<8 hex>`, which is the shape the live
tier's cleanup sweep is allowed to delete.

The resource-group read scenario
(`--filter FullyQualifiedName~GroupReadScenarioTests.ShowsAndListsResourceGroup`) creates one
empty, run-scoped group and exercises `group show`, `group list`, and both parameter sets of
`Get-AzResourceGroup`, with explicit and profile-default subscriptions. PowerShell playback
invokes the real cmdlet in a runspace, including parameter binding and output enumeration.
Record mode captures the CLI workflow and deletes the group afterwards. List responses retain
only the scenario's group before they are saved. The other `GroupReadScenarioTests` cases use
an injected in-memory HTTP backend for pagination, empty results, errors and invalid parameters;
they do not access Azure, even when `AZURE_TEST_MODE=Record`.

The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: the marketplace image lookups behind `--image`, vnet, the default nsg
(`scenario-vmNSG`, with its `rdp` rule), the default public
IP, nic, and the vm with its polling, then the detail reads behind its output (the vm with
`$expand=instanceView`, the nic, the public IP), twenty-odd entries in one file. Before recording
it live, the subscription needs the `Microsoft.Compute` and `Microsoft.Network` providers
registered and quota for one small vm and one Standard public IP. The subscription comes from the
signed-in profile. The run deletes its resource group afterwards, which removes the vm, its
public IP, its nsg and all its other network resources with it.

The storage account lifecycle scenario
(`--filter FullyQualifiedName~StorageAccountLifecycleScenarioTests`) creates a resource group and
an account, reads it back through show and both list forms, then deletes the account. Storage
account names are globally unique across Azure, so if `azclipslifecycletest` is taken the create
fails with `StorageAccountAlreadyTaken`; change the name in the test and re-record. The scenario
deletes the account itself, so a re-record does not collide with the previous run, but the
resource group (`scenario-storage-lifecycle-rg`) is left behind and should be removed afterwards.

The Storage data-plane lifecycle scenarios
(`--filter FullyQualifiedName~StorageDataPlaneLifecycleScenarioTests`) exercise single and multiple
`--include` values on Blob, Share, and File list operations. Record mode captures the CLI workflow;
playback runs both CLI and PowerShell against the same recordings, including the serialized query
values. File listings also verify the requested timestamps and ETags are returned. Recording requires
`AZCLIPS_TEST_STORAGE_ACCOUNT_KEY` for the account named in the test. The Authorization header is
removed from recordings, and the scenarios clean up their container and share after recording.

The VNet scenarios have two live recordings: `CreateShowListDeleteVirtualNetwork` covers the CLI
dispatcher and PowerShell processor invocations, while
`PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets` runs the real PowerShell cmdlets,
including subnet hashtables with array values, `ResourceName` aliases, and `-Force -PassThru`.
The VNet request-shape and delete-polling tests use a fake ARM backend and capture traffic only in
memory; they do not produce recording JSON files.

The deployment group lifecycle scenario
(`--filter FullyQualifiedName~DeploymentGroupLifecycleScenarioTests`) runs validate, what-if,
create, show, list, export, cancel, and delete against one resource group, then cancels an
in-flight deployment. Before recording it live, the subscription needs the `Microsoft.Storage`
provider registered: what-if links a public quickstart storage template, pinned to a commit on
`raw.githubusercontent.com`, and the in-flight deployment provisions a storage account so it stays
cancellable. If that deployment finishes before the cancel lands, the test fails asking you to
re-record; run it again. The run deletes its resource group afterwards.

The subscription deployment scenario
(`--filter FullyQualifiedName~SubscriptionDeploymentScenarioTests`) records one session at
subscription scope: validate, what-if, create, show, list, export, cancel, and delete, a
cancelled in-flight deployment, and failure cases, followed by parameters, what-if, template spec,
and Bicep parts. Record mode runs the CLI only; playback replays the session through the CLI and
PowerShell, which has no what-if or export cmdlet at this scope. As with the deployment
group scenario, the subscription needs the `Microsoft.Storage` provider registered, because the
in-flight deployment provisions a storage account in `scenario-sub-slow-rg` so it stays
cancellable. The parameters part reads a remote parameters file pinned to an `Azure/azure-cli`
commit and a remote template pinned to an `Azure/azure-quickstart-templates` commit, both on
`raw.githubusercontent.com`. Recording needs the Bicep CLI; playback skips the Bicep part
unless the Bicep CLI is on `PATH` or in `~/.azure/bin`, where `az bicep install` puts it. The run
deletes its deployments and the resource groups it created (`scenario-sub-slow-rg`,
`scenario-sub-template-uri-rg`, `scenario-sub-whatif-rg`, and `scenario-sub-ts-rg`, which holds the
template spec) afterwards.

## Secret scanning

The sanitisers are the first line of defence; a PR gate is the second. The
`recording-secret-scan` job in `build-and-test.yml` runs
`devops/scripts/ci/scan_recordings.py` against the recordings a PR adds or changes and fails on
anything that looks like a leak: auth headers, bearer tokens or JWTs, non-placeholder
subscription/tenant ids, key material (storage keys, SAS, client secrets, OpenAI keys), passwords.
Findings report file, line and rule only, never the value.

Run it locally the same way:

```
python3 devops/scripts/ci/scan_recordings.py --all
python3 devops/scripts/ci/scan_recordings.py --base origin/main
```

Azure CLI runs an incremental secret scan over its recordings; we keep that approach (scan only
what the PR touched) but not their implementation, which is built around vcrpy YAML recordings and
their credential handler. Our recordings are our own JSON, so the rules live in the script above
and grow alongside `RecordingSanitizers`: when a sanitiser learns a new secret shape, the scan
should learn it too.

## Authoring a scenario

Put a new scenario in its module's folder (`Compute`, `Network`, `Resources`,
`ManagedServiceIdentity`, ...; add one for a new module), with any module-specific fake ARM
backend next to it. Cross-surface parity tests go in `Parity`, tests of the record playback
machinery in `Recording`, and shared hosts and helpers in `Infrastructure`. Keep the
`Scenario.Tests` namespace: recordings resolve by class name, so `SessionRecords` stays flat.

```csharp
var recording = SessionRecordFiles.OpenSession(typeof(MyScenarioTests), nameof(MyTest));

if (recording.Mode == RecorderMode.Record)
{
    // useLiveAuth keeps the real profile and token; the stubs would 401 against real ARM.
    using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true);
    // run the command against the live pipeline
    recording.Save();
    return;
}

using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor()))
{
    // replay + assert on the CLI surface
}
using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor()))
{
    // replay + assert on the PowerShell surface
}
```

`RecordingSession` resolves the mode, loads or creates the recording at the canonical path, and
hands out a fresh interceptor per surface (each replays the whole recording independently). `Save`
writes the file in record mode and does nothing in playback.

## Script scenarios

A scenario can also be a script run by a real shell against the real az binary, one process per
command, chaining values the way a user would (see `Scripts/vm-create.ps1` and `.sh`). The
PowerShell module surface works the same way: `Scripts/vm-create-module.ps1` imports the built
modules and drives the real cmdlets, with the subscription resolving from the active profile.

The product carries no test code. The recording assembly lives under `tests/Tests.Recording` and
the runtime injects it into every spawned az or pwsh process through `DOTNET_STARTUP_HOOKS`; with
`AZCLIPS_TEST_RECORDING` set, the hook registers the record playback interceptor as the http
pipeline's terminate handler, the seam the product reserves for mocking. `AZCLIPS_HOME` points
every azclips path (profiles, config, auth context) at an isolated directory so runs never touch
the developer's real state.

`ScriptScenarioRunner` handles the environment layer for a test: temp home, seeded placeholder
profile, a private copy of the recording (playback consumption is tracked in a ledger file next
to it, so it survives across the script's processes), spawn pwsh or bash, capture stdout and the
exit code. Assert on those, plus full ledger consumption to prove the whole recording replayed.
