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

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-duplicate-base-parameter" "For backward compatibility"
@singleton("default")
@extensionResource
model TagsResource is Azure.ResourceManager.ExtensionResource<Tags, false> {
  ...ResourceNameParameter<Resource = TagsResource, KeyName = "tag", SegmentName = "tags", NamePattern = "">;
}

@armResourceOperations(#{ omitTags: true })
interface TagsResources {
  @tag("Tags")
  @summary("Gets the entire set of tags on a resource or subscription.")
  getAtScope is Extension.Read<Extension.ScopeParameter, TagsResource, Error = CloudError>;

  #suppress "@azure-tools/typespec-azure-core/no-openapi" "Extension decorator for LRO options"
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-put-operation-response-codes" "For backward compatibility"
  @tag("Tags")
  @summary("Creates or updates the entire set of tags on a resource or subscription.")
  createOrUpdateAtScope is Extension.CreateOrReplaceAsync<
    Extension.ScopeParameter,
    TagsResource,
    Response =
      | ArmResourceUpdatedResponse<TagsResource>
      | ArmAcceptedLroResponse<
          LroHeaders = ArmLroLocationHeader<FinalResult = TagsResource> & Azure.Core.Foundations.RetryAfterHeader
        >,
    Error = CloudError
  >;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-patch" "For backward compatibility"
  @tag("Tags")
  @summary("Selectively updates the set of tags on a resource or subscription.")
  @patch(#{ implicitOptionality: false })
  updateAtScope is Extension.CustomPatchAsync<
    Extension.ScopeParameter,
    TagsResource,
    PatchModel = TagsPatchResource,
    Error = CloudError
  >;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-delete-operation-response-codes" "For backward compatibility"
  @tag("Tags")
  @summary("Deletes the entire set of tags on a resource or subscription.")
  deleteAtScope is Extension.DeleteWithoutOkAsync<
    Extension.ScopeParameter,
    TagsResource,
    Response = ArmDeletedResponse | ArmDeleteAcceptedLroResponse,
    Error = CloudError
  >;
}

@@doc(TagsResource.name, "");
@@doc(TagsResource.properties, "The set of tags.");
@@doc(TagsResources.createOrUpdateAtScope::parameters.resource, "");
@@doc(TagsResources.updateAtScope::parameters.properties, "");
