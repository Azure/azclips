import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.ResourceManager;

@service(#{ title: "Legacy deployment contract fixture" })
@armProviderNamespace
@versioned(Versions)
namespace DeploymentLegacy;

enum Versions {
  v2024: "2024-01-01",
  v2025: "2025-01-01",
}

model WidgetProperties {
  @minLength(2)
  displayName?: string;

  @added(Versions.v2025)
  newerOnly?: string;
}

model Widget extends Azure.ResourceManager.CommonTypes.Resource {
  @visibility(Lifecycle.Read)
  @key("widgetName")
  @segment("widgets")
  @path
  name: string;

  location: string;
  properties?: WidgetProperties;
}

@armResourceOperations
interface Widgets {
  @armResourceCreateOrUpdate(Widget)
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/DeploymentLegacy/widgets/{widgetName}")
  @put
  createOrUpdate(
    ...ApiVersionParameter,
    @path subscriptionId: string,
    @path resourceGroupName: string,
    @path widgetName: string,
    @bodyRoot body: Widget,
  ): Widget;
}
