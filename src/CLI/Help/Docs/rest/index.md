---
summary: Send an authenticated HTTP request to Azure Resource Manager.
status: preview
schema: 1.0.0
online version:
---

Description: Universal authenticated HTTP passthrough for Azure Resource Manager (ARM), mirroring `az rest`. Builds an arbitrary request from the supplied method, endpoint, body, and headers, sends it through the already-authenticated pipeline, and returns only the response body by default. Use `--full` to include the HTTP status code, response headers, raw content, and parsed JSON. There is no LRO polling and no pagination — a single request is sent and a single untyped response is returned. The ARM bearer token is attached to every request, so absolute URLs must target an Azure management host (management.azure.com and the US Gov, China, and Germany sovereign clouds). The endpoint can be supplied two ways, which are mutually exclusive: as a full URL with `--uri`, or piece-wise with the ByParameters flags (`--subscription`, `--resource-group`, `--resource-provider`, `--resource-type`, `--name`, `--api-version`). Supported parameters: `--method` (HTTP method, defaults to GET), `--uri` (absolute URL or relative ARM path; `{subscriptionId}` is replaced with the current subscription), `--body` (request body, typically JSON; sent whenever provided, for any method), `--headers` (KEY=VALUE request headers), `--full` (return the response envelope), `--subscription`, `--resource-group`, `--resource-provider`, `--resource-type`, `--name`, `--api-version`, and `--uri-parameters` (KEY=VALUE query-string parameters, allowed in both modes). An HTTP status of 400 or higher still prints the response body but exits non-zero.

JSON and JSONC output is prettified in an interactive terminal and compact when piped or redirected, including after `--query`. Valid JSON is recognized regardless of the response content type. Queries select from the body by default (for example, `--query value[].name`), or from the envelope with `--full` (for example, `--full --query json.value[].name`). Non-JSON and malformed JSON bodies are returned as raw content; malformed JSON advertised as JSON also produces a diagnostic. Empty response bodies produce no stdout output unless `--full` is specified. Use `-o json` to preserve JSON scalar types when piping into JSON-consuming tools; otherwise scalar results use the configured simple output format (TSV by default). Other commands and output formats are unaffected. PowerShell `Invoke-AzRestMethod` continues to return the response envelope by default.

### Inspect the full HTTP response

```bash
az rest --uri "/subscriptions/{subscriptionId}/resourceGroups?api-version=2021-04-01" --full
```

### GET a resource group with a full URI

```bash
az rest --method GET --uri "/subscriptions/{subscriptionId}/resourceGroups/myresourcegroup?api-version=2021-04-01"
```

### GET a resource using the piece-wise ByParameters flags

```bash
az rest --method GET --subscription 00000000-0000-0000-0000-000000000000 --resource-group myresourcegroup --resource-provider Microsoft.Storage --resource-type storageAccounts --name mystorageacct --api-version 2023-01-01
```

### PUT a resource with a JSON body and a custom header

```bash
az rest --method PUT --uri "/subscriptions/{subscriptionId}/resourceGroups/myresourcegroup?api-version=2021-04-01" --body '{"location":"eastus"}' --headers Content-Type=application/json
```

### List resources with an extra query-string parameter

```bash
az rest --method GET --uri "/subscriptions/{subscriptionId}/resources" --uri-parameters api-version=2021-04-01 '$filter=resourceType eq '\''Microsoft.Storage/storageAccounts'\'''
```
