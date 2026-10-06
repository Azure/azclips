---
summary: Manage Azure Resource Manager deployments at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect Azure Resource Manager deployments scoped to a management group. `az deployment mg show` retrieves one deployment by name and `az deployment mg list` enumerates every deployment in the management group, optionally narrowed with an OData `--filter`. Both require `--management-group-id` (alias `-m`).

### Show a management-group-scoped deployment

```bash
az deployment mg show --management-group-id myManagementGroup --name myDeployment
```

### List management-group-scoped deployments

```bash
az deployment mg list --management-group-id myManagementGroup
```
