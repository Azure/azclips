---
summary: List deployment operations at resource group scope.
status: preview
schema: 1.0.0
online version:
---

Description: List every operation of a resource-group-scoped Azure Resource Manager deployment, mirroring `az deployment operation group list`. `--resource-group` (required, alias `-g`) is the resource group that contains the deployment and `--name` (required, alias `-n`) is the deployment name. `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. Each operation reports its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response. All result pages are retrieved and returned as a single array.

### List the operations of a resource-group deployment

```bash
az deployment operation group list --resource-group myResourceGroup --name myDeployment
```

### List operations using short flags

```bash
az deployment operation group list -g myResourceGroup -n myDeployment
```
