---
title: AI-based error recovery in Azure CLI (Private preview)
description: Learn how AI-based error recovery in Azure CLI analyzes command failures, identifies likely root causes, and provides guided troubleshooting steps.
---

# AI-based error recovery in Azure CLI (Private preview)

AI-based error recovery is a built-in capability in Azure CLI that helps you resolve command
failures more efficiently.

When enabled, Azure CLI saves command failures in a private encrypted history on your machine.
The original error remains visible. Use `az troubleshoot` when you want an integrated AI model to
analyze a saved failure and provide guided troubleshooting.

The AI model can:

- Analyze the error message
- Identify the likely root cause
- Suggest corrective actions
- Provide a guided plan for resolution

The AI-generated response is designed to be clear and actionable. It typically includes the
following sections:

- **What went wrong:** Explains the error and its likely cause.
- **Plan or next actions:** Recommends steps to resolve the issue.
- **Values to review:** Identifies parameters or inputs that you should review.
- **Disclaimer:** Reminds you that AI-generated content may be inaccurate.

The feature works in interactive terminals and headless environments, such as Azure DevOps agents
and GitHub Actions workflows.

The goal is to reduce debugging time, improve command success rates, and provide a more guided
command-line experience.

> [!NOTE]
> AI-based error recovery is currently available only for Azure CLI (`az`) commands. Azure
> PowerShell workflows aren't supported during the private preview.

## Enable or disable AI-based error recovery

AI-based error recovery is disabled by default.

Enable the feature:

```azurecli
az config set --key agent.enabled --value true
```

Disable the feature:

```azurecli
az config set --key agent.enabled --value false
```

View the current setting:

```azurecli
az config get --key agent.enabled
```

Enabling the feature does **not** automatically call AI. After a failure is securely saved, the CLI
prints a hint containing its record ID:

```text
To troubleshoot this error with AI, run: az troubleshoot --id <failure-id>
```

If secure persistence fails, the CLI reports that it could not save the record and does not print
an unusable ID. The original error and exit code remain unchanged.

## Analyze a saved failure

Analyze the latest saved failure:

```azurecli
az troubleshoot
```

List saved failures and choose a specific one:

```azurecli
az troubleshoot --list --output table
az troubleshoot --id <failure-id>
```

Shell Tab completion after `--id` offers saved IDs. Omitting `--id` selects the most recently saved
failure. An unknown or evicted ID is an error; it never silently selects a different record.

Explicit troubleshooting always uses **one-shot**: it returns an AI plan but does not execute the
suggested commands or enter an approval/chat loop. It remains available whenever `agent.enabled`
is true, regardless of the automatic recovery settings. `--output` and `--query` apply to `--list`,
not to the AI plan display.

The list and ID completion are local-only operations and remain available for old records when
`agent.enabled` is false. They do not start AI or perform Azure authentication. Tab completion
uses only record IDs and timestamps, without unlocking the protected key store.

## Enable automatic recovery separately

Automatic recovery is disabled by default. To also start AI after eligible command failures:

```azurecli
az config set --key agent.autoRecovery.enabled --value true
```

Turn it off without disabling explicit troubleshooting:

```azurecli
az config set --key agent.autoRecovery.enabled --value false
```

> [!IMPORTANT]
> This is a change from the previous default. Existing users who only configured
> `agent.enabled=true` must also enable `agent.autoRecovery.enabled` to continue automatic recovery.
> Well-known structured CLI errors still do not trigger automatic AI; you can select them manually.

## Local encrypted failure history

The history belongs to the current **local operating-system user**, not to a shared service.
It retains complete received command arguments and original error details, which can include
credentials. The data is encrypted with a key protected by Windows DPAPI, macOS Keychain, or
Linux Keyring. If protected storage is unavailable, the CLI reports the failure and never falls
back to plaintext. No Azure login is required just to save local history.

The history is kept in the local azclips data directory and respects `AZCLIPS_HOME`. The default
limit is the latest **10** records:

```azurecli
az config set --key agent.failureHistory.maxEntries --value 10
```

The limit must be a positive integer. Zero does not mean unlimited. Lowering the limit prunes old
records on the next successful history write; listing and completion do not prune. Later successful
commands neither remove old failures nor mark them resolved. Turning off `agent.enabled` stops new
records and AI calls but leaves existing records in place.

