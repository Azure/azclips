import "@typespec/http";

using TypeSpec.Http;

/**
 * Minimal service with a path parameter that allows reserved characters (a scope
 * such as `subscriptions/{id}/resourceGroups/{name}`) beside an ordinary one. An
 * explicit route spells that as RFC 6570 reserved expansion, `{+scope}`.
 */
@service(#{ title: "PathSvc" })
@server("https://path.example.com", "PathSvc endpoint")
namespace PathSvc;

model Thing {
  name: string;
}

@route("/{+scope}/things/{name}")
@get
op get(@path scope: string, @path name: string): Thing;
