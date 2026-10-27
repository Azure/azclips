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

If the secure storage is unavailable, for example in an SSH session or on a locked Linux keyring, the latest failure is saved without encryption and a warning is shown. Encrypted and unencrypted failures are kept in the same history file, which only the current user can read. At most one failure is unencrypted; earlier encrypted failures are kept but cannot be read until the secure storage is available again. The next save with secure storage available deletes the unencrypted failure; it is not encrypted or kept.

IDs have the form `PID-xxxxxxxx`: the parent shell PID and 8 random lowercase hexadecimal digits, such as 48212-cfd9ddb4. An unknown or evicted ID is an error and never selects a different failure automatically.

A history file that cannot be parsed is treated as empty history and is replaced on the next save. If the secure storage is available but its key is gone, a new key is created; earlier encrypted failures are skipped and deleted on the next save. To clear the history, run az config set --key agent.recovery.trigger --value off. Viewing history never clears it.
