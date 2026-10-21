---
summary: Show details for a network interface.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve the full definition of a single network interface, including its IP configurations, DNS settings, MAC address and provisioning state. Use `--expand` to inline referenced sub-resources.

### Show a network interface

```bash
az network nic show --name mynic --resource-group myrg
```

### Show using shell variables

```bash
az network nic show --name $nicName --resource-group $rg --subscription-id $subId
```

### Show a network interface with its public IP address expanded

```bash
az network nic show --name mynic --resource-group myrg --expand ipConfigurations/publicIPAddress
```
