import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;
using Azure.ResourceManager;

@service(#{ title: "Deployment contract fixture" })
@armProviderNamespace
@versioned(Versions)
namespace DeploymentService;

enum Versions {
  v2024: "2024-01-01",
  v2025: "2025-01-01",
}

model WidgetProperties {
  @encodedName("application/json", "display_name")
  @minLength(2)
  @maxLength(30)
  displayName?: string;

  @secret
  @minLength(8)
  @maxLength(128)
  password?: string;

  @visibility(Lifecycle.Read)
  status?: string;

  @added(Versions.v2025)
  newerOnly?: string;
}

model Widget is TrackedResource<WidgetProperties> {
  @key("widgetName")
  @segment("widgets")
  @path
  name: string;
}

@parentResource(Widget)
model Child is ProxyResource<WidgetProperties> {
  @key("childName")
  @segment("children")
  @path
  name: string;
}

@subscriptionResource
model Account is TrackedResource<WidgetProperties> {
  @key("accountName")
  @segment("accounts")
  @path
  name: string;
}

@armResourceOperations
interface Widgets {
  get is ArmResourceRead<Widget>;
  createOrUpdate is ArmResourceCreateOrReplaceSync<Widget>;
}

@armResourceOperations
interface Children {
  get is ArmResourceRead<Child>;
  createOrUpdate is ArmResourceCreateOrReplaceSync<Child>;
}

@armResourceOperations
interface Accounts {
  get is ArmResourceRead<Account>;
  createOrUpdate is ArmResourceCreateOrReplaceSync<Account>;
}
