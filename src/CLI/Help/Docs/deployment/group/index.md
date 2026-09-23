---
summary: Manage Azure Resource Manager deployments scoped to a resource group.
status: preview
schema: 1.0.0
online version:
---

Description: Create, inspect, and cancel Azure Resource Manager deployments scoped to a resource group. These commands mirror the corresponding Azure CLI verbs. `az deployment group create` and `az deployment group what-if` support inline ARM/Bicep templates, remote template URIs, Template Specs, parameters files, inline `--parameter KEY=VALUE` overrides, deployment modes (`Incremental` or `Complete`), validation levels, and rollback options. `az deployment group show` and `az deployment group list` read existing deployment records, and `az deployment group cancel` stops a deployment that is still running.

### Preview the changes an ARM/Bicep template would make before deploying

```bash
az deployment group what-if --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus
```

### Deploy a local Bicep template with inline parameters

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```
