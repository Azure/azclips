---
summary: Preview a management-group-scoped deployment without applying it.
status: preview
schema: 1.0.0
online version:
---

Description: Return the changes an ARM or Bicep deployment would make at management-group scope without modifying resources.

### Preview a local Bicep template

```bash
az deployment mg what-if --management-group-id myManagementGroup --name myDeployment --location eastus --template-file ./main.bicep --result-format FullResourcePayloads
```
