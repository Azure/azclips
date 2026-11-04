---
summary: Inspect command-help metadata or rebuild the local catalog used by the Agent.
status: ga
schema: 1.0.0
online version:
---

Description: The `az agent tool help` commands provide local command metadata for the Agent and external AI tools. The base command lists discoverable commands or returns one command's metadata when `--name` is supplied. The `generate` subcommand rebuilds the catalog from the current extension set.

### Discover agent tool help commands

```bash
az agent tool help --help
```
