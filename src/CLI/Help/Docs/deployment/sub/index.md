---
summary: Manage Azure Resource Manager deployments scoped to a subscription.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments scoped to a subscription, typically used to create resource groups, policy definitions, or role assignments. Backed by `az deployment sub create`, `az deployment sub validate`, `az deployment sub what-if`, `az deployment sub show`, `az deployment sub list`, `az deployment sub export`, `az deployment sub cancel`, and `az deployment sub delete`, which mirror the corresponding Azure CLI verbs and support remote template URIs, Template Specs, remote parameters files, deployment modes, and validation levels. Subscription scoped deployments store their metadata in the region given by `--location`.

### Check that a template would be accepted before deploying

```bash
az deployment sub validate --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```

### Preview the changes an ARM template would make before deploying

```bash
az deployment sub what-if --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```

### Deploy a remote ARM template that creates a resource group

```bash
az deployment sub create --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```
