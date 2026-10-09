---
summary: List the deployments in a subscription.
status: preview
schema: 1.0.0
online version:
---

Description: List every Azure Resource Manager deployment scoped to a subscription. Results are returned as a flat array of deployments with their provisioning state, mode, timestamp, and outputs; paged results are followed automatically. The subscription defaults to the active profile's subscription unless `--subscription-id` is given.

### List the deployments in the current subscription

```bash
az deployment sub list
```

### List deployments in a specific subscription

```bash
az deployment sub list --subscription-id 00000000-0000-0000-0000-000000000000
```

### List only failed deployments

Use `--filter` to pass an OData filter expression to the service.

```bash
az deployment sub list --filter "provisioningState eq 'Failed'"
```
