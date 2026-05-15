---
summary: Create a new resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Provision a new resource group in your Azure subscription. Specify the resource group name, location (region), and subscription ID. An optional resource ID can be provided for advanced scenarios. The resource group will be created immediately and can be used to organize your Azure resources.

### Create a resource group in East US (CLI)

```bash
azv2 group create --name myresourcegroup --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a resource group in West US with explicit resource ID (CLI)

```bash
azv2 group create --name myrg --location westus --resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a resource group (PowerShell)

```powershell
New-ResourceGroup -Name "myresourcegroup" -Location "eastus" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
