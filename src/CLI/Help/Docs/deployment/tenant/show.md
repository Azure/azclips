---
summary: Show a deployment at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get a single Azure Resource Manager deployment scoped to the tenant, mirroring `az deployment tenant show`. `--name` (required, alias `-n`) is the deployment name. Returns the full deployment resource including its provisioning state, correlation id, timestamp, duration, mode, template hash, outputs, and the list of provisioned output resources.

### Show a tenant-scoped deployment by name

```bash
az deployment tenant show --name myDeployment
```

### Show a tenant-scoped deployment using the short flag

```bash
az deployment tenant show -n myDeployment
```
