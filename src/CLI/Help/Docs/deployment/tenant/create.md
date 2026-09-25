---
summary: Create or preview a tenant-scope deployment.
status: preview
schema: 1.0.0
online version:
---

Description: Validate and create a tenant-scope deployment in Incremental mode. Supply exactly one template source with `--template-file`, `--template-uri`, or `--template-spec`. The deployment name is derived from the template when omitted. Use `--what-if` to preview without creating or `--confirm-with-what-if` to preview and confirm before creating.

### Create a tenant deployment from a local Bicep template

```bash
az deployment tenant create --location eastus --name tenantBaseline --template-file ./tenant.bicep --parameters environment=prod
```

### Create a tenant deployment from a remote template

```bash
az deployment tenant create --location eastus --name tenantBaseline --template-uri https://example.com/tenant.json
```
