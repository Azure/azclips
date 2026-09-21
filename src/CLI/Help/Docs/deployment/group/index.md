---
summary: Manage Azure Resource Manager deployments scoped to a resource group.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments scoped to a resource group. Backed by `az deployment group create`, `az deployment group validate`, `az deployment group what-if`, `az deployment group show`, `az deployment group list`, `az deployment group export`, `az deployment group cancel`, and `az deployment group delete`, which mirror the corresponding Azure CLI verbs and support inline ARM/Bicep templates, remote template URIs, Template Specs, parameters files, inline `--parameter KEY=VALUE` overrides, deployment modes (`Incremental` or `Complete`), validation levels, and rollback options.

### Check that a template would be accepted before deploying

```bash
az deployment group validate --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus
```

### Preview the changes an ARM/Bicep template would make before deploying

```bash
az deployment group what-if --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus
```

### Deploy a local Bicep template with inline parameters

```bash
az deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```
