---
summary: Manage Azure Resource Manager deployments at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect Azure Resource Manager deployments scoped to the tenant. `az deployment tenant show` retrieves one deployment by name and `az deployment tenant list` enumerates every tenant-scoped deployment, optionally narrowed with an OData `--filter`.

### Show a tenant-scoped deployment

```bash
az deployment tenant show --name myDeployment
```

### List tenant-scoped deployments

```bash
az deployment tenant list
```
