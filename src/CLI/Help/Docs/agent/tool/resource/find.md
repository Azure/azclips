---
summary: Search the local ARM resource cache by name pattern, returning matching resources as JSON.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource find` wraps the agent's `find_resources` tool: exact match preferred, falls back to substring, then Levenshtein typo correction. Results include the resource id, name, type, resource group, and location. External AI tools use this to resolve a user-supplied name to a concrete resource id before performing further operations. Backed by the on-disk ARM cache under `~/.azclips/agent/arm/`, so it is orders of magnitude faster than an ARM roundtrip.

### Find any resource whose name matches "prod"

```bash
az agent tool resource find --name prod
```

### Restrict the search to resource groups only

```bash
az agent tool resource find --name prod --kind resourceGroup
```

### Disambiguate a name that repeats across subscriptions

```bash
az agent tool resource find --name prod --subscription-id 00000000-0000-0000-0000-000000000000
```

### Narrow to a resource group and cap the result count

```bash
az agent tool resource find --name prod --kind resource --resource-group my-rg --top 25
```

Optional flags: `--kind` (tenant | subscription | resourceGroup | resource; defaults to all), `--subscription-id` (subscription id or display name), `--resource-group` (only meaningful for `--kind resource`), `--top` (max candidates, default 10).
