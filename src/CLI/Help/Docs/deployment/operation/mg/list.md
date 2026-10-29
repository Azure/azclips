---
summary: List deployment operations at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: List every operation of a management-group-scoped Azure Resource Manager deployment, mirroring `az deployment operation mg list`. `--management-group-id` (required, alias `-m`) is the management group that contains the deployment and `--name` (required, alias `-n`) is the deployment name. Each operation reports its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response. All result pages are retrieved and returned as a single array.

### List the operations of a management-group-scoped deployment

```bash
az deployment operation mg list --management-group-id myManagementGroup --name myDeployment
```

### List operations using short flags

```bash
az deployment operation mg list -m myManagementGroup -n myDeployment
```
