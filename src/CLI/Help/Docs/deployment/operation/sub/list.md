---
summary: List deployment operations at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: List every operation of a subscription-scoped Azure Resource Manager deployment, mirroring `az deployment operation sub list`. `--name` (required, alias `-n`) is the deployment name and `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. Each operation reports its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response. All result pages are retrieved and returned as a single array.

### List the operations of a subscription-scoped deployment

```bash
az deployment operation sub list --name myDeployment
```

### List operations in an explicit subscription

```bash
az deployment operation sub list -n myDeployment --subscription-id 00000000-0000-0000-0000-000000000000
```
