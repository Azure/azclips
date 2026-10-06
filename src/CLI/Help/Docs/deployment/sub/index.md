---
summary: Manage Azure Resource Manager deployments at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: Inspect Azure Resource Manager deployments scoped to a subscription. `az deployment sub show` retrieves one deployment by name and `az deployment sub list` enumerates every subscription-scoped deployment, optionally narrowed with an OData `--filter`.

### Show a subscription-scoped deployment

```bash
az deployment sub show --name myDeployment
```

### List subscription-scoped deployments

```bash
az deployment sub list
```
