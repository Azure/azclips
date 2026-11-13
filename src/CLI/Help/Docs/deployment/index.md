---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments at resource-group, subscription, management-group scope, and tenant scope. Resource-group-scoped deployments are managed with `az deployment group`, subscription-scoped deployments with `az deployment sub`, tenant commands under `az deployment tenant`, and management-group-scoped deployments with `az deployment mg`. 

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a management-group deployment

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep
```

### Create a subscription-scoped deployment that creates a resource group

```bash
az deployment sub create --location eastus --name myDeployment --mode Incremental --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```
