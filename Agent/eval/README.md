# Agent evaluation

TL;DR: run the packaged Agent locally and add `-Upload` to submit the Promptfoo artifact to the evaluation UI.

## Local setup

Use PowerShell 7 and Node.js 24. The Agent executable must come from a full package
built from the source revision you intend to evaluate.

Install dependencies once:

```powershell
Set-Location Agent\eval
npm ci --legacy-peer-deps
```

The builtin Agent model uses AZCLIPS's own Azure sign-in. All ten cases use
deterministic checks and an explicit, scenario-specific LLM judge for recovery
correctness and relevance. Uploading and the Azure judge use the separate classic Azure
CLI credential; signing in to one CLI does not sign in to the other.

### Semantic judge configuration

Every case checks whether the explanation and proposed plan address the user's
goal and actual failure without unsupported claims or unrelated changes.
Config and Extension additionally distinguish a relevant, conditional `config set`
or `extension add` suggestion from a mandatory unrelated change. The presence of a
write command in a proposed plan is not itself a failure or evidence of execution.
The judge receives the original command/error, full response, structured plan,
and recorded tool context. It is not asked to grade prose style or length.

Save the endpoint and **existing deployment name** for an approved Azure OpenAI
resource in `judge.config.local.json`, beside this
README. This is the evaluation client's persistent local configuration, not
AZCLIPS's global configuration or dashboard server configuration. It is ignored
by Git and contains no credentials.

On a new checkout, copy [judge.config.example.json](./judge.config.example.json)
to `judge.config.local.json`, then fill the empty `endpoint` and `deployment`
fields. The example deliberately contains no default resource or judge model.
The optional `apiVersion` and `timeoutMs` fields default to
`2024-12-01-preview` and `60000`.

Both wrapper preflight and Promptfoo resolve this file relative to the evaluation
directory, regardless of the shell's current directory. Missing files are allowed
when environment variables supply the required settings. Invalid JSON, unsupported
fields, unreadable files, and invalid values fail explicitly. Never put an API
key, token, or client secret in this file.

Environment variables override the corresponding local file values, allowing CI
or a one-off local run to use a different deployment without editing the file:

```powershell
$env:AZURE_OPENAI_ENDPOINT = 'https://<approved-resource>.openai.azure.com'
$env:AZURE_OPENAI_DEPLOYMENT = '<existing-judge-deployment>'
$env:AZURE_OPENAI_API_VERSION = '2024-12-01-preview'
```

These PowerShell assignments last only for that shell and its child processes;
the local JSON file persists across shells. Check configuration without logging
in, calling a model, or changing Agent settings with:

```powershell
node .\judge\judge-azure.js --check-config
```

This check validates configuration only, not the actual deployment or inference
permission. The provider receives the resolved settings from Promptfoo; explicit
provider configuration takes precedence over its environment.

The signed-in classic Azure CLI identity must have inference permission for that
resource and be able to acquire a `https://cognitiveservices.azure.com/.default`
token. This is distinct from the dashboard's `Evaluation.Submit` permission and
from the target Agent's builtin APIM authentication. No resource or deployment is
created automatically, and no judge model is silently selected.

The wrapper validates judge configuration before changing Agent settings or
running any target case. Missing configuration fails explicitly; HTTP, token,
timeout, or malformed-response failures are reported by the grader, never
replaced with an automatic passing grade. The offline tests use mocked judges
and are not evidence of a real semantic score. Every baseline result must include
its actual LLM verdict, reason and token usage. Judge token usage is recorded separately from target usage;
target provider latency does not include judge calls.

Use classic Azure CLI to complete the one-time interactive browser login for the evaluation API:

```powershell
$env:AZURE_CORE_ENABLE_BROKER_ON_WINDOWS = 'false'
& "$env:ProgramFiles\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" login `
  --tenant 72f988bf-86f1-41af-91ab-2d7cd011db47 `
  --scope api://d70aaaba-e3a4-4746-bbd3-15ebcd9cb454/access_as_user
```

The internal dashboard grants signed-in users in its configured tenant full
evaluation access without individual app-role assignment. Automated uploaders
still require an explicit `Evaluation.Submit` app role on `azclis-evaluation`.
Token retrieval is bounded to 60 seconds; complete interactive sign-in separately
if the uploader reports that the CLI timed out or requires interaction.
On Windows the helper resolves classic Azure CLI from its standard installation,
including case-insensitive environment variables. It never falls back to bare
`az`, which can invoke the AZCLIPS Agent instead of acquiring a token. On other
platforms, set `AZCLIPS_EVALUATION_AZURE_CLI` to the explicit classic CLI executable.

