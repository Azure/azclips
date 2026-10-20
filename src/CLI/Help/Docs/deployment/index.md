---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and manage Azure Resource Manager deployments at resource-group and tenant scope. Resource-group commands are available under `az deployment group`, and tenant commands under `az deployment tenant`. Both support create, validate, what-if, list, show, cancel, delete, and export with local ARM JSON or Bicep templates, remote ARM JSON templates, or Template Specs.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a tenant-scoped deployment

```bash
az deployment tenant validate --location eastus --name tenantValidation --template-file ./tenant.bicep
```
