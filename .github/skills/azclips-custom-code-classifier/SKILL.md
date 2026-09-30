---
name: azclips-custom-code-classifier
description: "Use when integrating Azure CLI or Azure PowerShell custom code from remote main into Azclips. Classifies behavior as shared customization logic, CLI-only surface logic, PowerShell-only surface logic, or blacklisted no-longer-needed code such as parameter validation."
argument-hint: "Provide Azclips module/command and the remote main upstream Azure CLI or Azure PowerShell custom behavior to classify."
---

# Azclips Custom Code Classifier

Use this skill before implementing Azure CLI or Azure PowerShell custom-code behavior in Azclips.

The classifier does not copy code. It first removes blacklisted behavior, then classifies the remaining behavior so implementation work lands in the correct Azclips layer or stops for user direction.

## Inputs
- Azclips module or command, such as `Compute`, `vm create`, or `New-AzVM`.
- Upstream Azure CLI custom-code behavior, repo-relative remote `main` source path, or command name.
- Upstream Azure PowerShell custom-code behavior, repo-relative remote `main` source path, or cmdlet name.
- Current editor file or nearby Azclips implementation, if available.

## Upstream Source Rule

Read upstream Azure CLI, Azure CLI Extensions, and Azure PowerShell evidence from the remote GitHub `main` branch only. Do not inspect local clones, local checkouts, forks, feature branches, or PR branches as upstream evidence. Local files are valid only for the current Azclips repo.

If remote `main` cannot be reached or does not contain the needed upstream evidence, report `insufficient-evidence` instead of falling back to local upstream files.

## Blacklist Check
Before classification, read [the blacklist](./references/blacklist.md). If a behavior item is only a blacklisted concern, remove it from the candidate work and do not route it to implementation.

The classification order is mandatory:
1. Identify behavior items from upstream Azure CLI and Azure PowerShell custom code.
2. Remove behavior items that match the blacklist.
3. Classify only the remaining non-blacklisted behavior.

Record each skipped item as:

```yaml
blacklist_skips:
- item: "<behavior>"
  reason: "<matching blacklist entry>"
  evidence: "<remote main upstream signal or current Azclips local signal>"
```

## Evidence To Gather
Gather only the evidence needed to classify the behavior.

1. Locate the Azclips extension index for the module, usually `src/Extensions/<Module>/ExtensionIndex.cs`.
2. Read the current customization for the target command under `src/Extensions/<Module>/Customization` when one exists, and confirm active routing from `ExtensionIndex.cs`.
3. Read atomic processors under `src/Extensions/<Module>/Processors` only as evidence. Do not modify them.
4. Read the PowerShell wrapper under `src/PS/<Module>/Cmdlets` only when needed to determine whether behavior is PowerShell-only. Do not implement PowerShell-only behavior without user direction.
5. Read CLI parser, help, or command metadata code only when needed to determine whether behavior is CLI-only. Do not implement CLI-only behavior without user direction.
6. Compare upstream CLI and PowerShell behavior by intent: Azure operations, request shape, orchestration steps, user-facing parameters, confirmation behavior, and output shape.

## Classification Buckets
Split the upstream custom behavior into work items. Each work item must go into exactly one bucket.

### Route 1: `shared-customization`

Choose this when the behavior is common Azure operation logic that both CLI and PowerShell should share.

Strong signals:
- Azure CLI and Azure PowerShell both implement the same or compatible business behavior.
- The behavior creates, updates, deletes, polls, or composes Azure resources independent of shell UX.
- The behavior coordinates multiple ARM calls, creates dependency resources, handles LRO flow, constructs shared request models, or normalizes service responses for both surfaces.
- The Azclips command already maps CLI and PowerShell invocation names to the same `ProcessorType`.

Likely implementation locations:
- Handwritten shared behavior: `src/Extensions/<Module>/Customization`.
- Multi-step behavior: override `SendAsync()` in a customization class and compose atomic processors with `CreateExtendedProcessor<T>`.
- Request-shape behavior: use `Method`, `CreateRequestUri()`, `CreateRequestBody()`, and `CreateRequestHeaders(HttpRequestMessage request)`.
- Pre-processing or output behavior: prefer `BeforeProcessAsync()` and `BeforeWriteOutputAsyncAndReturn(TResponse? body)` before overriding `ProcessAsync()`.
- Base-wide behavior: implement the base partial hook `OverrideBeforeProcessBase()` in a companion partial customization file.
- Routing: `src/Extensions/<Module>/ExtensionIndex.cs` so CLI and PowerShell point to the shared customization class when it replaces an atomic command.

