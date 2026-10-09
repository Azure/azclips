---
summary: Manage Azure Resource Manager deployments scoped to a subscription.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments scoped to a subscription, typically used to create resource groups, policy definitions, or role assignments. Backed by `az deployment sub create`, `az deployment sub validate`, `az deployment sub what-if`, `az deployment sub show`, `az deployment sub list`, `az deployment sub export`, `az deployment sub cancel`, `az deployment sub delete`, and `az deployment sub wait`, which mirror the corresponding Azure CLI verbs and support inline ARM/Bicep templates, remote template URIs, Template Specs, parameters files, inline `--parameter KEY=VALUE` overrides, and validation levels. Subscription scoped deployments always run in `Incremental` mode and store their metadata in the region given by `--location`.

### Check that a template would be accepted before deploying

```bash
az deployment sub validate --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```

### Preview the changes an ARM/Bicep template would make before deploying

```bash
az deployment sub what-if --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```

### Deploy a local Bicep template that creates a resource group

```bash
az deployment sub create --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```
