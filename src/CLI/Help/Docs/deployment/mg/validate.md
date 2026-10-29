---
summary: Validate a management-group deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate whether a template would be accepted for a management group scoped deployment, without creating any resources. Takes the same template, parameter, and `--validation-level` inputs as `az deployment mg create`. Validation always uses `Incremental` mode, even if `--mode Complete` is supplied. `--name` is optional and defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived. Returns the validation result on success; on failure the command reports the validation errors and exits non-zero.

### Validate a Bicep template

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --template-file ./main.bicep
```

### Validate a remote template with a parameters file

```bash
az deployment mg validate --management-group-id myManagementGroup --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-file ./azuredeploy.parameters.json --validation-level Template
```