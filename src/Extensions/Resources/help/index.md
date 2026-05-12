Summary: Manage resource groups in your Azure subscription.

Description: Create, view, update, and delete resource groups. Resource groups are containers for managing related Azure resources. Use create to establish a new resource group, list to view all groups, show to view details of a specific group, and delete to remove a group.

### Create a resource group

```bash
azv2 group create --name myresourcegroup --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### List all resource groups

```bash
azv2 group list --subscription-id 00000000-0000-0000-0000-000000000000
```

### Show a specific resource group

```bash
azv2 group show --name myresourcegroup --subscription-id 00000000-0000-0000-0000-000000000000
```

### PowerShell: Create a resource group

```powershell
New-ResourceGroup -Name "myresourcegroup" -Location "eastus" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### PowerShell: List resource groups

```powershell
Get-ResourceGroup_List -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### PowerShell: Show a resource group

```powershell
Get-ResourceGroup_Get -Name "myresourcegroup" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
