---
summary: Preview changes from a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Run a tenant-scope what-if operation. What-if is read-only: it predicts the changes (for example Create, Modify, Delete, or NoChange) a template would make without creating or modifying any resources. Takes the same template, parameter, and `--validation-level` inputs as `az deployment tenant create`, defaults `--name` the same way, and always uses `Incremental` mode. `--result-format` selects how much detail the predicted changes carry, `FullResourcePayloads` (default) or `ResourceIdOnly`. `--exclude-change-types` removes the listed change types (for example `NoChange Ignore`) from the predicted changes. The command waits for the what-if operation to finish and prints a change report: a legend, the predicted changes grouped by scope with one symbol per change, and a summary count per change type. If the operation fails, the command reports the error and exits non-zero.

### Preview a local Bicep template

```bash
az deployment tenant what-if --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```

### Preview resource IDs only

```bash
az deployment tenant what-if --location eastus --name myDeployment --template-file ./main.bicep --result-format ResourceIdOnly
```

### Hide unchanged resources

```bash
az deployment tenant what-if --location eastus --name myDeployment --template-file ./main.bicep --exclude-change-types NoChange Ignore
```
