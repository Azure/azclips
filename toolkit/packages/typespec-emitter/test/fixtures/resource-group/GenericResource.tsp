import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";
import "@typespec/openapi";
import "@typespec/rest";
import "./models.tsp";
import "@azure-tools/typespec-client-generator-core";

using Azure.ClientGenerator.Core;
using TypeSpec.Rest;
using Azure.ResourceManager;
using TypeSpec.Http;
using TypeSpec.OpenAPI;

namespace Microsoft.Resources;

/**
 * Resource information.
 */
#suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "For backward compatibility"
model GenericResource is Azure.ResourceManager.Legacy.GenericResource<unknown, true> {
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  plan?: Plan;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  @pattern("^[-\\w\\._,\\(\\)]+$")
  kind?: string;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  managedBy?: string;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  sku?: Sku;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  identity?: Identity;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  location?: string;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  @visibility(Lifecycle.Read, Lifecycle.Create)
  extendedLocation?: ExtendedLocation;

  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "For backward compatibility"
  tags?: Record<string>;
}

alias genericOps = Azure.ResourceManager.Legacy.RoutedOperations<
  {},
  {
    #suppress "@azure-tools/typespec-azure-core/documentation-required" "Existing parameter from original swagger"
    @path(#{ allowReserved: true }) @key resourceId: string;

    @apiVersion(false)
    @query("api-version")
    apiVersion: string;
  },
  CloudError
>;

@armResourceOperations
interface Resources {
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  checkExistenceById is genericOps.CheckExistence<GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  getById is genericOps.Read<GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/lro-location-header" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  createOrUpdateById is genericOps.CreateOrUpdateAsync<
    GenericResource,
    Response =
      | ArmResourceUpdatedResponse<GenericResource>
      | ArmResourceCreatedResponse<
          GenericResource,
          ArmLroLocationHeader<FinalResult = GenericResource> & Azure.Core.Foundations.RetryAfterHeader
        >
      | ArmAcceptedResponse
  >;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  updateById is genericOps.CustomPatchAsync<GenericResource, GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  deleteById is genericOps.DeleteWithoutOkAsync<
    GenericResource,
    Response = ArmDeletedResponse | ArmDeleteAcceptedLroResponse | ArmDeletedNoContentResponse
  >;
}

alias specificResourcesOps = Azure.ResourceManager.Legacy.LegacyOperations<
  {
    ...SubscriptionIdParameter;

    @path
    @pattern("^[-\\w\\._\\(\\)]+$")
    @minLength(1)
    @maxLength(90)
    @segment("resourcegroups")
    resourceGroupName: string;

    @path
    @segment("providers")
    resourceProviderNamespace: string;

    @path(#{ allowReserved: true })
    parentResourcePath: string;

    @path(#{ allowReserved: true })
    resourceType: string;
  },
  {
    @path
    @key
    resourceName: string;

    @apiVersion(false)
    @query("api-version")
    apiVersion: string;
  },
  CloudError
>;

@armResourceOperations(#{ omitTags: true })
interface SpecificResources {
  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  @tag("Resources")
  checkExistence is specificResourcesOps.CheckExistence<GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  @tag("Resources")
  get is specificResourcesOps.Read<GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  #suppress "@azure-tools/typespec-azure-resource-manager/lro-location-header" "For backward compatibility"
  @tag("Resources")
  createOrUpdate is specificResourcesOps.CreateOrUpdateAsync<
    GenericResource,
    Response =
      | ArmResourceUpdatedResponse<GenericResource>
      | ArmResourceCreatedResponse<
          GenericResource,
          ArmLroLocationHeader<FinalResult = GenericResource> & Azure.Core.Foundations.RetryAfterHeader
        >
      | ArmAcceptedResponse
  >;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  @tag("Resources")
  update is specificResourcesOps.CustomPatchAsync<GenericResource, GenericResource>;

  #suppress "@azure-tools/typespec-azure-resource-manager/use-api-version" "For backward compatibility"
  @tag("Resources")
  delete is specificResourcesOps.DeleteWithoutOkAsync<
    GenericResource,
    Response = ArmDeletedResponse | ArmDeleteAcceptedLroResponse | ArmDeletedNoContentResponse
  >;
}
