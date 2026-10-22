---
name: "custom-code-spec"
description: "Create or update the OpenSpec change for selected custom-code features only."
agent: "integrate-custom-code"
tools: [read, search, edit, execute, todo]
argument-hint: "Feature content: the target and one block per feature; optional change ID"
---

Run the agent's `Spec Command Behavior`. This command writes OpenSpec artifacts only.

**Input**: feature content in the `Input Format` below or an equivalent reviewed behavior list, plus the target service module, resource, and action, or enough conversation context to infer them. Optionally a preferred OpenSpec change ID.

End with the `openspec_review` report and `blocked_on_user_direction` asking the user to review or approve the change, then run `/opsx:apply <change-id>`.

## Input Format

The migration skill assembles a comparison's selected features into this format, as its step 10.2 describes. A maintainer describing a change by hand uses the same fields, omitting `Commands` and the feature headings when they do not apply.

```text
Target: <Module> / <resource> / <action>
Commands:
- CLI: <Azclips CLI command, such as vm create>
- PowerShell: <Azclips PowerShell command, such as New-AzVM>

## F1 <feature title>
Surface: <both | cli-only | powershell-only>
Action: <add behavior | update behavior | remove behavior | add parameter | add validation | fix bug | other>
Business logic:
- When <condition>, <observable outcome: ARM request, output, or error raised by Azclips>.
Reference: <optional: upstream link, issue, or PR>
```

Field rules:

- `Target` locates the features, not a CLI command: one target covers both surfaces and does not change when the processor behind a command does. Its action only identifies the command the features concern. The spec covers the whole resource, named as the agent's `Spec Command Behavior` defines.
- `Commands` uses the keys of the module's `ExtensionIndex.cs` command tables. CLI keys carry no `az` prefix, and PowerShell keys may carry a `_<ParameterSet>` suffix. Upstream command names belong in `Reference`. Omit `Commands` while the module has no command table yet, as with a new extension.
- Write one `Business logic` line per observable outcome, in Azclips terms rather than upstream function names.
