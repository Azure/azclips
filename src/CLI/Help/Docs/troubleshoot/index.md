---
summary: Analyze a saved command failure with AI.
status: preview
schema: 1.0.0
online version:
---

Description: Diagnose a saved failure using the same AI flow as automatic recovery. Without --id, select this terminal's latest saved failure, falling back to the global latest. Use an exact --id to select another record. agent.recovery defaults to manual; off disables analysis and clears history. The default style is oneshot. --style overrides agent.recovery.style for this invocation only. --list and show --id are read-only local views that do not start AI, authenticate, or perform cleanup.

### Analyze this terminal's latest saved failure

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

### Show the complete saved command and error locally

```bash
az troubleshoot show --id $failureId --output table
```

### Use an interactive session for this analysis

```bash
az troubleshoot --id $failureId --style interactive
```

### Enable automatic recovery

```bash
az config set --key agent.recovery --value auto
```

Automatic recovery does not disable explicit troubleshooting. Both use agent.recovery.style unless this invocation supplies --style.

### Notes

Full command arguments and error details are encrypted for the current local OS user. The default history limit is 10; configure a positive integer with agent.recovery.historySize. A lower limit applies on the next successful history write. Later successful commands do not remove old failures.

IDs have the form <PID>-<yyyy>-<MMdd>-<HHmm>-<counter>, using UTC, the parent shell PID, and a durable sequence. List output has id, command, and errorDetail fields, sorted by numeric PID ascending, then time and counter descending. Only list error detail is single-line and limited to 200 characters. Show preserves the complete command and error, including line breaks, even in a narrow table.

If the operating system's protected key storage is unavailable, az reports that the failure could not be saved; it does not store plaintext. Local encryption does not redact the selected diagnostic input sent to your configured AI provider.

--id and --list cannot be combined. show requires --id. --style is not valid with --list or show. --output and --query apply to local views, not to the AI-generated plan. An unknown or evicted ID is an error and never selects a different failure automatically.

Interactive analysis requires terminal input and output. Parent-PID association assumes the CLI is launched directly by the shell; extra wrappers, reused PIDs, and shared directories across PID namespaces can affect association. If the parent PID cannot be resolved, no-ID analysis warns and uses the global latest.

Incompatible v1 draft history, missing keys, corrupt data, and pending cleanup are errors, not empty history. Explicitly reset old draft history with az config set --key agent.recovery --value off before re-enabling manual or auto. Viewing history or pressing Tab never performs that reset.
