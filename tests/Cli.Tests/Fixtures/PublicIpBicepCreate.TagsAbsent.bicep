// Generated from the source-aware Public IP bridge; do not edit.
targetScope = 'resourceGroup'

param publicIpAddressName resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.name

param location resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.location

param publicIPAllocationMethod resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.properties.publicIPAllocationMethod

param publicIPAddressVersion resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.properties.publicIPAddressVersion

@minValue(-2147483648)
@maxValue(2147483647)
param idleTimeoutInMinutes resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.properties.idleTimeoutInMinutes

param skuName resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.sku.name

param skuTier resourceInput<'Microsoft.Network/publicIPAddresses@2026-03-01'>.sku.tier?

resource publicIp 'Microsoft.Network/publicIPAddresses@2026-03-01' = {
  name: publicIpAddressName
  location: location
  properties: {
    publicIPAllocationMethod: publicIPAllocationMethod
    publicIPAddressVersion: publicIPAddressVersion
    idleTimeoutInMinutes: idleTimeoutInMinutes
  }
  sku: {
    name: skuName
    ...((skuTier != null) ? {
      tier: skuTier
    } : {})
  }
}
