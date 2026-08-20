import "@typespec/http";

using TypeSpec.Http;

@service
namespace LengthService;

@minLength(3)
@maxLength(12)
scalar ConstrainedName extends string;

model Widget {
  @minLength(0)
  @maxLength(24)
  constrainedName: string;

  scalarConstrainedName: ConstrainedName;

  unconstrainedName: string;
}

@route("/widgets/{id}")
@get
op getWidget(
  @path
  @minLength(1)
  @maxLength(10)
  id: string,
): Widget;