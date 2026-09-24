---
summary: List the deployments in a resource group.
status: preview
schema: 1.0.0
online version:
---

Description: List every Azure Resource Manager deployment scoped to a resource group. Results are returned as a flat array of deployments with their provisioning state, mode, timestamp, and outputs; paged results are followed automatically. The subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### List the deployments in a resource group

```bash
az deployment group list --resource-group myResourceGroup
```

### List deployments in a specific subscription

```bash
az deployment group list --resource-group myResourceGroup --subscription-id 00000000-0000-0000-0000-000000000000
```
