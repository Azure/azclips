import "@typespec/http";

using TypeSpec.Http;

/**
 * Minimal service that declares authentication directly via `@useAuth`, so the
 * emitter's `authFrom` mapping (IRSource.auth) can be exercised without pulling
 * in the full ARM library. (The oauth2 scope-flattening path is covered against
 * the real ElasticSan ARM spec; this keeps the committed test lightweight.)
 */
@service(#{ title: "AuthSvc" })
@server("https://auth.example.com", "AuthSvc endpoint")
@useAuth(ApiKeyAuth<ApiKeyLocation.header, "x-api-key">)
namespace AuthSvc;

@route("/ping")
op ping(): void;
