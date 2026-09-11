---
summary: Delete a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Delete a user-assigned managed identity by name and resource group, or by one or more full Azure resource IDs.

### Delete an identity by name

```bash
az identity delete --name myIdentity --resource-group myResourceGroup
```

### Delete identities by resource ID

```bash
az identity delete --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/myIdentity
```
