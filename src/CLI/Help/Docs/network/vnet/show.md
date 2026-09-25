---
summary: Show details for a virtual network.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve the full definition of a single virtual network, including its address space, subnets, and provisioning state. Use `--expand` to inline referenced sub-resources.

### Show a virtual network

```bash
az network vnet show --name myvnet --resource-group myrg
```

### Show using shell variables

```bash
az network vnet show --name $vnetName --resource-group $rg --subscription-id $subId
```

### Show a virtual network with its subnet IP configurations expanded

```bash
az network vnet show --name myvnet --resource-group myrg --expand subnets/ipConfigurations
```
