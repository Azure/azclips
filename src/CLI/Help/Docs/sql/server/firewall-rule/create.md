---
summary: Create a firewall rule on an Azure SQL server.
status: preview
schema: 1.0.0
online version:
---

Description: Create a server-level firewall rule (`Microsoft.Sql/servers/firewallRules`), mirroring `az sql server firewall-rule create`. `--resource-group` (required) is the resource group that holds the server, `--server` (required) is the SQL server the rule applies to, and `--name` (required) is the rule name. `--start-ip-address` and `--end-ip-address` (both required) define the inclusive IPv4 range allowed through the server's public endpoint; use the same value for both to allow a single address. A firewall rule is a proxy resource, so it has no region of its own and there is no location flag. Setting both addresses to `0.0.0.0` is the ARM convention for "allow all Azure-internal IP addresses" rather than a literal address, and is how the well-known `AllowAllWindowsAzureIps` rule is expressed. `--subscription-id` (optional) selects the subscription. The request is a create-or-update, so re-running it with a different range updates the existing rule. The operation is synchronous, so the command returns the stored rule immediately. Addresses are validated by the service, which rejects malformed addresses and ranges whose end address is lower than the start address.

### Allow a single client IP address

```bash
az sql server firewall-rule create --resource-group myResourceGroup --server myserver001 --name AllowMyIp --start-ip-address 203.0.113.5 --end-ip-address 203.0.113.5 --subscription-id 00000000-0000-0000-0000-000000000000
```

### Allow a range of office IP addresses

```bash
az sql server firewall-rule create -g myResourceGroup -s myserver001 -n AllowOffice --start-ip-address 203.0.113.5 --end-ip-address 203.0.113.10 --subscription-id 00000000-0000-0000-0000-000000000000
```

### Allow all Azure services to reach the server

```bash
az sql server firewall-rule create --resource-group myResourceGroup --server myserver001 --name AllowAllWindowsAzureIps --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --subscription-id 00000000-0000-0000-0000-000000000000
```
