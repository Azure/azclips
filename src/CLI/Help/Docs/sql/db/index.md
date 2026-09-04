---
summary: Manage databases in an Azure SQL server.
status: preview
schema: 1.0.0
online version:
---

Description: Manage databases hosted by a logical SQL server. Currently scoped to `az sql db create`, which mirrors the Azure CLI verb and creates a `Microsoft.Sql/servers/databases` resource with an optional SKU (edition, service objective, family and capacity), collation, maximum size, elastic pool membership and zone redundancy. A database inherits its region from its server, so no location is asked for. The command waits for the long-running create operation to finish and returns the final database resource.

### Create a database with the server's default settings

```bash
az sql db create --resource-group myResourceGroup --server myserver001 --name mydb --subscription-id 00000000-0000-0000-0000-000000000000
```
