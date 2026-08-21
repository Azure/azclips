import "@typespec/http";

using TypeSpec.Http;

@service
namespace ConstraintsService;

@minLength(3)
@maxLength(12)
@pattern("^[a-z]+$")
scalar ConstrainedName extends string;

@minValue(-10)
@maxValue(10)
scalar ConstrainedNumber extends int32;

model Widget {
  @minLength(0)
  @maxLength(24)
  @pattern("^[A-Z]+$")
  constrainedName: string;

  scalarConstrainedName: ConstrainedName;

  @pattern("   ")
  blankPattern: string;

  unconstrainedName: string;

  @minValue(0)
  @maxValue(100)
  constrainedNumber: int32;

  scalarConstrainedNumber: ConstrainedNumber;

  unconstrainedNumber: int32;
}

@route("/widgets/{id}")
@get
op getWidget(
  @path
  @minLength(1)
  @maxLength(10)
  @pattern("^[0-9]+$")
  id: string,

  @query
  @minValue(0)
  @maxValue(50)
  count: int32,
): Widget;