---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments at resource-group, subscription, and management-group scope. Resource-group-scoped deployments are managed with `az deployment group`, subscription-scoped deployments with `az deployment sub`, and management-group-scoped deployments with `az deployment mg`. Resource-group and subscription commands mirror the corresponding Azure CLI verbs and support inline ARM templates, remote ARM templates, Template Specs, parameters files, inline `--parameter KEY=VALUE` overrides, and validation levels. Resource-group and subscription deployments also support deployment modes (`Incremental` or `Complete`). Resource-group deployments also support rollback options. Create commands wait for the long-running deployment operation to finish and return the final result.

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
