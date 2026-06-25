---
title: AI-based error recovery in AzCLIPS (Private preview)
description: Learn how AI-based error recovery in AzCLIPS analyzes command failures, identifies possible root causes, and provides guided troubleshooting steps.
---

# AI-based error recovery in AzCLIPS (Private preview)

AI-based error recovery in AzCLIPS is a built-in feature that helps you resolve command failures in
Azure CLI and Azure PowerShell workflows more efficiently.

When a command fails, instead of displaying only raw or cryptic error messages, AzCLIPS uses an
integrated AI model to:

- Analyze the error message
- Identify the possible root cause
- Suggest corrective actions
- Offer a guided recovery plan

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

## Enable or disable AI-based error recovery

AI-based error recovery is disabled by default.

To enable or disable the feature, configure the appropriate AzCLIPS setting.

```console
# Configuration examples
```

## Frequently asked questions

### What does AI-based error recovery do?

AI-based error recovery analyzes command failures and provides:

- A root cause explanation.
- Suggested fixes.
- Step-by-step recovery guidance.

Instead of leaving you with only raw error output, it helps you understand what went wrong and what
to do next.

### How is this different from existing error messages?

Traditional CLI error messages often:

- Are cryptic or incomplete
- Point to symptoms instead of root causes
- Offer little or no remediation guidance

AI-based error recovery:

- Translates errors into plain language
- Identifies the potential root cause
- Suggests actionable next steps

### What kinds of issues can it handle?

AI-based error recovery can assist with issues such as:

- Invalid arguments or parameters
- Missing required inputs
- Azure service or API errors, such as unavailable SKUs or policy restrictions

Sometimes, AI can query Azure APIs to suggest valid options, such as available SKUs.

### Is the feature enabled by default?

No. AI-based error recovery is disabled by default and must be enabled through configuration.

### Does it require authentication?

Yes. You must be signed in to use AI-based error recovery. If you aren't signed in, only the
original error output is displayed.

### Which model powers this feature?

AzCLIPS uses a built-in AI model (GPT-5.4). Alternative models may also be available through
external subscriptions.

### Who pays for AI usage?

Use of the built-in model is currently included at no additional cost.

If you use an external AI provider, costs depend on the provider and your subscription plan.

### What data is used and collected?

The AI processes error messages and related command context to generate troubleshooting guidance.

Telemetry and usage information may also be collected, subject to privacy review. For more
information, see the product privacy documentation.

### Can I rely completely on the AI suggestions?

No. AI-generated responses include a disclaimer and may be inaccurate.

Always review suggested actions before executing commands or making changes to Azure resources.

### How can I provide feedback?

During the private preview, feedback is collected through user interviews and surveys.
