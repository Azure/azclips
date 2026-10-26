---
summary: Create or update a subnet.
status: ga
schema: 1.0.0
online version:
---

Description: Create or replace a subnet in an existing virtual network.

### Create a subnet

```bash
az network vnet subnet create --resource-group myrg --vnet-name myvnet --name web --address-prefixes 10.0.1.0/24
```

### Create a delegated subnet

```bash
az network vnet subnet create --resource-group myrg --vnet-name myvnet --name apps \
  --address-prefixes 10.0.2.0/24 --delegations Microsoft.Web/serverFarms
```

### Create a subnet with a network security group and service endpoints

```bash
az network vnet subnet create --resource-group myrg --vnet-name myvnet --name data \
  --address-prefixes 10.0.3.0/24 --nsg mynsg --service-endpoints Microsoft.Storage Microsoft.Sql
```
