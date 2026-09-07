import "@typespec/http";
import "@typespec/versioning";
import "@azure-tools/typespec-client-generator-core";

using TypeSpec.Http;
using TypeSpec.Versioning;
using Azure.ClientGenerator.Core;

namespace Shared {
  interface Readable<T> {
    @get
    @route("/{id}")
    read(@path id: string): T;
  }

  @get
  @route("/{id}")
  op fetch<T>(@path id: string): T;
}

@service(#{ title: "Interface ownership" })
@server("https://interfaces.example.com", "Endpoint")
@versioned(Versions)
@clientNamespace("Sdk.Layout")
namespace InterfaceService {
  enum Versions {
    v1: "2025-01-01",
  }

  model Widget {
    name: string;
  }

  @get
  @route("/health")
  @clientName("ping")
  @clientLocation("SharedOperations")
  op health(@query @clientName("detailed") verbose?: boolean): string;

  @route("/widgets")
  @clientName("SdkWidgets")
  interface Widgets {
    @get
    @route("/{id}")
    @clientName("fetchWidget")
    @clientLocation("SharedOperations")
    get(@path @clientName("key") id: string, @query filter?: string, @header trace?: string): Widget;
  }

  @route("/accounts")
  interface Accounts {
    @get
    @route("/{id}")
    @clientName("fetchAccount")
    @clientLocation("SharedOperations")
    get(@path id: string): Widget;
  }

  @route("/derived")
  interface DerivedWidgets extends Shared.Readable<Widget> {}

  @route("/aliases")
  interface Aliases {
    fetchAlias is Shared.fetch<Widget>;
  }
}
