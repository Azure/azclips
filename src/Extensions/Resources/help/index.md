---
summary: Manage resource groups in your Azure subscription.
status: ga
schema: 1.0.0
online version:
---

Description: Create, view, update, and delete resource groups. Resource groups are containers for managing related Azure resources. Use create to establish a new resource group, list to view all groups, show to view details of a specific group, and delete to remove a group.

### Create a resource group (CLI)

```bash
azv2 group create --name myresourcegroup --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### List all resource groups (CLI)

```bash
azv2 group list --subscription-id 00000000-0000-0000-0000-000000000000
```

### Show a specific resource group (CLI)

```bash
azv2 group show --name myresourcegroup --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a resource group (PowerShell)

```powershell
New-ResourceGroup -Name "myresourcegroup" -Location "eastus" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### List resource groups (PowerShell)

```powershell
Get-ResourceGroup_List -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### Show a resource group (PowerShell)

```powershell
Get-ResourceGroup_Get -Name "myresourcegroup" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
