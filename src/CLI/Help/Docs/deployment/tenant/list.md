---
summary: List tenant-scope deployments.
status: preview
schema: 1.0.0
online version:
---

Description: List deployments recorded at tenant scope. The command follows ARM pagination. Use `--filter` to send an OData filter to ARM.

### List all tenant deployments

```bash
az deployment tenant list
```

### List successful tenant deployments

```bash
az deployment tenant list --filter "provisioningState eq 'Succeeded'"
```
