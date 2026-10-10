---
summary: Create or preview a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate and create a tenant-scope deployment in Incremental mode, mirroring `az deployment tenant create`. Supply exactly one template source with `--template-file` (ARM JSON or Bicep), `--template-uri`, or `--template-spec`; `--query-string` requires `--template-uri`. `--parameters` can be repeated and accepts a parameters file (`@file` or path), a URI, a JSON string, or `KEY=VALUE` pairs typed by the template; later values win. Missing template parameters are prompted for unless `--no-prompt` is set. When `--name` is omitted it defaults to the template file or URI name, the template spec version, or `deployment1`. Use `--what-if` to preview without creating, or `--confirm-with-what-if` to preview and confirm before creating. The command waits for the deployment and returns the deployment result; a failed deployment returns the ARM error.

### Create a tenant deployment from a local Bicep template

```bash
az deployment tenant create --location eastus --name tenantBaseline --template-file ./tenant.bicep --parameters environment=prod
```

### Create a tenant deployment from a remote template and parameters file

```bash
az deployment tenant create --location eastus --name tenantBaseline --template-uri https://example.com/tenant.json --parameters https://example.com/tenant.parameters.json
```

### Preview the changes and confirm before creating

```bash
az deployment tenant create --location eastus --template-file ./tenant.json --confirm-with-what-if --what-if-exclude-change-types NoChange Ignore
```
