---
summary: Update a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Update tags, identity isolation scope, resource assignment restrictions, or generic properties on a user-assigned managed identity.

### Update an identity's isolation scope

```bash
az identity update --name myIdentity --resource-group myResourceGroup --isolation-scope None
```

### Update an identity by resource ID

```bash
az identity update --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myResourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/myIdentity --tags env=prod
```

### Update resource assignment restrictions

```bash
az identity update --name myIdentity --resource-group myResourceGroup --resource-restriction '{"providers":["Microsoft.Storage"]}' --isolation-scope Regional
```
