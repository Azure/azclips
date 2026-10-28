# resources-deployment-operations Specification

## Purpose
TBD - created by archiving change resources-deployment-operations-get-list-ps-input-output. Update Purpose after archive.
## Requirements
### Requirement: Show deployment operations by id
Each deployment operation show command, and the `Get` parameter set of each PowerShell deployment operation cmdlet, SHALL fetch every requested operation id with its own GET at the command's scope, one after another in the order the ids are given, and SHALL output the operations of all requested ids together.

#### Scenario: Resource-group scope fetches each id in order
- **WHEN** `deployment operation group show` or `Get-AzResourceGroupDeploymentOperation -OperationIds` (`Get-AzResourceGroupDeploymentOperation_Get`) runs with one or more operation ids
- **THEN** Azclips SHALL send one `GET /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations/{operationId}` per id, one after another, in the order given

#### Scenario: Subscription scope fetches each id
- **WHEN** `deployment operation sub show` or `Get-AzDeploymentOperation -OperationIds` (`Get-AzDeploymentOperation_Get`) runs
- **THEN** each GET SHALL target `/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}`

#### Scenario: Management-group scope fetches each id
- **WHEN** `deployment operation mg show` or `Get-AzManagementGroupDeploymentOperation -OperationIds` (`Get-AzManagementGroupDeploymentOperation_Get`) runs
- **THEN** each GET SHALL target `/providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}`

#### Scenario: Tenant scope fetches each id
- **WHEN** `deployment operation tenant show` or `Get-AzTenantDeploymentOperation -OperationIds` (`Get-AzTenantDeploymentOperation_Get`) runs
- **THEN** each GET SHALL target `/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}`

#### Scenario: Show uses the active profile's subscription by default
- **WHEN** `deployment operation group show` or `deployment operation sub show` runs without `--subscription-id`, or `Get-AzResourceGroupDeploymentOperation_Get` or `Get-AzDeploymentOperation_Get` runs without `-SubscriptionId`
- **THEN** the GET for each id SHALL use the active profile's subscription

#### Scenario: CLI prints the requested operations as one array
- **WHEN** every GET succeeds for `deployment operation group show`, `deployment operation sub show`, `deployment operation mg show` or `deployment operation tenant show`
- **THEN** the CLI SHALL print one JSON array of the operations in request order, even for a single id
- **AND** each element SHALL be the full operation JSON

#### Scenario: PowerShell outputs one object per requested id
- **WHEN** every GET succeeds for `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_Get` or `Get-AzTenantDeploymentOperation_Get`
- **THEN** PowerShell SHALL output one operation object per id
- **AND** each object SHALL have the shape defined by the requirement "PowerShell deployment operation output shape"

#### Scenario: A failed GET stops the show
- **WHEN** any GET requested by `deployment operation group show`, `deployment operation sub show`, `deployment operation mg show`, `deployment operation tenant show`, `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_Get` or `Get-AzTenantDeploymentOperation_Get` fails, for example with 404 for an unknown id
- **THEN** Azclips SHALL stop at that id
- **AND** Azclips SHALL output no operations
- **AND** Azclips SHALL report the ARM error

### Requirement: List every operation of a deployment
Each deployment operation list command, and the `List` parameter set of each PowerShell deployment operation cmdlet, SHALL send a GET to the deployment's operations collection at the command's scope, SHALL follow every `nextLink` until a page has none, and SHALL output the operations of all pages together.

#### Scenario: Resource-group scope lists the operations collection
- **WHEN** `deployment operation group list` runs, or `Get-AzResourceGroupDeploymentOperation` runs without `-OperationIds` (`Get-AzResourceGroupDeploymentOperation_List`)
- **THEN** Azclips SHALL send `GET /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations`

#### Scenario: Subscription scope lists the operations collection
- **WHEN** `deployment operation sub list` runs, or `Get-AzDeploymentOperation` runs without `-OperationIds` (`Get-AzDeploymentOperation_List`)
- **THEN** Azclips SHALL send the GET to `/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations`

#### Scenario: Management-group scope lists the operations collection
- **WHEN** `deployment operation mg list` runs, or `Get-AzManagementGroupDeploymentOperation` runs without `-OperationIds` (`Get-AzManagementGroupDeploymentOperation_List`)
- **THEN** Azclips SHALL send the GET to `/providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations`

#### Scenario: Tenant scope lists the operations collection
- **WHEN** `deployment operation tenant list` runs, or `Get-AzTenantDeploymentOperation` runs without `-OperationIds` (`Get-AzTenantDeploymentOperation_List`)
- **THEN** Azclips SHALL send the GET to `/providers/Microsoft.Resources/deployments/{deploymentName}/operations`

