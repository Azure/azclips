---
summary: Manage the individual operations of an Azure Resource Manager deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect the individual operations that make up an Azure Resource Manager deployment. A deployment operation records one resource action taken during a deployment, including its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response. Operations can be listed or shown at resource group (`az deployment operation group`), subscription (`az deployment operation sub`), management group (`az deployment operation mg`), and tenant (`az deployment operation tenant`) scope. These commands are the primary way to find the specific resource that caused a deployment to fail.

### List the operations of a resource-group deployment

```bash
az deployment operation group list --resource-group myResourceGroup --name myDeployment
```

### Show specific deployment operations

```bash
az deployment operation group show --resource-group myResourceGroup --name myDeployment --operation-ids 0123456789012345678 9876543210987654321
```
