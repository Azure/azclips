---
summary: Manage ARM deployments at management-group scope.
status: preview
schema: 1.0.0
online version:
---

Description: Create, inspect, validate, preview, export, wait for, cancel, and delete deployments at management-group scope. Deployment mode is Incremental.

### Create an empty management-group deployment

```bash
az deployment mg create --management-group-id myManagementGroup --location eastus --template-file ./main.bicep
```
