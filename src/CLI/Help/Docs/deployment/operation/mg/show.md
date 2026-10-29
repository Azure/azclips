---
summary: Show a deployment operation at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get one or more operations of a management-group-scoped Azure Resource Manager deployment, mirroring `az deployment operation mg show`. `--management-group-id` (required, alias `-m`) is the management group that contains the deployment, `--name` (required, alias `-n`) is the deployment name, and `--operation-ids` (required) is a space-separated list of operation ids to retrieve. Each id is fetched individually and the results are returned as a single array. Operation ids come from `az deployment operation mg list`.

### Show a single operation of a management-group-scoped deployment

```bash
az deployment operation mg show --management-group-id myManagementGroup --name myDeployment --operation-ids 0123456789012345678
```

### Show several operations at once

```bash
az deployment operation mg show -m myManagementGroup -n myDeployment --operation-ids 0123456789012345678 9876543210987654321
```
