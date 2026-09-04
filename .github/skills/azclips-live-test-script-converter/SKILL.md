---
name: azclips-live-test-script-converter
description: "Use when converting remote main Azure CLI or Azure PowerShell live tests into Azclips live test scripts under tests/Scripts with verified command syntax, assertion evidence, and user review approval."
argument-hint: "Provide target Azclips command plus the remote main upstream Azure CLI or Azure PowerShell live test to convert."
---

# Azclips Live Test Script Converter

Use this skill to translate Azure CLI or Azure PowerShell live tests into Azclips script-style live tests.

The converted scripts are review artifacts. This skill only creates or rewrites live test scripts and then stops for user review.

Read upstream Azure CLI, Azure CLI Extensions, and Azure PowerShell live tests from the remote GitHub `main` branch only. Do not inspect local clones, local checkouts, forks, feature branches, or PR branches as upstream evidence. If remote `main` cannot be reached or does not contain the needed live test, record the gap instead of falling back to local upstream files.

When this skill is invoked by the `integrate-custom-code` agent, write only Azclips live-test scripts under `tests/Scripts/CLI/<ModuleName>/<CommandName>/<testFileName>.sh` and `tests/Scripts/PS/<ModuleName>/<CommandName>/<testFileName>.ps1`. Do not create xUnit tests, Scenario.Tests, nested live-test script directories, or other test-project files.

Generated scripts must include behavior-relevant assertions. Do not produce assertion-free scripts for selected custom-code behavior unless the user explicitly asks for command-only smoke scripts.

## Mandatory Order

The order is mandatory:

1. Resolve the current Azclips command surface and output shape for every command used by the script.
2. Create an assertion plan that maps each selected behavior to a verified evidence source: direct command JSON, returned PowerShell object, supported read/show command, or supported REST passthrough response.
3. Convert the relevant Azure CLI and Azure PowerShell live tests into the approved Azclips live test script path with assertions for the selected behavior.
4. Run only lightweight syntax or parser validation when it fits the generated file shape. Do not execute live Azure operations unless the user explicitly asks for a specific command.
5. Stop and ask the user to review and manually approve the generated scripts.
6. Do not perform unrelated repo changes in this skill.

Do not modify product code in this skill.

## Target Locations

- CLI live scripts: `tests/Scripts/CLI/<ModuleName>/<CommandName>/<testFileName>.sh`.
- PowerShell live scripts: `tests/Scripts/PS/<ModuleName>/<CommandName>/<testFileName>.ps1`.
- Use the Swagger/TSP service module for `<ModuleName>` and a filesystem-safe Azclips command or cmdlet name for `<CommandName>`.
- Do not create files under `tests/Scenario.Tests`, `tests/Cli.Tests`, `tests/Cmdlets.Tests`, `tests/Core.*.Tests`, or nested `tests/Scripts/**/live-test-scripts/**` directories for this workflow.
- Do not write flat ad hoc script paths directly under `tests/Scripts/CLI` or `tests/Scripts/PS` unless the user explicitly names an existing script as the target.

Use [the PowerShell template](./templates/live-test-template.ps1) and [the CLI template](./templates/live-test-template.sh) as starting points. The templates show the preferred command-plus-assertion shape. They are examples only: extract the actual commands, values, and verified assertion paths from the upstream live test and current Azclips command surface. Do not rewrite existing ad hoc scripts unless the user explicitly asks to modify those files.

## Inputs To Resolve

- Azclips command names for both surfaces, such as `vm create` and `New-AzVM`.
- Selected custom-code feature ids and expected behavior contracts, when provided by the custom-code integration workflow.
- The verified Azclips evidence source for each generated assertion, including command/cmdlet name, parameter name, output property path, and helper-command response shape.
- Required runtime input: subscription id only. The generated scripts must require the caller to pass `AZCLIPS_SUBSCRIPTION_ID` or an equivalent script argument.
- Resource group, location, prerequisites, sensitive values, happy paths, expected failures, and cleanup behavior should come from the upstream live test case being converted.

## Conversion Rules

