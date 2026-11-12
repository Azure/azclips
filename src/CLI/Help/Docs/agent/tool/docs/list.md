---
summary: List discoverable commands from the local help catalog.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool docs list` shares the local catalog reader used by the Agent and returns a JSON index of all known commands. Missing, stale, or corrupt catalog files are regenerated on demand. External AI tools use this metadata to discover commands before proposing plans.

### List all known commands

```bash
az agent tool docs list
```

### Rebuild the local help catalog

```bash
az agent tool docs generate
```
