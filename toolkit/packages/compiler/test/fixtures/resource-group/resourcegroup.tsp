// Minimal, hand-authored TypeSpec fixture mimicking the real Microsoft.Resources
// ResourceGroups shape (get/createOrUpdate/delete/list) — chosen specifically
// because a real hand-written comparison exists in azclips itself:
// src/PS/Resources/Cmdlets/{New,Get}ResourceGroupCmdlet.cs +
// src/Extensions/Resources/Processors/*ResourceGroupProcessor.cs.
//
// Deliberately NOT built on @azure-tools/typespec-azure-resource-manager's
// resource-modeling decorators — this is a small, explicit spec so its
// resulting IR shape (operation names, HTTP methods, namespace) stays fully
// predictable for the create-ps-commands/derivePsCommands proof in
// create-ps-commands.e2e.test.ts.

import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;

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
  @pageItems value: ResourceGroup[];
  @nextLink nextLink?: url;
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
