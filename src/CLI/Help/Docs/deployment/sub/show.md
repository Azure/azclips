---
summary: Show details of a single subscription deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Display a single Azure Resource Manager deployment scoped to a subscription, including its provisioning state, mode, timestamp, duration, template hash, outputs, and the resources it provisioned. Specify the deployment name; the subscription defaults to the active profile's subscription unless `--subscription-id` is given. `-n` is accepted for `--name`.

### Show a deployment

```bash
az deployment sub show --name myDeployment
```

### Show a deployment in a specific subscription

```bash
az deployment sub show --name myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
