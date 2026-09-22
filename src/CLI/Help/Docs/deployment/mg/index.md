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

### Validate a management-group deployment

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --template-file ./main.bicep
```

### List management-group deployments

```bash
az deployment mg list --management-group-id myManagementGroup
```

### Show a management-group deployment

```bash
az deployment mg show --management-group-id myManagementGroup --name myDeployment
```
