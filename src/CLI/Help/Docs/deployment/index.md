---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and manage Azure Resource Manager deployments at resource-group, subscription, management-group, and tenant scope using `az deployment group`, `az deployment sub`, `az deployment mg`, and `az deployment tenant`. All support create, validate, what-if, list, show, cancel, delete, and export with local ARM JSON or Bicep templates, remote ARM templates, Template Specs, parameter files, inline `--parameter KEY=VALUE` overrides, and validation levels. Resource-group and subscription deployments also support deployment modes (`Incremental` or `Complete`). Tenant deployments always use `Incremental` mode. Resource-group deployments also support rollback options. Create commands wait for the long-running deployment operation to finish and return the final result, unless running a what-if preview.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a management-group deployment

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep
```

### Validate a tenant-scoped deployment

```bash
az deployment tenant validate --location eastus --name tenantValidation --template-file ./tenant.bicep
```

### Create a subscription-scoped deployment that creates a resource group

```bash
az deployment sub create --location eastus --name myDeployment --mode Incremental --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```
