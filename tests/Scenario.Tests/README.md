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

The recording under `SessionRecords/<TestClass>/<TestName>.json` is rewritten; review it (no live
ids, tokens, or passwords should appear, the sanitisers scrub them) and commit it. Record from a
single surface: the recording is shared by both the CLI and PowerShell runs on playback. Clean up
any real resources the scenario created.

The vm create scenario (`--filter FullyQualifiedName~VmCreateScenarioTests`) records a whole
orchestration: vnet, nsg, the default public IP, nic, and the vm with its polling, then the detail
reads behind its output (the vm with `$expand=instanceView`, the nic, the public IP), twenty-odd
entries in one file. Before recording it live, the subscription needs the `Microsoft.Compute` and
`Microsoft.Network` providers registered and quota for one small vm and one Standard public IP.
The subscription comes from the signed-in profile. Afterwards delete the resource group the run
created (`scenario-vm-rg`), which removes the vm, its public IP and all its network resources with it.

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
