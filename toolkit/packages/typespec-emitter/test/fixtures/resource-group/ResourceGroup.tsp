import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";
import "@typespec/openapi";
import "@typespec/rest";
import "./models.tsp";

using TypeSpec.Rest;
using Azure.ResourceManager;
using TypeSpec.Http;
using TypeSpec.OpenAPI;

namespace Microsoft.Resources;

/**
 * Resource group information.
 */
@subscriptionResource
model ResourceGroup is Azure.ResourceManager.TrackedResource<ResourceGroupProperties> {
  ...ResourceNameParameter<
    Resource = ResourceGroup,
    KeyName = "resourceGroupName",
    SegmentName = "resourcegroups",
    NamePattern = "^[-\\w\\._\\(\\)]+$"
  >;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  managedBy?: string;
}

@armResourceOperations(#{ omitTags: true })
interface ResourceGroups {
  @tag("ResourceGroups")
  get is ArmResourceRead<ResourceGroup, Provider = {}, Error = CloudError>;

  @tag("ResourceGroups")
  checkExistence is ArmResourceCheckExistence<ResourceGroup, Provider = {}, Error = CloudError>;

  @tag("ResourceGroups")
  createOrUpdate is ArmResourceCreateOrReplaceSync<ResourceGroup, Provider = {}, Error = CloudError>;

  @patch(#{ implicitOptionality: false })
  @summary("Updates a resource group.")
  @tag("ResourceGroups")
  update is ArmCustomPatchSync<ResourceGroup, PatchModel = ResourceGroupPatchable, Provider = {}, Error = CloudError>;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-delete-operation-response-codes" "For backward compatibility"
  @summary("Deletes a resource group.")
  @tag("ResourceGroups")
  delete is ArmResourceDeleteWithoutOkAsync<
    ResourceGroup,
    Parameters = {
      @query("forceDeletionTypes")
      forceDeletionTypes?: string;
    },
    Response = ArmDeletedResponse | ArmDeleteAcceptedLroResponse,
    Provider = {},
    Error = CloudError
  >;

  @segment("resourcegroups")
  @tag("ResourceGroups")
  list is ArmResourceListByParent<
    ResourceGroup,
    Parameters = {
      @query("$filter")
      $filter?: string;

      @query("$top")
      $top?: int32;
    },
    Provider = {},
    Error = CloudError
  >;

  @tag("ResourceGroups")
  @list
  @get
  @action("resources")
  listByResourceGroup is ArmResourceActionSync<
    ResourceGroup,
    void,
    ArmResponse<ResourceListResult>,
    Parameters = {
      @query("$filter")
      $filter?: string;

      @query("$expand")
      $expand?: string;

      @query("$top")
      $top?: int32;
    },
    Provider = {},
    Error = CloudError
  >;

  #suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-post-operation-response-codes" "For backward compatibility"
  @summary("Moves resources from one resource group to another resource group.")
  @tag("Resources")
  @Azure.ResourceManager.Legacy.renamePathParameter("resourceGroupName", "sourceResourceGroupName")
  moveResources is ArmResourceActionAsync<
    ResourceGroup,
    ResourcesMoveInfo,
    NoContentResponse,
    LroHeaders = ArmLroLocationHeader<FinalResult = void>,
    Provider = {},
    Error = CloudError
  >;

  #suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-post-operation-response-codes" "For backward compatibility"
  @summary("Validates whether resources can be moved from one resource group to another resource group.")
  @tag("Resources")
  @Azure.ResourceManager.Legacy.renamePathParameter("resourceGroupName", "sourceResourceGroupName")
  validateMoveResources is ArmResourceActionAsync<
    ResourceGroup,
    ResourcesMoveInfo,
    NoContentResponse,
    LroHeaders = ArmLroLocationHeader<FinalResult = void>,
    Provider = {},
    Error = CloudError
  >;

  @tag("ResourceGroups")
  exportTemplate is ArmResourceActionAsync<
    ResourceGroup,
    ExportTemplateRequest,
    ResourceGroupExportResult,
    Provider = {},
    Error = CloudError
  >;
}

@@maxLength(ResourceGroup.name, 90);
@@minLength(ResourceGroup.name, 1);
@@doc(ResourceGroup.name, "The name of the resource group to get. The name is case insensitive.");
@@doc(ResourceGroup.properties, "The resource group properties.");
@@doc(ResourceGroups.createOrUpdate::parameters.resource, "Parameters supplied to the create or update a resource group.");
@@doc(ResourceGroups.update::parameters.properties, "Parameters supplied to update a resource group.");
@@doc(ResourceGroups.moveResources::parameters.body, "Parameters for moving resources.");
@@doc(ResourceGroups.validateMoveResources::parameters.body, "Parameters for moving resources.");
@@doc(ResourceGroups.exportTemplate::parameters.body, "Parameters for exporting the template.");
