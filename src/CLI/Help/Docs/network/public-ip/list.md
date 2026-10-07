---
summary: List public IP addresses.
status: ga
schema: 1.0.0
online version:
---

Description: List public IP addresses in a resource group, or across the whole subscription when `--resource-group` is omitted.

### List the public IP addresses in a resource group

```bash
az network public-ip list --resource-group myrg
```

### List every public IP address in the subscription

```bash
az network public-ip list --subscription-id $subId
```
