---
summary: Show a deployment operation at resource group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get one or more operations of a resource-group-scoped Azure Resource Manager deployment, mirroring `az deployment operation group show`. `--resource-group` (required, alias `-g`) is the resource group that contains the deployment, `--name` (required, alias `-n`) is the deployment name, and `--operation-ids` (required) is a space-separated list of operation ids to retrieve. Each id is fetched individually and the results are returned as a single array. Operation ids come from `az deployment operation group list`. `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription.

### Show a single operation of a resource-group deployment

```bash
az deployment operation group show --resource-group myResourceGroup --name myDeployment --operation-ids 0123456789012345678
```

### Show several operations at once

```bash
az deployment operation group show -g myResourceGroup -n myDeployment --operation-ids 0123456789012345678 9876543210987654321
```

### Find the failing operations, then inspect them

```bash
az deployment operation group list -g myResourceGroup -n myDeployment --query "[?properties.provisioningState=='Failed'].operationId"
az deployment operation group show -g myResourceGroup -n myDeployment --operation-ids 0123456789012345678
```
