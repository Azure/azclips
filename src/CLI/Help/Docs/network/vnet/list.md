---
summary: List virtual networks.
status: ga
schema: 1.0.0
online version:
---

Description: List virtual networks in a resource group, or across the whole subscription when `--resource-group` is omitted. Results are paginated transparently: every page is followed and the combined set is returned as a single array.

### List virtual networks in a resource group

```bash
az network vnet list --resource-group myrg
```

### List every virtual network in the subscription

```bash
az network vnet list --subscription-id 00000000-0000-0000-0000-000000000000
```
