---
summary: Delete a virtual network.
status: ga
schema: 1.0.0
online version:
---

Description: Remove the specified virtual network from its resource group. The delete fails while the virtual network is still in use — detach or delete dependent resources such as network interfaces, private endpoints, and gateways first.

### Delete a virtual network

```bash
az network vnet delete --name myvnet --resource-group myrg
```

### Delete using shell variables

```bash
az network vnet delete --name $vnetName --resource-group $rg --subscription-id $subId
```
