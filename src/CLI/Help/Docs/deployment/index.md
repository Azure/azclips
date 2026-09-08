---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and preview Azure Resource Manager deployments at resource-group and management-group scope.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Preview a management-group deployment

```bash
az deployment mg what-if --management-group-id myManagementGroup --location eastus --template-file ./main.bicep
```
