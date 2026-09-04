---
summary: Manage Azure SQL resources.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure SQL resources. Covers logical SQL servers via `az sql server create` and their databases via `az sql db create`, both mirroring the Azure CLI verbs. Creating a server produces a `Microsoft.Sql/servers` resource with an administrator login, optional server version, minimal TLS version, and public network access setting. Creating a database produces a `Microsoft.Sql/servers/databases` resource under an existing server, inheriting the server's region. Both commands wait for their long-running operation to finish and return the final resource. Server-level firewall rules are created with `az sql server firewall-rule create`, which opens an inclusive IPv4 range on the server's public endpoint and returns the stored rule immediately.

### Create a SQL server in East US

```bash
az sql server create --resource-group myResourceGroup --name myserver001 --location eastus --admin-user sqladmin --admin-password "$SQL_ADMIN_PASSWORD" --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a database on that server

```bash
az sql db create --resource-group myResourceGroup --server myserver001 --name mydb --subscription-id 00000000-0000-0000-0000-000000000000
```

### Allow a client IP address through the server's firewall

```bash
az sql server firewall-rule create --resource-group myResourceGroup --server myserver001 --name AllowMyIp --start-ip-address 203.0.113.5 --end-ip-address 203.0.113.5 --subscription-id 00000000-0000-0000-0000-000000000000
```
