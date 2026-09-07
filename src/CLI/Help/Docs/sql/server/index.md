---
summary: Manage Azure SQL logical servers.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure SQL logical servers (`Microsoft.Sql/servers`). Backed by `az sql server create`, which mirrors the corresponding Azure CLI verb and supports the administrator login and password, the server version, the minimal TLS version enforced by the server, and whether public network access is allowed.

### Create a SQL server in East US

```bash
az sql server create --resource-group myResourceGroup --name myserver001 --location eastus --admin-user sqladmin --admin-password "$SQL_ADMIN_PASSWORD" --subscription-id 00000000-0000-0000-0000-000000000000
```