#### Scenario: List uses the active profile's subscription by default
- **WHEN** `deployment operation group list` or `deployment operation sub list` runs without `--subscription-id`, or `Get-AzResourceGroupDeploymentOperation_List` or `Get-AzDeploymentOperation_List` runs without `-SubscriptionId`
- **THEN** the list GET SHALL use the active profile's subscription

#### Scenario: Every page is read
- **WHEN** a response page for `deployment operation group list`, `deployment operation sub list`, `deployment operation mg list`, `deployment operation tenant list`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_List` carries a `nextLink`
- **THEN** Azclips SHALL GET that link
- **AND** Azclips SHALL continue until a page has no `nextLink`

#### Scenario: CLI prints every operation as one array
- **WHEN** all pages are read for `deployment operation group list`, `deployment operation sub list`, `deployment operation mg list` or `deployment operation tenant list`
- **THEN** the CLI SHALL print one JSON array of every operation
- **AND** each element SHALL be the full operation JSON

#### Scenario: PowerShell outputs one object per operation
- **WHEN** all pages are read for `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_List`
- **THEN** PowerShell SHALL output one operation object per operation
- **AND** each object SHALL have the shape defined by the requirement "PowerShell deployment operation output shape"

#### Scenario: Deployment without operations
- **WHEN** the deployment has no operations and `deployment operation group list`, `deployment operation sub list`, `deployment operation mg list`, `deployment operation tenant list`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_List` runs
- **THEN** the CLI SHALL print `[]`
- **AND** PowerShell SHALL output nothing

#### Scenario: A failed page request stops the list
- **WHEN** any page request of `deployment operation group list`, `deployment operation sub list`, `deployment operation mg list`, `deployment operation tenant list`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_List` fails
- **THEN** Azclips SHALL report the ARM error
- **AND** Azclips SHALL output no operations

### Requirement: PowerShell cmdlets get by id or list
Each PowerShell deployment operation cmdlet SHALL list every operation of the deployment when `-OperationIds` is not given, and SHALL fetch only the given operations when it is. `Get-AzSubscriptionDeploymentOperation` SHALL run `Get-AzDeploymentOperation`, and `-Name` SHALL bind to `-DeploymentName`.

#### Scenario: Without operation ids the cmdlet lists
- **WHEN** `Get-AzResourceGroupDeploymentOperation`, `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation` runs without `-OperationIds` (`Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_List`, `Get-AzTenantDeploymentOperation_DeploymentObject`)
- **THEN** Azclips SHALL list every operation of the deployment

#### Scenario: Operation ids fetch only those operations
- **WHEN** `-OperationIds`, or its alias `-OperationId`, is given one or more ids on `Get-AzResourceGroupDeploymentOperation`, `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation` (`Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_Get`)
- **THEN** Azclips SHALL fetch only those operations

#### Scenario: Subscription cmdlet alias
- **WHEN** `Get-AzSubscriptionDeploymentOperation` is invoked
- **THEN** Azclips SHALL run `Get-AzDeploymentOperation` (`Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_Get` or `Get-AzDeploymentOperation_DeploymentObject`)

#### Scenario: Name binds to DeploymentName
- **WHEN** `-Name` is passed to `Get-AzResourceGroupDeploymentOperation`, `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation` (`Get-AzResourceGroupDeploymentOperation_List`, `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_List`, `Get-AzTenantDeploymentOperation_Get`)
- **THEN** the value SHALL bind to `-DeploymentName`

### Requirement: PowerShell deployment object pipeline input
`Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` and `Get-AzTenantDeploymentOperation` SHALL accept a deployment object from the pipeline or through `-DeploymentObject` and SHALL list every operation of the deployment the object names. `Get-AzResourceGroupDeploymentOperation` SHALL bind the deployment name and resource group from the properties of a piped object.

#### Scenario: Subscription or tenant deployment object
- **WHEN** a deployment object is piped to `Get-AzDeploymentOperation` or `Get-AzTenantDeploymentOperation`, or passed as `-DeploymentObject` (`Get-AzDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_DeploymentObject`)
- **THEN** Azclips SHALL list the operations of the deployment the object names
- **AND** Azclips SHALL use the active profile's subscription for `Get-AzDeploymentOperation` and tenant scope for `Get-AzTenantDeploymentOperation`

#### Scenario: Management-group deployment object
- **WHEN** a deployment object is piped to `Get-AzManagementGroupDeploymentOperation` (`Get-AzManagementGroupDeploymentOperation_DeploymentObject`)
- **THEN** Azclips SHALL take both the management group id and the deployment name from the object
- **AND** Azclips SHALL list that deployment's operations

#### Scenario: Deployment name comes from DeploymentName, then Name
- **WHEN** a deployment object is piped to `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation`, or passed as `-DeploymentObject` (`Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_DeploymentObject`)
- **THEN** Azclips SHALL use the object's `DeploymentName` property as the deployment name
- **AND** when `DeploymentName` is missing or empty, Azclips SHALL use the object's `Name` property

#### Scenario: Management group id comes from ManagementGroupId, then Id
- **WHEN** a deployment object is piped to `Get-AzManagementGroupDeploymentOperation`, or passed as `-DeploymentObject` (`Get-AzManagementGroupDeploymentOperation_DeploymentObject`)
- **THEN** Azclips SHALL use the object's `ManagementGroupId` property as the management group id
- **AND** when `ManagementGroupId` is missing or empty and the object's `Id` has the form `/providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}`, compared case-insensitively, Azclips SHALL use its `{managementGroupId}` segment

#### Scenario: Deployment object that cannot be resolved
- **WHEN** a deployment object piped to `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation`, or passed as `-DeploymentObject` (`Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_DeploymentObject`), yields no deployment name, or an object piped to `Get-AzManagementGroupDeploymentOperation` (`Get-AzManagementGroupDeploymentOperation_DeploymentObject`) yields no management group id
- **THEN** Azclips SHALL report an error that names the value the object lacks
- **AND** Azclips SHALL send no request for that object
- **AND** Azclips SHALL go on with the next piped object
#### Scenario: Deployment object excludes operation ids
- **WHEN** `-DeploymentObject` is used with `Get-AzDeploymentOperation`, `Get-AzManagementGroupDeploymentOperation` or `Get-AzTenantDeploymentOperation` (`Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_DeploymentObject`)
- **THEN** `-OperationIds` SHALL NOT be accepted together with it
- **AND** the cmdlet SHALL list every operation

#### Scenario: Resource-group deployment piped by property name
- **WHEN** an object with `DeploymentName` (or `Name`) and `ResourceGroupName` properties is piped to `Get-AzResourceGroupDeploymentOperation` (`Get-AzResourceGroupDeploymentOperation_List`)
- **THEN** Azclips SHALL bind both values
- **AND** Azclips SHALL list that deployment's operations

### Requirement: PowerShell deployment operation output shape
The PowerShell deployment operation cmdlets SHALL output each operation as an object with only `Id`, `OperationId`, `ProvisioningState`, `StatusCode`, `StatusMessage` and `TargetResource`, and the CLI deployment operation commands SHALL keep printing the full operation JSON.

#### Scenario: PowerShell output properties
- **WHEN** `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_Get`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_DeploymentObject` outputs an operation
- **THEN** the object SHALL have only `Id`, `OperationId`, `ProvisioningState`, `StatusCode`, `StatusMessage` and `TargetResource`
- **AND** these SHALL come from the operation's `id`, `operationId`, `properties.provisioningState`, `properties.statusCode`, `properties.statusMessage.error` and `properties.targetResource.id`

