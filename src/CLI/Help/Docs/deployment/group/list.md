---
summary: List deployments at resource group scope.
status: preview
schema: 1.0.0
online version:
---

Description: List the Azure Resource Manager deployments scoped to a resource group, mirroring `az deployment group list`. `--resource-group` (required, alias `-g`) is the resource group to list deployments from and `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. `--filter` narrows the result with an OData filter expression, for example `--filter "provisioningState eq 'Succeeded'"`. All result pages are retrieved and returned as a single array.

### List every deployment in a resource group

```bash
az deployment group list --resource-group myResourceGroup
```

### List only the deployments that succeeded

```bash
az deployment group list -g myResourceGroup --filter "provisioningState eq 'Succeeded'"
```
