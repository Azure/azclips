Summary: List all resource groups in a subscription.

Description: Retrieve a list of all resource groups in the specified Azure subscription. If no subscription ID is provided, the default subscription from your current login context is used. Output includes group names and locations.

### List all resource groups

```bash
azv2 group list --subscription-id 00000000-0000-0000-0000-000000000000
```

### List using default subscription

```bash
azv2 group list
```

### PowerShell: List resource groups

```powershell
Get-ResourceGroup_List -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### PowerShell: List using default subscription

```powershell
Get-ResourceGroup_List
```
