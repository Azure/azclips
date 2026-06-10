// Deliberately malformed Bicep used to verify that compiler diagnostics surface
// through the CLI/PS error path. 'resourcz' is not a valid keyword and the type
// reference is incomplete, so 'bicep build --stdout' exits non-zero with diagnostics.
targetScope = 'resourceGroup'

param storageAccountName string

resourcz storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
