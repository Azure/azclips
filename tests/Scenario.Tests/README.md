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

The Storage data-plane scenarios
(`--filter FullyQualifiedName~StorageDataPlaneScenarioTests`) run Blob and Files commands
against resources that each record run creates and deletes itself. Record mode creates the group
`scenario-storage-dp-rg-t<8 hex>` and a StorageV2 Standard_LRS account `azclipsdpt<8 hex>` with
azclips commands, then the share `scenario-files` and the scenario's containers; it deletes the
group when the run ends, pass or fail. Account names allow no hyphen, so the account carries the
run id after a `t`; the sanitisers strip it and the recordings keep `azclipsdp`. They record from
the CLI surface; playback replays each recording on the CLI and then on PowerShell.
`BlobPowerShellPipeline` records the PowerShell surface itself. Before recording it live, the
signed-in identity needs Storage Blob Data Owner and Storage File Data Privileged Contributor at
subscription scope, because the commands use `--auth-mode login` on an account that does not
exist until the run starts. The tests do not create role assignments. The identity also needs to
create resource groups and list account keys (for example Contributor). Record one scenario at a
time, for example
`--filter FullyQualifiedName~StorageDataPlaneScenarioTests.BlobCopyStart`. If a record run is
interrupted, the group `scenario-storage-dp-rg-t<8 hex>` can be left behind; delete it. The
sanitisers redact SAS signatures and record account keys and the user delegation key as the fake
value `veryFakedStorageAccountKey==`, as azure-cli does. Playback moves the delegation key's
validity window around the current time, because the product checks it against the clock.

The scenarios route all 12 added CLI commands and eight added PowerShell cmdlets through public
command dispatch or the real cmdlet binder. `FileAndDirectory` uses CLI file upload's supported
parent-directory creation for setup; PowerShell replays those directory requests through
`New-AzStorageDirectory`, then exercises `Get-AzStorageFile` listing, exact file/directory lookup,
native directory pipeline input and creation with `-WhatIf`. Cleanup errors fail the run; when
both the workflow and cleanup fail, both exceptions are reported.

Storage CLI metadata declares one default parameter set containing every existing parameter,
with required inputs and aliases unchanged. Missing required inputs are rejected by the shared
parameter-set binder before processor execution. PowerShell keeps its existing cmdlet parameter
sets and native-context handling. Storage data-plane recordings live under
`Storage/Records/StorageDataPlaneScenarioTests/`, following the repository recording helper.

The removed commands' orphaned adapters, stdout-binary plumbing, Files-upload preflight,
CLI directory-create boolean output and account-key enumeration/Kerberos modes are removed.
Retained batch/PowerShell Blob downloads write to destination files. The account-key workflow
still supplies a validated collection for explicit copy-source lookup. Shared Blob/Files
workflow regression tests remain under `Extensions.Storage.Tests`.
Copy scenarios cover unsigned Blob sources, explicit Files
source-key lookup, requested tier, completion and returned length. Positive supplied-source
SAS/key and signed-URI variants remain processor-test coverage gaps at the scenario layer:
the strict command table has no public source-key/SAS fixture generator. Do not add a public
command or a raw-HTTP credential helper to work around that prerequisite. The documented
share/container/snapshot provisioning exception remains unchanged.

Existing recordings are replayed without manual changes. Re-record changed lifecycles only
after separate live-test approval; offline playback is not fresh live acceptance. The four
cleanup regression tests in this class do not contact Azure or create recordings.

The repository's CLI and PowerShell Storage smoke scripts require explicit live-test opt-in. They use a dedicated temporary resource group, separate small Standard LRS destination/Files-source accounts and small payloads. Only the source account enables Shared Key. They check all three Files-copy source modes and then delete and verify removal of their resources, including on failure. The caller-supplied SAS fixture is generated locally by Azure CLI; migrated operations run through the built Azclips CLI or PowerShell module. CLI and PowerShell use separate sign-in applications and both must be authenticated. The scripts never accept an existing resource group for deletion and do not run the separate 5 GiB SHA-256/working-set acceptance test.

The separate [large-transfer harness](../Scripts/StoragePerformance.ps1) requires explicit `-AllowLive` and prior provisioning CLI, Azclips CLI and PowerShell sign-ins. Run it from the repository root after building the solution:

```powershell
.\tests\Scripts\StoragePerformance.ps1 `
    -TenantId '<tenant-id>' -SubscriptionId '<subscription-id>' `
    -CliProfile '<cli-profile>' -PowerShellProfile '<powershell-profile>' `
    -ResultPath '.\storage-performance-results.json' -Location australiaeast -AllowLive
```

Use `-ProvisioningCli` when Azure CLI is not the default `az` executable, and choose a new result filename for each run. The default test performs six actual 5 GiB transfers: Blob upload/download on both hosts and Files upload/download on CLI (15 GiB uploaded and 15 GiB downloaded). CLI Blob downloads use `download-batch` with an exact-name pattern and isolated destination directories. PowerShell Files content transfers and public account-key listing are outside the explicit issue #660 scope; smoke-test source keys come from the provisioning CLI and PowerShell Files-copy fixtures are uploaded through Azclips CLI. Internal workflows retain processor-level regression coverage without public registration. It creates one owned temporary Australian Standard LRS account with Shared Key disabled and cleans up its Azure resources and local payloads even after failure. Azure usage and transfer charges may apply; authorize this separately from small smoke tests.

Each operation uses four workers and at most 8 MiB Blob / 4 MiB Files chunks in a fresh process. After a 1 MiB warmup round trip, the harness takes the median of 20 idle working-set samples, samples the transfer every 25 ms and conservatively includes the OS lifetime peak. The CLI runs its actual public entrypoint in a warmed host with the original CLI runtime configuration. No forced GC, heap overrides or working-set trimming are used. Acceptance requires exact 5 GiB lengths, matching download SHA-256 values and no more than 256 MiB additional working set per operation. Throughput is recorded without a numeric pass threshold. Results and cleanup status are persisted as JSON; a failed transfer or memory limit makes the run fail.

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

## Secret scanning

The sanitisers are the first line of defence; a PR gate is the second. The
`recording-secret-scan` job in `build-and-test.yml` runs
`devops/scripts/ci/scan_recordings.py` against the recordings a PR adds or changes and fails on
anything that looks like a leak: auth headers, bearer tokens or JWTs, non-placeholder
subscription/tenant ids, key material (storage keys, SAS, client secrets, OpenAI keys), passwords.
Findings report file, line and rule only, never the value.

The gate first runs the scanner's regression tests. Storage rules recognize both
literal and JSON-escaped SAS separators and XML delimiters, while allowing the
approved fake keys/identity IDs and redacted signatures.

Run it locally the same way:

```
python3 devops/scripts/ci/scan_recordings.py --all
python3 devops/scripts/ci/scan_recordings.py --base origin/main
python3 -B -m unittest discover -s devops/scripts/ci -p test_scan_recordings.py -v
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
