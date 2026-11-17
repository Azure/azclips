---
summary: List deployment operations at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: List every operation of a tenant-scoped Azure Resource Manager deployment, mirroring `az deployment operation tenant list`. `--name` (required, alias `-n`) is the deployment name. Each operation reports its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response. All result pages are retrieved and returned as a single array.

### List the operations of a tenant-scoped deployment

```bash
az deployment operation tenant list --name myDeployment
```

### List operations using the short flag

```bash
az deployment operation tenant list -n myDeployment
```
