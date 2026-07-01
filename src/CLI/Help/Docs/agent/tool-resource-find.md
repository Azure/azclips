---
summary: Search the local ARM resource cache by name substring, returning matching resources as JSON.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource find` wraps the agent's `find_resources` tool: fuzzy substring match against the on-disk ARM cache under `~/.azclips/arm-cache/`. Results include the resource id, name, type, resource group, and location. External AI tools use this to resolve a user-supplied name to a concrete resource id before performing further operations. Backed by the cache, it is orders of magnitude faster than an ARM roundtrip.

### Find any resource whose name contains "prod"

```bash
az agent tool resource find --query prod
```

### Restrict the search to resource groups only

```bash
az agent tool resource find --query prod --kind resource-groups
```
