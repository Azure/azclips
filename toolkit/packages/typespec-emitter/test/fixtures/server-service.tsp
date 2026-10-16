import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

@service(#{ title: "Shared servers" })
@server("https://{region}.example.com", "Regional endpoint", { region: string = "west" })
@server("https://backup.example.com", "Backup endpoint")
@versioned(Versions)
namespace ServerService;

enum Versions {
  v1: "2025-01-01",
}

@route("/first")
@get
op first(): string;

@route("/second")
@get
op second(): string;
