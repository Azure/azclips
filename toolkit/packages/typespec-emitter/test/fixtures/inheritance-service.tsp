import "@typespec/http";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Versioning;

@service(#{ title: "Inherited property provenance" })
@server("https://inheritance.example.com", "Endpoint")
@versioned(Versions)
namespace InheritanceService;

enum Versions {
  v1: "2025-01-01",
}

model Base {
  value: string;
  baseOnly: string;
}

model Middle extends Base {
  value: "fixed";
  middleOnly: string;
}

model Leaf extends Middle {
  leafOnly: string;
}

model OverrideLeaf extends Middle {
  value: "fixed";
}

@route("/leaf")
@get
op getLeaf(): Leaf;

@route("/override-leaf")
@get
op getOverrideLeaf(): OverrideLeaf;