- Keep the generated script close to the upstream live test. Preserve its resource group, location, prerequisites, fake placeholders, environment variables, expected failures, and cleanup behavior unless a value is personal, subscription-specific, or likely to collide.
- Parameterize the subscription id. Generate unique resource names only when the upstream test creates resources with collision-prone names.
- Keep generated files as simple command-plus-assertion scripts unless the upstream live test itself needs setup logic, cleanup logic, or a wrapper.
- Add assertions only after verifying the observation path in the current Azclips repo. Prefer assertions against the create command result or returned object; use read/show commands or REST passthrough only when the behavior is not observable in the create result.
- Do not assert unrelated service-generated values, timestamps, etags, ordering, generated IP addresses, or other unstable details.
- Use comments sparingly, only to clarify live side effects, prerequisites, or expected failures.

## Conversion Workflow

1. Read the upstream live test from remote `main` and extract behavior, not source text.
2. Map each upstream command to the Azclips CLI and PowerShell command names.
3. Verify the Azclips command syntax, parameters, and output shape that each assertion will use.
4. Create only scripts required by the selected OpenSpec behavior and command surfaces. Each script must use the target location pattern above.
5. Preserve live behavior: Azure calls, service-side validation, LRO/polling behavior, cleanup, expected failures, and selected assertions.
6. Record the remote `main` upstream source path or URL for each generated script.
7. Apply the conversion rules above.
8. Run lightweight syntax or parser validation when it fits the generated file shape.
9. Stop and ask for user review and manual approval.

## Assertion Evidence Rules

- Do not invent Azclips command syntax, parameter names, PowerShell parameters, module imports, output property paths, REST response shapes, or ARM api-versions. Verify them in the current repo before writing the script, or list the behavior as a review gap.
- Prefer assertions against the command result or returned PowerShell object. Use resource-state reads only when the selected behavior is not visible in the command result.
- When using `rest` or `Invoke-AzRestMethod`, unwrap the current Azclips `RestResponse` envelope before asserting.
- Assertion messages must state the expected Azclips behavior contract.

## Assertion Plan Format

Before editing scripts, prepare this compact plan in the working notes or approval output:

```yaml
assertion_plan:
- behavior: "<selected spec behavior>"
  surface: "<cli | powershell>"
  script_command: "<Azclips command or helper command>"
  evidence_source: "<repo file or existing script proving command, parameter, or output shape>"
  assertion: "<field or resource-state check>"
  status: "verified | review-gap"
```

## Approval Output

After converting scripts, report only the scripts that were created or updated:

```yaml
live_test_scripts:
- path: "tests/Scripts/CLI/<ModuleName>/<CommandName>/<testFileName>.sh"
  surface: "cli"
  upstream_sources:
  - "<Azure CLI live test URL or repo path>"
  selected_features: []
  assertions:
  - "<return value or verified resource-state assertion>"
  verified_dependencies:
  - "<repo evidence for command, parameter, output shape, or REST helper>"
  review_gaps: []
  live_effects: "<creates/updates/deletes/reads resources>"
- path: "tests/Scripts/PS/<ModuleName>/<CommandName>/<testFileName>.ps1"
  surface: "powershell"
  upstream_sources:
  - "<Azure PowerShell live test URL or repo path>"
  selected_features: []
  assertions:
  - "<return value or verified resource-state assertion>"
  verified_dependencies:
  - "<repo evidence for command, parameter, output shape, or REST helper>"
  review_gaps: []
  live_effects: "<creates/updates/deletes/reads resources>"
required_user_approval: true
next_action: "waiting for user review and manual approval"
```

## Common Mistakes To Avoid

- Do not skip the user approval checkpoint after generating or rewriting live scripts.
- Do not ask for approval without listing the upstream live test sources used for each generated script.
- Do not create xUnit, Scenario.Tests, test-project, flat ad hoc script, or nested live-test-script files when converting live tests for the custom-code integration workflow.
- Do not hard-code subscription IDs or personal resource names.
- Do not turn service-side live behavior into local-only assertions.
- Do not generate assertion-free scripts for selected custom-code features unless the user explicitly asked for command-only smoke scripts.
- Do not add assertions that depend on unverified helper commands, unverified response envelopes, guessed output paths, or guessed ARM api-versions.
- Do not modify product code in this skill.
- Do not use non-live-test inputs or local upstream checkouts for this skill.
- Do not copy upstream test source text directly; reconstruct the behavior in Azclips script style.