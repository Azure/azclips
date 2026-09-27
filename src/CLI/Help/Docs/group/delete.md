---
summary: Delete a resource group and all resources it contains.
status: ga
schema: 1.0.0
online version:
---

Description: Delete a resource group and all resources it contains. The command waits for deletion to finish.

### Delete a resource group

```bash
az group delete --name myresourcegroup
```

### Force deletion of supported virtual machines

```bash
az group delete --name myresourcegroup --force-deletion-types Microsoft.Compute/virtualMachines
```
