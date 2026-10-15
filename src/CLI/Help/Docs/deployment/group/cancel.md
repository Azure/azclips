---
summary: Cancel a running resource group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Cancel a currently running Azure Resource Manager deployment scoped to a resource group. A deployment can only be cancelled while its provisioning state is `Accepted` or `Running`; once cancelled the state becomes `Canceled` and the resource group is left partially deployed, so any resources the deployment already created remain and must be cleaned up separately. Cancelling a deployment that has already reached a terminal state fails with `DeploymentCannotBeCancelled`. The command prints nothing on success; the subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### Cancel a running deployment

```bash
az deployment group cancel --resource-group myResourceGroup --name myDeployment
```

### Cancel a running deployment in a specific subscription

```bash
az deployment group cancel --resource-group myResourceGroup --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
