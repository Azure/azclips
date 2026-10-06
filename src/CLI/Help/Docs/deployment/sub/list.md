---
summary: List deployments at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: List the Azure Resource Manager deployments scoped to a subscription, mirroring `az deployment sub list`. `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription. `--filter` narrows the result with an OData filter expression, for example `--filter "provisioningState eq 'Succeeded'"`. All result pages are retrieved and returned as a single array.

### List every subscription-scoped deployment

```bash
az deployment sub list
```

### List only the subscription-scoped deployments that failed

```bash
az deployment sub list --filter "provisioningState eq 'Failed'"
```
