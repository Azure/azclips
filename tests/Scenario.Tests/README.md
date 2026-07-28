# Scenario tests

Scenario tests exercise a command end to end — through the real dispatcher, argument binders, and
engine pipeline — while the HTTP traffic to Azure is served from a recorded file instead of the
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

Normal runs need nothing — the default is playback:

```
dotnet test tests/Scenario.Tests
```

To refresh a recording, sign in to a test subscription and run the single scenario in record mode:

```
# PowerShell
$env:AZURE_TEST_MODE = "Record"
dotnet test tests/Scenario.Tests --filter FullyQualifiedName~GroupCreateScenarioTests
$env:AZURE_TEST_MODE = $null
```

```
# bash
AZURE_TEST_MODE=Record dotnet test tests/Scenario.Tests --filter FullyQualifiedName~GroupCreateScenarioTests
```

The recording under `SessionRecords/<TestClass>/<TestName>.json` is rewritten; review and commit it.
Record from a single surface — the recording is shared by both the CLI and PowerShell runs on
playback.

## Authoring a scenario

```csharp
var recording = RecordingSession.Open(typeof(MyScenarioTests), nameof(MyTest));

if (recording.Mode == RecorderMode.Record)
{
    using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor());
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
