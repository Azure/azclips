---
summary: Show a deployment operation at subscription scope.
status: preview
schema: 1.0.0
online version:
---

Description: Get one or more operations of a subscription-scoped Azure Resource Manager deployment, mirroring `az deployment operation sub show`. `--name` (required, alias `-n`) is the deployment name and `--operation-ids` (required) is a space-separated list of operation ids to retrieve. Each id is fetched individually and the results are returned as a single array. Operation ids come from `az deployment operation sub list`. `--subscription-id` (optional) selects the subscription, defaulting to the active profile's subscription.

### Show a single operation of a subscription-scoped deployment

```bash
az deployment operation sub show --name myDeployment --operation-ids 0123456789012345678
```

### Show several operations at once

```bash
az deployment operation sub show -n myDeployment --operation-ids 0123456789012345678 9876543210987654321
```
