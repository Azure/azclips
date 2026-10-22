---
name: azclips-scenario-test-converter
description: "Use when converting remote main Azure CLI or Azure PowerShell live tests into Azclips record/playback scenario tests under tests/Scenario.Tests with verified command syntax, assertion evidence, and user review approval."
argument-hint: "Provide the Azclips commands, command group, or module to scope, and optionally the upstream live test to convert."
---

# Azclips Scenario Test Converter

Use this skill to translate Azure CLI or Azure PowerShell live tests into Azclips record/playback scenario tests.

`tests/Scenario.Tests/README.md` is the authoritative guide for what a scenario test is, how record and playback are selected, the code shape to write, and how a recording is produced and scanned. Read it first. This skill does not restate it; it only covers what is specific to converting upstream live tests, and it defers to the README and the repo whenever they disagree with anything here.

The work runs in two phases. The summary, run by `/integrate-scenario-test`, is read-only: it writes the test cases the Coverage Rules require. `/scenario-test-spec` writes them into an OpenSpec change, whose apply writes the tests, stops for user review, and records only what the user approves.

Generated scenario tests must include behavior-relevant assertions. Do not produce assertion-free tests for selected custom-code behavior unless the user explicitly asks for a smoke scenario.

## Mandatory Order

Scope the work from what Azclips exposes, then work from the upstream evidence inward. The command table bounds the work before any upstream file is read; the upstream cases are proposed before anything is synthesized. The summary's order is mandatory:

1. Enumerate the Azclips command surface for the module in scope: the CLI and PowerShell keys in `src/Extensions/<Module>/ExtensionIndex.cs`, and the parameters each command declares.
2. Confirm the scenario prerequisites, and report a review gap when either is missing: `tests/Scenario.Tests/ScenarioHost.cs` declares a static `ExtensionUnderTest` entry for the module, carrying its short name, package id, `ExtensionIndex` assembly, CLI verb, and CLI leaves; and `tests/Scenario.Tests/Scenario.Tests.csproj` has a `ProjectReference` to `src/Extensions/<Module>/Extensions.<Module>.csproj`. The PowerShell surface needs no cmdlet module project, because `RunPowerShell` builds the same `CommandInvocation` a cmdlet's `ProcessRecord` would build.
3. Determine which of those commands already have a scenario, by reading the existing scenarios under `tests/Scenario.Tests` and the commands they drive through `RunCli` and `RunPowerShell`.
4. Only now read the upstream tests, resolved from the `Upstream Repository Map` in `.github/agents/integrate-custom-code.agent.md`, and map every case onto that command table. See Coverage Rules.
5. Verify each planned assertion against an evidence source: the emitted output model, a supported read/show command, or the recorded request body. An assertion you cannot verify is a review gap.
6. Propose the upstream-derived test cases first, applying Rule 2.
7. Recompute coverage against the command table. Only the commands still uncovered after step 6 need a synthesized test case, under Rule 1.
8. Write the `Test cases` section, and hand it on as `Next Step` describes.

## Coverage Rules

The command table decides scope, not judgment about which upstream behavior matters. The two rules are numbered by importance; Mandatory Order sets when to apply them.

### Rule 1: every command in scope needs a scenario

Every CLI and PowerShell key the `ExtensionIndex` exposes ends with a scenario, or with a reported reason it has none for the user to accept. Scope is the command table exactly: a REST operation with no key is out, and a key backed by a generated processor is in, because its command-to-REST translation is still worth pinning.

### Rule 2: an upstream case whose logic exists in Azclips must be converted

Run four checks in order, stopping at the first failure:

1. **Command.** Is it in `GetCommandTable` for either surface?
2. **Parameters.** Is every parameter the case uses in that command's `Parameters` or `ParameterAliases`?
3. **Implementation.** Is the asserted behavior reachable? A parameter can bind to a processor property that nothing reads, accepting the input and ignoring it.
4. **Dependencies.** Can every command the case leans on — setup, intermediate state, cleanup — be driven from the table?

| Outcome | Decision |
| --- | --- |
| All four pass | `convert` |
| Command and behavior exist, some parameters absent | `convert-partial`: convert the expressible part, record the absent parameters as a review gap |
| Command and parameters exist, behavior not reachable | `not-implemented`: skip the assertion, and send the finding back to `/custom-code-spec` |
| Command absent from the table | `out-of-scope` |
| The case needs another command absent from the table | `prerequisite-missing`: skip the command under test and report it |

### Confirming the behavior is implemented

Check 3 needs implementation evidence, not command-table evidence. Confirm at least one:

