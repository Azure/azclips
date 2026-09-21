import "@typespec/http";

using TypeSpec.Http;

@service(#{ title: "EnumSvc" })
@server("https://enum.example.com", "EnumSvc endpoint")
namespace EnumSvc;

enum ClosedEnum {
  One: "one",
  Two: "two",
}

union ClosedUnion {
  One: "one",
  Two: "two",
}

union OpenUnion {
  One: "one",
  Two: "two",
  string,
}

model EnumModel {
  closedEnum: ClosedEnum;
  closedUnion: ClosedUnion;
  openUnion: OpenUnion;
}

@route("/value")
@get
op get(): EnumModel;