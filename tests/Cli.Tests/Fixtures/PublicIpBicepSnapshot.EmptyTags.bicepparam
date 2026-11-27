using 'PublicIpBicepCreate.TagsPresent.json'

param publicIpAddressName = 'my-pip'
param location = 'eastus'
param skuName = 'Standard'
param tags = {}
param publicIPAllocationMethod = 'Static'
param publicIPAddressVersion = 'IPv4'
param idleTimeoutInMinutes = 4
