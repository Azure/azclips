---
summary: Validate whether a subscription scoped deployment template would be accepted, without deploying it.
status: preview
schema: 1.0.0
online version:
---

Description: Validate a subscription scoped deployment, mirroring `az deployment sub validate`. Validate is read-only: it asks Azure Resource Manager whether a template is syntactically correct and would be accepted, without creating or modifying any resources. Unlike `az deployment sub what-if`, it reports whether the template is valid rather than predicting the individual resource changes. The template is supplied with one of `--template-uri` (a remote ARM template URL) or `--template-spec-id` (the resource id of a Template Spec version). `--name` (required) is the deployment name; `--location` (required) is the region where the deployment metadata is stored; `--mode` (required) is the deployment mode, `Incremental` or `Complete`; `--subscription-id` (optional) selects the subscription. Template parameters can be supplied with `--parameters-uri` (a remote parameters file URL). `--query-string` is a SAS token or other query string sent on the template link with `--template-uri`; it is not added to `--parameters-uri`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. When the template is valid the command returns the validation result; when it is not, the command fails with the Azure Resource Manager error.

### Validate a remote ARM template with a remote parameters file

```bash
az deployment sub validate --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Validate a Template Spec at the Template validation level

```bash
az deployment sub validate --location eastus --name myDeployment --mode Incremental --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --validation-level Template
```
