---
name: azclips-live-test-script-converter
description: "Use when converting Azure CLI or Azure PowerShell live tests into Azclips live test scripts under tests/Scripts. Produces live scripts only and stops for user review and approval."
argument-hint: "Provide target Azclips command plus the upstream Azure CLI or Azure PowerShell live test to convert."
---

# Azclips Live Test Script Converter

Use this skill to translate Azure CLI or Azure PowerShell live tests into Azclips script-style live tests.

The converted scripts are review artifacts. This skill only creates or rewrites live test scripts and then stops for user review.

## Mandatory Order

The order is mandatory:

1. Convert the relevant Azure CLI and Azure PowerShell live tests into Azclips live test scripts.
2. Stop and ask the user to review and manually approve the generated scripts.
3. Do not perform unrelated repo changes in this skill.

Do not modify product code in this skill.

## Target Locations

- CLI live scripts: `tests/Scripts/CLI/live-test-scripts/<module-name>/<Scenario>.sh`.
- PowerShell live scripts: `tests/Scripts/PS/live-test-scripts/<module-name>/<Scenario>.ps1`.

Use [the PowerShell template](./templates/live-test-template.ps1) and [the CLI template](./templates/live-test-template.sh) as starting points. The templates show the preferred simple command-collection shape, modeled after `tests/Scripts/PS/VMCreate.ps1` and `tests/Scripts/CLI/VMCreate.sh`. They are examples only: extract the actual commands and values from the upstream live test being converted. Do not rewrite existing ad hoc scripts unless the user explicitly asks to modify those files.

## Inputs To Resolve

- Azclips command names for both surfaces, such as `vm create` and `New-AzVM`.
- Required runtime input: subscription id only. The generated scripts must require the caller to pass `AZCLIPS_SUBSCRIPTION_ID` or an equivalent script argument.
- Resource group, location, prerequisites, sensitive values, happy paths, expected failures, and cleanup behavior should come from the upstream live test case being converted.

## Conversion Rules

- Keep the generated script close to the upstream live test. Preserve its resource group, location, prerequisites, fake placeholders, environment variables, expected failures, and cleanup behavior unless a value is personal, subscription-specific, or likely to collide.
- Parameterize the subscription id. Generate unique resource names only when the upstream test creates resources with collision-prone names.
- Keep generated files as simple command collections unless the upstream live test itself needs setup logic, cleanup logic, or a wrapper.
- Use comments sparingly, only to clarify live side effects, prerequisites, or expected failures.

## Conversion Workflow

1. Read the upstream live test and extract behavior, not source text.
2. Map each upstream command to the Azclips CLI and PowerShell command names.
3. Create paired CLI and PowerShell scripts when both surfaces exist.
4. Preserve live behavior: Azure calls, service-side validation, LRO/polling behavior, cleanup, and expected failures.
5. Record the upstream source path or URL for each generated script.
6. Apply the conversion rules above.
7. Run lightweight validation when it fits the generated file shape.
8. Stop and ask for user review and manual approval.

## Approval Gate Output

After converting scripts, report:

```yaml
live_test_scripts:
- path: "tests/Scripts/CLI/live-test-scripts/<module-name>/<Scenario>.sh"
  surface: "cli"
  upstream_sources:
  - "<Azure CLI live test URL or repo path>"
  live_effects: "<creates/updates/deletes/reads resources>"
- path: "tests/Scripts/PS/live-test-scripts/<module-name>/<Scenario>.ps1"
  surface: "powershell"
  upstream_sources:
  - "<Azure PowerShell live test URL or repo path>"
  live_effects: "<creates/updates/deletes/reads resources>"
required_user_approval: true
next_action: "waiting for user review and manual approval"
```

## Common Mistakes To Avoid

- Do not skip the user approval gate after generating or rewriting live scripts.
- Do not ask for approval without listing the upstream live test sources used for each generated script.
- Do not hard-code subscription IDs or personal resource names.
- Do not turn service-side live behavior into local-only assertions.
- Do not modify product code in this skill.
- Do not use non-live-test inputs for this skill.
- Do not copy upstream test source text directly; reconstruct the behavior in Azclips script style.