---
summary: Create a network security group.
status: ga
schema: 1.0.0
online version:
---

Description: Create an Azure network security group. When `--location` is omitted, the resource group's location is used.

### Create a network security group with tags

```bash
az network nsg create --name my-nsg --resource-group my-rg --location eastus --tags owner=network env=test
```

### Create in the resource group's location

```bash
az network nsg create --name my-nsg --resource-group my-rg
```
