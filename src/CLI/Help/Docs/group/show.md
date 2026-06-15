---
summary: Show details of a specific resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Display information about a resource group including its name, location, provisioning state, and resource count. Specify the resource group name and optionally the subscription ID (defaults to current subscription if not provided).

### Show a resource group

```bash
azv2 group show --name myresourcegroup --subscription-id 00000000-0000-0000-0000-000000000000
```

### Show using default subscription

```bash
azv2 group show --name myresourcegroup
```