#### Scenario: Status message error without details
- **WHEN** `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_Get`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_DeploymentObject` outputs an operation whose error has no `details`
- **THEN** `StatusMessage` SHALL be the string `<message> (Code:<code>)`

#### Scenario: Status message error with details
- **WHEN** `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_Get`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_DeploymentObject` outputs an operation whose error has `details`
- **THEN** `StatusMessage` SHALL be `<message> (Code: <code>)` followed by one line per detail
- **AND** each detail SHALL be formatted the same way, prefixed with `" - "`, and indented two more spaces per nesting level

#### Scenario: No status message error or target resource
- **WHEN** `Get-AzResourceGroupDeploymentOperation_Get`, `Get-AzResourceGroupDeploymentOperation_List`, `Get-AzDeploymentOperation_Get`, `Get-AzDeploymentOperation_List`, `Get-AzDeploymentOperation_DeploymentObject`, `Get-AzManagementGroupDeploymentOperation_Get`, `Get-AzManagementGroupDeploymentOperation_List`, `Get-AzManagementGroupDeploymentOperation_DeploymentObject`, `Get-AzTenantDeploymentOperation_Get`, `Get-AzTenantDeploymentOperation_List` or `Get-AzTenantDeploymentOperation_DeploymentObject` outputs an operation that has no status-message error or no target resource
- **THEN** `StatusMessage` or `TargetResource`, respectively, SHALL be null

#### Scenario: CLI output stays the full operation JSON
- **WHEN** `deployment operation group show`, `deployment operation group list`, `deployment operation sub show`, `deployment operation sub list`, `deployment operation mg show`, `deployment operation mg list`, `deployment operation tenant show` or `deployment operation tenant list` outputs operations
- **THEN** the output SHALL stay the full operation JSON

