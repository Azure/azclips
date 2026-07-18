---
summary: Create a new resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Provision a new resource group in your Azure subscription. Specify the resource group name and location (region). The subscription ID is optional and defaults to the current subscription when omitted. The resource group will be created immediately and can be used to organize your Azure resources.

### Create a resource group in the current subscription

```bash
az group create --name myresourcegroup --location eastus
```

### Create a resource group in a specific subscription

```bash
az group create --name myresourcegroup --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### Create a resource group in West US

```bash
az group create --name myrg --location westus --subscription-id 00000000-0000-0000-0000-000000000000
```
