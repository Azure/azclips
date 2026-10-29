---
summary: Manage deployment operations at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect the individual operations of a tenant-scoped Azure Resource Manager deployment. `az deployment operation tenant list` enumerates every operation of a deployment and `az deployment operation tenant show` retrieves one or more operations by id. Both require `--name` (alias `-n`, the deployment name).

### List the operations of a tenant-scoped deployment

```bash
az deployment operation tenant list --name myDeployment
```

### Show specific operations of a tenant-scoped deployment

```bash
az deployment operation tenant show --name myDeployment --operation-ids 0123456789012345678
```
