---
summary: Authenticate to Azure.
status: ga
schema: 1.0.0
online version:
---

Description: Authenticate to Azure and persist account context for subsequent commands.

### Login with subscription id

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with device code
 
```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

### Login with a managed identity

```bash
az login --identity
```

### Login with a user-assigned managed identity

```bash
az login --identity --client-id 11111111-1111-1111-1111-111111111111
```

### Login with a managed identity using short flags

```bash
az login -i
```
