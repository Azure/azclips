---
summary: Create a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Create a tenant-scope deployment, mirroring `az deployment tenant create`. The deployment is validated first, then submitted. Supply one template source: `--template-file` (a local ARM JSON or Bicep template), `--template-uri` (a remote ARM JSON template URL), or `--template-spec-id` (the resource id of a Template Spec version). A `.bicepparam` file supplied with `--parameters-file` can provide its own template reference instead. Bicep templates and `.bicepparam` files require the Bicep CLI for local compilation. `--location` (required) is where the deployment metadata is stored, and `--name` is the deployment name; when omitted it defaults to the template file name, else the template URI file name, else a generated GUID. Parameters can be supplied with `--parameters-file`, `--parameters-uri`, or repeatable inline `--parameter` values (KEY=VALUE, a JSON object, a JSON parameters file path optionally prefixed with `@`, or a `.bicepparam` path). A KEY=VALUE for a parameter the local template declares is read as that parameter's type; other values are parsed as JSON when possible, else as strings. Inline values override matching keys from a local parameters file; with a `.bicepparam` file they are compiled into it as overrides (Bicep CLI 0.22.6 or later). `--parameters-uri` cannot be combined with `--parameters-file` or `--parameter`. Required template parameters without a default value must be supplied; the command reports missing ones instead of prompting. `--query-string` requires `--template-uri` and applies only to that template link; include any required SAS token directly in `--parameters-uri`. Like legacy, tenant deployments always use `Incremental` mode. `--validation-level` accepts `Template`, `Provider`, or `ProviderNoRbac`; when omitted, ARM applies its default. The command waits for the deployment to finish and returns the final deployment result. `--what-if` returns the structured what-if result instead of deploying and sends no validate or deployment request; `--what-if-result-format` and `--what-if-exclude-change-types` work like `--result-format` and `--exclude-change-types` on `az deployment tenant what-if`.

### Deploy a local Bicep template with inline parameters

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep --parameter environment=prod
```

### Deploy a remote ARM template with a remote parameters file

```bash
az deployment tenant create --location eastus --name myDeployment --template-uri https://example.com/azuredeploy.json --parameters-uri https://example.com/azuredeploy.parameters.json
```

### Preview the deployment without deploying

```bash
az deployment tenant create --location eastus --name myDeployment --template-file ./main.bicep --what-if
```
