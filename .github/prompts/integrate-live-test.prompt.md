---
name: "integrate-live-test"
description: "Generate or update Azclips live-test scripts for approved custom-code behavior."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, web, todo]
argument-hint: "OpenSpec change ID, selected feature IDs, and optional remote main live-test hints"
---

Generate or update Azclips live-test scripts for approved custom-code behavior. This command writes live-test scripts only.

**Input**: The argument should include an approved OpenSpec change ID or selected feature IDs plus the target service module, resource, and action. Usually run this after `/opsx:apply <change-id>` when product-code changes were required. Optional hints may include repo-relative upstream live-test paths on `main`.

Read upstream live-test evidence from the remote GitHub `main` branch only. Do not inspect local clones, local checkouts, forks, feature branches, or PR branches as upstream evidence. If remote `main` cannot be reached or does not contain the needed live test, record `missing_upstream_live_tests` and do not fall back to local upstream files.

**Steps**

1. Read the approved OpenSpec spec and selected features before creating scripts. If no approved behavior is available, stop and ask the user to run `/integrate-spec` first.
2. Resolve relevant upstream live tests from targeted remote `main` paths:
   - Azure CLI: `src/azure-cli/azure/cli/command_modules/<cli-module>/tests/latest/test_*.py`.
   - Azure CLI extensions: `src/<extension-name>/azext_<extension_package>/tests/latest/test_*.py`.
   - Azure PowerShell: `src/<Service>/<Module>.Test/ScenarioTests/*.ps1` and relevant Autorest tests.
3. Treat Azure CLI recordings as supporting evidence only, not primary live-test sources.
4. Use `azclips-live-test-script-converter` to convert the approved behavior into scripts with behavior-relevant assertions.
5. Write only these paths:
   - `tests/Scripts/CLI/<ModuleName>/<CommandName>/<testFileName>.sh`
   - `tests/Scripts/PS/<ModuleName>/<CommandName>/<testFileName>.ps1`
6. Do not create or edit xUnit/test-project files, including `tests/Scenario.Tests`, `tests/Cli.Tests`, `tests/Cmdlets.Tests`, or `tests/Core.*.Tests`.
7. Do not create nested `tests/Scripts/**/live-test-scripts/**` files. Do not write flat ad hoc scripts directly under `tests/Scripts/CLI` or `tests/Scripts/PS` unless the user explicitly names an existing script as the target.

**Output**

Report selected feature IDs, created or updated script paths, upstream live-test sources, assertion coverage, and live Azure effects.

End with `blocked_on_user_direction` asking the user to review scripts before live execution. Syntax checks are allowed; do not execute live Azure operations unless explicitly asked.