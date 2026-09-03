---
summary: Create a user-assigned managed identity.
status: ga
schema: 1.0.0
online version:
---

Description: Create a user-assigned managed identity in the specified subscription and resource group.

### Create an identity

```bash
az identity create --name myIdentity --resource-group myResourceGroup --location eastus
```

### Create an identity with regional assignment restrictions

```bash
az identity create --name myIdentity --resource-group myResourceGroup --location eastus --isolation-scope Regional
```

### Create an identity with resource assignment restrictions

```bash
az identity create --name myIdentity --resource-group myResourceGroup --location eastus --resource-restriction '{"providers":["Microsoft.Compute"]}'
```
