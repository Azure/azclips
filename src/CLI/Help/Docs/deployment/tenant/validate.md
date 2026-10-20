---
summary: Validate a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate whether a template would be accepted for a tenant-scope deployment, without creating any resources. Takes the same template, parameter, and `--validation-level` inputs as `az deployment tenant create`, and always uses `Incremental` mode. `--name` is optional and defaults to a new GUID. Returns the validation result on success; on failure the command reports the validation errors and exits non-zero.

### Validate a Bicep template

```bash
az deployment tenant validate --location eastus --template-file ./main.bicep
```

### Validate a remote template with a parameters file

```bash
az deployment tenant validate --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-file ./azuredeploy.parameters.json --validation-level Template
```
