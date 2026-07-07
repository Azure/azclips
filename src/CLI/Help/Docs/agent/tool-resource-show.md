---
summary: Return details for one resource from the local ARM cache, optionally augmented with a live ARM GET for deep fields (properties, provisioningState).
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource show` wraps the agent's `get_resource_details` tool. Given a resource name or full ARM id, returns the cache row (id, type, location, sku, tags, kind, identity, managedBy). Pass `--live` to also fetch the current ARM body — costs one live GET (~200-500ms) but exposes fields the cache does not carry (`properties`, `provisioningState`, endpoints, encryption). Default is cache-only.

### Show one VM from the cache

```bash
az agent tool resource show --name my-vm --kind resource
```

### Get live ARM state (needed for provisioningState / properties)

```bash
az agent tool resource show --name my-vm --kind resource --live true
```

### Look up by ARM id

```bash
az agent tool resource show --name /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Compute/virtualMachines/my-vm
```
