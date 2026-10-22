---
summary: Analyze saved command failures with AI.
status: preview
schema: 1.0.0
online version:
---

Description: Analyze a saved failure with AI, list saved failures, or show one saved failure locally. agent.recovery.trigger defaults to off, which saves no failures; set it to manual or auto first. Setting off later disables analysis and clears history. Use analyze to start AI troubleshooting, list to view saved failures, and show to read one complete saved failure.

### Turn on failure saving for troubleshooting

```bash
az config set --key agent.recovery.trigger --value manual
```

### Analyze this terminal's latest saved failure

```bash
az troubleshoot analyze
```

### List saved failures

```bash
az troubleshoot list --output table
```

### Show the complete saved command and error locally

```bash
az troubleshoot show --id $failureId --output table
```

### Enable automatic recovery

```bash
az config set --key agent.recovery.trigger --value auto
```

Automatic recovery does not disable explicit troubleshooting. Both use agent.recovery.style unless analyze supplies --style.

### Notes

Full command arguments and error details are encrypted with a key kept in the operating system's secure storage, for the current local OS user only. The default history limit is 10; configure a positive integer with agent.recovery.historySize. A lower limit applies on the next successful history write.

If the secure storage is unavailable, for example in an SSH session or on a locked Linux keyring, only the latest failure is saved, without encryption, in a file that only the current user can read, and a warning is shown. Earlier encrypted failures cannot be read until the secure storage is available again. The next save with secure storage available encrypts the latest failure and deletes the unencrypted file.

IDs have the form `PID-yyyy-MMdd-HHmm-counter`, using UTC, the parent shell PID, and a durable sequence. An unknown or evicted ID is an error and never selects a different failure automatically.

Incompatible v1 draft history, missing keys, corrupt data, and pending cleanup are errors, not empty history. Explicitly reset old draft history with az config set --key agent.recovery.trigger --value off before re-enabling manual or auto. Viewing history never performs that reset.
