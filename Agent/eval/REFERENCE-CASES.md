# Reference evaluation cases

These three cases define the initial authoring pattern for Agent plan evaluation.
They are not command execution tests and do not claim exhaustive coverage of a
command group.

## What is graded

- Use deterministic assertions for commands, parameter values, plan operations,
  scope, and explicit constraints.
- Prefer the structured `response.metadata.plan` and the shared parser over
  matching the full console transcript.
- Use LLM grading only when a separately stated semantic requirement cannot be
  checked reliably in code. Natural-language text alone is not a reason to add a
  judge.
- Do not require longer explanations, a particular sentence, or an explanation of
  why every selected value needs confirmation.
- Never remove a valid constraint merely to obtain a green result.

The default suite has no LLM judge. The optional Azure judge provider remains
available for future cases with justified semantic assertions; those cases must
configure their provider and rubric explicitly.

## Prerequisites

Run the wrapper against a full package built from the declared revision. The CLI
must have a signed-in active profile and the fixture group must already exist in
that subscription.

The default group is `azclips-test-rg`. Override it before starting the wrapper:

```powershell
$env:AZCLIPS_EVALUATION_RESOURCE_GROUP = '<existing-resource-group>'
```

The wrapper obtains `signed_in` and `subscription_id` from the target CLI. Do not
fake those values to make a test pass. Scope resolution belongs to the actual
command contract: `--subscription-id` is optional for these VM/deployment commands
because the active profile supplies it.

## The three cases

| Stable ID | Goal and stimulus | Deterministic acceptance |
| --- | --- | --- |
| `profile-show-active` | Show the active profile after an `--actve` typo. | Use the CLI's default-active behavior or a valid explicit active switch, without named selection, unknown flags, or profile/authentication changes. |
| `vm-create-long-flags` | Correct the unsupported short flags while preserving the requested VM name and existing group. | Correct create command, required values present, user values retained, no unsupported short flags, and unknown password represented by a declared placeholder. |
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

## Assertion responsibilities

`command-set` checks required value-taking arguments, boolean switches, exact user
values, supported equivalent path forms, and prohibited flags. Current CLI
`profile show` defaults to the active profile when no name is supplied, so the
old missing-selection stimulus is no longer an error. The reference now uses a
flag typo and accepts the supported implicit or explicit active selection.

`plan-contract` limits the reference plan to its intended operation, validates
the recorded subscription context, and rejects a fabricated or undeclared secret.
It does not grade explanatory prose or a conservative risk label on an otherwise
allowed operation.

VM and deployment plans may omit the subscription flag in this signed-in
reference context. An explicit value must match that context. Missing context is
not a license to omit an otherwise unresolved required value.

## Positive and negative examples

The offline tests exercise the same assertion functions used by Promptfoo.

- Profile: accept default-active and valid explicit selection; reject invalid
  flags/boolean values, named selection, help-only output, and added removal.
- VM: accept complete long flags and a declared password placeholder; reject
  missing values, changed user input, short flags, invented passwords, and wrong
  explicit subscriptions.
- Deployment: accept the current-profile default and equivalent relative path
  separators; reject a different subscription/template, missing parameters,
  unrelated resource creation, and Complete mode.
- Context: reject an absent active profile rather than treating an undefined
  subscription as a valid default.

Each new case should carry `case_id`, `command_group`, a concise `goal`, a
controlled failing command, declared prerequisites, and positive/negative
fixtures. Register it explicitly in the suite; adding a file does not register a
test.

## Baseline evidence

A reference baseline must contain all three cases in one real run with zero
execution errors. Record source revision, target package, runtime prompt hash,
model, suite identity, outputs, assertions, and usage.

Keep unsuccessful runs when diagnosing a failure. A process or environment error
is not a quality score, and a result from a different binary or test set is not
evidence for the current baseline. Upload failures remain failures even when the
local three-case result is green.
