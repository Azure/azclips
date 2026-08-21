import "@typespec/http";

using TypeSpec.Http;

@service
namespace ConstraintsService;

@minLength(3)
@maxLength(12)
@pattern("^[a-z]+$")
scalar ConstrainedName extends string;

model Widget {
  @minLength(0)
  @maxLength(24)
  @pattern("^[A-Z]+$")
  constrainedName: string;

  scalarConstrainedName: ConstrainedName;

  @pattern("   ")
  blankPattern: string;

  unconstrainedName: string;
}

@route("/widgets/{id}")
@get
op getWidget(
  @path
  @minLength(1)
  @maxLength(10)
  @pattern("^[0-9]+$")
  id: string,
): Widget;