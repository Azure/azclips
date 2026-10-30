---
summary: Delete a subscription deployment from the deployment history.
status: preview
schema: 1.0.0
online version:
---

Description: Delete an Azure Resource Manager deployment scoped to a subscription. This removes the deployment record and its associated deployment operations from the deployment history; it does **not** delete the resources the deployment created, such as resource groups. A deployment that is currently running cannot be deleted -- cancel it with `az deployment sub cancel` first, or wait for it to reach a terminal state. The delete is a long-running operation and the command waits for it to finish, unless `--no-wait` is given, in which case it returns as soon as ARM accepts the delete. Deleting a deployment name that does not exist succeeds silently, because ARM answers the same `204 No Content` in both cases. The command prints nothing on success; the subscription defaults to the active profile's subscription unless `--subscription-id` is given. `-n` is accepted for `--name`.

### Delete a deployment from the history

```bash
az deployment sub delete --name myDeployment
```

### Delete a deployment in a specific subscription

```bash
az deployment sub delete --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```

### Delete a deployment without waiting for the delete to finish

```bash
az deployment sub delete --name myDeployment --no-wait
```
