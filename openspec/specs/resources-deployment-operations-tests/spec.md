# resources-deployment-operations-tests Specification

## Purpose
TBD - created by archiving change resources-deployment-operations-tests. Update Purpose after archive.
## Requirements
### Requirement: Resource-group deployment operations scenario test
`DeploymentOperationScenarioTests.CreateDeploymentThenListAndShowOperations` SHALL replay its committed recording on the CLI and the PowerShell surface, and SHALL verify that `deployment operation group list`, `deployment operation group show`, `Get-AzResourceGroupDeploymentOperation_List` and `Get-AzResourceGroupDeploymentOperation_Get` behave as the `resources-deployment-operations` spec states: the full operation model on the CLI, the flattened operation object on PowerShell, one GET per request, and output in the requested order.

#### Scenario: T1 Resource-group operations listed and shown on both surfaces
- **WHEN** the CLI surface runs `deployment operation group list` for the scenario's resource-group deployment
- **THEN** it SHALL output one `List<DeploymentOperation>` carrying the full operation model, not `PSDeploymentOperation`
- **AND** exactly one GET SHALL have been sent, to `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations`, with no `providers/Microsoft.Resources` segment
- **AND** every operation SHALL have an `operationId` and `properties.provisioningState` `Succeeded`
- **AND** the operations whose `properties.provisioningOperation` is `Create` and whose `properties.targetResource.resourceType` is `Microsoft.Network/networkSecurityGroups` SHALL name exactly the deployment's network security groups, compared as a set
- **AND** exactly one other operation SHALL remain, with `properties.provisioningOperation` `EvaluateDeploymentOutput` and no `properties.targetResource`
- **WHEN** the CLI surface runs `deployment operation group show` with the first listed operation id
- **THEN** it SHALL output one `List<DeploymentOperation>` holding exactly that operation
- **AND** its `properties.provisioningState` SHALL be `Succeeded` and its `properties.provisioningOperation` SHALL equal that of the listed operation with the same id
- **WHEN** the CLI surface runs `deployment operation group show` with the network security group operation ids in reverse list order
- **THEN** the output's `operationId`s SHALL equal the requested ids in the requested order
- **AND** one GET SHALL have been sent per requested id, in the requested order, each to `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations/{operationId}`
- **AND** every element SHALL have `properties.provisioningOperation` `Create`, `properties.targetResource.resourceType` `Microsoft.Network/networkSecurityGroups` and `properties.provisioningState` `Succeeded`
- **WHEN** the CLI surface runs `deployment operation group show` with every listed operation id in list order
- **THEN** the output's `operationId`s SHALL equal the requested ids in the requested order
- **AND** each element's `properties.provisioningOperation`, `properties.provisioningState`, `properties.statusCode` and `properties.targetResource.id` SHALL equal those of the listed operation with the same id
- **WHEN** the PowerShell surface runs `Get-AzResourceGroupDeploymentOperation_List` for the same deployment
- **THEN** it SHALL output one `List<PSDeploymentOperation>`, not `List<DeploymentOperation>`
- **AND** the public properties of `PSDeploymentOperation` SHALL be exactly `Id`, `OperationId`, `ProvisioningState`, `StatusCode`, `StatusMessage` and `TargetResource`
- **AND** exactly one GET SHALL have been sent, to `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations`
- **AND** every element SHALL have an `OperationId`, `ProvisioningState` `Succeeded`, `StatusCode` `OK` and a null `StatusMessage`
- **AND** every element's `Id` SHALL be `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{OperationId}`, compared case-insensitively
- **AND** exactly the elements for the deployment's network security groups SHALL have a `TargetResource`, their resource ids compared as a set and case-insensitively, and the one remaining element's `TargetResource` SHALL be null
- **WHEN** the PowerShell surface runs `Get-AzResourceGroupDeploymentOperation_Get` with the first listed `OperationId`
- **THEN** it SHALL output one `List<PSDeploymentOperation>` holding exactly that operation
- **AND** that element SHALL equal the listed element with the same `OperationId` in all six properties
- **WHEN** the PowerShell surface runs `Get-AzResourceGroupDeploymentOperation_Get` with the network security group `OperationId`s in reverse list order
- **THEN** the output's `OperationId`s SHALL equal the requested ids in the requested order
- **AND** one GET SHALL have been sent per requested id, in the requested order, each to `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations/{operationId}`
- **AND** each element's `TargetResource` SHALL equal that of the listed element with the same `OperationId`
- **WHEN** the PowerShell surface runs `Get-AzResourceGroupDeploymentOperation_Get` with every listed `OperationId` in list order
- **THEN** the output's `OperationId`s SHALL equal the requested ids in the requested order
- **AND** each element SHALL equal the listed element with the same `OperationId` in all six properties

