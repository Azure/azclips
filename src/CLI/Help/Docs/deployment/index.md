---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments at resource-group and management-group scope. Management-group commands are available under `az deployment mg`.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a management-group deployment

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep
```
