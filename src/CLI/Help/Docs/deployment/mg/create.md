---
summary: Create a management-group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validates and creates an Incremental deployment. Supply exactly one template source. Use `--no-wait` to return after ARM accepts the request.

### Create a management-group deployment from Bicep

```bash
az deployment mg create --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep --parameters environment=prod
```
