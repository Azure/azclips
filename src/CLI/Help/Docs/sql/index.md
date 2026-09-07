---
summary: Manage Azure SQL resources.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure SQL resources. Currently scoped to logical SQL servers via `az sql server create`, which mirrors the Azure CLI verb and creates a `Microsoft.Sql/servers` resource with an administrator login, optional server version, minimal TLS version, and public network access setting. The command waits for the long-running create operation to finish and returns the final server resource.

### Create a SQL server in East US

```bash
az sql server create --resource-group myResourceGroup --name myserver001 --location eastus --admin-user sqladmin --admin-password "$SQL_ADMIN_PASSWORD" --subscription-id 00000000-0000-0000-0000-000000000000
```
