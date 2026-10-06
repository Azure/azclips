---
summary: Show a deployment at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get a single Azure Resource Manager deployment scoped to a management group, mirroring `az deployment mg show`. `--management-group-id` (required, alias `-m`) is the management group that contains the deployment and `--name` (required, alias `-n`) is the deployment name. Returns the full deployment resource including its provisioning state, correlation id, timestamp, duration, mode, template hash, outputs, and the list of provisioned output resources.

### Show a management-group-scoped deployment by name

```bash
az deployment mg show --management-group-id myManagementGroup --name myDeployment
```

### Show a management-group-scoped deployment using short flags

```bash
az deployment mg show -m myManagementGroup -n myDeployment
```
