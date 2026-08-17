// Minimal, hand-authored TypeSpec fixture mimicking the real Microsoft.Resources
// ResourceGroups shape (get/createOrUpdate/delete/list) — chosen specifically
// because a real hand-written comparison exists in azclips itself:
// src/PS/Resources/Cmdlets/{New,Get}ResourceGroupCmdlet.cs +
// src/Extensions/Resources/Processors/*ResourceGroupProcessor.cs.
//
// Deliberately NOT built on @azure-tools/typespec-azure-resource-manager's
// resource-modeling decorators — this is a small, explicit spec so its
// resulting IR shape (operation names, HTTP methods, namespace) stays fully
// predictable for the create-commands/deriveCommands proof in
// create-commands.e2e.test.ts.

import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@service(#{ title: "Resources" })
@server("https://management.azure.com", "ARM endpoint")
@versioned(Versions)
namespace Microsoft.Resources;

enum Versions {
  v2025_04_01: "2025-04-01",
}

model ResourceGroupProperties {
  provisioningState?: string;
}

model ResourceGroup {
  @key name: string;
  location: string;
  properties?: ResourceGroupProperties;
}

model ResourceGroupListResult {
  value: ResourceGroup[];
}

@route("/subscriptions/{subscriptionId}/resourcegroups")
interface ResourceGroups {
  @get list(@path subscriptionId: string): ResourceGroupListResult;

  @route("/{resourceGroupName}")
  @get
  get(@path subscriptionId: string, @path resourceGroupName: string): ResourceGroup;

  @route("/{resourceGroupName}")
  @put
  createOrUpdate(
    @path subscriptionId: string,
    @path resourceGroupName: string,
    @body resourceGroup: ResourceGroup,
  ): ResourceGroup;

  @route("/{resourceGroupName}")
  @delete
  delete(@path subscriptionId: string, @path resourceGroupName: string): void;
}
