---
summary: Return the resource hierarchy under a node (tenant, subscription, resource group, or resource) from the local ARM cache.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource tree` wraps the agent's `get_resource_tree` tool. Given a node identifier and its kind (`tenant`, `subscription`, `resourceGroup`, or `resource`), returns the subtree beneath it as JSON — subscriptions under a tenant, resource groups under a subscription, resources under a group. `--max-depth` (default 2) caps the traversal so the JSON does not balloon on large scopes. Backed by the on-disk ARM cache under `~/.azclips/arm-cache/`.

### List every resource group in a subscription

```bash
az agent tool resource tree --name my-subscription --kind subscription
```

### Drill deeper (subscription → RG → resource)

```bash
az agent tool resource tree --name my-subscription --kind subscription --max-depth 3
```

### Show what a resource group contains

```bash
az agent tool resource tree --name my-rg --kind resourceGroup
```
