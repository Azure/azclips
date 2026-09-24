# Deployment Operations — API 映射参考

记录 `Microsoft.Resources` deployment operations 相关 API 与 Azure CLI / Azure PowerShell 命令的对应关系。

所有操作均为管理平面 `GET` 请求，Base URL 为 `https://management.azure.com`。

## API 与命令映射

| # | REST API | HTTP | Azure CLI | Azure PowerShell |
|---|---|---|---|---|
| 1 | `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations` | GET | `az deployment operation group list` | `Get-AzResourceGroupDeploymentOperation` |
| 2 | `/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/deployments/{deploymentName}/operations/{operationId}` | GET | `az deployment operation group show` | 无对应命令 |
| 3 | `/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations` | GET | `az deployment operation sub list` | `Get-AzDeploymentOperation`（不带 `-OperationId`） |
| 4 | `/subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}` | GET | `az deployment operation sub show` | `Get-AzDeploymentOperation -OperationId` |
| 5 | `/providers/Microsoft.Management/managementGroups/{groupId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations` | GET | `az deployment operation mg list` | `Get-AzManagementGroupDeploymentOperation`（不带 `-OperationId`） |
| 6 | `/providers/Microsoft.Management/managementGroups/{groupId}/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}` | GET | `az deployment operation mg show` | `Get-AzManagementGroupDeploymentOperation -OperationId` |
| 7 | `/providers/Microsoft.Resources/deployments/{deploymentName}/operations` | GET | `az deployment operation tenant list` | `Get-AzTenantDeploymentOperation`（不带 `-OperationId`） |
| 8 | `/providers/Microsoft.Resources/deployments/{deploymentName}/operations/{operationId}` | GET | `az deployment operation tenant show` | `Get-AzTenantDeploymentOperation -OperationId` |

八个 API 对应同一种资源类型 `Microsoft.Resources/deployments` 的 operations 子资源，差异在于部署所处的 scope。

资源组 scope 的 operations 路径省略了 `providers/Microsoft.Resources`，属于历史遗留：同 scope 的部署资源本身（get/put/delete）仍使用完整路径，只有 operations 子资源在 `routes.tsp` 中被写成短路径，`readme.md` 为此保留了 `PathResourceProviderMatchNamespace` lint 抑制。

## Swagger 模块

规范位于 `azure-rest-api-specs` 的独立 deployments 模块，而非 `Microsoft.Resources/resources`：

```text
specification/resources/resource-manager/Microsoft.Resources/deployments/
```

AutoRest 配置为该目录下的 `readme.md`，`title: DeploymentsClient`。

| 工具 | readme tag | swagger 文件 |
|---|---|---|
| Azure CLI | `package-2025-04` | `stable/2025-04-01/deployments.json` |
| Azure PowerShell | `package-2024-11` | `stable/2024-11-01/deployments.json` |

TypeSpec 源文件同目录：`main.tsp`、`routes.tsp`、`DeploymentExtended.tsp`、`models.tsp`、`client.tsp`、`back-compatible.tsp`。`main.tsp` 的 `Versions` 仅包含 `2025-04-01` 与 `2026-06-01`，`2024-11-01` 只存在于旧 swagger JSON。

`client.tsp` 中的 `@@clientName(Microsoft.Resources, "DeploymentsMgmtClient", "python")` 对应 Azure CLI 使用的 Python 客户端类型。

### Operation ID

