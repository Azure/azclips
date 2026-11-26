import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.ResourceManager;

@armProviderNamespace
@service
@versioned(Versions)
namespace Test.ScopeRepro;

enum Versions { current: "2025-07-01" }

model WidgetProperties {
  displayName?: string;
}

model StandardWidget is TrackedResource<WidgetProperties> {
  ...ResourceNameParameter<
    StandardWidget,
    KeyName = "standardWidgetName",
    SegmentName = "standardWidgets"
  >;
}

@armResourceOperations
interface StandardWidgets {
  get is ArmResourceRead<StandardWidget>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<StandardWidget>;
}

model GenericWidget is Azure.ResourceManager.Legacy.GenericResource<WidgetProperties> {
  @visibility(Lifecycle.Read)
  @path
  @key("genericWidgetName")
  @segment("genericWidgets")
  name: string;
}

alias GenericWidgetOps = Azure.ResourceManager.Legacy.LegacyOperations<
  {
    ...ApiVersionParameter;
    ...SubscriptionIdParameter;
    ...ResourceGroupParameter;
    ...Azure.ResourceManager.Legacy.Provider;
  },
  {
    @path
    @key
    @segment("genericWidgets")
    genericWidgetName: string;
  }
>;

@armResourceOperations
interface GenericWidgets {
  get is GenericWidgetOps.Read<GenericWidget>;
  createOrUpdate is GenericWidgetOps.CreateOrUpdateAsync<GenericWidget>;
}
