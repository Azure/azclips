---
summary: Manage deployment operations at resource group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect the individual operations of a resource-group-scoped Azure Resource Manager deployment. `az deployment operation group list` enumerates every operation of a deployment and `az deployment operation group show` retrieves one or more operations by id. Both require `--resource-group` (alias `-g`) and `--name` (alias `-n`, the deployment name).

### List the operations of a resource-group deployment

```bash
az deployment operation group list --resource-group myResourceGroup --name myDeployment
```

### Show specific operations of a resource-group deployment

```bash
az deployment operation group show --resource-group myResourceGroup --name myDeployment --operation-ids 0123456789012345678
```
