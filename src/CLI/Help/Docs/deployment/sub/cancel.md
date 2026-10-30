---
summary: Cancel a running subscription deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Cancel a currently running Azure Resource Manager deployment scoped to a subscription. A deployment can only be cancelled while its provisioning state is `Accepted` or `Running`; once cancelled the state becomes `Canceled` and any resources the deployment already created remain and must be cleaned up separately. Cancelling a deployment that has already reached a terminal state fails with `DeploymentCannotBeCancelled`. The command prints nothing on success; the subscription defaults to the active profile's subscription unless `--subscription-id` is given. `-n` is accepted for `--name`.

### Cancel a running deployment

```bash
az deployment sub cancel --name myDeployment
```

### Cancel a running deployment in a specific subscription

```bash
az deployment sub cancel --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
