---
summary: Show a network security group.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve a single Azure network security group.

### Show a network security group

```bash
az network nsg show --name my-nsg --resource-group my-rg
```

### Expand the associated network interfaces

```bash
az network nsg show --name my-nsg --resource-group my-rg --expand networkInterfaces
```
