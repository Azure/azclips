---
summary: Show details of a single resource group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Display a single Azure Resource Manager deployment scoped to a resource group, including its provisioning state, mode, timestamp, duration, template hash, outputs, and the resources it provisioned. Specify the resource group and the deployment name; the subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### Show a deployment

```bash
az deployment group show --resource-group myResourceGroup --name myDeployment
```

### Show a deployment in a specific subscription

```bash
az deployment group show --resource-group myResourceGroup --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
