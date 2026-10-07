---
summary: Show details for a public IP address.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve the full definition of a single public IP address, including its allocated IP address, SKU, DNS settings and provisioning state. Use `--expand` to inline referenced sub-resources.

### Show a public IP address

```bash
az network public-ip show --name mypip --resource-group myrg
```

### Show using shell variables

```bash
az network public-ip show --name $pipName --resource-group $rg --subscription-id $subId
```

### Show a public IP address with its IP configuration expanded

```bash
az network public-ip show --name mypip --resource-group myrg --expand ipConfiguration
```