## Run and upload

The reference suite requires a signed-in active profile. Its VM, deployment, and
VNet fixtures use `azclips-test-rg` by default. Set
`AZCLIPS_EVALUATION_RESOURCE_GROUP` to another group in the active subscription
when needed. Deployment and VNet scenarios retain their existing-group
precondition. The harness does not create fixture groups or execute proposed
resource-creation commands.

By default (`-ResourceCache refresh`), the wrapper runs the product's documented
first-time setup, `az agent tool resource refresh`, in the selected home before it
changes any configuration. It then requires the refreshed cache to contain the
fixture group in the active subscription, or it stops before calling a model.
The product answers lookups from this on-disk cache and refreshes it in the
background only after its TTL expires, so every case sees the same near-real-time
cache. Without the refresh, the first lookup in a new home reads an empty cache
(`cache.resources` is 0) while the background refresh is still running; that miss
reflects a cold start, not a missing group. Use `-ResourceCache existing` only to
measure an unrefreshed cache deliberately or for a target without the refresh
command; the choice is recorded with every result.

The VM scenario accepts the cached-lookup policy confirmed on 2026-10-12:
a cached lookup with no match may lead to proposing `az group create` before
`az vm create`, without a synchronous live refresh. Both the deterministic scope
check and the VM Judge rubric allow this prerequisite. The group must have a
name and location, match the later VM's supplied group, and stay in the recorded
subscription; password placeholders and unrelated-operation restrictions remain.
This allowance does not require a lookup or group creation for every flag-only
repair. Without recorded cache-miss evidence, a complete VM-only repair is valid;
the Judge must not infer a missing group from an empty tool-call history.
The VM and identity scenarios share the prerequisite checks without changing the
identity scenario's policy. This rule correction changes the configured test
identity; keep older artifacts intact and create a new `-Repeat 3` baseline
before comparing optimized builds under the corrected rules.

Use a target package that exposes every configured command, including Network
and managed-identity creation. Verify the target catalog before running models;
an extension missing from the installed package is a prerequisite issue, not a
quality failure. Source metadata and passing offline tests alone do not prove
that the installed target supports the suite.

For latency comparisons, the suite runs one provider call at a time and allows
up to five minutes per call. Promptfoo's duration measures the complete provider
call, including CLI startup, error recovery, and trace extraction; it is not
LLM-only latency. Preserve the same concurrency, timeout, case set, model,
subscription, and cache conditions when comparing an optimized build.

Every result records the facts needed to compare builds:

- `response.metadata.target`: `version` is the `azclipsVersion` reported by
  `agent tool env` (it includes the source commit), and `packageSha256` hashes the
  relative path and SHA-256 of every file in the executable's directory.
  Framework-dependent packages keep the product code in DLLs beside a generic app
  host, so the executable hash alone does not identify a build.
- `response.metadata.resourceCache`: the cache mode, refresh start time and
  duration, cached resource count, and fixture-group check.
- `response.metadata.agentSettings`: the effective `agent.provider`,
  `agent.builtin.*` and recovery settings with their source (`default` or the
  configuration file that set them). The wrapper does not change these settings,
  so an inherited override applies to every observation. A pinned
  `agent.builtin.modelId` also hides a candidate build that only changes the
  default model; keep these settings identical in both arms or remove the override
  in both.
- `response.metadata.timing`: `processMs` is the Agent process duration (the
  troubleshoot process in manual mode). `llmMs` and `llmTurns` cover model turns,
  `toolMs` is the union of tool executions, and `hostMs` is process time spent
  neither waiting for the model nor running tools. `sessionStartMs`,
  `firstLlmRequestMs`, `planProposedMs` (trace start to the accepted plan) and
  `traceMs` locate startup and answer work. A phase that the trace does not report
  is omitted rather than recorded as zero.
- `response.metadata.planActivity`: plan attempts, plans rejected by the host
  (for example a `preToolUse` hook) and their reasons. Rejected plans add model
  turns that the tool-call list does not show.
- `response.metadata.observationStartedAt` and `warmupObservations` place each
  observation in time and record the discarded warm-up count. The Judge's grading
  metadata records its deployment, the model name returned by the service, and its
  request latency.

By default the wrapper first runs one discarded warm-up observation
(`-WarmupObservations 1`) through the same provider and Judge path. In every
recorded 2026-10 run, the first Agent process of a run spent 0.6–4.4 s more host
time than the run's median. The warm-up artifact is written beside the output as
`<name>.warmup.json`, and its traces move to a `warmup-<id>` folder under the trace
path, so they never mix with measured results. If an observation times out, the
provider ends the whole process tree; ending only `az.exe` leaves the Agent child
process running into later observations.

