---
summary: Create a tenant deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Create a tenant scoped deployment. The deployment is validated first, then submitted; both requests use `Incremental` mode, the only mode at tenant scope. Supply one template source: `--template-file` (a local ARM JSON or Bicep template), `--template-uri` (a remote ARM JSON template URL), or `--template-spec-id` (the resource id of a Template Spec version). A `.bicepparam` file supplied with `--parameters-file` can provide its own template reference instead. Bicep templates and `.bicepparam` files require the Bicep CLI for local compilation. `--location` (required) is where the deployment metadata is stored, and `--name` is the deployment name; when omitted, a generated GUID is used. Parameters can be supplied with `--parameters-file`, `--parameters-uri`, or repeatable inline `--parameter` values (KEY=VALUE, a JSON object, or a JSON parameters file path, optionally prefixed with `@`). Inline values override matching keys from a local parameters file. `--parameters-uri` cannot be combined with `--parameters-file` or `--parameter`. `--query-string` requires `--template-uri` and applies only to that template link. `--validation-level` accepts `Template`, `Provider`, or `ProviderNoRbac`; when omitted, ARM applies its default. A validation error stops the command before anything is deployed. By default the command waits for the deployment to finish and returns the final deployment result. With `--no-wait`, it returns the deployment's initial state as soon as the deployment is submitted; use `az deployment tenant show` or `az deployment tenant wait` to follow it.

### Deploy a local Bicep template with inline parameters

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```

### Deploy a remote ARM template with a remote parameters file

```bash
az deployment tenant create --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```

### Submit a deployment without waiting for it to finish

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep --no-wait
```
