import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";
import "./models.tsp";
import "./back-compatible.tsp";
import "./ResourceGroup.tsp";
import "./TagsResource.tsp";
import "./GenericResource.tsp";
import "./routes.tsp";
import "./client.tsp";

using TypeSpec.Rest;
using TypeSpec.Http;
using Azure.ResourceManager.Foundations;
using Azure.Core;
using Azure.ResourceManager;
using TypeSpec.Versioning;

/**
 * Provides operations for working with resources and resource groups.
 */
@armProviderNamespace
@service(#{ title: "ResourceManagementClient" })
@versioned(Versions)
@armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v3)
namespace Microsoft.Resources;

enum Versions {
  v2025_04_01: "2025-04-01",
}

#suppress "@azure-tools/typespec-azure-core/no-private-usage" "For backward compatibility"
interface Operations {
  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Operations")
  @autoRoute
  @Azure.ResourceManager.Private.armUpdateProviderNamespace
  @doc("List the operations for the provider")
  @segment("operations")
  @get
  @list
  list(
    ...Azure.ResourceManager.CommonTypes.ApiVersionParameter,

    @path
    @segment("providers")
    provider: "Microsoft.ThisWillBeReplaced",
  ): ArmResponse<OperationListResult> | CloudError;
}