> [!NOTE]
> Local encryption protects stored data; it does not redact the diagnostic input supplied to AI.
> Explicit analysis sends the selected record's original command and error to your configured
> provider, not the entire history. Only use this feature with input you are permitted to send
> to that provider. Existing opt-in diagnostic agent traces have their own data-handling behavior.

## Configure the AI provider

By default, AI-based error recovery uses Microsoft's built-in AI provider.

Alternatively, you can configure Azure CLI to use your GitHub Copilot subscription.

Use the built-in provider:

```azurecli
# Requires az login
az config set --key agent.provider --value builtin
```

Use GitHub Copilot:

```azurecli
# Requires copilot login
az config set --key agent.provider --value github
```

## Choose the automatic interaction style

When automatic recovery is enabled, its default interaction style is one-shot: generate a recovery
plan and exit. `agent.sessionStyle` controls only this automatic flow; explicit `az troubleshoot`
always stays one-shot.

To use a multi-turn interactive session that remains open until you type `exit`, run:

```azurecli
az config set --key agent.sessionStyle --value interactive
```

To return to the default one-shot experience:

```azurecli
az config set --key agent.sessionStyle --value oneshot
```

## Frequently asked questions

### What does AI-based error recovery do?

AI-based error recovery analyzes command failures and provides:

- A root cause explanation
- Suggested fixes
- Step-by-step recovery guidance

Instead of displaying only raw error output, it helps you understand what went wrong and how to
resolve the issue.

### How is this different from existing error messages?

Traditional CLI error messages often:

- Are cryptic or incomplete
- Point to symptoms instead of root causes
- Offer little or no remediation guidance

AI-based error recovery:

- Translates errors into plain language
- Identifies the likely root cause
- Suggests actionable next steps

### What kinds of issues can it handle?

AI-based error recovery can assist with issues such as:

- Invalid arguments or parameters
- Missing required inputs
- Azure service or API errors, such as unavailable SKUs or policy restrictions

In some cases, the AI can query Azure APIs to suggest valid alternatives, such as available SKUs.

### Is the feature enabled by default?

No. AI-based error recovery is disabled by default.

To enable the feature, run:

```azurecli
az config set --key agent.enabled --value true
```

### Does it require authentication?

AI analysis requires the configured provider's authentication. Local history recording, listing,
and ID completion do not require Azure sign-in. If analysis cannot authenticate, it reports an
actionable error without deleting the selected failure.

The required sign-in depends on the configured AI provider:

- **builtin** (default) requires `az login`.
- **github** requires `copilot login`.

### Which model powers this feature?

AI-based error recovery uses a built-in AI model (GPT-5.4).

You can also configure Azure CLI to use your GitHub Copilot subscription:

```azurecli
az config set --key agent.provider --value github
```

### Who pays for AI usage?

Use of the built-in AI provider is currently included at no additional cost.

If you configure Azure CLI to use your GitHub Copilot subscription, costs depend on your
subscription plan and usage.

### What data is used and collected?

AI-based error recovery emits usage telemetry as its own trace, separate from the
telemetry for the command that failed. The trace records:

- How long recovery ran, and how long each phase took
- How many conversation turns and tool calls occurred
- The configured AI provider and the effective interaction style for this invocation
- Whether recovery was explicitly requested or automatically triggered
- The model name and the exit code
- A reference to the failed command's trace, so the two can be correlated

It does **not** record your prompts, the error text, the model's response, or the values
of any command parameters.

Generating a plan is not evidence that you adopted it or that the original problem was repaired.
This feature does not track subsequent commands to infer plan adoption or execution success.

To turn all of it off:

```azurecli
az config set --key telemetry.enabled --value false
```

This setting disables telemetry, not the separately encrypted local history. Use `agent.enabled=false`
to stop new history records and AI calls.

Separately, if you want a local record of the model input and output for debugging or auditing, enable tracing:

```azurecli
az config set --key agent.trace.enabled --value true
```

When tracing is enabled, each session writes a JSONL trace file to the location specified by
`agent.trace.path`. The file contains the model input and output and remains on your local machine.

For more information, see [Configuration Management][01].

### Can I rely completely on the AI suggestions?

No. AI-generated content may be inaccurate.

Always review the suggested recovery plan and values before running commands, especially before
deleting resources, resizing resources, or changing permissions.

### How can I provide feedback?

During the private preview, feedback is collected through user interviews and surveys.

<!-- link references -->

[01]: ./config.md
