---
summary: Recover from a saved command failure with AI.
status: preview
schema: 1.0.0
online version:
---

Description: Diagnose a saved failure using the same AI flow as automatic recovery. Without --id, select this terminal's latest saved failure, falling back to the global latest. Use an exact --id to select another record. The default style is oneshot. --style overrides agent.recovery.style for this invocation only.

### Recover from this terminal's latest saved failure

```bash
az troubleshoot recover
```

### Select a failure

```bash
az troubleshoot recover --id $failureId
```

Set $failureId to an ID from the failure hint or az troubleshoot list.

### Use an interactive session for this recovery

```bash
az troubleshoot recover --id $failureId --style interactive
```

### Notes

Local encryption does not redact the selected diagnostic input sent to your configured AI provider. --output and --query do not apply to the AI-generated plan.

Interactive recovery requires terminal input and output. Parent-PID association assumes the CLI is launched directly by the shell; extra wrappers, reused PIDs, and shared directories across PID namespaces can affect association. If the parent PID cannot be resolved, recovery without --id warns and uses the global latest.
