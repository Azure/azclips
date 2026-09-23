targetScope = 'tenant'

param subscriptionId string
param resourceGroupName string
param marker string = 'azclips-tenant-deployment'

module nestedDeployment './resource-group-empty.bicep' = {
  name: 'azclips-tenant-nested'
  scope: resourceGroup(subscriptionId, resourceGroupName)
  params: {
    marker: marker
  }
}

output marker string = marker
