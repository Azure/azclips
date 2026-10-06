---
summary: Show a deployment at resource group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get a single Azure Resource Manager deployment scoped to a resource group, mirroring `az deployment group show`. `--resource-group` (required, alias `-g`) is the resource group that contains the deployment and `--name` (required, alias `-n`) is the deployment name. `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. Returns the full deployment resource including its provisioning state, correlation id, timestamp, duration, mode, template hash, outputs, and the list of provisioned output resources.

### Show a deployment by name

```bash
az deployment group show --resource-group myResourceGroup --name myDeployment
```

### Show a deployment in an explicit subscription

```bash
az deployment group show -g myResourceGroup -n myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