- `CreateRequestUri()`, `CreateRequestBody()`, or `CreateRequestHeaders()` reads the property the parameter binds to.
- A customization under `src/Extensions/<Module>/Customization` branches on it, or passes it to a composed processor.
- The asserted property exists on the output model under `src/Extensions/<Module>/Models`, and the processor writes that model out.

### All upstream suites, one scenario

Azure CLI, Azure CLI Extensions, and Azure PowerShell rarely cover the same cases, so enumerate every case in every suite resolved; one that appears in a single suite still converts. The suite does not decide the surface: a case asserts on both Azclips surfaces when the behavior is reachable from both `GetCommandTable(CLI)` and `GetCommandTable(PowerShell)`, on one when it is reachable from one. Cases proving the same behavior become one scenario, the later ones marked `duplicate-of` the first.

### Collapsing parameter variations

Upstream tests one command many times with different values, and different suites repeat each other. Collapse them only when the values reach the same code path. A value that selects a different branch never collapses: a different enum member, parameter set, defaulted versus explicit value, sync versus long-running path, or success versus expected failure.

### Avoiding duplicate scenarios

Each recording costs a real Azure run, so take the cheapest option that still proves the behavior:

| Situation | Action | Recording cost |
| --- | --- | --- |
| The behavior is visible in data an existing committed recording already holds | Add the assertion to that scenario | None |
| The case needs a parameter or command the existing scenario does not send | Extend that scenario, if the behavior belongs to the same lifecycle | That scenario must be re-recorded |
| The case selects a different branch, or needs setup that conflicts with the existing one | New scenario | A new recording |

## Target Locations

Only writing is restricted; read anything. These three paths, and nothing else:

| Path | Allowed edit |
| --- | --- |
| `tests/Scenario.Tests/<CommandName>ScenarioTests.cs` | Create it. `<CommandName>` is a PascalCase Azclips command or behavior name. |
| `tests/Scenario.Tests/ScenarioHost.cs` | Add an `ExtensionUnderTest` entry for a module that has none. |
| `tests/Scenario.Tests/Scenario.Tests.csproj` | Add the `ProjectReference` that entry needs. |

`tests/Scenario.Tests/SessionRecords/**` is written by record mode only, after the recording approval in `Applying A Scenario-Test Change`. When playback fails, never hand-write, hand-edit, or hand-sanitise the JSON to make it pass.

For a worked example, read an existing scenario that resembles the target: `tests/Scenario.Tests/GroupCreateScenarioTests.cs` for a single command, and `tests/Scenario.Tests/VNetScenarioTests.cs` for a create/read/list/delete lifecycle across both surfaces.

## Inputs To Resolve

- The scope anchor: an approved OpenSpec change, or an Azclips command, command group, or module. A command name resolves fastest, because it is an exact `ExtensionIndex` CLI key (PowerShell keys carry a `_<ParameterSet>` suffix, so match by prefix) and a literal string in the upstream tests. Grep `src/Extensions/*/ExtensionIndex.cs` for it to get the module, then narrow the command table to that command or group.
- Azclips command names for both surfaces, such as `vm create` and `New-AzVM`.
- Selected custom-code feature ids and expected behavior contracts, when provided by the custom-code integration workflow.
- The verified Azclips evidence source for each generated assertion, including command/cmdlet name, parameter name, output model type, and property path.
- Resource names, location, prerequisites, happy paths, expected failures, and cleanup behavior from the upstream live test case being converted.

Scenario tests take no subscription input. Playback resolves the placeholder subscription `00000000-0000-0000-0000-000000000000` from the stub profile, and record mode resolves the developer's signed-in subscription through `host.LiveSubscriptionId()`.

## Conversion Rules

Follow the shape in the `Authoring a scenario` section of `tests/Scenario.Tests/README.md`. These rules cover only what converting an upstream live test adds.

- Reconstruct the behavior in Azclips style; do not copy upstream test source text.
- Preserve the upstream case's resource names, location, prerequisites, ordering, expected failures, and cleanup calls, unless a value is personal, subscription-specific, or likely to collide.
- A lifecycle upstream test converts to one scenario, not several: a recording replays in recorded order.
- Drive the same commands with the same values in both the record and playback branches, and share assertion helpers, so one recording holds both surfaces to one contract.
- Never invent command syntax, parameter names, model types, property paths, or api-versions. Verify each in the repo, or report the behavior as a review gap. PowerShell names must match the `ExtensionIndex` entry, which is sometimes suffixed: `Get-AzVirtualNetwork_Get`, `Get-AzVirtualNetwork_List`.
- Assert on the emitted output object, not console text, typed with the extension's own models from `src/Extensions/<Module>/Models`. Use a read/show command only when the result does not show the behavior.
- Never assert service-generated values, timestamps, etags, ordering, or generated IPs; they change on every re-record. Never hard-code a subscription, and assert a subscription-scoped id only in playback.

