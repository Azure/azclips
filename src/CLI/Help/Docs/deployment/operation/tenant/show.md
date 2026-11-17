---
summary: Show a deployment operation at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get one or more operations of a tenant-scoped Azure Resource Manager deployment, mirroring `az deployment operation tenant show`. `--name` (required, alias `-n`) is the deployment name and `--operation-ids` (required) is a space-separated list of operation ids to retrieve. Each id is fetched individually and the results are returned as a single array. Operation ids come from `az deployment operation tenant list`.

### Show a single operation of a tenant-scoped deployment

```bash
az deployment operation tenant show --name myDeployment --operation-ids 0123456789012345678
```

### Show several operations at once

```bash
az deployment operation tenant show -n myDeployment --operation-ids 0123456789012345678 9876543210987654321
```
