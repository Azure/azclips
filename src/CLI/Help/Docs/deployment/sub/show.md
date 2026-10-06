---
summary: Show a deployment at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get a single Azure Resource Manager deployment scoped to a subscription, mirroring `az deployment sub show`. `--name` (required, alias `-n`) is the deployment name and `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. Returns the full deployment resource including its provisioning state, correlation id, timestamp, duration, mode, template hash, outputs, and the list of provisioned output resources.

### Show a subscription-scoped deployment by name

```bash
az deployment sub show --name myDeployment
```

### Show a subscription-scoped deployment in an explicit subscription

```bash
az deployment sub show -n myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