| API | Swagger operationId | TypeSpec 操作 |
|---|---|---|
| 1 | `DeploymentOperations_List` | `DeploymentOperationsOperationGroup.list` |
| 2 | `DeploymentOperations_Get` | `DeploymentOperationsOperationGroup.get` |
| 3 | `DeploymentOperations_ListAtSubscriptionScope` | `DeploymentExtendedSubscriptionGroup.deploymentOperationsListAtSubscriptionScope` |
| 4 | `DeploymentOperations_GetAtSubscriptionScope` | `DeploymentExtendedSubscriptionGroup.deploymentOperationsGetAtSubscriptionScope` |
| 5 | `DeploymentOperations_ListAtManagementGroupScope` | `DeploymentExtendedManagementGroup.deploymentOperationsListAtManagementGroupScope` |
| 6 | `DeploymentOperations_GetAtManagementGroupScope` | `DeploymentExtendedManagementGroup.deploymentOperationsGetAtManagementGroupScope` |
| 7 | `DeploymentOperations_ListAtTenantScope` | `DeploymentsExtendedTenant.listAtTenantScope` |
| 8 | `DeploymentOperations_GetAtTenantScope` | `DeploymentsExtendedTenant.deploymentOperationsGetAtTenantScope` |

spec 另有通用 scope 版本 `GetAtScope` / `ListAtScope`，CLI 与 PowerShell 当前均未使用。

## API version

两套工具通过各自 SDK 固定不同的默认 API version。

| 工具 | SDK | API version |
|---|---|---|
| Azure CLI | `azure-mgmt-resource-deployments==1.0.0b1` | `2025-04-01` |
| Azure PowerShell | `Resources.Management.Sdk`（`ResourceManagementClient`） | `2024-11-01` |

## 调用链

Azure CLI：

```text
az deployment operation <scope> <verb>
  -> command_modules/resource/custom.py
  -> DeploymentsMgmtClient.deployment_operations
  -> azure-mgmt-resource-deployments
  -> ARM REST API
```

Azure PowerShell：

```text
Get-Az*DeploymentOperation
  -> NewResourceManagerSdkClient
  -> ResourceManagementClient.DeploymentOperations
  -> Resources.Management.Sdk（AutoRest 生成）
  -> ARM REST API
```

两者都是 SDK 调用，而非 TypeSpec/AAZ codegen 直接生成的 atomic command。

## CLI 命令与 SDK 方法

| Azure CLI | SDK 方法 | API |
|---|---|---|
| `az deployment operation group list` | `list` | 1 |
| `az deployment operation group show` | `get` | 2 |
| `az deployment operation sub list` | `list_at_subscription_scope` | 3 |
| `az deployment operation sub show` | `get_at_subscription_scope` | 4 |
| `az deployment operation mg list` | `list_at_management_group_scope` | 5 |
| `az deployment operation mg show` | `get_at_management_group_scope` | 6 |
| `az deployment operation tenant list` | `list_at_tenant_scope` | 7 |
| `az deployment operation tenant show` | `get_at_tenant_scope` | 8 |

`show` 接受多个 operation ID，CLI 会逐个发送单项 GET 请求。

## PowerShell 命令与 SDK 方法

| Azure PowerShell | 条件 | SDK 方法 | API |
|---|---|---|---|
| `Get-AzResourceGroupDeploymentOperation` | 始终 | `List` | 1 |
| `Get-AzDeploymentOperation` | 无 `-OperationId` | `ListAtSubscriptionScope` | 3 |
| `Get-AzDeploymentOperation` | 有 `-OperationId` | `GetAtSubscriptionScope` | 4 |
| `Get-AzManagementGroupDeploymentOperation` | 无 `-OperationId` | `ListAtManagementGroupScope` | 5 |
| `Get-AzManagementGroupDeploymentOperation` | 有 `-OperationId` | `GetAtManagementGroupScope` | 6 |
| `Get-AzTenantDeploymentOperation` | 无 `-OperationId` | `ListAtTenantScope` | 7 |
| `Get-AzTenantDeploymentOperation` | 有 `-OperationId` | `GetAtTenantScope` | 8 |

`Get-AzResourceGroupDeploymentOperation` 未暴露 `-OperationId`，因此只会调用 list API。

## 查询参数与分页

- 所有请求携带 `api-version`。
- 仅 list API 支持 `$top`。
- list 响应包含 `nextLink` 时，后续请求直接 `GET {nextLink}`，不重新拼接查询参数。
