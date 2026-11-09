---
summary: Look up a command's metadata from the local help catalog.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool docs show` shares the local catalog reader used by the Agent. The required `--name` argument selects a command whose full metadata is returned as JSON, including parameters, aliases, required flags, and extension owner. Missing, stale, or corrupt catalog files are regenerated on demand. External AI tools use this metadata to validate command syntax before proposing plans, avoiding hallucinated flags. Use `az agent tool docs list` to list all known commands.

### Look up metadata for a specific command

```bash
az agent tool docs show --name "vm create"
```

### Rebuild the local help catalog

```bash
az agent tool docs generate
```
