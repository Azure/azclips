// Sample resource-group-scoped Bicep template for manual deployment testing.
// Deploys a single Standard_LRS storage account. The name is derived from a
// parameter so each run can pass a unique, timestamped value.
targetScope = 'resourceGroup'

@description('Globally-unique storage account name (3-24 lowercase alphanumerics).')
param storageAccountName string

@description('Location for the storage account. Defaults to the resource group region.')
param location string = resourceGroup().location

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

output storageAccountId string = storageAccount.id
