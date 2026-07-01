---
summary: Search the local ARM resource cache by name pattern, returning matching resources as JSON.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource find` wraps the agent's `find_resources` tool: exact match preferred, falls back to substring, then Levenshtein typo correction. Results include the resource id, name, type, resource group, and location. External AI tools use this to resolve a user-supplied name to a concrete resource id before performing further operations. Backed by the on-disk ARM cache under `~/.azclips/arm-cache/`, so it is orders of magnitude faster than an ARM roundtrip.

### Find any resource whose name matches "prod"

```bash
az agent tool resource find --name prod
```

### Restrict the search to resource groups only

```bash
az agent tool resource find --name prod --kind resourceGroup
```
