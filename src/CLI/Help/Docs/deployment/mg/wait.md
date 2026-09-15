---
summary: Wait for a management-group deployment condition.
status: preview
schema: 1.0.0
online version:
---

Description: Poll a management-group deployment until it is created, updated, deleted, exists, or its provisioning state matches a condition.

### Wait until a deployment succeeds

```bash
az deployment mg wait --management-group-id myManagementGroup --name myDeployment --created
```

### Wait for a custom provisioning state

```bash
az deployment mg wait --management-group-id myManagementGroup --name myDeployment --custom provisioningState==Canceled
```
