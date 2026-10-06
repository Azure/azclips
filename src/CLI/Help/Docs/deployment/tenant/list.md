---
summary: List deployments at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: List the Azure Resource Manager deployments scoped to the tenant, mirroring `az deployment tenant list`. `--filter` narrows the result with an OData filter expression, for example `--filter "provisioningState eq 'Succeeded'"`. All result pages are retrieved and returned as a single array.

### List every tenant-scoped deployment

```bash
az deployment tenant list
```

### List only the tenant-scoped deployments that succeeded

```bash
az deployment tenant list --filter "provisioningState eq 'Succeeded'"
```
