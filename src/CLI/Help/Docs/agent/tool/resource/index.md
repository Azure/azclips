---
summary: Search and inspect Azure resources from the local ARM cache.
status: ga
schema: 1.0.0
online version:
---

Description: The `az agent tool resource` command group wraps the agent's resource-inspection tools (find, show, refresh, sku, tree) against the on-disk ARM cache under `~/.azclips/agent/arm/`. All commands emit JSON so external AI tools can consume them as skills. Cache-backed lookups are orders of magnitude faster than round-tripping to ARM.

### Find a resource by name pattern

```bash
az agent tool resource find --name prod
```

### Show a resource by name

```bash
az agent tool resource show --name my-vm --kind resource
```

### Refresh the local ARM cache

```bash
az agent tool resource refresh
```
