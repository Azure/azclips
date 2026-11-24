import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

@service
@versioned(Versions)
@server("https://example.com", "Service endpoint")
namespace NoArm;

enum Versions {
  v2024: "2024-01-01",
}

@route("/items")
@get
op read(@query("api-version") apiVersion: Versions): { message: string };
