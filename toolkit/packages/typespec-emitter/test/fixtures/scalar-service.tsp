import "@typespec/http";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using Azure.Core;

/**
 * Minimal service exercising Azure.Core semantic string scalars so the emitter's
 * format mapping can be verified. These scalars `extends string`, so TCGC
 * flattens them to `kind: "string"`; ace recovers the semantic from the scalar's
 * crossLanguageDefinitionId and stamps the IR `format` hint.
 */
@service(#{ title: "ScalarSvc" })
@server("https://scalar.example.com", "ScalarSvc endpoint")
namespace ScalarSvc;

model Thing {
  resourceId: armResourceIdentifier;
  resourceIds: armResourceIdentifier[];
  correlationId: uuid;
  address: ipV4Address;
  address6: ipV6Address;
  plain: string;
}

@route("/things")
@get
op get(): Thing;
