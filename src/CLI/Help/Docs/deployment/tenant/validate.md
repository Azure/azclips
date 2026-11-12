---
summary: Validate a tenant deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate whether a template is syntactically correct and would be accepted by Azure Resource Manager at tenant scope, without deploying it. Takes the same template, parameter, `--location`, and `--validation-level` inputs as `az deployment tenant create`; `--name` defaults to a generated GUID. Validation uses `Incremental` mode. If the template fails validation, the command reports each error and exits non-zero; otherwise it returns the validation result.

### Validate a local Bicep template

```bash
az deployment tenant validate --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```
