import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.ResourceManager;

@service(#{ title: "Scoped deployment contract fixture" })
@armProviderNamespace
@versioned(Versions)
namespace DeploymentScoped;

enum Versions { v2024: "2024-01-01" }

@minLength(3)
scalar LimitedText extends string;

model BaseSettings {
  @secret
  password?: string;
}

model Settings extends BaseSettings {
  @minItems(1)
  values?: LimitedText[];
  labels?: Record<LimitedText>;
  next?: Settings;
}

model WidgetProperties {
  settings?: Settings;

  @visibility(Lifecycle.Read)
  @encode(string)
  computed?: int64;
}

model Widget extends Azure.ResourceManager.CommonTypes.Resource {
  @visibility(Lifecycle.Read)
  @key("widgetName")
  @segment("widgets")
  @path
  name: string;
  properties?: WidgetProperties;
}

model OtherProperties {
  @encode(string)
  value?: int64;
}

model OtherWidget extends Azure.ResourceManager.CommonTypes.Resource {
  @visibility(Lifecycle.Read)
  @key("otherName")
  @segment("others")
  @path
  name: string;
  properties?: OtherProperties;
}

@armResourceOperations
interface Widgets {
  @armResourceCreateOrUpdate(Widget)
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/DeploymentScoped/widgets/{widgetName}")
  @put
  createOrUpdate(
    ...ApiVersionParameter,
    @path subscriptionId: string,
    @path resourceGroupName: string,
    @path widgetName: string,
    @bodyRoot body: Widget,
  ): Widget;
}

@armResourceOperations
interface Others {
  @armResourceCreateOrUpdate(OtherWidget)
  @route("/subscriptions/{subscriptionId}/providers/DeploymentScoped/others/{otherName}")
  @put
  createOrUpdate(
    ...ApiVersionParameter,
    @path subscriptionId: string,
    @path otherName: string,
    @bodyRoot body: OtherWidget,
  ): OtherWidget;
}
