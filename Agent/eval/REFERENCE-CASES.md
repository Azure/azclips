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
| `profile-show-active` | Show the active profile after an incomplete profile-show request. | The active switch is true, no mutually exclusive name is added, and no profile/authentication state is changed. |
| `vm-create-long-flags` | Correct the unsupported short flags while preserving the requested VM name and existing group. | Correct create command, required values present, user values retained, no unsupported short flags, and unknown password represented by a declared placeholder. |
| `deployment-template-recovery` | Correct an invalid deploy command while preserving the supplied local template and existing group. | Correct group deployment command, original name/group/template retained, valid scope, and no added resource operation or destructive Complete mode. |

Verify the failing inputs against the target version's live catalog before a
baseline run. If a future CLI supports the previously invalid input, update the
scenario to a real error condition rather than penalizing valid CLI behavior.

## Assertion responsibilities

`command-set` checks required value-taking arguments, boolean switches, exact user
values, supported equivalent path forms, and prohibited flags. For the profile
case, `--active` is a declared boolean and its value must be true.

`plan-contract` limits the reference plan to its intended operation, validates
the recorded subscription context, and rejects a fabricated or undeclared secret.
It does not grade explanatory prose or a conservative risk label on an otherwise
allowed operation.

VM and deployment plans may omit the subscription flag in this signed-in
reference context. An explicit value must match that context. Missing context is
not a license to omit an otherwise unresolved required value.

## Positive and negative examples

The offline tests exercise the same assertion functions used by Promptfoo.

- Profile: accept the active switch; reject no selection, false selection,
  mutually exclusive naming, and an added remove operation.
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
