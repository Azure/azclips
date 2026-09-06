import "@typespec/http";

using TypeSpec.Http;

/**
 * Minimal service exercising `@summary` alongside `@doc` (doc-comment) so the
 * emitter's separate `summary` / `docs` mapping can be verified. Every element
 * gives `@summary` *different* text from its long-form doc, proving both
 * survive independently (before item J, only the doc survived).
 */
@service(#{ title: "SummarySvc" })
@server("https://summary.example.com", "SummarySvc endpoint")
namespace SummarySvc;

/**
 * A widget is a long-form described thing with many nuances.
 */
@summary("A widget.")
model Widget {
  /**
   * The unique widget identifier, assigned by the service on create.
   */
  @summary("Widget id.")
  id: string;

  // Only a doc-comment, no @summary -> summary omitted, docs present.
  /** The display name. */
  name: string;
}

/**
 * Retrieve a single widget by its identifier from the widget store.
 */
@summary("Get a widget.")
@route("/widgets/{id}")
@get
op get(@path id: string): Widget;
