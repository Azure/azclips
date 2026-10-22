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
- Upstream Azure CLI custom-code behavior, repo-relative upstream source path, or command name.
- Upstream Azure PowerShell custom-code behavior, repo-relative upstream source path, or cmdlet name.
- Current editor file or nearby Azclips implementation, if available.

## Blacklist Check
Before classification, read [the blacklist](./references/blacklist.md). If a behavior item is only a blacklisted concern, remove it from the candidate work and do not route it to implementation.

The classification order is mandatory:
1. Identify behavior items from upstream Azure CLI and Azure PowerShell custom code.
2. Remove behavior items that match the blacklist.
3. Classify only the remaining non-blacklisted behavior.

Record every skipped item under `blacklist_skips` in the output format below.

## Evidence To Gather
Gather only the evidence needed to classify the behavior. Local files are evidence of current Azclips state only; resolve upstream evidence from the `Upstream Repository Map` in `.github/agents/integrate-custom-code.agent.md`, which sets the branches and the unreachable-upstream rule.

1. Locate the Azclips extension index for the module, usually `src/Extensions/<Module>/ExtensionIndex.cs`.
2. Read the current customization for the target command under `src/Extensions/<Module>/Customization` when one exists, and confirm active routing from `ExtensionIndex.cs`.
3. Read atomic processors under `src/Extensions/<Module>/Processors` only as evidence. Do not modify them.
4. Read the PowerShell wrapper under `src/PS/<Module>/Cmdlets` only when needed to determine whether behavior is PowerShell-only.
5. Read CLI parser, help, or command metadata code only when needed to determine whether behavior is CLI-only.
6. Compare upstream CLI and PowerShell behavior by intent: Azure operations, request shape, orchestration steps, user-facing parameters, confirmation behavior, and output shape.

## Classification Buckets
Split the upstream custom behavior into work items. Each work item must go into exactly one bucket. Only `shared-customization` is implemented here; both surface routes stop, so put those behaviors in `blocked_on_user_direction` and ask the user how to handle them.

### Route 1: `shared-customization`

Choose this when the behavior is common Azure operation logic that both CLI and PowerShell should share.

Strong signals:
- Azure CLI and Azure PowerShell both implement the same or compatible business behavior.
- The behavior creates, updates, deletes, polls, or composes Azure resources independent of shell UX.
- The behavior coordinates multiple ARM calls, creates dependency resources, handles LRO flow, constructs shared request models, or normalizes service responses for both surfaces.
- The Azclips command already maps CLI and PowerShell invocation names to the same `ProcessorType`.

Likely implementation locations:
- Handwritten shared behavior: `src/Extensions/<Module>/Customization`.
- Routing: `src/Extensions/<Module>/ExtensionIndex.cs` so CLI and PowerShell point to the shared customization class when it replaces an atomic command.

Which override expresses the behavior is settled by `eng-docs/howto/Extension-Development-For-Private-Preview.md`, which carries the full escalation order and the bypass warnings that come with each entry.

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

### Route 3: `powershell-only-surface`

Choose this when the behavior is specific to Azure PowerShell UX and should not change CLI.

Strong signals:
- The behavior concerns `PSCmdlet`, parameter sets, `ShouldProcess`, `ShouldContinue`, `-Force`, pipeline input, aliases, PowerShell object output, or PowerShell stream behavior.
- The upstream behavior exists in Azure PowerShell but has no CLI equivalent and does not change the underlying Azure operation.
- The behavior is needed for cmdlet compatibility but would be unnatural as a CLI option or command group.

## Tie Breakers
These decide only the cases the strong signals leave ambiguous.

- Shared wins when both surfaces carry the behavior, even if one of them wraps it in local validation or helper code.
- Split a command that has both shared and surface-specific parts into separate work items instead of forcing one route.

## Output Format

Return a compact classification result before implementation:

```yaml
target: "<service module>/<resource>/<action>, or unknown"
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
  powershell-only-surface:
  - behavior: "<PowerShell-only behavior>"
blacklist_skips:
- item: "<skipped behavior>"
  reason: "<blacklist entry>"
  evidence: "<upstream or Azclips signal>"
blocked_on_user_direction: []
```

Every `cli-only-surface` and `powershell-only-surface` behavior also belongs in `blocked_on_user_direction`. If that list is non-empty, stop and ask one focused question before implementation.

When you also present the Feature Comparison Table, keep only `target`, `target_mapping`, `blacklist_skips`, and `blocked_on_user_direction` in this result. The table and its business logic section already carry the evidence and the routes.

## Feature Comparison Table

When the caller is comparing a command rather than classifying one behavior, present the result as a table the user can select from. Give every row a stable feature ID (`F1`, `F2`, `F3`) so later stages can refer to it, and use exactly these columns:

| command ID | Feature | Azure CLI source + code block | Azure PowerShell source + code block | Current Azclips evidence block | Action |
|---|---|---|---|---|---|

Column requirements:

- `command ID`: the stable feature ID.
- `Feature`: concise behavior name and intent.
- `Azure CLI source + code block`: a remote GitHub link to the Azure CLI or Azure CLI Extensions source, with line anchors when available, plus only the one or two key lines that show the behavior, quoted or summarized. The business logic section states the behavior in full, and the link leads to the rest. Never link a local clone path.
- `Azure PowerShell source + code block`: the same, for the Azure PowerShell source.
- `Current Azclips evidence block`: local Azclips evidence, such as `ExtensionIndex.cs`, `Customization`, `Processors`, or a PowerShell wrapper. Local workspace links are allowed here. State whether a generated processor, an atomic command, or existing customization already covers the behavior.
- `Action`: exactly one of `implement: shared-customization`, `needs-user-direction: cli-only-surface`, `needs-user-direction: powershell-only-surface`, or `drop: <blacklist or reason>`.

Blacklisted behavior still gets a row, with `drop:` and the matching entry as its reason. Dropping it silently hides a decision the user may want to revisit.

### Business Logic Per Feature

After the table, state every row's business logic in a `Business logic` section, keyed by feature ID. Write it while classifying, from the full upstream code you read, not from the short snippets in the table: conditions, defaults, and errors a snippet leaves out belong here. This is the one place a feature's behavior is written, so the next step copies the selected entries instead of deriving them again.

```markdown
### Business logic

**F1 <feature title>**
- When <condition>, <observable outcome: ARM request, output, or error raised by Azclips>.
- When <condition>, <observable outcome>.

**F2 <feature title>**
- When <condition>, <observable outcome>.
```

- Write each line by the `Business logic` field rule of the `/custom-code-spec` Input Format in `.github/prompts/custom-code-spec.prompt.md`, so it can be copied as is. Assembling a selection into that full format is the next step's job.
- Cover every row, including `drop:` rows, where one line is usually enough, so any selection can be copied as is.

## Common Mistakes To Avoid
- Do not treat every upstream custom function as an Azclips customization. Single-call custom behavior may only need a small split request or lifecycle hook override.
- Do not port blacklisted validation just because more than one upstream project contains it.

## Next Step

End the output with one question: which feature IDs to implement. The user may add a Surface note per feature, such as `F10 powershell-only`. Features they leave out are not carried forward.

The comparison stops there. `eng-docs/howto/integrate-custom-code-agent.md` owns the rest of the chain, including how the selection reaches `/custom-code-spec`.