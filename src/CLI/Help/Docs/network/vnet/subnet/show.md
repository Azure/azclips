---
summary: Show a subnet.
status: ga
schema: 1.0.0
online version:
---

Description: Show a subnet in a virtual network.

### Show a subnet

```bash
az network vnet subnet show --resource-group myrg --vnet-name myvnet --name web
```

### Expand the subnet's network security group

```bash
az network vnet subnet show --resource-group myrg --vnet-name myvnet --name web --expand networkSecurityGroup
```
