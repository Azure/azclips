---
summary: Manage firewall rules on an Azure SQL server.
status: preview
schema: 1.0.0
online version:
---

Description: Manage server-level firewall rules on an Azure SQL logical server (`Microsoft.Sql/servers/firewallRules`). A firewall rule opens an inclusive IPv4 range on the server's public endpoint. Backed by `az sql server firewall-rule create`, which mirrors the corresponding Azure CLI verb. Creating a rule is a single synchronous request, so the command returns the stored rule immediately.

### Allow a single client IP address

```bash
az sql server firewall-rule create --resource-group myResourceGroup --server myserver001 --name AllowMyIp --start-ip-address 203.0.113.5 --end-ip-address 203.0.113.5 --subscription-id 00000000-0000-0000-0000-000000000000
```
