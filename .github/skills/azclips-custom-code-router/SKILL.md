---
name: azclips-custom-code-router
description: "Use when integrating Azure CLI or Azure PowerShell custom code into Azclips. Routes behavior into shared orchestrator logic, CLI-only surface logic, PowerShell-only surface logic, and applies a blacklist for no-longer-needed code such as parameter validation."
argument-hint: "Provide Azclips module/command and the upstream Azure CLI or Azure PowerShell custom behavior to classify."
---

# Azclips Custom Code Router

Use this skill before implementing Azure CLI or Azure PowerShell custom-code behavior in Azclips.

The router does not copy code. It first removes blacklisted behavior, then classifies the remaining behavior so implementation work lands in the correct Azclips layer or stops for user direction.

## Inputs
- Azclips module or command, such as `Compute`, `vm create`, or `New-AzVM`.
- Upstream Azure CLI custom-code behavior, source path, PR link, or command name.
- Upstream Azure PowerShell custom-code behavior, source path, PR link, or cmdlet name.
- Current editor file or nearby Azclips implementation, if available.

## Blacklist Gate
Before routing, read [the blacklist](./references/blacklist.md). If a behavior item is only a blacklisted concern, remove it from the candidate work and do not route it to implementation.

The routing order is mandatory:
1. Identify behavior items from upstream Azure CLI and Azure PowerShell custom code.
2. Remove behavior items that match the blacklist.
3. Route only the remaining non-blacklisted behavior.

Record each skipped item as:

```yaml
blacklist_skips:
- item: "<behavior>"
  reason: "<matching blacklist entry>"
  evidence: "<upstream or local signal>"
```

## Evidence To Gather
Gather only the evidence needed to classify the behavior.

1. Locate the Azclips extension index for the module, usually `src/Extensions/<Module>/ExtensionIndex.cs`.
2. Read the current orchestrator for the target command under `src/Extensions/<Module>/Orchestrators` when one exists.
3. Read atomic processors under `src/Extensions/<Module>/Processors` only as evidence. Do not modify them.
4. Read the PowerShell wrapper under `src/PS/<Module>/Cmdlets` only when needed to determine whether behavior is PowerShell-only. Do not implement PowerShell-only behavior without user direction.
5. Read CLI parser, help, or command metadata code only when needed to determine whether behavior is CLI-only. Do not implement CLI-only behavior without user direction.
6. Compare upstream CLI and PowerShell behavior by intent: Azure operations, request shape, orchestration steps, user-facing parameters, confirmation behavior, and output shape.

## Route Buckets
Split the upstream custom behavior into work items. Each work item must go into exactly one bucket.

### Route 1: `shared-orchestrator`

Choose this when the behavior is common Azure operation logic that both CLI and PowerShell should share.

Strong signals:
- Azure CLI and Azure PowerShell both implement the same or compatible business behavior.
- The behavior creates, updates, deletes, polls, or composes Azure resources independent of shell UX.
- The behavior coordinates multiple ARM calls, creates dependency resources, handles LRO flow, constructs shared request models, or normalizes service responses for both surfaces.
- The Azclips command already maps CLI and PowerShell invocation names to the same `ProcessorType`.

Likely implementation locations:
- Multi-step behavior: `src/Extensions/<Module>/Orchestrators/*_Orchestrator.cs`.
- Shared helper behavior only when it can live with the orchestrator or another explicitly allowed shared location.
- Routing: `src/Extensions/<Module>/ExtensionIndex.cs` so CLI and PowerShell point to the shared processor or orchestrator.

Forbidden locations:
- Do not edit atomic processors under `src/Extensions/<Module>/Processors`.
- If the behavior appears to require an atomic processor change, add it to `blocked_on_user_direction` instead of implementing it.

### Route 2: `cli-only-surface`

Choose this when the behavior is specific to Azure CLI UX and should not change PowerShell.

Strong signals:
- The behavior only concerns CLI command spelling, aliases, option names, help text, parser behavior, command discovery, or CLI-only output/table formatting.
- The upstream behavior exists in Azure CLI but has no PowerShell equivalent and does not change the underlying Azure operation.
- The behavior is needed for `az ...` compatibility but would be unnatural as a PowerShell cmdlet parameter or parameter set.

Routing action:
- Do not implement CLI-only behavior in this workflow.
- Add the behavior to `blocked_on_user_direction` and ask the user how to handle the CLI-specific change.

### Route 3: `powershell-only-surface`

Choose this when the behavior is specific to Azure PowerShell UX and should not change CLI.

Strong signals:
- The behavior concerns `PSCmdlet`, parameter sets, `ShouldProcess`, `ShouldContinue`, `-Force`, pipeline input, aliases, PowerShell object output, or PowerShell stream behavior.
- The upstream behavior exists in Azure PowerShell but has no CLI equivalent and does not change the underlying Azure operation.
- The behavior is needed for cmdlet compatibility but would be unnatural as a CLI option or command group.

Routing action:
- Do not implement PowerShell-only behavior in this workflow.
- Add the behavior to `blocked_on_user_direction` and ask the user how to handle the PowerShell-specific change.

## Tie Breakers
- If both upstream surfaces share behavior, prefer `shared-orchestrator` even if one surface implements it with validation or helper code locally.
- If behavior changes Azure resource side effects, request body construction, LRO handling, or response semantics, it is usually shared.
- If behavior changes only command-line spelling, help, completion, or shell-specific prompting, it is surface-specific.
- If a behavior is only defensive input checking, run the blacklist gate before assigning a route.
- If a command has both shared and surface-specific parts, split it into multiple work items instead of forcing one route.
- If a non-blacklisted behavior requires an atomic processor edit, stop and ask for user direction.

## Output Format

Return a compact routing result before implementation:

```yaml
target:
  azclips_module: "<module or unknown>"
  azclips_commands:
  - "<vm create | New-AzVM | other>"
evidence:
- "<file, symbol, or upstream behavior signal>"
routes:
  shared-orchestrator:
  - behavior: "<shared behavior>"
    target_layer: "src/Extensions/<Module>/Orchestrators or ExtensionIndex.cs"
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
- Do not treat every upstream custom function as an Azclips orchestrator. Single-call custom behavior may be a handwritten processor.
- Do not put PowerShell parameter-set behavior into the shared extension layer.
- Do not put CLI option spelling or help behavior into a PowerShell cmdlet.
- Do not port blacklisted validation just because both upstream projects contain it.
- Do not edit atomic processors under `src/Extensions/<Module>/Processors`.