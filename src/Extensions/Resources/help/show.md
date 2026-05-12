Summary: Show details of a specific resource group.

Description: Display information about a resource group including its name, location, provisioning state, and resource count. Specify the resource group name and optionally the subscription ID (defaults to current subscription if not provided).

### Show a resource group

```bash
azv2 group show --name myresourcegroup --subscription-id 00000000-0000-0000-0000-000000000000
```

### Show using default subscription

```bash
azv2 group show --name myresourcegroup
```

### PowerShell: Show a resource group

```powershell
Get-ResourceGroup_Get -Name "myresourcegroup" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### PowerShell: Show using default subscription

```powershell
Get-ResourceGroup_Get -Name "myresourcegroup"
```
