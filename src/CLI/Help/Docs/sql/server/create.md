---
summary: Create an Azure SQL logical server.
status: preview
schema: 1.0.0
online version:
---

Description: Create an Azure SQL logical server (`Microsoft.Sql/servers`), mirroring `az sql server create`. `--resource-group` (required) is the resource group to create the server in, `--name` (required) is the server name, and `--location` (required) is the Azure region. `--admin-user` (required) and `--admin-password` (required) set the server administrator login and its password. `--server-version` selects the server version (for example `12.0`), `--minimal-tls-version` sets the minimal TLS version the server enforces, one of `None`, `1.0`, `1.1`, `1.2`, or `1.3`, and `--public-network-access` allows or blocks access from public endpoints, either `Enabled`, `Disabled`, or `SecuredByPerimeter`. `--tags` assigns resource tags as space-separated `key[=value]` pairs; a bare key creates a tag with an empty value. `--subscription-id` (optional) selects the subscription. The command waits for the create long-running operation to finish and returns the final server resource.

### Create a SQL server in East US

```bash
az sql server create --resource-group myResourceGroup --name myserver001 --location eastus --admin-user sqladmin --admin-password "$SQL_ADMIN_PASSWORD" --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a SQL server with public network access disabled, TLS 1.2 enforced, and resource tags

```bash
az sql server create -g myResourceGroup -n myserver001 -l eastus -u sqladmin -p "$SQL_ADMIN_PASSWORD" --minimal-tls-version 1.2 --public-network-access Disabled --tags env=prod owner=data-team --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a SQL server in a specific subscription and pin the server version

```bash
az sql server create --resource-group myResourceGroup --name myserver001 --location westus2 --admin-user sqladmin --admin-password "$SQL_ADMIN_PASSWORD" --server-version 12.0 --subscription-id 00000000-0000-0000-0000-000000000000
```
