---
summary: Validate whether a resource group scoped deployment template would be accepted, without deploying it.
status: preview
schema: 1.0.0
online version:
---

Description: Validate a resource group scoped deployment, mirroring `az deployment group validate`. Validate is read-only: it asks Azure Resource Manager whether a template is syntactically correct and would be accepted, without creating or modifying any resources. Unlike `az deployment group what-if`, it reports whether the template is valid rather than predicting the individual resource changes. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template evaluated inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--resource-group` (required) is the resource group to validate against and `--name` (optional) is the deployment name and defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived; `--subscription-id` (optional) selects the subscription. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameter values are parsed as JSON when possible (for example `count=3` becomes a number and `flag=true` becomes a bool) and otherwise treated as strings, quote a value to force a string (for example `zip="01234"`). Inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. `--query-string` carries a SAS token or other query string to use with the template URI and/or the parameters URI. `--mode` selects the deployment mode, `Incremental` (default) or `Complete`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. `--rollback-on-error` records that the resource group should roll back to the last successful deployment on error, and `--rollback-to-deployment-name` rolls back to a specific named deployment instead; the two are mutually exclusive. When the template is valid the command returns the validation result; when it is not, the command reports the validation errors and exits with a non-zero status.

### Validate a local Bicep template with inline parameters

```bash
az deployment group validate --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Validate a remote ARM template with a remote parameters file

```bash
az deployment group validate --resource-group myResourceGroup --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --mode Incremental --subscription-id 00000000-0000-0000-0000-000000000000
```

### Validate a Template Spec in Complete mode at the Template validation level

```bash
az deployment group validate --resource-group myResourceGroup --name myDeployment --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --mode Complete --validation-level Template
```
