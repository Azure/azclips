---
summary: Create a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Create (start) a tenant-scope deployment, mirroring `az deployment tenant create`. The deployment is validated first, then submitted. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template deployed inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--location` (required) is where the deployment metadata is stored, and `--name` (required) is the deployment name. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. `--query-string` carries a SAS token or other query string to use with the template URI and/or the parameters URI. Like legacy, tenant deployments always use `Incremental` mode. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. The command waits for the deployment to finish and returns the final deployment result.

### Deploy a local Bicep template with inline parameters

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```

### Deploy a remote ARM template with a remote parameters file

```bash
az deployment tenant create --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```
