---
summary: List network interfaces.
status: ga
schema: 1.0.0
online version:
---

Description: List network interfaces in a resource group, or across the whole subscription when `--resource-group` is omitted.

### List the network interfaces in a resource group

```bash
az network nic list --resource-group myrg
```

### List every network interface in the subscription

```bash
az network nic list --subscription-id $subId
```
