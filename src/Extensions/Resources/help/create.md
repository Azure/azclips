Summary: Create a new resource group.

Description: Provision a new resource group in your Azure subscription. Specify the resource group name, location (region), and subscription ID. An optional resource ID can be provided for advanced scenarios. The resource group will be created immediately and can be used to organize your Azure resources.

### Create a resource group in East US

```bash
azv2 group create --name myresourcegroup --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a resource group in West US with explicit resource ID

```bash
azv2 group create --name myrg --location westus --resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### PowerShell: Create a resource group

```powershell
New-ResourceGroup -Name "myresourcegroup" -Location "eastus" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### PowerShell: Create with resource ID

```powershell
New-ResourceGroup -Name "myrg" -Location "westus" -ResourceId "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg" -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
