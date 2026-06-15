---
summary: Manage Azure Resource Manager (ARM/Bicep) deployments.
status: preview
schema: 1.0.0
online version:
---

Description: Create and inspect Azure Resource Manager deployments. Currently scoped to resource-group deployments via `azv2 deployment group create`, which mirrors `az deployment group create` and supports inline ARM templates, remote ARM templates, Template Specs, parameters files, inline `--parameter KEY=VALUE` overrides, deployment modes (`Incremental` or `Complete`), validation levels, and rollback options. The command waits for the long-running deployment operation to finish and returns the final result.

### Create a resource-group-scoped deployment from a local Bicep template

```bash
azv2 deployment group create --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```