Forbidden locations:
- Do not edit atomic processors under `src/Extensions/<Module>/Processors`.
- Do not edit generated models under `src/Extensions/<Module>/Models`.
- If the behavior appears to require a generated model or atomic processor change, add it to `blocked_on_user_direction` instead of implementing it.

### Route 2: `cli-only-surface`

Choose this when the behavior is specific to Azure CLI UX and should not change PowerShell.

Strong signals:
- The behavior only concerns CLI command spelling, aliases, option names, help text, parser behavior, command discovery, or CLI-only output/table formatting.
- The upstream behavior exists in Azure CLI but has no PowerShell equivalent and does not change the underlying Azure operation.
- The behavior is needed for `az ...` compatibility but would be unnatural as a PowerShell cmdlet parameter or parameter set.

Classification action:
- Do not implement CLI-only behavior in this workflow.
- Add the behavior to `blocked_on_user_direction` and ask the user how to handle the CLI-specific change.

### Route 3: `powershell-only-surface`

Choose this when the behavior is specific to Azure PowerShell UX and should not change CLI.

Strong signals:
- The behavior concerns `PSCmdlet`, parameter sets, `ShouldProcess`, `ShouldContinue`, `-Force`, pipeline input, aliases, PowerShell object output, or PowerShell stream behavior.
- The upstream behavior exists in Azure PowerShell but has no CLI equivalent and does not change the underlying Azure operation.
- The behavior is needed for cmdlet compatibility but would be unnatural as a CLI option or command group.

Classification action:
- Do not implement PowerShell-only behavior in this workflow.
- Add the behavior to `blocked_on_user_direction` and ask the user how to handle the PowerShell-specific change.

## Tie Breakers
- If both upstream surfaces share behavior, prefer `shared-customization` even if one surface implements it with validation or helper code locally.
- If behavior changes Azure resource side effects, request body construction, LRO handling, or response semantics, it is usually shared.
- If behavior changes only command-line spelling, help, completion, or shell-specific prompting, it is surface-specific.
- If a behavior is only defensive input checking, run the blacklist check before assigning a route.
- If a command has both shared and surface-specific parts, split it into multiple work items instead of forcing one route.
- If a non-blacklisted behavior requires a generated model or atomic processor edit, stop and ask for user direction.

## Output Format

Return a compact classification result before implementation:

```yaml
target:
  service_module: "<Swagger/TSP service module or unknown>"
  resource: "<resource or unknown>"
  action: "<action or unknown>"
target_mapping:
  azclips_commands: []
  azure_cli_commands: []
  azure_powershell_cmdlets: []
evidence:
- "<current Azclips file or remote main upstream behavior signal>"
routes:
  shared-customization:
  - behavior: "<shared behavior>"
    target_layer: "src/Extensions/<Module>/Customization or ExtensionIndex.cs"
  cli-only-surface:
  - behavior: "<CLI-only behavior>"
    action: "stop-for-user-direction"
  powershell-only-surface:
  - behavior: "<PowerShell-only behavior>"
    action: "stop-for-user-direction"
blacklist_skips:
- item: "<skipped behavior>"
  reason: "<blacklist entry>"
blocked_on_user_direction: []
```

If `blocked_on_user_direction` is non-empty, stop and ask one focused question before implementation.

## Common Mistakes To Avoid
- Do not port Azure CLI or Azure PowerShell source text directly.
- Do not treat every upstream custom function as an Azclips customization. Single-call custom behavior may only need a small split request or lifecycle hook override.
- Do not put PowerShell parameter-set behavior into the shared extension layer.
- Do not put CLI option spelling or help behavior into a PowerShell cmdlet.
- Do not port blacklisted validation just because both upstream projects contain it.
- Do not edit generated models under `src/Extensions/<Module>/Models` or atomic processors under `src/Extensions/<Module>/Processors`.