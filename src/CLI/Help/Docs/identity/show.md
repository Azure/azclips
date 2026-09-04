---
summary: Show a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Get a user-assigned managed identity by name and resource group, or by its full Azure resource ID.

### Show an identity by name

```bash
az identity show --name myIdentity --resource-group myResourceGroup
```

### Show an identity by resource ID

```bash
az identity show --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/myIdentity
```
