---
title: AI-based error recovery in Azure CLI (Private preview)
description: Learn how AI-based error recovery in Azure CLI analyzes command failures, identifies likely root causes, and provides guided troubleshooting steps.
---

# AI-based error recovery in Azure CLI (Private preview)

AI-based error recovery in Azure CLI private preview is a built-in feature that helps you resolve
command failures in Azure CLI and Azure PowerShell workflows more efficiently.

When a command fails, instead of displaying only raw or cryptic error messages, Azure CLI uses an
integrated AI model to:

- Analyze the error message
- Identify the likely root cause
- Suggest corrective actions
- Provide a guided recovery plan

The AI-generated response is designed to be clear and actionable. It typically includes the
following sections:

- **What went wrong:** A clear explanation of the error.
- **Plan or next actions:** Recommended steps to resolve the issue.
- **Values to review:** Parameters or inputs generated or identified by AI that you should verify.
- **Disclaimer:** A reminder that AI-generated content may be inaccurate.

The feature works in interactive terminals and headless environments, such as Azure DevOps agents
and GitHub Actions workflows, using text formatting for readability.

The goal is to reduce time spent debugging, improve command success rates, and provide a more
guided, intelligent command-line experience.

> [!NOTE]
> AI-based error recovery is currently available for Azure CLI (`az`) commands only. Azure
> PowerShell workflows aren't supported during the private preview.

## Enable or disable AI-based error recovery

AI-based error recovery is disabled by default. Enable it by running the following command:

```azurecli
az config set --key agent.enabled --value true
```

Disable AI-based error recovery at any time:

```azurecli
az config set --key agent.enabled --value false
```

Check the current setting:

```azurecli
az config get --key agent.enabled
```

After you enable the feature, Azure CLI automatically invokes AI-based error recovery whenever an
`az` command fails. No per-command option is required.

## Configure the AI provider

By default, AI-based error recovery uses Microsoft's built-in AI provider.

You can also configure Azure CLI to use your GitHub Copilot subscription.

Use the built-in provider:

```azurecli
az config set --key agent.provider --value builtin
```

Use GitHub Copilot:

```azurecli
az config set --key agent.provider --value github
```

The built-in provider requires `az login`. The GitHub provider requires `copilot login`.

## Configure the interaction style

By default, AI-based error recovery uses a one-shot experience that presents a recovery plan and
then exits.

To enable a multi-turn interactive session, run:

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

Instead of leaving you with only raw error output, it helps you understand what went wrong and what
to do next.

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

No. AI-based error recovery is disabled by default. To enable it, run:

```azurecli
az config set --key agent.enabled --value true
```

### Does it require authentication?

Yes. You must be signed in to use AI-based error recovery. If you aren't signed in, only the
original error output is displayed.

The required sign-in depends on the configured AI provider:

- **builtin** (default) requires `az login`.
- **github** requires `copilot login`.

### Which model powers this feature?

Azure CLI uses a built-in AI model (GPT-5.4).

You can also configure AI-based error recovery to use your GitHub Copilot subscription:

```azurecli
az config set --key agent.provider --value github
```

### Who pays for AI usage?

Use of the built-in AI provider is currently included at no additional cost.

If you configure AI-based error recovery to use your GitHub Copilot subscription, costs depend on
your subscription plan and usage.

### What data is used and collected?

During the private preview, Azure CLI doesn't collect telemetry from AI-based error recovery.

If you want a local record of the model input and output for debugging or auditing purposes, enable
tracing:

```azurecli
az config set --key agent.trace.enabled --value true
```

Each session writes a JSONL trace file to the directory specified by `agent.trace.path`. The trace
file remains on your local machine.

### Can I rely completely on the AI suggestions?

No. AI-generated content may be inaccurate.

Always review the suggested recovery plan and values before running commands, especially for
destructive operations such as deleting resources, resizing resources, or changing permissions.

### How can I provide feedback?

During the private preview, feedback is collected through user interviews and surveys.
