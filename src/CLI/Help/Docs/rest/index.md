---
summary: Send an authenticated HTTP request to Azure Resource Manager.
status: ga
schema: 1.0.0
online version:
---

Description: Universal authenticated HTTP passthrough for Azure Resource Manager (ARM), mirroring `az rest`. Builds an arbitrary request from the supplied method, endpoint, body, and headers, sends it through the already-authenticated pipeline, and surfaces the raw status code, response headers, and body. There is no LRO polling and no pagination — a single request is sent and a single untyped response is returned. The ARM bearer token is attached to every request, so absolute URLs must target an Azure management host (management.azure.com and the US Gov, China, and Germany sovereign clouds). The endpoint can be supplied two ways, which are mutually exclusive: as a full URL with `--uri`, or piece-wise with the ByParameters flags (`--subscription`, `--resource-group`, `--resource-provider`, `--resource-type`, `--name`, `--api-version`). Supported parameters: `--method` (HTTP method, defaults to GET), `--uri` (absolute URL or relative ARM path; `{subscriptionId}` is replaced with the current subscription), `--body` (request body, typically JSON; sent whenever provided, for any method), `--headers` (KEY=VALUE request headers), `--subscription`, `--resource-group`, `--resource-provider`, `--resource-type`, `--name`, `--api-version`, and `--uri-parameters` (KEY=VALUE query-string parameters, allowed in both modes). An HTTP status of 400 or higher still prints the response body but exits non-zero.

### GET a resource group with a full URI

```bash
azv2 rest --method GET --uri "/subscriptions/{subscriptionId}/resourceGroups/myresourcegroup?api-version=2021-04-01"
```

### GET a resource using the piece-wise ByParameters flags

```bash
azv2 rest --method GET --subscription 00000000-0000-0000-0000-000000000000 --resource-group myresourcegroup --resource-provider Microsoft.Storage --resource-type storageAccounts --name mystorageacct --api-version 2023-01-01
```

### PUT a resource with a JSON body and a custom header

```bash
azv2 rest --method PUT --uri "/subscriptions/{subscriptionId}/resourceGroups/myresourcegroup?api-version=2021-04-01" --body '{"location":"eastus"}' --headers Content-Type=application/json
```

### List resources with an extra query-string parameter

```bash
azv2 rest --method GET --uri "/subscriptions/{subscriptionId}/resources" --uri-parameters api-version=2021-04-01 '$filter=resourceType eq '\''Microsoft.Storage/storageAccounts'\'''
```
