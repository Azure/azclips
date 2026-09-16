---
summary: Check whether a resource group exists.
status: ga
schema: 1.0.0
online version:
---

Description: Check whether a resource group exists in a subscription. The command returns `true` when the resource group exists and `false` when it does not. The subscription defaults to the active profile when `--subscription-id` is omitted.

### Check whether a resource group exists

```bash
az group exists --name myresourcegroup
```

### Check in a specific subscription

```bash
az group exists --name myresourcegroup --subscription-id 00000000-0000-0000-0000-000000000000
```