## Test Cases

After the Summary Report, list every proposed case in a `Test cases` section, upstream-derived first and synthesized after, keyed by test case ID (`T1`, `T2`). Write each block in the `Input Format` of `.github/prompts/scenario-test-spec.prompt.md`, from the command table you verified and the upstream test you read, so the next step takes the blocks as written instead of deriving them again.

- A case gets a block only when its decision is `convert` or `convert-partial`, or when Rule 1 synthesizes it. The `upstream_cases` entry of every other case gives its reason.
- Set `Surface` by reachability, as `All upstream suites, one scenario` describes, and give each surface its own steps, each with the expectations that check its result.
- Write the `expect:` lines by the Conversion Rules: assert on the step's output model, the request it sent, or the error it fails with, never on values that change on every re-record.

## Next Step

Do not ask the user to choose test cases: the Coverage Rules already decide which cases become tests, so every case in the `Test cases` section moves on. End the summary by naming the next step, `/scenario-test-spec` with the whole `Test cases` section. `eng-docs/howto/integrate-custom-code-agent.md` owns the rest of the chain.

## Applying A Scenario-Test Change

The tasks of a change written by `/scenario-test-spec` point `/opsx:apply` here. When the user skips OpenSpec, follow it directly with the summary's `Test cases` section instead.

1. Write one test method per test case, following the Conversion Rules and editing only the Target Locations. Run its steps in order, and turn each `expect:` line into an assertion on that step's result. Name the method after the case title, or after the behavior when the case has none, and name in a comment the case ID, and the test spec scenario it verifies when there is one.
2. Place each case by `Avoiding duplicate scenarios`: add its assertions to an existing scenario, extend that scenario, or create a new one.
3. Build the test project and run it in playback mode. Playback of a new or extended scenario fails until it is recorded, which is expected; report it as `recording-required` rather than working around it.
4. Stop for user review of the scenario code with the Apply Report. Apply the corrections they ask for and show the result again, repeating until they confirm.
5. Only after that confirmation, ask whether to record. Record mode performs real Azure operations against the user's own subscription, so record only the scenarios the user approved, never on your own initiative, one `--filter` at a time, following `Record vs. playback` in `tests/Scenario.Tests/README.md`. Confirm the user is signed in with the azclips CLI first. Then report the rewritten recordings for the user to review and commit, and the resources the run created that still need cleanup.

## Report Format

### Summary Report

Prepare `command_surface` and `upstream_cases` in the working notes before proposing test cases; they are what make Rule 1 and Rule 2 checkable. Report them at the end of the summary, followed by the `Test cases` section.

```yaml
command_surface:
- key: "<CLI path or PowerShell invocation name>"
  coverage: "existing <TestClass> | test case T<id> | uncovered: <reason>"

upstream_cases:
- id: U1
  suite: "azure-cli | azure-cli-extensions | azure-powershell"
  case: "<upstream test name, and the values that make this variation distinct>"
  azclips_command: "<command table key, or none>"
  asserted_on: ["cli", "powershell"]
  implementation_evidence: "<file that reads the parameter or exposes the asserted property>"
  decision: "convert | convert-partial | not-implemented | out-of-scope | prerequisite-missing | duplicate-of: U<id>"
  test_case: "T<id> | none"
  target: "new scenario <Class> | extend <Class>, re-record | assert on <Class>, no re-record | none"
  reason: "<required unless decision is convert; name the absent parameters, the one nothing reads, or the missing command and surface>"

review_gaps:
- "<assertion that could not be verified, or prerequisite that is missing, and why>"
```

### Apply Report

Report this at the review checkpoint of the apply.

```yaml
scenario_tests:
- path: "tests/Scenario.Tests/<CommandName>ScenarioTests.cs"
  test_methods: []
  review_gaps: []
harness_changes: "<ExtensionUnderTest entry and ProjectReference added for <Module>, or none>"
coverage_result:
  commands_in_scope: 0
  covered_by_upstream_conversion: 0
  covered_by_synthesized_scenario: 0
  still_uncovered: 0
recordings:
  new: []
  re_record_required: []
record_filter: "<the --filter value for these scenarios>"
live_effects: "<resources the record run creates, and required cleanup>"
playback_result: "<passed | recording-required | failed with reason>"
```
