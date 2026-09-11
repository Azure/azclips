import "@typespec/http";
import "@azure-tools/typespec-client-generator-core";

using TypeSpec.Http;
using Azure.ClientGenerator.Core;

/**
 * Minimal service exercising `@usage` (data-flow) and `@access` (visibility) so
 * the emitter's `usage` / `access` mapping can be verified. `getSecret` is
 * `@access(Access.internal)`, which propagates internal access to `Secret` —
 * proving ace now *emits* internal-access types/operations (carry everything)
 * instead of silently skipping them.
 */
@service(#{ title: "AccessSvc" })
@server("https://access.example.com", "AccessSvc endpoint")
namespace AccessSvc;

model Widget {
  name: string;
}

model Secret {
  token: string;
}

@route("/widgets")
@put
op put(@body widget: Widget): Widget;

@access(Access.internal)
@route("/secret")
@get
op getSecret(): Secret;
