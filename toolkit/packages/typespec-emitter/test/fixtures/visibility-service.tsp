import "@typespec/http";

using TypeSpec.Http;

/**
 * Minimal service exercising lifecycle `@visibility(...)` so the emitter's
 * phase mapping (IRProperty.visibility) can be verified without the ARM
 * library. Every property keeps `Read` visibility so it survives the GET
 * (Read) response context; the phase set the emitter records is the property's
 * *declared* lifecycle visibility, independent of that context.
 */
@service(#{ title: "VisSvc" })
@server("https://vis.example.com", "VisSvc endpoint")
namespace VisSvc;

model Widget {
  @visibility(Lifecycle.Read)
  id: string;

  @visibility(Lifecycle.Read, Lifecycle.Create)
  size: int32;

  // No @visibility -> visible in every lifecycle phase -> omitted from IR.
  label: string;
}

@route("/widgets/{id}")
@get
op get(@path id: string): Widget;
