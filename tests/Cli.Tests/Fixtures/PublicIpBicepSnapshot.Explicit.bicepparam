using 'PublicIpBicepCreate.TagsPresent.json'

param publicIpAddressName = 'my-pip'
param location = 'eastus'
param skuName = 'Standard'
param skuTier = 'Regional'
param tags = { Owner: 'team-A' }
param publicIPAllocationMethod = 'Static'
param publicIPAddressVersion = 'IPv4'
param idleTimeoutInMinutes = 30
