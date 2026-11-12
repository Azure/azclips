---
summary: Manage deployment operations at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect the individual operations of a management-group-scoped Azure Resource Manager deployment. `az deployment operation mg list` enumerates every operation of a deployment and `az deployment operation mg show` retrieves one or more operations by id. Both require `--management-group-id` (alias `-m`) and `--name` (alias `-n`, the deployment name).

### List the operations of a management-group-scoped deployment

```bash
az deployment operation mg list --management-group-id myManagementGroup --name myDeployment
```

### Show specific operations of a management-group-scoped deployment

```bash
az deployment operation mg show --management-group-id myManagementGroup --name myDeployment --operation-ids 0123456789012345678
```
