---
summary: Update a subnet.
status: ga
schema: 1.0.0
online version:
---

Description: Update the properties of an existing subnet in a virtual network. Only the options you give are changed. Use "" to detach a network security group, route table, or NAT gateway.

### Attach a network security group to a subnet

```bash
az network vnet subnet update --resource-group myrg --vnet-name myvnet --name web --network-security-group mynsg
```

### Detach the route table and add a service endpoint

```bash
az network vnet subnet update --resource-group myrg --vnet-name myvnet --name web \
  --route-table "" --service-endpoints Microsoft.Storage
```
