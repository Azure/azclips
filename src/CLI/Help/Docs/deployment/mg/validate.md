---
summary: Validate a management-group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validates an Incremental management-group deployment without creating resources.

### Validate a Bicep template

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep
```
