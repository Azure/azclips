---
summary: Look up command metadata, list discoverable commands, or use generate to rebuild the local help catalog.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool help` shares the local catalog reader used by the agent. With `--name`, it returns full metadata for the specified command (parameters, aliases, required flags, extension owner). Without `--name`, it lists every command the agent knows about. Missing, stale, or corrupt catalog files are regenerated on demand. The `generate` subcommand rebuilds the catalog directly. External AI tools use this metadata to validate command syntax before proposing plans, avoiding hallucinated flags.

### List all known commands

```bash
az agent tool help
```

### Look up metadata for a specific command

```bash
az agent tool help --name "vm create"
```

### Rebuild the local catalog

```bash
az agent tool help generate
```
