---
summary: Create a subscription scoped deployment from a remote ARM template or a Template Spec.
status: preview
schema: 1.0.0
online version:
---

Description: Create (start) a subscription scoped deployment, mirroring `az deployment sub create`. The template is supplied with one of `--template-uri` (a remote ARM template URL) or `--template-spec-id` (the resource id of a Template Spec version). `--name` (required) is the deployment name; `--location` (required) is the region where the deployment metadata is stored; `--mode` (required) is the deployment mode, `Incremental` or `Complete`; `--subscription-id` (optional) selects the subscription. Template parameters can be supplied with `--parameters-uri` (a remote parameters file URL). `--query-string` is a SAS token or other query string sent on the template link with `--template-uri`; it is not added to `--parameters-uri`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. The command waits for the deployment long-running operation to finish and returns the final deployment result.

### Deploy a remote ARM template with a remote parameters file

```bash
az deployment sub create --location eastus --name myDeployment --mode Incremental --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Deploy a Template Spec at the Template validation level

```bash
az deployment sub create --location eastus --name myDeployment --mode Incremental --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --validation-level Template
```
