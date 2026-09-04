---
summary: Create a database in an Azure SQL server.
status: preview
schema: 1.0.0
online version:
---

Description: Create an Azure SQL database (`Microsoft.Sql/servers/databases`), mirroring `az sql db create`. `--resource-group` (required) is the resource group that holds the server, `--server` (required) is the SQL server to create the database in, and `--name` (required) is the database name. The database inherits its region from its server, so there is no location flag — the command reads the server first and reuses its location. `--edition` sets the service tier (for example `GeneralPurpose` or `Standard`), `--service-objective` sets the SKU name (for example `S0` or `GP_Gen5_2`), and `--family` and `--capacity` set the compute generation and the vCore or DTU count. `--collation` sets the database collation, `--max-size-bytes` caps the database size in bytes, `--elastic-pool` places the database in the named elastic pool on the same server, and `--zone-redundant` makes the database zone redundant. `--subscription-id` (optional) selects the subscription. The command waits for the create long-running operation to finish and returns the final database resource.

### Create a database with the server's default settings

```bash
az sql db create --resource-group myResourceGroup --server myserver001 --name mydb --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a Standard S0 database with an explicit collation and size cap

```bash
az sql db create -g myResourceGroup -s myserver001 -n mydb -e Standard --service-objective S0 --collation SQL_Latin1_General_CP1_CI_AS --max-size-bytes 268435456000 --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a zone-redundant General Purpose database on Gen5 hardware

```bash
az sql db create --resource-group myResourceGroup --server myserver001 --name mydb --edition GeneralPurpose --family Gen5 --capacity 2 --zone-redundant --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a database inside an existing elastic pool

```bash
az sql db create --resource-group myResourceGroup --server myserver001 --name mydb --elastic-pool mypool --subscription-id 00000000-0000-0000-0000-000000000000
```
