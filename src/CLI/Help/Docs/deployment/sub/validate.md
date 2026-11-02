---
summary: Validate whether a subscription scoped deployment template would be accepted, without deploying it.
status: preview
schema: 1.0.0
online version:
---

Description: Validate a subscription scoped deployment, mirroring `az deployment sub validate`. Validate is read-only: it asks Azure Resource Manager whether a template is syntactically correct and would be accepted, without creating or modifying any resources. Unlike `az deployment sub what-if`, it reports whether the template is valid rather than predicting the individual resource changes. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template evaluated inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--location` (required) is the region where the deployment metadata is stored; `--name` (optional) is the deployment name and defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived; `--subscription-id` (optional) selects the subscription. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameter values are parsed as JSON when possible (for example `count=3` becomes a number and `flag=true` becomes a bool) and otherwise treated as strings, quote a value to force a string (for example `zip="01234"`). Inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. `--query-string` is a SAS token or other query string sent on the template link; it requires `--template-uri`, a leading `?` or `&` is removed, and it is not added to `--parameters-uri`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. Subscription scoped deployments always run in `Incremental` mode. When the template is valid the command returns the validation result; when it is not, the command reports the validation errors and exits with a non-zero status.

### Validate a local Bicep template with inline parameters

```bash
az deployment sub validate --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```

### Validate a remote ARM template with a remote parameters file

```bash
az deployment sub validate --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Validate a Template Spec at the Template validation level

```bash
az deployment sub validate --location eastus --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --validation-level Template
```
