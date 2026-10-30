---
summary: Preview the changes a resource group scoped deployment would make, without applying them.
status: preview
schema: 1.0.0
online version:
---

Description: Run a resource group scoped what-if operation, mirroring `az deployment group what-if`. What-if is read-only: it predicts the set of changes (Create, Modify, Delete, Deploy, NoChange, Ignore) an ARM or Bicep template would make without creating or modifying any resources. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template evaluated inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--resource-group` (required) is the resource group to evaluate against and `--name` (required) is the deployment name; `--subscription-id` (optional) selects the subscription. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameter values are typed by the parameter's declared type when the template is local (`string` and `securestring` values are kept as-is, `int` and `bool` values must parse, and `object`, `array` and `secureObject` values must be valid JSON, otherwise the command fails naming the parameter); otherwise they are parsed as JSON when possible (for example `count=3` becomes a number and `flag=true` becomes a bool) and treated as strings when not, quote a value to force a string (for example `zip="01234"`). Inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. `--query-string` carries a SAS token or other query string to use with the template URI and/or the parameters URI. `--mode` selects the deployment mode, `Incremental` (default) or `Complete`. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. `--result-format` selects how much detail the predicted changes carry, `FullResourcePayloads` (default, full before/after resource snapshots) or `ResourceIdOnly` (resource ids only). The command waits for the what-if long-running operation to finish and returns the predicted change list.

### Preview a local Bicep template with inline parameters

```bash
az deployment group what-if --resource-group myResourceGroup --name myDeployment --template-file ./main.bicep --parameter location=eastus --parameter instanceCount=3
```

### Preview a remote ARM template with a remote parameters file

```bash
az deployment group what-if --resource-group myResourceGroup --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --mode Incremental --subscription-id 00000000-0000-0000-0000-000000000000
```

### Preview a Template Spec in Complete mode returning resource ids only

```bash
az deployment group what-if --resource-group myResourceGroup --name myDeployment --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --mode Complete --validation-level Template --result-format ResourceIdOnly
```
