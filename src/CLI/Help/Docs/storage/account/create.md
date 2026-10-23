---
summary: Create a storage account.
status: preview
schema: 1.0.0
online version:
---

Description: Create an Azure Storage account in the specified resource group. The account name and resource group are required; location defaults to the resource group location, SKU defaults to Standard_RAGRS, and kind defaults to StorageV2. The account name must be 3-24 characters using lower-case letters and numbers only and must be globally unique. Optional settings cover access tiers, TLS and public access, hierarchical namespace, SFTP and NFS, network rules, customer-managed encryption keys, managed identities, Azure Files identity-based authentication, SAS and key policies, and account-level immutability. Options that are not supplied are omitted from the request so the service default applies. The command waits for the long-running create operation to finish and returns the created account.

### Create a general-purpose v2 account

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2
```

### Create a hardened account

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false --allow-shared-key-access false --https-only true
```

### Create an account restricted to specific networks

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --default-action Deny --bypass AzureServices --ip-address 13.64.0.0/16 --subnet mySubnet --vnet-name myVnet
```

### Create a Data Lake Storage Gen2 account with SFTP

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --enable-hierarchical-namespace true --enable-sftp true
```

### Create an account with a customer-managed key

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --identity-type SystemAssigned --encryption-key-vault https://myvault.vault.azure.net/ --encryption-key-name mykey
```

### Create an account with tags

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --tags env=dev owner=platform
```
