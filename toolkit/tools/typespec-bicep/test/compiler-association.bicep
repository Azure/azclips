param properties resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.properties
param sku resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.sku
param location resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.location
param name resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.name

resource selected 'Microsoft.Network/publicIPAddresses@2026-03-01' = {
  name: name
  location: location
  sku: sku
  properties: {
    idleTimeoutInMinutes: 4
  }
}
