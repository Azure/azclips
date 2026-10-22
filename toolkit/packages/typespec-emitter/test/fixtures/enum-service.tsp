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

enum Level {
  Low: 1,
  High: 2,
}

enum Big {
  Huge: 3000000000,
}

enum Weight {
  Light: 0.5,
  Heavy: 2,
}

union Priority {
  int64,
  Low: 1,
  High: 2,
}

union Ratio {
  float,
  Half: 0.5,
  Double: 2,
}

enum Precise {
  Fine: 1.23456789,
}

enum Tiny {
  Small: 1e-7,
}

enum Vast {
  Far: 1e21,
}

model EnumModel {
  closedEnum: ClosedEnum;
  closedUnion: ClosedUnion;
  openUnion: OpenUnion;
  level: Level;
  big: Big;
  weight: Weight;
  priority: Priority;
  ratio: Ratio;
  precise: Precise;
  tiny: Tiny;
  vast: Vast;
}

@route("/value")
@get
op get(): EnumModel;