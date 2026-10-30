---
summary: Preview the changes a subscription scoped deployment would make, without applying them.
status: preview
schema: 1.0.0
online version:
---

Description: Run a subscription scoped what-if operation, mirroring `az deployment sub what-if`. What-if is read-only: it predicts the set of changes (Create, Modify, Delete, Deploy, NoChange, Ignore) an ARM or Bicep template would make without creating or modifying any resources. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template evaluated inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--location` (required) is the region where the deployment metadata is stored; `--name` (optional) is the deployment name and defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived; `--subscription-id` (optional) selects the subscription. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameter values are typed by the parameter's declared type when the template is local (`string` and `securestring` values are kept as-is, `int` and `bool` values must parse, and `object`, `array` and `secureObject` values must be valid JSON, otherwise the command fails naming the parameter); otherwise they are parsed as JSON when possible (for example `count=3` becomes a number and `flag=true` becomes a bool) and treated as strings when not, quote a value to force a string (for example `zip="01234"`). Inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. `--query-string` carries a SAS token or other query string to use with the template URI. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. Subscription scoped deployments always run in `Incremental` mode. `--result-format` selects how much detail the predicted changes carry, `FullResourcePayloads` (default, full before/after resource snapshots) or `ResourceIdOnly` (resource ids only). The command waits for the what-if long-running operation to finish and returns the predicted change list. A `.bicepparam` parameters file requires Bicep CLI 0.16.1 or later, or 0.22.6 or later when combined with inline `--parameter` values; with an older Bicep CLI the command fails asking to upgrade and sends no request.

### Preview a local Bicep template with inline parameters

```bash
az deployment sub what-if --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```

### Preview a remote ARM template with a remote parameters file

```bash
az deployment sub what-if --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Preview a Template Spec returning resource ids only

```bash
az deployment sub what-if --location eastus --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --result-format ResourceIdOnly
```
