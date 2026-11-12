---
summary: List tenant deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Lists deployments recorded at tenant scope, following every page of results. Use `--filter` to pass an OData filter to the service, for example to restrict results by provisioning state.

### List all tenant deployments

```bash
az deployment tenant list
```

### List succeeded deployments

```bash
az deployment tenant list --filter "provisioningState eq 'Succeeded'"
```
