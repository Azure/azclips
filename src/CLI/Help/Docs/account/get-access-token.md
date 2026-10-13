---
summary: Get an access token for the current signed-in context.
status: ga
schema: 1.0.0
online version:
---

Description: Acquire an access token from the current signed-in context for a resource, a well-known resource type, or an explicit list of Microsoft Entra scopes. When no target is specified, a token for Azure Resource Manager (ARM) is returned. The token value is redacted by default and is not returned. Pass --plaintext to obtain the raw token value.

### Get an access token for Azure Resource Manager (the default target)

```bash
az account get-access-token
```

### Get an access token for a well-known resource type

```bash
az account get-access-token --resource-type ms-graph
```

### Get an access token for a specific resource URL

```bash
az account get-access-token --resource https://vault.azure.net
```

### Get an access token for an explicit list of scopes

```bash
az account get-access-token --scope https://graph.microsoft.com/.default
```

### Print the raw access token as plain text

```bash
az account get-access-token --plaintext
```
