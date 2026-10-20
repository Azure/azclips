---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and manage Azure Resource Manager deployments at resource-group or tenant scope. Resource-group deployments support create and what-if workflows. Tenant deployments support create, list, show, cancel, delete, export, validate, and what-if workflows with ARM JSON, Bicep, remote templates, or Template Specs.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a tenant-scoped deployment

```bash
az deployment tenant validate --location eastus --name tenantValidation --template-file ./tenant.bicep
```
