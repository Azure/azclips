---
summary: Delete a resource group deployment from the deployment history.
status: preview
schema: 1.0.0
online version:
---

Description: Delete an Azure Resource Manager deployment scoped to a resource group. This removes the deployment record and its associated deployment operations from the deployment history; it does **not** delete the resources the deployment created, and it leaves the resource group itself untouched. A deployment that is currently running cannot be deleted -- cancel it with `az deployment group cancel` first, or wait for it to reach a terminal state. The delete is a long-running operation and the command waits for it to finish. Deleting a deployment name that does not exist succeeds silently, because ARM answers the same `204 No Content` in both cases. The command prints nothing on success; the subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### Delete a deployment from the history

```bash
az deployment group delete --resource-group myResourceGroup --name myDeployment
```

### Delete a deployment in a specific subscription

```bash
az deployment group delete --resource-group myResourceGroup --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
