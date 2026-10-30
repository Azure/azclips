---
summary: Create a subscription scoped deployment from an ARM or Bicep template.
status: preview
schema: 1.0.0
online version:
---

Description: Create (start) a subscription scoped deployment, mirroring `az deployment sub create`. The template is supplied with exactly one of `--template-file` (a local ARM or Bicep template deployed inline), `--template-uri` (a remote template URL), or `--template-spec-id` (the resource id of a Template Spec). `--location` (required) is the region where the deployment metadata is stored; `--name` (optional) is the deployment name and defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived; `--subscription-id` (optional) selects the subscription. Parameters can be supplied with `--parameters-file` (a local parameters file), `--parameters-uri` (a remote parameters file URL), and/or one or more repeatable inline `--parameter KEY=VALUE` pairs; inline parameter values are typed by the parameter's declared type when the template is local or downloaded from `--template-uri` (`string` and `securestring` values are kept as-is, `int` and `bool` values must parse, and `object`, `array` and `secureObject` values must be valid JSON, otherwise the command fails naming the parameter); otherwise they are parsed as JSON when possible (for example `count=3` becomes a number and `flag=true` becomes a bool) and treated as strings when not, quote a value to force a string (for example `zip="01234"`). Inline parameters overlay same-named keys from a parameters file and cannot be combined with `--parameters-uri`. A `--parameter` value that is an `http://` or `https://` URL of a JSON parameters file is downloaded without Azure credentials and its `parameters` are merged like a local parameters file, in order with the other `--parameter` values; a SAS URL is read as a URL, not as a KEY=VALUE pair. Unless `--parameters-uri` is given, a `--template-uri` template is downloaded the same way (with `--query-string` applied) to type inline values and to report missing required parameters before the deployment request is sent; the request still links to the URI. A failed download fails the command before any Azure request. A `--parameter` value that is a path ending in `.bicepparam` is used as the parameters file, exactly as if given as `--parameters-file`, and the other `--parameter` values overlay it; combining it with `--parameters-file` or another `.bicepparam` path fails with an invalid-input error. `--query-string` carries a SAS token or other query string to use with the template URI. `--validation-level` sets the validation level, one of `Template`, `Provider`, or `ProviderNoRbac`. Subscription scoped deployments always run in `Incremental` mode. The command first validates the deployment and, when validation fails, stops with `Deployment validation failed.` and the ARM error without submitting the deployment. The command waits for the deployment long-running operation to finish and returns the final deployment result; with `--no-wait` it returns as soon as ARM accepts the deployment, without waiting or printing anything, and `az deployment sub show` reports its progress. With `--what-if` (`-w`) the command runs only a what-if, exactly as `az deployment sub what-if` does, and creates nothing: it sends no validation and no deployment, waits for the what-if to finish, and returns the predicted change list as JSON, where Azure CLI prints a formatted report instead; `--no-wait` does not apply. `--what-if-result-format` (`-r`) selects `FullResourcePayloads` (default) or `ResourceIdOnly`, and `--what-if-exclude-change-types` (`-x`) removes the listed change types (`Create`, `Delete`, `Deploy`, `Ignore`, `Modify`, `NoChange`, `Unsupported`, case-insensitive) from the returned change list; both apply only with `--what-if`. A `.bicepparam` parameters file requires Bicep CLI 0.16.1 or later, or 0.22.6 or later when combined with inline `--parameter` values; with an older Bicep CLI the command fails asking to upgrade and sends no request. Azure CLI aliases are accepted: `-n` for `--name`, `-f` for `--template-file`, `-u` for `--template-uri`, `-s` or `--template-spec` for `--template-spec-id`, `-p` or `--parameters` for `--parameter`, `-w` for `--what-if`, `-r` for `--what-if-result-format`, and `-x` for `--what-if-exclude-change-types`.

### Deploy a local Bicep template that creates a resource group

```bash
az deployment sub create --location eastus --name myDeployment --template-file ./main.bicep --parameter resourceGroupName=myResourceGroup
```

### Deploy a remote ARM template with a remote parameters file

```bash
az deployment sub create --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json --subscription-id 00000000-0000-0000-0000-000000000000
```

### Deploy a Template Spec at the Template validation level

```bash
az deployment sub create --location eastus --template-spec-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.Resources/templateSpecs/myTemplateSpec/versions/1.0 --validation-level Template
```

### Preview a deployment with what-if instead of deploying it

```bash
az deployment sub create --location eastus --name myDeployment --template-file ./main.bicep --what-if --what-if-exclude-change-types NoChange Ignore
```

### Start a deployment without waiting for it to finish

```bash
az deployment sub create --location eastus --name myDeployment --template-file ./main.bicep --no-wait
```

### Deploy a local Bicep template using the short option aliases

```bash
az deployment sub create -l eastus -n myDeployment -f ./main.bicep --parameters resourceGroupName=myResourceGroup
```
