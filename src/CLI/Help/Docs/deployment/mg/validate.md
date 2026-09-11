---
summary: Validate a management-group-scoped deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate an ARM or Bicep deployment at management-group scope without creating resources.

### Validate a local Bicep template

```bash
az deployment mg validate --management-group-id myManagementGroup --name myDeployment --location eastus --template-file ./main.bicep
```
