---
summary: List deployments at management group scope.
status: preview
schema: 1.0.0
online version:
---

Description: List the Azure Resource Manager deployments scoped to a management group, mirroring `az deployment mg list`. `--management-group-id` (required, alias `-m`) is the management group to list deployments from. `--filter` narrows the result with an OData filter expression, for example `--filter "provisioningState eq 'Succeeded'"`. All result pages are retrieved and returned as a single array.

### List every deployment in a management group

```bash
az deployment mg list --management-group-id myManagementGroup
```

### List only the management-group deployments that succeeded

```bash
az deployment mg list -m myManagementGroup --filter "provisioningState eq 'Succeeded'"
```
