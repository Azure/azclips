---
summary: Create a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Create a user-assigned managed identity in the specified subscription and resource group. If `--location` is omitted, the resource group's location is used.

### Create an identity

```bash
az identity create --name myIdentity --resource-group myResourceGroup
```

### Create an identity with regional assignment restrictions

```bash
az identity create --name myIdentity --resource-group myResourceGroup --isolation-scope Regional
```

### Create an identity with resource assignment restrictions

```bash
az identity create --name myIdentity --resource-group myResourceGroup --resource-restriction '{"providers":["Microsoft.Compute"]}'
```
