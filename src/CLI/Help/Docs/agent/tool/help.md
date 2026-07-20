---
summary: Look up metadata for an azclips command by name, or list all discoverable commands when no name is given.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool help` wraps the internal `HelpTools` used by the agent. With `--name`, it returns full metadata for the specified command (parameters, aliases, required flags, extension owner). Without `--name`, it lists every command the agent knows about. External AI tools use this to validate command syntax before proposing plans, avoiding hallucinated flags.

### List all known commands

```bash
az agent tool help
```

### Look up metadata for a specific command

```bash
az agent tool help --name "vm create"
```