Model turns dominate end-to-end latency. Host time varies less than model time
within one run, but whole runs shift together: in the recorded 2026-10 runs most
of the variance of host time and of total latency came from run-to-run shifts.
One baseline run against one candidate run therefore detects only large changes.
Run the baseline and candidate builds close together (for example baseline,
candidate, candidate, baseline), compare `hostMs` and `llmMs` separately as well as
the total, and do not treat a difference within the spread of repeated runs of
the same build as a speedup.

For the on-demand recovery baseline, use a complete package containing PR #934
and pass `-RecoveryMode manual`. The provider first runs the failing command,
then invokes `az troubleshoot --id <exact-failure-id> --style oneshot`.
It never selects the global latest failure. Duration includes failure recording,
the troubleshoot process, and trace extraction; compare runs using the same mode.
The provider records the original CLI diagnostic in
`response.metadata.originalError` before starting analysis. It is separate from
the proposed plan, assertion failures, and provider/process errors. Existing
credential-shaped redaction also applies to this diagnostic.
The captured response and plaintext diagnostic filter follows the actual
[spinner renderer](../Hosting/ConsoleAgentRenderer.cs): two spaces, a known
[spinner frame](../Hosting/SpinnerService.cs), a space and the status text, then
carriage return, padding of the same UTF-16 length, and another carriage return.
Only complete frame/erase sequences at line boundaries are removed. Ordinary
prose (including lines starting with `Running` or `Planning`), literal Braille,
and unrecognized or incomplete spinner-like text are preserved rather than
guessed to be progress. The existing trace-path removal and whitespace cleanup
remain unchanged. This corrects future captures only; stored responses and
frozen baseline scores are not rewritten or regraded.
The displayed and graded response comes from the renderer's stdout channel.
ConsoleSink diagnostics on stderr (including profile-loading messages and local
paths) are not appended to the answer. Both streams remain available to locate
the trace and capture the original CLI error. No prose-prefix filter is used to
guess which stdout lines are diagnostics.
Project schema `agent-eval-v2` presents this as `Original CLI error` and never
falls back to a grading failure. Cases supply `config.checkName` for readable
checks; the original assertion implementation remains available for diagnostics.
Script-defined checks also export `expectation` alongside their original
pass/score/reason, so the dashboard can show the concrete rule instead of a file
name. This presentation metadata does not change the assertion definition,
grading decision, or suite identity. Older artifacts retain their recorded
declarative contracts; unrecorded script internals must be labelled as missing,
not reconstructed from a model response or an unrelated newer script version.
The target Model field is read only from `LlmTurnCompleted.Model` reported by the
running Agent. A configured model name is not a substitute for observed usage.
Older target packages that omit that field remain explicitly unrecorded; use a
versioned target with the metadata instrumentation for a model-complete baseline.
Reasoning and cache-write totals are present only when every captured usage event
reports them. An absent field is not converted into a reported zero.
An unsuccessful troubleshoot process is an execution error, even if it emitted a
partial plan; it must not be reported as an ordinary assertion failure.
The original automatic mode remains available for compatible older suites.

The PR #934 package uses `vm create --vm-size` and `rest --uri`. Stable 0.1.4
instead uses `vm create --size` and does not start Agent recovery for several
structured errors in this suite; it cannot be substituted for the on-demand
baseline. The REST fixture resolves its URI from the recorded active subscription;
the unsupported `fetch` method is rejected before an HTTP request is sent.

Build and install the intended source revision using the repository packaging
scripts before running the wrapper. It does not build or update the executable,
and an older installed package does not become current when source files change.
Use a full packaged installation, not the unstaged debug CLI layout:

```powershell
Set-Location <repository-root>
.\Agent\eval\run-evaluation.ps1 `
  -AzclipsExe "$env:LOCALAPPDATA\Programs\azcli-dev\az.exe" `
  -RecoveryMode manual `
  -Repeat 3 `
  -Upload
```

`-Repeat` defaults to 1 for compatibility. Use `-Repeat 3` for the baseline and
each candidate build: ten cases produce thirty independently generated answers
and thirty real semantic judgments in one artifact. A repeat is a new target call,
not a reused response or an internal LLM turn. All attempts are retained, including
failures. Compare case-level median and range as well as the overall mean; three
observations per case are not sufficient to claim stable p95 latency.

