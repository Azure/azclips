---
summary: Preview changes from a management-group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Returns the resource changes predicted for an Incremental management-group deployment without applying them.

### Preview resource IDs

```bash
az deployment mg what-if --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep --result-format ResourceIdOnly
```
