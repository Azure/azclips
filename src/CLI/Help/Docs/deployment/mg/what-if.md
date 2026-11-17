---
summary: Preview changes from a management-group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Run a management group scoped what-if operation. What-if is read-only: it predicts the set of changes (Create, Modify, Delete, Deploy, NoChange, Ignore) a template would make without creating or modifying any resources. Takes the same template, parameter, and `--validation-level` inputs as `az deployment mg create`. What-if always uses `Incremental` mode, even if `--mode Complete` is supplied. `--result-format` selects how much detail the predicted changes carry, `FullResourcePayloads` (default) or `ResourceIdOnly`. The command waits for the what-if operation to finish and returns the what-if result; if the operation fails, the command reports the error and exits non-zero.

### Preview a local Bicep template

```bash
az deployment mg what-if --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```

### Preview resource IDs only

```bash
az deployment mg what-if --management-group-id myManagementGroup --location eastus --name myDeployment --template-file ./main.bicep --result-format ResourceIdOnly
```