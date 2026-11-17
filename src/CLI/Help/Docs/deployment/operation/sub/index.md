---
summary: Manage deployment operations at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect the individual operations of a subscription-scoped Azure Resource Manager deployment. `az deployment operation sub list` enumerates every operation of a deployment and `az deployment operation sub show` retrieves one or more operations by id. Both require `--name` (alias `-n`, the deployment name).

### List the operations of a subscription-scoped deployment

```bash
az deployment operation sub list --name myDeployment
```

### Show specific operations of a subscription-scoped deployment

```bash
az deployment operation sub show --name myDeployment --operation-ids 0123456789012345678
```