Freeze the case inputs, rubric version/content, judge deployment, runtime settings,
fixture subscription/group, concurrency, repetition count and cache policy across
baseline and candidate runs. The wrapper records the target version, package
fingerprint, cache state, Agent model settings and runtime-prompt hash in every
result; check that they identify the intended builds and settings before
comparing. Case inputs do not include run provenance, so the suite identity does
not change with the commit, branch or time of a run. Do not present a replay/regrade as
a fresh speed measurement. A provider, judge, or
authentication error invalidates that measurement rather than producing a passing
grade. Never retry until green and discard the failed attempts.

The wrapper uses the package-local Promptfoo version, runs the configured suite
without Promptfoo caching, restores temporary Agent configuration and the
`AZCLIPS_EXE` environment variable, and prints the artifact path and evaluation UI
URL. Promptfoo telemetry and update checks are disabled during evaluation, without
changing AZCLIPS telemetry settings, and their environment overrides are restored
afterwards.

Before applying temporary settings, the wrapper backs up the original
`config.json` to `config.json.evaluation-<id>.bak` in the same directory and prints
the backup path. It uses `AZCLIPS_HOME` when set, otherwise `.azclips` under the
current user's profile directory, matching the target CLI's configuration path.
The whole file is restored byte-for-byte after evaluation or a setup/run failure,
preserving JSON types, formatting, and encoding. If the file did not exist before
the run, the temporary file is removed instead. Successful restoration consumes
the backup; a backup failure stops before changing settings, and a restoration
failure keeps the backup, reports its path, and prevents upload.

Do not change configuration or run another evaluation against the same AZCLIPS
home while the wrapper is running, because restoration replaces the whole file.
If the process is forcibly terminated, automatic restoration may not run. Restore
the printed backup to `config.json` manually before the next run; if the file
originally did not exist, remove the newly created `config.json` instead.

The wrapper captures the target CLI's active subscription before evaluation.
Assertions use that recorded context: an optional `--subscription-id` can be
omitted when the active profile supplies it, but an explicit conflicting value
fails. A missing active profile is a precondition error, not a model quality
score. These temporary context variables are restored afterwards.

Each invocation uses a unique artifact and trace path by default. An explicit
`-OutputPath` must not already exist: the wrapper never overwrites a previous
result or uploads it after a new evaluation fails. Missing, invalid, or empty
artifacts and configuration backup/restoration failures are errors.

Quality failures still produce an artifact and can be uploaded with `-Upload`;
the wrapper preserves Promptfoo's nonzero exit code. Process failures such as a
timeout or a missing executable are execution errors, not ordinary quality
scores. Upload failures are reported explicitly.

Upload metadata identifies the current checkout or CI source revision, not the
provenance of an arbitrary executable. Verify that `-AzclipsExe` points to the
package built from that revision before publishing a baseline. Local source
metadata can be supplied explicitly with `AZCLIPS_EVALUATION_BRANCH`,
`AZCLIPS_EVALUATION_COMMIT`, and `AZCLIPS_EVALUATION_ENVIRONMENT` when the build
source is known independently.

## Offline validation and CI

```powershell
Set-Location Agent\eval
npm test
```

These tests use local fixtures and mocks; they do not sign in to Azure, call
models, or upload results. PowerShell is required for the wrapper lifecycle tests.

The alpha pipeline runs the same offline tests on its shared named Windows pool.
Full model evaluation and upload are intentionally not enabled in CI while
AZCLIPS lacks a supported service-principal login path for the builtin Agent.
No unsupported login command or Azure service connection is used by the offline
stage.

The uploader supports PR, push, manual, and scheduled Azure Pipelines metadata.
PR runs use the PR source commit rather than a synthetic merge commit; other
runs use `BUILD_SOURCEBRANCH` and `BUILD_SOURCEVERSION`. Its existing
`AzurePipelinesCredential` support is independent of the blocked Agent login.

## Deterministic command assertions

`requiredArguments` requires a nonempty argument value by default. Declare
boolean switches such as `--active` in `booleanArguments` to allow a bare switch.
An explicit boolean in `argumentValues` is still checked. `argumentValues` and
`pathArguments` preserve exact values and the supported equivalent path forms;
`match: all` checks every matching command, while `match: any` requires one match.
Invalid assertion configuration or an unparseable command fails explicitly.

See [REFERENCE-CASES.md](./REFERENCE-CASES.md) for the original authoring examples,
the creation-focused baseline, prerequisites, positive/negative checks, and the
boundary between command contracts and optional semantic grading. No judge model
is reported when no LLM grading result exists.