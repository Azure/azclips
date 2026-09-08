---
summary: Create a management-group-scoped deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate and create an ARM or Bicep deployment at management-group scope. Supply exactly one template source. The deployment name defaults to the template file name or a timestamp.

### Deploy a local Bicep template

```bash
az deployment mg create --management-group-id myManagementGroup --name myDeployment --location eastus --template-file ./main.bicep --parameters environment=prod
```

### Start a remote template deployment without waiting

```bash
az deployment mg create --management-group-id myManagementGroup --location eastus --template-uri https://example.com/main.json --no-wait
```
