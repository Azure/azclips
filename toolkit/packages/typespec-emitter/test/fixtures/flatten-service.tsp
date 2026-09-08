import "@typespec/http";
import "@azure-tools/typespec-client-generator-core";

using TypeSpec.Http;
using Azure.ClientGenerator.Core;

/**
 * Minimal service exercising `@Legacy.flattenProperty` so the emitter's flatten
 * mapping (IRProperty.rendering.flatten) can be verified without the ARM
 * library. The `"!javascript"` scope mirrors ElasticSan and also pins ace's
 * TCGC scope resolution: ace resolves to `engine`, which is *not* javascript,
 * so the negation scope applies and TCGC surfaces `flatten=true`.
 */
@service(#{ title: "FlatSvc" })
@server("https://flat.example.com", "FlatSvc endpoint")
namespace FlatSvc;

model WidgetProperties {
  color: string;
  size: int32;
}

model Widget {
  name: string;
  properties: WidgetProperties;
}

@route("/widgets/{name}")
@get
op get(@path name: string): Widget;

@@Azure.ClientGenerator.Core.Legacy.flattenProperty(Widget.properties, "!javascript");
