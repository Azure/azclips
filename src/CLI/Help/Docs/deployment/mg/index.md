---
summary: Manage Azure Resource Manager deployments scoped to a management group.
status: preview
schema: 1.0.0
online version:
---

Description: Create management-group-scoped ARM and Bicep deployments.

### Create a management-group deployment

```bash
az deployment mg create --management-group-id myManagementGroup --location eastus --template-file ./main.bicep
```
