# Reference evaluation cases

The default suite combines three original authoring references with seven
module-baseline cases. Prefer creation scenarios when a module offers a create
command; retain non-create cases for modules without one. This is a sampling
policy, not a measured claim about which commands fail most often.

These are Agent plan evaluations, not command execution tests, and do not claim
exhaustive coverage of a command group.

## What is graded

- Use deterministic assertions for commands, parameter values, plan operations,
  scope, and explicit constraints.
- Prefer the structured `response.metadata.plan` and the shared parser over
  matching the full console transcript.
- Every case also has a separately stated semantic rubric: the recovery must
  address the recorded failure and user goal, preserve intent, and avoid false
  claims of execution or unsupported changes. Code checks alone cannot establish
  that the explanation and plan agree with each other and with the evidence.
- Do not require longer explanations, a particular sentence, or an explanation of
  why every selected value needs confirmation.
- Never remove a valid constraint merely to obtain a green result.

All ten cases include one explicit `llm-rubric` check from
[recovery-quality.js](./judge/recovery-quality.js), version `recovery-quality-v1`.
Each case has its own goal-specific acceptance criteria plus the same shared
evidence, brevity, suggested-value and non-execution rules. Config and Extension
also judge conditional alternatives rather than looking only at a command name.
The suite explicitly wires the configured judge to every case. Configure the existing Azure judge deployment
as described in [README.md](./README.md#semantic-judge-configuration).

## Prerequisites

Run the wrapper against a full package built from the declared revision. The CLI
must have a signed-in active profile and the fixture group must already exist in
that subscription. By default the wrapper synchronously refreshes the target's
ARM resource cache and stops before any configuration change or model call when
the refreshed cache does not contain the fixture group.

The default group is `azclips-test-rg`. Override it before starting the wrapper:

```powershell
$env:AZCLIPS_EVALUATION_RESOURCE_GROUP = '<existing-resource-group>'
```

The wrapper obtains `signed_in` and `subscription_id` from the target CLI. Do not
fake those values to make a test pass. Scope resolution belongs to the actual
command contract: `--subscription-id` is optional for these VM/deployment commands
because the active profile supplies it.

The target package must also expose the Network, managed-identity, and MsGraph
creation commands used below. Check their metadata with `az agent tool help`
before a model baseline run. A command present in source but absent from the
installed catalog is a package prerequisite failure, not an Agent quality score.
Do not replace it with a similarly named classic Azure CLI command.

## The three original references

| Stable ID | Goal and stimulus | Deterministic acceptance |
| --- | --- | --- |
| `profile-show-active` | Show the active profile after an `--actve` typo. | Use the CLI's default-active behavior or a valid explicit active switch, without named selection, unknown flags, or profile/authentication changes. |
| `vm-create-long-flags` | Correct the unsupported short flags while preserving the requested VM name and supplied group. | Correct create command, required values present, user values retained, no unsupported short flags, and unknown password represented by a declared placeholder. Creating the same group first is allowed when a cached lookup reports no match; the prerequisite must have a name/location, precede the VM that uses it, and stay in the recorded subscription. A VM-only repair is equally valid. |
| `deployment-template-recovery` | Correct the `deploment group create` command-group typo while preserving the supplied operation name, local template, and existing group. | Correct group deployment command, original operation name/group/template retained, valid scope, and no added resource operation or destructive Complete mode. |

Verify the failing inputs against the target version's live catalog before a
baseline run. If a future CLI supports the previously invalid input, update the
scenario to a real error condition rather than penalizing valid CLI behavior.

The deployment input retains `group create` so the name's role is unambiguous.
The former `deploy web app --name ...` input was ambiguous: a valid plan
could interpret that name as the web app resource name and choose a different
deployment operation name. The corrected stimulus makes the name's meaning
explicit instead of weakening the exact-value assertion after a model response.

A calibration run also exposed a separate CLI gap: a typo in a nested subcommand
(`deployment group creat`) returned `CommandNotFound` without entering Agent
recovery. That result is retained as an observed product gap, not claimed as
covered or fixed here. This reference preserves the original top-level
unknown-command-group recovery path; it does not substitute for a regression
test or product fix for nested dispatch.

## Creation-focused module baseline

| Stable ID | Goal and stimulus | Deterministic acceptance |
| --- | --- | --- |
| `group-create-missing-location` | Create a named resource group without supplying the required location. | Supply a nonempty location and retain the requested group name. |
| `vnet-create-missing-location` | Create a VNet in an existing group with an explicit address space and DNS server, but omit the required location. | Supply a nonempty location; retain the name, group, address prefix, and DNS server; do not substitute discovery or add unrelated resource changes. |
| `identity-create-missing-rg` | Create a named managed identity in `eastus`, but omit the required resource group. | Supply a nonempty resource group and retain both name and location. Creating that same group first is allowed; the prerequisite must have a name/location and be used by a later identity step in the intended subscription. |
| `msgraph-app-create-missing-display-name` | Register a single-tenant application without the required display name. | Supply a nonempty display name and retain `--sign-in-audience AzureADMyOrg`; do not broaden the audience or add unrelated operations. |
| `config-get-unknown-key` | Read an unknown configuration key. | Include `config info`/`config list` discovery, a read with a corrected key, or conditional creation followed by a read. Reject unchanged missing-key retries, missing values, and invalid prerequisites; the LLM checks whether the suggested key and advice are justified. |
| `extension-show-not-found` | Inspect an extension name that is not installed. | Include installed-extension discovery. A conditional `extension add` suggestion for the intended extension is allowed; the LLM judge evaluates the explanation, rather than treating installation advice as execution. |
| `rest-invalid-method` | Use the unsupported HTTP method `fetch` with a valid `--uri` in the recorded subscription. | Preserve the URI and use explicit GET (case-insensitive) or the supported GET default; permit the equivalent ARM collection keyword spellings `resourcegroups`/`resourceGroups`, but reject other origin/path/query changes, credentials, fragments, non-GET steps and unrelated arguments. |

Compute is already represented by `vm-create-long-flags`; a second disk-list
discovery case is unnecessary. The Network show/list case and MsGraph fake-group-ID
case are replaced, not retained alongside their creation cases. Accounts, Config,
and Extension have no create command in this inventory, so their non-create cases
remain. The additional Resources group/deployment and REST cases are retained.

The four new missing-parameter creation cases deliberately omit one required
value. Their assertions check every matching create step, require nonempty
values, and preserve the values already supplied. They do not prescribe one
specific region, resource group, or display name for the missing parameter.
An identity's resource-group prerequisite is not an unrelated operation merely
because it adds a create step. The group may use a different Azure location from
the identity; the identity's explicitly supplied location still must be retained.
Offline grading does not prove that a selected region/group exists or that a
proposed operation would deploy successfully.

Network parameters follow the [Network registration](../../src/Extensions/Network/ExtensionIndex.cs),
and identity parameters follow the [Identity registration](../../src/Extensions/Identity/ExtensionIndex.cs).
Recheck the target package before a real run. The original typo-based stimuli
do not imply a typo requirement for the new baseline cases. The on-demand
PR #934 package uses `--vm-size` for VM creation and `--uri` for REST.
Stable release 0.1.4 uses `--size` instead and skips automatic recovery for
missing-required-parameter errors; it is not interchangeable with that package.

## Assertion responsibilities

`command-set` checks required value-taking arguments, boolean switches, exact user
values, supported equivalent path forms, and prohibited flags. Current CLI
`profile show` defaults to the active profile when no name is supplied, so the
old missing-selection stimulus is no longer an error. The reference now uses a
flag typo and accepts the supported implicit or explicit active selection.

`plan-contract` limits the reference plan to its intended operation, validates
the recorded subscription context, and rejects a fabricated or undeclared secret.
It does not grade explanatory prose or a conservative risk label on an otherwise
allowed operation. VM and identity plans use `resource-group-plan`, which applies
the same contract and additionally allows a complete `az group create` for the
group used by a later VM or identity step.

Deterministic checks verify required, forbidden, and exact argument values. They
do not validate the complete argument set of every step, so an extra unsupported
flag on an allowed command is left to the semantic Judge's CLI-contract criterion.

VM and deployment plans may omit the subscription flag in this signed-in
reference context. An explicit value must match that context. Missing context is
not a license to omit an otherwise unresolved required value.

## Positive and negative examples

The offline tests exercise the same assertion functions used by Promptfoo.

- Profile: accept default-active and valid explicit selection; reject invalid
  flags/boolean values, named selection, help-only output, and added removal.
- VM: accept complete long flags and a declared password placeholder, with or
  without a complete prerequisite that creates the same group first; reject
  missing values, changed user input, short flags, invented passwords, wrong
  explicit subscriptions, unused or incomplete group prerequisites, and a group
  created after the VM.
- Deployment: accept the current-profile default and equivalent relative path
  separators; reject a different subscription/template, missing parameters,
  unrelated resource creation, and Complete mode.
- Group/VNet/identity/application creation: reject the unchanged failing input
  and any invalid create step even when another step is valid. Accept different
  supplied values for the missing field; reject empty values or changes to the
  supplied name, network settings, identity location, or application audience.
- VNet/application: reject discovery-only plans and unrelated resource changes.
- Identity: accept a complete matching resource-group prerequisite before the
  identity; reject unused groups, missing prerequisite values, and changed
  subscriptions without forbidding the legitimate create sequence.
- REST: accept explicit GET in any casing or its default; reject changed URIs,
  unsupported argument names, and any non-GET step even after a valid one.
- Config/Extension: structural checks accept usable recovery plus optional set/add
  advice and still reject removal or incomplete recovery. Config accepts a corrected
  key read without requiring `config list`; Extension still requires installed
  extension discovery. Their semantic rubric
  checks the full response and conditional labels, not write-command keywords.
  Mock judge tests validate wiring and failure propagation, not model quality.
- Context: reject an absent active profile rather than treating an undefined
  subscription as a valid default.

Each new case should carry `case_id`, `command_group`, a concise `goal`, a
controlled failing command, declared prerequisites, and positive/negative
fixtures. Register it explicitly in the suite; adding a file does not register a
test.

The first three-repeat all-LLM calibration exposed a mismatched Config contract:
the LLM rubric already accepted the documented `output.format` read example, but
the deterministic check required `config list` in every plan. The corrected
structural assertion recognizes alternative read paths and checks every supplied
parameter and the ordering of custom-key creation before a read. It does not
establish that an arbitrary replacement key exists; that evidence belongs to the
semantic check. Keep the 28/30 calibration result as evidence and rerun the full
suite after freezing this correction, rather than selecting its successful rows.

Subsequent calibration also verified `config info --keys-only` in the installed
catalog and equivalent ARM collection keyword casing via read-only requests.
These are supported alternatives, not exceptions for a particular model response.
The assertions still reject a missing `az` executable, changed subscription or
API version, unrelated endpoints and operations. Preserve raw grading evidence
when correcting a deterministic false positive after collection; do not alter
the generated response, measured duration or actual LLM verdict.

## Baseline evidence

A baseline of the current default suite must contain all ten configured cases in
one real run, using `-Repeat 3` for thirty independent results, with zero execution
or judge errors and a real LLM verdict for every result. Record source revision, target package,
runtime prompt hash, model, suite identity, outputs, assertions, and usage. The
wrapper records the target version and package fingerprint, the refreshed
resource-cache state and the phase timing in every result.
Earlier three-reference runs remain historical evidence for that smaller suite,
not a baseline for the expanded set.

Earlier runs of these ten cases used deterministic-only grading. Do not relabel
them as LLM-graded or change their scores in place. Adding semantic checks changes
the suite identity; a real judge result must include its rubric, reason and model
configuration, with original target responses and timings preserved when replayed.

The latency baseline uses single concurrency and a five-minute provider timeout.
Use `-RecoveryMode manual` with a package supporting `az troubleshoot`. The
provider captures the precise failure ID and requests oneshot analysis for that
ID, so a different process's latest failure cannot be selected. Missing IDs and
unexpectedly successful stimuli are execution errors, not quality scores.
Duration includes failure recording and troubleshooting, not just model inference. One pass
provides one observation per case; it does not establish stable tail latency.
Use the same three-repeat protocol for later optimization PRs. Retain every
result, report per-case median and range, and keep judge usage/time separate from
target measurements. Freeze the rubric before measuring; investigate failures
without relaxing valid constraints or selecting only successful attempts.

Older packages may report token usage without model names in their trace events.
Keep the observed model list empty in that situation and record the configured
model separately in the run's provenance; do not fabricate observed model telemetry.

Keep unsuccessful runs when diagnosing a failure. A process or environment error
is not a quality score, and a result from a different binary or test set is not
evidence for the current baseline. Upload failures remain failures even when the
local result is green. Passing offline tests validates assertion behavior, not
model quality or availability of the commands in the installed package.
