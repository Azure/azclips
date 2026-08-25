import "@azure-tools/typespec-azure-core";
import "@typespec/rest";
import "./models.tsp";
import "@azure-tools/typespec-azure-resource-manager";
import "@typespec/openapi";

using TypeSpec.Rest;
using TypeSpec.Http;
using Azure.ResourceManager;
using TypeSpec.OpenAPI;

namespace Microsoft.Resources;

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-interface-requires-decorator" "For backward compatibility"
interface ProvidersOperationGroup {
  @tag("Providers")
  @route("/providers")
  @list
  @get
  listAtTenantScope(
    ...ApiVersionParameter,

    @query("$expand")
    $expand?: string,
  ): ArmResponse<ProviderListResult> | CloudError;

  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/unregister")
  @post
  unregister(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    @path
    resourceProviderNamespace: string,
  ): ArmResponse<Provider> | CloudError;

  @tag("Providers")
  @route("/providers/Microsoft.Management/managementGroups/{groupId}/providers/{resourceProviderNamespace}/register")
  @post
  registerAtManagementGroupScope(
    ...ApiVersionParameter,

    @path
    resourceProviderNamespace: string,

    #suppress "@azure-tools/typespec-azure-core/documentation-required" "Existing parameter from original swagger"
    @path
    @minLength(1)
    @maxLength(90)
    groupId: string,
  ): OkResponse | CloudError;

  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/providerPermissions")
  @get
  providerPermissions(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    @path
    resourceProviderNamespace: string,
  ): ArmResponse<ProviderPermissionListResult> | CloudError;

  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/register")
  @post
  register(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    @path
    resourceProviderNamespace: string,

    @body
    properties?: ProviderRegistrationRequest,
  ): ArmResponse<Provider> | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers")
  @list
  @get
  list(
    ...ApiVersionParameter,

    @query("$expand")
    $expand?: string,

    ...SubscriptionIdParameter,
  ): ArmResponse<ProviderListResult> | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}")
  @get
  get(
    ...ApiVersionParameter,

    @query("$expand")
    $expand?: string,

    @path
    resourceProviderNamespace: string,

    ...SubscriptionIdParameter,
  ): ArmResponse<Provider> | CloudError;

  @tag("Providers")
  @route("/providers/{resourceProviderNamespace}")
  @get
  getAtTenantScope(
    ...ApiVersionParameter,

    @query("$expand")
    $expand?: string,

    @path
    resourceProviderNamespace: string,
  ): ArmResponse<Provider> | CloudError;
}

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-interface-requires-decorator" "For backward compatibility"
interface ProviderResourceTypesOperationGroup {
  @tag("Providers")
  @route("/subscriptions/{subscriptionId}/providers/{resourceProviderNamespace}/resourceTypes")
  @get
  list(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    @query("$expand")
    $expand?: string,

    @path
    resourceProviderNamespace: string,
  ): ArmResponse<ProviderResourceTypeListResult> | CloudError;
}

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-interface-requires-decorator" "For backward compatibility"
interface ResourcesOperationGroup {
  @tag("Resources")
  @route("/subscriptions/{subscriptionId}/resources")
  @get
  @list
  list(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    @query("$filter")
    $filter?: string,

    @query("$expand")
    $expand?: string,

    @query("$top")
    $top?: int32,
  ): ArmResponse<ResourceListResult> | CloudError;
}

#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-interface-requires-decorator" "For backward compatibility"
interface TagsOperationGroup {
  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Tags")
  @summary("Deletes a predefined tag value for a predefined tag name.")
  @route("/subscriptions/{subscriptionId}/tagNames/{tagName}/tagValues/{tagValue}")
  @delete
  deleteValue(
    ...ApiVersionParameter,

    @path
    tagName: string,

    @path
    tagValue: string,

    ...SubscriptionIdParameter,
  ): OkResponse | NoContentResponse | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Tags")
  @summary("Creates a predefined value for a predefined tag name.")
  @route("/subscriptions/{subscriptionId}/tagNames/{tagName}/tagValues/{tagValue}")
  @put
  createOrUpdateValue(
    ...ApiVersionParameter,

    @path
    tagName: string,

    @path
    tagValue: string,

    ...SubscriptionIdParameter,
  ): ArmResponse<TagValue> | ArmResourceCreatedSyncResponse<TagValue> | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Tags")
  @summary("Creates a predefined tag name.")
  @route("/subscriptions/{subscriptionId}/tagNames/{tagName}")
  @put
  createOrUpdate(
    ...ApiVersionParameter,

    @path
    tagName: string,

    ...SubscriptionIdParameter,
  ): ArmResponse<TagDetails> | ArmResourceCreatedSyncResponse<TagDetails> | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Tags")
  @summary("Deletes a predefined tag name.")
  @route("/subscriptions/{subscriptionId}/tagNames/{tagName}")
  @delete
  delete(
    ...ApiVersionParameter,

    @path
    tagName: string,

    ...SubscriptionIdParameter,
  ): OkResponse | NoContentResponse | CloudError;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-operation-decorator" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "FIXME: review suppression and update justification"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-interface" "non-standard operations"
  @tag("Tags")
  @summary("Gets a summary of tag usage under the subscription.")
  @route("/subscriptions/{subscriptionId}/tagNames")
  @get
  @list
  list(...ApiVersionParameter, ...SubscriptionIdParameter): ArmResponse<TagsListResult> | CloudError;
}
