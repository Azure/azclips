---
summary: Analyze a saved command failure with AI.
status: preview
schema: 1.0.0
online version:
---

Description: Diagnose the latest saved failure, or select one with --id. Explicit troubleshooting always returns a one-shot AI recovery plan without executing its commands, even when automatic recovery uses interactive sessions. AI analysis requires agent.enabled=true. --list reads local history without calling AI and remains available when Agent is disabled.

### Analyze the most recent saved failure

```bash
az troubleshoot
```

### List saved failures

```bash
az troubleshoot --list --output table
```

### Select a failure

```bash
az troubleshoot --id $failureId
```

Set $failureId to an ID from the failure hint or the history list. Shell Tab completion after --id offers saved IDs.

### Control automatic recovery

```bash
az config set --key agent.enabled --value true
az config set --key agent.autoRecovery.enabled --value true
```

Automatic recovery is off by default. Enabling it does not disable explicit troubleshooting.

### Notes

Full command arguments and error details are encrypted for the current local OS user. The default history limit is 10; configure a positive integer with agent.failureHistory.maxEntries. A lower limit applies on the next successful history write. Later successful commands do not remove old failures.

If the operating system's protected key storage is unavailable, az reports that the failure could not be saved; it does not store plaintext. Local encryption does not redact the selected diagnostic input sent to your configured AI provider.

--id and --list cannot be combined. --output and --query apply to --list, not to the AI-generated plan. An expired ID is an error and never selects a different failure automatically.
