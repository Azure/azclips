targetScope = 'managementGroup'

param subscriptionId string
param resourceGroupName string
param marker string = 'azclips-management-group-deployment'

module nestedDeployment './resource-group-empty.bicep' = {
  name: 'azclips-management-group-nested'
  scope: resourceGroup(subscriptionId, resourceGroupName)
  params: {
    marker: marker
  }
}

output marker string = marker
