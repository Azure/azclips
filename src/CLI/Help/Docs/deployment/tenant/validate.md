---
summary: Validate a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate a tenant-scope ARM JSON or Bicep deployment without creating resources. Supply exactly one template source and the location used to store deployment metadata.

### Validate a local Bicep template

```bash
az deployment tenant validate --location eastus --name tenantValidation --template-file ./tenant.bicep --parameters environment=dev
```
