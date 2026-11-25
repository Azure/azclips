---
summary: Manage ARM deployments at tenant scope.
status: preview
schema: 1.0.0
online version:
---

Description: Create, validate, preview, list, inspect, wait for, export, cancel, and delete deployments at tenant scope. Tenant deployments always use `Incremental` mode.

### Create a tenant deployment from a local Bicep template

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep
```
